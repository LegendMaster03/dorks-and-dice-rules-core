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

Rules-defined identity metadata follows the same boundary. Backgrounds, alignments, deities, species/races, size categories, advancement rules, and similar source concepts remain Rules Core definitions with stable concept identity and provenance. The Character owns which applicable concept is selected and mutable advancement state such as current XP. Player name, Campaign display name, appearance, age/height/weight entered by the player, personality traits, ideals, bonds, flaws, backstory, allies/organizations, symbols, and other freeform biography remain Character/Site data. Rules Core represents a physical or biographical constraint only when a rule source actually defines one; it does not create rule concepts merely because an official paper sheet has a field.

A selected race/species size is projected directly as the text mechanic `character.size-category` with structured source contributions and provenance. Common D&D size codes are normalized to their canonical display names, while an unknown source-defined size identity is retained rather than discarded. When a source defines more than one allowed Size, Rules Core exposes an ordinary `size-category` Character choice and leaves the Size mechanic `choice-required` until the Character supplies one of the source-defined options. Conflicting resolved size rules produce an explicit conflict. Character consumers therefore do not need to reopen a race/species source document, pick the first array entry, or infer Size from a label.

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

Character-support discovery/projection and recovery resolution use the same effective rules context:

```text
GET  /api/rules/mechanics/support
POST /api/rules/mechanics/support
POST /api/rules/mechanics/recovery/{procedureKey}/resolve

GET  /api/campaigns/{campaignId}/rules/mechanics/support
POST /api/campaigns/{campaignId}/rules/mechanics/support
POST /api/campaigns/{campaignId}/rules/mechanics/recovery/{procedureKey}/resolve
```

The support `GET` endpoints are discovery calls with no Character facts. They retain applicable definitions even when Character capability/state is not yet available and report an unresolved state instead of deleting the row. The support `POST` endpoints accept Character-owned facts and capability keys so passive values and qualification state can be projected. Recovery resolution is separate because a procedure can require additional player choices, rolls, resource expenditure, or other runtime facts.

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

The original `conceptKey` remains present in the response. The source-shaped implementation mechanic namespace is retained for evaluation and compatibility; it is not the Character semantic identity.

### Universal competency surface

The mechanics response also includes a top-level `competencies` catalog. This is the authoritative Character-facing semantic surface. It contains one entry per learned competency, even when more than one source-shaped Rule Concept or mechanical facet implements it.

A universal competency does not require a cross-edition equivalent. A tool-only, kit, instrument, gaming-set, third-party, or manual/custom capability can be a valid universal competency by itself. Rules Core does not fabricate a historical skill or Craft specialty merely to provide a second facet.

For example:

```text
competency.alchemy
  display: Alchemy
  family: Craft
  trainingStateKey: competency.alchemy.training
  implementation mechanics:
    competency.skill.craft-alchemy
    competency.tool.alchemists-supplies
```

The two implementation mechanics remain available because a 3.x ranked skill and a later tool proficiency do not share a numeric calculation. The Character consumer does not render them as separate learned competencies and does not infer their relationship. The universal entry already exposes its implementation mechanic keys, compatibility keys, reviewed aliases, profiles, facets, normalized `mechanics` semantics, relationships, provenance, and open-ended `presentationCategory`.

`presentationCategory` is Rules Core-owned semantic placement metadata. `skill` identifies ordinary skill presentation. `competency` identifies Craft/tool and other broader learned-capability presentation. Future non-`skill` values remain valid open categories. Consumers must not recover this classification by matching words such as `Craft`, `Tools`, `Kit`, or `Supplies`.

The universal `mechanics.governingAbility` contract reports `fixed`, `varies-by-implementation`, or `none`, together with normalized Ability keys. Equivalent source spellings such as `wis` and `wisdom` converge before this resolution. A real conflict remains explicit instead of causing the universal Ability to disappear.

`Craft`, `Perform`, and `Profession` appear as explicit organizational entries whose `childCompetencyKeys` identify independently trainable children. Their reviewed children are mechanically usable even when no effective source-shaped row exists: Rules Core supplies a `profileOrigin: rules` ranked-skill implementation with no fabricated source attribution. `Knowledge` does not appear as a family.

Historical `Speak Language` evidence is handled by the language-proficiency subsystem and is excluded from this ordinary competency surface.

See `docs/universal-character-concepts.md` for the reviewed catalog, alias decisions, migration strategy, and Size model.

## Input ownership

Every mechanic declares its inputs and their origin:

