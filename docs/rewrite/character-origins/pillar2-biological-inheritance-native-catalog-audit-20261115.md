# Pillar 2 Biological Origination — Native Characteristic Eligibility Audit

**Date:** 2026-11-15  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Working branch:** `feature/pillar2-power-evaluation-prototype-20261114`  
**Status:** Execution-based catalog audit and bounded implementation increment. **This is not the approval of a final inheritance budget or a blanket license to transmit all Species features.**

## Purpose and policy boundary

The Origin system translates a player's desired backstory into playable mechanics. Both narrative-first and mechanics-first workflows use deliberate **player selection** and the same construction pipeline. Neither requires randomly assigned characteristics. The universal Origin model and procedure-specific native directory/data separation already exist; this audit does not redesign them.

Current first-party biological eligibility requires (1) the **biological** origination method; (2) a native, selectable biological contributor, including the valid selected Ancestry chain of a nonselectable shared Species/family; (3) a core intrinsic slot expressly permitted by `rule.creature-origination.biologicalTransmission` **or** a canonical `origin-trait` with `inheritanceMethods: ["biological"]`; and (4) an explicit player-selected manifestation subject to the common validation and reconciliation pipeline. Donor traits are *offers*, not automatic grants. Custom sources, nonbiological methods, historical ancestry references, and independently acquired traits do not acquire biological permission by default. DM rulings remain a separate verified exception.

**Audited content:** All **nine** base Species, **34** Ancestry records (including nonselectable family nodes), and the canonical trait files under `native/traits/`. Trait declarations are source-neutral; an Ancestry leaf is not a separate implementation of a shared canonical trait. Source-native property chains (for example, Dragonborn damage/area parameters) and cross-trait/resource dependencies were included in the review.

## Executable first-party subset

The existing initial implementation included three intrinsic slots (`creatureType`, `size`, `walkingSpeedFeet`), plus Low-Light Vision and ordinary Dragonborn Breath Weapon. This audit extends the explicitly opted-in canonical traits by **four independently self-contained capabilities**, without modifying their mechanical payloads:

| Canonical origin-trait | Example valid native donor | Reason for initial independent eligibility |
| --- | --- | --- |
| `trait.shared.low-light-vision` | Elf, Orc, playable Dragonborn leaf | Existing verified passive-vision projector; duplicate identical occurrences collapse |
| `trait.dragonborn.breath-weapon` | Red/Bronze Dragonborn leaf | Complete resource/progression preserved; authored `damageType` and **whole** `area` may be composed from actual offered values |
| `trait.shared.amphibious` | Bronze, Black, Green, or Gold Dragonborn leaf | One authored air/water breathing capability; explicitly grants **no** swim speed |
| `trait.elf.trance` | Elf | Complete four-hour resting/sleep-need effect; does not silently grant Magical Sleep immunity |
| `trait.goliath.altitude-acclimation` | Fire Goliath (or another playable Goliath Ancestry) | Explicitly bounded innate high-altitude adaptation; no cold, air or suffocation immunity |
| `trait.orc.relentless-endurance` | Orc | Entire reduced-to-zero hit point trigger and **single** Long Rest use pool remain together |

These four new declarations create valid first-party **inherent grants** in Origin profiles. They do **not** prove that every downstream gameplay subsystem (resting, breathing, altitude, HP and combat) interprets those grants yet. Unlike Low-Light Vision, there are not currently corresponding verified runtime projectors for each newly opted-in capability. The rule stays conservative while consumer integration is built. The proposed mixed-allocation allowance is still pending, so this is a demonstrator rather than permission to call arbitrary high-power unions balanced.

## Nine-Species assessment

This table records **future authoring or integration work**, not a judgment that an unmarked trait is biologically impossible.

