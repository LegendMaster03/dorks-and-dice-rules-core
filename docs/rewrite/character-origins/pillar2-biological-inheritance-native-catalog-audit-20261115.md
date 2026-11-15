# Pillar 2 Biological Origination — Native Characteristic Eligibility Audit

**Date:** 2026-11-15  
**Repository:** \`LegendMaster03/dorks-and-dice-rules-core\`  
**Working branch:** \`feature/pillar2-power-evaluation-prototype-20261114\`  
**Status:** Execution-based catalog audit and bounded implementation increment. **This is not the approval of a final inheritance budget or a blanket license to transmit all Species features.**

## Purpose and policy boundary

The Origin system translates a player's desired backstory into playable mechanics. Both narrative-first and mechanics-first workflows use deliberate **player selection** and the same construction pipeline. Neither requires randomly assigned characteristics. The universal Origin model and procedure-specific native directory/data separation already exist; this audit does not redesign them.

Current first-party biological eligibility requires (1) the **biological** origination method; (2) a native, selectable biological contributor, including the valid selected Ancestry chain of a nonselectable shared Species/family; (3) a core intrinsic slot expressly permitted by \`rule.creature-origination.biologicalTransmission\` **or** a canonical \`origin-trait\` with \`inheritanceMethods: ["biological"]\`; and (4) an explicit player-selected manifestation subject to the common validation and reconciliation pipeline. Donor traits are *offers*, not automatic grants. Custom sources, nonbiological methods, historical ancestry references, and independently acquired traits do not acquire biological permission by default. DM rulings remain a separate verified exception.

**Audited content:** All **nine** base Species, **34** Ancestry records (including nonselectable family nodes), and the canonical trait files under \`native/traits/\`. Trait declarations are source-neutral; an Ancestry leaf is not a separate implementation of a shared canonical trait. Source-native property chains (for example, Dragonborn damage/area parameters) and cross-trait/resource dependencies were included in the review.

## Executable first-party subset

The existing initial implementation included three intrinsic slots (\`creatureType\`, \`size\`, \`walkingSpeedFeet\`), plus Low-Light Vision and ordinary Dragonborn Breath Weapon. This audit extends the explicitly opted-in canonical traits by **four independently self-contained capabilities**, without modifying their mechanical payloads:

| Canonical origin-trait | Example valid native donor | Reason for initial independent eligibility |
| --- | --- | --- |
| \`trait.shared.low-light-vision\` | Elf, Orc, playable Dragonborn leaf | Existing verified passive-vision projector; duplicate identical occurrences collapse |
| \`trait.dragonborn.breath-weapon\` | Red/Bronze Dragonborn leaf | Complete resource/progression preserved; authored \`damageType\` and **whole** \`area\` may be composed from actual offered values |
| \`trait.shared.amphibious\` | Bronze, Black, Green, or Gold Dragonborn leaf | One authored air/water breathing capability; explicitly grants **no** swim speed |
| \`trait.elf.trance\` | Elf | Complete four-hour resting/sleep-need effect; does not silently grant Magical Sleep immunity |
| \`trait.goliath.altitude-acclimation\` | Fire Goliath (or another playable Goliath Ancestry) | Explicitly bounded innate high-altitude adaptation; no cold, air or suffocation immunity |
| \`trait.orc.relentless-endurance\` | Orc | Entire reduced-to-zero hit point trigger and **single** Long Rest use pool remain together |

These four new declarations create valid first-party **inherent grants** in Origin profiles. They do **not** prove that every downstream gameplay subsystem (resting, breathing, altitude, HP and combat) interprets those grants yet. Unlike Low-Light Vision, there are not currently corresponding verified runtime projectors for each newly opted-in capability. The rule stays conservative while consumer integration is built. The proposed mixed-allocation allowance is still pending, so this is a demonstrator rather than permission to call arbitrary high-power unions balanced.

## Nine-Species assessment

This table records **future authoring or integration work**, not a judgment that an unmarked trait is biologically impossible.

| Base Species | Current directly heritable examples | Remaining characteristic work |
| --- | --- | --- |
| Dragonborn | Three physical slots, Low-Light Vision, ordinary Breath Weapon (from a **selectable Ancestry**, not the restricted base Species) | Draconic Resistance depends on leaf damage type; ensure resistance and Warding relationships are consistent, and limit ordinary/Metallic Breath and Gem Flight pools independently |
| Dwarf | Three physical slots | Dwarven Toughness grants HP at **every level**; Resilience overlaps other poison resistance; Stonebound Tremorsense interacts with Rock Gnome's passive Earthbound mode; native \`darkvisionFeet\` is not yet a projected general intrinsic |
| Elf | Physical slots, Low-Light Vision, Trance | Keen Senses adds composite skill bonuses; Fey Ancestry and Magical Sleep need clear effect/overlap semantics; Drow Darkvision/Sunlight Sensitivity and selected spell courses need coupling and source-bound choices |
| Gnome | Physical slots | Gnomish Cunning has scoped saving-throw advantage; Forest innate spell course needs progression/ability integration; Rock Earthbound Tremorsense conflicts with Dwarf Stonebound in the selected fixture |
| Goliath | Physical slots, Altitude Acclimation (via a selectable Ancestry) | Powerful Build overlaps Orc carrying capacity; Large Form is temporary and must not become a permanent size slot; Mountain Leap needs ordinary movement/jumping integration; Giant Ancestry Aptitude governs many later abilities; shared charge pools and advanced features need closure |
| Halfling | Physical slots | Luck, Brave, Nimbleness and Naturally Stealthy have interaction/conditional rules; Keen Hearing overlaps Elf Listen bonus; Lightfoot Natural Agility has additional composite skills |
| Human | Physical slots, including the authored Small/Medium choice | Skillful and Versatile require trusted open-catalog Skill/Origin Feat selection; Adaptable Learning requires full per-level rank progression and skill-cap integration, not just a level-one bonus |
| Orc | Physical slots, Low-Light Vision, Relentless Endurance | Adrenaline Rush uses a separate short/long rest pool; Powerful Build duplicates some but not all Goliath effects; Savage Attacks depends on critical-hit rules; Menacing Presence is conditional on physical intimidation |
| Tiefling | Physical slots via a selectable Fiendish Ancestry | Native Tiefling Low-Light Vision is an independent canonical definition with equivalent behavior and needs cross-definition reconciliation; Otherworldly Presence and Abyssal/Chthonic/Infernal innate spell courses depend on Ancestry spellcasting choices; elemental resistance can overlap other sourced resistance |

## Ancestry and family-node findings

- **Dragonborn:** Chromatic, Metallic and Gem family nodes are **not** standalone donors. Selected Chromatic and Metallic leaves supply parent-chain offers; Red/Bronze parameter recombination is already verified. Black/Green/Bronze/Gold share the Amphibious definition. Other leaf adaptations (Climber, Icewalker, Coldwater Swimmer, Sand Burrower, Soft Landing and Displaced Voice) require movement/effect integration and general overlap review before opt-in. Chromatic Warding must retain its resistance relation; Metallic Breath is a *different* one-use pool from ordinary Breath Weapon. Gem leaves are currently nonselectable and are not silently enabled; their Psionic Mind/Flight definitions remain available for later first-party gameplay review. Additional movement properties such as \`climbingSpeedFeet\`, \`swimmingSpeedFeet\`, and \`burrowingSpeedFeet\` are not fully covered by the three current intrinsic foundation slots.
- **Dwarf:** Hill Stability is currently a descriptive trait without normalized executable fields. It needs a mechanical definition before automatic mixed-grant authorization can be treated as an executable effect.
- **Elf:** Drow adds a 120-foot Darkvision source and Sunlight Sensitivity alongside an innate spell progression. High and Wood add three-stage spells bound to a selected spellcasting ability; Wood's 35-foot walking speed must reconcile with its shared 30-foot Elf root. Spell courses must not be casually merged stage by stage.
- **Gnome:** Forest's innate illusion and animal communication are not yet source-complete spell courses. Rock's 15-foot passive Earthbound Tremorsense is *not* automatically combined with Dwarf's activated 60-foot Stonebound Tremorsense. The owner-selected Dwarf/Rock test outcome is Stonebound-only.
- **Goliath:** Six playable Ancestries inherit a nonselectable Goliath base. Cloud, Stone and Storm have explicitly shared parent/use-pool relationships between signature activations and Strikes; Frost/Hill include alternate Strike modes; Fire has tiered attacks and innate resistance. Higher-level unlocks are committed when a trait is inherited, not free future choices. The base's Giant Ancestry Aptitude is a source-bound Strength/Constitution choice that advanced features must resolve.
- **Halfling:** Lightfoot adds Natural Agility with three Skill bonuses. This needs the same conflict/stacking treatment as other source-derived Skill bonuses.
- **Tiefling:** Three playable Fiendish Ancestries inherit a nonselectable Tiefling base. Resistance types are separate from the three-stage innate spell courses. Preserve their individual spellcasting-ability selections, free-cast limits and full future progression. None of these abilities is currently automatically biologically transferable.

## Executable source-neutral shared-resource dependency increment — 2026-11-15

The common Origin engine now invokes \`NativeOriginResourceDependencyBinder\` **after** selected grants have been reconciled and authored intrinsic choices bound. It recognizes \`sharedUsesFromTrait\` on a canonical native \`origin-trait\`, not guessed capability names or Species-specific exception code.

- Three existing Goliath Ancestry relationships are implemented without modifying their underlying mechanical definitions: Cloud Strike **spends Cloud's Jaunt**, Stone Strike **spends Stone's Endurance**, and Storm Strike **spends Storm's Thunder**.
- A dependent Strike must have exactly one **actually selected** resource-owner trait from the **same contributing lineage** and exact native source. A different donor's equivalent ability is not permission to borrow its charges.
- The binder verifies that the effective trait preserves the original \`sharedUsesFromTrait\` and \`extraUsesGranted: false\` declarations and that the target owner retains a canonical \`uses\` definition. A missing, ambiguous, altered or unverified owner suppresses the Strike and produces an incomplete/unresolved result. It **never** synthesizes a new resource or implicitly transmits an unselected owner.
- A valid dependent has \`ResourceOwners: { "uses": "<actual-owner-grant-id>" }\` in its \`NativeOriginEffectiveGrant\`. This is a stable save/load relationship for downstream consumers; it is **not** an additional pool. \`NativeOriginContractValidator.ReviewProfile\` validates these persisted references and the canonical owner rules so fabricated owner IDs cannot become effective by serialization.
- \`Cloudy Escape\` has \`independentUsesFrom: "clouds-jaunt"\` and its *own* \`uses\` object, so it remains an **independent** Proficiency Bonus/Long Rest pool. The resource binder does not mistake it for a shared-use dependent.
- Regression tests exercise all three shared owner/Strike pairs with present and absent owner, two native contributors offering the same Ancestry but separate selections, forged saved owner IDs, the explicit independent Cloudy Escape pool, and preservation of evidence across JSON serialization.

**Scope boundary:** These changes validate how an **already authorized or DM-authorized selected** trait spends another authorized trait's pool. They **do not** opt Goliath's currently unmarked Strike abilities into automatic biological inheritance; do not implement a final resource-expenditure state machine, giant-ancestry ability modifier binding, new allocation policy or the complete set of generic characteristic dependencies. Those remain separate steps. The native Goliath definitions were not modified.

## Required next gates before general first-party mixed construction

1. **Native coverage:** Continue auditing and authoring opt-in only where inherent mechanics, acquisition scope, full future progression and associated restrictions are known. Do not mark learned features biological merely because they appear on a source Species. Unmarked traits remain eligible for a verified DM decision but not default inheritance.
2. **Trait dependencies and equivalence:** Add source-neutral authored links for required traits (for example, shared-resource Goliath Strikes), conditional drawback coupling, upgrade/substitution, equivalent sense mechanics and size/movement conflicts. The engine must fail closed if the complete linked configuration is not satisfied.
3. **Full projection:** Consume chosen biological grants through actual combat, hit points, skill/sense, spell, resources and character-sheet pipelines. Persist per-parameter provenance, choice evidence and the applicable rule revision.
4. **Mixed descendant trust:** Authenticate a previously manifested parent's *current effective inherent* profile; prevent unexpressed historical family traits and acquired Upbringing, Class, supernatural or temporary-form grants from becoming biological offers.
5. **Allocation:** Introduce one editable **provisional** method-scoped safeguard against unrestricted independent-benefit unions. Do not adopt an unvalidated seven/eight-point cost model, fixed two-feature quota, arbitrary primary-Species rule, or precise combat-power target.
6. **End-to-end acceptance:** Test ordinary selectable Species, mixed combinations, multiple contributors and multigenerational constructions at actual future unlocks. Conventional presets remain valid direct selections.

**No biological RNG, complete combat model, Construct procedure implementation, universal numerical power equality, or new directory architecture is required by this milestone.**
