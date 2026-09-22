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

- `character-state`: raw Character-owned facts/state, such as ranks, training/proficiency state, or whether the Character has a tool proficiency;
- `source-input`: supplied by the applicable resolved rule/source, such as a DC, selected tool, selected ability, or creature-type competency;
- `runtime`: supplied for the current resolution attempt, such as a d20 result or Helper participation fact;
- `derived`: an effective/calculated mechanical value rather than raw Character state. It can be produced by an earlier Rules Core evaluation or by Character orchestration of already-resolved facts. For example, the component values consumed by a composite parent are `derived`, not `character-state`.

Rules Core owns the arithmetic and composition semantics represented by this contract. The Character backend owns obtaining Character-specific facts and transporting resolved values between operations when a multi-step workflow calls for it; transporting a derived value does not make that value raw Character state.

This distinction is important for 3.x source translation. A PCGen racial modifier, class progression fragment, or other partial source declaration is not automatically a final Character value. The consumer contract therefore requests the resolved input rather than fabricating a final score from incomplete source evidence.

## Generalized competency checks

`check.competency` represents an ability + competency check without assuming the 5e default ability associated with a skill. The check owns the selected/fixed Ability contribution separately from the competency contribution.

The preferred composition path supplies:

- the check Ability identity and resolved Ability modifier;
- d20 result and other check-local inputs;
- a nested `competency` request containing the competency mechanic key plus the Character facts/profile selection needed to resolve it.

Rules Core evaluates that competency to its non-Ability contribution and injects both the resolved concept identity and contribution into the check. A caller can still supply a pre-resolved direct competency contribution to the scalar check contract, but it must not supply both the direct contribution and nested composition in one request.

This supports checks such as Intelligence (Arcana), Dexterity (Survival), tool checks, older-edition ranked competencies, and composite competencies without placing ability/skill or competency arithmetic in the Character frontend/backend.

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

## Published competency baseline

The normal installation publishes reviewed bundled SRD skill/tool concepts through the Global Rules Layer during baseline synchronization. The mechanics consumer continues to read only effective resolved rules. It does not scan the Source Layer for unpublished competencies and does not maintain its own skill list.

Because publication uses the same canonical identity that ingestion already produced, direct-convergence names appear under the canonical Rule Concept while accessible source-equivalent profiles can remain available. Composite relationships continue to come from the existing Rules Layer relationship model rather than frontend hierarchy data.

## Competency metadata

Resolved skill and tool concepts carry normalized competency metadata for Character consumers. The contract can describe:

- ordinary skills, specialized skills, and tools;
- specialized skill family and specialty, such as `Knowledge (the planes)`;
- governing ability when the effective source provides one, with a unanimous accessible-profile ability used as presentation metadata when the selected representation omits it;
- whether the competency supports ranks;
- whether class-skill state is meaningful;
- whether training state is meaningful;
- trained-only state when the source determines it;
- Armor Check Penalty applicability when the source determines it.

PCGen translation normalizes understood competency semantics into `_rulesCore.competency` during ingestion. That normalized profile includes the competency kind, specialty family/value when applicable, governing ability, trained-only behavior, Armor Check Penalty applicability, rank/class-skill support, training support, game edition, and any capability qualification. The original `KEYSTAT`, `USEUNTRAINED`, `ACHECK`, and other PCGen evidence remains preserved under `_rulesCore.pcgen.unmappedSegments` for source inspection; the Character mechanics consumer does not parse those PCGen tags or infer specialty semantics from display names.

A competency can expose multiple normalized mechanical profiles across accessible canonical-equivalent source representations. This is important for reviewed direct equivalences such as 3.x `Bluff` -> `Deception` or `Craft (alchemy)` -> `Alchemist's Supplies`: when an effective published decision selects any reviewed representation, the other accessible profile evidence remains available. Genuine unresolved mechanical differences are not assigned a published representation by edition precedence; they remain subject to Rules Lawyer adjudication.

Profile selection is explicit and local to the competency evaluation. Each profile exposes its source-revision identity, capability requirements, evaluation profile, typed inputs, boolean requirements, and `canEvaluate`. The evaluation request may supply `competencyProfileSourceEntityRevisionId`; when it is omitted, Rules Core uses the profile belonging to the effective published source revision. This is not an edition-wide Character mode.

