# Universal Character concepts

Rules Core separates universal Character semantics from source identity and from the rule implementation that calculates a value.

The authoritative flow is:

```text
universal semantic concept
  <- explicit source/import aliases
  <- immutable source provenance
  -> one or more applicable rule/mechanical implementations
```

A source entity, a canonical source identity, and a universal Character concept are different abstractions. Two source entities can map to one Character semantic concept while retaining different source types and different mechanical contracts. Conversely, related concepts are not merged merely because a later edition groups their functions differently.

## Compatibility boundary

Persisted Source Layer entities and Rule Concepts are not destructively rewritten by this model. Existing concept keys such as `skill.craft-alchemy` and `tool.alchemists-supplies` remain valid implementation identities and continue to carry their source bindings, decisions, and provenance.

The Character mechanics response now exposes two intentionally different surfaces:

- `mechanics`: implementation/evaluation mechanics, including legacy source-shaped mechanic keys required for compatibility and rule-specific calculations;
- `competencies`: the authoritative universal semantic competency catalog.

A universal competency entry exposes:

- `semanticKey`, such as `competency.alchemy`;
- stable `identityKey` and display name;
- universal family membership;
- one Character training-state key;
- family child keys for organizational family entries;
- implementation mechanic keys;
- compatibility mechanic keys;
- reviewed source/import aliases;
- applicable profiles and facets;
- normalized universal mechanical semantics, including deterministic governing-Ability resolution;
- relationships;
- source attribution/provenance;
- open-ended `presentationCategory` metadata supplied by Rules Core for consumer placement, including `skill`, Character-facing `competency`, and non-row `supporting`.

Consumers should render and store semantic competency identity from `competencies`. They should use the listed implementation mechanic/profile when a calculation is required. They should not rebuild semantic identity by grouping `skill.*` and `tool.*` records themselves.

## Reviewed aliases

The reviewed direct historical aliases are:

| Source/import representation | Universal competency |
| --- | --- |
| Bluff | Deception |
| Diplomacy | Persuasion |
| Handle Animal | Animal Handling |
| Heal | Medicine |
| Intimidate | Intimidation |
| Sense Motive | Insight |
| Pick Pocket (3e) | Sleight of Hand |
| Wilderness Lore (3e) | Survival |
| Knowledge (Arcana) | Arcana |
| Knowledge (History) | History |
| Knowledge (Nature) | Nature |
| Knowledge (Religion) | Religion |
| Knowledge (Psionics) | Psionics |
| Knowledge (the planes) | The Planes |

The Knowledge rule is a reviewed pattern: `Knowledge (X)` maps to semantic competency `X`. Knowledge is not a visible Character family.

The reviewed cross-source skill/tool shared identities are:

| Universal competency | Reviewed source/import aliases |
| --- | --- |
| Alchemy | Alchemy; Craft (alchemy); Alchemist's Supplies |
| Calligraphy | Craft (calligraphy); Calligrapher's Supplies |
| Carpentry | Craft (carpentry); Carpenter's Tools |
| Cobbling | Craft (cobbling); Cobbler's Tools |
| Gemcutting | Craft (gemcutting); Jeweler's Tools |
| Leatherworking | Craft (leatherworking); Leatherworker's Tools |
| Painting | Craft (painting); Painter's Supplies |
| Pottery | Craft (pottery); Potter's Tools |
| Stonemasonry | Craft (stonemasonry); Mason's Tools |
| Weaving | Craft (weaving); Weaver's Tools |
| Forgery | Forgery; Forgery Kit |

A universal identity does not require aliases from multiple editions. Source-defined tools and other trainable capabilities can establish their own universal competency without a historical skill counterpart. Rules Core does not invent a source-native `Craft (...)` or other historical record to fill such a gap.

The semantic identity does not merge numeric systems. Alchemy can simultaneously have a 3.x ranked-skill profile and a later tool-proficiency profile. Ranks, class-skill state, governing Ability, trained-only behavior, Armor Check Penalty behavior, proficiency contribution, and tool-use rules remain attached to the applicable profile/facet.

