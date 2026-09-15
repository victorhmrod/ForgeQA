# Unreal Integration (Milestone 3)

## What this gives a game

A running Unreal build — in the Editor (PIE) or packaged — can answer one question without any
network access: **which ForgeQA Project and Build am I?** Later milestones (Bug Reporting,
Telemetry, Performance, Crash Reporting, Playtest Sessions) all correlate their data against
`Project.id` + `Build.id`; M3 is purely the plumbing that makes that identity available.

```text
               Unreal Editor
                    |
                    | authenticated HTTP
                    v
               ForgeQA API
                    |
              Project / Build
                    |
                    v
           ForgeQA Build Manifest
                    |
              packaged with game
                    |
                    v
          UForgeQASubsystem
                    |
        +-----------+-----------+
        |                       |
       C++                  Blueprint
```

## Scope boundary

M3 is integration foundation only. It does **not** implement in-game bug reporting, telemetry,
performance/crash reporting, CI authentication, automatic build registration/upload, patching,
launcher integration, tester accounts, or runtime authentication — those are later milestones.

## Plugin architecture

`unreal-plugin/ForgeQA/` has two modules:

- **`ForgeQA`** (Runtime, `Type: Runtime`) — everything a packaged game needs. Depends only on
  `Core`, `CoreUObject`, `Engine`, `HTTP`, `Json`, `JsonUtilities`, `DeveloperSettings`, `Projects`.
  Never references `UnrealEd`, `Slate`, or `ToolMenus`.
- **`ForgeQAEditor`** (Editor-only, `Type: Editor`) — the login/linking UI and the manifest
  commandlet. Depends on `ForgeQA` (Runtime) plus `Slate`, `SlateCore`, `UnrealEd`, `ToolMenus`.

The Runtime module is never aware the Editor module exists — the dependency is one-directional.

### Runtime module (`Source/ForgeQA/`)

| File | Responsibility |
| --- | --- |
| `ForgeQASettings.h/.cpp` | `UForgeQASettings : UDeveloperSettings` — Project Settings > Plugins > ForgeQA |
| `ForgeQABuildContext.h` | `FForgeQABuildContext` — the Blueprint-visible identity struct |
| `ForgeQABuildManifest.h/.cpp` | Manifest schema + JSON read/write |
| `ForgeQAApiTypes.h` | Plain DTOs for the ForgeQA responses this plugin consumes |
| `ForgeQAApiResponseParser.h/.cpp` | Pure JSON→DTO parsing, unit-testable without HTTP |
| `ForgeQAApiClient.h/.cpp` | Centralized async HTTP client (login/orgs/projects/builds) |
| `ForgeQASubsystem.h/.cpp` | `UForgeQASubsystem : UGameInstanceSubsystem` — resolves and exposes the Build Context |
| `ForgeQALog.h` | `LogForgeQA` log category |

### Editor module (`Source/ForgeQAEditor/`)

| File | Responsibility |
| --- | --- |
| `ForgeQAEditorModule.h/.cpp` | Registers the **Tools > ForgeQA** menu entry |
| `ForgeQAEditorSession.h/.cpp` | Login, cached Org/Project/Build lists, the one binding-validation routine, Apply |
| `SForgeQAPanel.h/.cpp` | The Slate UI (API URL, login, Project/Build pickers, status, Validate/Apply) |
| `ForgeQAGenerateBuildManifestCommandlet.h/.cpp` | Non-interactive manifest generation for future CI |

## Build identity: resolution and precedence

`UForgeQASubsystem::Initialize()` resolves `FForgeQABuildContext` at `GameInstance` startup, highest
precedence first, **without ever contacting the ForgeQA API**:

1. **Command-line override**: `-ForgeQAProjectId=<uuid> -ForgeQABuildId=<uuid>` (both required;
   optionally `-ForgeQAVersion=`, `-ForgeQABuildNumber=`, `-ForgeQAPlatform=`,
   `-ForgeQAConfiguration=` for supplemental metadata). Either UUID being malformed is logged as a
   warning and the whole override is ignored (it does not partially apply).