| Base Species | Current directly heritable examples | Remaining characteristic work |
| --- | --- | --- |
| Dragonborn | Three physical slots, Low-Light Vision, ordinary Breath Weapon (from a **selectable Ancestry**, not the restricted base Species) | Draconic Resistance depends on leaf damage type; ensure resistance and Warding relationships are consistent, and limit ordinary/Metallic Breath and Gem Flight pools independently |
| Dwarf | Three physical slots | Dwarven Toughness grants HP at **every level**; Resilience overlaps other poison resistance; Stonebound Tremorsense interacts with Rock Gnome's passive Earthbound mode; native `darkvisionFeet` is not yet a projected general intrinsic |
| Elf | Physical slots, Low-Light Vision, Trance | Keen Senses adds composite skill bonuses; Fey Ancestry and Magical Sleep need clear effect/overlap semantics; Drow Darkvision/Sunlight Sensitivity and selected spell courses need coupling and source-bound choices |
| Gnome | Physical slots | Gnomish Cunning has scoped saving-throw advantage; Forest innate spell course needs progression/ability integration; Rock Earthbound Tremorsense conflicts with Dwarf Stonebound in the selected fixture |
| Goliath | Physical slots, Altitude Acclimation (via a selectable Ancestry) | Powerful Build overlaps Orc carrying capacity; Large Form is temporary and must not become a permanent size slot; Mountain Leap needs ordinary movement/jumping integration; Giant Ancestry Aptitude governs many later abilities; shared charge pools and advanced features need closure |
| Halfling | Physical slots | Luck, Brave, Nimbleness and Naturally Stealthy have interaction/conditional rules; Keen Hearing overlaps Elf Listen bonus; Lightfoot Natural Agility has additional composite skills |
| Human | Physical slots, including the authored Small/Medium choice | Skillful and Versatile require trusted open-catalog Skill/Origin Feat selection; Adaptable Learning requires full per-level rank progression and skill-cap integration, not just a level-one bonus |
| Orc | Physical slots, Low-Light Vision, Relentless Endurance | Adrenaline Rush uses a separate short/long rest pool; Powerful Build duplicates some but not all Goliath effects; Savage Attacks depends on critical-hit rules; Menacing Presence is conditional on physical intimidation |
| Tiefling | Physical slots via a selectable Fiendish Ancestry | Native Tiefling Low-Light Vision is an independent canonical definition with equivalent behavior and needs cross-definition reconciliation; Otherworldly Presence and Abyssal/Chthonic/Infernal innate spell courses depend on Ancestry spellcasting choices; elemental resistance can overlap other sourced resistance |

## Ancestry and family-node findings

- **Dragonborn:** Chromatic, Metallic and Gem family nodes are **not** standalone donors. Selected Chromatic and Metallic leaves supply parent-chain offers; Red/Bronze parameter recombination is already verified. Black/Green/Bronze/Gold share the Amphibious definition. Other leaf adaptations (Climber, Icewalker, Coldwater Swimmer, Sand Burrower, Soft Landing and Displaced Voice) require movement/effect integration and general overlap review before opt-in. Chromatic Warding must retain its resistance relation; Metallic Breath is a *different* one-use pool from ordinary Breath Weapon. Gem leaves are currently nonselectable and are not silently enabled; their Psionic Mind/Flight definitions remain available for later first-party gameplay review. Additional movement properties such as `climbingSpeedFeet`, `swimmingSpeedFeet`, and `burrowingSpeedFeet` are not fully covered by the three current intrinsic foundation slots.
- **Dwarf:** Hill Stability is currently a descriptive trait without normalized executable fields. It needs a mechanical definition before automatic mixed-grant authorization can be treated as an executable effect.
- **Elf:** Drow adds a 120-foot Darkvision source and Sunlight Sensitivity alongside an innate spell progression. High and Wood add three-stage spells bound to a selected spellcasting ability; Wood's 35-foot walking speed must reconcile with its shared 30-foot Elf root. Spell courses must not be casually merged stage by stage.
- **Gnome:** Forest's innate illusion and animal communication are not yet source-complete spell courses. Rock's 15-foot passive Earthbound Tremorsense is *not* automatically combined with Dwarf's activated 60-foot Stonebound Tremorsense. The owner-selected Dwarf/Rock test outcome is Stonebound-only.
- **Goliath:** Six playable Ancestries inherit a nonselectable Goliath base. Cloud, Stone and Storm have explicitly shared parent/use-pool relationships between signature activations and Strikes; Frost/Hill include alternate Strike modes; Fire has tiered attacks and innate resistance. Higher-level unlocks are committed when a trait is inherited, not free future choices. The base's Giant Ancestry Aptitude is a source-bound Strength/Constitution choice that advanced features must resolve.
- **Halfling:** Lightfoot adds Natural Agility with three Skill bonuses. This needs the same conflict/stacking treatment as other source-derived Skill bonuses.
- **Tiefling:** Three playable Fiendish Ancestries inherit a nonselectable Tiefling base. Resistance types are separate from the three-stage innate spell courses. Preserve their individual spellcasting-ability selections, free-cast limits and full future progression. None of these abilities is currently automatically biologically transferable.

## Executable source-neutral shared-resource dependency increment — 2026-11-15

