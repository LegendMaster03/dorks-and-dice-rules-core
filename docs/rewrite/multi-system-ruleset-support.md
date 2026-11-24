# Multi-system Rules Core — development checkpoint

Status: incomplete; not accepted for merge or deployment.

## Registration and authority

Keep Rulesets:DefaultRuleset = first-party-001 for existing consumers. Register additional independent packages through:

    Rulesets__AdditionalRulesets__0=dnd-5e
    Rulesets__AdditionalRulesets__1=dnd-5-5e
    Rulesets__AdditionalRulesets__2=fate-condensed

Each package has its own native manifest, snapshot, fingerprint and readiness slot. No fallback to another catalog or the working/reference trees. A failed additional package does not invalidate the default. Existing /ready remains default-package readiness, not aggregate health. Registry readiness is internal; a separately authorized operational readiness endpoint remains outstanding.

Anonymous discovery and reads use /api/rulesets and /api/rulesets/{rulesetId}/rules[/{ruleId}]. Publication remains disabled unless RulesCore:PublicDistribution:Packages:{rulesetId} supplies Enabled=true, RightsReviewReference, AttributionStatement and ApprovedFingerprint matching that package's exact snapshot. Loading is not rights approval. Do not use test approval values in production.

Existing authenticated direct consumer catalog and private Wiki catalog remain first-party only. Site tickets have no ruleset entitlement claims. Do not broaden them to unreleased foreign content. New representative mechanics require existing Character Sheet ticket validation plus exact package publication approval. This is a conservative interim boundary, not a substitute for future scoped Site authorization. Legacy first-party character mechanics do not execute against foreign defaults.

## Coverage and source provenance

| Package | Native records | Status |
|---|---:|---|
| first-party-001 | Existing catalog unchanged | Existing first-party mechanics |
| dnd-5e | 2 | Verified paraphrased seeds: Ability Check and Exhaustion; full SRD 5.1 import outstanding |
| dnd-5-5e | 2 | Verified paraphrased seeds: Ability Check and Exhaustion; full SRD 5.2.1 import outstanding |
| fate-condensed | 127 | Complete pinned Markdown text segmented by heading; not a structured entity-by-entity import or live edition parity certification |

D&D official source URLs and required CC BY 4.0 attributions are in data/reference/srd/manifest.json and the new packages. The pre-existing structured corpus stays reference-only: source-fidelity audit documents omissions and material outside the SRDs. Edition tags alone do not establish fidelity or licensing. Full import requires inventory of each source array, source-to-official reconciliation, contextual IDs for features/subraces, edition-specific reference validation and a reproducible reviewed promotion pipeline. Do not count the two seed records as complete imports.

Fate source: fate-srd/fate-srd-content, commit 93ed848e723f31b4994b466b7241011ba24be885, docs/markdown/Fate-Condensed-SRD-CC-BY.md, blob cc313c53ee75939e1f5c53f0f2ee59181e78ee00. CC BY 3.0. Preserved original attribution and added publisher's current attribution. Source has unresolved page XX placeholders; do not silently invent destinations. Current website parity and consumer-safe Markdown rendering require review. The original Markdown is reference-only; the native JSON records retain every original section verbatim. Reproduce/check with python3 tools/import_fate_condensed.py [--check].

## Edition review catalog generation

Run `python3 tools/stage_srd_editions.py --output build/srd-review` to materialize both full source-tagged working catalogs from the pinned 5e.tools-shaped reference input. The workflow SRD Edition Review Catalogs uploads the working-only output and summary as an artifact. Every tagged top-level source occurrence is preserved verbatim with an exact source pointer, payload digest, contextual proposed identity and unreviewed status. The original inventory also records untagged and non-object values; these need separate classification. Metadata, fluff and source defects are not silently promoted. Proposed identity collisions are reported rather than collapsed. This is the candidate import, not a claim of official completeness or a native promotion. Generated artifacts never enter native roots.

## Representative evaluation

GET /api/mechanics/v1/rulesets/{rulesetId}/capabilities lists only representative operations, complete=false. POST /api/mechanics/v1/rulesets/{rulesetId}/resolution accepts fingerprint and resolution object (operation, dice, modifier, target, optional d20 options advantage/disadvantage/exhaustion). The private ingress uses /api/private/mechanics/v1 instead. All require existing tickets and matching package publication approval.

Both D&D editions use d20-ability-check. 5.1 exhaustion imposes disadvantage on ability checks; 5.2.1 subtracts twice the exhaustion level. Fate uses fate-roll with four signed dice and outcome categories derived from margin. No D&D prerequisites. Results are deterministic evaluations of supplied rolls, not RNG or character state authorization. Unsupported operations return 422, invalid requests 400, fingerprint mismatch 409, unavailable/unknown/unapproved package 404. Full systems mechanics, action-specific Fate effects and per-character state remain out of scope for this checkpoint.

## Packaging and validation

Both Docker builds stage only manifests and native trees, and content-only updates replace the whole catalog root. Working/reference/conversion material is excluded. Existing deployment digest and consumer contracts require additional audit before deployment. No deployment is authorized.

Run dotnet test RulesCore.slnx -c Release and python3 -m unittest discover -s tests -p 'test_*.py' -v, plus full and content-only Docker builds. Exact-head CI and four-package end-to-end HTTP mechanics acceptance remain required. The synthetic and repository-package tests do not substitute for downstream Character Sheet, Block Initiative and Rules Wiki compatibility verification.
