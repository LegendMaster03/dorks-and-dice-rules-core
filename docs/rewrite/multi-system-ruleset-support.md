# Multi-system Rules Core — development checkpoint

Status: incomplete; not accepted for merge or deployment.

## Registration and authority

Keep Rulesets:DefaultRuleset = first-party-001 for existing consumers. Register additional independent packages through:

    Rulesets__AdditionalRulesets__0=dnd-5e
    Rulesets__AdditionalRulesets__1=dnd-5-5e
    Rulesets__AdditionalRulesets__2=fate-condensed

Each package has its own native manifest, snapshot, fingerprint and readiness slot. No fallback to another catalog or the working/reference trees. A failed additional package does not invalidate the default. Existing /ready remains default-package readiness, not aggregate health. `/ready/all` checks every configured package without disclosing identities. `/ready/rulesets/{rulesetId}` reports exact package readiness and fingerprint only over an actual loopback connection; it returns 404 for external clients and unknown IDs. The shipped Compose configuration registers the three additional packages through `RULES_CORE_ADDITIONAL_RULESETS` (comma-separated IDs, or empty for default-only); the equivalent host setting is `Rulesets:AdditionalRulesetsCsv`. Do not combine CSV and indexed registration.

Anonymous discovery and reads use /api/rulesets and /api/rulesets/{rulesetId}/rules[/{ruleId}]. Publication remains disabled unless RulesCore:PublicDistribution:Packages:{rulesetId} supplies Enabled=true, RightsReviewReference, AttributionStatement and ApprovedFingerprint matching that package's exact snapshot. Loading is not rights approval. Do not use test approval values in production.

Existing authenticated direct consumer catalog and private Wiki catalog retain first-party access, and can discover/read foreign packages only when their exact fingerprints are independently approved for public distribution. Site tickets have no ruleset entitlement claims and confer no unpublished foreign access. New representative mechanics require existing Character Sheet ticket validation plus exact package publication approval. This is a conservative interim boundary, not a substitute for future scoped Site authorization. Legacy first-party character mechanics do not execute against foreign defaults.

## Coverage and source provenance

| Package | Native records | Status |
|---|---:|---|
| first-party-001 | Existing catalog unchanged | Existing first-party mechanics |
| dnd-5e | 1,707 | 401 publisher-source gameplay pages, 18 skills, 15 conditions, ability-check seed, 319 structured spells, 323 monsters, 630 equipment/item/weapon rules |
| dnd-5-5e | 1,710 | 360 publisher-source gameplay pages, 18 skills, 15 conditions, ability-check seed, 339 structured spells, 336 monsters, 641 equipment/item/weapon rules |
| fate-condensed | 153 | 127 original source sections, 19 default skills, four actions and three structured rating/allocation/stress rules; live edition parity unverified |

D&D official source URLs and required CC BY 4.0 attributions are in data/reference/srd/manifest.json and the new packages. The pinned corpus remains unchanged, with explicitly selected structured definitions promoted into the native packages: source-fidelity audit documents omissions and material outside the SRDs. Edition tags alone do not establish fidelity or licensing. Full import requires inventory of each source array, source-to-official reconciliation, contextual IDs for features/subraces, edition-specific reference validation and a reproducible reviewed promotion pipeline. The official publisher PDFs are now separately imported as native descriptive `rulebook-page` records with pinned PDF hashes, edition identity, page URLs and attribution. Front matter is excluded from gameplay records. This provides direct-source page coverage independent of third-party content; page layout is extracted, not a certified table or semantic parse, and the source PDFs remain authoritative. Use `python3 tools/import_official_srd_pages.py <edition> <pinned-pdf> --output build/official-srd --check` to reproduce. The official-source workflow retrieves the exact pinned PDFs and compares generated page records to committed content. Do not equate page coverage with structured entity coverage or executable mechanics.

Fate source: fate-srd/fate-srd-content, commit 93ed848e723f31b4994b466b7241011ba24be885, docs/markdown/Fate-Condensed-SRD-CC-BY.md, blob cc313c53ee75939e1f5c53f0f2ee59181e78ee00. CC BY 3.0. Preserved original attribution and added publisher's current attribution. Source has unresolved page XX placeholders; do not silently invent destinations. Current website parity and consumer-safe Markdown rendering require review. The original Markdown is reference-only; the native JSON records retain every original section verbatim. Reproduce/check with python3 tools/import_fate_condensed.py [--check].

