# Rogue — Band 1 reconciliation (working decisions)

**Status:** Partial owner-approved design record, 2026-10-23. **Not implemented as a native class.** Scope: Rogue class levels 1–20, no subclasses, prestige classes or executable runtime.

## Approved: Sneak Attack frequency

**Owner decision (2026-10-23):** Sneak Attack applies to **every successful qualifying attack**, not only once per turn or round. The 5.x once-per-turn cap is removed for native Dorks & Dice. The historical 3.x repeated-attack capability is preserved.

- Determine eligibility **separately for each attack at the time it resolves**; an earlier qualifying hit does not automatically qualify later hits.
- When the Rogue's attack reveals their location or otherwise ends a hidden/unseen advantage under the applicable native stealth rules, subsequent attacks must qualify by another valid condition or regain eligibility legitimately.
- **Surprise is not a blanket Sneak Attack prerequisite.** Historical denied-Dexterity-defense and flanking conditions and modern Advantage/nearby-ally conditions may persist across attacks; their precise reconciliation remains separately open.
- Each eligible successful attack receives **one** application of the Rogue's class-level Sneak Attack damage. A single attack does not receive multiple duplicate Sneak Attack dice pools merely because several qualifications apply.
- This feature **does not grant additional attacks**, remove action/resource restrictions, or resolve the system-wide BAB/Extra Attack progression.
- The design deliberately allows a character with legitimately acquired additional attacks and maintained Sneak Attack eligibility to deal Sneak Attack damage on each such hit, even if that produces substantially higher damage than a simple single-attack turn.

**Rationale:** Basic play remains straightforward; interested players may obtain greater combat effectiveness through tactical preparation, maintaining favorable positions or other legitimate qualifying attack opportunities, without a hard per-turn damage cap.

## Explicitly unresolved: eligibility and related mechanics

This decision changes **frequency only**. The following are still to be reconciled against 3.0, 3.5e, SRD 5.1, SRD 5.2.1, and established native combat rules:
- Consolidated Sneak Attack qualification, including target vulnerability, flanking, denied Dexterity defense, Advantage, an allied combatant, and how Disadvantage interacts.
- Historical restrictions (e.g., precision-target anatomy, concealment, attack range) versus modern Finesse/ranged weapon requirements and any applicable immunities.
- Interaction with Cunning Strike's Sneak Attack die costs, native critical escalation, special attack sequences, reaction attacks, and broader action economy.
- Other Rogue base-class reconciliation (e.g., historical Trapfinding, Trap Sense, special ability selections; modern Cunning Action, defenses, Weapon Mastery).

No native Rogue JSON has been implemented or marked reconciled by this document.

## Source references

- 3.5e Rogue: data/reference/3x/band1-baseclasses/srd35/classes-ii.md on feature/band1-baseclass-srd-sources
- 5.1 / 5.2.1 Rogue: data/reference/srd/structured/class/class-rogue.json
- Governing Dorks & Dice design: docs/rewrite/native-rules-design-specification.md
