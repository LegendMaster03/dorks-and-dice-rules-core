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

## Pillar 2 foundational ruling — approved

> Origination establishes how an individual came into existence. Heritage identifies the sources from which inherent characteristics may be derived. A character's actual characteristics are determined by the traits that manifest or are explicitly established through its origin. Neither the existence of a contributor nor a relationship with that contributor automatically grants its complete mechanical characteristics.

**Interpretation:** The presence of any source, including one or more mixed-Species parents, does not grant that source's full Species/Ancestry package. Sources may be biological, artificial, supernatural, collective, or unknown. Actual grant and expression rules remain for later reconciliation.

**Not yet approved:** inheritance probabilities, dormant/transmissible traits, trait exchange rates, creature-type resolution for mixed heritage, or universal point budgets. Approval of the distinction does not settle these questions.

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

## Inheritance complexity and player choice — approved direction (2026-11-03)

- **Do not require Punnett squares, dominant/recessive allele calculations, or randomized genetic outcomes for playable-character creation.** Modeling heredity should not displace player control of the character concept.
- Biological dominance, recessiveness, or dormant ancestry may be inspiration for particular traits, families, or optional worldbuilding, but are **not** an approved mandatory game-mechanics system.
- When mixed heritage offers eligible inherent characteristics, the player should be able to choose among permissible outcomes rather than roll for the character they are allowed to play. Specific eligibility, limits, conflicts, and balance remain unresolved.
- A full mechanical record of unexpressed ancestral alleles or indefinite genealogical inheritance is **not required** by this decision. Whether dormant hereditary characteristics have any gameplay effect remains open.
- This does not remove restrictions where an approved trait requires compatible anatomy, a particular supernatural source, or other justified prerequisites.

## Direct review of user-provided source PDFs — 2026-11-03

**Source collection reviewed:** *An Elf and an Orc Had a Little Baby* Volumes 1–3 and *An Owl and a Bear Had a Little Baby* (Volume 4). All four files were accessed in the user's selected Library folder; the private Drive URLs are not reproduced here. These texts are **reference evidence, not adopted native rules**.

### Mechanical observations

- **Volume 1, introduction (printed pp. 3–4):** Character creation provides a **16-point parentage-trait pool** and separate Upbringing. Although examples emphasize two birth parents, players explicitly choose *one or more parentages*. Age, speed, sight, spellcasting, and other features may be priced as traits; repeated same-name traits normally do not stack.
- **Volume 1, Appendix B (printed pp. 108–111):** Trait selection can draw from *as many parentages as desired* within the 16-point cap. Appendix B also prices Upbringing benefits, nominally 12–17 points, including a fictional case mixing parents' and grandparents' **upbringings**. This is not evidence of tracking unexpressed biological alleles across generations.
- **Volume 1, Elven Parentage (printed pp. 24–27) and Orc Parentage (pp. 69–70):** Elven Fey Ancestry is priced at 3, Keen Senses at 2, Trance at 1; Orc Powerful Build at 1, Relentless Endurance at 6, and source Darkvision at 4. These numbers are **not compatible point costs** for Dorks & Dice: the approved native definitions differ (e.g., Orc uses Low-Light Vision and Fey Ancestry is not identical in expression). Long-Lived can have a changed result if both parents contribute it; Orc Relentless Endurance also states an enhancement if inherited twice. The source therefore contains conditionally interacting traits, not only independent purchases.
- **Volume 1, Warforged (pp. 86–88):** Describes constructed, magical, and speculative biological ways to originate mixed Warforged; nevertheless retains the two-birth-parent trait-purchase framing. Provides a concrete example of construct physiology without a general Origination model.
- **Volume 2, introduction and contents (pp. 2–4):** Retains 16-point parentage selection and extends the option catalog to additional monstrous and unusual parentages, including further Warforged material.
- **Volume 3, Autognomish (pp. 10–11), Myconid (pp. 64–66), Plasmoid (pp. 73–74):** Extends options to constructed, fungal, and Ooze-bodied characters, while generally retaining birth-parent language. Creature type may itself be selected as a priced trait (e.g., Myconid's Plant; Plasmoid's Shape Self includes Ooze classification). Autognome healing and repair options show that anatomical properties, magical effects, and targeting eligibility cannot always be treated as unrelated purchases.
- **Volume 4, introduction (pp. 3–5) and Appendix (p. 111):** Applies the same 16-point parentage approach to animals and animal-like creatures, offers no new Upbringings in that volume, and directs readers to earlier Upbringings or a design guide.

