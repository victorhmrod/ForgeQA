# Performance (Milestone 6)

M6 extends M5's telemetry pipeline with `PerformanceSample` — lightweight, periodically-collected
runtime performance signals attached to the same `TelemetrySession` that already carries a Build's
telemetry events. This is **not** a profiler: ForgeQA samples once per configured interval (default
1 second), never per frame, and never performs per-function/per-draw-call instrumentation, render
graph inspection, or trace capture. The goal is Build-level regression visibility, session-level
trends, and map-level context a QA team can act on — not an Unreal Insights replacement.

```text
Organization
└── Project
    └── Build
        └── TelemetrySession
            ├── TelemetryEvent      (M5)
            └── PerformanceSample   (M6)
```

## Scope boundary

Explicitly out of scope for M6: per-function CPU profiling, per-draw-call GPU profiling, render
graph inspection, asset-level memory attribution, stat-dump/trace-file ingestion, flame graphs,
network packet capture, any GPU vendor profiler integration (PIX/RenderDoc/Nsight), crash
reporting, symbolication, AI/ML anomaly detection, automated CI performance gates, benchmark
orchestration, device farms, heatmaps, and session replay. Those are separate, later concerns.

## Domain model

### PerformanceSample

Belongs to exactly one `TelemetrySession` (never a second, unrelated session concept) — reusing
M5's `Project`/`Build`/`RuntimeSessionId`/`TelemetrySession` relationship rather than duplicating
identity onto every sample. Immutable and append-only, mirroring `TelemetryEvent`'s conventions
exactly: `SequenceNumber` is client-assigned and unique **within its own sequence space** (a
session's telemetry events and performance samples are numbered independently — reusing
`TelemetryEvent`'s sequence numbers was considered and rejected, since the two are unrelated
streams with different production rates); `ClientTimestamp` is never trusted as server truth alone,
so `ReceivedAt` is always recorded independently.

### Metrics: implemented vs. optional vs. not implemented

| Metric | Status | Semantics |
| --- | --- | --- |
| `fps` | **Implemented, required** | Frames observed during the sampling interval divided by elapsed time — a short-interval average, not a single-frame instantaneous value. Floating-point, never integer-rounded. |
| `frameTimeMs` | **Implemented, required** | Average frame duration during the sampling interval, in milliseconds — the primary signal; more analytically useful than FPS alone (see Percentiles below). |
| `memoryUsedBytes` | **Implemented, required** | `FPlatformMemory::GetStats().UsedPhysical` on the Unreal side — the **process's** used physical memory as reported by the platform abstraction layer. Documented precisely here so it is never confused with total system memory or virtual/committed memory. |
| `gameThreadTimeMs`, `renderThreadTimeMs`, `gpuTimeMs` | **Implemented, optional** | Sourced from the same engine-maintained globals (`GGameThreadTime`, `GRenderThreadTime`, `GGPUFrameTime`, all microseconds) that back Unreal's built-in "stat unit" HUD — updated every frame regardless of whether that overlay is visible, so reading them adds no extra instrumentation cost. **Not independently verified against a real UE 5.8 build in this environment** (see Known limitations) — nullable by design, so if a future engine version renames or removes these, the correct fix is to mark them unavailable, not to fake a value. |
| `memoryAvailableBytes`, `cpuUtilizationPercent`, `gpuUtilizationPercent`, `drawCalls`, `playerCount`, `pingMs` | **Domain supports them; Unreal client does not currently populate them** | The backend entity, validation, and storage fully support these fields (see below) so a future client change can start sending them without a schema migration, but M6's Unreal subsystem does not collect them — they were judged unreliable-to-collect-cheaply or not yet needed for the Build-vs-Build/QA questions M6 targets. Never sent as `0`; simply omitted. |

Every optional metric is nullable end-to-end: the Domain entity stores `null` (never a sentinel
zero), the ingestion DTO fields are nullable, the Unreal wire format omits the JSON key entirely
when unavailable (see `FForgeQAPerformanceSample`'s `bHas*` flags), and every aggregate (`AVG`,
`MIN`, `MAX`, percentiles) naturally ignores nulls without special-casing.

