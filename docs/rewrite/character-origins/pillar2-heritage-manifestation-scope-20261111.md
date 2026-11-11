# Character Origins — Pillar 2 scope and next design boundary

**Date:** 2026-11-11  
**Status:** Proposed Origin design; this document records boundaries and open decisions, not a final inheritance allocation, a gameplay rule, or an authorized runtime schema.

## Pillar 2 outcome

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
