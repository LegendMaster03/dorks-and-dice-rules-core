# Character mechanics consumer contract

Rules Core exposes a normalized consumer contract for character-oriented tools. The contract is intentionally separate from source-native JSON and from Character-owned state.

The ownership boundary is:

```text
Rules Core
  owns mechanic identity, definitions, applicability, relationships,
  evaluation semantics, normalization, and source provenance

Character backend
  owns Character-specific state, selections, derived state orchestration,
  and supplying the inputs required by a mechanic

Character frontend
  owns presentation
```

Consumers must not parse `SourceEntityRevision.RawJson`, PCGen tags, 5e.tools fields, or publisher-native documents to rediscover mechanics already represented by this contract.

## Endpoints

Global effective mechanics:

```text
GET  /api/rules/mechanics
POST /api/rules/mechanics/evaluate
POST /api/rules/mechanics/{mechanicKey}/evaluate
```

Campaign effective mechanics:

```text
GET  /api/campaigns/{campaignId}/rules/mechanics
POST /api/campaigns/{campaignId}/rules/mechanics/evaluate
POST /api/campaigns/{campaignId}/rules/mechanics/{mechanicKey}/evaluate
```

`includeUnavailable=true` includes source-dependent mechanics whose activation source is not currently accessible/effective, with `isAvailableUnderRuleset=false`. Character capability requirements are different: a capability-driven definition can be available in Rules Core while declaring `requiredCapabilityKeys` that the Character backend must satisfy before presenting or evaluating it. Independently implemented external-public mechanics are available without a user source-package import and identify their external rules work through attribution rather than `SourcePackage.Key`.

Global access follows the existing resolved-rule source grant model. Campaign access follows the existing campaign read boundary. The mechanics API does not create a second source-access or campaign-authorization model.

## Stable mechanic and concept identity

`mechanicKey` identifies the normalized mechanical operation or value. `conceptKey` identifies a Rules Layer concept when the mechanic corresponds to one.

Examples include:

```text
check.competency
save.fortitude
save.reflex
save.will
defense.ac.touch
defense.ac.flat-footed
combat.base-attack-bonus
combat.grapple
competency.skill-ranks
resource.nonlethal-damage
defense.spell-resistance
defense.damage-reduction

check.harvesting.assessment
check.harvesting.carving
check.harvesting.total
check.crafting.manufacturing
check.crafting.enchanting
```

Resolved skill/tool concepts are exposed as competency mechanics with keys derived from the stable Rules Layer concept key:

```text
skill.arcana
-> competency.skill.arcana
```

The original `conceptKey` remains present in the response. The `competency.` mechanic namespace does not replace canonical identity.

## Input ownership

Every mechanic declares its inputs and their origin:

- `character-state`: owned by the Character backend, such as whether the Character has a tool proficiency;
- `source-input`: supplied by the applicable resolved rule/source, such as a DC, selected tool, selected ability, or creature-type competency;
- `runtime`: supplied for the current resolution attempt, such as a d20 result;
- `derived`: calculated by the Character backend from Character state and effective rules.

Rules Core owns how those inputs combine. The Character backend owns obtaining Character-specific values.

This distinction is important for 3.x source translation. A PCGen racial modifier, class progression fragment, or other partial source declaration is not automatically a final Character value. The consumer contract therefore requests the resolved input rather than fabricating a final score from incomplete source evidence.

## Generalized competency checks

`check.competency` represents an ability + competency check without assuming the 5e default ability associated with a skill. The caller supplies:

- the ability identity;
- the competency concept identity;
- d20 result;
- resolved ability modifier;
- resolved competency contribution, excluding the separately supplied ability modifier;
- any other applicable modifier;
- optional target DC.

This supports checks such as Intelligence (Arcana), Dexterity (Survival), tool checks, and older-edition competencies without placing ability/skill pairing logic in the Character frontend.

