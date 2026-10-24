# Investigating generalized class-development systems: subclass-to-parts feasibility

**Status: research and design hypothesis only — not owner approval, native schema adoption, or feature implementation.**
**Scope:** Six representative subclasses from the imported SRD 5.1 (2014) and SRD 5.2.1 (2024) candidates on `feature/rules-core-rewrite`. This is intentionally a *small cross-class stress test*, not a new audit of all classes, subclasses, or historical options. Existing approved base-class rulings retain priority. Warlock remains the planned first full vertical slice.

## Questions being tested

1. Can the Warlock-inspired four pillars be treated as **four universally required systems**, **four categories into which each base class installs one system**, or a **class-composed vocabulary with optional/multiple systems**?
2. Can ordinary subclasses become individual **parts** plus **source-compatible presets** without losing source mechanics, bundling assumptions, or coherent advancement?
3. Should the *same parts* be selectable across classes, or should only their *shared underlying effects and acquisition procedures* be reused?

## Findings in brief

**Neither four compulsory systems nor exactly one per category survives the examples without artificial exceptions.** Class abilities and options cross category boundaries, and some classes need zero or several independent systems within a category. Treat Identity, Specialization, Capabilities as descriptive axes; Progression/Guidance is a cross-cutting scheduling/composition/presentation concern, not a competing pool. Retain "four pillars" as Warlock's organizing experience, but do not hard-code them as four columns that every base class must fill.

**A subclass is not necessarily a set of independently selectable feature items.** It can be decomposed into one or more **atomic parts**, **dependent upgrade chains**, and **inseparable or stateful bundles**. A traditional subclass **preset** describes how these parts are granted or selected, but does not itself grant an additional duplicate feature budget.

**Reuse at three distinct levels:** (1) common rule/effect execution, (2) configurable development-system implementations, (3) reusable *content parts* only when ability semantics, prerequisites, costs, scaling, and entitlement align. Shared category membership by itself does **not** make a feature portable across classes.

## Source-based subclass examples

### A. Warlock — Fiend (2014 / 2024): mostly discrete abilities, with source timing differences

Source: `data/native/classes/subclasses/warlock/fiend.json`, candidate features under `data/native/features/subclasses/warlock/fiend/`.

- **Patron identity**: Fiend relationship/origin; must not be confused with an automatically purchased or active ability.
- **Spell-access part**: 2014 Fiend adds options to the Warlock list; 2024 grants a different set of specified spells. Native spell preparation is already abolished, so "always prepared" cannot be translated literally without conversion.
- **Dark One's Blessing**: source levels 1 (2014) or 3 (2024). The newer version also triggers on a nearby enemy reduced to 0 HP by someone else. This is a coherent feature and a viable individual part.
- **Dark One's Own Luck**: level 6 in both, using different recovery/use schedules. A selectable part needs an edition-reconciled native use limit and relevant roll hooks.
- **Fiendish Resilience**: level 10, *damage-type resistance*, selected on a rest. **It is not** the approved 3.5e *base-class* Fiendish Resilience (periodic healing). Different stable IDs and rules are necessary even if source display names collide.
- **Hurl Through Hell**: level 14, targeting/return/recharge differ by edition; 2024 permits Pact-slot spending to restore a use. Remains an effect with its own resource contract.

**Split:** Fiend identity + spell-access grant + four feature parts, organized by a Fiend preset. **Not automatically freely mixable** with other Patron parts; prerequisites and advancement grants must say which cross-origin combinations are legal. For testing, compare a traditional Fiend preset against a custom combination with identical entitlements.

### B. Druid — Circle of the Land (2014 / 2024): shared state and tightly coupled effects

Source: `data/native/classes/subclasses/druid/land.json`, candidate features under `data/native/features/subclasses/druid/land/`.

- 2014 specifies multiple named terrain spell lists and fixed subclass improvements; 2024 specifies four land choices (Arid, Polar, Temperate, Tropical), with an **active land chosen each Long Rest**.
- The 2024 **Circle Spells** depend on the currently selected land. **Nature's Ward** at level 10 maps the **same active land** to a damage-resistance type.
- **Nature's Sanctuary** at level 14 grants allies the **current Nature's Ward resistance** inside its area; it is not a generic unrelated defensive power.
- **Land's Aid** at level 3 and **Nature's Sanctuary** consume finite **Wild Shape** uses. **Natural Recovery** at 6 refers both to circle spell access and ordinary slot recovery.

