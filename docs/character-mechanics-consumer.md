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
POST /api/rules/mechanics/{mechanicKey}/evaluate
```

Campaign effective mechanics:

```text
GET  /api/campaigns/{campaignId}/rules/mechanics
POST /api/campaigns/{campaignId}/rules/mechanics/{mechanicKey}/evaluate
```

`includeUnavailable=true` includes known source-dependent mechanics whose required source is not currently accessible/effective, with `isAvailableUnderRuleset=false`. Character capability requirements are different: a capability-driven definition can be available in Rules Core while declaring `requiredCapabilityKeys` that the Character backend must satisfy before presenting or evaluating it.

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

- Fortitude, Reflex, and Will saves;
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

For PCGen 3.x sources, Rules Core reads this from the already-preserved mechanical translation, including retained `KEYSTAT`, `USEUNTRAINED`, and `ACHECK` evidence under `_rulesCore.pcgen.unmappedSegments`. The Character consumer does not parse the native PCGen record itself.

Ranks, class-skill state, training state, and Armor Check Penalty adjustment remain Character inputs. Source metadata says which concepts exist and which rules apply; it does not fabricate a Character's current ranks or training.

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

- Assessment as a generalized competency check using Intelligence;
- Carving as a generalized competency check using the source-selected carving ability;
- Harvesting as the sum of Assessment and Carving;
- disadvantage on both harvesting component checks when one creature performs both roles;
- Manufacturing as a rule-resolved tool/ability competency check;
- disadvantage on Manufacturing when the Character lacks the required tool proficiency, without adding a proficiency contribution to the check;
- Enchanting as a rule-resolved competency check using the Character-resolved spellcasting ability;
- the spellcasting requirement for Enchanting.

The contract does **not** bundle Harvest tables, creature-type-to-skill tables, component DCs, manufacturing tables, recipes, item data, materials, or other publisher-owned source content. Those values are represented as `source-input` requirements and must come from an accessible/effective Loot Tavern source.

Loot Tavern mechanics carry explicit source attribution with `presentationRequired=true` and `referenceLinkRequired=true`, so a downstream consumer can present the required source reference without hard-coding publisher-specific behavior.

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

The evaluation endpoint is deterministic. It does not roll dice, select Character state, choose a source table row, or mutate Character data.

For a scalar definition it combines caller-supplied inputs according to the normalized mechanic. Conditional inputs are included only when their declared Character-state condition is satisfied. For a composite competency it delegates to the existing Rules Core composite evaluator and accepts explicit concept-targeted modifiers.

Roll-mode effects such as disadvantage are returned as structured rules. The consumer remains responsible for actually performing the roll according to its dice/runtime architecture.