### Validation

The `PerformanceSample` constructor rejects: a non-positive `SequenceNumber`, a default
`ClientTimestamp`, `NaN`/`Infinity` in any numeric field, a negative `fps`/`frameTimeMs`/thread-
timing/`gpuTimeMs`/memory/draw-calls/player-count, and a utilization percentage outside `[0, 100]`.
No upper bound is imposed on `fps` — a legitimate benchmark can legitimately report very high
frame rates, and an arbitrary cap would silently corrupt real data.

## Ingestion architecture

### Endpoint

```http
POST /api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples
```

Requires a Project API key holding **`PERFORMANCE_WRITE`** — a new scope, isolated from M4's
`BUG_REPORT_WRITE` and M5's `TELEMETRY_WRITE`. A key may hold any combination of the three; none is
implied by another. Existing keys retain only the scopes they were explicitly issued — adding
`PERFORMANCE_WRITE` to the enum never auto-upgrades a key minted before M6.

### Session relationship: reject, never auto-create

Performance ingestion targets an **existing** `TelemetrySession`, looked up the same way Telemetry
itself is addressed (`ProjectId + RuntimeSessionId`). If no such session exists yet — the runtime
submitted a performance batch before calling Telemetry's session-start — the request is rejected
with `404 Not Found` and a message pointing at the real fix ("start a telemetry session first").
**ForgeQA never silently creates a partial session from a performance batch.** The Unreal
Performance subsystem also never creates or waits synchronously on that session; it only checks the
Build Context is valid before attempting a flush.

An **ended** session (`EndedAt` set) rejects new samples with `409 Conflict`, exactly matching M5's
telemetry-event semantics — which is why the Unreal client always flushes performance and ends the
telemetry session in that order, never the reverse (see Shutdown ordering below).

### Batch validation and limits

```text
Performance:
  MaxSamplesPerBatch = 120
  MaxBatchBytes = 1 MiB
```

The whole batch is validated before anything is written: a batch above `MaxSamplesPerBatch`, a
duplicate `SequenceNumber` **within** the same request, or any single invalid sample (see
Validation above) fails the entire batch (`400`) — nothing from a rejected batch is ever partially
persisted. A batch whose body exceeds `MaxBatchBytes` is rejected at the controller (`413`) before
the service layer does any work.

### Idempotency

A `SequenceNumber` already persisted for the session is treated as an already-accepted duplicate,
not an error — the response reports `{ accepted, duplicates }`, mirroring M5's telemetry-event
contract exactly, computed via one existence-check query before a single `AddRange` + one
`SaveChanges` per batch (never one write per sample).

## Aggregation

All aggregation is computed in PostgreSQL — samples are never loaded into application memory to
compute a summary. Definitions:

- **Average FPS / Average Frame Time / Average Memory**: arithmetic mean (`AVG`) of the relevant
  persisted samples.
- **Minimum FPS**: `MIN(fps)` over persisted samples.
- **Peak Memory**: `MAX(memoryUsedBytes)` over persisted samples.
- **p50 / p95 / p99 Frame Time**: PostgreSQL `PERCENTILE_CONT(0.5|0.95|0.99) WITHIN GROUP (ORDER BY
  "FrameTimeMs")` — continuous (interpolated) percentiles, computed server-side, never by loading
  and sorting samples in .NET.
- All averages/percentiles are `null` (not `0`) when the underlying set has zero samples, or when a
  particular optional metric was never collected for any sample in the set.

## Backend endpoints

| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| `POST` | `/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples` | Project key (`PERFORMANCE_WRITE`) | Batched, idempotent sample ingestion |
| `GET` | `/api/projects/{projectId}/performance/sessions` | JWT | Paginated list of sessions that have at least one performance sample, with per-session summary metrics; filterable by Build, date range, platform |
| `GET` | `/api/projects/{projectId}/performance/sessions/{sessionId}` | JWT | Full aggregate summary for one session |
| `GET` | `/api/projects/{projectId}/performance/sessions/{sessionId}/samples` | JWT | Paginated raw samples, `sequenceNumber ASC` |
| `GET` | `/api/projects/{projectId}/performance/sessions/{sessionId}/series` | JWT | Downsampled time series for charting (`maxPoints`, default 500) |
| `GET` | `/api/projects/{projectId}/performance/sessions/{sessionId}/maps` | JWT | Per-map breakdown (sample count, avg FPS, p95 frame time) for sessions that changed maps |
| `GET` | `/api/projects/{projectId}/performance/builds` | JWT | One row per Build with performance data, aggregated across all its performance-bearing sessions |
| `GET` | `/api/projects/{projectId}/performance/compare?buildA=&buildB=` | JWT | Raw metric values and deltas between two Builds in the same Project |

Runtime (Project API) keys can reach only the ingestion endpoint — every query endpoint is JWT +
Project-membership only, so a runtime key can never enumerate performance data, matching the
IDOR-safe pattern used everywhere else in ForgeQA (see IDOR protection below).

### Session list and Build aggregation semantics

The session list only includes `TelemetrySession`s that have at least one `PerformanceSample` —
this is a real filter (`EXISTS` against `PerformanceSamples`), not a display-time hiding of empty
rows. Build aggregation follows the same rule: "session count" for a Build means *sessions
containing at least one performance sample*, not every telemetry session for that Build; Builds
with zero performance-bearing sessions are simply omitted from `GET .../performance/builds`.
Archived Builds remain fully valid comparison/aggregation targets — archiving only affects Build
Distribution's own listing, never historical performance data.

### Downsampling (series endpoint)

`GET .../series?maxPoints=N` buckets the session's samples (ordered by `SequenceNumber`, via
`ROW_NUMBER()`) into at most `N` buckets of roughly equal size and returns, per bucket: the
bucket's start timestamp, `averageFrameTimeMs` **and** `maximumFrameTimeMs` (so a spike is never
smoothed away by averaging alone), `averageFps` and `minimumFps`, and `averageMemoryUsedBytes`. A
session with fewer than `maxPoints` samples still goes through the same bucketing logic — each
sample simply becomes its own single-item bucket, so the response shape is uniform regardless of
session size.

### Map breakdown

Since a single runtime session can load multiple maps, `PerformanceSample.MapName` is captured **at
sample time**, not inherited once from the session's initial environment — the map breakdown
endpoint groups by this per-sample field, so a session that played `Lobby` then `Strike_Factory`
shows both distinctly.

## Database

- Table: `PerformanceSamples`. Foreign key `TelemetrySessionId → TelemetrySessions`,
  `ON DELETE CASCADE` (a sample has no meaning outside its session).
- Numeric columns use `double precision` for all timing/percentage fields and `bigint` for byte
  counts — never `decimal`, since nothing here requires exact-decimal semantics.
- Indexes: `(TelemetrySessionId, SequenceNumber)` **unique** (the idempotency backstop),
  `(TelemetrySessionId, ClientTimestamp)`, `(TelemetrySessionId, MapName)`. No index on individual
  metric columns — nothing in M6 queries by e.g. `WHERE Fps < 30` directly; add one only if a real
  query pattern needs it.
- Migration `AddPerformance` applied cleanly both via Testcontainers (integration tests) and
  against the live Docker Compose Postgres.

## Frontend

- **Performance** added to project navigation between Telemetry and API Keys.
- **Performance page** (`/app/projects/{projectId}/performance`): the session list (Build/Started/
  Duration/Samples/Avg FPS/Min FPS/p95 Frame Time/Peak Memory), Build/platform/date-range filters
  (`Last 24h` / `7 days` / `30 days` / all time presets), a Build Performance summary table, and a
  Build-vs-Build comparison tool (two selectors, a metric table showing both raw values and the
  delta/delta% — never a "GOOD/BAD" classification, matching the milestone's explicit instruction
  to expose evidence rather than auto-judge it).
