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

**Not yet approved:** An exhaustive list of eligible conditions, actions or techniques; the timing/duration of anticipation and awareness; whether and how an attack reveals its attacker; qualification through existing flanking or denied-Dexterity defenses; unusual attack forms or explicit precision-damage immunities; exact interactions with Cunning Strike, concealment/visibility, critical damage dice and general combat action rules. The fixed 30-foot cap and finesse/ranged weapon gate are **removed**; normal attack range and visibility still apply. Swashbuckler's eventual place among classes, subclasses and prestige classes remains open, rather than a reason to narrow the Rogue's base feature.

## Approved: range, weapons, damage immunity and distinct defensive grants

**Owner-approved rulings (2026-10-23):**

- **No fixed Sneak Attack distance cap.** An attack from long range can qualify, including a sniper's attack. Ordinary weapon/effect range, targeting, visibility and the established detection-or-vulnerability criteria still apply; remove the historical 30-foot Sneak Attack range limit.
- **No finesse-or-ranged weapon gate.** Qualifying Sneak Attacks are not restricted to Finesse weapons or Ranged weapons. Weapon choice by itself is not a qualifying vulnerability. Weapon/attack legality, successful hit, and the established detection-or-vulnerability criteria remain necessary. The attack category's interactions with spells, unarmed strikes and unusual attack forms should be defined by the general combat system rather than presumed here.
- **Immunity is immunity.** Sneak Attack does not bypass actual damage-type resistance or immunity. The Sneak Attack damage portion follows the applicable damage type(s) and defenses determined by ordinary native damage rules. Do **not** infer a universal Sneak Attack immunity simply from the target being nonliving, having unusual anatomy, or resisting critical hits; whether a particular creature has a separate explicit precision-damage immunity must be established by its actual traits and the applicable conversion policy.
- **Keep distinct historical and modern Uncanny Dodge capabilities.** The 3.x feature preserving eligible Dexterity defenses when Flat-Footed or facing an unseen attacker (and its historical anti-flanking improvement) is mechanically separate from modern 5.x Uncanny Dodge's Reaction-based incoming-damage reduction. Retain both abilities, not two copies under an identical name. **Working naming suggestion, not yet owner-approved:** historical `Uncanny Awareness` and `Improved Uncanny Awareness`; modern `Uncanny Dodge`. Historical-versus-modern feature levels and exact defense interactions remain for final reconciliation.
- **Critical escalation is optional and requires natural die results.** Effects that change, substitute or set a d20 result, including Stroke of Luck's numerical result of 20, do not by that change supply the natural die faces required to initiate or advance an optional critical escalation chain. Preserve normal explicit numeric/test success effects and the separate ordinary critical rules. The shared critical-system specification, not a Rogue-specific exception, governs this distinction.

## Explicitly unresolved: eligibility and related mechanics

The decisions settle **frequency and the high-level eligibility principle**, not every concrete trigger or the executable detection system. The following details remain to be reconciled against 3.0, 3.5e, SRD 5.1, SRD 5.2.1, and established native combat rules:
- Define qualifying detection failures and exploitable vulnerabilities, including the relationship to flanking, denied Dexterity defenses, feints, nearby allies and how Advantage/Disadvantage interacts.
- Remaining precision-damage restrictions (e.g., cover or concealment, unusual target traits, and explicit precision immunity) under the shared detection and ordinary damage systems; **do not reinstate** a 30-foot cap or Finesse/ranged-only gate.
- Interaction with Cunning Strike's Sneak Attack die costs, ordinary critical damage and optional natural-result escalation, special attack sequences, reaction attacks, and broader action economy.
- Other Rogue base-class reconciliation (e.g., historical Trapfinding, Trap Sense, special ability selections; modern Cunning Action, defenses, Weapon Mastery).

No native Rogue JSON has been implemented or marked reconciled by this document.

## Source references

- 3.5e Rogue: data/reference/3x/band1-baseclasses/srd35/classes-ii.md on feature/band1-baseclass-srd-sources
- 5.1 / 5.2.1 Rogue: data/reference/srd/structured/class/class-rogue.json
- Governing Dorks & Dice design: docs/rewrite/native-rules-design-specification.md