- `character-state`: raw Character-owned facts/state, such as ranks, training/proficiency state, or whether the Character has a tool proficiency;
- `source-input`: supplied by the applicable resolved rule/source, such as a DC, selected tool, selected ability, or creature-type competency;
- `runtime`: supplied for the current resolution attempt, such as a d20 result or Helper participation fact;
- `derived`: an effective/calculated mechanical value rather than raw Character state. It can be produced by an earlier Rules Core evaluation or by Character orchestration of already-resolved facts. For example, the component values consumed by a composite parent are `derived`, not `character-state`.

Rules Core owns the arithmetic and composition semantics represented by this contract. The Character backend owns obtaining Character-specific facts and transporting resolved values between operations when a multi-step workflow calls for it; transporting a derived value does not make that value raw Character state.

This distinction is important for 3.x source translation. A PCGen racial modifier, class progression fragment, or other partial source declaration is not automatically a final Character value. The consumer contract therefore requests the resolved input rather than fabricating a final score from incomplete source evidence.

## Character support: recovery, passive values, and qualifications

The Character-support projection is one extension of the Character mechanics consumer boundary, but its three result families remain semantically distinct:

- recovery procedures are executable rule procedures;
- passive values are scalar Character mechanics whose formula belongs to Rules Core;
- qualifications are Character training/proficiency/knowledge facts interpreted under Rules Core definitions.

All three read the same published global or Campaign-effective Rules Layer snapshot and use the same access-aware source provenance. They are projected only from normalized `_rulesCore.characterSupport` metadata on the effective rule document. Character-oriented consumers do not parse 5e.tools, PCGen, or publisher-native fields to reconstruct these features.

A support entry can be `available`, `not-applicable`, `unresolved-character-state`, `unresolved-rule-definition`, or resolved as appropriate. A required capability is not treated as absent when the caller simply has not supplied Character capability state: discovery reports it as unresolved. When the caller explicitly supplies a capability set that omits a required capability, the entry is not applicable.

### Recovery procedures

"Short Rest" and "Long Rest" are stable Character Sheet user intents, not universal algorithms. Rules Core does not define either label to mean a particular duration, Hit Dice rule, HP restoration rule, spell-slot reset, exhaustion interaction, or condition interaction.

An effective rule can publish any number of recovery procedures. A procedure has a stable `procedureKey`, display name, optional presentation role such as `short-rest` or `long-rest`, applicability/capability requirements, declared typed inputs, source attribution, and structured runtime requirements. Procedures without those roles are equally valid. More than one procedure can use the same presentation role when the effective rules genuinely expose multiple procedures; the consumer must use the discovered procedure key rather than assuming the role itself is an algorithm.

Recovery resolution accepts only:

- declared Character/source/runtime inputs;
- required Character capability keys;
- declared player choices;
- declared roll results supplied through the existing runtime roll boundary.

It does not accept final Character state such as a new HP total, restored slot collection, or reset feature-use state unless that value is itself a declared rule input for a different mechanical purpose. Undeclared typed inputs are rejected.

If information is missing, Rules Core returns a structured continuation state: `input-required`, `choice-required`, or `roll-required`. It does not guess a choice or perform browser-side dice behavior. A resolved procedure returns structured consequences such as a target kind/key, operation, resolved amount/value, and optional rule-defined reference key. Operations and targets are intentionally open mechanical identities rather than a fixed 5e rest effect list.

Rules Core resolves what should happen. The Character-owning backend persists those consequences to Character runtime state. Rules Core does not mutate Character Sheet storage.

### Passive values

Passive/automatic Character values use the `passive-value` mechanic kind. Each normalized passive mechanic supplies its own evaluation kind, constant, participating inputs, applicability, optional presentation role, optional relationship to another mechanic/concept/ability, and source attribution.

There is no built-in `Passive Perception / Passive Investigation / Passive Insight` taxonomy and no universal `10 + modifier` formula. A 5e passive check can use that formula only when normalized rule evidence explicitly defines it. Older-edition take-10/automatic mechanics or source-specific passive values retain their own identities and formulas rather than being renamed into 5e terminology.

If required Character input is absent, the passive value remains present with `unresolved-character-state`. The frontend renders the supplied resolved value when one exists; it does not recalculate the formula.

### Proficiencies, training, and qualifications

Qualification projection is intentionally not a fixed Armor/Weapons/Tools/Languages table. A normalized qualification supplies a stable `qualificationKey`, display name, arbitrary category and optional family, a typed Character-owned state input, applicability/capability requirements, optional associated Rule Concept identity, and source attribution.