Checks also expose structured resolution semantics. Ability selection can be `fixed`, `caller-selected`, `rule-resolved`, or `character-resolved`; competency selection has the corresponding resolution model. This lets the generic check remain caller-selectable while Harvesting, Manufacturing, and Enchanting state which choices come from source rules or Character state.

## 3e/3.5e character mechanics

The consumer catalog includes normalized definitions for 3.x mechanics that a 5e/5.5e-style Character Sheet would not normally expose:

- Fortitude, Reflex, and Will saves, explicitly classified as `saving-throw` mechanics rather than generic defenses;
- touch AC and flat-footed AC;
- base attack bonus;
- grapple modifier, using the 3.x grapple-specific size modifier rather than the ordinary AC size modifier;
- skill ranks, keyed to the competency whose ranks are being supplied;
- nonlethal damage;
- spell resistance;
- damage reduction.

These definitions are **capability-driven**, not edition-toggle-driven. For example, `save.fortitude` declares a required Character capability of `save.fortitude`. A hybrid Character can therefore expose Fortitude, Touch AC, or BAB beside 5e/5.5e mechanics without changing the entire Character to a "3.5e mode." The Character backend owns capability derivation from the Character's actual classes, species, feats, source selections, and campaign rules.

The definitions intentionally do not infer values from unrelated fields. For example, touch AC consumes only contributions the Character backend has already determined apply to touch AC. The evaluation endpoint requires the declared capability key for capability-driven mechanics and rejects evaluation when it is absent.

## Competency metadata

Resolved skill and tool concepts carry normalized competency metadata for Character consumers. The contract can describe:

- ordinary skills, specialized skills, and tools;
- specialized skill family and specialty, such as `Knowledge (the planes)`;
- governing ability when the effective source provides one;
- whether the competency supports ranks;
- whether class-skill state is meaningful;
- whether training state is meaningful;
- trained-only state when the source determines it;
- Armor Check Penalty applicability when the source determines it.

PCGen translation normalizes understood competency semantics into `_rulesCore.competency` during ingestion. That normalized profile includes the competency kind, specialty family/value when applicable, governing ability, trained-only behavior, Armor Check Penalty applicability, rank/class-skill support, training support, game edition, and any capability qualification. The original `KEYSTAT`, `USEUNTRAINED`, `ACHECK`, and other PCGen evidence remains preserved under `_rulesCore.pcgen.unmappedSegments` for source inspection; the Character mechanics consumer does not parse those PCGen tags or infer specialty semantics from display names.

A competency can expose multiple normalized mechanical profiles across accessible canonical-equivalent source representations. This is important for reviewed direct equivalences such as 3.x `Bluff` -> `Deception` or `Craft (alchemy)` -> `Alchemist's Supplies`: selecting a later-edition representation for the published rule does not erase the accessible 3.x profile that supports ranks and class-skill state.

Profile selection is explicit and local to the competency evaluation. Each profile exposes its source-revision identity, capability requirements, evaluation profile, typed inputs, boolean requirements, and `canEvaluate`. The evaluation request may supply `competencyProfileSourceEntityRevisionId`; when it is omitted, Rules Core uses the profile belonging to the effective published source revision. This is not an edition-wide Character mode.

Rules Core owns the arithmetic described by the selected profile. The Character backend supplies Character-owned or already-resolved contributions, not a final competency value:

- a ranked 3.x profile uses `abilityContribution`, required `ranks`, an Armor Check Penalty adjustment only when that profile says the penalty applies, and `otherModifier`;
- a later-edition proficiency profile uses `abilityContribution`, optional `trainingContribution`, and `otherModifier`;
- each participating profile input declares whether it contributes to the Ability portion or the non-Ability competency portion;
- `classSkillState` is preserved as nonnumeric Character state and does not create a modifier by itself;
- a trained-only profile expresses `isTrained == true` as a Rules Core requirement;
- unknown or not-yet-faithful profiles expose `canEvaluate=false` instead of accepting an opaque final `value`.

