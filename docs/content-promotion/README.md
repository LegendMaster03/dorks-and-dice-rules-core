# Continuous native rules content promotion

**Ruleset:** `first-party-001`  
**Review branch:** `content/coherent-rule-promotion-wave-1` (historical branch name; **no wave-based release model**)  
**Initial main baseline:** `1f5f910d0998f2991b33a25d042bfd46c5641e31`

Content promotion is **continuous**. Approved coherent rulings may be moved into `data/rulesets/first-party-001/native/` whenever they are ready; other content stays under `working/` or `reference/`. There are no numbered content waves, batch milestones, or fixed catalog sizes. The [promotion ledger](promotion-ledger.json) preserves the reviewed snapshot, provenance, selected identities and outstanding design decisions. It is **not a per-rule runtime approval detector**.

## Current reviewed catalog snapshot (not an invariant)

| Kind | At initial baseline | At this review |
| --- | ---: | ---: |
| condition | 29 | 29 |
| leveled-condition | 1 | 1 |
| rule | 4 | 4 |
| ruleChapter | 1 | 1 |
| class | 0 | 7 |
| classFeature | 0 | 161 |
| monster | 0 | 16 |
| monsterFamily | 0 | 3 |
| **Total** | **35** | **222** |

**These counts document one reviewed state only.** Tests and release digests must derive current totals from native JSON. Adding, replacing or removing an approved record should not require changing a catalog count in Python, C#, a Docker smoke test, or a digest utility.

The selected Band 1 classes are Barbarian, Bard, Cleric, Druid, Rogue, Sorcerer and Wizard. Their coherent progression data and associated feature records live in native; selectable options are not automatic grants. The Druid's mutually exclusive companion paths reflect the later owner approval. Additional classes, features, monsters, and general rules are eligible for promotion individually once reconciled.

The native bestiary now contains every previously selected `retained-native-definition` creature from the staged bestiary. These preserve source statistics, encounter actions, and family relations; historical `native-identity-alias` entries remain inert in `working/` instead of creating duplicate creatures. The Assassin automatic-critical detail remains a separate unresolved design question; the otherwise selected creature definition is still usable. All 30 conditions are selected native data with readable descriptions and chapter navigation; executable condition mechanics remain a separate responsibility. See [conditions-readiness.md](conditions-readiness.md).

**Selected native experimental rules:** Modular Warlock Alpha 0.3.1 is now the authoritative current Warlock for playtesting in `native/classes/base/warlock.json`, with separate native features, Fiend Patron, four Pacts and selected Invocations. Its historical comparison record remains under `data/reference/experiments/` but is not runtime authority. All rules are subject to ongoing revision; native means selected coherent rule, not finalized ruleset.

## Permanent validation policy

1. Parse only the selected `native/` directory with `NativeCatalogLoader`. Reject invalid files, duplicate IDs, bad condition references and missing content; never fall back to `working/`.
2. Verify category totals, search pagination and distinct identities against the **actual loaded catalog**. Do not compare them with a historic 35/222-record inventory.
3. Run `tests/test_native_content_regressions.py`, `tests/test_conditions_wiki_readiness.py`, and existing shared-rule and C# tests. Focus on approved mechanical behavior, provenance, identity relationships and access restrictions.
4. Compute a semantic SHA-256 fingerprint for each reviewed commit. It is a **release integrity identifier**, not a constant that tests require across ordinary editing.
5. For deployment, independently authorize the specific release commit and fingerprint; verify the image matches that authorization and retains public/private access restrictions. No merge or deployment is authorized by a green test alone.

The original release's fixed fingerprint was `870ca5e89a97b9176f0a3b55fb6cb9467227d13562a83dd7d72ecee5a57ecda6`. The latest **reviewed content snapshot** fingerprint is `b2af2ac5b47d133e181b82604a0619593ac5c6c67ae73da6e01f96f920acfd32` (222 entities; verified by the native C# loader). It may legitimately change on the next JSON edit. The deployment script no longer pins a content fingerprint. Deployment is an explicit maintainer-dispatched workflow for the reviewed current `main` SHA, requiring that SHA's successful CI run. The workflow independently computes the catalog fingerprint from the reviewed checkout with `NativeCatalogLoader`, then compares it to both live ingress instances and verifies the release image's Git revision label. Merging content does not automatically deploy it. The prior fingerprint remains only as historical provenance; no release is authorized until separately dispatched.

## Ownership and continuation

- **Rules Content:** reconcile and promote individual native JSON definitions continuously; maintain evidence and targeted regression tests.
- **Rules Core Code Rewrite:** maintain the native loader, CI/release authentication, distribution security and deployment process. C# and Docker tests should not embed content counts.
- **Rules Wiki UI:** read the native authorized catalog, show approved classes, condition effects and creature identities, and label any separately authorized experiments.

Review notes are in `docs/handoffs/` and the machine-readable ledger. Deferred mechanics are documented for their relevant subsystem; they do not block unrelated coherent rules.

**Continuing promotion:** The selected native Skills, Competencies and fear-family interactions are now represented in `native/skills.json`, `native/competencies.json`, and `native/fear.json`. Original working records remain for source comparison. Deferred fear escalation is explicitly non-operative; these selections are not claims of finality.

## Ongoing native content recovery

The active migration branch continues promoting previously **selected coherent rules**, without requiring the rest of the system to be finalized. Its committed native additions include:

- Skills and Competencies (selected composites and training facets), copied without substituting unresolved imported definitions.
- The fear-family interaction rule, while preserving the separately deferred historical escalation.
- Emphasis, optional critical escalation, spell-access policy, rest-based recovery and the agreed Grapple starting procedure. Open edge cases remain documented rather than invented.
- More selected monster statblocks that already have a reviewed single `retained-native-definition` in working. Original source candidates, identity aliases and unresolved monsters are not automatically activated.

This is **not a completion claim** for the bestiary, spell corpus, equipment, feats, subclasses or other pending systems. There is no arbitrary sequence of promotion waves and no fixed catalog-count gate. The active branch is deliberately separate from the previously merged Warlock content.

**Bestiary parity validation:** `tests/test_all_native_monsters_source_mirror.py` dynamically checks both directions: every native monster matches its retained selected source definition, and every retained selected source monster has a native record. Source aliases must point to an existing selected monster without producing their own native stat block. No fixed record count or fingerprint is required for content editing.

**Creature types:** The previously approved 19-type hierarchy and shared creature-inheritance foundation are now native searchable records. The unchanged staged `working/creature-types.json` retains nonselected specializations and deferred compatibility research. Existing monster statblocks are not recalculated from type defaults merely because these rules are available.