This model can represent weapon groups, exotic-weapon proficiency, tool training, class-skill state, language knowledge, vehicles, instruments, magic/psionic training, and future source-specific categories without adding universal schema rows.

The distinction from competency profiles is deliberate:

- "Stealth is trained-only under this profile" is a Rules Core competency-profile rule.
- "This Character is trained in Stealth" is Character-owned qualification/training state projected through a qualification definition.
- "This Character is proficient with martial weapons" is a Character qualification fact, potentially capability-qualified.
- "This Character knows Draconic" is a Character qualification/knowledge fact.
- "This source says a class grants martial-weapon proficiency" is source rule evidence that Character derivation can use; it is not automatically a persisted Character fact.

Rules Core defines rule meaning, applicability, derivation contracts, and canonical relationships. The Character backend owns selections and acquired/runtime state. Missing Character state therefore remains unresolved rather than being fabricated from source grants.

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

These definitions are **capability-driven**, not edition-toggle-driven. For example, `save.fortitude` declares a required Character capability of `save.fortitude`. A hybrid Character can therefore expose Fortitude, Touch AC, or BAB beside 5e/5.5e mechanics without changing the entire Character to a "3.5e mode." The bulk projection derives the core 3.x capability set when the Character actually selects a normalized 3.x class; direct per-mechanic evaluation continues to require the caller to supply the applicable capability explicitly. Additional capabilities from species, feats, items, and other source selections remain source-driven rather than edition-wide toggles.

The definitions intentionally do not infer values from unrelated fields. For example, touch AC consumes only contributions the Character backend has already determined apply to touch AC. The evaluation endpoint requires the declared capability key for capability-driven mechanics and rejects evaluation when it is absent.

## Published competency baseline

The normal installation publishes reviewed bundled SRD skill/tool concepts through the Global Rules Layer during baseline synchronization. The mechanics consumer continues to read only effective resolved rules. It does not scan the Source Layer for unpublished competencies and does not maintain its own skill list.

Because publication uses the same canonical identity that ingestion already produced, direct-convergence names appear under the canonical Rule Concept while accessible source-equivalent profiles can remain available. Composite relationships continue to come from the existing Rules Layer relationship model rather than frontend hierarchy data.

## Competency metadata

Resolved skill and tool concepts carry normalized competency metadata for Character consumers. The Character Sheet does not need to inspect `_rulesCore`, source-native JSON, or edition-specific skill lists to understand the structure.

The contract exposes, as applicable:

- ordinary skill/tool identity;
- competency family/category and specialty;
- whether a row is an organizational family;
- learned-competency `identityKey` / `identityName`;
- `sharedTrainingKey` for Character-owned training/proficiency state shared across established facets;
- facet type and the complete facet set for the learned competency;
- the Rule mechanic keys belonging to each facet;
- governing Ability;
- rank support;
- class-skill-state support;
- training-state support;
- trained-only state;
- Armor Check Penalty applicability;
- scoped/related competency relationships and relationship scope;
- source profiles and provenance.

### Families and specialized competencies

`Craft`, `Perform`, and `Profession` can be organizational competency families. Their children remain independent competencies. `Craft (alchemy)` and `Craft (blacksmithing)`, for example, do not share a rank value merely because both render under Craft. The same rule applies to separate Perform and Profession specialties.

`Knowledge` intentionally does not use this visible family structure. Reviewed `Knowledge (X)` identities normalize directly to `X`, so a Character consumer sees `Arcana`, `History`, `Psionics`, `The planes`, and similar competencies directly while provenance retains the historical source name.

A family hierarchy and a composite competency can look similar in a nested UI, but they are mechanically different. A family is taxonomy containing independent children. A composite competency, such as Hide + Move Silently -> Stealth, has an explicit Rules Layer value-composition relationship.

### Shared competency facets

A learned competency can have more than one mechanical facet without making those facets numerically equivalent. Reviewed shared identities include Alchemy, Forgery, Calligraphy, Carpentry, Cobbling, Gemcutting, Leatherworking, Painting, Pottery, Stonemasonry, and Weaving. The complete reviewed mapping and deliberately separate tool relationships are maintained in `docs/competency-reconciliation-audit.md`.

For Alchemy, the catalog can expose both:

```text
Alchemy
  skill facet
    3e Alchemy
    3.5e Craft (alchemy)
  tool facet
    Alchemist's Supplies
```