Rules Core owns the arithmetic described by the selected profile. The Character backend supplies Character-owned or already-resolved contributions, not a final competency value:

- a ranked 3.x profile uses `abilityContribution`, required `ranks`, an Armor Check Penalty adjustment only when that profile says the penalty applies, and `otherModifier`;
- a later-edition proficiency profile uses `abilityContribution`, optional `trainingContribution`, and `otherModifier`;
- each participating profile input declares whether it contributes to the Ability portion or the non-Ability competency portion;
- `classSkillState` is preserved as nonnumeric Character state and does not create a modifier by itself;
- a trained-only profile expresses `isTrained == true` as a Rules Core requirement;
- unknown or not-yet-faithful profiles expose `canEvaluate=false` instead of accepting an opaque final `value`.

A direct competency evaluation returns both the effective value and a Rules Core-produced breakdown containing `abilityContribution` and `competencyContribution`. The caller does not derive one by subtracting the other.

Checks that consume competencies expose a `competencyComposition` contract. The request can supply a nested `competency` input containing the competency mechanic key, profile selection, Character facts/contributions, and capabilities. For a direct competency, Rules Core evaluates that selected profile in check-composition mode, omits the profile's own Ability contribution, and injects only its non-Ability competency contribution into the check's declared contribution input. Rules Core also injects/verifies the competency concept identity.

For an effective composite competency, the same nested input can carry a `components` collection. Each component is itself a nested competency request, so Rules Core recursively evaluates its selected profile to a non-Ability contribution. The parent then passes those derived component contributions and any relationship-targeted `modifiers` to the existing `CompositeCompetencyEvaluator`. The result is the parent competency's non-Ability contribution, which is injected into the check. This gives the end-to-end flow:

```text
component Character facts/contributions
  -> Rules Core component competency profiles
  -> derived non-Ability component contributions
  -> Rules Core CompositeCompetencyEvaluator
  -> derived non-Ability parent competency contribution
  -> Rules Core generalized/source-defined check
```

The check supplies its own selected or fixed Ability contribution independently. Therefore a Wisdom-based Stealth check can derive Stealth from Hide + Move Silently while adding Wisdom exactly once; component profile Ability contributions are not required or counted during check composition. The same rule supports alternate-Ability direct competencies such as Dexterity-based Survival.

The same contract is used by generic `check.competency` and by source-defined Assessment, Carving, Manufacturing, and Enchanting checks. Supplying both a direct contribution and a nested competency composition request is rejected as ambiguous.

Consequently, an accessible 3.x profile does not add ranks to the effective 5e/5.5e profile. The Character backend can deliberately select the 3.x source profile when its Character capabilities support that mechanic. The static `competency.skill-ranks` mechanic remains a raw Character-owned quantity and is not a substitute for effective competency evaluation.

Ranks, class-skill state, training state, ability contributions, proficiency/training contributions, Armor Check Penalty adjustments, and other resolved modifiers remain Character inputs. Rules Core decides which inputs participate and how they combine; it does not fabricate Character state or advancement/rank-purchase rules.

## Composite competencies

The existing Rules Layer composite competency definitions remain authoritative:

- Hide + Move Silently -> Stealth;
- Listen + Spot -> Perception;
- Climb + Jump + Swim -> Athletics;
- Balance + Tumble -> Acrobatics.

The mechanics catalog exposes the effective persisted Rules Lawyer resolution. When the effective resolution is `derive-parent` and all component competencies are present in the effective accessible ruleset, the parent competency is evaluatable through the existing `CompositeCompetencyEvaluator`.

For standalone parent evaluation, the parent mechanic exposes its effective component values as inputs. Those inputs have origin `derived` because they are calculated mechanical values, not raw Character-owned state.

For nested check composition, callers do not need to pre-resolve those effective component values. They provide one nested competency input per required component. Rules Core verifies the component set against the effective relationship, recursively evaluates each component profile to its non-Ability contribution, applies relationship-targeted component modifiers, performs the registered composite arithmetic, applies parent modifiers, and passes the resulting non-Ability parent contribution into the check. Missing, duplicate, or unknown component inputs fail explicitly.