The common Origin engine now invokes `NativeOriginResourceDependencyBinder` **after** selected grants have been reconciled and authored intrinsic choices bound. It recognizes `sharedUsesFromTrait` on a canonical native `origin-trait`, not guessed capability names or Species-specific exception code.

- Three existing Goliath Ancestry relationships are implemented without modifying their underlying mechanical definitions: Cloud Strike **spends Cloud's Jaunt**, Stone Strike **spends Stone's Endurance**, and Storm Strike **spends Storm's Thunder**.
- A dependent Strike must have exactly one **actually selected** resource-owner trait from the **same contributing lineage** and exact native source. A different donor's equivalent ability is not permission to borrow its charges.
- The binder verifies that the effective trait preserves the original `sharedUsesFromTrait` and `extraUsesGranted: false` declarations and that the target owner retains a canonical `uses` definition. A missing, ambiguous, altered or unverified owner suppresses the Strike and produces an incomplete/unresolved result. It **never** synthesizes a new resource or implicitly transmits an unselected owner.
- A valid dependent has `ResourceOwners: { "uses": "<actual-owner-grant-id>" }` in its `NativeOriginEffectiveGrant`. This is a stable save/load relationship for downstream consumers; it is **not** an additional pool. `NativeOriginContractValidator.ReviewProfile` validates these persisted references and the canonical owner rules, rejecting fabricated owner IDs, missing owner links, and altered dependent-use declarations after serialization.
- `Cloudy Escape` has `independentUsesFrom: "clouds-jaunt"` and its *own* `uses` object, so it remains an **independent** Proficiency Bonus/Long Rest pool. The resource binder does not mistake it for a shared-use dependent.
- Regression tests exercise all three shared owner/Strike pairs with present and absent owner, two native contributors offering the same Ancestry but separate selections, forged saved owner IDs, the explicit independent Cloudy Escape pool, and preservation of evidence across JSON serialization.

**Scope boundary:** These changes validate how an **already authorized or DM-authorized selected** trait spends another authorized trait's pool. They **do not** opt Goliath's currently unmarked Strike abilities into automatic biological inheritance; do not implement a final resource-expenditure state machine, giant-ancestry ability modifier binding, new allocation policy or the complete set of generic characteristic dependencies. Those remain separate steps. The native Goliath definitions were not modified.

## Source-bound Giant Ancestry Aptitude increment — 2026-11-15

The existing canonical `trait.goliath.giant-ancestry-aptitude` already authors a **per-character Strength or Constitution decision**. Its `abilityChoice.from` options now produce a first-party, creation-time finite choice requirement on the *selected aptitude trait*; this does not change its mechanical definition, allow random selection, or make the aptitude automatically heritable.

The universal `NativeOriginDependentBinder` now executes a second source-neutral binding operation: `originBindings.<parameter>.lookup = "manifested-trait-choice"`, identifying a canonical owning `origin-trait`, its authored finite choice requirement and its contributor scope. The derived `giantAncestryAbility` parameter is stored on the dependent **effective grant**, and `ParameterSources` identifies the matching owner's candidate. This is **not** an additional ability score, proficiency, feat, or resource pool. It is a durable choice required to evaluate the dependent feature's existing symbolic attack and saving-throw modifier.

The six Goliath Ancestry trait files now author that binding for all **nine** capabilities containing `chosen-giant-ancestry-ability-modifier`: Cloud Strike; Fire's Searing Ignition; Frost's Chill and Frigid Retaliation; Hill's Tumble; Stone Strike and Stone Throw; Storm Strike and Maelstrom Aura. This includes modifiers nested inside alternate Strike modes; the whole canonical feature and progression remain intact.

**Validation/authorization boundaries:**

- Both **Strength and Constitution** are supported, deliberately selected, and checked against the canonical native menu. Missing or invalid choices suppress the affected dependent. An aptitude that was not explicitly selected never appears automatically just because an ancestry mentions its effects.
- A selected aptitude from *another contributor* cannot silently supply the dependent trait's modifier. One consolidated aptitude grant may carry evidence from more than one contributor, but the dependent field must record evidence from the **matching contributor**, not a lexicographically arbitrary owner. The multicontributor regression covers matching choices that consolidate into one grant, together with conflicting choices that must remain unresolved instead of selecting an arbitrary winner.
- A stored profile's bound parameters and `ParameterSources` are rederived and compared against its original proposal to reject forged or stale modifier/provenance claims. An explicit direct Stone Goliath creation is also tested to ensure a conventional authored Ancestry remains a valid alternative to mixed construction.
- The previously implemented Goliath shared-use pool bindings still apply *independently*. Cloud Strike, Stone Strike and Storm Strike require their actual selected pool owners in addition to the aptitude.
- **No default biological transmission** is newly granted for Giant Ancestry Aptitude or the nine dependent abilities. Tests use direct native creation or exact verified DM eligibility, and no automatic allocation budget is implied.