The skill and tool remain separate Rule Concepts and separate canonical source identities. Both definitions carry the same learned `identityKey` and `sharedTrainingKey`. After the catalog is assembled, Rules Core groups every effective mechanic with that identity and exposes the complete facet list, including the mechanic keys and profile revision IDs that belong to each facet.

Shared training state does not imply shared numerical state. A ranked profile consumes `ranks` and its source-specific skill mechanics. A later tool profile consumes the later proficiency/training inputs. The Character backend can store one conceptual trained/proficient fact under the shared training key while retaining facet-specific rank/class-skill state separately.

The same model is used for Forgery / Forgery Kit. It is extensible, but new shared identities require reviewed source/rules evidence.

### Scoped and related relationships

The contract exposes cross-type relationships that do not share training identity:

- Open Lock -> Thieves' Tools, scope `open-lock`;
- Disable Device -> Thieves' Tools, scope `disable-device`;
- Disguise -> Disguise Kit as `related-competency`.

A scoped relationship never grants unrestricted proficiency in the target tool. A related relationship reports the rules connection without asserting that training in one is training in the other.

### Mechanical profiles and evaluation

Each competency profile retains its own source revision, edition/profile identity, capability requirements, typed inputs, boolean requirements, and `canEvaluate` state. Profile selection is local to a competency evaluation through `competencyProfileSourceEntityRevisionId`; it is not an edition-wide Character mode.

Rules Core owns the arithmetic described by the selected profile. The Character backend supplies Character-owned or already-resolved contributions, not a final competency value:

- a ranked 3.x profile uses `abilityContribution`, required `ranks`, an Armor Check Penalty adjustment only when that profile says the penalty applies, and `otherModifier`;
- a later-edition proficiency profile uses `abilityContribution`, optional `trainingContribution`, and `otherModifier`;
- `classSkillState` remains nonnumeric Character state;
- a trained-only profile expresses `isTrained == true` as a Rules Core requirement;
- unknown or not-yet-faithful profiles expose `canEvaluate=false` rather than accepting an opaque final value.

A direct competency evaluation returns the effective value and a Rules Core-produced Ability/competency breakdown. Numeric fields never migrate between incompatible facet profiles.



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

Rules Core models the reusable mechanical semantics of Loot Tavern's public Harvesting & Crafting Lite release and now exposes the public creature-type Harvesting defaults through a dedicated rules catalog.

Public source reference:

```text
Loot Tavern
Harvesting & Crafting Lite
https://www.patreon.com/LootTavern/posts/helianas-and-to-107406117
```

The Harvesting catalog is available independently from Character mechanics:

```text
GET  /api/rules/harvesting
POST /api/rules/harvesting/resolve
POST /api/campaigns/{campaignId}/rules/harvesting/resolve
```

The catalog supplies each supported creature type's associated Rules Core skill concept and its default harvestable components with Component DCs. Resolution by `creatureConceptKey` reads the effective monster rule, derives its creature type, and overlays optional creature-specific Harvesting changes. Resolution by `creatureType` is available when no monster Rule Concept exists. A request may then apply a final manual edit layer for encounter-specific corrections without mutating the published monster rule.

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

The catalog intentionally separates **creature-type defaults** from **specific-creature changes**. A monster mechanical document may provide:

```json
{
  "_rulesCore": {
    "harvesting": {
      "removeComponents": ["egg"],
      "upsertComponents": [
        {
          "key": "unique-organ",
          "displayName": "Unique organ",
          "componentDc": 25,
          "quantity": 1
        }
      ]
    }
  }
}
```

`removeComponents` removes impossible/default parts for that creature. `upsertComponents` can modify a default component or add a creature-specific component. The same remove/upsert shape is accepted as a runtime `manualEdits` layer by the resolver, with manual edits applied after stored creature changes. This gives a future Harvesting workspace an editable table even when imported monster data has no Harvesting metadata.

Rules Core still does **not** copy manufacturing tables, recipes, item data, materials, prose, art, or source layout into the generic Character mechanics surface. Those remain separate source/rule concerns. The existing check definitions continue to use typed `source-input` requirements where a procedure needs source-selected runtime context.

Loot Tavern mechanics carry explicit source attribution with `presentationRequired=true` and `referenceLinkRequired=true`, so a downstream consumer can present the required source reference without hard-coding publisher-specific behavior.