An `independent-parent` ruling keeps the relationship visible but disables component recursion. A nested request that still supplies components is rejected instead of silently deriving the parent. If the independent parent has an evaluatable profile, that parent profile can be composed into the check directly. The Character Sheet/backend must not independently reinterpret the ruling.

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

For a scalar definition it combines caller-supplied inputs according to the normalized mechanic. For a dynamic competency it selects the requested competency profile, enforces that profile's capability and boolean requirements, and combines only the contribution inputs declared by that profile. The caller never supplies a final opaque competency `value`. For a competency-consuming check, nested competency composition returns the non-Ability contribution directly from the selected profile; the caller does not subtract an Ability modifier or reproduce profile arithmetic. When that competency is an effective `derive-parent` composite, Rules Core recursively evaluates the supplied component competency inputs and delegates component modifiers, composite arithmetic, and parent modifiers to the existing `CompositeCompetencyEvaluator`. Conditional inputs are included only when their declared condition is satisfied. Conditional roll-mode rules can require multiple boolean conditions, which lets Rules Core distinguish "not proficient" from "not proficient and lacking qualified guidance." Contributor groups follow the same boundary: the caller supplies contributor facts, while Rules Core owns eligibility requirements, count limits, conditional full/fractional value, rounding, and how contributor totals enter the parent mechanic.

The batch endpoints accept multiple mechanic evaluations and build the effective global or Campaign mechanics context once for the request. This is the preferred Character backend path when resolving several values for one Character:

```text
POST /api/rules/mechanics/evaluate
POST /api/campaigns/{campaignId}/rules/mechanics/evaluate
```

The legacy per-mechanic evaluation endpoints remain available, but callers that need many mechanics should use the batch contract rather than rebuilding the effective context once per mechanic.

Roll-mode effects such as disadvantage are returned as structured rules. The consumer remains responsible for actually performing the roll according to its dice/runtime architecture.

## Character projection Armor Class

The bulk Character projection resolves `defense.ac.total` from equipped items only when the item role is mechanically unambiguous. Native/reference item type `LA` uses the armor base plus the full Dexterity modifier, `MA` caps the Dexterity contribution at +2, `HA` contributes no Dexterity modifier, and `S` contributes its shield bonus. Multiple armor bases, multiple shields, or an AC-bearing item whose role is not normalized produce an explicit unresolved/conflict result instead of implicit stacking.

Rules Core does not assume the ordinary unarmored formula when only a shield is equipped because another selected rule may replace that formula. Until such a formula is normalized from the effective ruleset, the result remains `missing-character-input` rather than being guessed.

## Character projection weapon attacks

The bulk Character projection resolves attack and damage modifiers for equipped 5e.tools-shaped melee (`M`) and ranged (`R`) weapons when the source supplies a damage expression. Ranged weapons use Dexterity. Melee weapons use Strength unless the item has the `F` (Finesse) property; finesse remains an explicit runtime choice between Strength and Dexterity rather than Rules Core silently selecting the larger modifier.

Weapon proficiency is derived when a selected class or other normalized rule grants a matching `qualification.weapons.*` capability. A caller can also supply the explicit boolean Character fact `weapon.<concept-key>.proficient`. Absence of both is treated as unknown, not as non-proficiency. Standard proficiency bonus, `bonusWeapon`, `bonusWeaponAttack`, `bonusWeaponDamage`, and caller-supplied `attack.<concept-key>.other` / `damage.<concept-key>.other` facts are incorporated with provenance-preserving contributions. Unsupported weapon roles remain unresolved.

## Character projection spellcasting resources

For a single selected caster whose effective class document exposes `rowsSpellProgression`, the bulk Character projection reads the row for the supplied class level and returns one `resource.spell-slot.<level>` resource for every nonzero slot maximum. The table remains source-owned; Rules Core selects the row and carries the class rule provenance into each resource.

The published Dorks & Dice resource-choice house rule is recognized from its actual `casterChoosesResourceSystem` and `availableResourceSystems` fields. `spellcasting.resource-system` accepts the normalized choices `spell-slots` or `spell-points` (human forms such as `spell slots` are normalized). Until that choice is supplied, spellcasting resources are `choice-required`. A spell-points choice remains explicitly unresolved until a point-progression/conversion rule is normalized; Rules Core does not derive a point pool from slot counts.

