# Rogue — Band 1 reconciliation (working decisions)

**Status:** Partial owner-approved design record, 2026-10-23. **Not implemented as a native class.** Scope: Rogue class levels 1–20, no subclasses, prestige classes or executable runtime.

## Approved: Sneak Attack frequency

**Owner decision (2026-10-23):** Sneak Attack applies to **every successful qualifying attack**, not only once per turn or round. The 5.x once-per-turn cap is removed for native Dorks & Dice. The historical 3.x repeated-attack capability is preserved.

- Determine eligibility **separately for each attack at the time it resolves**; an earlier qualifying hit does not automatically qualify later hits.
- When the Rogue's attack reveals their location or otherwise ends a hidden/unseen advantage under the applicable native stealth rules, subsequent attacks must qualify by another valid condition or regain eligibility legitimately.
- **Surprise is not a blanket Sneak Attack prerequisite.** Detection mechanics also apply during ongoing combat. Other established vulnerabilities may persist even after the Rogue is detected; the qualifying details remain separately open.
- Each eligible successful attack receives **one** application of the Rogue's class-level Sneak Attack damage. A single attack does not receive multiple duplicate Sneak Attack dice pools merely because several qualifications apply.
- This feature **does not grant additional attacks**, remove action/resource restrictions, or resolve the system-wide BAB/Extra Attack progression.
- The design deliberately allows a character with legitimately acquired additional attacks and maintained Sneak Attack eligibility to deal Sneak Attack damage on each such hit, even if that produces substantially higher damage than a simple single-attack turn.

**Rationale:** Basic play remains straightforward; interested players may obtain greater combat effectiveness through tactical preparation, maintaining favorable positions or other legitimate qualifying attack opportunities, without a hard per-turn damage cap.

## Approved: Sneak Attack eligibility principle

**Owner-approved native rule (2026-10-23), exact wording:**

> A Rogue can apply Sneak Attack damage to a successful attack when the target has failed to detect or anticipate the attack, or when the target has an explicitly established vulnerability that the Rogue can exploit with a precision strike.
>
> Detection uses the shared awareness and detection mechanics. Other qualifying vulnerabilities are established by specific conditions, actions, features, or successful techniques.
>
> Advantage does not independently establish Sneak Attack eligibility. Each attack is evaluated separately.

**Interpretation and boundary:** This builds upon historical Rogue precision damage while making failed detection or anticipation independently relevant. The shared surprise-awareness procedures can inform the determination, but a formal surprise round is not required. The target can know the Rogue's location and still have an established vulnerability. An Advantage roll by itself, flanking by name, or the presence of an ally is not sufficient without an explicitly established qualifying rule. A successful qualifying attack can activate the separately approved Sneak Attack damage frequency once, even when multiple routes qualify.

**Not yet approved:** An exhaustive list of eligible conditions, actions or techniques; the timing/duration of anticipation and awareness; whether and how an attack reveals its attacker; qualification through existing flanking or denied-Dexterity defenses; unusual attack forms or explicit precision-damage immunities; exact interactions with Cunning Strike, concealment/visibility, critical damage dice and general combat action rules. The fixed 30-foot cap is removed. Sneak Attack with a weapon requires proficiency with that weapon; neither Finesse nor Ranged properties are mandatory. Neither Disadvantage nor Weapon Mastery is imposed as an additional Sneak Attack weapon-eligibility rule. Normal attack range and visibility still apply. Swashbuckler's eventual place among classes, subclasses and prestige classes remains open, rather than a reason to narrow the Rogue's base feature.

## Approved: range, weapons, damage immunity and distinct defensive grants

**Owner-approved rulings (2026-10-23):**