Helper handling uses the general contributor-group contract. The Character backend supplies per-helper Character/runtime facts such as `proficiencyBonus`, `isProficient`, whether the creature participated for the entire required duration, and whether it was an Assessment or Carving participant, plus the source-selected `creatureSize` context. Contributor groups can declare boolean eligibility requirements. Rules Core rejects a submitted Helper when those supplied facts do not satisfy the Helper requirements; inclusion in the contributor list is not itself proof of eligibility.

For an eligible Helper, Rules Core validates the contributor count against the mechanic's context table, performs the full-or-fractional contribution and rounding, and adds the result to the Harvesting total. The request never contains one opaque `helperBonus`. The group also explicitly reports `standardHelpActionApplies=false`; an ordinary Help-action flag is not interpreted as a substitute for Helper participation.

## D20 roll-selection modes

Rules Core treats Normal, Advantage, Disadvantage, and Emphasis as peer d20
selection modes:

- `normal`: generate one d20 and use it;
- `advantage`: generate two d20s and use the higher value;
- `disadvantage`: generate two d20s and use the lower value;
- `emphasis`: generate two d20s and use the value furthest from 10.

Rules Core owns these semantics and may attach a mode to a mechanic through
`conditionalRollRules`. It does **not** centralize random-number generation.
Each consuming Tool generates its own d20 values locally and applies the same
selection contract.

A Tool must allow the user to override the rule-derived mode before rolling.
The rule-derived mode remains useful context; a manual override changes the
effective mode for that roll rather than rewriting the Rules Layer.

For Emphasis, equally distant results such as 7 and 13 are a genuine selection
tie under the stated rule. The canonical selector preserves the first generated
die as its deterministic selected value and reports `selectionTied=true`, so a
consumer can show both raw dice instead of silently inventing an additional
game rule.

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

When no armor base is selected, the ordinary unarmored formula is `10 + Dexterity modifier`, with an equipped shield and explicit other modifiers applied normally. Rule-projected replacement formulas remain a separate extension point rather than being encoded as class-name exceptions.

A selected normalized 3.x class grants the core 3.x Character capabilities used by the bulk projection, including Fortitude/Reflex/Will, BAB, grapple, touch AC, flat-footed AC, skill ranks, and nonlethal-damage tracking. Touch AC uses the 3.x formula and omits armor, shield, and natural-armor bonuses. Flat-footed AC removes a positive Dexterity contribution and ordinary dodge contribution by default while retaining armor, shield, size, natural armor, and deflection. Explicit rule-derived Character facts can replace the Dexterity or flat-footed dodge contributions when a feature changes those normal rules. For a pure 3.x projection, ordinary AC is also composed from the same 3.x contribution set; a hybrid Character that also has standard 5.x proficiency keeps its normal effective AC while exposing the additional 3.x defenses.

Every calculated AC value carries its structured contribution list. The 3.x total preserves distinct armor, shield, Dexterity, size, natural-armor, deflection, dodge, and other contribution identities rather than forcing them into one opaque bonus. Arbitrary source-defined additions use the open `defense.ac.contribution.<identity>` namespace; touch-only and flat-footed-only additions use `defense.ac.touch.contribution.<identity>` and `defense.ac.flat-footed.contribution.<identity>`. Rule effects may target the same namespaces, retaining their source provenance and active-condition state. A Details surface therefore does not need to reconstruct or rename source-defined contribution types from the final number.

Miss Chance is a separate mechanic, not an AC contribution. Normalized rules should use the stable `defense.miss-chance` mechanic identity when that rule applies and may supply a `percent` unit through `_rulesCore.character.passives`. Other source-defined avoidance mechanics may use their own keys; the projection does not force them into Miss Chance or Armor Class.

## Character projection equipment definitions

The request separates item definitions from Character inventory state. `itemConceptKeys` asks Rules Core to project semantic definitions for items the Character backend cares about; `equippedItemConceptKeys` is the subset currently equipped and therefore eligible to contribute active combat effects. Equipped concepts are automatically included in the item-definition set.

The `equipment` result exposes stable item/concept identity, source provenance, item type/category, normalized armor role when known, weight and unit, ammunition relationship text when the source defines it, capacity text, attunement requirement, and arbitrary source property keys. These are rule/source definitions. Occurrences, possessed quantity, carried/equipped state, current ammunition/resource quantity, and current attunement selections remain Character state. An unequipped item can therefore be described without granting its AC or attack effects.

