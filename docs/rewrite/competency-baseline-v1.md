# Native Competency Baseline v1 — grant and check contract

**Status:** Usable initial *mechanics slice* on `feature/competency-profile-reconciliation`. The grant reducer, basic numeric character projection, and prerequisite helper are implemented. **This is not yet an end-to-end origin/feature-entitlement integration, a finalized rank scale, or a deploy-ready crafting progression system.**

## Scope and agreed design

- A native **Competency** represents one learned capability. Brewing, Cooking, Herbalism, Smithing, Alchemy, and similar methods consolidate overlapping craft Skills and 5.x crafting-tool training.
- A Competency has a **nonnegative rank count** and an independent **binary proficiency flag**. Each can be granted separately, or the same source can grant both. No rank inherently confers proficiency, and proficiency does not imply a rank.
- Legitimate **multiple rank grants accumulate**, even when they refer to the same Competency. A repeated feat can be represented by **distinct purchase occurrences**. Duplicate proficiencies from **distinct authorized source occurrences** follow the selected ruleset policy: the provisional default grants **one rank in that same Competency per additional proficiency**, while the alternative requires an authorized proficiency choice in another unproficient Competency. Repeating a source occurrence itself remains invalid.
- Source-based training grants must specify their effect, rather than assuming every 5.x tool proficiency becomes rank + proficiency. A reviewed source-to-native conversion can grant one rank and optionally proficiency, but the conversion value is **not automatically determined by the name of the tool**.
- **Thieves' Tools** remain a separately granted, binary, nonleveling equipment qualification. `skill.open-lock` is a Skill; trap disarming is task-dependent. A crafting Competency's associated equipment does not give the character the item or automatically confer a second equipment proficiency.
- The established **Journeyman, Expert, and Master** titles are reference milestones, with **Master** at the top. Their rank thresholds, extra techniques, caps, and advancement costs remain future content/balance work. The number of numeric ranks should follow the design of meaningful benefits, rather than a predetermined count.

## Initial check calculation

For an activity that explicitly selects an applicable Competency and governing Ability:

```text
resulting check modifier =
    governing Ability modifier
  + Competency ranks
  + (normal proficiency bonus, if the character is proficient)
  + explicitly applicable other modifiers
```

The character projector now permits rank + proficiency on **Competencies**, calculates the combined modifier when the Ability and proficiency bonus are available, and reports a missing-Ability or unavailable-proficiency result as unsupported. It continues to enforce the older combined-profile restriction for **Skills**, whose broader rank/proficiency reconciliation is separate.

This **initial numeric behavior** enables ordinary playtesting and must be revisited against bounded accuracy, Skill scaling, high rank totals, multiclass proficiency, and progression costs. The only implemented rank bound is a **transport safety limit**, not a ruleset maximum. No rank threshold automatically grants advanced techniques, crafting speed, improved recipe quality, materials, tool ownership, or success.

A specific task or recipe still governs whether the character may attempt it, which tools/materials are required, whether proficiency qualifies the actor, which Ability is relevant, and which other modifiers apply. Do not add two full bonuses for an overlapping craft Skill and associated tool proficiency without an explicit rule.

## Typed grant data — post-validation contract

The mechanics helper `NativeCompetencyTraining.CombineValidated` accepts an array of:

| Field | Meaning |
| --- | --- |
| `SourceId` | The rule concept providing the grant (e.g., an Upbringing feature, feat, Background, or class choice) |
| `OccurrenceId` | **Unique actual grant occurrence**. Duplicate occurrences are invalid; separately earned repeatable-feat purchases have distinct occurrences |
| `CompetencyId` | A native `competency.*` identity that exists in the loaded catalog |
| `Ranks` | Nonnegative rank increment, possibly zero |
| `Proficient` | Whether this occurrence grants proficiency |
| `DuplicateProficiencyAlternativeCompetencyId` | Optional source-authorized replacement in `ChooseAnotherProficiency` mode only |

