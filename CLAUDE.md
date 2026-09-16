# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ForgeQA is a developer-first platform for game QA: build distribution, playtesting, contextual
bug reporting, telemetry, performance analytics, crash reporting, CI/CD, and automated QA. The
initial integration target is Unreal Engine, but the backend is intentionally not coupled to it.

The project is built incrementally through milestones (see `README.md` for the full roadmap and
current status). Each milestone's implementation notes live under `docs/` (e.g.
`docs/build-registry.md` for the Build Registry milestone) — check there for the domain rationale
behind a feature before assuming intent from code alone.

**Never build a feature belonging to a future milestone while implementing the current one.** Scope
boundaries between milestones are deliberate (e.g. Build Registry stores metadata only; artifact
upload/storage is a separate, later milestone) — check the relevant `docs/*.md` file for the
explicit "what's out of scope" section before adding anything that smells like scope creep.

## Commit conventions

- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`,
  `test:`, `refactor:`, `chore:`, etc.) for commit subject lines.
- Do **not** add a `Co-Authored-By` trailer (Claude or otherwise) to commits in this repository.

## Repository layout

```
backend/            ASP.NET Core API (C#, .NET 10, EF Core, PostgreSQL, ASP.NET Identity, JWT)
  src/
    ForgeQA.Domain/          entities, enums — no framework dependencies
    ForgeQA.Application/     use-case services, DTOs, repository/service interfaces
    ForgeQA.Infrastructure/  EF Core, PostgreSQL, Identity, JWT issuance, repositories, migrations
    ForgeQA.Api/             controllers, middleware, Swagger, health checks, Program.cs
  tests/
    ForgeQA.UnitTests/         domain + application logic, no external dependencies
    ForgeQA.IntegrationTests/  full HTTP stack via WebApplicationFactory + Testcontainers Postgres
frontend/           Next.js App Router, TypeScript, TanStack Query, Tailwind v4
launcher/ForgeQA.Launcher/   Avalonia desktop launcher (foundation only, no build download/launch yet)
unreal-plugin/ForgeQA/       Unreal Engine 5.8 plugin: ForgeQA (Runtime) + ForgeQAEditor (Editor-only)
docs/                Per-milestone documentation (domain model, API, scope boundaries)
```

## Commands

### Backend (run from `backend/`)

```bash
dotnet restore
dotnet build
dotnet test tests/ForgeQA.UnitTests
dotnet test tests/ForgeQA.IntegrationTests          # requires Docker (Testcontainers spins up Postgres)
dotnet test tests/ForgeQA.IntegrationTests --filter "FullyQualifiedName~ClassName.MethodName"  # single test
dotnet run --project src/ForgeQA.Api
```

Add a migration after changing an entity or EF configuration:

```bash
dotnet ef migrations add <Name> --project src/ForgeQA.Infrastructure --startup-project src/ForgeQA.Api -o Persistence/Migrations
```

The API applies pending migrations automatically at startup (`Program.cs`), so there is normally no
need to run `dotnet ef database update` manually — restarting the API (or the `backend` Docker
Compose service) is enough. The `dotnet ef` CLI reads `appsettings.json`'s connection string, which
uses different default credentials than `docker-compose.yml`/`.env`; if you need to run `dotnet ef
database update` directly against the Compose Postgres, pass a matching `ConnectionStrings__Default`
override rather than relying on the CLI's default config.

### Frontend (run from `frontend/`)

```bash
npm install
npm run dev
npm run lint
npx tsc --noEmit
npm run test          # Vitest + Testing Library
npm run build
```

### Full stack

```bash
cp .env.example .env   # set a real JWT_SECRET (e.g. `openssl rand -base64 64`) and strong passwords
docker compose up --build
```
Frontend on :3000, API on :5000 (`/swagger` in Development, `/health` for the health check), MinIO
console on :9001.

## Backend architecture

Clean-Architecture-style layering, strictly enforced by project references:

```
Domain (no EF/ASP.NET/Identity deps) ← Application (use cases, DTOs) ← Infrastructure (EF, Identity, JWT) ← Api
```

Conventions established in this codebase — follow them rather than introducing parallel patterns:

- **Identity lives in Infrastructure, not Domain.** `ApplicationUser : IdentityUser<Guid>` is an
  Infrastructure concern; Domain entities only reference users by `Guid` (e.g.
  `OrganizationMember.UserId`, `Build.CreatedByUserId` + a denormalized `CreatedByDisplayName`
  snapshot taken at creation time). Don't add a Domain `User` entity or pull Identity types into
  Domain/Application.
- **`Result<T>` for expected failures.** Application services return `Result<T>` with an
  `ErrorType` (NotFound / Forbidden / Conflict / Validation / Unauthorized), mapped to
  `ProblemDetails` + the matching HTTP status in the API layer via
  `ResultExtensions.ToActionResult`. Unhandled exceptions go through `UseExceptionHandler()` +
  `AddProblemDetails()` — never let a stack trace reach the client.
- **Thin, purpose-built repositories** (`IOrganizationRepository`, `IProjectRepository`,
  `IBuildRepository`) — only the query shapes actually used by a service, not a generic
  repository/unit-of-work abstraction. Filtering, searching (via `EF.Functions.ILike`), and
  pagination happen in the repository's LINQ query, not in application-layer post-filtering.
- **Authorization propagates down the hierarchy** `Organization → Project → Build`. Every
  service method that touches a Project or Build re-derives access by loading the parent and
  checking `IOrganizationRepository.GetMembershipAsync(organizationId, userId)` — there is no
  cached/claims-based authorization shortcut. Missing membership → `403 Forbidden`; missing
  entity → `404 NotFound`. Child resources (e.g. a Build) are looked up scoped to both their
  route-level parent ID and their own ID (`GetByIdForProjectAsync(projectId, buildId)`) so a
  resource belonging to a different parent resolves as `404`, never leaking existence — replicate
  this pattern for any new nested resource instead of trusting a bare ID from the route.
- **Enums serialize as their C# member names**, in upper snake case matching the wire format
  (e.g. `BuildPlatform.WINDOWS`, `BuildConfiguration.DEBUG_GAME`) — `Program.cs` registers a
  global `JsonStringEnumConverter`. Any HTTP client reading these responses (including test code)
  needs the same converter configured or `System.Text.Json` will fail to parse the enum values.
- **Pagination convention**: `Application.Common.PagedResult<T>` (`Items`, `Page`, `PageSize`,
  `TotalCount`, `TotalPages`), driven by explicit `page`/`pageSize` query parameters (default 20,
  capped at 100). List endpoints don't expose a configurable sort parameter — sorting is fixed
  (`CreatedAt DESC`, then `Id DESC` as a stable tiebreaker) precisely to avoid needing a sort-field
  allowlist. Follow this for any new paginated list rather than inventing a different shape.
- **PATCH DTOs only contain editable fields.** Identity/audit fields (`Id`, `ProjectId`,
  `CreatedBy*`, `CreatedAt`, and any field that's part of a resource's logical uniqueness key,
  e.g. a Build's `BuildNumber`/`Platform`/`Configuration`) are simply absent from the update DTO's
  shape — mass-assignment is prevented structurally, not by a runtime allowlist check.
- **Config values consumed at app-startup time must be read lazily from DI, not eagerly from
  `builder.Configuration`.** `WebApplicationFactory`-based integration tests inject their own
  config (e.g. a Testcontainers connection string, a test JWT secret) via
  `ConfigureWebHost`/`ConfigureAppConfiguration`, but those overrides are only visible to code that
  reads `IConfiguration` after the host is fully built. Reading a config value eagerly in
  `Program.cs` top-level statements (e.g. `var secret = builder.Configuration["Jwt:Secret"]` used
  directly in a closure) silently captures the pre-override value — this previously broke JWT
  validation and DB connectivity for every authenticated integration test with no useful error
  (401s / connection failures with a correct-looking token). Use `AddOptions<T>().Configure<IConfiguration>((opts, config) => ...)`
  or resolve `IConfiguration` inside a factory delegate (see `AddJwtBearer`'s `JwtBearerOptions`
  setup and `AddInfrastructure`'s `AddDbContext` in `DependencyInjection.cs`) for anything that
  must reflect test-time overrides.
- **Dual authentication schemes for endpoints a packaged game must call.** `"Bearer"` (JWT, for the
  web dashboard) and `"ForgeQAProjectKey"` (a Project-scoped API key, for the Unreal runtime) are
  both registered in `Program.cs`; an endpoint that must accept either declares
  `[Authorize(AuthenticationSchemes = "Bearer,ForgeQAProjectKey")]` explicitly — plain `[Authorize]`
  means JWT-only. The API key travels as `X-ForgeQA-Key`, never `Authorization: Bearer`, so it can
  never collide with JWT parsing on a dual-scheme endpoint. Resolve "who is the caller" via
  `ClaimsPrincipal.ToBugReportAuthor()` (`BugReportAuthorExtensions`), which checks for API-key
  claims first and falls back to the JWT `sub`; branch application-layer authorization on the
  resulting `BugReportAuthor.IsApiKey` rather than re-deriving auth-method from raw claims in
  service code. See `docs/bug-reporting.md` for the full model (hashing, scopes, revocation).
- **`IObjectStorage` is the generic storage primitive; `IArtifactStorage` extends it with
  multipart-only operations.** Introduced in M4 when Bug Reporting needed simple single-PUT
  screenshot storage without pulling in Build Distribution's multipart-upload machinery. A new
  storage consumer should depend on `IObjectStorage` unless it genuinely needs multipart (large
  file) semantics — don't add single-shot methods to `IArtifactStorage` or bypass the interface
  split. Both interfaces are implemented by the same `S3ArtifactStorage` singleton, registered once
  and exposed under both service types in DI.
- **A Project API key's scope must be checked explicitly wherever it matters — never inferred from
  "the key exists and matches this Project."** M4 shipped `BugService` checking only
  `ApiKeyProjectId == projectId`, which was harmless while `BUG_REPORT_WRITE` was the only scope in
  existence, but became a real authorization gap the moment M5 added `TELEMETRY_WRITE` (a
  telemetry-only key could still submit bug reports). `BugReportAuthor`/`TelemetryIngestionCaller`
  both carry the caller's scopes for exactly this reason — when adding a new scoped capability,
  audit every existing `AuthorizeAsync`-style method for the same "any key from this Project is
  good enough" shortcut and add the explicit `HasScope`/`HasApiKeyScope` check. See
  docs/telemetry.md's Security section for the specific fix and its regression test.
- **Machine-only ingestion endpoints use a single authentication scheme, not the dual-scheme
  pattern.** Bug Reporting accepts either JWT or Project key (a human can also file a bug via the
  dashboard); Telemetry ingestion accepts **only** `ForgeQAProjectKey` — there is no
  dashboard-user equivalent for starting/ingesting/ending a session, so
  `[Authorize(AuthenticationSchemes = ProjectApiKeyDefaults.AuthenticationScheme)]` alone is
  correct there. Don't default every new runtime-facing endpoint to the dual-scheme pattern just
  because Bug Reporting used it — match the endpoint's actual caller population.
- **Idempotent create endpoints for client-generated identifiers**: `TelemetryService.StartSessionAsync`
  looks up by the caller-supplied natural key (`ProjectId + RuntimeSessionId`) before constructing a
  new entity, returning the existing one on a match (and rejecting a mismatched secondary attribute,
  e.g. a different `BuildId`, as a `409 Conflict` rather than silently mutating identity). Follow
  this shape for any future endpoint whose caller might retry after a dropped response.
- **Batch ingestion validates the entire payload before writing anything, and treats a
  previously-accepted item as a no-op duplicate rather than an error.** See
  `TelemetryService.IngestEventsAsync`: one bad event fails the whole batch (simpler for a client to
  retry against), while a duplicate `SequenceNumber` already persisted for the session is silently
  skipped and counted, computed via a single existence-check query before any `AddRange` — never a
  per-row `SaveChanges` and never a database unique-constraint violation used as control flow.

### Testing

- Unit tests cover Domain invariants (entity constructors/guard clauses) and Application-layer
  authorization/business logic using in-memory fake repositories (see
  `tests/ForgeQA.UnitTests/Application/Fake*Repository.cs`) — no EF Core, no database.
- Integration tests spin up a real ASP.NET Core host (`WebApplicationFactory<Program>`) against a
  disposable Testcontainers PostgreSQL instance per test class (`ForgeQAWebApplicationFactory`).
  They exercise real HTTP requests end-to-end, including auth, authorization, and cross-resource
  IDOR checks (e.g. a build belonging to Project B is never reachable through Project A's route).
  `tests/ForgeQA.IntegrationTests/xunit.runner.json` disables cross-class parallelization
  deliberately — running multiple Testcontainers Postgres instances concurrently is expensive and
  was a source of flakiness; keep this setting when adding new integration test classes.
- Both require the connection string / JWT secret to be genuinely overridable at runtime — see the
  lazy-config-read convention above before adding new startup-time configuration reads.

## Frontend architecture

- App Router pages under `src/app/`; a client-side `AuthProvider` (`src/lib/auth/auth-context.tsx`)
  holds the JWT/refresh token pair in `localStorage` and gates the `/app/*` routes via
  `components/auth-guard.tsx` — there is no server-side session/middleware auth.
- All backend calls go through `src/lib/api/*.ts` (one file per backend resource: `auth.ts`,
  `organizations.ts`, `projects.ts`, `builds.ts`), which wrap a shared `apiClient`
  (`src/lib/api/client.ts`) that attaches the bearer token and normalizes errors into `ApiError`.
  Don't call `fetch` directly from components — add a method to the relevant API module instead.
- Remote state is TanStack Query; each resource has its own query-key convention (e.g.
  `["builds", projectId, { page, search, platform, configuration, status }]` for the filtered
  build list) — invalidate the resource's base key (`["builds", projectId]`) after a mutation
  rather than manually patching cached data.
- Enum-like backend values (build platform/configuration) get human-readable labels via small
  `Record` maps in `lib/api/builds.ts` (`PLATFORM_LABELS`, `CONFIGURATION_LABELS`) — the backend
  wire format stays uppercase/raw; presentation formatting is a frontend-only concern.
- Tests use Vitest + `@testing-library/react`, configured in `vitest.config.ts`
  (`jsdom` environment, `@` path alias matching `tsconfig.json`). Mock `next/navigation` and the
  relevant `lib/api/*` module per test file (see
  `src/app/app/projects/[projectId]/builds/__tests__/BuildsPage.test.tsx` for the pattern),
  wrapping the component under test in a fresh `QueryClientProvider`
  (`__tests__/test-utils.tsx`'s `renderWithQueryClient`).
- `frontend/AGENTS.md` (auto-generated by `next dev`, do not hand-edit) warns that this Next.js
  version (16) has breaking changes from training-data assumptions — check
  `node_modules/next/dist/docs/` before relying on remembered Next.js APIs, especially around
  routing, params, and caching.

## Unreal plugin architecture (`unreal-plugin/ForgeQA/`)

Two modules, one-directional dependency:

- **`ForgeQA`** (Runtime, `Source/ForgeQA/`) — everything a packaged game needs: settings
  (`UForgeQASettings`), the Blueprint-visible `FForgeQABuildContext`, manifest read/write
  (`ForgeQABuildManifest.h/.cpp`), the HTTP client and its separated JSON parsing
  (`ForgeQAApiClient` / `ForgeQAApiResponseParser` — kept apart so parsing is unit-testable with
  literal JSON strings, no live server needed), and `UForgeQASubsystem`. Depends only on
  `Core`/`CoreUObject`/`Engine`/`HTTP`/`Json`/`JsonUtilities`/`DeveloperSettings`/`Projects` —
  **never** `UnrealEd`/`Slate`/`ToolMenus`. Enforce this boundary for any new runtime code.
- **`ForgeQAEditor`** (Editor-only, `Source/ForgeQAEditor/`) — the Tools > ForgeQA Slate panel,
  `FForgeQAEditorSession` (login + the single reusable binding-validation routine used by both
  "Validate" and "Apply" — don't duplicate that check elsewhere), and the
  `ForgeQAGenerateBuildManifest` commandlet for future CI use.

**Build identity resolution never contacts the ForgeQA API at runtime** (a packaged build must
know its own identity offline). Precedence, implemented in `UForgeQASubsystem`'s public static
`TryResolveFrom*` methods (kept public specifically so automation tests can call them without a
live `UGameInstance`): command-line override (`-ForgeQAProjectId=` / `-ForgeQABuildId=`) → packaged
manifest (`Content/ForgeQA/ForgeQABuild.json`) → Project Settings (`ProjectId` only — Settings
alone can never supply a `BuildId`). See `docs/unreal-integration.md` for the full rationale,
including why `UForgeQASettings.DefaultBuildId` is a plain (per-developer, gitignored) `config`
property while `ApiBaseUrl`/`ProjectId` are `defaultconfig` (committed) — that split is deliberate,
not an oversight.

This environment has no Unreal Engine installation: the plugin's C++ has never been compiled here.
Treat any Unreal-side change as unverified until someone runs it against a real UE 5.8 install —
don't claim compilation or editor behavior succeeded without actually having run it.

### Bug reporting subsystem (M4, `Source/ForgeQA/`)

`UForgeQABugReportingSubsystem` is a separate `UGameInstanceSubsystem` from `UForgeQASubsystem` —
identity/Build-Context resolution and bug submission are different responsibilities, kept apart
deliberately rather than growing one subsystem into a God object. It reads the Build Context and
runtime API key (`FForgeQARuntimeCredentials::ResolveApiKey()`, precedence: command-line
`-ForgeQAApiKey=` → `FORGEQA_API_KEY` env var → `UForgeQASettings::DevelopmentRuntimeApiKey`,
a per-developer `config` property, never `defaultconfig`) rather than owning either itself.
`UForgeQABugReportWidget` is a `UUserWidget` **C++ base class only** — its visual layout is a
Widget Blueprint `.uasset` that must be authored inside the Editor and cannot be produced by this
repository's text-based tooling; don't attempt to fabricate one. See `docs/bug-reporting.md` for
the full submission state machine and screenshot upload flow.

### Telemetry subsystem (M5, `Source/ForgeQA/`)

`UForgeQATelemetrySubsystem` is a third, separate `UGameInstanceSubsystem` — distinct from both
`UForgeQASubsystem` (identity only) and `UForgeQABugReportingSubsystem` (bug submission only),
because telemetry has its own always-running lifecycle (queue/batch/flush/retry) that would bloat
either of the other two. It reuses `UForgeQASubsystem`'s Build Context/`RuntimeSessionId` and
`FForgeQARuntimeCredentials` exactly as `UForgeQABugReportingSubsystem` does — never a second
identity or credential path. `TrackEvent()` never touches the network directly (it validates,
assigns a sequence number, and enqueues); flushing happens on a size trigger or a timer, with at
most one flush in flight and bounded exponential backoff on transient failures only (network/5xx/
429 — never 400/401/403, which are dropped and logged instead of retried forever). See
`docs/telemetry.md` for the full queue/retry/shutdown behavior and the Blueprint/C++ API surface.

## Docker Compose services

`docker-compose.yml` defines `postgres`, `minio` (backs Build Distribution's S3-compatible artifact
storage and, since M4, Bug Reporting's screenshot storage via the shared `IObjectStorage`
abstraction — see `docs/build-distribution.md` and `docs/bug-reporting.md`), `backend`, and
`frontend`, wired together via environment variables sourced from `.env` (copy from
`.env.example`, never commit `.env`). Telemetry (M5) stores events directly in PostgreSQL — it does
not add or use any new Compose service.
