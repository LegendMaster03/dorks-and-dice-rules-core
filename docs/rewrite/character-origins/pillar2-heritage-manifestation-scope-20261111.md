# Character Origins — Pillar 2 scope and next design boundary

**Date:** 2026-11-11  
**Status:** Proposed Origin design; this document records boundaries and open decisions, not a final inheritance allocation, a gameplay rule, or an authorized runtime schema.

## Pillar 2 outcome

## Work sequence: general model first

**Owner decision (2026-11-11):** Establish and validate the **universal Character Origins characteristic, contributor, manifestation and compatibility model** before introducing rules for any particular same-Species mixed-Ancestry pairing. [General Origin model work plan](pillar2-universal-origin-model-first-20261111.md) is the active architectural priority.

The Dragonborn formula, shared-foundation audit, same-Species pair matrix, and synthetic compatibility contracts remain useful **design probes** and future acceptance fixtures. They do **not** justify selecting Dragonborn category/material grouping, damage-type coupling or pair exceptions before the shared framework is defined. The same general pipeline must accept direct standard, mixed Ancestry, mixed Species, nested and homebrew contributors, with only actual authored differences changing its review outcomes.


An Origin profile should identify **how a character originated, where potential inherited characteristics came from, and which characteristics actually manifest**. It must be possible to produce the same profile structure for direct standard Species/Ancestry selection, custom Heritage, and homebrew. Nothing about Pillar 2 requires implementing the mechanics of Breath Weapon, combat, condition resolution, spells, or the Character Sheet.

**Established principle:** Origination establishes how an individual came into existence. Heritage identifies sources from which inherent characteristics *may* derive. The actual character is determined by manifest traits or explicit Origin decisions. Contributor identity, parenthood, and inheritance candidacy do not automatically grant a complete source's traits.

## Application of the system-wide table customization principle