A grant must supply a positive rank increment, proficiency, or both. Valid grants with different occurrence IDs **add rank increments** and combine proficiency through logical OR. Any invalid grant or duplicate occurrence invalidates the **entire input batch**, preventing a partly calculated training result.

**Duplicate policy:** The native rule `rule.competency-duplicate-proficiency` supports `add-rank` (provisional default) and `alternate-proficiency` (breadth option). `NativeCompetencyTraining.CombineValidated` accepts a typed `NativeCompetencyDuplicateProficiencyPolicy`, validates required alternatives, and returns source-occurrence resolutions. One base proficiency remains; each additional distinct proficiency becomes a rank or a proficiency in one other valid, unproficient Competency. Explicit rank grants remain on their original Competency. A missing, repeated, or invalid alternative fails without exposing partial training. The mode is **an experimental optional rule selectable now** at the campaign/playtest-session level in native JSON (`optional-rule.competency-duplicate-proficiency`, setting `competencyDuplicateProficiencyResolution`); its selected value is passed to the existing typed reducer by trusted ruleset configuration. A Character Sheet toggle is not implemented yet, so manual playtests must record the selected mode. The comparison is intended to establish whether a permanent optional rule is balanced, not to assume that equivalence.

**Authority boundary:** This is a **pure reducer, not an API for obtaining grants**. It checks identity, numerical validity, and occurrence uniqueness, but it **does not** prove the source was earned, ensure a character selected an eligible option, determine whether a feat could repeat, resolve source-edition tool names, or enforce a maximum gameplay rank. The authorized class/origin/feat selection engine **must validate those facts, any replacement choice, and the active ruleset policy before invoking the reducer**. Do not accept a client-supplied `SourceId` as authorization. Maintain actual grant provenance in the calling character/campaign workflow.

### Example (illustrative, not an approved origin allocation)

```text
Upbringing grant:  Brewing +1 rank, no proficiency
Background grant:  Brewing proficiency only
Feat purchase A:   Brewing +1 rank
Feat purchase B:   Brewing +1 rank
---------------------------------
Effective state:   Brewing 3 ranks, proficient
```

This example demonstrates separate grant operations; it does **not** assert that any named Upbringing, Background, or feat actually grants those precise benefits. Conversion and allocation decisions still belong to the corresponding rule-concept passes.

## Prerequisites

A native Competency requirement can independently specify:

```text
CompetencyId          exact native identity
MinimumRanks          nonnegative threshold (default: 0)
RequiresProficiency   boolean (default: false)
```

`NativeCompetencyTraining.CheckPrerequisite` checks the post-validation training state against these fields. A rank-only threshold can be satisfied **without proficiency**; a proficiency-dependent requirement may be checked at any rank. It returns **unresolved** for invalid/unknown native targets rather than granting access. The helper does not evaluate arbitrary feature text or enforce source authorization.

These fields are the minimal contract for future explicit rank-gated feats, features, recipes, class choices, and techniques. Do not invent missing threshold values for the current catalog. Higher-rank milestone content is not implemented by the existence of a requirement shape.

## Existing equipment associations for source conversion

The current **26** native Competencies have **22** definitions with named associated equipment. These are **candidate identity relationships** for reconciling source-edition tool proficiency grants; they do not themselves award ranks or proficiency, validate source rights, or imply that a tool grant covers every possible task.

| Associated source equipment | Candidate native Competency |
| --- | --- |
| Alchemist's Supplies | `competency.alchemy` |
| Brewer's Supplies | `competency.brewing` |
| Calligrapher's Supplies | `competency.calligraphy` |
| Carpenter's Tools | `competency.carpentry` |
| Cartographer's Tools | `competency.cartography` |
| Cobbler's Tools | `competency.cobbling` |
| Cook's Utensils | `competency.cooking` |
| Disguise Kit | `competency.disguise` |
| Forgery Kit | `competency.forgery` |
| Glassblower's Tools | `competency.glassblowing` |
| Herbalism Kit | `competency.herbalism` |
| Jeweler's Tools | `competency.gemcutting` |
| Leatherworker's Tools | `competency.leatherworking` |
| Mason's Tools | `competency.stonemasonry` |
| Navigator's Tools | `competency.navigation` |
| Painter's Supplies | `competency.painting` |
| Poisoner's Kit | `competency.poisoning` |
| Potter's Tools | `competency.pottery` |
| Smith's Tools | `competency.smithing` |
| Tinker's Tools | `competency.tinkering` |
| Weaver's Tools | `competency.weaving` |
| Woodcarver's Tools | `competency.woodcarving` |