**Split:** A **Land-attunement bundle** must own the current land selection and expose typed context. Spell-list, Ward and Sanctuary components may be separately defined parts but must depend on that shared state. Land's Aid can be a separately granted capability using the existing Wild Shape resource. This is **not** a flat pick-any-five menu: selecting Sanctuary without compatible Land-attunement/Ward state would be invalid. The current native Druid has independently approved companion-path choices and Wild Shape mechanics; do not replace those with subclass-specific clones.

**Main framework requirement:** reusable choice state with rest-bound recalculation, part dependencies, and resource references. Do not require an abstract universal "Land" subsystem.

### C. Bard — College of Lore (2014 / 2024): discrete parts sharing a class resource

Source: `data/native/classes/subclasses/bard/lore.json`, candidate features under `data/native/features/subclasses/bard/lore/`.

- **Bonus Proficiencies** at level 3: a compact independently meaningful selectable skill grant; uses universal skill/competency entitlements.
- **Cutting Words** at level 3: a Reaction consuming Bardic Inspiration and modifying an opponent's qualifying roll. It is *not* generic speech flavor; actual perception, trigger and use costs must hold.
- **Additional Magical Secrets** (2014) / **Magical Discoveries** (2024) at level 6: class-list-crossing spell access, but different source eligibility. The native no-preparation and optional Open Spell Access decisions affect how much value the feature still adds; must avoid automatic redundancy or duplicate spell slots.
- **Peerless Skill** at level 14: another Bardic Inspiration-dependent feature, with different trigger/recovery details across editions.

**Split:** Four independently defined capabilities, but two rely on **Bardic Inspiration** and one relies on Bard spell-access mechanics. Their shared dependency is the *resource contract*, not their membership in "Bard subclass." A class that does not have Bardic Inspiration should not acquire Cutting Words merely because it has a "Capabilities" category; either disallow or require an explicitly approved alternate resource/conversion.

**Main framework requirement:** parts may be independently modeled without being universally **eligible**.

### D. Fighter — Champion (2014 / 2024): upgrade chain, not additive pick-list

Source: `data/native/classes/subclasses/fighter/champion.json`, candidate features under `data/native/features/subclasses/fighter/champion/`.

- **Improved Critical** at 3 changes the weapon/unarmed critical threshold to 19–20.
- **Superior Critical** at 15 changes it again to 18–20. This must **upgrade** or supersede, not stack as two independent critical-range bonuses. Interactions with the native more-extreme-d20 and maximum-critical policies must be tested.
- **Remarkable Athlete** (edition timing and scope differ), **Additional Fighting Style** (2014 at 10, 2024 at 7), **Heroic Warrior** (2024 at 10), and **Survivor** at 18 are distinguishable parts; some include bundled effects such as multiple Survivor benefits.
- The 2014/2024 Champion preset maps differing source milestones and entitlement counts into the same native part graph.

**Split:** critical-optimization **upgrade chain** plus separate athletic, fighting-style, inspiration and survival parts; Survivor may remain a **composite part**. Do not make every named paragraph a separately purchasable feature, and do not create a Champion-specific critical engine.

**Main framework requirement:** upgrades, exclusivity/supersession, source-specific timing, and coherent composites.

### E. Rogue — Thief (2014 / 2024): enhancement of an existing class action

Source: `data/native/classes/subclasses/rogue/thief.json`, candidate features under `data/native/features/subclasses/rogue/thief/`.

- **Fast Hands** at level 3 changes how the Rogue can use its existing quick-action/Cunning Action facility. The newer source broadens item-use possibilities, including appropriate magic items.
- **Second-Story Work** (3), **Supreme Sneak** (9), **Use Magic Device** (13), and **Thief's Reflexes** (17) are separate grants whose actual mechanics still require later reconciliation.
- **Use Magic Device** changed substantially between versions: 2014 ignored certain item requirements; 2024 grants extra attunement, charge-conservation chances and scroll-use capability. Its resemblance to 3.5 Warlock **Deceive Item** does not make the grants identical.

