# Warlock Pact pillar — approved acquisition architecture

**Status:** Owner-approved level-1 Pact acquisition, three-pillar identity, and Pact-bound Invocation lifecycle (2026-10-25). Additional Pact acquisition gating and Pact-specific mechanics are deferred. These are design approvals, not activation of the pending native Warlock.

## Three independent pillars

The native Warlock progresses through three distinct pillars:

| Pillar | Conceptual role | Initial native entitlement |
| --- | --- | --- |
| **Patron** | **Who** the Warlock derives power from | Patron identity at Warlock **level 1** |
| **Pact** | **What form the power relationship takes / terms of the arrangement** | One **dedicated Pact selection at Warlock level 1**, separate from Invocations |
| **Eldritch Invocations** | **Additional clauses/capabilities** that extend or modify the arrangement | Invocation selections remain separately budgeted; preserve level-1 access |

This is a **narrative analogy, not a universal legal-contract requirement**. Warlocks may derive their abilities from inherited Patron power, an unusual arrangement, or setting-specific noncontractual connections. A Pact choice defines the character's magical specialization and its mechanical entitlements regardless of whether a literal bargain was signed. The Patron-pillar rules regarding inherited power and obligations remain in force.

## Approved native Pact rules

1. **Every Warlock receives one first Pact selection at class level 1.** It is a class feature in the **Pact pillar** with its own dedicated selection entitlement.
2. A first Pact **never consumes an Eldritch Invocation selection**. Being a Pact is not equivalent to being an Invocation even though the 2024 source classifies the three as invocations.
3. **Pact of the Blade**, **Pact of the Chain**, and **Pact of the Tome** are the three currently in-scope Pact options.
4. Eldritch Invocations represent clauses attached to a specific Pact. They may enhance a Pact or have an explicit Pact prerequisite. These are **additional choices**, using separately approved Invocation entitlements, rather than a second instance of the base Pact grant. Pact affiliation is distinct from a mechanical prerequisite that names Blade, Chain, or Tome.
5. Guided Warlock presets and custom/advanced creation **use precisely the same** first-Pact selection and any additional entitlements. No guided-mode double-grants.
6. Initial Pact and Patron identities both exist at Warlock **level 1**. Any prerequisite depending on one must be evaluated in an order-independent level-up/character-creation process; the player should not be forced to choose one narrative ordering to make an otherwise valid combination.
7. The Pact pillar remains separate from the Patron and Invocation pillars in native data and rules evaluation.

## Pact lifecycle and Pact-bound Invocations (approved design direction)

A **Pact is a major agreement**, not an ordinary Invocation or interchangeable minor customization. **Every Eldritch Invocation is a clause attached to a Pact**. That Pact's continuing existence controls access to its attached Invocations; this is true even for an Invocation that does not require a specific type of Pact.

- **Pact termination:** When a Pact is broken, its attached Invocations **cease to be available**. An Invocation attached to a broken Pact does **not** continue functioning independently.
- **New agreement:** A newly formed Pact **may explicitly restore or transfer** Invocations originating under a previous Pact, if the new terms allow. **There is no automatic carryover**.
- **Traceability:** Preserve each Invocation's association with its Pact and its acquisition history, so loss, restoration, or transfer is explainable and auditable. A useful implementation distinction is **unavailable but recorded**, rather than permanently deleting the ability record when its Pact is broken.
- **Multiple Pacts:** The rules/data model must allow *distinct Pact agreements* and therefore identify which Pact provides which Invocation. This does **not** grant additional Pacts in the current default progression.
- **Narrative flexibility:** Pact is an agreement/relationship metaphor, not always a written contract. Established Patron inheritance/noncontractual-source rulings remain valid.

**Not yet specified:** What events constitute a broken Pact, when its benefits stop, whether Invocation selection entitlements are refunded or repurposed, how many Invocations may transfer, transfer costs, whether Invocations may have more than one Pact association, and any additional-Pact gating. Do not invent those mechanics during native encoding. Retaining a historical record does not authorize automatic reacquisition.

## Additional Pacts — advanced possibility, gate deliberately deferred

**Owner direction:** A second or third Pact must remain a possible future *advanced character option*, understood as another **major agreement**. It is **not an ordinary Eldritch Invocation purchase**, and it is neither permanently forbidden nor automatically available under current native progression. Eligibility, earliest level, special entitlements/costs, Pact limits, coexistence rules and the exact terms for Invocation transfer will be decided when implementing advanced options. Current default Warlocks receive **one level-1 Pact**.

## Source comparisons (preserved, not controlling native costs)

- **2014 / SRD 5.1:** Pact Boons were distinct class choices at Warlock **level 3**.
- **2024 / SRD 5.2.1:** Blade, Chain, and Tome appear among **Eldritch Invocations**, with a level-1 route at an Invocation-selection cost.
- Native Dorks & Dice uses a **dedicated level-1 Pact entitlement** without Invocation cost. This combines an early specialization with a meaningful dedicated Pact identity.
- The two SRD classifications remain unchanged in `data/reference/srd/warlock-srd-slice-inventory.json` (`PB` for 2014, `EI` for 2024). They remain historical records, not alternate native ability definitions.

## Explicitly undecided

- **Additional Pacts:** conceptually permitted as a future advanced option, **not available through current default progression**; their level, cost, maximum, coexistence rules and acquisition restrictions are deferred.
- **Later Pact upgrades/entitlements**: whether a Pact receives any special advancement choices separate from optional Invocations.
- **Mechanics** of Pact of the Blade, Chain, and Tome individually, including any interaction with Eldritch Blast, familiars, and spellcasting.
- **Invocation budgets, scaling, and option prerequisites** remain under the Invocations pillar; approval of a free dedicated first Pact must not silently rewrite those counts.

## Next case

Additional Pacts remain possible but **their gating is intentionally deferred**. Proceed to reconcile **Pact of the Blade**, **Pact of the Chain**, and **Pact of the Tome** individually. Pact-termination edge cases and invocation transfer costs remain follow-up rule design.

Related: [Warlock integrated SRD slice](warlock-srd-integrated-vertical-slice.md), [Patron reconciliation](warlock-patron-pillar-reconciliation.md).
