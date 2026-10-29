# Rules Core source — Phase 1B

- `RulesCore.Host/` — .NET 10 headless Minimal API host. Only liveness and intentionally-unready readiness endpoints exist.
- `RulesCore.Content/` — framework-independent native ruleset path and readiness contracts. No content loader exists yet.

The retired service remains on `main` solely for reference. Neither current project serves game rules, exposes authoring APIs, nor depends on PostgreSQL. See `docs/rewrite/phase-1b-host.md` for configuration and Phase 2 handoff.