Currency totals are likewise Character state. Rules-defined currency denominations/conversions, load formulas, encumbrance thresholds, container capacities, ammunition rules, and item prerequisites belong in Rules Core when their effective source has normalized them. They should be represented through canonical concepts and structured mechanics/effects/procedures rather than parsed from item prose. Unknown source semantics remain unresolved instead of receiving invented stack, capacity, conversion, or encumbrance defaults.

## Character projection weapon attacks

The bulk Character projection resolves attack and damage modifiers for equipped 5e.tools-shaped melee (`M`) and ranged (`R`) weapons when the source supplies a damage expression. Ranged weapons use Dexterity. Melee weapons use Strength unless the item has the `F` (Finesse) property; finesse remains an explicit runtime choice between Strength and Dexterity rather than Rules Core silently selecting the larger modifier.

Weapon proficiency is derived when a selected class or other normalized rule grants a matching `qualification.weapons.*` capability. A caller can also supply the explicit boolean Character fact `weapon.<concept-key>.proficient`. Absence of both is treated as unknown, not as non-proficiency. Standard proficiency bonus, `bonusWeapon`, `bonusWeaponAttack`, `bonusWeaponDamage`, and caller-supplied `attack.<concept-key>.other` / `damage.<concept-key>.other` facts are incorporated with provenance-preserving contributions. Unsupported weapon roles remain unresolved.

## Character projection spellcasting resources

Spell actions preserve source-defined casting metadata when the spell concept provides it. The action result can carry the spell level, school identity, full casting-time text, range, V/S/M component identities, material-component text, duration, ritual state, concentration state, source concept identity, and provenance. The Character Sheet does not need to reopen the spell source document to reconstruct those fields.
For a single selected standard caster whose effective class document exposes `rowsSpellProgression`, the bulk Character projection reads the row for the supplied class level and returns one `resource.spell-slot.<level>` resource for every nonzero slot maximum. The table remains source-owned; Rules Core selects the row and carries the class rule provenance into each resource.

A class with `casterProgression: "pact"` is projected through its Pact Magic table instead of `rowsSpellProgression`. Rules Core locates the source `Spell Slots` and `Slot Level` columns, reads the supplied class-level row, and returns `resource.pact-slot.<class-concept-key>.level-<slot-level>`. Pact slots remain a `pact-magic` resource system and coexist with normal spell slots; selecting a Pact Magic class alongside a standard caster does not create a false multiclass-slot conflict.

Known/prepared spell lists are Character state inputs associated with stable spell concepts; source and grant provenance remain Rules Core data. A source edition's preparation rules do not become an implicit frontend restriction. The effective published rules decide whether preparation is required. A normalized `preparedSpellRestriction` policy is exposed as the stable `spellcasting.preparation-restriction` mechanic with source provenance. In the Dorks & Dice effective rules, the baseline `house.spell-preparation` value is `removed`; the Character Sheet must not reintroduce preparation simply because an underlying official source document contains it.

The published Dorks & Dice resource-choice house rule is recognized from its actual `casterChoosesResourceSystem` and `availableResourceSystems` fields. `spellcasting.resource-system` accepts the normalized choices `spell-slots` or `spell-points` (human forms such as `spell slots` are normalized). Until that choice is supplied, standard spellcasting resources are `choice-required`. A spell-points choice uses the official 2014 spell-point progression for standard Spellcasting. Rules Core derives the effective spell-point caster level from the normalized caster progression, projects the point-pool maximum, maximum slot level, per-slot point costs, and the one-per-long-rest creation limit for 6th-level and higher slots. Pact Magic remains a separate resource system and is not converted by this policy. The spell-point policy is isolated from Pact Magic so a distinct Pact conversion can be added later without changing the standard progression.

When more than one selected class contributes a standard spell-slot table, Rules Core combines caster levels from the source `casterProgression` identities rather than adding class-table slot counts. `full` contributes the full class level, `1/2` contributes half rounded down, `1/3` contributes one third rounded down, and `artificer` contributes half rounded up. The resulting effective caster level selects a row from the retained source slot tables. If available source tables disagree for that effective level, or a progression identity is unsupported, the result remains explicit and conflicted instead of choosing one table silently. Pact Magic is excluded from this calculation and remains a separate resource.

## Character projection maximum HP

Class hit-die size and advancement level are Rules Core inputs to maximum-HP projection. The caller supplies one raw hit-die outcome per class level through `hitPointGains`; that outcome may represent a resolved roll or a fixed value selected under the effective rules. The stable identity is the class concept plus the class level, for example `health.hit-point-gain.class.example.level-3`. Runtime `rolls` may use the same key when the outcome was rolled.

