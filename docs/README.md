# Rules Core documentation

Rules Core's current implementation is the native, ruleset-first service on `main`. This index separates operational contracts, evolving game design, and historical evidence.

## Current implementation and operational contracts

- [Architecture](architecture.md) — native runtime, content authority, API surfaces, authentication, and service boundaries.
- [Native mechanics API](native-mechanics-api.md) — current versioned mechanics routes and their authorization/evaluation boundaries.
- [Authenticated native catalog](native-authenticated-consumer-catalog.md) — Site-authenticated catalog queries.
- [Private tunnel deployment](private-tool-tunnel-deployment.md) — Rules Wiki ingress isolation and deployment topology.
- [Server-Timing](server-timing.md) — instrumented request timing contract.
- [CI and native content validation](rewrite/ci-validation-boundary.md) — which checks apply during active rules development.
- [Repository README](../README.md) — build, test, and manually authorized deployment.

The authoritative gameplay definitions are in `data/rulesets/first-party-001/native/`; documentation describes those rules but does not activate them. `working/` and `reference/` are not runtime fallbacks.

## Living game design and integration

- [Native rules design specification](rewrite/native-rules-design-specification.md) — approved choices, proposals, and unresolved decisions; proposals are not automatically operative rules.
- [Native equipment qualifications](rewrite/equipment-qualification-contract.md) — equipment proficiency and Weapon Mastery entitlement evaluation.
- [Competency baseline](rewrite/native-seed-skills-and-competencies.md) and [competency mechanics slice](rewrite/competency-baseline-v1.md) — consult alongside the latest native data and development branch; the design is still evolving.
- [Character Origins equipment integration](rewrite/character-origins-equipment-integration.md) — active integration work, not a completed migration.
- [Warlock modular alpha review](content-promotion/warlock-modular-alpha-0.3.1.md) — test-slice decisions; source analyses and unresolved proposals remain under `rewrite/`.
- [Continuous content promotion](content-promotion/README.md) — promotion policy and a dated audit snapshot.

Many `rewrite/` files are *research or decision records*, not current API specifications. A historical cross-edition comparison is not an approval of a mechanical change. Prefer native JSON and executable tests for current implemented behavior, and the living specification for unresolved game-design decisions.

## Historical evidence retained for tooling

- `rewrite/phase-1a-native-working-ledger.json` is read by `tools/validate_ruleset_split.py` and must not be deleted as routine documentation cleanup.
- `baseline-bootstrap.md`, `competency-reconciliation-audit.md`, `mechanical-relationships.md`, and `universal-character-concepts.md` are **legacy evidence only**. They remain because `data/reference/` audit records cite them; they do not describe the native runtime.
- `rewrite/phase-1a-original-native-readme.md`, `rewrite/phase-1a-audit.md`, and original SRD staging reports explain the migration snapshot; they are not current implementation guides.
- `rewrite/native-reconciliation-statistics.json`, `rewrite/monster-legacy-feature-results.json`, and `content-promotion/promotion-ledger.json` have audit/test/script or provenance consumers. Their counts are not fixed native catalog requirements.

Temporary handoffs, obsolete phase-completion notes, legacy endpoint instructions, and superseded proposals are removed rather than copied into an ever-growing archive. Git history preserves those records. Restore a historical decision only after reconciling it with the current native ruleset.
