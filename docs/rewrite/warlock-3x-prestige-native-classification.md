# Warlock 3.5e prestige material — source-to-native classification

**Status:** owner-approved native **classification rule** (2026-10-24), with case-based classification of the four researched Warlock-related historical prestige classes. Their mechanics, entitlement, and non-SRD integration remain deferred; the SRD-only Warlock slice is unchanged.

## Governing classification rule (owner decision)

**If a specialization involves more than one base class, it is a prestige class.** If it develops only a single base class, it belongs to that class's development system (conventional subclass or a class-specific modular specialization, as appropriate). Do **not** override this class-count distinction based on 3.5e publication labels, number of levels, or subjective breadth of abilities.

- **One class:** represent as a subclass or, for Warlock, coherently selectable parts with a guided preset. A one-class historical source prestige class does **not** require a native prestige progression merely because the source used one.
- **More than one class:** retain native prestige-class classification. The interaction of two classes, including existing arcane/divine spellcasting and Warlock invocation advancement, is the defining criterion. Preserve those separate class mechanics and advancement effects.
- **Always retain source provenance.** An imported 3.5e `prestigeClass` source concept remains faithfully labeled in its source data, while a native conversion record can represent it as a one-class specialization. Do not silently overwrite the imported identity.

This rule settles the **category**, not detailed prerequisites, numerical effects, balancing, how a preset allocates selections, or when implementation is authorized.

## Secondary review questions (only for implementation inside the selected category)

1. Which class-owned abilities, dependencies, and resources must the resulting parts reference?
2. Which effects are independently acquirable, which form upgrade chains, and which should remain cohesive bundles?
3. Can a preset reproduce the historical specialization without duplicating advancement entitlements?
4. Do the source abilities translate cleanly into the established native mechanics (including removed alignment restrictions and approved rest/competency rules)?
5. For a multi-class prestige option, how do the independent class advancement tracks and effective caster/invocation levels interact?

## Four historical cases — classification settled, mechanics deferred

| 3.5e source prestige class | Native category | Why | Work still to do |
| --- | --- | --- | --- |
| **Hellfire Warlock** (*Fiendish Codex II*, 3 levels) | **Warlock specialization** (parts and preset) | Develops Warlock invocation/Eldritch Blast capabilities rather than a second base class | Preserve its dangerous hellfire trade-off, invocation prerequisites, infusion/defense effects and source-based progression when later eligible |
| **Enlightened Spirit** (*Complete Mage*, 10 levels) | **Warlock specialization** (cohesive branch/preset) | Develops Warlock's supernatural/spiritual powers, not a different base class's advancement | Preserve its interdependent blast, aura, flight and defense progression; do not conflate automatically with a modern Celestial Patron |
| **Eldritch Disciple** (*Complete Mage*, 10 levels) | **Prestige class** | Combines invocation-using Warlock development with **divine spellcasting** from another class | Preserve cross-class prerequisites and progression rather than converting to a Warlock-only parts preset |
| **Eldritch Theurge** (*Complete Mage*, 10 levels) | **Prestige class** | Combines invocation-using Warlock development with **arcane spellcasting** from another class | Preserve both class advancement paths and spellcasting/invocation interactions |

**Classification is an approved ruling. Implementation of the non-SRD mechanics is not.** None of these four expansions is included in the currently authorized SRD-only playable Warlock vertical slice.

## Why this matters to the active vertical slice

- The approved implementation remains **SRD 5.1 + SRD 5.2.1** full Band 1 Warlock including Fiend, Pacts and invocations, plus prior approved historical 3.5e *base-class* feature rulings.
- The four non-SRD 3.5e prestige classes are **reference-only compatibility stress cases**, useful for verifying the model is not incompatible with future thematic paths or multiclass progression.
- Do **not** add Hellfire or Enlightened Spirit playable features, 3.5e heritage feat catalogs, or 3.5e invocation prerequisites to the active content inventory now.
- Once the SRD-based Warlock model is tested, integrate the **already classified** single-class Warlock specializations and multi-class prestige options in separately approved content passes; review each one's detailed prerequisites, effects and budget without reopening the approved one-class-versus-multiple-class categorization.

## Historical reference sources (research)

- *Fiendish Codex II*, Hellfire Warlock: https://srd.dndtools.org/srd/classes/prestigeFc2/hellfireWarlock.html
- *Complete Mage*, Enlightened Spirit: https://srd.dndtools.org/srd/classes/prestigeCmage/enlightenedSpirit.html
- *Complete Mage*, Eldritch Disciple: https://srd.dndtools.org/srd/classes/prestigeCmage/eldritchDisciple.html
- *Complete Mage*, Eldritch Theurge: https://srd.dndtools.org/srd/classes/prestigeCmage/eldritchTheurge.html
- Repo’s structural distinction: `docs/mechanical-relationships.md`; native Band levels: `docs/rewrite/native-rules-design-specification.md`.