- **No fixed Sneak Attack distance cap.** An attack from long range can qualify, including a sniper's attack. Ordinary weapon/effect range, targeting, visibility and the established detection-or-vulnerability criteria still apply; remove the historical 30-foot Sneak Attack range limit.
- **Weapon proficiency is the Sneak Attack weapon requirement.** A Rogue can apply Sneak Attack with **any weapon they are proficient with**, if the attack hits and satisfies the independently approved detection-or-vulnerability requirements. Finesse and Ranged weapons remain familiar Rogue choices, but those properties are **not required**. Proficient longsword, greatsword or other unconventional weapon builds are valid without a special Sneak Attack Disadvantage penalty or reduction in Sneak Attack dice. Ordinary attack modifiers (including Disadvantage when independently imposed) still apply normally. **Weapon Mastery gives only its normal benefits** and neither replaces proficiency nor separately grants Sneak Attack eligibility. Proficiency is earned through normal class, species, feat, multiclass or other legitimate sources; no blanket Rogue proficiency with martial weapons is implied. The treatment of spell attacks, unarmed strikes and other non-weapon attacks awaits the general attack-category rules.
- **Immunity is immunity; creature type or anatomy alone is not immunity.** The owner explicitly approved removing the historical blanket Sneak Attack exclusions for undead, constructs, and creatures with unusual anatomy. Sneak Attack does not bypass actual damage-type resistance or immunity or a separately defined applicable precision-damage immunity. Apply ordinary damage types and defenses. A creature does not become immune merely for lacking ordinary vital organs, being nonliving, or having immunity to critical hits unless another rule expressly grants that protection.
- **Keep distinct historical and modern defenses under clear names.** **Uncanny Awareness** is the approved native name for historical 3.x Dexterity-defense retention when Flat-Footed or facing an unseen attacker; its later anti-flanking enhancement improves **the same Uncanny Awareness feature** without changing its name to Improved Uncanny Awareness. **Uncanny Dodge** remains the separate modern 5.x Reaction-based incoming-damage reduction. Historical 3.5e levels 4/8 and modern level 5 are source-based working placements, not yet independently finalized cross-edition class progression; retain proper defense interactions and any relevant exceptions.
- **Critical escalation is optional and requires natural die results.** Effects that change, substitute or set a d20 result, including Stroke of Luck's numerical result of 20, do not by that change supply the natural die faces required to initiate or advance an optional critical escalation chain. Preserve normal explicit numeric/test success effects and the separate ordinary critical rules. The shared critical-system specification, not a Rogue-specific exception, governs this distinction.

## Explicitly unresolved: eligibility and related mechanics

The decisions settle **frequency and the high-level eligibility principle**, not every concrete trigger or the executable detection system. The following details remain to be reconciled against 3.0, 3.5e, SRD 5.1, SRD 5.2.1, and established native combat rules:
- Define qualifying detection failures and exploitable vulnerabilities, including the relationship to flanking, denied Dexterity defenses, feints, nearby allies and how Advantage/Disadvantage interacts.
- Remaining precision-damage restrictions (e.g., cover or concealment and explicitly defined precision immunity) under shared detection and damage systems. **Do not reinstate** a 30-foot cap, historical blanket creature-type/anatomy immunity, special Disadvantage for unconventional proficient weapons, or a Finesse/Ranged-only restriction. Weapon Mastery is not a substitute for proficiency.
- Interaction with Cunning Strike's Sneak Attack die costs, ordinary critical damage and optional natural-result escalation, special attack sequences, reaction attacks, and broader action economy.
- Other Rogue base-class reconciliation (e.g., historical Trapfinding, Trap Sense, special ability selections; modern Cunning Action, defenses, Weapon Mastery).

No native Rogue JSON has been implemented or marked reconciled by this document.

## Source references

- 3.5e Rogue: data/reference/3x/band1-baseclasses/srd35/classes-ii.md on feature/band1-baseclass-srd-sources
- 5.1 / 5.2.1 Rogue: data/reference/srd/structured/class/class-rogue.json
- Governing Dorks & Dice design: docs/rewrite/native-rules-design-specification.md
