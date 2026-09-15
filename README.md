# ForgeQA

ForgeQA is an open-source, developer-first platform for quality assurance in game development. It
is designed to bring build distribution, playtesting, contextual bug reporting, telemetry,
performance analytics, crash reporting, CI/CD, and automated QA into one workflow.

The first integration target is Unreal Engine, while the backend remains engine-agnostic.

> **Project status:** ForgeQA is under active development. The repository is currently at **Milestone
> 1 — Build Registry** and is not yet production-ready.

## What works today

```text
Register or log in → create an organization → create a project → register and manage builds
```

Included today:

- ASP.NET Core API with JWT authentication and refresh tokens.
- Organization membership and role storage.
- Project creation and listing.
- Build Registry: register, list (paginated/filtered/searched), view, edit, archive, and restore
  builds, scoped to project membership. See [`docs/build-registry.md`](docs/build-registry.md).
- PostgreSQL persistence with Entity Framework Core migrations.
- Next.js web frontend with login, registration, project, and build management views.
- Docker Compose development environment for PostgreSQL, MinIO, the API, and the frontend.
- Unit and integration test projects (backend) and a Vitest + Testing Library suite (frontend).
- Initial Avalonia launcher and Unreal Engine plugin scaffolds.

The planned roadmap is:

```text
M0 Foundation → M1 Build Registry → M2 Build Distribution → M3 Unreal Integration →
M4 Bug Reporting → M5 Telemetry → M6 Performance → M7 Crash Reporting → M8 CI/CD →
M9 Automated QA → M10 Regression Detection → M11 Integrations → M12 AI
```

## Architecture

The backend uses a modular Clean Architecture-inspired layout:

```text
ForgeQA.Domain          Core entities and rules; no framework dependencies
        ↑
ForgeQA.Application     Use cases, DTOs, and application abstractions
        ↑
ForgeQA.Infrastructure  EF Core, PostgreSQL, Identity, JWT, and repositories
        ↑
ForgeQA.Api             HTTP controllers, middleware, Swagger, and health checks
```

The repository also contains the following clients and integrations:

```text
frontend/                Next.js + TypeScript web application
launcher/                Avalonia desktop launcher scaffold
unreal-plugin/           Unreal Engine 5.8 plugin scaffold
infrastructure/          Infrastructure configuration reserved for future use
docs/                    Project documentation
```

## Requirements

- .NET SDK 10.0 or later.
- Node.js 20 or later (CI currently uses Node.js 24).
- Docker Desktop with Docker Compose.
- Unreal Engine 5.8, only when working on the Unreal plugin.

## Quick start with Docker

1. Copy the example environment file:

   ```bash
   cp .env.example .env
   ```

2. Set a real `JWT_SECRET` and strong local database and MinIO passwords in `.env`.

3. Start the development stack:

   ```bash
   docker compose up --build
   ```

The services are available at:

| Service | URL |
| --- | --- |
| Frontend | http://localhost:3000 |
| API | http://localhost:5000 |
| Swagger UI | http://localhost:5000/swagger |
| API health check | http://localhost:5000/health |
| MinIO console | http://localhost:9001 |

The API applies pending EF Core migrations during startup.

## Local development

Start only the database when running the applications locally:

```bash
docker compose up postgres -d
```

### Backend

```bash
cd backend
dotnet restore
dotnet run --project src/ForgeQA.Api
```

For migrations and local database work, use:

```bash
dotnet ef database update --project src/ForgeQA.Infrastructure --startup-project src/ForgeQA.Api
```

Configure the connection string and `Jwt:Secret` in
`backend/src/ForgeQA.Api/appsettings.Development.json` or with environment variables.

### Frontend

```bash
cd frontend
cp .env.local.example .env.local
npm ci
npm run dev
```

### Launcher

```bash
cd launcher/ForgeQA.Launcher
dotnet run
```

### Unreal plugin

Copy or symlink `unreal-plugin/ForgeQA` into
`<YourUnrealProject>/Plugins/ForgeQA`, then enable **ForgeQA** in the Unreal Editor's Plugins
window. The current plugin only registers an empty runtime module.

## Tests and checks

Run backend tests from `backend/`:

```bash
dotnet test tests/ForgeQA.UnitTests
dotnet test tests/ForgeQA.IntegrationTests
```

Integration tests use Testcontainers and require Docker.

Run frontend checks from `frontend/`:

```bash
npm run lint
npx tsc --noEmit
npm run build
npm run test
```

The same checks run in GitHub Actions for pushes and pull requests targeting `main`. The Unreal
plugin is not compiled in CI because that requires a full Unreal Engine installation.

## API endpoints

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/auth/register` | No | Register and create a personal organization |
| `POST` | `/api/auth/login` | No | Authenticate and issue access and refresh tokens |
| `POST` | `/api/auth/refresh` | No | Exchange a refresh token |
| `POST` | `/api/auth/logout` | No | Revoke a refresh token |
| `GET` | `/api/auth/me` | Yes | Get the current user |
| `GET` | `/api/organizations` | Yes | List the user's organizations |
| `GET` | `/api/organizations/{organizationId}` | Yes | Get an organization |
| `POST` | `/api/organizations/{organizationId}/projects` | Yes | Create a project |
| `GET` | `/api/organizations/{organizationId}/projects` | Yes | List organization projects |
| `GET` | `/api/projects/{projectId}` | Yes | Get a project |
| `POST` | `/api/projects/{projectId}/builds` | Yes | Register a build |
| `GET` | `/api/projects/{projectId}/builds` | Yes | List a project's builds (paginated/filtered/searched) |
| `GET` | `/api/projects/{projectId}/builds/{buildId}` | Yes | Get a build |
| `PATCH` | `/api/projects/{projectId}/builds/{buildId}` | Yes | Update editable build metadata |
| `POST` | `/api/projects/{projectId}/builds/{buildId}/archive` | Yes | Archive a build |
| `POST` | `/api/projects/{projectId}/builds/{buildId}/restore` | Yes | Restore an archived build |
| `GET` | `/health` | No | Check API and PostgreSQL health |

See [`docs/build-registry.md`](docs/build-registry.md) for field semantics, filters, and example requests.

## Contributing

Contributions are welcome. Before opening a pull request:

1. Create a focused branch from `main`.
2. Keep changes scoped and add or update tests when behavior changes.
3. Run the relevant backend and frontend checks locally.
4. Describe the motivation, implementation, and verification in the pull request.

Please do not commit secrets, `.env` files, dependency folders, build output, or generated IDE
files. See [`.gitignore`](.gitignore) for the repository policy.

## Security

Do not report security vulnerabilities in public issues. Use the repository's private security
reporting channel when one is configured, or contact the maintainers privately with reproduction
details and an appropriate disclosure window.

## License

ForgeQA is distributed under the terms of the [GNU General Public License v3.0](LICENSE).