Rules Core validates each supplied value against the source hit die, applies the resolved Constitution modifier for every level, applies the standard minimum gain of 1 hit point per level, and returns the composed `health.maximum-hp` value with per-level provenance. It does not assume a first-level maximum, choose a fixed average, or fill in a missing level. Missing level outcomes remain `missing-character-input`; stale, duplicate, or out-of-range outcomes are explicit conflicts.

Each contributing class/prestige-class Hit Die is also projected as its own `resource.hit-die.<class-concept-key>` resource. The maximum comes from source-defined Hit Die size plus the supplied advancement level, so multiclass Characters retain separate pools by granting source. The current/spent value is read only from Character-owned `currentResources`. Recovery behavior is not hard-coded into the pool; an effective ruleset can attach the relevant recovery procedure.

Death Saves use the same generalized boundary. When an effective rule defines them, success and failure tracks can be projected as resources such as `resource.death-save.successes` and `resource.death-save.failures`, including source-defined maxima and a recovery/reset procedure key. The Character owns the current track values. A normalized procedure can carry structured `set`/adjustment effects for reset behavior, so the Character Sheet does not infer the limit or clear the tracks from UI convention. Rulesets without Death Saves simply do not project those resources.

## Native class and subclass feature progression

The bulk Character projection reads acquisition levels directly from native 5e.tools class-feature and subclass-feature UIDs. Class feature references use `Name|Class|ClassSource|Level|...`; subclass feature references use `Name|Class|ClassSource|Subclass|SubclassSource|Level|...`. Object-wrapped `classFeature` and `subclassFeature` references are treated identically.

Only features whose source-defined acquisition level is at or below the supplied advancement level are projected as resolved Character features. Future features are omitted. A feature entry with no trustworthy acquisition level remains `applicable-unresolved` instead of being assigned to a level by position or display order.

Every projected feature has a stable `featureKey` and occurrence identity, source concept/provenance, granting-source kind, and acquisition level when the source exposes one. This preserves distinctions such as class, subclass, prestige-class, race/species, background, and feat grants even when a Character UI presents them in one Features & Traits list. Two grants with the same display name do not become one occurrence merely because their labels match. Normalized effects remain structured rule effects rather than forcing the Character Sheet to crawl source documents.

## Character projection choices

The bulk Character projection exposes rule-defined selections as `choices` rather than requiring a Character client to reopen source JSON. A choice carries a stable choice key and group key, kind, state, legal options, selected value when supplied, source concept, and provenance. Runtime selections are sent back through the existing `choices` request collection.

`startingProficiencies.skills` and top-level `skillProficiencies` use the same normalized choice path. Fixed source skill proficiencies become Character training directly. A 5e.tools `choose.from` group becomes one choice slot per required selection, and `any: N` is populated from the effective accessible skill competency catalog. Fixed boolean grants and a choice can coexist in one source object. Duplicate selections within one source group and values outside the legal option set are explicit conflicts. When an option resolves to a canonical competency, selecting it feeds the Rules Core competency calculation directly; the Character Sheet does not need to mirror that selection into `TrainingKeys`.

Missing choices do not imply non-proficiency. Competencies that could still be selected remain unresolved until the Character supplies the choice or a complete external training-state set.

## Structured proficiency projection

The bulk Character projection reads 5e.tools `weaponProficiencies`, `armorProficiencies`, and `toolProficiencies` in addition to the human-facing `weapons`, `armor`, and `tools` arrays. Fixed boolean weapon and armor entries become qualifications/capabilities. For weapon `all.fromFilter` expressions, Rules Core evaluates the normalized filter against accessible effective weapon concepts when every filter clause is supported. The current exact grammar supports `type` clauses for simple/martial and melee/ranged weapons plus `property` clauses; pipe-separated clauses are ANDed and semicolon-separated values within one clause are ORed. Native Finesse (`F`) and Light (`L`) item properties are normalized to the source filter names. Matching weapon concepts become ordinary weapon qualifications, so equipped attack projection consumes the result through the same proficiency capability path. Unsupported filter fields remain `applicable-unresolved`, and a supported filter with no accessible matching weapon concept remains `source-unavailable`; Rules Core does not broaden the expression.

