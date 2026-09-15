# Bug Reporting (Milestone 4)

M4 is the first milestone where ForgeQA is useful as an actual QA product: a tester can report a
bug from inside a running Unreal build, and it shows up in the ForgeQA web dashboard already
correlated to the exact Project, Build, and environment it came from.

```text
Unreal Runtime
    |
    | Project API key
    |
    | POST Bug
    v
ForgeQA API
    |
    +------ PostgreSQL
    |        BugReport
    |
    +------ Object Storage
             Screenshot

ForgeQA Web
    |
    | User JWT
    v
ForgeQA API
    |
    v
Bug Dashboard
```

## Scope boundary

Explicitly **not** implemented in M4: video/continuous recording, telemetry ingestion, performance
monitoring, crash reporting, AI classification, duplicate detection, Jira/Linear/GitHub/Slack/Discord
sync, CI/CD automation, tester account management, a public external tester portal, release
channels, session replay, voice notes, or an annotation/markup editor. Those belong to later
milestones.

## Domain model

### BugReport

Belongs to exactly one **Project** (`ProjectId`, never null) and optionally references a **Build**
(`BuildId`, nullable) — a manually created web report can be project-level with no specific build,
but a report submitted from the Unreal runtime always requires a valid, verified Build (enforced by
the `BugReport` constructor itself, not just API-layer validation: constructing one with
`Source = UNREAL_RUNTIME` and no `BuildId` throws).

| Field | Notes |
| --- | --- |
| `Title` | Required, ≤200 chars |
| `Description`, `ReproductionSteps` | Optional, ≤8000 chars each |
| `Severity` | `LOW` / `MEDIUM` / `HIGH` / `CRITICAL` |
| `Status` | `OPEN` / `IN_PROGRESS` / `RESOLVED` / `CLOSED` — starts at `OPEN`, always server-set |
| `Source` | `UNREAL_RUNTIME` / `WEB` / `API` |
| `ReporterUserId`, `ReporterDisplayName` | Set from the JWT for dashboard-created reports; `ReporterUserId` is `null` for Project-API-key (runtime) reports |
| `RuntimeSessionId` | Correlation token, see below — not a foreign key to anything persisted yet |
| `Environment` | An owned value object (`BugEnvironment`), embedded in the same row — see below |
| `CreatedAt`/`UpdatedAt`/`ResolvedAt`/`ClosedAt`/`DeletedAt` | All server-authoritative |

**Editable fields are structurally limited.** `BugReport.UpdateDetails(title, description,
reproductionSteps, severity)` and `ChangeStatus(status)` are the only mutators; `ProjectId`,
`BuildId`, `Source`, `ReporterUserId`, `RuntimeSessionId`, `CreatedAt`, and `Environment` have no
setter at all after construction — mass-assignment of historical/identity fields is impossible by
construction, not just by omission from `UpdateBugRequest`'s shape.

### Environment capture

`BugEnvironment` is an EF Core **owned type** embedded directly in the `BugReports` row (columns
prefixed `Environment_*`) — not a join, not a separate table, since there is exactly one per
BugReport and it is never queried independently. Every field is optional and descriptive only:
`MapName`, `GameMode`, `Platform`, `EngineVersion`, `OsVersion`, `Cpu`, `Gpu`, `MemoryBytes`,
`Locale`. Deliberately **excluded** for privacy: file paths, usernames, IP addresses, MAC/hardware
serial numbers, or any other machine-identifying data.

### Lifecycle

`OPEN → IN_PROGRESS → RESOLVED → CLOSED`, changed via `PATCH .../bugs/{bugId}`. Moving to
`RESOLVED` sets `ResolvedAt`; moving to `CLOSED` sets `ClosedAt` (and backfills `ResolvedAt` if a
bug was closed directly from `OPEN`/`IN_PROGRESS`). **Reopening** (moving back to `OPEN` or
`IN_PROGRESS`) clears both terminal timestamps — a bug that's open again is, by definition, neither
resolved nor closed. Deletion is soft (`DeletedAt`) and excluded from every list/detail query; there
is no hard-delete endpoint.

### RuntimeSessionId

Generated once per `GameInstance` lifetime by `UForgeQASubsystem::Initialize()` (a plain `FGuid`,
not a persisted entity) and attached to every bug report submitted during that play session. A new
launch gets a new session ID. This is *only* a correlation token in M4 — no `TelemetrySession`
table exists yet; M5 may turn the same concept into one without changing this field's meaning.