**Split:** individual parts that modify existing **actions and item-use behavior**, with ability-specific prerequisites. Reuse common item-competency, scroll and attunement procedures with Warlock while preserving distinct entitlement definitions.

**Main framework requirement:** a part can change **the permitted uses of an existing feature** rather than adding a freestanding action or a new resource.

### F. Wizard — Evocation / Evoker (2014 / 2024): dependency on spellbook and source conversion

Source: `data/native/classes/subclasses/wizard/evocation.json`, `data/native/classes/subclasses/wizard/evoker.json`; candidate features under `data/native/features/subclasses/wizard/`.

- **Evocation Savant**: 2014 modifies the cost/time of copying appropriate spells; 2024 adds free Evocation spells to the spellbook. The native Wizard spellbook and no-preparation choices already exist; this is not a generic unconditional "extra spell" grant.
- **Potent Cantrip**, **Sculpt Spells**, **Empowered Evocation**, **Overchannel** are class/school-specific capabilities at different historical levels, referencing spell types and casting mechanics.
- Existing native Wizard also has separate level-5/10/15/20 Specialization selection grants (Metamagic or Item Creation), which are **not** the same currency as its subclass features.

**Split:** spellbook/school-tag-dependent part(s) plus separate spell-enhancing features, grouped into an Evoker preset. Do not silently merge Wizard Specialization selections with subclass entitlements.

**Main framework requirement:** metadata-based eligibility, named spell school, and a distinction between general selection opportunities and source-specific subclass allowances.

## Three decomposition modes supported by the examples

| Mode | Typical example | Mechanical handling | Preset role |
| --- | --- | --- | --- |
| **Independent parts** | Lore Bard skills, Fiend's Dark One's Blessing | Each grants a distinct capability with its own prerequisites, cost and resource references | Select/grant the corresponding parts at relevant levels |
| **Dependency or upgrade chain** | Champion Improved → Superior Critical; Blade → Thirsting Blade; Land → Ward → Sanctuary | Store prerequisites, exclusive upgrades or references to earlier parts and state | Select in valid order; avoid double-counting upgraded effects |
| **Cohesive bundle/stateful system** | Land's active terrain and derived spell/resistance effects; source-specific spellbook rules | Bundle or state-owner part exposes shared context to dependent effects; individual effects need not each be freely purchasable | Establish a coherent package and allocate costs to it as a unit if needed |

A **part** is the smallest *coherently acquirable* rule unit—not necessarily the smallest named feature, nor a guarantee of independent portability. Parts can contain multiple coupled effects. Source names are display aliases, not stable mechanical IDs.

## Architectural implications for the two options under investigation

### Four categories versus four mandatory systems

The Warlock's original four pillars are useful **presentation categories**, but examples do not support exactly one system per category:
- Fighter does not inherently require an otherworldly **Origin** development mechanic; forcing one would be artificial.
- Druid has companion-path choice, Wild Shape charges, terrain attunement and other specializations that may need **multiple** systems regardless of category.
- Bard combines independent Bardic Music and Bardic Inspiration resources; subclass capabilities depend on one or both but do not constitute mutually exclusive category selections.
- Land's mutable terrain state is distinct from the permanent act of choosing Circle of the Land. They could both be called "Specialization" but have different lifecycle rules.
- Presets are **guided assembly of advancement grants**, not a fourth independent source of power. The schedule and builder UI should be orthogonal to the other categories.

**Research recommendation:** A class config can have zero, one or multiple development systems, each carrying optional categories/tags (Identity, Specialization, Capabilities); advancement schedules and presets are common orchestration/presentation, not a mandatory "fourth system." Retain class-specific pillars as a conceptual organization, not a global fixed cardinality rule.

### Reusable modules versus class-custom modules

