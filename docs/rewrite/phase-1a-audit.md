# Phase 1A native versus working audit

## Evidence and classification

Audit base: `feature/rules-core-rewrite` at `6e6907703b27ef318216ab12efa334833c6e9055`. Evidence sources: `docs/rewrite/native-baseline-consistency-gate.md`, `native-rules-design-specification.md`, `native-seed-skills-and-competencies.md`, the existing regression tests and `data/reference/srd/native-coverage-manifest.json`.

Only a reviewed, internally coherent, approved Dorks & Dice rule may enter **native**. A selected SRD source default, `retained-native-definition` marker, source-only alias or accepted partial class slice is not necessarily a complete rule. Lack of a C# executor alone does not disqualify an approved reference rule. **Working** holds staged, provisional, candidate, incomplete and raw imported content. No runtime approval detector or per-rule publication allowlist is used.

## Reconciliation

| Measure | Before | After |
| --- | ---: | ---: |
| Rule-corpus files | 2277 | 2276 + 1 archived README |
| JSON files | 2258 | 6 native + 2252 working |
| SRD logical coverage records | 2522 | 2522 |
| SRD records without a physical mapping | 0 | 0 |

All moved data files keep exactly the original Git blob SHA and bytes. The complete **2277-file** mapping and **2522-record** mapping, including source statuses, sizes, and category totals, is in `phase-1a-native-working-ledger.json`. The old native README remains available as `phase-1a-original-native-readme.md` for provenance, but its paths and counts were historical. The authoritative source coverage manifest has been updated only for physical paths; source selection statuses, IDs, and underlying SRD references were not revised.

## Current authoritative native reference definitions

- `conditions.json`: 30 previously approved conditions including Wet, Held and Pinned
- `rules/conditions.json`: associated common condition policy and references
- `defenses.json`: selected ordinary, Touch, Flat-Footed and condition-defense definitions
- `held-pinned.json`: selected Held/Pinned distinction and dependencies
- `spatial-rules.json`: approved nine-category sizes, space, reach and movement foundation
- `monster-statblock-foundation.json`: approved source-neutral stat-block systems (not imported monster definitions)

These are approved rule contracts, not executable implementations or approvals of separately deferred procedures.

## Borderline classifications

- **Skills (88) and Competencies (33):** both catalogs have explicitly provisional metadata, despite approved identities and skill-composition principles. Their numerical mechanics remain incomplete. Hold each whole catalog in working rather than publishing an incomplete catalog as final.
- **Creature types:** approved hierarchy has `candidateSpecializations` and deferred type mechanics within one object. Preserve the entire object in working until its rules are coherent.
- **Monsters:** selected newer stat blocks, 23 aliases and restored older abilities are valuable reconciled source data, but raw 5etools-style content and an open Assassin critical component prevent certification of all creature records as fully native. The aliases are not operative creatures.
- **Band 1 classes, subclasses, features, feats and invocations:** partial approvals do not certify entire playable class entities; non-SRD invocation rights review remains relevant. Preserve all unapproved source candidates and design contracts in working without modifying mechanics.
- **Items, Weapon Mastery and other consolidated catalogs:** mixed source-selection statuses are not evidence of a mixture of approved authoritative versus unfinished rules. No record-level split is justified yet. All logical entries remain in working without creating duplicate final authority.
- **`fear.json`:** its own status declares historical multiple-fear escalation unresolved. Approved individual conditions remain native.
- **Monster families:** relationship/navigation records without approved independently operative mechanics stay working.

## Per-source-kind counts

| Source kind | Total | Native | Working | Missing |
| --- | ---: | ---: | ---: | ---: |
| action | 24 | 0 | 24 | 0 |
| background | 4 | 0 | 4 | 0 |
| baseitem | 98 | 0 | 98 | 0 |
| class | 12 | 0 | 12 | 0 |
| classFeature | 416 | 0 | 416 | 0 |
| condition | 15 | 15 | 0 | 0 |
| deity | 67 | 0 | 67 | 0 |
| disease | 3 | 0 | 3 | 0 |
| feat | 17 | 0 | 17 | 0 |
| hazard | 14 | 0 | 14 | 0 |
| item | 561 | 0 | 561 | 0 |
| itemEntry | 7 | 0 | 7 | 0 |
| itemGroup | 26 | 0 | 26 | 0 |
| itemMastery | 8 | 0 | 8 | 0 |
| itemProperty | 25 | 0 | 25 | 0 |
| itemType | 33 | 0 | 33 | 0 |
| language | 19 | 0 | 19 | 0 |
| magicvariant | 42 | 0 | 42 | 0 |
| monster | 372 | 0 | 372 | 0 |
| object | 1 | 0 | 1 | 0 |
| optionalfeature | 56 | 0 | 56 | 0 |
| race | 11 | 0 | 11 | 0 |
| ruleChapter | 53 | 1 | 52 | 0 |
| sense | 4 | 0 | 4 | 0 |
| skill | 18 | 0 | 18 | 0 |
| spell | 341 | 0 | 341 | 0 |
| status | 3 | 0 | 3 | 0 |
| subclass | 13 | 0 | 13 | 0 |
| subclassFeature | 116 | 0 | 116 | 0 |
| subrace | 9 | 0 | 9 | 0 |
| table | 4 | 0 | 4 | 0 |
| trap | 14 | 0 | 14 | 0 |
| variantrule | 115 | 0 | 115 | 0 |
| vehicle | 1 | 0 | 1 | 0 |