- **Performance session detail page** (`/app/projects/{projectId}/performance/{sessionId}`):
  summary metrics, three charts (FPS with 30/60/120 reference lines, Frame Time in ms with
  average+max to keep spikes visible, Memory in human-readable MB), a map breakdown table, a link
  to the same session's Telemetry view, and a Related Bugs list (bugs sharing the same
  `RuntimeSessionId`, found via a small extension to the M4 bug-list filter — `runtimeSessionId` —
  rather than a new endpoint).
- **Bug ↔ performance correlation**: Bug Detail's existing "View telemetry session" link (M5) is
  joined by a **"View performance"** link, shown only when the correlated `TelemetrySession`
  actually has performance samples (checked via the session summary's `sampleCount`), so it never
  points at an empty page.
- **API Keys UI**: scope checkboxes now include **Performance Write** alongside Bug Report Write
  and Telemetry Write (see M5's `docs/telemetry.md` for the scope-checkbox pattern this reuses
  unchanged).
- **Charting**: no chart library existed in this repository. Given the modest requirements (a time
  axis, a metric axis, tooltips, readable units, explicitly **no** zoom/pan) a small, dependency-free
  inline-SVG line chart component (`PerformanceLineChart`) was written instead of introducing a new
  package — this was judged genuinely trivial for the three charts M6 needs, consistent with this
  repository's existing preference for a minimal dependency footprint (Tailwind-only styling, no UI
  kit). If a future milestone needs richer interaction (zoom, brushing, many overlapping series), a
  real charting library should be introduced then rather than growing this component indefinitely.

## Unreal integration

### `UForgeQAPerformanceSubsystem`

A third, separate `UGameInstanceSubsystem` — distinct from `UForgeQASubsystem` (identity only) and
`UForgeQATelemetrySubsystem` (event telemetry only), since continuous lightweight sampling is its
own responsibility with its own queue/lifecycle. It reuses, never duplicates: Build Context and
`RuntimeSessionId` (from `UForgeQASubsystem`) and the Project API key (from
`FForgeQARuntimeCredentials`) — the same key used for Bug Reporting/Telemetry may also carry
`PERFORMANCE_WRITE`. It never re-parses the Build manifest.

**Sampling**: frame counting and `DeltaTime` accumulation happen on every frame via
`FCoreDelegates::OnEndFrame` (a cheap increment and add — no allocation, no serialization); a
separate timer fires once per `PerformanceSampleIntervalSeconds` (default 1s) to turn the
accumulated frames/time into one `FPS`/`FrameTimeMs` sample, capture memory
(`FPlatformMemory::GetStats().UsedPhysical`) and, where available, thread timing (see the Metrics
table above), then reset the accumulators. No sample is emitted for an interval with zero observed
frames (e.g. the game was paused/backgrounded), rather than fabricating an FPS of 0.

**Queue/batch/flush**: identical shape to M5's Telemetry subsystem — a bounded queue
(`PerformanceMaxQueuedSamples`, default 600) that drops the *oldest* sample under sustained
overflow (never the newest) with throttled warning logging; a flush triggered either by queue size
(`PerformanceBatchSize`, default 30) or a timer; at most one flush in flight at a time; and bounded
exponential retry for transient failures only.

**Shared retry policy**: M5's Telemetry subsystem and M6's Performance subsystem now share one
`FForgeQARetryPolicy` (transient-failure classification + backoff delay calculation) instead of
each keeping its own copy — introduced in M6 specifically to avoid the two pipelines drifting into
subtly different retry behavior over time. This is deliberately just the one policy both need, not
a general-purpose retry framework.

**Shutdown ordering**: `Deinitialize()` stops sampling and performs a best-effort flush of whatever
is queued. Because `UForgeQATelemetrySubsystem` and `UForgeQAPerformanceSubsystem` are independent
`GameInstanceSubsystem`s, the engine determines the relative order their `Deinitialize()` calls run
in; Performance's shutdown never assumes Telemetry has already ended its session — it only ever
flushes whatever samples are queued against the session that was still valid moments before. Both
subsystems' HTTP calls remain fire-and-forget, never synchronously awaited, so shutdown is never
blocked on the network.

### C++ / Blueprint API

