# Dorks & Dice — Rules Core

Rules Core is the headless native rules engine and rules-content service for the Dorks & Dice tabletop system. The separately hosted [Rules Wiki](https://github.com/LegendMaster03/dorks-and-dice-rules-wiki) owns the user-facing experience.

## Rewrite status

`feature/rules-core-rewrite` is the clean-slate baseline; `feature/rules-core-phase1-host` is an isolated **Phase 1B development branch** introducing a bootable .NET 10 Minimal API foundation. It **does not yet load or serve rules**. The deployed old implementation on `main` remains reference evidence only.

No merge, deployment, database migration, or access activation is authorized. Both Docker Compose deployment definitions and the deployment workflow remain disabled.

## Phase 1B project layout

- `src/RulesCore.Host/` — Kestrel headless host; `GET /health` returns liveness 200 and `GET /ready` returns 503 until Phase 2 validates a native catalog.
- `src/RulesCore.Content/` — C# library defining the native-only directory seam and readiness contract.
- `tests/RulesCore.Host.Tests/` — integration tests for startup/shutdown, configuration, endpoint behavior and public/private route non-exposure.
- `Dockerfile` — multi-stage runtime image; building it does not enable deployment.
- `docs/rewrite/phase-1b-host.md` — detailed contract and integration handoff.

Run `dotnet build RulesCore.slnx` and `dotnet test RulesCore.slnx`. PostgreSQL is not needed for startup. There are no `/api` rule endpoints yet.

## Rules data: coordinated Phase 1A

A separate workstream is classifying `data/native/` into `data/rulesets/first-party-001/native/` (reviewed authoritative rules) and `working/` (unfinished and inert content), with provenance under `data/reference/`. This branch **does not move, approve or load any data**. Runtime path resolution targets only `<ContentRoot>/<RulesetId>/native`, with no fallback to `working/`. The internal ruleset ID is provisional, not a public game name.

The existing native and reference data and Python validation tools remain untouched until the two branches are integrated. Source import must never silently become authoritative native rules.
