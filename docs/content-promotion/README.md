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

The representative monster records preserve existing source statistics, encounter actions, and family relations without inventing unresolved derived defenses or saving throws. All 30 conditions are selected native data with readable descriptions and chapter navigation; executable condition mechanics remain a separate responsibility. See [conditions-readiness.md](conditions-readiness.md).

**Experimental only:** Modular Warlock Alpha 0.3.1 remains under `data/reference/experiments/`, outside native runtime authority. It does not become an authoritative class through content packaging.

## Permanent validation policy

1. Parse only the selected `native/` directory with `NativeCatalogLoader`. Reject invalid files, duplicate IDs, bad condition references and missing content; never fall back to `working/`.
2. Verify category totals, search pagination and distinct identities against the **actual loaded catalog**. Do not compare them with a historic 35/222-record inventory.
3. Run `tests/test_native_content_regressions.py`, `tests/test_conditions_wiki_readiness.py`, and existing shared-rule and C# tests. Focus on approved mechanical behavior, provenance, identity relationships and access restrictions.
4. Compute a semantic SHA-256 fingerprint for each reviewed commit. It is a **release integrity identifier**, not a constant that tests require across ordinary editing.
5. For deployment, independently authorize the specific release commit and fingerprint; verify the image matches that authorization and retains public/private access restrictions. No merge or deployment is authorized by a green test alone.

The original release's fixed fingerprint was `870ca5e89a97b9176f0a3b55fb6cb9467227d13562a83dd7d72ecee5a57ecda6`. The latest **reviewed content snapshot** fingerprint is `b2af2ac5b47d133e181b82604a0619593ac5c6c67ae73da6e01f96f920acfd32` (222 entities; verified by the native C# loader). It may legitimately change on the next JSON edit. The existing deployment script still pins the older value; update that release authorization mechanism separately rather than weakening integrity checks.

## Ownership and continuation

- **Rules Content:** reconcile and promote individual native JSON definitions continuously; maintain evidence and targeted regression tests.
- **Rules Core Code Rewrite:** maintain the native loader, CI/release authentication, distribution security and deployment process. C# and Docker tests should not embed content counts.
- **Rules Wiki UI:** read the native authorized catalog, show approved classes, condition effects and creature identities, and label any separately authorized experiments.

Review notes are in `docs/handoffs/` and the machine-readable ledger. Deferred mechanics are documented for their relevant subsystem; they do not block unrelated coherent rules.
