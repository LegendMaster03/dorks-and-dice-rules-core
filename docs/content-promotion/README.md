# Coherent rules recovery — Wave 1 ledger

**Ruleset:** `first-party-001`  
**Base:** `1f5f910d0998f2991b33a25d042bfd46c5641e31`  
**Content branch:** `content/coherent-rule-promotion-wave-1`

This file is a navigation summary. [The machine-readable ledger](promotion-ledger.json) retains named classifications and specific unresolved decisions for later conversations and content batches. `native/` remains the sole runtime catalog. `working/` retains original candidate definitions and the nonauthoritative Warlock experiment.

## Catalog inventory

| Kind | Before | After (expected) |
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

The 7 unified Band 1 classes now have their selected 1–20 level tables and separately defined features. The class files contain embedded historical feature mechanics where those mechanics were selected. Optional Cleric, Druid and Rogue selections remain **options**, not unconditional grants. Unselected subclasses, Epic Boons, imported historical variants, and higher bands remain in working.

The 16 creature examples include simple beasts, three Goblin roles, two Hobgoblin roles, two Bugbear roles, a lycanthrope, a spellcasting Priest, and three advanced magical/legendary creatures. Creature data preserve existing reconciled statistics and source action economies, with a reference to the native unified monster-statblock foundation; **missing derived defenses/saves are not invented**.

## Review status and next batches

- **Native before Wave 1:** 35 condition/foundation records. This batch does not redefine them.
- **Native after Wave 1:** seven classes, 161 selected class features, 16 monsters and three creature families.
- **Experimental only:** Warlock modular Fiend alpha 0.3.1 under `data/reference/experiments/`; no native `class.warlock`.
- **Further eligible review:** additional coherent monster stat blocks, and already reconciled skill/competency/general-mechanic entries after checking references.
- **Unresolved:** see `statusGroups.unresolved` in the JSON ledger; these are concrete decisions and execution limitations, not a declaration that all seven class models are blocked.

## Safety and release

This is a **content-only** proposal. The selected native definitions change the semantic catalog digest; the old deployment fingerprint `870ca5e89a97b9176f0a3b55fb6cb9467227d13562a83dd7d72ecee5a57ecda6` must not be accepted for an updated image. The exact semantic digest **3b60c6f125eb525285333bc26758abeb41424aa4b4c362831d36987e11375f8c** was computed and verified with the C# native loader by the read-only content workflow. The actual release SHA must still be explicitly approved when the updated image is built. Do not change the deployment script to skip the check.

The existing C# `NativeCatalogTests.ApprovedPackageLoads35PlayableRecords` asserts the original count of 35 and will need a separate Core Code rewrite owner update, not an unreviewed content-side rewrite. The review/verification handoffs live under `docs/handoffs/`.

**Validation:** `tests/test_coherent_rule_promotion_wave1.py` checks class-to-feature references, class progression, source provenance, monster role mappings, exact expected counts and Warlock nonauthority. The existing Python validations still apply. Green content checks do **not** imply that all runtime/consumer tests pass, or that deployment is authorized.
