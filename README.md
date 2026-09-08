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

The Source Layer persists package, work, edition, entity, immutable entity-revision, and per-user source-grant records in PostgreSQL. `ISourceImportService` accepts 5e.tools-shaped JSON, preserves complete entity objects as JSONB, computes stable SHA-256 content fingerprints, and creates a new revision only when source content changes.

The source read API is access-aware:

- `GET /api/sources`
- `GET /api/sources/entities/{entityId}`

Anonymous/direct requests see only public packages. Hosted requests redeem the main site's one-time Tool authentication ticket and use the resulting stable user ID to include private packages for which Rules Core stores an explicit `user_source_grant`. Missing and inaccessible private entities both return not-found behavior.

Grant mutation and source import remain internal application services for now; there is no unauthenticated write endpoint.

See `docs/source-layer.md` for the persistence, fingerprinting, provenance, and access boundaries.

## Global Rules Layer

Stable `rule_concept` records provide source-independent rule identities. A concept can bind one or more source-specific entities, while append-only global decisions select an exact immutable source revision. Reimporting changed source material therefore does not silently alter an already-adjudicated rule.

Rules Lawyers can create concepts, bind source entities, set the current source-selection decision, and publish immutable global ruleset revisions through authenticated Tool Host requests. Global mutation requires both the `dorks-and-dice` site mode and the effective `Rules Lawyer` role.

`GET /api/rules/{conceptKey}` resolves the concept from the latest published ruleset and returns exact decision/source provenance. The source document is returned only when the caller independently satisfies Source Layer access control. Rules Lawyer authority does not grant restricted source access, and a source grant does not grant Rules Lawyer authority.

Publishing the same effective decisions twice is idempotent. Source changes become visible in the resolved ruleset only after a Rules Lawyer selects the new source revision and publishes a new ruleset revision.

See `docs/rules-layer.md` for concept identity, decision history, publication, authority, and resolution behavior.

## Deployment

Pushes to `main` trigger `.github/workflows/deploy.yml`, which follows the same self-hosted deployment pattern as the main Dorks & Dice site. The workflow runs the test suite against disposable PostgreSQL 18, builds and smoke-tests `dorks-and-dice-rules-core:latest`, then recreates the production container through Docker Compose.

The production workflow expects:

- a self-hosted GitHub Actions runner with the `dorks-and-dice-rules-core` label;
- `/mnt/HDDs/www/dorks-and-dice-rules-core/.env` containing `ConnectionStrings__RulesCore`;
- the external Docker network `dorks-and-dice-backend`;
- the production PostgreSQL database referenced by the deployment-only connection string.

The production database and credentials are not created, modified, or stored by the workflow.

## Tool hosting

The Dorks & Dice Tool Host supports both embedded modules and proxied applications. Rules Core currently exposes:

- `/health` - liveness endpoint used by the host/deployment.
- `/ready` - database-aware readiness endpoint.
- `/app.js` - minimal ES module for the existing Embedded Module integration.
- `/` and `/api` - standalone service metadata.
- `/api/integration/session` - authenticated backend view of the redeemed Tool Host context.

The host strips browser Cookie, Authorization, forwarding, and reserved Tool-auth headers before proxying. For authenticated backend requests it injects a short-lived one-time ticket. Rules Core redeems that ticket against the main site's fixed `rules-core` introspection endpoint before accepting the supplied identity, global roles, or campaign memberships.

Rules Core does not use Blazor Server or a separate production Identity store. Dorks & Dice remains the identity and change-authority source; Rules Core separately owns restricted source-content grants.

See `docs/architecture.md` and `docs/tool-hosting.md` for the current boundaries.