**Further work:** The `giantAncestryAbility` string is a selected *ability name*, not a calculated ability modifier. Character Sheet/combat consumers must resolve the character's current corresponding modifier and retain the original level scaling. Mixed descendants, cross-lineage aptitude composition and future recombination rules require a separate trusted current-profile and provenance policy. Other origination procedures remain unaffected.

## Native typed-sense overlap and selected Dwarf/Rock disposition — 2026-11-15

The common `NativeOriginReconciler` now detects **selected, distinct canonical `origin-trait` definitions that author the same `trait.sense` type**. This is evidence of possible overlap, **not** proof that their full mechanics are equivalent, stack, or must be collapsed. It applies after contributor eligibility and explicit player manifestation, so unselected parental senses are not conflicts. It does not confuse general Low-Light Vision with Tremorsense, or infer sense identity from descriptive prose.

- If two different selected definitions both author `sense: "tremorsense"`, the engine requires a trusted disposition. An unresolved pair is suppressed and reported as `same-sense-reconciliation-required` rather than yielding both complete benefits.
- The recorded **Dwarf Stonebound-only** choice is represented as an explicit, scoped `RetainFirst` disposition: 60-foot stone-contact, Bonus Action, 10-minute duration, Proficiency Bonus uses per Long Rest. The Gnome's 15-foot continuous Earthbound mode is **not** also granted. That selected example does **not** establish a global rule that the longer-range sense wins.
- A separately authorized `RetainSecond` can preserve **only** passive Earthbound, with its earth/stone shared-contact requirement; neither activation nor Stonebound charges are imported.
- A verified, scoped `RetainBoth` is supported for a table that intentionally permits both modes. It maintains *two separate unchanged mechanics*, not an inferred 60-foot passive hybrid. Every pair of simultaneously retained distinct sense definitions must have authorized coexistence; caller-supplied relationship claims and unverified decisions grant no permission.
- The implementation does **not** automatically transmit either Tremorsense trait. They remain unreviewed for first-party biological eligibility until their own inheritance, overlap, activation and allocation decisions are ready; the execution tests supply individually scoped verified DM eligibility and relationship decisions.
- Regression tests cover unresolved overlap, each retained-only mode, authorized coexistence, denied unauthenticated coexistence, unrelated vision plus Tremorsense, full Stonebound resource/activation constraints, and JSON round-trip of the selected effective grant.

**Pending:** Non-trait senses and intrinsic Darkvision choices still need projection, upgrade and condition rules; the conventional preset should remain unaffected. Typed resistance overlap is now conservatively detected as described below, but its eventual general stacking/percentage policy is not approved. This increment is a conservative reconciliation gate, **not** a universal sense upgrade, overlap cap, balancing result or random inheritance system.

## Native durable damage-resistance overlap — 2026-11-15

The same reconciliation stage now detects **selected, distinct canonical origin traits that provide unconditional resistance to the same damage type**. The evidence is read from the native typed `damageResistance` payload, rather than a description or broad keyword.

- Native percentage records such as Infernal Fire 50%, Goliath Born of Flame Fire 50%, Abyssal Poison 50%, Chthonic Necrotic 50% and Frost resistance are compared by **damage type**, not by an arbitrary constant score.
- The Dwarf's legacy `damageResistance: { "poison": 50 }` shape is recognized alongside the percentage shape. Dwarven Resilience remains a **complete trait** containing both poison resistance and advantage against poison-effect saving throws. A `RetainFirst` decision does not silently split its benefits.
- A Dragonborn's `damageTypeFrom: "selected-dragonborn-leaf"` is resolved using the **actual, already authorized manifested `damageType` parameter** from its authored leaf binding; a Red Dragonborn Fire resistance and Infernal Fire resistance therefore overlap even though the original JSON schemas are not identical.
- Two selected, distinct static resistance traits for the same type require an explicit reconciliation. Unresolved candidates are suppressed and report `same-damage-resistance-reconciliation-required`; the engine does **not** add percentages, treat resistance as a new use pool, or select the strongest ancestor.
- Different damage types (such as Fire and Necrotic) remain independent. A temporary, event-scoped reaction defense such as Cloudy Escape is **excluded** from permanent resistance collisions. Authored activation, duration and scope matter.
- A scoped and verified `RetainFirst`, `RetainSecond`, or `RetainBoth` decision uses the existing universal relationship engine. A table can expressly permit coexistence without any invented percentage-stacking formula, but downstream damage calculations must use their own approved resistance-combination rules.
- Regression tests use real Dwarf, Abyssal/Infernal/Chthonic Tiefling, Fire Goliath, Red Dragonborn and Cloud Goliath definitions. Eligibility of the individual features remains separately DM-authorized where no first-party native biological transmission rule has been approved.