A direct competency evaluation returns both the effective value and a Rules Core-produced breakdown containing `abilityContribution` and `competencyContribution`. The caller does not derive one by subtracting the other.

Checks that consume competencies expose a `competencyComposition` contract. The request can supply a nested `competency` input containing the competency mechanic key, profile selection, Character facts/contributions, and capabilities. Rules Core evaluates that selected profile in check-composition mode, omits the profile's own Ability contribution, and injects only its non-Ability competency contribution into the check's declared contribution input. Rules Core also injects/verifies the competency concept identity. This gives the composition flow:

```text
Character-owned facts/contributions
  -> Rules Core competency profile
  -> Rules Core non-Ability competency contribution
  -> Rules Core generalized/source-defined check
```

The check supplies its own selected or fixed Ability contribution independently. Therefore a Dexterity-based Survival check can use a Survival profile without assuming Survival's normal governing Ability, and the Ability contribution is not counted twice. The same contract is used by generic `check.competency` and by source-defined Assessment, Carving, Manufacturing, and Enchanting checks. Supplying both a direct contribution and a nested competency composition request is rejected as ambiguous.

Consequently, an accessible 3.x profile does not add ranks to the effective 5e/5.5e profile. The Character backend can deliberately select the 3.x source profile when its Character capabilities support that mechanic. The static `competency.skill-ranks` mechanic remains a raw Character-owned quantity and is not a substitute for effective competency evaluation.

Ranks, class-skill state, training state, ability contributions, proficiency/training contributions, Armor Check Penalty adjustments, and other resolved modifiers remain Character inputs. Rules Core decides which inputs participate and how they combine; it does not fabricate Character state or advancement/rank-purchase rules.

## Composite competencies

The existing Rules Layer composite competency definitions remain authoritative:

- Hide + Move Silently -> Stealth;
- Listen + Spot -> Perception;
- Climb + Jump + Swim -> Athletics;
- Balance + Tumble -> Acrobatics.

The mechanics catalog exposes the effective persisted Rules Lawyer resolution. When the effective resolution is `derive-parent` and all component competencies are present in the effective accessible ruleset, the parent competency is evaluatable through the existing `CompositeCompetencyEvaluator`.

An `independent-parent` ruling keeps the relationship visible but disables composite derivation. The Character Sheet must not independently reinterpret this ruling.

## Loot Tavern Harvesting & Crafting Lite

Rules Core models the reusable mechanical semantics of Loot Tavern's public Harvesting & Crafting Lite release while leaving publisher-owned tables/content in the source layer.

Public source reference:

```text
Loot Tavern
Harvesting & Crafting Lite
https://www.patreon.com/LootTavern/posts/helianas-and-to-107406117
```

The consumer contract models:

- Assessment as a generalized competency check with fixed Intelligence and the rule-resolved creature-type competency;
- Carving as a generalized competency check with fixed Dexterity and the same rule-resolved creature-type competency;
- Harvesting as the sum of Assessment and Carving plus any evaluated Helper contributions;
- disadvantage on both harvesting component checks when one creature performs both roles;
- Helpers as a first-class contributor group rather than a caller-precalculated bonus;
- a creature-size helper cap of Tiny 0, Small 1, Medium 2, Large 4, Huge 6, and Gargantuan 10;
- a proficient Helper contributing its full supplied proficiency bonus;
- a non-proficient Helper contributing one-half of its supplied proficiency bonus, rounded down;
- the ordinary Help action not supplying a Harvesting or Crafting bonus;
- Manufacturing as a rule-resolved tool/ability competency check;
- disadvantage on Manufacturing when the Character lacks the required tool proficiency, unless the GM-resolved input says the character has qualified guidance from a book or a creature with the requisite proficiency;
- qualified guidance does not grant tool proficiency and therefore does not add a tool-proficiency contribution;
- Enchanting as a rule-resolved competency check using the Character-resolved spellcasting ability;
- the spellcasting requirement for Enchanting.