The [native rules design specification](../native-rules-design-specification.md#governing-design-principle--the-rules-are-a-framework-not-a-constraint-on-the-table) establishes **table customization as a standing principle for all Dorks & Dice rules**, not a Pillar 2-specific exception.

In Character Origins, an unmodified Heritage candidate pool describes **default-source eligibility**, not every character a DM may approve. The Rock Gnome example below demonstrates the difference: Gnome + Dwarf does not inherently supply Rock Gnome's passive Earthbound Tremorsense under the current first-party data, but a table may explicitly authorize a custom variant or Origin. Such a customization should remain distinguishable from the first-party source characteristics and retain its own provenance; the exact customization mechanism is not yet designed.

## Separate concepts

1. **Origination:** The process by which the individual came into existence. Biological parenthood is one possible process, not a universal prerequisite.
2. **Heritage contributors:** Individual entities or documented Heritage configurations that supply *eligible* characteristic candidates. A contributor may itself be mixed, may be nonbiological, and must not be implicitly constrained to one of the first-party selectable Species.
3. **Characteristic definitions:** Shared trait identities with complete behavior references, fixed restrictions, and explicit inheritable **parameter slots**. A defined trait is not automatically selected.
4. **Heritage candidate pool:** Available trait identities **and** offered values per eligible parameter. Retains contributor occurrence IDs and source lineage; distinguishes fixed operational fields from inherited configurable values.
5. **Manifestation selection:** Chosen traits and parameter values with their provenance and compatibility constraints. This determines the Origin's actual inherent characteristics. **Eligibility is not manifestation.**
6. **Origin profile:** An immutable *conceptual* record of the resulting manifested traits/parameters, lineage references, explicit choices and unresolved/adjudicated entries. This is what a future Rules Core API should project; its mechanical effects are not implemented by Pillar 2.

**Cultural separation:** Background and Upbringing are independently selected parts of Character Origins. Their features and cultural defaults do not become biological grants because a contributor has that Background or Upbringing.

## Example with existing approved scope

Red Dragonborn contributes a configurable ordinary Breath Weapon with Fire damage and a complete 30 × 5-foot line area. Bronze Dragonborn contributes Lightning damage and a 15-foot cone.

**Approved for this trait:** independent value selection for damage type and **whole area**. Thus Fire+Line, Fire+Cone, Lightning+Line, and Lightning+Cone are each valid *potential configured characteristics*, with separate provenance for each parameter.

The trait's other characteristics—activation, limited uses, saving throw, and level-based progression—remain part of the same feature, **not** extra inheritable parameters. Pillar 2 should preserve these definitions and restrictions, but it does not need to calculate their numerical values, resolve targets, roll damage or simulate a combat turn.

This decision does not establish unrestricted recombination for all traits. Linked parameters remain representable for traits where their rules explicitly bind values together. An area object is one parameter; its length and width do not automatically become separately inheritable.

## Proposed minimal profile representation (non-authoritative)

```json
{
  "origination": {"method": "author-defined"},
  "heritage": {
    "contributors": [
      {"contributorId": "source-a", "heritageRef": "ancestry.dragonborn.red"},
      {"contributorId": "source-b", "heritageRef": "ancestry.dragonborn.bronze"}
    ]
  },
  "manifestation": {
    "traits": [
      {
        "mechanicId": "ability.breath-weapon",
        "parameters": {
          "damageType": "fire",
          "area": {"shape": "cone", "lengthFeet": 15}
        },
        "parameterSources": {
          "damageType": ["source-a"],
          "area": ["source-b"]
        }
      }
    ]
  }
}
```

This illustrates **relationships only**. It is not an adopted ID format, mandatory Origin form, contributor count, generation algorithm, or permission to manifest Breath Weapon. In particular, the profile does not grant all other Dragonborn traits by omission, and `method: author-defined` is not an approved origination type.

## Dragonborn expansion reference

The [Dragonborn three-factor Ancestry formula](pillar2-dragonborn-expansion-dimensions-20261111.md) identifies each **standard Dragonborn Ancestry** through **category, category-related skin material, and damage type**. The ordinary Breath Weapon's complete area **usually follows the category** for a standard Ancestry; it is not a fourth independent defining factor. Additional categories and Ancestries are planned, with a long-term goal of each relevant damage type appearing within every major category. Today's native sample is deliberately limited.

**Upbringing is outside this Pillar 2 Dragonborn system.** It belongs to independently selected cultural Character Origins content, even when a separate hoard-related cultural suggestion is authored. The three defining ancestry factors are not automatically three unrestricted mixed-Heritage selection choices. The previously approved independent damage-type/whole-area recombination for ordinary Breath Weapon **still applies to mixed Heritage**, while other characteristic grouping and cross-trait dependencies remain open.

## Rites of Rebirth — post-origin Species/Ancestry replacement

**Owner-established design direction:** A **Rite of Rebirth** completely replaces the character's current Species **and** Ancestry configuration, including the former configuration's inherent grants. It is not a new inheritance merge or an additive feature package. A later Rite replaces the then-current configuration again. Historical parents, origination records, Background, and Upbringing are preserved. Independently obtained benefits are retained according to their own sources, rather than copied forward as old Species traits.

**Draconic Rebirth** is the Dragonborn specialization. It also provides the planned acquisition path for exceptional category-leader Dragonborn forms, normally inaccessible at character creation without DM approval. Chromatic composite and Metallic singular Platinum are distinct patterns; neither grants every category trait automatically. See [Rites of Rebirth and exceptional Dragonborn](pillar2-rites-of-rebirth-20261111.md) for agreed boundaries and unresolved requirements.

This is an approved **conceptual replacement rule**, not an implementation of ritual execution, persistent transformations, or the present provisional Origin profile schema.

## Mixed Ancestry versus mixed Species

**Design inference from the Species/Ancestry separation:** Combining multiple Ancestries **within one Species** should normally be simpler than combining different Species. Same-Species contributors already share a defined Species foundation, so the character's Origin can focus on the Ancestries' distinct eligible traits, characteristic values, category relationships, and manifestation decisions. For Dragonborn, the standard ancestry variation is largely expressed through **category, category-related skin material, and damage type**. A category usually supplies ordinary Breath Weapon geometry for a standard Ancestry; the separately approved mixed-Heritage rule still permits an eligible whole area and damage type to be inherited independently.

**Cross-Species composition** may require reconciling several Species foundations, including possibly incompatible creature types, size, movement, senses, physical anatomy and complete traits. The presence of two Species contributors does not authorize automatically stacking or inheriting both complete foundations. Contributors remain evidence for candidate characteristics, not guaranteed grants.

These are **two common cases handled by the same general Origin/Heritage engine**, not a mandate for separate evaluators or two incompatible rulesets:

- **Shared Species foundation:** When contributors share a Species, retain that common definition once as a source of eligible characteristics; inspect the distinct Ancestry contributions and their dependencies without pretending that the shared Species grants stack.
- **Distinct Species foundations:** When contributors differ in Species, preserve each foundation's candidate evidence and resolve competing intrinsic values, dependencies and trait interactions through generalized, explicitly selected rules.
- **Neither is automatically unrestricted:** Same-Species mixed Ancestries can still have category/material or trait conflicts. Some cross-Species combinations may be mechanically straightforward. Complexity is a consequence of actual characteristic differences, not an arbitrary prohibition or a mandatory extra budget.

**Implementation implication:** Source/characteristic identity, parameter bindings, manifestation and conflict handling should be reusable in both cases. The [read-only shared-foundation audit](pillar2-shared-species-foundation-audit-20261111.md) now demonstrates that a source-neutral projection can retain one copy of each shared authored Species node while preserving each contributor's evidence and distinct configured characteristic offers. **This is not a shared-foundation grant or conflict-resolution policy.** An optimization or clearer user experience for shared-Species composition is reasonable, but should not become a Dragonborn-only solver or a hard-coded requirement that every custom Origin have biological parents. The final permissions, default manifestation groups and conflicting Species foundation policies remain to be determined.

## Planned compatibility coverage: within-Species and cross-Species

**Owner design direction:** A formulaic Species can define the dimensions that vary across its own Ancestries **in advance**, allowing intentional coverage of their combinations, dependencies and exceptional cases. For Dragonborn, the known standard-Ancestry dimensions are **category, category-related skin material and damage type**, while ordinary Breath Weapon area normally follows category and remains independently available for approved mixed-Heritage selection. This makes systematic testing and authored exceptions feasible as additional categories and Ancestries are introduced.

**Mixed-Ancestry design target:** For each Species, author and validate compatibility **rules over the characteristic dimensions** (including explicit constraints, substitutions and narrow exceptions) rather than programming every pair of Ancestry IDs. A new Ancestry that uses the existing pattern should receive compatibility treatment through its data. This can justify a stronger expectation of deliberate within-Species support than the broad mixed-Species disclaimer provides. It still does not guarantee unrestricted or balanced combinations; real conflicts must be reported and reviewed.

**Mixed-Species coverage target:** The universal Origin engine should preserve both contributor foundations, recognize generic characteristic conflicts and offer DM adjudication or customization. Attempting exhaustive pairwise Species/Ancestry compatibility rules is **not** a reasonable requirement. Curated **cross-Species exceptions** may be authored for cases worth supporting, but they must not imply complete pairwise coverage or bypass general dependency validation.

**Implementation distinction:** A declarative Species-level compatibility policy would be **data interpreted by the same general engine**, not a dedicated Dragonborn/Elf/etc. evaluator. The [synthetic compatibility-contract prototype](pillar2-declarative-compatibility-prototype-20261111.md) now demonstrates conditional requirements, exclusions and narrowly documented exception reviews against **explicit hypothetical choices**, while the same-Species audit systematically enumerates existing pairs without frozen counts. **Neither adds real Dragonborn category/material constraints, approves any new compatibility combinations, or settles a final contract schema.** Cross-Species acceptance should focus on representative generic conflicts, selected curated exceptions and clear unresolved results, rather than exhaustive pairing.

## Mixed-Species limits, balance expectations, and table authority

**Owner-established expectation:** The generalized Origin system should support as many mixed-Species combinations as reasonably feasible, but the rules do **not** promise that every Species combination has a mechanically coherent default configuration or comparable balance with standard characters. This is a realistic design boundary, **not** an instruction to stop investigating difficult combinations or to reject a combination solely because its contributor Species differ.

**Proposed player-facing disclaimer (wording subject to later editorial approval):**

> **Mixed-Species Character Creation**
>
> Mixed-Species characters offer additional customization, but some combinations may introduce mechanical incompatibilities or balance differences not present in standard Species and Ancestry options. The rules aim to support as many combinations as practical; not every combination is guaranteed to have a compatible or equally balanced default configuration. Some may require DM adjudication or custom adjustments. The DM and players may agree on alternatives appropriate for their campaign.

**Implementation implications:**

- Distinguish **supported default composition**, **unresolved or incompatible default composition**, and **explicitly table-customized composition**. Do not silently assign conflicting creature types, size, movement, traits, or other foundational characteristics merely to produce a result.
- A failure to construct a valid **default** combination must report the actual unresolved dependency or conflict, rather than claiming that the fictional character or a DM-approved homebrew character is inherently impossible.
- Balance remains an **evidence and playtest question**. Neither this disclaimer nor the mere existence of a candidate profile establishes equal power, balance certification, an automatic compensation budget, or an unreviewed restriction.
- Mixed Ancestry within one shared Species remains a primary tractable case; the disclaimer is not a pretext to skip expected support for ordinary same-Species composition. Mixed Species should still use the **same** generalized characteristic engine with the additional reconciliation it actually needs.
- The standing [system-wide table customization principle](../native-rules-design-specification.md#governing-design-principle--the-rules-are-a-framework-not-a-constraint-on-the-table) continues to apply. A table's custom adjustment is not silently represented as an unmodified first-party default.

This records the **accepted limitation and a draft disclaimer**, not final approved text, a blacklist of Species pairs, or an automated compatibility classifier.

## Reference example for future rules: Rock Gnome divergence

The [Gnome + Dwarf → Rock Gnome reconstruction](pillar2-gnome-dwarf-rock-gnome-reconstruction-20261111.md) is an intentional counterexample to the assumption that every Ancestry must be recreatable as an immediate sum of its plausible predecessors. Gnome + Dwarf offers the Gnome baseline and a Dwarf Tremorsense trait, but **not** Rock Gnome's different passive Tremorsense trait.

**Illustrative historical possibility, not canon:** A population descended from Gnomes and Dwarves might, over many generations, have developed Rock Gnome's distinct Tremorsense. The narrative relationship would not grant a first-generation character that variant automatically.

Future Pillar 2 rules should distinguish **possible ancestry history**, **shared mechanical family**, **the actual available trait variants and their parameters**, and **what manifests on the character**. No genetic algorithm or forced recipe across generations is needed. An established descendant variant may be acquired through its actual source even if its distant historical origin is unknown. A new direct derivation recipe, if desired, requires separate explicit approval.

The read-only [illustrative relationship fixture](pillar2-illustrative-ancestry-relationships-20261111.json) and regression tests preserve this as a future rule-design example without changing native Gnome, Dwarf, or Rock Gnome gameplay content.

## Pillar 2 decisions to resolve in order

| Priority | Question | Acceptance evidence |
| --- | --- | --- |
| 1 | What does a contributor reference when the contributor is themselves mixed or custom? | A two-generation example preserves the contributor's *manifested* characteristic values rather than magically restoring traits that never manifested |
| 2 | What makes a trait and its parameters available, and how are availability and manifestation distinguished? | Candidate pool and manifested profile have different explicit records; a contributor cannot automatically confer its full rules package |
| 3 | Which traits require other characteristics, are incompatible, or use linked parameter groups? | Structured dependency/conflict tests reject invalid selections without requiring special cases for Species IDs |
| 4 | How are overlapping characteristics, replacement, upgrades, and mutually exclusive physical properties handled? | A standard Elf/High Elf inheritance example preserves intended sense/speed distinctions without duplicate grants |
| 5 | How do direct standard selections use the same profile representation? | Round-trip Origin-profile comparisons for Human, High Elf, Red/Gold Dragonborn and other selectable presets, **without running combat** |
| 6 | What are the creation-time trait budget and optional balancing constraints? | Separate explicit, playtestable allocation policies, not an assumed equal-power scoring algorithm |

Tests should also cover an independently authored homebrew contributor, two identical contributors permitting a standard configuration, and missing/unknown characteristic definitions remaining explicit for DM review.

These decisions should be solved through general concepts first, not ad hoc rules for any one Species.

## Out of scope

- Executing actions, targeting, saving throws, damage, healing, combat resource consumption, condition resolution, or spellcasting.
- Reimplementing existing shared mechanical subsystems solely to prove a trait's inheritance.
- Character Sheet UI, new gameplay APIs, character persistence, and deployment.
- Mandatory trait point costs, hard-coded limits on parent count, or implementation of unapproved inheritance/recombination policies.
- Conflating biological Species with upbringing and other non-inherent parts of Character Origins.

Existing read-only trait inventory, binding, and configured-instance tooling can continue to provide **Origin model evidence**. No further numerical combat preview work is needed for Pillar 2.

**Next substantive design work:** define the structure and provenance of **mixed contributors who are themselves mixed**, then represent the distinction between available and manifested traits for ordinary and nested contributors. This is distinct from choosing a final balancing policy.