Four currently selected Competencies have no named associated tool in the catalog: `competency.basketweaving`, `competency.bookbinding`, `competency.locksmithing`, `competency.sculpting`. They remain full trainable Competencies; that absence does not prevent origin, class, feat, or practice-based rank grants.

**Exception:** Thieves' Tools do not map to a leveled Competency. Their separate binary equipment proficiency can interact with `skill.open-lock` and relevant trap procedures only as specified by the actual equipment/check rules. Do not map Thieves' Tools to Locksmithing or a retired Trap Disarming identity.

The crosswalk is **identity guidance**, not a universal conversion effect. When an actual Background/Upbringing/Class/feat source is reconciled, separately assign its **rank increment**, **proficiency grant**, or both. Also validate the source-grant choice and its entitlement before aggregation.

## Integration to unblock the remaining pillars

| Consumer or rule concept | Immediately useful contract | Remaining independent integration |
| --- | --- | --- |
| **Backgrounds and Upbringings** | May specify rank, proficiency, or both for a selected/native Competency | Resolve individual source grants and eligible choices; verify selection/provenance. Existing source `toolProficiencies` JSON is not itself an executable conversion |
| **Classes, feats, and features** | Typed grants with distinct occurrences; rank and proficiency prerequisites | Authorize feature ownership and purchase occurrences; determine actual granted amounts, class advancement/rank budgets, and choice eligibility |
| **Character Sheet** | Store ranks/proficient independently; can use native projector total when it has an explicit activity Ability | Consumer binding to authorized grants and activities, persistence of occurrences, UI, and versioned API integration |
| **Crafting/harvesting** | Can consume an already validated Competency state and ordinary check modifier | Replace manual manufacturing contributions only after task-specific method/Ability/qualification and recipe rules are wired; preserve current qualified guidance and helper rules |
| **Other rule concepts** | Can refer to a Competency identity and express typed minimum-rank/proficiency requirements | A common, source-authorized rule prerequisite evaluator and higher-tier feature effects remain later work |

### Acceptance checks in the current slice

- A character with ranks and proficiency gains the ability, rank, and proficiency contributions exactly once in its ordinary Competency check.
- Rank-only and proficiency-only states produce their respective contributions; an unresolved governing Ability does not silently choose one.
- Skills retain their current independent behavior (this slice does **not** remove the existing Skill combined-profile restriction).
- Validated distinct grant occurrences accumulate explicit ranks; repeated proficiency grants add one rank each by default or resolve to authorized alternate Competency proficiencies in alternate mode.
- Duplicate grant occurrences, non-Competency targets, and invalid rank amounts fail closed.
- A rank prerequisite does not imply proficiency, and a proficiency prerequisite does not imply ranks.

## Deferred, not blockers for grant design

Journeyman/Expert/Master thresholds; additional rank titles; a finalized rank maximum; practice and training schedules; feat cost and duplicate-choice balance; rank-based abilities, crafting speed, quirk control, ingredient/reagent substitution; formal source conversion matrices; multi-Competency manufacturing procedure; and full client/server entitlement enforcement.

**Validation:** The finalization branch CI passed the Mechanics tests covering both policies, explicit rank preservation, replacement eligibility, occurrence provenance, and order independence. Manual playtest selection is supported by the authoritative optional-rule metadata and the typed reducer parameter; Character Sheet UI and automatic feedback capture are not yet implemented.
