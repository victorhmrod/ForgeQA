# Build Registry (Milestone 1)

## What a Build represents

A `Build` is a first-class ForgeQA domain entity that records the metadata of one registered
game/application build for a `Project`. It always belongs to exactly one project, which in turn
belongs to exactly one organization — the same membership-based authorization used for projects
applies transitively to every build.

A Build is **metadata only**. Milestone 1 does not store or serve binaries, installers, or any
other artifact — see [Scope boundary](#scope-boundary-m1-vs-m2) below.

## Why build identity matters

`Build.Id` is a stable, permanent correlation identifier. Future milestones will attach data to a
specific build — downloadable artifacts (M2), tester releases (M2), bug reports (M4), crash
reports (M7), telemetry sessions (M5), and performance measurements (M6) — all by referencing
`Build.Id`. This is why:

- Builds are never hard-deleted. They are archived instead, so historical references from later
  milestones never dangle.
- The database primary key (`Id`, a GUID) is the canonical identity — `version` alone is **not**
  assumed unique, since the same version can be rebuilt for different platforms, configurations,
  or CI attempts.

## Logical identity and duplicate prevention

The combination `(projectId, buildNumber, platform, configuration)` is treated as the build's
logical identity within a project: the API rejects an exact duplicate registration with
`409 Conflict`, and the database additionally enforces this with a unique index
(`IX_Builds_ProjectId_BuildNumber_Platform_Configuration`) as a second line of defense.

## Metadata fields

| Field | Required | Notes |
| --- | --- | --- |
| `name` | No | Human-readable label (e.g. "Gamescom Demo"). Not an identifier. |
| `version` | Yes | Free-form string, up to 100 characters. Not forced into SemVer. |
| `buildNumber` | Yes | Free-form string, up to 100 characters (e.g. `jenkins-891`, `CL-182901`). |
| `platform` | Yes | Enum: `WINDOWS`, `LINUX`, `MACOS`. |
| `configuration` | Yes | Enum: `DEBUG`, `DEBUG_GAME`, `DEVELOPMENT`, `TEST`, `SHIPPING`. |
| `branch` | No | Source-control branch name, up to 200 characters. |
| `commitSha` | No | Source-control revision identifier, up to 100 characters. Not restricted to 40-hex Git SHAs, since other SCMs may be added later. |
| `engineVersion` | No | Free-form string (e.g. `UE 5.8.2`, `Unity 6.1`). Not restricted to Unreal. |
| `changelog` | No | Free-form notes, up to 4000 characters. |

`name`, `version`, `branch`, `commitSha`, `engineVersion`, and `changelog` can be edited after
creation. `buildNumber`, `platform`, and `configuration` are part of the logical identity and are
immutable once a build is registered — they are not accepted by the update endpoint.

## Lifecycle

A build has exactly two states:

- **Active** (`archivedAt = null`) — the default state, shown in the default build list.
- **Archived** (`archivedAt` set) — hidden from the default list, but still retrievable by ID and
  reachable through an explicit filter. Archiving is reversible via restore.

Milestone 1 deliberately does not introduce upload/processing/deployment-style statuses (e.g.
"Uploading", "Deploying") since no artifact pipeline exists yet — those belong to M2.

## Authorization model

Every build operation is scoped through its project:

1. The project referenced by the route must exist (`404` otherwise).
2. The caller must be a member of that project's organization (`403` otherwise).
3. For single-build operations, the build is looked up **by `(projectId, buildId)` together** —
   a build that exists but belongs to a different project resolves as `404`, exactly like a build
   that doesn't exist at all. This prevents an authorized user from probing build IDs that belong
   to organizations/projects they have no access to (IDOR).

## API surface

All endpoints require a bearer token and are nested under a project, matching the existing
`/api/projects/{projectId}/...` convention from Milestone 0.

| Method | Path | Description |
| --- | --- | --- |
| `POST` | `/api/projects/{projectId}/builds` | Register a build |
| `GET` | `/api/projects/{projectId}/builds` | List builds (paginated, filterable, searchable) |
| `GET` | `/api/projects/{projectId}/builds/{buildId}` | Get a single build |
| `PATCH` | `/api/projects/{projectId}/builds/{buildId}` | Update editable metadata |
| `POST` | `/api/projects/{projectId}/builds/{buildId}/archive` | Archive a build |
| `POST` | `/api/projects/{projectId}/builds/{buildId}/restore` | Restore an archived build |

### List query parameters

| Parameter | Default | Notes |
| --- | --- | --- |
| `page` | `1` | 1-based |
| `pageSize` | `20` | Capped at 100 |
| `platform` | none | Exact match, one of `WINDOWS`/`LINUX`/`MACOS` |
| `configuration` | none | Exact match, one of `DEBUG`/`DEBUG_GAME`/`DEVELOPMENT`/`TEST`/`SHIPPING` |
| `status` | `active` | `active`, `archived`, or `all` |
| `search` | none | Case-insensitive substring match across name, version, build number, branch, and commit SHA (executed in PostgreSQL via `ILIKE`, not in application memory) |

Sorting is fixed (`createdAt DESC`, `id DESC` as a stable tiebreaker) — there is no
caller-configurable sort parameter, which avoids ever needing an allowlist for one.

### Example: register a build

```bash
curl -X POST http://localhost:5000/api/projects/{projectId}/builds \
  -H "Authorization: Bearer <access-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "QA Candidate",
    "version": "0.4.2",
    "buildNumber": "1842",
    "platform": "WINDOWS",
    "configuration": "DEVELOPMENT",
    "branch": "main",
    "commitSha": "a941de3",
    "engineVersion": "UE 5.8.2",
    "changelog": "Inventory replication fixes and movement improvements."
  }'
```

### Example: list active Windows builds, page 2

```bash
curl "http://localhost:5000/api/projects/{projectId}/builds?platform=WINDOWS&status=active&page=2&pageSize=20" \
  -H "Authorization: Bearer <access-token>"
```

## Scope boundary (M1 vs M2)

Milestone 1 stores and serves build **metadata only**. The following are explicitly out of scope
for this milestone and belong to Milestone 2 — Build Distribution:

- Executable/artifact upload and storage (including MinIO/S3 usage — MinIO is provisioned in
  `docker-compose.yml` but unused by any M1 endpoint).
- Signed download URLs, CDN delivery, patching, or installation.
- Tester distribution, release channels, or build invitations.
- Any ingestion from CI systems, Unreal Automation Tool, or GitHub.

## Frontend

- `Project / Builds` (`/app/projects/{projectId}/builds`) — table of builds with search, platform
  filter, configuration filter, status filter, pagination, a "Register Build" action, and an
  empty state explaining why builds matter for later milestones.
- `Project / Builds / {buildId}` (`/app/projects/{projectId}/builds/{buildId}`) — build detail
  view (Build / Source / Environment / Changelog / Metadata sections) with edit, archive, and
  restore actions.