### Build correlation is always re-verified server-side

The client never gets to assert "this Build belongs to this Project." `BugService` looks up the
Build via `IBuildRepository.GetByIdForProjectAsync(projectId, buildId)`, which is scoped by both
IDs together — a Build ID from a different Project (or one that doesn't exist at all) resolves
identically to "not found," collapsing both cases into one `400 Validation` response with no way to
distinguish them. **Archived Builds are explicitly accepted** — an already-distributed build may
still be actively under test, so archiving it must not retroactively block bug reports against it.

## Security model: Project API keys

Runtime bug submission needs authentication, but a packaged game cannot safely hold a developer's
JWT or password. Instead, M4 introduces `ProjectApiKey` — a narrowly-scoped, revocable machine
credential bound to exactly one Project.

- **Format**: `fqa_proj_<8-hex-prefix>_<64-hex-secret>` (`ProjectApiKeyHasher.Generate()`). The
  prefix is stored in plaintext for human identification in the UI; it is *not* used for lookup.
- **Hashing**: the full key string is SHA-256 hashed (`ProjectApiKeyHasher.Hash`) and only the hash
  (`ProjectApiKey.KeyHash`) is ever persisted. The plaintext is generated, returned to the caller
  exactly once in the create-key API response, and is never recoverable again — not even by an
  administrator.
- **Scope**: M4 defines one scope, `BUG_REPORT_WRITE`. A key with only this scope can create a bug
  report and its attachments — nothing else. It cannot list/read bugs, manage Projects, delete
  Builds, download private artifacts, manage the Organization, or create other keys. Future scopes
  (`TELEMETRY_WRITE`, `CRASH_WRITE`, `BUILD_WRITE`, ...) are additive and never granted implicitly.
- **Revocation**: `POST /api/projects/{projectId}/api-keys/{keyId}/revoke` sets `RevokedAt`; a
  revoked key fails authentication identically to an unknown one (no information disclosure about
  which keys exist). `LastUsedAt` is updated on every successful authentication.
- **Transport**: a Project API key is sent as `X-ForgeQA-Key: <key>` — deliberately **not**
  `Authorization: Bearer`, so it is never at risk of being parsed as (or confused with) a user JWT
  when an endpoint accepts both schemes (`ProjectApiKeyAuthenticationHandler` in the API layer).
- **Never in the manifest.** The M3 Build manifest (`ForgeQABuild.json`) remains
  identifier/metadata-only. The runtime API key lives in a completely separate resolution path
  (below) with its own lifecycle.

**Security reality check**: a secret embedded in a distributed client executable can eventually be
extracted by a sufficiently motivated attacker. This is why the key is scoped (write-only, one
Project, one capability), revocable, and rate-limited — it limits blast radius rather than
pretending extraction is impossible.

### Runtime key resolution (Unreal)

`FForgeQARuntimeCredentials::ResolveApiKey()`, precedence highest first:

1. `-ForgeQAApiKey=<key>` command-line override (the intended path for a future M8 CI packaging step)
2. `FORGEQA_API_KEY` environment variable
3. `UForgeQASettings::DevelopmentRuntimeApiKey` — a plain `config` (per-developer `Saved/Config`,
   not `defaultconfig`) property intended for local development convenience only, editable via
   Project Settings > Plugins > ForgeQA > Bug Reporting. A production/Shipping packaging pipeline
   should use options 1 or 2 instead.

This repository ships only the plugin source, not a host `.uproject`; a real host project's
`Saved/` directory (where option 3 is written) is excluded by Unreal's own standard
`.gitignore` conventions, not by anything in this repository.

## Backend API

All endpoints are nested under a Project, matching the existing convention.

| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| `POST` | `/api/projects/{projectId}/bugs` | JWT or Project key | Creates a bug; rate-limited (see below) |
| `GET` | `/api/projects/{projectId}/bugs` | JWT only | Paginated, filtered, searched list |
| `GET` | `/api/projects/{projectId}/bugs/{bugId}` | JWT only | Full detail incl. attachments |
| `PATCH` | `/api/projects/{projectId}/bugs/{bugId}` | JWT only | Title/description/reproductionSteps/severity/status |
| `POST` | `/api/projects/{projectId}/bugs/{bugId}/attachments` | JWT or Project key | Initiates an attachment upload |
| `POST` | `/api/projects/{projectId}/bugs/{bugId}/attachments/{attachmentId}/complete` | JWT or Project key | Verifies the upload and marks it `READY` |
| `POST` | `/api/projects/{projectId}/bugs/{bugId}/attachments/{attachmentId}/download` | JWT only | Signed, short-lived GET URL |
| `POST` | `/api/projects/{projectId}/api-keys` | JWT only | Returns the plaintext key **once** |
| `GET` | `/api/projects/{projectId}/api-keys` | JWT only | Metadata only — no plaintext, no hash |
| `POST` | `/api/projects/{projectId}/api-keys/{keyId}/revoke` | JWT only | Idempotent |

A Project API key can only reach the two dual-scheme endpoints above, and only for the exact
Project it was issued for — `BugService.AuthorizeAsync` rejects any mismatch as `403 Forbidden`
before touching the database. List/detail/update/api-key-management endpoints accept **only** a
user JWT; presenting a Project key there fails with `401` (the request never satisfies the
JWT-only `[Authorize]` policy).

### List filters

`page`, `pageSize` (capped at 100), `status`, `severity`, `buildId`, `source`, and `search`
(case-insensitive, `ILIKE` against title/description, executed in PostgreSQL — never loaded into
memory and filtered in .NET). Default sort is `CreatedAt DESC, Id DESC` — fixed, not
caller-configurable, so there's no sort-field injection surface to defend.

### Rate limiting

`POST /bugs` and the two attachment endpoints share a fixed-window rate limiter
(`RateLimiting.BugIngestionPolicy` in `Program.cs`), partitioned by the caller's Project API key ID
(falling back to remote IP for JWT/unauthenticated callers) — one noisy key never throttles another
Project. The limit is configurable via `BugReporting:MaxRuntimeReportsPerMinutePerKey` (default 60);
see `BugReportingOptions`.

## Screenshot storage

Screenshots reuse the exact same S3-compatible object storage introduced in M2 for build artifacts
— no second storage system. The M2 abstraction (`IArtifactStorage`) was multipart-upload-specific,
so M4 extracted the actually-generic primitives (`GetMetadataAsync`, `CreateDownloadUrl`,
`DeleteObjectAsync`, plus a new single-shot `CreatePutUrl`) into a new `IObjectStorage` interface;
`IArtifactStorage` now extends it with the multipart-only operations large build artifacts need.
`S3ArtifactStorage` is the single concrete implementation of both, registered once in DI and
resolved as whichever interface a consumer needs.

Screenshots are small enough that a single presigned PUT is sufficient — no multipart upload:

```text
1. Unreal calls POST .../bugs/{bugId}/attachments  → server creates a PENDING BugAttachment,
                                                       returns a short-lived presigned PUT URL
2. Unreal PUTs the PNG bytes directly to that URL (never through the ForgeQA API)
3. Unreal calls POST .../attachments/{id}/complete → server HEADs the object, verifies it exists
                                                       and its size matches what was registered,
                                                       marks the attachment READY (or FAILED)
4. The web dashboard requests a signed, short-lived GET URL to render/download it
```

- Object keys are always server-generated, never accepted from the client:
  `organizations/{orgId}/projects/{projectId}/bugs/{bugId}/attachments/{attachmentId}/{fileName}`.
- Configurable via `BugReporting` options: `MaxScreenshotSizeBytes` (default 20 MiB),
  `AttachmentUploadUrlLifetimeMinutes` (15), `AttachmentDownloadUrlLifetimeMinutes` (10).
- Allowed content types: `image/png`, `image/jpeg` for `SCREENSHOT`; `text/plain` /
  `application/octet-stream` for `LOG` (optional in M4, same upload path, not wired into the
  Unreal UI). Content type is treated as metadata, not proof of file contents — no malware
  scanning in M4.
- No permanent public URL is ever stored; every URL returned to a client is presigned and expires.

## Unreal integration

Builds on the M3 plugin without introducing a second HTTP client — `FForgeQAApiClient` gained
`CreateBugReport`, `InitiateBugAttachment`, `CompleteBugAttachment`, and a generic `PutObject`
(for the presigned-URL upload itself, which isn't a ForgeQA API call). Bug-specific JSON
parsing/serialization lives in `FForgeQAApiResponseParser`, same pattern as M3.

- **`UForgeQABugReportingSubsystem`** (new `UGameInstanceSubsystem`, separate from
  `UForgeQASubsystem` on purpose — identity resolution and bug submission are different
  responsibilities, and one subsystem doing both would start down the road to a God object):
  - `IsBugReportingAvailable()` — true only when both a valid Build Context *and* a resolvable
    Project API key are present.
  - `CaptureScreenshot(...)` — `FScreenshotRequest::RequestScreenshot()` +
    `UGameViewportClient::OnScreenshotCaptured()`, encoded to PNG via `FImageUtils`. No
    third-party screenshot library.
  - `SubmitBugReport(...)` — attaches `ProjectId`/`BuildId`/`RuntimeSessionId`/environment
    automatically from `UForgeQASubsystem`; rejects a second concurrent submission outright
    (duplicate-submission protection) rather than queuing or racing it; if the bug report itself
    succeeds but the screenshot attachment fails at any step, the overall result is still reported
    as success (the report exists and is useful without its screenshot) with the failure logged.
  - `GetSubmissionState()` — explicit `EForgeQABugSubmissionState`
    (`Idle → Capturing → SubmittingReport → UploadingScreenshot → Finalizing → Succeeded/Failed`),
    so UI code never has to infer progress from ad hoc booleans.
- **`UForgeQABugReportWidget`** (new `UUserWidget` C++ base class) provides the behavior — capture,
  submit, `BlueprintImplementableEvent`s for build-info/screenshot-preview/state/success/failure —
  that a Widget Blueprint asset binds to. **The actual visual UMG layout (text boxes, severity
  dropdown, buttons) is a `.uasset` that must be authored inside the Unreal Editor**; this
  repository's tooling cannot produce a binary Blueprint asset, only the C++ class it extends. See
  the wireframe in the milestone brief for the intended layout.
- Blueprint-callable, HTTP-detail-free: **Open Bug Report UI** (implement via the widget's own
  `AddToViewport`/visibility from Blueprint), **Submit Bug Report**, **Capture Bug Screenshot**,
  **Is Bug Reporting Available** are all plain Blueprint nodes; no raw `FHttpRequest` ever reaches
  Blueprint or the widget's visual layer.

## Tests

### Backend

- Unit: `BugReportTests`, `BugAttachmentTests`, `ProjectApiKeyTests` (domain invariants, status
  transitions, revocation) — 22 new tests.
- Integration: `BugsEndpointsTests` (26 tests: creation, source/auth-method matching, Build
  correlation incl. cross-project/unknown/archived, cross-project IDOR, listing/filters/search,
  lifecycle transitions, environment immutability, Project-key authorization incl. wrong-project
  and revoked-key rejection, full attachment lifecycle incl. size-mismatch and missing-object
  rejection) and `ProjectApiKeysEndpointsTests` (7 tests: plaintext-once, no-hash-in-response,
  revoke, outsider rejection). All run against the same Testcontainers PostgreSQL + `FakeObjectStorage`
  pattern established in M2/M3 — no new test infrastructure required.

### Frontend

`BugsPage` (list/empty/error/create-validation/create-success/filter) and `ApiKeysPage`
(list/empty/create-shows-secret-once/revoke/error) — 11 new tests, same Vitest + Testing Library
conventions as M1.

### Unreal

Automation Test **source** added (manifest-adjacent pattern from M3): bug-report JSON
serialization (incl. null `buildId` for a Build-less report), attachment response parsing, and
runtime credential command-line precedence. **Compilation and execution are NOT EXECUTED** — this
environment has no Unreal Engine installation. See [Manual Unreal validation](#manual-unreal-validation-status).

## Manual Unreal validation status

Per the milestone's own instructions, full UE compile/Editor/PIE/packaged validation is
intentionally deferred until all milestones are implemented. **None of the following should be
read as passing:**

| Item | Status |
| --- | --- |
| Plugin compile | NOT EXECUTED |
| In-game Bug Report widget (Blueprint layout) | NOT EXECUTED — also not yet authored, since it requires the Editor |
| Screenshot capture | NOT EXECUTED |
| Runtime bug submission | NOT EXECUTED |
| Packaged build submission | NOT EXECUTED |

Everything that does not require Unreal Engine — backend build/tests/migration, frontend
lint/typecheck/tests/build, Docker service health — was actually run; see the milestone report for
results.
