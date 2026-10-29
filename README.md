# Dorks & Dice — Rules Core

Rules Core is the headless rules-content service and future mechanical
execution engine for the Dorks & Dice tabletop system. Rules Wiki is a separate
user-facing application: https://github.com/LegendMaster03/dorks-and-dice-rules-wiki.

## Rewrite status

The rewrite branch integrates Phase 1A native/working separation and the
Phase 1B .NET 10 host foundation. This Phase 2 development branch loads and
validates a native-only catalog, but exposes no rules-serving API. The earlier
implementation on main is historical reference, not a second runtime.

The initial ruleset identifier is first-party-001. The approved content source
is exclusively data/rulesets/first-party-001/native/. Content in working/ is
inert and never a fallback. Reference evidence and conversions are not loaded
as native rules. Controlled promotion into native/ establishes approval; the
runtime has no per-rule publication allowlist or approval detector.

The current native package consists of six JSON files and 35 playable
definitions. Phase 1A migration evidence is preserved separately. Native data
updates do not require recompilation of RulesCore.Content or RulesCore.Host.

## Catalog and host

RulesCore.Content is independent of ASP.NET Core and PostgreSQL. It validates
the manifest and native JSON package, rejects inconsistent content, and builds
a complete immutable snapshot with stable identity lookup, category indexes,
case-insensitive searches, deterministic pagination, and SHA-256 fingerprint.
The ASP.NET Core Minimal API host initializes this snapshot through dependency
injection.

GET /health reports process liveness and remains HTTP 200 when content is
unavailable. GET /ready is HTTP 200 only with a validated catalog, otherwise
HTTP 503 with a safe reason. No rules, Rules Wiki or private API routes are
mapped. PublicOnly and PrivateOnly remain separate configuration options
without granting access to unfinished endpoints.

Run from the repository root:

    dotnet restore RulesCore.slnx
    dotnet build RulesCore.slnx -c Release --no-restore
    dotnet test RulesCore.slnx -c Release --no-build
    python3 -m unittest discover -s tests -p 'test_*.py'
    python3 tools/validate_ruleset_split.py
    dotnet run --project src/RulesCore.Host

For an alternate working directory, configure Rulesets__ContentRoot to the
absolute path of data/rulesets. Docker embeds the manifest and native content,
but excludes working drafts and conversion/reference data.

See docs/rewrite/phase-2-native-catalog.md for the validation contract,
readiness lifecycle, test commands and Phase 3A integration handoff.

No deployment, merge to the rewrite branch, consumer exposure or production
cutover is authorized by this phase.
