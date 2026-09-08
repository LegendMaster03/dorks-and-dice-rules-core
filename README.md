# Dorks & Dice Rules Core

Rules Core is the independent rules aggregation, normalization, adjudication, and API service for Dorks & Dice.

## Initial stack

- .NET 10 / ASP.NET Core
- PostgreSQL through Entity Framework Core and Npgsql
- 5e.tools-compatible rule documents with Rules Core extensions where required
- Docker
- xUnit integration and boundary tests

The PostgreSQL database is deployment-owned and runs independently of the Rules Core application container. Runtime source payloads, user-provided material, caches, database files, credentials, and access-controlled content are never committed to this repository.

## Projects

- `RulesCore.Domain` - domain concepts and invariants.
- `RulesCore.Application` - use cases and application contracts.
- `RulesCore.Infrastructure` - PostgreSQL, source ingestion, storage, and external integrations.
- `RulesCore.Web` - HTTP API, health endpoints, standalone host, and Dorks & Dice frontend module.
- `RulesCore.Tests` - domain/application boundary tests.
- `RulesCore.IntegrationTests` - HTTP and hosting-contract tests.

## Development

```bash
dotnet restore dorks-and-dice-rules-core.slnx
dotnet test dorks-and-dice-rules-core.slnx --configuration Release
dotnet run --project src/RulesCore.Web
```

Set `ConnectionStrings__RulesCore` to a PostgreSQL connection string before database-backed work. The service can start without a database during early standalone development; `/ready` reports unavailable until PostgreSQL is configured and reachable.

## Source Layer

The first Source Layer vertical slice persists package, work, edition, entity, and immutable entity-revision records in PostgreSQL. `ISourceImportService` accepts 5e.tools-shaped JSON, preserves complete entity objects as JSONB, computes stable SHA-256 content fingerprints, and creates a new revision only when source content changes.

The initial read API exposes only source packages explicitly marked public:

- `GET /api/sources`
- `GET /api/sources/entities/{entityId}`

Restricted-source grants and authenticated mutation endpoints are intentionally not part of this slice. There is no public HTTP import endpoint; ingestion remains behind the application service until Tool Host authentication and source-access enforcement are wired into Rules Core.

See `docs/source-layer.md` for the persistence, fingerprinting, provenance, and access boundaries.

## Deployment

Pushes to `main` trigger `.github/workflows/deploy.yml`, which follows the same self-hosted deployment pattern as the main Dorks & Dice site. The workflow runs the test suite against disposable PostgreSQL 18, builds and smoke-tests `dorks-and-dice-rules-core:latest`, then recreates the production container through Docker Compose.

The production workflow expects:

- a self-hosted GitHub Actions runner with the `dorks-and-dice-rules-core` label;
- `/mnt/HDDs/www/dorks-and-dice-rules-core/.env` containing `ConnectionStrings__RulesCore`;
- the external Docker network `dorks-and-dice-backend`;
- the production PostgreSQL database referenced by the deployment-only connection string.

The production database and credentials are not created, modified, or stored by the workflow.

## Tool hosting

The existing Dorks & Dice Tool Host supports both embedded modules and proxied applications. Rules Core currently exposes:

- `/health` - liveness endpoint used by the host/deployment.
- `/ready` - database-aware readiness endpoint.
- `/app.js` - minimal ES module for the existing Embedded Module integration.
- `/` and `/api` - standalone service metadata.

The host strips browser Cookie and Authorization headers before proxying and does not support WebSockets or tool-owned cookie sessions. Rules Core therefore does not use Blazor Server or a separate Identity store. Dorks & Dice remains the production identity/authorization authority; Rules Core will consume the host's authenticated Tool gateway contract rather than infer identity from browser-controlled headers.

See `docs/architecture.md` and `docs/tool-hosting.md` for the current boundaries.