2. **Packaged build manifest**: `Content/ForgeQA/ForgeQABuild.json`, written by the Editor's Apply
   action or the manifest commandlet. See [Manifest format](#manifest-format).
3. **Project Settings**: only `ProjectId` (never `BuildId` — Settings alone can never yield a
   complete, valid context; see [`HasValidBuildContext()`](#offline-runtime-behavior)).

If nothing resolves, `HasValidBuildContext()` is `false`. This is the expected state for PIE
without a linked Build, and the subsystem never crashes, asserts, or spams the log because of it.

`Build.id` and `Project.id` (both ForgeQA UUIDs) are the only canonical identity. `version`,
`buildNumber`, `branch`, `commitSha`, `platform`, `configuration` are metadata copied from the
Build Registry at manifest-generation time — never used to *identify* a build, only to describe it.

## Manifest format

```json
{
  "schemaVersion": 1,
  "projectId": "9d0f469c-5380-4243-9bb5-a35b6a59ffe7",
  "buildId": "50b09e84-5693-49cf-817c-5d928d086fc3",
  "version": "0.4.2",
  "buildNumber": "1842",
  "platform": "WINDOWS",
  "configuration": "DEVELOPMENT",
  "branch": "main",
  "commitSha": "a941de3"
}
```

`schemaVersion` starts at `1`. `FForgeQABuildManifestSerializer::TryParseJsonString` rejects (never
misinterprets) a manifest whose `schemaVersion` is higher than the plugin supports, a missing
`projectId`/`buildId`, or a malformed UUID — in every failure case it returns `false` with a
descriptive error and the runtime subsystem logs a warning and continues (`HasValidBuildContext()`
stays `false`); it never crashes the game.

**Packaging note (unverified — see [Unreal validation](#unreal-validation-results)):** a JSON file
under `Content/` is not automatically included in a packaged build by default. Packaging this
manifest correctly is expected to require adding `ForgeQA` to *Project Settings > Packaging >
Additional Non-Asset Directories to Copy*. This must be confirmed against a real UE 5.8 packaging
run before relying on it in production.

## Editor workflow

1. **Tools > ForgeQA** opens the panel (`SForgeQAPanel`), backed by a per-session
   `FForgeQAEditorSession` (owns the in-memory access token and cached lists — nothing here is
   written to disk).
2. **Login** — `POST /api/auth/login` against the configured API Base URL. On success the
   organizations the user belongs to are fetched, then every Project across all of them (there is
   no single "list all my projects" endpoint; the plugin composes it from the existing
   `GET /api/organizations` + `GET /api/organizations/{id}/projects`, matching how the ForgeQA web
   frontend does the same thing — no new backend endpoint was added for this).
3. **Select Project**, then **Select Build** — `GET /api/projects/{projectId}/builds?status=active`
   by default; the "Show archived Builds" checkbox switches to `status=all`. An archived Build can
   still be selected but is visually marked `[Archived]` and is never the default list.
4. **Validate Binding** — calls `GET /api/projects/{projectId}/builds/{buildId}` (the same
   `FForgeQAEditorSession::ValidateBinding` routine used by Apply, so the check is never
   duplicated). A Build that belongs to a different Project resolves as the backend's own `404`,
   which the panel surfaces as *"This Build does not belong to the selected Project, or no longer
   exists."*
5. **Apply** — re-runs the same validation, then:
   - writes `ProjectId` to `UForgeQASettings` (`Config/DefaultGame.ini`, `defaultconfig`) and
     `DefaultBuildId` to the per-developer `Saved/Config` override (plain `config`) — see
     [Source-control behavior](#source-control-behavior);
   - generates the build manifest from the *validated* Build Registry response, not from
     hand-typed UI state.

Login failures, network failures, and authorization failures are surfaced as the specific messages
`FForgeQAApiResponseParser::ParseProblemDetails` extracts from the backend's `ProblemDetails`
response (or a generic per-status-code fallback) — never a raw JSON blob or a stack trace.

## Runtime workflow

A packaged game never talks to the ForgeQA API to determine its own identity. At `GameInstance`
startup, `UForgeQASubsystem::Initialize()` runs the precedence chain above entirely from local
files/command-line/config, then exposes:

- C++: `GetBuildContext()`, `HasValidBuildContext()`, `GetProjectId()`, `GetBuildId()`.
- Blueprint: **Get ForgeQA Build Context**, **Is ForgeQA Build Linked**, **Get ForgeQA Project ID**,
  **Get ForgeQA Build ID** (all `BlueprintPure`).

## Source-control behavior

- `ApiBaseUrl` and `ProjectId` are `defaultconfig` on `UForgeQASettings`, written to
  `Config/DefaultGame.ini` — appropriate to commit, since the ForgeQA Project a repository belongs
  to is long-lived team configuration.
- `DefaultBuildId` is a plain `config` property (written to the per-developer `Saved/Config`
  override, which a standard Unreal `.gitignore` excludes). A Build changes far more often than a
  Project; binding the last-selected Build into shared, committed config would create constant,
  meaningless diffs. It only seeds the Editor's Build picker — the runtime subsystem never reads it.
- The actual packaged Build identity lives in the generated manifest file, which is a build output,
  not source — do not hand-edit or commit a specific manifest instance as "the" project manifest.

## API integration

Consumes only existing M0–M2 endpoints; **no backend changes were made or needed** for M3:

- `POST /api/auth/login`
- `GET /api/organizations`
- `GET /api/organizations/{organizationId}/projects`
- `GET /api/projects/{projectId}/builds` (with `status=active|all`)
- `GET /api/projects/{projectId}/builds/{buildId}`

## Automation-friendliness (for future M8 CI/CD)

Two non-interactive ways to inject Build identity already exist, so M8 will not need editor UI
automation:

- **Command-line override**, read directly by the packaged game itself
  (`-ForgeQAProjectId=... -ForgeQABuildId=...`), useful for a launch wrapper.
- **`ForgeQAGenerateBuildManifest` commandlet**, run against `UnrealEditor-Cmd.exe` before
  packaging, to write the manifest file non-interactively:

  ```text
  UnrealEditor-Cmd.exe Project.uproject -run=ForgeQAGenerateBuildManifest
    -ForgeQAProjectId=<uuid> -ForgeQABuildId=<uuid>
    -ForgeQAVersion=... -ForgeQABuildNumber=... -ForgeQAPlatform=... -ForgeQAConfiguration=...
  ```

  This commandlet deliberately does **not** call the ForgeQA API — it writes exactly the metadata
  it is given. Full CI automation (fetching/validating that metadata from ForgeQA, authenticating
  a CI identity) is explicitly out of scope for M3 and belongs to M8.

## Security model

- The developer's password is never persisted anywhere — it lives only in a Slate text box's
  transient state during the login call.
- The access/refresh tokens obtained by the Editor are held only in `FForgeQAEditorSession`
  in-memory for the life of the Editor session; they are never written to `DefaultGame.ini`,
  `DefaultEngine.ini`, the build manifest, or any packaged content, and are never logged.
  `LogForgeQA` never logs an `Authorization` header or a token value.
- The packaged game's manifest contains only identifiers and descriptive metadata — no
  credentials — and the runtime never authenticates to or contacts the ForgeQA API at all in M3.
- HTTP requests use whatever scheme is configured in `ApiBaseUrl` (`http://localhost:5000` by
  default for local development); production ForgeQA deployments should configure an `https://`
  URL, and the client does not disable TLS verification.

## Tests

### Unreal Automation Tests (source written; **compilation/execution NOT EXECUTED** — no Unreal Engine installation is available in this environment)

- `Private/Tests/ForgeQABuildManifestTests.cpp` — valid parse, serialize→parse round-trip, invalid
  JSON, missing `schemaVersion`, unsupported newer `schemaVersion`, invalid `projectId` UUID,
  missing `buildId`.
- `Private/Tests/ForgeQABuildContextPrecedenceTests.cpp` — command-line override with both valid
  UUIDs, missing `BuildId` not resolving, invalid UUID rejected, loading a missing manifest file
  failing cleanly.
- `Private/Tests/ForgeQAApiResponseParserTests.cpp` — valid/invalid auth response, projects
  response, build detail response (including archived-vs-active detection), `ProblemDetails` error
  parsing with and without a body.

These are gated by `WITH_DEV_AUTOMATION_TESTS` per standard Unreal convention and are excluded from
Shipping builds automatically.

### Backend / frontend regression

All pre-existing M0–M2 tests were run **before and after** this milestone's changes (which touched
only `unreal-plugin/` and `docs/`) and are unaffected:

- Backend unit tests: 39/39 passing.
- Backend integration tests: 41/41 passing.
- Frontend tests: 7/7 passing.

## Manual Unreal validation

**NOT EXECUTED.** This environment has no Unreal Engine 5.8 installation, so none of the following
could be performed, and none should be assumed to pass:

| Step | Result |
| --- | --- |
| Plugin compiles under UE 5.8.x | NOT EXECUTED |
| Unreal Editor loads with plugin enabled | NOT EXECUTED |
| ForgeQA panel opens | NOT EXECUTED |
| Login against a running ForgeQA API | NOT EXECUTED |
| Project selection | NOT EXECUTED |
| Build selection | NOT EXECUTED |
| Validate Binding / Apply / manifest generation | NOT EXECUTED |
| PIE Build Context | NOT EXECUTED |
| Packaged Development build Context (incl. whether the manifest actually ships) | NOT EXECUTED |

Before relying on this integration, a developer with a UE 5.8 installation must run through
`README.md`'s Unreal section and this checklist, particularly the packaging step, since that is the
one design decision here (manifest under `Content/`) that literally cannot be confirmed without a
real packaging run.