**Boundary:** This is a typed-overlap *recognition and reconciliation gate*, not a general defense merger, revised damage formula, new inherited resistance eligibility, forced adoption of drawbacks, or a full character-allocation safeguard. Other resource/defense interactions and full consumer integration remain outstanding.

## Drow source-linked Darkvision and Sunlight Sensitivity — 2026-11-15

The first-party Drow Ancestry source now declares a **source-local, one-way linked-selection requirement** under `originLinkedSelections`. The exact `darkvisionFeet` intrinsic (120 feet) requires the *same contributor's* `trait.elf.drow.sunlight-sensitivity` when that Drow Darkvision characteristic is deliberately manifested. The linked trait retains its existing `condition.dazzled` trigger on **direct unprotected sunlight** and its written end conditions (ending exposure or effective protection). The mere presence of ordinary light or magical light is not equivalent to the specified trigger. Sunlight Sensitivity may be selected independently without implicitly granting Darkvision.

This is a **provisional native gameplay default on the draft implementation branch**, not a universal rule that all Darkvision includes Sunlight Sensitivity, a general inherited-gene pairing law, or a conclusion that alternative narrative combinations are prohibited. A **verified scoped DM/table decision** can explicitly waive this Drow link while leaving both native characteristic definitions unchanged. The player continues to select and authorize each individual characteristic.

### Generic enforcement and exact source attribution

- `NativeOriginSourceDeconstructor` now offers authored `darkvisionFeet` as a typed intrinsic. This does **not** add it to the first-party biological transmission allowlist; mixed Drow Darkvision and Sunlight Sensitivity remain table-authorized until their eligibility is reviewed.
- `NativeOriginNativeLinkedSelectionEvaluator` expands native source metadata into an actual `Requires` constraint, pointing to one specific Darkvision offer and its corresponding same-contributor Sunlight Sensitivity offer. It does not construct a free drawback, choose traits automatically or accept another contributor's superficially identical source.
- The universal `NativeOriginConstraintEvaluator` enforces selection-time requirements before reconciliation. A **second source-neutral closure check runs after reconciliation and resource binding**, because a separately authorized relationship could otherwise remove the required drawback while leaving Darkvision active. Either missing selection or lost effective counterpart suppresses the dependent Darkvision grant and leaves construction incomplete. A verified `constraint` table decision with `allow: true` overrides the link consistently at both stages.
- Both native-link participant selectors are derived from the exact catalog. Proposals that try to replace the native link's source or target fail closed. Saving and replaying a legitimate native link is idempotent and does not require untrusted caller assertions.
- `NativeOriginDarkvisionProjector` provides a **read-only verified native range** for a completed direct Drow Origin or an authorized mixed Origin. It checks exact native source range and contributor/grant evidence; it does not infer range increases, magical-darkness penetration, Low-Light Vision substitution, or general sensory stacking.

### Acceptance coverage and remaining scope

Real-catalog tests cover default Drow linkage, missing linked selection, same/different contributors, authorized waiver and unauthorized replay, fabricated constraints, serialization/reconstruction, full conventional Direct Drow construction, verified direct/mixed Darkvision projections, **post-reconciliation disappearance of the drawback**, and an explicit waiver for that later change.

The linkage establishes *which mechanics the initial biological Origin actually owns*. Runtime exposure detection, protective eyewear rules, Dazzled condition application, combined passive vision, all other ancestry-specific trait couplings, previously mixed descendants and a provisional allocation budget remain separate work. Do not assume that exposing the range in the projector makes all Darkvision or sunlight mechanics operational in Character Sheet/combat.

## Level-gated native Darkvision extension — 2026-11-15

The Drow-only intrinsic Darkvision projector has been extended to read **separately selected native Darkvision traits** with explicit level-gated upgrade semantics. The initial implementation uses Stone Goliath's canonical **Cavernous Sight**, which grants a 60-foot Darkvision sense at character level 4 and, where another authorized Darkvision source already exists, increases that range by 60 feet.