If more than one selected class contributes a standard spell-slot table, Rules Core reports the multiclass combination as unresolved instead of adding class-table slots together. Pact-magic and other nonstandard progressions are likewise preserved separately until their exact resource rules are normalized.

## Native class and subclass feature progression

The bulk Character projection reads acquisition levels directly from native 5e.tools class-feature and subclass-feature UIDs. Class feature references use `Name|Class|ClassSource|Level|...`; subclass feature references use `Name|Class|ClassSource|Subclass|SubclassSource|Level|...`. Object-wrapped `classFeature` and `subclassFeature` references are treated identically.

Only features whose source-defined acquisition level is at or below the supplied advancement level are projected as resolved Character features. Future features are omitted. A feature entry with no trustworthy acquisition level remains `applicable-unresolved` instead of being assigned to a level by position or display order.

## Character projection choices

The bulk Character projection exposes rule-defined selections as `choices` rather than requiring a Character client to reopen source JSON. A choice carries a stable choice key and group key, kind, state, legal options, selected value when supplied, source concept, and provenance. Runtime selections are sent back through the existing `choices` request collection.

`startingProficiencies.skills` and top-level `skillProficiencies` use the same normalized choice path. Fixed source skill proficiencies become Character training directly. A 5e.tools `choose.from` group becomes one choice slot per required selection, and `any: N` is populated from the effective accessible skill competency catalog. Fixed boolean grants and a choice can coexist in one source object. Duplicate selections within one source group and values outside the legal option set are explicit conflicts. When an option resolves to a canonical competency, selecting it feeds the Rules Core competency calculation directly; the Character Sheet does not need to mirror that selection into `TrainingKeys`.

Missing choices do not imply non-proficiency. Competencies that could still be selected remain unresolved until the Character supplies the choice or a complete external training-state set.

## Structured proficiency projection

The bulk Character projection reads 5e.tools `weaponProficiencies`, `armorProficiencies`, and `toolProficiencies` in addition to the human-facing `weapons`, `armor`, and `tools` arrays. Fixed boolean weapon and armor entries become qualifications/capabilities. Structured filter expressions such as a filtered martial-weapon set remain `applicable-unresolved`; Rules Core does not expand a source filter into broader grants unless the filter semantics are normalized.

Fixed named tool entries resolve against the accessible canonical tool competency catalog and feed Character training directly. `anyTool: N` becomes N ordinary choice slots populated from that catalog. Narrower category quantities such as `anyArtisansTool` or `anyMusicalInstrument` are reported as `source-unavailable` choices until tool-category metadata exists to generate a trustworthy legal option set. This preserves the source requirement without presenting an invented list.

## Ability-score choice projection

The bulk Character projection treats a 5e.tools `ability` array as alternate ability-score sets, not cumulative entries. When more than one set is present, Rules Core exposes an `ability-score-set` choice and projects only the selected set. This prevents mutually exclusive schemes such as “+2/+1” versus “+1/+1/+1” from being added together.

Within the selected set, fixed numeric adjustments are applied directly. `choose.from` supports source-defined `count` and `amount`, while `choose.weighted` creates one distinct ability selection per source weight (for example +2 and +1). Duplicate selections inside the same weighted/uniform group are explicit conflicts. Missing choices mark only the ability scores that the unresolved source choice can affect; unrelated abilities remain resolved.

Ability choices are returned through the same generic `choices` contract used for proficiencies, with stable keys, legal options, selected values, source concept, and provenance. Unknown choice shapes are preserved as `source-unavailable` rather than guessed.

## Starting class and multiclass proficiencies

When exactly one selected base-class advancement has a positive level, Rules Core treats it as the starting class automatically. When more than one base class is present, the Character must identify the starting class with the `advancement.starting-class` runtime choice (or the same key in string facts). The projection does not infer the starting class from request order. The generic `choices` response exposes the legal selected base-class options.

Only the starting class grants its native `proficiency`/`savingThrows` proficiencies and `startingProficiencies`. Other selected base classes instead project `multiclassing.proficienciesGained`, including the normalized skill/tool/weapon/armor choice paths supported by Character projection. Standard proficiency bonus still uses the total supplied base-class advancement levels. If the starting class is unresolved, starting-only and multiclass-only grants are withheld and the projection reports `choice-required` rather than over-granting Character capabilities.
