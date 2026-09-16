# Telemetry (Milestone 5)

M5 turns the runtime correlation token introduced in M4 (`RuntimeSessionId`) into first-class QA
telemetry: a ForgeQA-enabled game can start a session, emit structured events throughout play, and
a developer can replay exactly what happened — in order — from the web dashboard.

```text
Unreal Runtime                          ForgeQA API                    ForgeQA Web
    |                                        |                              |
    | start session (RuntimeSessionId,       |                              |
    |  BuildId, environment)                 |                              |
    |--------------------------------------->| verify Build ∈ Project       |
    |                                        | upsert TelemetrySession      |
    |                                        |                              |
    | TrackEvent() x N  (local queue)        |                              |
    |                                        |                              |
    | flush: batch of queued events -------->| validate batch as a whole    |
    |                                        | insert new TelemetryEvents   |
    |                                        | (duplicates by SequenceNumber|
    |                                        |  are no-ops, not errors)     |
    |                                        |                              |
    | end session ---------------------------->| set EndedAt                |
    |                                        |                              |
    |                                        |<---------- GET sessions -----|
    |                                        |<---------- GET events -------| event timeline
```

## Scope boundary

M5 is **event telemetry**, not a profiler. Explicitly out of scope: continuous FPS/frame-time
sampling, CPU/GPU/memory profiling dashboards, crash reporting, stack symbolication, session
replay, video/input recording, heatmaps, AI analysis or anomaly detection, funnels/retention/
business analytics, player accounts, a public SDK ecosystem, and any dedicated
analytics/warehouse infrastructure (Kafka, ClickHouse, Elasticsearch, BigQuery, ...). Telemetry
events live in the same PostgreSQL database as everything else in ForgeQA; there is no second
datastore in M5.

## Domain model

### TelemetrySession

Belongs to exactly one Project and exactly one Build — both required and immutable after
construction. `RuntimeSessionId` is the client-generated correlation token from M4
(`UForgeQASubsystem::GetRuntimeSessionId()`); the database's own `Id` is the canonical internal
identifier, but a runtime session is always addressed over HTTP by `RuntimeSessionId`, since that's
the value the client actually holds. **`(ProjectId, RuntimeSessionId)` is unique** — a runtime
retry can never create two session rows for the same play session.

| Field | Notes |
| --- | --- |
| `StartedAt` | Set once, at construction |
| `EndedAt` | Null while active; set by the end-session call; never automatically timed out |
| `LastEventAt` | Updated once per *accepted batch*, not once per event |
| `Environment` | An owned value object, embedded in the row — see below |

A session with no `EndedAt` (e.g. the game crashed or was force-quit) simply stays "active"
forever — M5 has no idle-timeout sweep. This is an explicit, accepted tradeoff, not an oversight.

### Why TelemetrySession doesn't duplicate Build metadata

The conceptual model lists `gameVersion`/`buildNumber`/`branch`/`commitSha`/`platform`/
`configuration` as session-level context, but all of those already exist on the correlated `Build`
entity. Rather than snapshot them redundantly, `TelemetryService` joins the Build (batched via
`IBuildRepository.GetByIdsAsync`, the same N+1-avoidance pattern used by `BugService`) whenever a
session response needs them. `TelemetrySessionEnvironment` (the owned type actually stored on the
row) only captures what a Build record *cannot* supply and is genuinely a point-in-time snapshot:
`MapName`, `GameMode`, `Platform`, `Configuration`, `EngineVersion`, `OsVersion`, `Locale`. (Yes,
`Platform`/`Configuration` are also captured here even though the Build has its own — this is a
deliberate, minor duplication: the *session's* reported platform/configuration is what the running
client actually observed, which is a useful sanity check against the *Build's* declared
platform/configuration, and keeping both is cheap.) No CPU/GPU/memory fields — those add little
value for event telemetry and would just grow the row for no querying benefit in M5.

### TelemetryEvent

Immutable, append-only. `SequenceNumber` is assigned by the client (not the server) and is unique
within its session — this is what lets the timeline UI (and any future retry-tolerant client) know
the true order of events even when:

- batches arrive out of order over the network,
- the same batch is retried after a timeout,
- client and server clocks disagree (`ClientTimestamp` is never trusted as server truth —
  `ReceivedAt` is recorded independently on every event, always).