- `NativeOriginDarkvisionProjector.Project(..., characterLevel)` and `ProjectApprovedMixed(..., characterLevel)` now distinguish inherent fixed native ranges from separately manifested, canonically defined sense improvements. Direct Stone Goliath receives 60 feet only at level 4 or higher. A mixed Drow/Stone Origin with individually verified 120-foot Drow Darkvision, its linked Sunlight Sensitivity and Stone Cavernous Sight projects 120 feet before level 4, then **180 feet at level 4**.
- The projector checks **both** the original native trait definition and the actual effective grant, including authored `sense`, `rangeFeet`, `addToExistingDarkvisionFeet`, `requiredCharacterLevel`, `stacking: "once-per-feature"` and the fully authorized original candidate. It does not grant unselected Cavernous Sight or alter its default biological eligibility; mixed offspring still need separate trait authorization.
- If the character's level is **unknown** and a level-gated Darkvision feature is present, the projector reports `unresolved` instead of silently applying the upgrade or treating it as unavailable. Below the unlock level, its other verified Darkvision sources remain usable. The standalone Stone feature is absent before level 4. Invalid level and unverified sources fail closed.
- `GrantedDarkvision` keeps its original source/grant identity, adds an explicit `BaseRangeFeet`, and records one `NativeDarkvisionExtension` with separate grant, native source, occurrence and level provenance. The projected combined range is **not** misrepresented as a single unmodified original trait.
- Different simultaneous Darkvision-extension definitions and speculative multipliers are **not** automatically stacked. Source-specific exclusions, other senses, Low-Light Vision, Sunlight Sensitivity's Dazzled trigger, magical-darkness interpretation, and actual creature-level synchronization remain distinct runtime work.
- Full-catalog tests cover direct Stone Goliath and its unchanged aptitude requirement, pre/post-unlock range, a verified mixed Drow/Stone combination, Drow-only no-extension behavior, rejection of unreviewed Cavernous inheritance, separate contributor evidence and save/load preservation.

**Scope boundary:** The projection is still a **read-only, level-qualified preview** of what an authorized character owns. It is not a new eligibility policy, an automatic mix-of-senses rule, a permanent size/form transformation, a finalized combat/visibility implementation, or a quantitative power result.

## Provisional allocation preview and whole-progression commitment — 2026-11-15

The native Origin engine now has a **separate, read-only allocation assessment** in `NativeOriginAllocationPreview`. It accepts the *actual effective grant set* from a constructed Origin and an **explicit hypothetical** `NativeOriginAllocationPolicy`. No allowance, point weights or characteristic cost schedule has been added to the first-party native catalog. The preview does not participate in grant authorization; an `Authorized` construction result must **not** be described as having passed biological allocation admission until a real method-specific policy has been approved and wired into the trusted engine.