Structured skills and conditions use the existing 5e.tools entry format with verified official-page provenance. Explicit `catalogReferences` are validated against the current package only. Optional manifest declarations validate publisher-page coverage/provenance and Markdown parent relationships without imposing any game-specific schema on other packages. See [edition-specific structured promotion](srd-edition-structured-promotion.md). Both pinned bestiary inventories are promoted, including the additional publisher-only modern Avatar of Death and Giant Fly. See [structured bestiary promotion](srd-structured-bestiary-promotion.md). Publisher name locators are not full body verification. Both selected equipment inventories are promoted; see [structured item promotion](srd-structured-item-promotion.md). The remaining named entity categories are incomplete.

The spell importer crops each PDF column, extracts title, level, school, casting-field headers and layout text, and reconciles the entire named inventory against the pinned structured reference spell arrays. All 319/339 names and levels/schools agree; no extra or unmatched names and no missing/duplicate casting-field headers. Casting-field header strings are first-line summaries only; multiline material components and other continuations remain in officialTextLayout. The native definitions now additionally preserve the pinned structured JSON fields (time, range, components, duration, entries, higher-level entries and descriptive tags). Publisher evidence is verified independently; source body fidelity and tag resolution are not certified. CI reproduces the publisher evidence and verifies the structured source projection separately. See [structured JSON promotion](srd-structured-json-promotion.md). Other structured categories remain outstanding.

## Edition review catalog generation

Run `python3 tools/stage_srd_editions.py --output build/srd-review` to materialize both full source-tagged working catalogs from the pinned 5e.tools-shaped reference input. The workflow SRD Edition Review Catalogs uploads the working-only output and summary as an artifact. Every tagged top-level source occurrence is preserved verbatim with an exact source pointer, payload digest, contextual proposed identity and unreviewed status. The original inventory also records untagged and non-object values; these need separate classification. Metadata, fluff and source defects are not silently promoted. Proposed identity collisions are reported rather than collapsed. This is the candidate import, not a claim of official completeness or a native promotion. Generated review artifacts never enter authoritative native roots. A separate ignored `build/srd-loader-fixtures` directory projects every candidate into the loader envelope exclusively for CI scale and isolation testing; those fixture catalogs are not deployable content. The review workflow checks exact counts, pagination, facets and fingerprinting through the actual C# loader. The digest diagnostic accepts optional `contentRoot rulesetId` arguments while retaining the existing release output by default.

## Representative evaluation

GET /api/mechanics/v1/rulesets/{rulesetId}/capabilities lists only representative operations, complete=false. POST /api/mechanics/v1/rulesets/{rulesetId}/resolution accepts fingerprint and resolution object (operation, dice, modifier, target, optional d20 options advantage/disadvantage/exhaustion). The private ingress uses /api/private/mechanics/v1 instead. All require existing tickets and matching package publication approval.

Both D&D editions use d20-ability-check. 5.1 exhaustion imposes disadvantage on ability checks; 5.2.1 subtracts twice the exhaustion level. Fate uses fate-roll with four signed dice and outcome categories derived from margin. No D&D prerequisites. Results are deterministic evaluations of supplied rolls, not RNG or character state authorization. Unsupported operations return 422, invalid requests 400, fingerprint mismatch 409, unavailable/unknown/unapproved package 404. Full systems mechanics, action-specific Fate effects and per-character state remain out of scope for this checkpoint.

## Packaging and validation

Both Docker builds stage only manifests and native trees, and content-only updates replace the whole catalog root. Working/reference/conversion material is excluded. The release workflow computes independent loader fingerprints for every embedded package and requires both ingresses to load the same configured set with the reviewed per-package fingerprints. The preflight still prohibits public distribution configuration. No deployment is authorized; consumer contracts require separate review. No deployment is authorized.

Run dotnet test RulesCore.slnx -c Release and python3 -m unittest discover -s tests -p 'test_*.py' -v, plus full and content-only Docker builds. Exact-head CI and four-package end-to-end HTTP mechanics acceptance remain required. The synthetic and repository-package tests do not substitute for downstream Character Sheet, Block Initiative and Rules Wiki compatibility verification.
