# Phase 1B — Headless host foundation

This phase provides a new .NET 10 executable and a framework-independent content-layout library. It is intentionally **not a rule-serving release**.

## Runtime boundaries

- `RulesCore.Host` uses ASP.NET Core Minimal APIs and Kestrel without MVC, UI, database, source import, or rule resolution.
- `RulesCore.Content` contains the native-path contract and catalog-readiness interface; it has no ASP.NET dependency.
- `/health` returns HTTP 200 when the host can serve requests. It is process liveness, not content readiness.
- `/ready` returns HTTP 503 with `native_catalog_not_loaded` until Phase 2 supplies a validated, immutable native snapshot. Presence of files or a database connection is insufficient.
- No `/api` endpoints are mapped. Both public consumer and private Rules Wiki routes return 404. The planned authentication integration must not be bypassed by adding private routes prematurely.

## Configuration

| Key | Default | Meaning |
| --- | --- | --- |
| `Rulesets:ContentRoot` | `data/rulesets` | Root containing ruleset IDs |
| `Rulesets:DefaultRuleset` | `first-party-001` | Internal, provisional ruleset ID |
| `RulesCore:ApiSurface` | `PublicOnly` | Exactly `PublicOnly` or `PrivateOnly` |

Standard .NET environment-variable nesting works, e.g., `Rulesets__ContentRoot` and `RulesCore__ApiSurface`. Empty roots, non-path-safe IDs, and legacy `Compatibility`/`Combined` modes fail startup. The native directory is resolved strictly as `<ContentRoot>/<DefaultRuleset>/native`; there is no probing or fallback to `working/`.

Missing content is not a configuration error in Phase 1B. The process starts with liveness 200 and readiness 503. PostgreSQL is not initialized and no connection string is required.

## Verification

```sh
dotnet restore RulesCore.slnx
dotnet build RulesCore.slnx -c Release --no-restore
dotnet test RulesCore.slnx -c Release --no-build
docker build -t rules-core-phase1:local .
```

Tests cover both surface settings, startup/shutdown, invalid configuration, health/readiness truthfulness, non-exposure of public/private API paths, and the native-only path contract. A branch-scoped CI workflow also builds and smoke-tests the image without deploying it.

## Phase 1A and Phase 2 integration handoff

1. Merge/rebase Phase 1A's reviewed data split into the eventual integration branch separately. This phase does not alter `data/`, any Python reconciliation tools/tests, or `.github/workflows/validate.yml`.
2. Reconcile the approved manifest contract at `data/rulesets/first-party-001/manifest.json`, including authority and snapshot semantics. The host does not currently parse it.
3. In Phase 2, implement validation and immutable indexes in `RulesCore.Content` with reads explicitly confined to the selected ruleset's `native/`; never enumerate or promote `working/`. Consider symlink/canonical-path containment when content is mounted from untrusted sources.
4. Replace `UnavailableNativeCatalogReadiness` only after a usable validated native snapshot exists; make snapshot lifecycle and error behavior explicit.
5. Phase 3A must implement Site ticket redemption, exact `rules-wiki` private provenance, domain authorization and pair-specific private networking before any private API endpoint is introduced. Public and private deployments remain distinct.
6. Deployment Compose files and the disabled deployment workflow remain unchanged. No deployment, public exposure or merge is authorized by this phase.