- Each complete effective intrinsic or canonical trait is mapped to a source-neutral component key. Duplicate parent offers already consolidated by reconciliation cost **once**; different effective grants cost separately. The number of contributors is not an allowance multiplier.
- A versioned candidate price may group explicitly linked components, such as Drow Darkvision and Sunlight Sensitivity, into **one bundle**. Grouped members must each be effective and share at least one actual contributor. Partially present, ambiguously duplicated or unrelated-source bundles stay unresolved; negatives are not automatically transferable credits.
- Each whole canonical trait consumes its hypothetical full-progression cost at construction time. The ledger records the authored earliest unlock level (for example, Stone Goliath's level-4 Cavernous Sight), but does **not** defer its cost until that level. The experimental allocation is deliberately **not** a numerical measure of combat or utility power.
- The preview reports `not-configured`, `invalid-policy`, `unresolved`, `within-allowance`, `over-allowance` and `not-applicable`; missing costs, undefined native mechanics, incomplete Origins and malformed pricing do not silently become zero.
- Regression tests cover actual first-party Elf characteristics, linked Drow adaptation, future Cavernous Sight, direct preset exemption, three same-source donors, unpriced characteristics, over-budget scenarios, malformed policies and forged unknown definitions.

See [the detailed provisional allocation preview contract](pillar2-provisional-allocation-preview-20261115.md). **Next decision gate:** approve an editable native biological allowance and priced complete-characteristic bundles, include explicit alternatives/DM dispositions and rule revision on completed Origin records, then integrate admission with the construction engine. This preview alone does **not** satisfy that gate.

## Required next gates before general first-party mixed construction

1. **Native coverage:** Continue auditing and authoring opt-in only where inherent mechanics, acquisition scope, full future progression and associated restrictions are known. Do not mark learned features biological merely because they appear on a source Species. Unmarked traits remain eligible for a verified DM decision but not default inheritance.
2. **Trait dependencies and equivalence:** Add source-neutral authored links for required traits (for example, shared-resource Goliath Strikes), conditional drawback coupling, upgrade/substitution, equivalent sense mechanics and size/movement conflicts. The engine must fail closed if the complete linked configuration is not satisfied.
3. **Full projection:** Consume chosen biological grants through actual combat, hit points, skill/sense, spell, resources and character-sheet pipelines. Persist per-parameter provenance, choice evidence and the applicable rule revision.
4. **Mixed descendant trust:** Authenticate a previously manifested parent's *current effective inherent* profile; prevent unexpressed historical family traits and acquired Upbringing, Class, supernatural or temporary-form grants from becoming biological offers.
5. **Allocation:** Introduce one editable **provisional** method-scoped safeguard against unrestricted independent-benefit unions. Do not adopt an unvalidated seven/eight-point cost model, fixed two-feature quota, arbitrary primary-Species rule, or precise combat-power target.
6. **End-to-end acceptance:** Test ordinary selectable Species, mixed combinations, multiple contributors and multigenerational constructions at actual future unlocks. Conventional presets remain valid direct selections.

**No biological RNG, complete combat model, Construct procedure implementation, universal numerical power equality, or new directory architecture is required by this milestone.**


## Authenticated mixed-descendant biological transmission — 2026-11-15

The construction engine now accepts an optional `INativeOriginManifestedContributorAuthority`. An authenticated application service can resolve a previously constructed parent's **current authoritative underlying inherent configuration** and revision under a trusted scope. A C# `LocalSelfOverride` test fixture supplies this authority in-process, avoiding Character Sheet or site implementation during testing. No user-provided nested profile becomes native inheritance authority on its own. [Implementation progress and threat boundary](pillar2-authenticated-mixed-descendant-transmission-20261115.md).

- Each verified parent supplies only its **manifested underlying** grants. Historical Species alternatives, unselected traits, acquired independent grants, and effective temporary forms are excluded. The biological evaluator still requires an available biological method for the verified parent's effective underlying creature type and the native first-party trait opt-in for the particular grant; no unrestricted ancestry cache is created.
- The descendant selects explicit offers and reuses the normal reconciliation, constraint, binding, resource and validation stages. Configured parameters, resolved choices and full native trait definitions remain intact. Parent resource-owner IDs are historical only: selected descendant resources are rebound to their own effective grants.
- The current grant has one local source-offer record per contributor. Its optional `InheritedFrom` array records the verified parent's profile ID and revision, original grant ID, prior source candidates, field sources and resource-owner links. Reconciliation retains every immediate contributing parent through consolidation or authorized replacement without granting multiple resources. Earlier generations remain references to verified stored profile revisions, not automatically selectable sources.
- Serialized lineage validation rejects empty, malformed, contradictory or duplicated immediate source records. It validates internal shape and attribution but is **not** an application-ownership check or proof that a caller-supplied parent snapshot is authoritative.
- Real-catalog C# tests cover self-override trust, stale revisions/wrong scope/profile, temporary form exclusion, configured Red/Bronze Breath Weapon inheritance, two verified mixed parents with a consolidated trait, further-generation transmission, nonbiological underlying types, lineage tampering, and JSON preservation.

**Release boundary:** This is a bounded source-neutral mixed-descendant transmission increment, not final allocation approval, universal inheritance authoring, site integration, or complete downstream execution. No merge, deployment, or policy approval follows from passing this test suite.


### Serialized mixed projection and inherited resource binding — 2026-11-15

- A verified completed mixed descendant's physical foundation and Low-Light Vision can be projected **after a JSON profile round trip**, not just while the constructor retains reference-identical `Current` and `Underlying` objects. `NativeOriginApprovedMixedIntrinsicProjector` now uses the same normalized configuration equivalence as the source contract validator. This does not authorize independently loaded client profiles; construction status and source evidence remain required.
- `NativeCharacterSheetOriginBaselineAdapter.ApplyWalkSpeed` can consume the verified descendant's underlying walking-speed grant with an injected test authority. This is narrowly limited to one scalar baseline capability and leaves the Character Sheet site and other mechanics unchanged.
- A native Stone Goliath parent test verifies that its chosen Constitution aptitude, one shared Stone's Endurance pool and Stone Strike are retained as a **complete configured dependency** through a descendant's explicitly DM-authorized selection. The descendant's Strike references the **descendant's** pool grant ID, while the parent's old resource-owner ID remains historical provenance only. Goliath Strike and Aptitude remain unreviewed for first-party biological transmission.
- All existing C# and Python tests, native structure validation, and Docker/HTTP workflow checks passed at implementation heads `5e8e350138d2f40dec8fac8f3800f338373a79ae` and `9e59f0dac72cf9eae55bccb5c34ec17fce099e91`.

No numerical allocation admission, universal inherited spellcasting, transient-form runtime, production parent resolver or website integration was introduced.


## Dwarven Toughness HP projection and canonical passive-rule overlap — 2026-11-15

The next independent characteristic increment adds **read-only native maximum-HP progression** and a **source-neutral passive rule overlap guard**. It does not create a new biological cost or inheritance policy.

- `NativeOriginHitPointProgressionProjector` reads an already-selected and authorized canonical `origin-trait` with `hitPointsPerCharacterLevel`. For Dwarf Toughness, the authored value is `1` extra maximum HP per **total character level**. A single effective grant applies once even if duplicate donor manifestations consolidate; two distinct HP progressions remain unresolved rather than silently stacked. Forged or altered trait payloads and unresolved character levels fail closed.
- `Combine` uses the actual native character projector's resolved `advancement.total-level` and `hit-points.maximum`, adding the Origin bonus **once** without rewriting Hit Die gains, Constitution math, Class progression, current HP or authoritative saved state. It is a preview, not a new server grant. Direct Dwarf projection works at levels 1, 3, 5 and 17; mixed Dwarf testing requires scoped DM eligibility since Dwarven Toughness is **not yet opted into** first-party biological transmission.
- The conventional `NativeOriginIntrinsicProjector` now compares normalized `Underlying` and `Current` configurations instead of requiring in-memory reference identity. A valid JSON round-trip no longer erases otherwise verified direct physical foundation projection.
- Distinct selected canonical traits that reference the **same native passive rule** and carry no other mechanical fields must be explicitly reconciled, even when authored with different trait IDs. Elf `trait.shared.low-light-vision` and Tiefling `trait.tiefling.low-light-vision` both reference `rule.low-light-vision`. The common reconciler reports `same-passive-native-rule-reconciliation-required` instead of silently granting two effects. A verified scoped RetainFirst/RetainSecond keeps one **complete** source definition. RetainBoth requires explicit authority and still cannot be projected as one supported passive-vision effect. No automatic equivalence winner or canonical-ID merger was introduced.
- The Tiefling base Species remains nonselectable/restricted; its existing family offer is **Restricted**, not a default biologically heritable trait. Native source restrictions and real candidate eligibility remain independent of the overlap detector.
- Real native catalog regression tests verify expected Dwarf HP calculations, incomplete native HP history, selection/deduplication, serialized profiles, forgery, Elf/Tiefling unresolved overlap, two scoped selected outcomes, single-source behavior and rejection of fabricated native rule disposition.

**No new first-party biological traits were enabled, and no site, Class combat, hit-dice policy, current-HP mutation, arbitrary capacity or numerical trait-price schedule was changed.** See [increment details](pillar2-hp-progression-and-passive-rule-overlap-20261115.md).


## Human Adaptable Learning entitlement projection — 2026-11-15

The canonical Human `trait.human.adaptable-learning` now has a **read-only** source-neutral interpretation of its complete earned entitlement schedule: four extra Skill ranks at character level 1, plus one at each later level (`L+3` ranks through level `L`). `NativeOriginSkillRankEntitlementProjector` verifies an already authorized selected native trait, the actual native definition, source/provenance, compatible persisted baseline and a resolved character level. It rejects forged or stripped progressions and duplicate independently counted same-trait donors. It makes **no** statement that any ranks have been allocated within normal limits: no approved rank ledger/caps or automatic Skill changes exist.

Real-catalog tests cover conventional Human (only with an **in-process test-only** synthetic Origin Feat eligibility override), scoped DM-authorized mixed Elf/Human, two Human donors consolidating one schedule, ordinary Elf absence, serialization, invalid levels and altered payloads. The first-party Origin Feat catalog is **not** complete, so the unoverridden conventional Human remains incomplete. No additional trait is first-party opted in for biological inheritance, and Skillful/Versatile benefits remain separate from rank entitlements. See [details](pillar2-human-skill-rank-entitlement-projection-20261115.md).
