# Character Origins — Pillar 2 reconciliation

**Status:** Initial evidence review and unapproved proposals. This document is not authoritative native rules content.

**Repository base:** `main` at `d5a2b15b7ae2bd88ae9e47a5ddfad01d481623bb` (merged PR #130, 2026-11-03).  
**Working branch:** `feature/character-origins-pillar2-heritage`.  
**Authoritative rules:** `data/rulesets/first-party-001/native/`.  
**Ruleset:** `first-party-001`.

## Approved baseline preserved from Pillar 1

- Species, Ancestry, and Upbringing are distinct. A default Upbringing is a suggestion, not a restriction.
- Half-Elf and Half-Orc are not standalone native Species.
- Any defined creature type can be playable. The explicitly assigned type determines its direct type-targeting eligibility. Monster type hierarchy/statblock defaults do not automatically grant player traits.
- Learned cultural benefits are not automatically inherited from Species.
- Previously approved Species/Ancestry benefits remain in effect unless deliberately reconciled.

## Verified repository findings (2026-11-03)

The current native tree contains **nine Species base JSON files, 29 Ancestry JSON files (including nonselectable families and unresolved/deferred entries), and ten Upbringing JSON files**.

- Species records carry `creatureType`, size or size choice, walking speed, inline `traits`, and sometimes `defaultUpbringing`. There is no single uniform schema for all mechanical trait effects.
- Ancestries carry `parent` stable IDs, inline `traits`, sometimes `darkvisionFeet` or `walkingSpeedFeet` overrides, and spellcasting selections. Examples: `ancestry.elf.drow`, `ancestry.goliath.fire`, and the multilevel Dragonborn family.
- `species.elf` has Low-Light Vision, Keen Senses, Fey Ancestry, Trance, and magical-sleep protection. `species.orc` has Low-Light Vision, Adrenaline Rush, Relentless Endurance, Powerful Build, Savage Attacks, and Menacing Presence. `species.goliath` has a character-level-5 Large Form.
- Upbringings are independent records. For example, `upbringing.elf.general` grants Elvish and a cultural skill choice; `upbringing.elf.high` extends it with separate training.
- Some effects repeat inline under local trait IDs, such as `low-light-vision` and `powerful-build`. A local `trait.id` is **not** a globally indexed `NativeRuleEntity` ID. An existing shared rule reference for Low-Light Vision is partial reuse, not evidence of a general reusable trait resolver.
- `NativeCatalogLoader` validates and indexes top-level native entities; it does not execute inheritance from `parent`. `NativeCharacterRequest` has no dedicated Species, Ancestry, Upbringing, Heritage, or Origination fields, and `NativeCharacterProjector` has no general resolver for these packages. JSON visibility is not equivalent to executable grants.
- The creature-type catalog explicitly distinguishes classification/type targeting from monster/NPC statblock defaults. Multiple effective types and automatic inheritance resolution remain deferred.

**Priority implication:** Establish rule semantics and representative character profiles before refactoring the catalog. Once a rule is approved, separately mark whether it is authored, loadable, resolvable, or available through the character consumer.

## Reference-system comparison (design evidence, not adopted mechanics)

1. **An Elf and an Orc Had a Little Baby:** Separates parentage traits and upbringing. The initial main procedure uses two birth parents, but the first book's Appendix B permits traits from more than two parentages within its point limit, and later books explicitly allow one or more parentages. Useful: distinct inherent/acquired characteristics, multiple heritage inputs, and alternative trait packages. Limitations for this project: the source model is still centered on birth parentage, its specific 5e point valuations do not transfer automatically, and artificial or spontaneous origins require another generalization.
   - https://marketplace.roll20.net/browse/compendiumexpansion/24690/an-elf-and-an-orc-had-a-little-baby/
   - https://anyflip.com/ilydf/nule/basic/101-113
   - https://www.geeknative.com/77281/worth-your-money-an-elf-and-an-orc-had-a-little-baby-parentage-and-upbringing-in-dd/
   - https://www.scribd.com/document/968618475/If-an-Elf-and-an-Orc-Had-a-Baby-4
2. **3.5e SRD:** Half-Elf and Half-Orc have discrete race-based effects and special race-targeting eligibility; monsters as player characters use racial Hit Dice, Level Adjustment and ECL. Useful: a clear distinction between ancestry identity and effect eligibility, and awareness that creature power differs. Do **not** copy ECL, racial Hit Dice or Level Adjustment into the native character system.
   - https://www.d20srd.org/srd/races.htm
   - https://www.d20srd.org/srd/monstersAsRaces.htm
3. **5.1 SRD:** Half-Elf and Half-Orc contain distinct bundles, useful as recognizable acceptance targets but not mandatory new Species. Historic Half-Orc includes Relentless Endurance and Savage Attacks, now present in native Orc.
   - https://5e.d20srd.org/srd/races.htm
4. **5.2.1 SRD:** Removes Half-Elf and Half-Orc as standalone species, while adding Orc and Goliath. This does not by itself supply Dorks & Dice with a generalized mixed-inheritance mechanic.
   - https://www.dndbeyond.com/srd

## Foundational distinctions — **proposed, not approved**

- **Origination:** The event or process by which an individual came into existence. It can have zero or more participants/causes: genetic contributors, gestational hosts, creators, material providers, collective organisms, or magical forces. Participating in an origin is not itself a trait grant.
- **Heritage:** The set of relevant sources and established relationships for potential inherent characteristics. A source can be a Species, Ancestry, existing individual, designed blueprint, magical influence or other defined origin source. Heritage is not restricted to biological descent, pairs or percentages.
- **Species identity:** A character's declared playable biological/morphological/magical identity, represented conventionally by a native Species package. It need not be equivalent to every contributor, and unusual characters might use a custom playable body pattern rather than a single conventional Species identity. This does not remove the separate need for an explicitly resolved creature type.
- **Inherent traits:** The actual mechanically granted characteristics. Each grant needs an identified origin, effect, progression, and any qualifications or interactions. A source relationship establishes **eligibility**, not an automatic complete package.

**Proposed invariant:** A character receives each inherent effect only because a selected Species/Ancestry preset or explicit heritage decision grants it. Relationships and descriptors alone never grant mechanics. Upbringing and Background remain separate.

## First rules-design boundary — **working proposal, not approved**

An Origination event can have zero or more participants and sources. A relationship describes how a source was involved; it does **not** mean that every attribute of that source can be inherited.

Heritage establishes which source characteristics can **qualify** for expression. The character's explicit selected inherent traits establish what actually manifests. The transmission of an individual's variant (as distinct from its Species default) is a separate open question.

The effective character profile must resolve enough properties for play, including explicit creature type, size, physical form where relevant, movement, senses, and needs. A standard Species/Ancestry selection supplies a conventional predefined profile. A future advanced custom origin can supply one without being forced to adopt the identity of one contributor.

The Origination process itself may be mechanically relevant: a magical ritual, construct material, or supernatural manifestation might establish a characteristic through an explicit rule. This is not the same as passively inheriting the creator's Species/Class features.

The existing single effective creature-type rule remains in force for current characters; mixed-type eligibility and any broader identity targeting must be reconciled independently. An Elf/Orc heritage source should not automatically count as both Fey and Humanoid for all type-targeted effects.

A trait may be inheritable, physically dependent on a body plan, available only through a supernatural origin, or acquired rather than inherent. These categories are candidates to investigate, not finalized trait tags or replacement rules.

## Alternatives to test before selecting a permanent structure

| Option | Benefit | Limitation |
|---|---|---|
| Entire parent packages combined | Simple model | Unbounded benefits; ignores multiple/nonbiological sources |
| One Species package, cosmetic secondary ancestry | Simple balance | Mixed heritage mechanically empty |
| Primary conventional Species plus constrained trait exchanges | Smooth onboarding, accepts asymmetry | Requires justified equivalence/replacement rules; hard for parentless origins if treated as universal |
| Configurable trait package with eligible sources and constraints | Generalizable, handles unusual origins | Higher design/authoring and UI burden; avoid requiring manual reconstruction of conventional characters |

**Working recommendation:** Keep an easy predefined Species/Ancestry preset as a convenience route. Resolve all characters through a general trait-grant model under the hood, with constrained changes for mixed or unusual origins. Do not approve trait point costs or trait-exchange equivalences before representative comparisons.

## First representative character tests — **illustrative only**

1. **Conventional High Elf:** Resolve `species.elf` and `ancestry.elf.high` with the existing Fey classification, 30-foot walk speed, Low-Light Vision, base Elf traits and High Elf spell progression. A default Upbringing can be accepted or changed. No new choices are exposed unless player customizes.
2. **Elf with Orc heritage:** Keep an Elf-based conventional profile for the test (Fey, Medium, 30 feet, Low-Light Vision). Test replacing Keen Senses with Menacing Presence, subject to review of equivalence and source eligibility. This must not automatically add all Orc effects or grant Humanoid type eligibility.
3. **Orc with Elf heritage:** Keep an Orc-based conventional profile (Humanoid, Medium, 30 feet, Low-Light Vision). Test a distinct exchange, such as Menacing Presence for Fey Ancestry, only if the traits are judged compatible and balanced. The two mixed profiles need not be identical. The proposed transfers are **not approved mechanics**.
4. **Construct:** A fabricator, a designer and a material source may all participate in Origination, while an explicit created-body profile establishes creature type, inherent abilities and any material-related resistances. A designer's own Species must not automatically flow into the product.
5. **Fungal or hive character:** A spore source or colony may be one collective contributor or a process; no two-parent slots are required. Its plant classification and effective capabilities must be explicit character grants, not blindly inherited monster defaults.
6. **Inherited supernatural potential:** A historical bloodline or pact may establish provenance, potential, obligations or eligibility. Only explicit traits produce effects. Sorcerer or Warlock Class levels and associated features are never automatically granted.

## Open decisions (do not presume approval)

1. Is a primary Species identity required for all playable characters, or is it simply the default preset route while advanced characters can declare a custom inherent body profile?
2. Does mixed heritage use an explicit source/eligibility list with trait substitutions, or another constrained selection method?
3. How should a mixed character determine creature type: one explicit effective type, separately selected effective types, or a new classification procedure? Current approved single-type targeting remains intact until this is resolved.
4. Which traits are free-standing reusable definitions versus localized effects, and what constitutes an allowed replacement?
5. Can traits be inherited through individual variation rather than Species defaults, and what prevents unlimited multi-generational accumulation?
6. How are physiological dependencies, incompatible properties, duplicate abilities and level-dependent effects resolved?

## Implementation boundary

No mechanics, Species records, or C# contracts are changed by this initial evidence review. Await specific design approvals. When approved, develop the smallest native profiles and executable consumer tests needed to demonstrate at least conventional Elf and mixed Elf/Orc. Keep provisional Upbringing rules for the separate Life Experiences pass.