## Owner decision queue

1. Confirm the Skills/Competencies complete catalog and outstanding numerical mechanics before authorizing their promotion.
2. Resolve creature type candidate specializations and inheritance/save defaults before promoting the entire entity.
3. Complete markup conversion, native coherence checks and gameplay approval for selected monsters, spells, items, Masteries, class and feature definitions. Retained source status alone is not approval.
4. Preserve existing decision queues for Assassin Assassinate critical behavior, unresolved fear escalation, Pinned spellcasting/action integration and other documented subsystem dependencies.

## Phase 1B / Phase 2 contract

Ruleset stable identifier: `first-party-001`, independent of any public game name.

Only `data/rulesets/first-party-001/native/` is the authoritative runtime/API content root. `data/rulesets/first-party-001/working/` is never a fallback. Approved completed rules move to native with the superseded working definition removed. Source provenance, pinned SRD and license material stay under `data/reference/`; mappings remain under `data/conversions/`. This branch owns content, Python tests and CI paths, not `src/` or C# runtime scaffolding. No merge, deploy or database changes are authorized.

## Validation results — Phase 1A

Validation was performed by the repository's GitHub Actions `Validate Rules Core Rewrite Structure` workflow, [run 40822812806](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/40822812806), on commit `29c47af87adacb3c2fcdd42970257a7395fd1269` (2026-10-29). All required steps passed:

- Repository skeleton, old runtime exclusion and parsing of **2,372** JSON files across `data/`.
- Full existing and new Python suite: **239 tests**, **0 failures**, **0 errors**, with staged source-reference and promoted rule tests retained.
- Strict native consistency audit: no invalid JSON, duplicate native IDs or unresolved candidate definitions in the authoritative root.
- Working consistency inventory: JSON and staged candidates retained without being published.
- Independent ruleset boundary and relationship checks: **35 authoritative records**, **2,626 distinct working identities**, no overlap or missing referenced condition definitions.
- Migration snapshot verification: **2,277** original Git blobs checked against their old SHA and length, all identical; **2,522** source-coverage manifest rows remain mapped and accounted for.

The SHA snapshot mode (`python3 tools/validate_ruleset_split.py --check-migration-sha`) proves this Phase 1A move was lossless. It is a one-time historical migration gate, not a design decision or a permanent prohibition on editing or promoting approved rules. Future content changes should preserve the historical ledger and use ordinary rule validation, not require the current working-tree files to remain byte-identical to their original staged versions.

## Follow-up verification: candidate counts and full-class dependencies

The latest strict content audit from [CI run 40823367700](https://github.com/LegendMaster03/dorks-and-dice-rules-core/actions/runs/40823367700) reports **35 native records**, **2,626 working identities**, and **1,491 working records with `candidateDefinitions`** (983 with multiple candidates, 508 with one). The pinned SRD manifest has 1,501 historical `single-candidate` or `multiple-candidates` statuses (509 and 992 respectively); its ten extra source labels represent **nine now-approved Conditions and the approved Conditions rule chapter**. Those historical selection labels are not current publication statuses. The earlier 1,664 unresolved count in dated documents should not be interpreted as a current count.

The seven reconciled Band 1 base classes (Barbarian, Bard, Cleric, Druid, Rogue, Sorcerer, and Wizard) are not classified as unapproved designs. Their reconciliation documents explicitly record approved declarative class-level choices. However, their JSON progressions grant separately identified features currently stored in the working tree, alongside source candidates or deferred shared mechanics. Publishing only a class chassis without its referenced native feature closure would create an apparently complete but incomplete runtime reference. No class was promoted solely on its retained-source label; these are **reviewed class designs held for a coherent promotion pass**, not requests to reopen settled class decisions.

| Band 1 class | Manifest class + features | Selected/retained records | Still-staged candidates |
| --- | ---: | ---: | ---: |
| Barbarian | 35 | 26 | 9 |
| Bard | 36 | 26 | 10 |
| Cleric | 38 | 28 | 10 |
| Druid | 32 | 23 | 9 |
| Rogue | 40 | 29 | 11 |
| Sorcerer | 29 | 19 | 10 |
| Wizard | 22 | 12 | 10 |

These are **historical manifest labels**, not an owner-decision tally. Some candidate feature files are intentionally inactive subclass/epic placeholders and must not be granted during a later promotion. The Druid companion-path revision additionally requires reconciliation with the existing JSON's outdated coexistence representation; Rogue detection/anticipation details and Wizard/Sorcerer companion integration have documented deferrals. Preserve approved decisions and prior test coverage during future promotion.

The permanent test suite now validates the ledger's unique mappings, stable historical SRD identity roster, category/count reconciliation and current content boundary; it no longer requires the working files to retain their original byte representation forever. The original 2,277 SHA comparisons remain available as the **one-time completed Phase 1A snapshot proof** through `--check-migration-sha`. Legitimate later updates will not be blocked by that historical test.