Differentiate:
1. **Rule/effect primitive** (universal where equivalent): critical threshold resolution, damage reduction, healing, feature grants, spell access, item-use, resource recharge, conditional action permissions.
2. **Development module implementation** (reusable): one-of-N choice, multiple selections, bounded pool, upgrade chain, fixed grant track, dynamic rest-reselection, preset-following and custom configuration.
3. **Content entitlement / part definition** (portable *only when eligible*): Thirsting Blade, Cutting Words, Land's Aid, Dark One's Blessing. These can call generic mechanics but retain class-aware requirements and unique identities.
4. **Class configuration** (custom composition): which modules run at which levels; the part catalog, costs, prerequisites, resource references, and which systems are omitted or replaced.

**Do not conflate reuse of a rules primitive with permission to select a class feature from another class.** Cross-class reuse needs an explicit eligibility grant, not matching category tags. Distinguish effects with the same names (Warlock healing Fiendish Resilience versus Fiend resistance) and different names with similar mechanisms (Deceive Item versus Use Magic Device).

## Prototype design consequences (not yet implementation approval)

A minimal universal evaluator should support:
- Identity tags and independently granted/selected **parts** with stable IDs and source provenance.
- Multiple configurable advancement grants at the same level, each with a budget and valid option set.
- Automatic grants, one-of-N grants, non-exclusive selections, and tightly coupled bundles.
- Prerequisite expressions over level, existing parts, effective class, resource ownership, named state, and campaign policy; no class-name conditional branches in shared rule execution.
- Upgrades/supersession, different effect IDs behind overlapping display names, and safe replacement/refund when a dependent part would become invalid.
- Resource ownership, spending and recovery as shared underlying systems; part definitions can use those resources but should not duplicate them.
- Stateful selections, such as Land type selected at Long Rest, with dependent effect recalculation and validity checks.
- Presets as **ordinary selections/grants made on the same advancement ledger**; do not duplicate allowances. Guided mode is a view/automation of the same choices as advanced mode.
- Per-source conversion mapping and optional complexity with a single authoritative effective native rule context.

## Small proof tests before a full Warlock implementation

| Test | Fixture | Fails if |
| --- | --- | --- |
| Same budget, guided/custom | Fiend preset compared to manually chosen equivalent Warlock parts | Preset silently grants free duplicated features |
| Dependent path | Champion Improved Critical then Superior Critical | Critical thresholds stack instead of upgrading |
| Shared but not portable effect | Cutting Words uses Bardic Inspiration; a Fighter without that resource attempts to acquire it | Category alone bypasses resource/eligibility prerequisites |
| Dynamic state | Land changes from Arid to Polar at a Long Rest; Ward and Sanctuary follow | Derived resistances remain stale or unrelated |
| Cross-class rule reuse | Thief's magical item interactions and Warlock's Deceive/Imbue Item | Two incompatible magic-item check engines are created, or entitlements become interchangeable |
| Core feature augmentation | Thief Fast Hands modifies a defined existing action allowance | Invalid extra action resource is granted |
| Source compatibility | Reconstruct at least one 2014 and one 2024 subclass preset from common parts | Level timing, option costs, or source-specific requirements disappear |
| Optional architecture | Generic class fixture has **no** Origin system and **two** Specialization systems | A mandatory four-slot schema rejects a valid configuration |
| Non-regression | Existing Band 1 base class definitions remain unchanged | Research prototype breaks approved classes |

**Decision gate:** Do not globally convert all subclass content before the Warlock vertical slice is tested. This research supports a class-composed general model with optional modules and *several* valid subclass decomposition modes, but it does not establish balance, a final schema, or a universal migration policy.

## Repository source files examined

- `data/native/classes/subclasses/{warlock/fiend,druid/land,bard/lore,fighter/champion,rogue/thief,wizard/evocation,wizard/evoker}.json`
- `data/native/features/subclasses/{warlock/fiend,druid/land,bard/lore,fighter/champion,rogue/thief,wizard/evocation,wizard/evoker}/`
- `docs/rewrite/{native-rules-design-specification,druid-band-1-reconciliation,bard-band-1-reconciliation,wizard-band-1-reconciliation}.md`
- `data/reference/srd/class-reconciliation-decisions.json` and `data/reference/3x/warlock-band1-base-comparison.json`

All referenced subclass records remain **pending-native-reconciliation**. The comparisons above are examples of decomposition feasibility, not reconciled native subclass decisions. No generated artwork or diagrams requiring image assets were created.