Harvesting & Crafting Lite follows the same external-public-rules boundary used by the Kaiju Fighting Lite integration. Dorks & Dice implements the public mechanical procedure independently and links to the creator-hosted public release. Availability therefore does not depend on a magic package key, a user importing the PDF, or the PDF producing a resolved Rule Concept. The static definitions carry the stable work key `loot-tavern.harvesting-crafting-lite`, provider `Loot Tavern`, publication metadata, and the official creator-hosted URL.

The contract does **not** bundle Harvest tables, creature-type-to-skill tables, component DCs, manufacturing tables, recipes, item data, materials, prose, art, or layout from the publisher release. Source-selected values remain typed `source-input` requirements when the procedure needs them.

Loot Tavern mechanics carry explicit source attribution with `presentationRequired=true` and `referenceLinkRequired=true`, so a downstream consumer can present the required source reference without hard-coding publisher-specific behavior.

Helper handling uses the general contributor-group contract. The Character backend supplies per-helper Character/runtime facts such as `proficiencyBonus`, `isProficient`, whether the creature participated for the entire required duration, and whether it was an Assessment or Carving participant, plus the source-selected `creatureSize` context. Contributor groups can declare boolean eligibility requirements. Rules Core rejects a submitted Helper when those supplied facts do not satisfy the Helper requirements; inclusion in the contributor list is not itself proof of eligibility.

For an eligible Helper, Rules Core validates the contributor count against the mechanic's context table, performs the full-or-fractional contribution and rounding, and adds the result to the Harvesting total. The request never contains one opaque `helperBonus`. The group also explicitly reports `standardHelpActionApplies=false`; an ordinary Help-action flag is not interpreted as a substitute for Helper participation.

## Provenance and access

A competency mechanic derived from a resolved Rules Layer concept carries only provenance already accessible to the current consumer:

- package key/display name;
- provider;
- source code and exact source revision number;
- canonical publication/work key and display name when available;
- D&D game edition, release kind, and publication date when available;
- source entity title;
- source representation URI when available.

Private source names/content are not surfaced through this contract when the current user can not resolve that source. Canonical identity remains global recognition metadata; source access remains package/grant scoped.

## Evaluation boundary

Evaluation is deterministic. Rules Core does not roll dice, select Character state, choose a source table row, or mutate Character data.

For a scalar definition it combines caller-supplied inputs according to the normalized mechanic. For a dynamic competency it selects the requested competency profile, enforces that profile's capability and boolean requirements, and combines only the contribution inputs declared by that profile. The caller never supplies a final opaque competency `value`. For a competency-consuming check, nested competency composition returns the non-Ability contribution directly from the selected profile; the caller does not subtract an Ability modifier or reproduce profile arithmetic. Conditional inputs are included only when their declared condition is satisfied. Conditional roll-mode rules can require multiple boolean conditions, which lets Rules Core distinguish "not proficient" from "not proficient and lacking qualified guidance." For a composite competency, the Character backend supplies the already-resolved effective component competency values and Rules Core delegates the parent calculation to the existing `CompositeCompetencyEvaluator`; the composite relationship arithmetic remains Rules Core-owned. Contributor groups follow the same boundary: the caller supplies contributor facts, while Rules Core owns eligibility requirements, count limits, conditional full/fractional value, rounding, and how contributor totals enter the parent mechanic.

The batch endpoints accept multiple mechanic evaluations and build the effective global or Campaign mechanics context once for the request. This is the preferred Character backend path when resolving several values for one Character:

```text
POST /api/rules/mechanics/evaluate
POST /api/campaigns/{campaignId}/rules/mechanics/evaluate
```

The legacy per-mechanic evaluation endpoints remain available, but callers that need many mechanics should use the batch contract rather than rebuilding the effective context once per mechanic.

Roll-mode effects such as disadvantage are returned as structured rules. The consumer remains responsible for actually performing the roll according to its dice/runtime architecture.