At the universal level, Ability aliases such as `wis`/`wisdom` and `int`/`intelligence` normalize to one key. A competency with one known governing Ability reports `fixed`; a competency whose reviewed implementation profiles genuinely disagree reports `varies-by-implementation` plus the complete normalized Ability-key set. Rules Core does not erase a conflict by returning no Ability.

## Universal competency families

`Craft`, `Perform`, and `Profession` are organizational universal families. The Craft family itself and Craft-only historical specialties are `supporting` presentation identities: they continue to supply rules, ranks, and reconciliation facets without becoming a visible Character-facing competency list. Reviewed shared Craft/tool identities remain universal competencies and can be presented once under their normalized identity.

`Craft`, `Perform`, and `Profession` are organizational universal families. A family is not a shared rank pool. Every child is independently addressable and can carry its own source/rule implementation.

The reviewed family rule is also an authoritative Rules-layer mechanical default. A reviewed child inherits these mechanics unless a reviewed specialization explicitly overrides them:

| Family | Governing Ability | Ranks | Class-skill state | Training state | Trained only | Armor Check Penalty | Evaluation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Craft | Intelligence | yes | yes | yes | no | no | ranked skill |
| Perform | Charisma | yes | yes | yes | no | no | ranked skill |
| Profession | Wisdom | yes | yes | yes | yes | no | ranked skill |

These defaults are Rules-layer normalization, not fabricated source records. If a reviewed family member is absent from the effective source-shaped Rule Concepts, Rules Core materializes a usable universal mechanic/profile with `profileOrigin: rules`, no SourceEntity revision attribution, and the stable semantic mechanic key such as `competency.blacksmithing`. Character consumers do not fabricate the profile or infer the family Ability.

The reviewed baseline catalog is deliberately reviewed rather than inferred from arbitrary strings. Additional imported source-defined specialties can still exist, but they do not become reviewed baseline members merely by name similarity.

### Craft

Reviewed baseline membership:

- Alchemy
- Armorsmithing
- Basketweaving
- Blacksmithing
- Bookbinding
- Bowmaking
- Calligraphy
- Carpentry
- Cobbling
- Gemcutting
- Leatherworking
- Locksmithing
- Painting
- Pottery
- Sculpting
- Shipmaking
- Stonemasonry
- Trapmaking
- Weaponsmithing
- Weaving

Reviewed shared Craft/tool identities are limited to Alchemy, Calligraphy, Carpentry, Cobbling, Gemcutting, Leatherworking, Painting, Pottery, Stonemasonry, and Weaving. This is explicit policy, not a name-matching rule.

Other reviewed later tools remain independent universal competencies when one later proficiency spans several historical specialties or when the historical concept is only related. Smithing is independent from Blacksmithing/Armorsmithing/Weaponsmithing; Tinkering is independent from Locksmithing/Trapmaking; Woodcarving is independent from Bowmaking/Carpentry. Brewing, Cooking, Herbalism, and Navigation likewise remain distinct from related Profession specialties.

Standalone tool-only identities such as Glassblowing, Cartography, Poisoning, Thieves' Tools, musical instruments, gaming sets, and future source-defined tools require no fabricated Craft history.

### Perform

Reviewed baseline membership:

- Act
- Comedy
- Dance
- Keyboard Instruments
- Oratory
- Percussion Instruments
- Sing
- String Instruments
- Wind Instruments

3e Perform described performance forms under the broader Perform implementation rather than independently ranked children in the same way as the 3.5 categories. Those forms remain source/rule implementation evidence; they are not promoted into additional independently ranked universal children without a reviewed identity decision.

The nine independently ranked 3.5 categories are the universal baseline family members. Instrument-category names do not automatically imply a particular later instrument-tool proficiency.

### Profession

Reviewed baseline membership:

- Apothecary
- Boater
- Bookkeeper
- Brewer
- Cook
- Driver
- Farmer
- Fisher
- Guide
- Herbalist
- Herder
- Hunter
- Innkeeper
- Lumberjack
- Miller
- Miner
- Porter
- Rancher
- Sailor
- Scribe
- Siege Engineer
- Stablehand
- Tanner
- Teamster
- Woodcutter

These are the reviewed listed baseline specialties. Profession names do not automatically map to tools.

## Deliberately separate concepts

The following older competencies remain separate because their historical rules assign independent rank state and mechanically distinct checks:

```text
Hide + Move Silently -> Stealth
Listen + Spot -> Perception
Balance + Tumble -> Acrobatics
Climb + Jump + Swim -> Athletics
```

These are composite relationships, not aliases. The granular universal competencies remain independently meaningful and the later umbrella value is derived by the existing reviewed composition rule.

`Open Lock` and `Disable Device` also remain separate from unrestricted `Thieves' Tools`. Their relationships are scoped to their corresponding uses. `Disguise` remains distinct from `Disguise Kit` with a related-competency relationship rather than shared unrestricted training identity.

## Speak Language

Historical `Speak Language` source evidence remains preserved, but it is reconciled through the language-proficiency subsystem. It is not projected as an ordinary universal competency or skill mechanic, and Rules Core does not invent a governing Ability for it. Compatibility and source history remain available at the Source/Rules boundary for migration and audit purposes.

## Migration

The migration strategy is in-place and non-destructive:

1. retain immutable source-native evidence and SourceEntity revision identity;
2. retain existing Rule Concept IDs, concept keys, bindings, decisions, and Campaign references;
3. project old concept keys through explicit reviewed semantic aliases;
4. retain source-shaped `mechanics` entries for evaluation and compatibility;
5. expose one semantic entry in `competencies` for the Character consumer;
6. retain compatibility mechanic keys so old Character rank, class-skill, and training references can be recognized during migration;
7. accept both raw historical concept keys and historical `competency.*` mechanic keys when resolving Character-owned state;
8. keep facet-specific numeric Character state separate while using the universal training-state key for shared learned identity.

The legacy Knowledge-family cleanup additionally removes obsolete visible `Knowledge` family metadata from older persisted source revisions during Character projection without mutating immutable source evidence.

## Universal Size

Creature Size is a universal Character semantic category. Rules Core defines one nine-value catalog:

| Category | Source code | 3.x AC modifier | 3.x grapple modifier |
| --- | ---: | ---: | ---: |
| Fine | F | +8 | -16 |
| Diminutive | D | +4 | -12 |
| Tiny | T | +2 | -8 |
| Small | S | +1 | -4 |
| Medium | M | 0 | 0 |
| Large | L | -1 | +4 |
| Huge | H | -2 | +8 |
| Gargantuan | G | -4 | +12 |
| Colossal | C | -8 | +16 |

The Character-facing mechanic remains `character.size-category`; there is no edition-specific Large, Tiny, or other duplicate semantic category. Source codes and full names normalize through the same catalog. PCGen and legacy SRD translation recognize all nine values. Unknown source-defined values are preserved in source-specific/unmapped evidence instead of being silently discarded.

The Size category itself is universal. AC, grapple, reach, space, carrying, and other consequences remain rule implementations that consume it.

## Nearby Character concept audit

The same audit was applied to adjacent Character-facing mechanics. No additional source-edition identity split was found that required migration in this change:

- ability scores use stable semantic keys such as `ability.strength.score`; temporary and ordinary state are separate mechanics, not edition identities;
- initiative uses `combat.initiative`;
- movement modes use semantic keys such as `movement.walk`;
- maximum hit points use `health.maximum-hp`;
- saving throws use stable mechanical identities appropriate to the effective rule model, including the 3.x Fortitude/Reflex/Will mechanics and ability-based saving throws where applicable;
- Armor Class consequences are represented by semantic defense mechanics, including the reviewed 3.x Touch and Flat-Footed variants, rather than edition-prefixed AC identities.

These mechanics can have edition-specific formulas and source contributions without changing the semantic identity of the thing being calculated.