`EventName` is a free-form string (never a backend enum — instrumentation must remain extensible
without a deploy), constrained to 1–128 characters of letters, digits, `.`, `_`, `-`.
`Properties` is `jsonb`: arbitrary JSON-compatible structured data, validated only for "is this
actually a JSON object" and "is it under the configured size limit" — never an
Entity-Attribute-Value table, never a stringified blob parsed ad hoc.

## Ingestion architecture

### Session start (idempotent)

```http
POST /api/projects/{projectId}/telemetry/sessions
```

Requires a Project API key with `TELEMETRY_WRITE`. Resubmitting the same
`(ProjectId, RuntimeSessionId, BuildId)` returns the existing session rather than creating a
duplicate — this makes a client-side retry after a dropped response completely safe. Resubmitting
the same `RuntimeSessionId` with a **different** `BuildId` is rejected (`409 Conflict`) — a runtime
session's identity is never silently mutated.

### Event batching

```http
POST /api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events
```

One HTTP request carries many events (`MaxEventsPerBatch`, default 100). The whole batch is
validated *before* anything is written — one malformed event fails the entire batch (`400`) rather
than partially applying it, which is easier for a client to reason about on retry (`Telemetry`
options in `TelemetryOptions.cs`, section `Telemetry` in configuration):

```text
MaxEventsPerBatch = 100
MaxBatchBytes = 1 MiB
MaxEventPropertiesBytes = 32 KiB
```

A batch above `MaxBatchBytes` is rejected at the controller with `413 Payload Too Large` before
the service layer does any work.

**Idempotency**: sequence numbers already present for the session are treated as duplicates, not
errors — the response reports `{ accepted, duplicates }` so a client can log/verify without ever
needing to inspect individual event IDs. Persistence is a single batched `AddRange` +
`SaveChanges`, preceded by one query for already-existing sequence numbers in the target range —
never one `SaveChanges` per event.

**Ended sessions reject new events** (`409 Conflict`) — once `EndSession` has been called, later
events are treated as a client lifecycle bug, not queued for late delivery. This is why the Unreal
client always flushes *before* calling end, never after (see below).

### Session end (idempotent)

```http
POST /api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/end
```

Sets `EndedAt` if unset; repeating the call is a no-op that returns the same success response.

## Security