Fixed named tool entries resolve against the accessible canonical tool competency catalog and feed Character training directly. `anyTool: N` becomes N ordinary choice slots populated from that catalog. Narrower category quantities such as `anyArtisansTool`, `anyMusicalInstrument`, and `anyGamingSet` are populated only from accessible effective tool concepts whose source document exposes the matching normalized tool category. Native 5e.tools item type identities `AT`, `INS`, and `GS` are recognized, and normalized rules may instead publish `_rulesCore.toolCategory`. A category with no trustworthy available members remains `source-unavailable`; Rules Core does not fabricate tool names. Category placeholders inside `choose.from` are expanded through the same catalog and may coexist with explicitly named tools.

## Language proficiency projection

The bulk Character projection treats effective `language` Rule Concepts as the legal language catalog. Fixed entries in `languageProficiencies` become resolved `qualification.languages.*` capabilities. Choice tokens `any`/`anyLanguage`, `anyStandard`, `anyExotic`, and `anyRare` are populated from accessible effective language concepts; category-scoped choices use the source language `type` or normalized `_rulesCore.languageCategory` metadata.

Fixed languages are registered before category choices regardless of source JSON property order, so a language already granted by the same rule is not offered again. A selected language is likewise removed from later choice groups during the same projection. `choose.from` may mix category tokens and explicitly named languages and uses the same canonical option path. If the effective catalog can not represent a requested category, the choice remains `source-unavailable`; Rules Core does not synthesize a language list.

## Ability state and temporary effects

`ability.<ability>.base` is the Character-owned input score as received by Rules Core. `ability.<ability>.score` and `ability.<ability>.modifier` remain the effective values after rule contributions. Contributions now carry an optional state kind and condition identity. A conditional or active-condition contribution is marked `temporary` rather than being indistinguishable from a permanent ancestry/background/advancement bonus.

When at least one active temporary contribution exists, the projection additionally exposes `ordinary-score`, `ordinary-modifier`, `temporary-adjustment`, `temporary-score`, and `temporary-modifier` identities for that ability. Those values are Rules Core results, not frontend arithmetic. Conditional effects that are not active remain visible as rule effects but do not alter the effective score.

## Ability-score choice projection

The bulk Character projection treats a 5e.tools `ability` array as alternate ability-score sets, not cumulative entries. When more than one set is present, Rules Core exposes an `ability-score-set` choice and projects only the selected set. This prevents mutually exclusive schemes such as “+2/+1” versus “+1/+1/+1” from being added together.

Within the selected set, fixed numeric adjustments are applied directly. `choose.from` supports source-defined `count` and `amount`, while `choose.weighted` creates one distinct ability selection per source weight (for example +2 and +1). Duplicate selections inside the same weighted/uniform group are explicit conflicts. Missing choices mark only the ability scores that the unresolved source choice can affect; unrelated abilities remain resolved.

Ability choices are returned through the same generic `choices` contract used for proficiencies, with stable keys, legal options, selected values, source concept, and provenance. Unknown choice shapes are preserved as `source-unavailable` rather than guessed.

## Starting class and multiclass proficiencies

When exactly one selected base-class advancement has a positive level, Rules Core treats it as the starting class automatically. When more than one base class is present, the Character must identify the starting class with the `advancement.starting-class` runtime choice (or the same key in string facts). The projection does not infer the starting class from request order. The generic `choices` response exposes the legal selected base-class options.

Only the starting class grants its native `proficiency`/`savingThrows` proficiencies and `startingProficiencies`. Other selected base classes instead project `multiclassing.proficienciesGained`, including the normalized skill/tool/weapon/armor choice paths supported by Character projection. Standard proficiency bonus still uses the total supplied base-class advancement levels. If the starting class is unresolved, starting-only and multiclass-only grants are withheld and the projection reports `choice-required` rather than over-granting Character capabilities.


## Effective-only consumer contract

Normal Character consumers receive the universal competency after Rules Core has selected its effective implementation. Consumer catalog responses therefore do not expose competing `profiles`, `facets`, compatibility mechanic keys, or a caller-selectable source revision. Generic mechanic evaluation accepts only mechanic keys present in the effective consumer catalog and ignores any source-profile selector in a request rather than treating it as resolution authority.

The detailed mechanic/profile catalog still exists for Rules Core internals and authorized adjudication. Global Rules Lawyers may inspect it at `GET /api/admin/rules/mechanics`; campaign DMs may inspect the campaign-scoped detailed catalog at `GET /api/campaigns/{campaignId}/admin/rules/mechanics`.

The resolved Character projection includes the effective universal competencies and per-concept `ruleResolutions`. An unresolved rule remains usable but is explicitly marked `unresolved-fallback`; a later Rules Core publication is visible on the next projection request without a Character-owned migration or authoritative rules cache.
