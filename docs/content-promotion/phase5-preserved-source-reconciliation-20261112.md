# Phase 5 — preserved working-source divergence reconciliation

**Date:** 2026-11-12  
**Scope:** Rules Core native/working content integrity, independent of mixed-Origin gameplay.  
**Result intended:** Satisfy the existing exact-path source-mirror validator without editing `native/` mechanics, deleting `working/` source evidence, or weakening the release gate.

## Evidence and classification

The preexisting full Python validation reported **33** native/working record conflicts. This review compared the current selected-native definitions with the inert working records. All 33 refer to previously selected architecture or qualifications already described in the repository's rule reconciliation documents. The new ledger entries are **static source-divergence evidence**, not a new gameplay approval or runtime permission.

| Records | Count | Native/working difference | Reviewed design source |
| --- | ---: | --- | --- |
| Barbarian, Bard, Cleric, Druid, Rogue, Sorcerer, Wizard base classes | 7 | Typed `equipmentProficiencyGrants`; Bard additionally links its older single Martial-weapon choice to the canonical equipment grant | [Weapon qualification audit](../rewrite/character-origins/pillars-1-and-3-weapon-proficiency-mastery-audit-20261110.md) and [shared equipment contract](../rewrite/equipment-qualification-contract.md) |
| Overlapping native/working Competencies | 24 | Native `profile` and optional `associatedTools` replace working `trainingIdentity` and historical `facets`; IDs and names remain stable | [Native Skills and Competencies baseline](../rewrite/native-seed-skills-and-competencies.md) |
| Barbarian and Rogue level-1 Weapon Mastery class features | 2 | Native typed `weaponMasteryGrants` are independently awarded with proficiency prerequisites | [Weapon qualification audit](../rewrite/character-origins/pillars-1-and-3-weapon-proficiency-mastery-audit-20261110.md) |

The Druid entry previously documented the exclusive companion-path choice. Its original six changed paths are retained; the separate equipment grant adds one exact path to that same source-mirror record. No Druid companion rule is changed.

### Exact native/working differences

- Six class records (all except Druid) receive ledger entries for `/definition/equipmentProficiencyGrants`; Bard also has `/definition/legacySingleMartialWeaponChoice/implementedByEquipmentGrant`. The existing Druid entry adds `/definition/equipmentProficiencyGrants`.
- Two class-feature entries record only `/definition/weaponMasteryGrants`.
- Each of the 24 Competency entries records `/profile`, `/trainingIdentity`, and `/facets`. The 20 whose native record has `associatedTools` additionally record that exact path. Four without `associatedTools` do not.
- The 24 overlapping Competency source IDs are: `competency.alchemy`, `competency.basketweaving`, `competency.bookbinding`, `competency.brewing`, `competency.calligraphy`, `competency.carpentry`, `competency.cartography`, `competency.cobbling`, `competency.cooking`, `competency.gemcutting`, `competency.glassblowing`, `competency.herbalism`, `competency.leatherworking`, `competency.locksmithing`, `competency.navigation`, `competency.painting`, `competency.poisoning`, `competency.pottery`, `competency.sculpting`, `competency.smithing`, `competency.stonemasonry`, `competency.tinkering`, `competency.weaving`, `competency.woodcarving`.

## File-level provenance boundary

The historical `working/competencies.json` carries `"_meta": {"status": "provisional"}`, not `status` or `baselineStatus` on each child Competency. The original matcher allowed documented divergence only if the individual working record contained a status, so adding valid ledger entries alone would not resolve these 24 conflicts.

The matcher now recognizes the **file-level source status only for exactly `working/competencies.json`**, and only when the record does not already supply its own status. All 24 divergence entries require `"sourceWorkingStatus": "provisional"`. Existing safeguards remain: each record must have an explicit reconciliation document present in the repository, a unique nonempty `changedPaths` list, and an exact comparison against computed native-versus-working paths. The validator rejects missing metadata, an altered source status, a different file, or differences at paths not named in the ledger. This guards the structure and scope of reviewed divergence but does **not** freeze values inside already divergent paths; later mechanical changes within those paths still require ordinary design and regression review. Working content remains inert and never participates in runtime loading.

The original working files and native selected content are unchanged. None of these entries approves new class proficiencies, equipment mastery powers, additional Competency advancement rules, or DM/gameplay decisions.

## Validation

- `tests/test_ruleset_split.py` exercises the real 24-record ledger through the whole native/working contract test. A targeted regression verifies that a changed Competency or source-status claim is rejected.
- The retained Pillar 2 workflow now runs the complete Python suite and explicit strict native/working validation on the active Phase 5 branch, in addition to the complete C# build/test and Pillar 2 studies.
- The earlier creature-type test failure was separately addressed in the current branch: it compares shared executable structure without forcing deliberately superseded historical classification prose to match. This pass does not alter that test or any creature data.
- CI must pass before this reconciliation is regarded as validated. This branch CI is evidence for future integration, not a substitute for the deployment workflow's mandatory successful `main` push run.

## Follow-on

Preserve the source-promotion and immutable historical evidence model; do not automatically add ledger exceptions for future changes. Review new divergence semantics individually. Full Rules Core integration still requires a reviewed branch integration strategy and explicit merge/deployment authorization. This content-source correction does not complete the Phase 5 live Human–Elf acceptance or later Origin update/reconstruction requirements.
