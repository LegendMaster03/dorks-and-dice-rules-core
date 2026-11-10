# Native Competency Baseline v1 — grant and check contract

**Status:** Usable initial *mechanics slice* on `feature/competency-profile-reconciliation`. The grant reducer, basic numeric character projection, and prerequisite helper are implemented. **This is not yet an end-to-end origin/feature-entitlement integration, a finalized rank scale, or a deploy-ready crafting progression system.**

## Scope and agreed design

- A native **Competency** represents one learned capability. Brewing, Cooking, Herbalism, Smithing, Alchemy, and similar methods consolidate overlapping craft Skills and 5.x crafting-tool training.
- A Competency has a **nonnegative rank count** and an independent **binary proficiency flag**. Each can be granted separately, or the same source can grant both. No rank inherently confers proficiency, and proficiency does not imply a rank.
- Legitimate **multiple rank grants accumulate**, even when they refer to the same Competency. A repeated feat can be represented by **distinct purchase occurrences**. A repeated *proficiency-only* grant remains proficiency only and never becomes a rank by implication.
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

A grant must supply a positive rank increment, proficiency, or both. Valid grants with different occurrence IDs **add rank increments** and combine proficiency through logical OR. Any invalid grant or duplicate occurrence invalidates the **entire input batch**, preventing a partly calculated training result.

**Authority boundary:** This is a **pure reducer, not an API for obtaining grants**. It checks identity, numerical validity, and occurrence uniqueness, but it **does not** prove the source was earned, ensure a character selected an eligible option, determine whether a feat could repeat, resolve source-edition tool names, or enforce a maximum gameplay rank. The authorized class/origin/feat selection engine **must validate those facts before invoking the reducer**. Do not accept a client-supplied `SourceId` as authorization. Maintain actual grant provenance in the calling character/campaign workflow.

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
- Validated distinct grant occurrences accumulate ranks; repeated proficiency-only grants do not add ranks.
- Duplicate grant occurrences, non-Competency targets, and invalid rank amounts fail closed.
- A rank prerequisite does not imply proficiency, and a proficiency prerequisite does not imply ranks.

## Deferred, not blockers for grant design

Journeyman/Expert/Master thresholds; additional rank titles; a finalized rank maximum; practice and training schedules; feat cost and duplicate-choice balance; rank-based abilities, crafting speed, quirk control, ingredient/reagent substitution; formal source conversion matrices; multi-Competency manufacturing procedure; and full client/server entitlement enforcement.

**Testing limitation:** The C# test cases have been added in the mechanics test project, but a .NET SDK and executable checkout were not available in the current environment. Review and compilation must occur before declaring the slice tested or deploy-ready.