- **`TELEMETRY_WRITE`** is a new `ProjectApiKeyScope` (alongside M4's `BUG_REPORT_WRITE`). A key
  may hold either scope, both, or neither; scopes are never granted implicitly. All three
  ingestion endpoints require `TELEMETRY_WRITE` specifically — a key with only `BUG_REPORT_WRITE`
  is rejected (`403`), and vice versa for the bug-reporting endpoints.
  - **Fixing a real M4 gap while adding this**: M4's `BugService` verified only that an API key
    belonged to the target Project, never that it actually held `BUG_REPORT_WRITE` — harmless while
    that was the only scope in existence, but it meant a key minted with *only* `TELEMETRY_WRITE`
    could still submit bug reports once a second scope existed. `BugReportAuthor` now carries the
    caller's scopes and `BugService.AuthorizeAsync` checks `BUG_REPORT_WRITE` explicitly. Covered
    by `Key_With_Only_TelemetryWrite_Cannot_Submit_Bug` in `TelemetryEndpointsTests.cs`.
- **Ingestion is machine-oriented only.** Unlike Bug Reporting's dual-scheme endpoints, telemetry
  ingestion accepts *only* the `ForgeQAProjectKey` scheme — there is no JWT path for starting/
  ingesting/ending a session. Dashboard users only ever query telemetry (`[Authorize]`, JWT-only).
- **Rate limiting**: a separate, more permissive policy (`TelemetryIngestionPolicy`,
  `MaxBatchesPerMinutePerKey` = 120 by default) than Bug Reporting's, partitioned by Project API
  key ID — telemetry's expected volume is much higher than bug reports.
- **Privacy**: never automatically captured — real names, email, IP as event metadata, filesystem
  paths, OS usernames, environment variables, hardware serial numbers. Telemetry is for QA/
  development, not invasive tracking.
- **`ProjectApiKey.LastUsedAt`** updates once per ingestion *request*, reusing the exact mechanism
  from M4 — never once per event.

## Backend endpoints

| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| `POST` | `/api/projects/{projectId}/telemetry/sessions` | Project key (`TELEMETRY_WRITE`) | Idempotent session start |
| `POST` | `/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events` | Project key (`TELEMETRY_WRITE`) | Batch event ingestion |
| `POST` | `/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/end` | Project key (`TELEMETRY_WRITE`) | Idempotent session end |
| `GET` | `/api/projects/{projectId}/telemetry/sessions` | JWT | Paginated, filtered session list |
| `GET` | `/api/projects/{projectId}/telemetry/sessions/{sessionId}` | JWT | Full session detail |
| `GET` | `/api/projects/{projectId}/telemetry/sessions/{sessionId}/events` | JWT | Paginated event timeline |

Note the addressing asymmetry: the three ingestion endpoints are keyed by `runtimeSessionId`
(the only identifier the client actually has), while the three query endpoints are keyed by the
database `sessionId` (obtained from the list response) — this exactly matches the milestone's own
specified endpoint shapes.

### Session list filters

`buildId`, `from`/`to` (a `StartedAt` range), `runtimeSessionId`, `activeOnly` (true = `EndedAt`
is null, false = `EndedAt` is set). Event count per session is computed via a correlated SQL
aggregate in `TelemetryRepository.QueryAsync` — never by loading each session's full `Events`
collection into memory.

### Event list filtering

`eventName` is a `StartsWith` prefix filter (e.g. `weapon.` matches `weapon.fired` and
`weapon.reloaded`), translated directly into a SQL `LIKE` — no in-memory filtering, no full
wildcard grammar. JSON-property filtering is explicitly deferred past M5.

## Database

- Tables: `TelemetrySessions`, `TelemetryEvents`.
- `TelemetrySessions.BuildId → Builds` is `ON DELETE RESTRICT` — telemetry must never be lost
  because a Build is later archived (a Build is never hard-deleted; this is a documented safety
  net, same rationale as `BugReports.BuildId`).
- `TelemetryEvents.TelemetrySessionId → TelemetrySessions` is `ON DELETE CASCADE` — an event has no
  meaning outside its session.
- Unique constraints: `(ProjectId, RuntimeSessionId)` on sessions, `(TelemetrySessionId,
  SequenceNumber)` on events — both are the backstop behind the idempotency guarantees above, not
  just a performance index.
- `TelemetryEvents.Properties` is `jsonb`. No GIN index in M5 — nothing in this milestone queries
  into the JSON body itself; add one only once a real query pattern needs it.
- Telemetry rows are immutable and there is no user-facing deletion endpoint in M5. Retention is
  acknowledged as a real future concern (telemetry can grow quickly) but no automated deletion
  policy exists yet — see Known issues in the milestone report.

## Unreal integration

### `UForgeQATelemetrySubsystem`

A separate `UGameInstanceSubsystem` from both `UForgeQASubsystem` (identity/Build Context only)
and `UForgeQABugReportingSubsystem` (bug submission only) — telemetry has its own lifecycle
(started once per `GameInstance`, running continuously) and shouldn't grow either of the other two
into a God object. It reuses, never duplicates:

- **Build Context and `RuntimeSessionId`** from `UForgeQASubsystem`.
- **The Project API key** from `FForgeQARuntimeCredentials::ResolveApiKey()` (command-line →
  env var → dev-settings, exactly as M4 established) — the same key may carry both
  `BUG_REPORT_WRITE` and `TELEMETRY_WRITE`; there is no second, telemetry-specific credential.
- **HTTP transport and JSON parsing** via the same `FForgeQAApiClient` /
  `FForgeQAApiResponseParser` split established in M3/M4, extended with
  `StartTelemetrySession`/`SendTelemetryEvents`/`EndTelemetrySession` and their DTOs.

### Lifecycle

```text
Initialize()
  └─ if EnableTelemetry && Build Context valid && API key resolvable:
       StartTelemetry() — fires the session-start request and returns immediately;
                           never blocks game startup waiting on the network.
       events tracked before the session is confirmed remain safely queued.

TrackEvent(name, properties)
  └─ validate name locally (same rule as the backend) → drop + log if invalid
  └─ if queue is full: drop the OLDEST queued event, increment DroppedEventCount, log (throttled)
  └─ assign SequenceNumber, timestamp, enqueue
  └─ if queue size ≥ TelemetryBatchSize: trigger a flush
  (never touches the network synchronously — safe to call from gameplay-critical paths)

Every TelemetryFlushIntervalSeconds: TryFlush() (the other flush trigger)
  └─ at most one flush in flight at a time — a batch queued mid-flight waits for the next call
  └─ on success: continue with any further queued batch
  └─ on transient failure (network / timeout / 5xx / 429): bounded exponential backoff
     (1s, 2s, 4s, 8s, capped 30s, with jitter), batch re-queued at the front, retried later
  └─ on permanent failure (400 / 401 / 403) or retries exhausted: log an error and drop that
     batch — one bad/unauthorized batch never wedges telemetry forever

Deinitialize() / EndTelemetry()
  └─ enqueue "forgeqa.session.ended", trigger a flush, fire the end-session request
  └─ best-effort only: none of this is awaited synchronously, so shutdown is never blocked
     waiting on the network — losing the last few events on an abrupt process exit is accepted
```

Automatic baseline events: `forgeqa.session.started` (enqueued right after the session-start
response confirms) and `forgeqa.session.ended` (enqueued right before ending). Nothing else is
auto-instrumented — ForgeQA telemetry stays explicit developer instrumentation.

### Blueprint & C++ API

```text
Start ForgeQA Telemetry        Is ForgeQA Telemetry Available
Track ForgeQA Event            Get ForgeQA Queued/Dropped Event Count
Track ForgeQA Breadcrumb       Was Last ForgeQA Telemetry Flush Successful
Flush ForgeQA Telemetry        Is ForgeQA Telemetry Session Started
End ForgeQA Telemetry
```

`Track ForgeQA Event`/`Track ForgeQA Breadcrumb` take a Blueprint-friendly
`FForgeQATelemetryProperties` (three flat maps: `StringProperties`/`NumberProperties`/
`BoolProperties` — deliberately not a generic property type system). `TrackBreadcrumb` is a plain
alias for `TrackEvent` with `Category = "breadcrumb"`: a breadcrumb is telemetry, not a second
model. C++ callers needing full JSON flexibility (nested objects/arrays) use the C++-only
`TrackEvent(EventName, TSharedPtr<FJsonObject>, ...)` overload instead — never exposed to
Blueprint.

### Configuration (`UForgeQASettings`, Project Settings > Plugins > ForgeQA > Telemetry)

```text
Enable Telemetry (default true — not compiled out of Shipping; a developer may want telemetry
                   from an external playtest build)
Flush Interval (seconds)   default 5
Batch Size                 default 50
Max Queued Events          default 1000
```

## Bug ↔ telemetry correlation

A `BugReport` already stores `RuntimeSessionId` (M4). The web dashboard's Bug Detail page looks up
`GET /telemetry/sessions?runtimeSessionId={id}` for the bug's Project and, if a match exists,
renders a **"View telemetry session →"** link. This is a query-time lookup only — there is no
stored foreign key from `BugReport` to `TelemetrySession`, and no telemetry data is duplicated onto
the bug. `ProjectId + RuntimeSessionId` is a sufficient, already-unique correlation key.

## Event and property naming guidance

Event names: `domain.action`, stable and machine-readable — never a localized display string.

```text
player.spawned         weapon.fired            match.started
player.died            ability.activated       ui.menu_opened
qa.checkpoint
```

Properties: `camelCase` keys.

```json
{ "weaponId": "rifle_a", "ammoRemaining": 23, "isAiming": true }
```

## Tests

- **Backend unit**: `TelemetrySessionTests`, `TelemetryEventTests` — construction invariants,
  `RecordEventsReceived`/`End` idempotency, event-name validation, sequence-number positivity.
- **Backend integration** (`TelemetryEndpointsTests.cs`, 24 tests): scope enforcement (both
  directions, including the M4 gap fix above), cross-project key/session IDOR, Build correlation
  (foreign/unknown/archived), idempotent session start and its build-mismatch rejection, batch
  validation (count limit, duplicate-in-batch rejection, invalid event name, oversized properties),
  cross-batch duplicate handling, ended-session rejection, idempotent session end, list/detail/
  events queries and their filters.
- **Frontend**: `TelemetryPage` (list/empty/error/active-ended labels/filter), 
  `TelemetrySessionDetailPage` (session/build/environment/event timeline/pagination/empty/error),
  `BugDetailPage` telemetry-correlation link (shown only when a matching session exists), API-key
  scope UI (`TELEMETRY_WRITE` selectable and displayed).
- **Unreal**: automation test source for event-name validation, retry classification, queue
  overflow (drop-oldest) and per-event queuing behavior (via `NewObject` — no live `GameInstance`
  needed since queue logic never touches the network directly), and DTO JSON serialization/parsing
  for session-start/event-batch/session-end. Credential resolution reuse is already covered by
  M4's `ForgeQARuntimeCredentialsTests.cpp` and is not duplicated here. **Compilation and execution
  are NOT EXECUTED** — no Unreal Engine installation exists in this environment.