```text
Start ForgeQA Performance Monitoring     Is ForgeQA Performance Monitoring Active
Flush ForgeQA Performance                Get ForgeQA Queued/Dropped Sample Count
Stop ForgeQA Performance Monitoring      Get Current ForgeQA Performance Sample
                                          Was Last ForgeQA Performance Flush Successful
```

`FForgeQAPerformanceSample` (the Blueprint-facing diagnostic struct returned by
`GetCurrentForgeQAPerformanceSample`) uses explicit `bHas*` availability flags for every optional
metric rather than a sentinel zero — Blueprint has no nullable primitives, so this is the safe
equivalent of the backend DTOs' nullable doubles. Manual metric injection from Blueprint is
explicitly out of scope for M6.

### Settings (`UForgeQASettings`, Project Settings > Plugins > ForgeQA > Performance)

```text
Enable Performance Monitoring   default true (not compiled out of Shipping)
Sample Interval (seconds)       default 1.0
Batch Size                      default 30
Max Queued Samples              default 600
```

## Tests

- **Backend unit**: `PerformanceSampleTests` — construction, sequence/timestamp requirements,
  `NaN`/`Infinity`/negative-value/out-of-range-percentage rejection, optional-metric nullability.
- **Backend integration** (`PerformanceEndpointsTests.cs`, 24 tests): scope enforcement in all
  directions (`PERFORMANCE_WRITE`-only key cannot submit bugs or telemetry events;
  `BUG_REPORT_WRITE`/`TELEMETRY_WRITE`-only keys cannot ingest performance; revoked/cross-project
  keys rejected), session-relationship validation (ingestion before session exists → 404, ended
  session → 409), batch validation (count limit, duplicate-in-batch, invalid metrics rejected
  atomically), cross-batch duplicate idempotency, session summary aggregation with a deterministic
  1–100 fixture (exact percentile/average assertions), paginated samples, series downsampling,
  map breakdown grouping, Build aggregation (including "Builds with no samples are omitted"), Build
  comparison (deltas, foreign-Build rejection), and IDOR (outsider list rejection, cross-project
  session inaccessibility).
- **Frontend**: `PerformancePage` (session rows, empty/error states, Build filter, Build summaries,
  compare-and-render-deltas) and `PerformanceSessionDetailPage` (summary metrics, telemetry
  cross-link, empty/error states, map breakdown, related bugs) — 11 tests; `BugDetailPage` extended
  with 2 tests for the new "View performance" link; `ApiKeysPage` extended with a `Performance
  Write` scope-selection test.
- **Unreal**: automation test source for batch/DTO JSON serialization (including explicit
  assertions that unavailable optional metrics are omitted, never sent as `0`), the shared
  `FForgeQARetryPolicy` (classification and backoff growth/cap), and settings defaults.
  **Compilation and execution are NOT EXECUTED** — no Unreal Engine installation exists in this
  environment.

## Known limitations

- **Thread/GPU timing globals unverified against a real UE 5.8 build.** `GGameThreadTime` /
  `GRenderThreadTime` / `GGPUFrameTime` are long-standing, well-documented Unreal Engine globals
  (the same ones behind the built-in "stat unit" HUD), but this environment has no engine
  installation to compile against, so the exact header/linkage has not been confirmed for 5.8
  specifically. If unavailable, the correct fix is to mark these three fields as unavailable
  (`bHasGameThreadTime = false`, etc.) rather than removing the feature.
- **Queue overflow and sequence-assignment behavior for Performance samples is not independently
  automation-tested** the way Telemetry's equivalent logic is (see `ForgeQATelemetryTests.cpp`),
  because sample collection is driven by `FCoreDelegates::OnEndFrame` and cannot be triggered from
  a pure automation test without a live engine tick loop (PIE). The underlying queue algorithm is
  identical to Telemetry's already-tested one.
- `memoryAvailableBytes`, `cpuUtilizationPercent`, `gpuUtilizationPercent`, `drawCalls`,
  `playerCount`, and `pingMs` are fully supported by the backend domain/schema but are not yet
  populated by the Unreal client.
- No automated data-retention policy for `PerformanceSamples` (same acknowledged gap as M5's
  telemetry events — performance data can grow quickly with no deletion path yet).