### Implications for the working Dorks & Dice proposal

1. **Retain player selection rather than genetic probability:** This is closer to the reference books' actual mechanics. A Punnett-square system would add simulation complexity not required by these sources.
2. **Study trait-value methods, not published trait prices:** Native benefits are already different from the source, and point costs require testing for packages, progression, dependencies, and undesirable negative-trait arbitrage.
3. **Do not equate all traits with independent purchases:** Some benefits require compatible morphology, depend on another feature, or change when repeated. We need a distinction among eligible traits, prerequisites, conflicts, and actual expression.
4. **Do not model Origination as exactly two parent slots:** Volume 3 includes constructed and magical origins, but still expresses them largely through parentage options. Dorks & Dice must remain able to represent zero or multiple creators or biological contributors.
5. **Keep creature type as explicit resolved classification:** A priced Fey/Plant/Ooze entry in a source table does not automatically settle native mixed-type targeting or override Pillar 1 classifications.
6. **Distinguish multi-parentage selection from genealogy simulation:** The sources support selection across multiple parentage pools, but do not require hereditary carrier records for parents, grandparents, or unexpressed traits.

**Status:** These are observations and design implications for review; no new native inheritance mechanics approved by documenting them.

## First rules-design boundary — **working proposal, not approved**

An Origination event can have zero or more participants and sources. A relationship describes how a source was involved; it does **not** mean that every attribute of that source can be inherited.

Heritage establishes which source characteristics can **qualify** for expression. The character's explicit selected inherent traits establish what actually manifests. A biological parent or any other contributing individual may themselves have mixed Species heritage; a contributor therefore cannot be modeled as exactly one Species. Heritage may extend across multiple generations without assuming equal, fixed-percentage contribution from every source. The transmission of an individual's variant (as distinct from its Species default), including traits they carry but do not express, is a separate open question.

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

## Multi-generation mixed heritage — scenario to reconcile

**Confirmed scope:** A biological parent can themselves be mixed-Species, including descendants of previous mixed parents. The model must therefore support repeated and asymmetric mixing across generations, not just one Elf parent plus one Orc parent.

**Test case:** One parent has Elf/Orc heritage, another has Human/Gnome heritage. A child might express characteristics from more than two Species; those characteristics should be evaluated individually, and their eligibility should not be equated with mechanically receiving all four Species packages.

**Unresolved rules:**
- Can an unexpressed trait remain transmissible, or is inheritance limited to traits expressed by an individual?
- Can a mixed parent transmit an inherited characteristic that is not present in their own current playable Species package?
- What limits repeated accumulation of advantageous traits without imposing universal fixed Species percentages?
- How much genealogical detail matters mechanically versus being optional character history?

**Design constraint:** Do not equate a parent's selected Species identity with the full extent of their heritage. Do not impose `50/50` or `25/25/25/25` genetic ratios as game rules without specific justification.

## Open decisions (do not presume approval)

1. Is a primary Species identity required for all playable characters, or is it simply the default preset route while advanced characters can declare a custom inherent body profile?
2. Does mixed heritage use an explicit source/eligibility list with trait substitutions, or another constrained selection method?
3. How should a mixed character determine creature type: one explicit effective type, separately selected effective types, or a new classification procedure? Current approved single-type targeting remains intact until this is resolved.
4. Which traits are free-standing reusable definitions versus localized effects, and what constitutes an allowed replacement?
5. Can traits be inherited through individual variation rather than Species defaults, and what prevents unlimited multi-generational accumulation?
6. How are physiological dependencies, incompatible properties, duplicate abilities and level-dependent effects resolved?

## Implementation boundary

No mechanics, Species records, or C# contracts are changed by this initial evidence review. Await specific design approvals. When approved, develop the smallest native profiles and executable consumer tests needed to demonstrate at least conventional Elf and mixed Elf/Orc. Keep provisional Upbringing rules for the separate Life Experiences pass.
