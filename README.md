# Dorks & Dice — Rules Core

Rules Core is the headless rules-content service and future mechanical execution engine for the Dorks & Dice tabletop system. [Rules Wiki](https://github.com/LegendMaster03/dorks-and-dice-rules-wiki) is a separate user-facing application.

## Rewrite implementation status

`feature/rules-core-rewrite` contains the reviewed Phase 1A native/working data separation and Phase 1B C#/.NET 10 host foundation. The previous implementation remains on `main` as historical reference, not a second runtime to deploy. **The new host boots but does not yet load or serve rules**. Phase 2 will implement the native catalog and Phase 3 will connect authorized consumers.

No gameplay evaluator, rules database integration, public/private rule endpoint, or Rules Wiki integration has been implemented in this rewrite. PostgreSQL is not required to start the host. Production deployment remains intentionally disabled; do not enable public/tester access or merge to `main` without separate authorization.

## Data layout and authority

- `data/rulesets/first-party-001/manifest.json` — provisional internal ruleset identity and layout.
- `data/rulesets/first-party-001/native/` — **only** approved, authoritative native rules. This is the **only** eventual runtime rules source.
- `data/rulesets/first-party-001/working/` — inert drafts, staged SRD candidates, incomplete reconciliations, and identity aliases. Never load or fall back to this directory in the runtime.
- `data/reference/` — pinned imported evidence and licensing/source provenance; not authoritative runtime mechanics.
- `data/conversions/` — reviewed conversion mappings for later phases.
- `data/schemas/` — future native schema documentation and validation.

Each native rule has a stable identity independent of its file path. Approval is determined by the controlled working-to-native promotion workflow, **not** by a runtime status detector or per-rule publication allowlist. Preserve source history independently from the authoritative native definition.

The Phase 1A audit documents 35 authoritative records, 2,626 distinct working identities, and lossless preservation of 2,522 SRD coverage entries at the time of migration. See `docs/rewrite/phase-1a-audit.md` and `docs/rewrite/phase-1a-native-working-ledger.json`; these counts are historical snapshots and change as approved rules are promoted.

## .NET 10 host foundation

- `src/RulesCore.Host/` — ASP.NET Core Minimal API/Kestrel host, with structured logging, configuration validation, and graceful shutdown.
- `src/RulesCore.Content/` — transport-independent native path and readiness contracts.
- `tests/RulesCore.Host.Tests/` — startup, configuration, native-only path, and endpoint exposure tests.
- `RulesCore.slnx` and `Dockerfile` — buildable .NET service and container image.
- `docs/rewrite/phase-1b-host.md` — Phase 1B behavior and Phase 2/3 handoff.

Run `dotnet restore RulesCore.slnx`, `dotnet build RulesCore.slnx -c Release`, and `dotnet test RulesCore.slnx -c Release`. Native data validation runs in `.github/workflows/validate.yml`; host tests and Docker smoke tests run in `.github/workflows/phase1-host-ci.yml`.

At present `GET /health` returns HTTP 200 (process alive), `GET /ready` returns HTTP 503 (native catalog not yet loaded), and rule API paths are unmapped (HTTP 404). These are intentional. `RulesCore:ApiSurface` accepts only `PublicOnly` or `PrivateOnly`; future private routes must implement Site ticket redemption, exact authorized tunnel-source verification, and per-user authorization before exposure.

## Next steps

**Phase 2:** load and validate only `native/` into an immutable indexed catalog, support consolidated JSON category files and individual entities, test authoritative identity integrity and search, and activate readiness only when a usable snapshot exists. **Phase 3:** implement distinct public/private APIs and the existing Rules Wiki contracts without bypassing the Site private Tool tunnel.

Game mechanics and source conversions are implemented later, subsystem by subsystem, according to approved rules decisions. Unresolved SRD candidate definitions remain inert.
