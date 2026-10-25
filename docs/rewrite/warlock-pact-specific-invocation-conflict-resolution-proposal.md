# Warlock Pact-specific Invocations — complete reconciliation proposal for owner review

**Date:** 2026-10-25  
**Branch:** `feature/rules-core-rewrite`  
**Status:** **OWNER-APPROVED NATIVE DESIGN (2026-10-25); IMPLEMENTATION PENDING.** The owner explicitly approved the complete 16-option resolution package in this document, including the Chain familiar action-economy adaptation. Original source variants and stage-only native candidates remain unchanged until actual conversion and validation. **These approved rules are not yet executable or merged to main.**

## Design rules used throughout

1. **Exactly one actual Pact owns each elected Invocation**, even if no Pact type is required for other/general options. The Patron of **that specific Pact** must accept any new Invocation clause; compatibility gates must all validate.
2. **First Pact at Warlock level 1 is free of Invocation cost.** Its already approved intrinsic advancement at 5 is neither an Invocation tax nor a second selectable feature: Blade two weapon attacks, Chain familiar extra attack, Tome three per-Long-Rest free level-3 spells, Talisman Shared Ward.
3. An adopted Invocation normally **costs one selection**, but **how many selections are available and at which levels is deliberately postponed** until the owner elects to discuss Invocation progression. Listed source-level prerequisites are **minimum eligibility gates**, not an approved global selection schedule.
4. **Default to a coherent single 2024 version when 2014 and 2024 effects conflict**, preserving all 2014 variants in the immutable reference/source candidates. Do **not** blindly sum damage from two editions or turn an Invocation into a free Pact feature.
5. **Base Pact remains usable without its own optional Invocations**. Optional Invocation upgrades should broaden tactics, increase potency or improve action economy; they should not be necessary merely to make an already-approved intrinsic feature function.
6. **Owner acceptance has been received;** activation still requires source-neutral native implementation, regression testing and appropriate validation. Do not merge to `main` without separate authorization. Seven options remain published **non-SRD references** and must be independently converted/rights-reviewed before any native gameplay data; no 3.5e catalog or Star Chain/Seeker expansion.

## Recommended resolution: Blade (five Invocations)

| Invocation | Proposed native disposition | Important details |
| --- | --- | --- |
| **Thirsting Blade** | **Retire from selectable native Invocations**; preserve name, 2014/2024 texts and source projections | The already-approved **Warlock 5 Blade Pact** grants exactly two Pact-weapon attacks when the Warlock takes the Attack action. Do not award an additional attack, a free replacement Invocation choice, or a selectable no-effect option. The native base feature may show a historical alias/reference to Thirsting Blade. |
| **Devouring Blade** | **Retain elective Invocation at Warlock 12**, require **Blade Pact and its intrinsic level-5 extra attack** | Change prerequisite from the obsolete **Thirsting Blade Invocation** to the **native Blade's existing two-attack feature**; choosing Devouring Blade raises the total to **three Pact-weapon attacks** for that Attack action. These are not three extra attacks, do not accumulate from duplicate purchases, and do not stack with another feature granting the same attack count. In any future 3.x iterative attack conversion, decide cross-system stacking separately. |
| **Lifedrinker** | **Adopt 2024's version at Warlock 9** as **one native Invocation**; retain the 2014 version as source history | On a Pact-weapon hit, **once per turn** deal an additional **1d6 Necrotic/Psychic/Radiant** (damage type chosen as supplied by the 2024 option); optionally expend **one Hit Die** to regain rolled Hit Die + Constitution modifier HP (at least 1). **Do not add 2014 Charisma Necrotic on every hit on top.** Its healing is separate from Fiend Infernal Retaliation; does not create unlimited fast healing or replenish the expended die. No new once-per-rest cap invented. |
| **Eldritch Smite** | **Adopt 2024 source version without redesign**; optional at **Warlock 5** and Blade Pact | Once per turn on a hit with the Pact weapon, optionally **spend a Pact Magic spell slot** for extra **Force dice scaling with slot level** and optionally inflict **Prone on Huge-or-smaller target**. Not free, not a new slot source. Resolve attack/critical bonus dice through ordinary unified damage/critical rules later rather than adding a special competing Warlock critical rule. |
| **Improved Pact Weapon** (*Xanathar's*, non-SRD) | **Keep as optional Invocation**; remove its already-automatic *focus* sub-benefit **only** from the native effective grant | Its remaining two benefits are **(a)** conditional **+1 to Pact-weapon attack and damage** when that weapon has no existing numerical magical attack/damage enhancement, **not an additive +1 over an existing enhancement**, and **(b)** permit conjuring a **shortbow, longbow, light crossbow or heavy crossbow** as the Pact weapon. This explicitly extends conjured ranged **weapon forms**, while existing mundane melee weapon-bond approval remains unchanged; do **not** silently grant broad ordinary ranged-weapon bonding. Apply ammunition and normal ranged attack rules. The weapon's focus status is already intrinsic to Blade; do not grant it twice. Keep research-only until separately approved as native content. |

**Blade overall:** This preserves separate **burst (Smite), sustained damage/self-heal (Lifedrinker), elite three-attack martial focus (Devouring), and ranged/accuracy versatility (Improved)** without a wasted mandatory Thirsting slot. The 2014 vs 2024 Lifedrinker difference is settled by **choosing newer rather than stacking**.

## Recommended resolution: Chain (four Invocations)

### Chain basic familiar action economy — the one substantive integration rule

**Propose the following adaptation of the existing Pact of the Chain command, not a new Invocation:**

1. Pact familiar is a **controlled creature on the Warlock's initiative**, with an individually tracked turn, action, movement, and reaction, following the owner's standing controlled-creature initiative rule. Turns may be ordered together subject to the existing shared initiative rules.
2. **Without Investment:** When the Warlock takes the **Attack action**, they may **forgo one of their own attacks** to direct their Pact familiar to take **one Attack action on its turn in that shared initiative round**. This replaces the 2014/2024 *single familiar Reaction attack* presentation for the unified native controlled-creature action engine. The familiar can still perform its normal non-Attack tasks when no attack command occurs.
3. **Warlock level 5:** Whenever the familiar takes its one permitted Attack action, the already approved intrinsic **extra familiar attack** gives **two attacks in that single action**. The extra attack does **not** create another Attack action; merely making a familiar Reaction attack outside this action would **not automatically produce two attacks**. **No Invocation is required** for this two-attack baseline, only a valid base Pact command.
4. **With Investment:** The Warlock can **alternatively** spend a **Bonus Action** to order the familiar's Attack action, keeping the Warlock's Action free for a spell or weapon action. **One familiar Attack action maximum per own turn/initiative round from the two command pathways**, not one from each. The optional Invocation thus buys **flexible combat action economy** plus its other familiar upgrades.
5. The ordinary rules for summoning, independent movement, Attack permissions, incapacitation, familiar spellcasting and specific monster statblocks still apply. Do not import a blanket action-grant into all controlled summons or Chain-independent familiars.

This *is a meaningful mechanical conversion*, not a mere text correction. It merits owner review independently from the easier Invocation wording fixes, since some tables may prefer a native Bonus-Action command baseline or autonomous familiar action. It is proposed specifically to make the approved level-5 companion extra attack **real without purchasing Investment** while leaving Investment's Quick Attack upgrade valuable.

### Four Chain Invocations

| Invocation | Proposed native disposition | Important details |
| --- | --- | --- |
| **Voice of the Chain Master** | **Retain 2014 elective utility unchanged in substance** | Allow same-plane familiar telepathy, remote sensory perception and speaking through familiar in Warlock's own voice. Treat the familiar's base sensory-sharing procedure as the foundation; do not duplicate its ordinary hearing/seeing benefit as a new creature or a free extra action. No additional Warlock-level gate beyond Chain is proposed. |
| **Chains of Carceri** | **Retain 2014 elective at Warlock 15** | Slotless *Hold Monster* only on **Celestial, Fiend or Elemental** creature types. **Same individual target cannot be affected by this Invocation again until the Warlock completes a Long Rest**, even if an earlier attempt failed; a different qualifying target remains eligible. Use native creature typing and control-condition engine, not a universal one-cast-per-day limit. |
| **Investment of the Chain Master** | **Retain 2024 Warlock-5 elective enhancements**, with Quick Attack clarified as the **alternate** command pathway described above | Bonus Action Attack command, choice of **40-ft Fly or Swim** speed where granted, **Necrotic or Radiant** replacement for eligible familiar physical damage, Warlock spell save DC for familiar-forced saves, and Warlock Reaction granting familiar Resistance against an instance of damage. Preserve familiar base mobility where already superior; **do not add a second familiar Attack action or automatic free Invocation selection**. The 2020 Tasha's option has a related but different magical-attack wording; archive that source variant, use the imported 2024 SRD variant for native proposal. |
| **Gift of the Ever-Living Ones** (*Xanathar's*, non-SRD) | **Retain elective support Invocation at no extra level gate** | While the **Pact familiar is within 100 feet**, **maximize Hit Point restoration dice rolled to heal the Warlock**. **Do not** maximize spell damage, Temp HP, the companion's own recovery or **flat/no-dice fast healing** from approved Fiend Infernal Retaliation. It may maximize healing dice from other valid healing sources (unless already maximized by an existing rule, in which case it has no extra benefit). Preserve as research until non-SRD native conversion approved. |

## Recommended resolution: Tome (four Invocations)

| Invocation | Proposed native disposition | Important details |
| --- | --- | --- |
| **Book of Ancient Secrets** | **Keep elective Invocation as *advanced ritual collection***; remove duplicated two starter rituals from its native **effective** grant | The basic Tome already has **two level-1 rituals** from its approved 2024-base reconciliation direction. On selecting this Invocation, permit **finding and permanently transcribing additional Ritual spells** of up to **half the Warlock level (rounded up)**, using the historical **2 hours and 50 gp per ritual-spell level**. These become rituals accessible through the active Book of Shadows, not automatically learned spells or free nonritual casts. Keep accrued ritual records in the Pact's archive and restore them to a replacement book without refilling any other resources; **never receive a second free pair of level-1 rituals**. An Invocation choice is purchasing expanded ritual collection, not elementary ritual capability. |
| **Gift of the Protectors** | **Retain elective 2024 version at Warlock 9** | Book page accepts consented names, **number of names = CHA modifier (minimum 1) in imported 2024 SRD**, not proficiency bonus from older Tasha's printing. Once per Long Rest, the first named creature that would reach 0 HP **without dying outright** reaches 1 HP instead; this is **one shared trigger for the page**, not one per name. Name removal uses the 2024 Magic action. Keep base book and protection pages distinct from Talisman's Shared Ward. |
| **Aspect of the Moon** (*Xanathar's*, non-SRD) | **Retain optional Tome utility at no added level gate** | Warlock **does not need sleep and cannot be forced to sleep**; still must complete the normal **Long Rest** to recover rest-limited resources. It may take the rest as source-permitted **light activity/watchfulness**; no extra Long Rests, automatic recovery, or immunity to all Exhaustion/rest effects. |
| **Far Scribe** (*Tasha's*, non-SRD) | **Retain elective Tome communication at Warlock 5** | Book page accepts up to **Warlock proficiency bonus** willing recorded correspondents. Warlock writes a message for a named person to send with *Sending* **without a slot**, using its required source casting/action procedure; response is written into the book and writing fades after the source-defined duration. Name enrollment/removal follows its source action requirements. Do not add this to Tome's three automatic level-5 free spells or turn it into a general learned-spell grant. |

**Tome cross-source caution:** Published *Tasha's* **Gift of the Protectors** uses proficiency-bonus name capacity, while the imported **2024 SRD version uses Charisma modifier**. This proposal deliberately selects the **2024 SRD** count for **Gift of the Protectors**, and the separately published proficiency-bonus limit for **Far Scribe**. The two similarly themed pages must not share an accidental resource ledger or name capacity; neither feature's source is silently misquoted.

**Book replacement:** Restoring paid inscriptions through a new Book is a **native QoL decision proposed here**, not a property already verified as explicit in the imported 2014 source. Approving it would preserve investment without creating free rituals, because their original acquisition/cost histories persist and no additional spells are added.

## Recommended resolution: Talisman (three Invocations)

All three **already approved to remain optional** under the owner's Talisman decision. Their separate action/resources are the straightforward fixes; **none changes Talisman's approved level-1 or level-5 intrinsic mechanic**.

| Invocation | Proposed native disposition | Important details |
| --- | --- | --- |
| **Rebuke of the Talisman** | **Retain published 2020 option** with Reaction conflict resolved | When an attacker **visible to the Warlock within 30 feet of the Warlock** hits the talisman wearer, the Warlock may spend their Reaction to inflict **psychic damage equal to Warlock proficiency bonus** and push the attacker **up to 10 feet away from the wearer**. Warlock may be wearer. **Shared Ward** triggers from damage to a wearer within **60 feet** and consumes Warlock Reaction **plus one shared Talisman charge**. An attack may qualify for both, but **the Warlock picks which Reaction to use**; Rebuke does **not consume an intrinsic Talisman charge**. Push is resolved according to native hit/damage/reaction timing and does not create an additional interrupted attack. |
| **Protection of the Talisman** | **Retain published 2020 Warlock-7 elective** | When wearer fails a **saving throw**, add **1d4**, potentially succeeding. Has **its own Warlock-PB uses per Long Rest**; **does not spend** the native Talisman Fortunate Correction/Shared Ward charge pool. No stacking with Fortunate Correction because it affects a **saving throw**, not an ability check; native general bonus-stacking rules otherwise apply. |
| **Bond of the Talisman** | **Retain published 2020 Warlock-12 elective** | If **another creature** wears the talisman on the **same plane**, either the Warlock or wearer can spend **their own Action** to teleport to the nearest unoccupied space by the other. **Both directions consume one shared Bond Invocation use pool** of **Warlock-PB uses per Long Rest**, separate from native Talisman charges and from Protection's uses. No extra teleportation, free movement, or duplicated Bond uses from transferring the amulet. |

## Decision register: 16 specific acceptance targets

| # | Feature | Decision type | Approval consequence |
| ---: | --- | --- | --- |
| 1 | Thirsting Blade | **retire native elective** | Historical variants stay intact; Pact's intrinsic two attacks remain |
| 2 | Devouring Blade | **rewrite stale prerequisite** | Elective 3 total attacks at Warlock 12 |
| 3 | Lifedrinker | **select 2024 variant** | No duplicate 2014 per-hit CHA rider |
| 4 | Eldritch Smite | **adopt 2024 source** | Elective Pact Magic-slot damage, not free |
| 5 | Improved Pact Weapon | **deduplicate focus component** | Conditional +1/ranged conjuration remain elective, non-SRD conversion later |
| 6 | Voice of the Chain Master | **retain 2014** | Same-plane familiar communication/senses/speech |
| 7 | Chains of Carceri | **retain 2014** | Individual target Long Rest restriction |
| 8 | Investment of the Chain Master | **retain 2024 with clarified command** | Optional Bonus Action command; no multiple familiar Attack actions |
| 9 | Gift of the Ever-Living Ones | **retain source** | Maximizes Warlock healing dice; not flat Fiend healing |
| 10 | Book of Ancient Secrets | **remove duplicate starter rituals** | Pay for additional found-ritual collection; persistent purchased transcriptions |
| 11 | Gift of the Protectors | **retain 2024** | CHA-limited names and one shared page rescue/LR |
| 12 | Aspect of the Moon | **retain source** | No sleep required, Long Rests still required |
| 13 | Far Scribe | **retain source** | PB-limited correspondent list, separate Sending option |
| 14 | Rebuke of the Talisman | **retain source and Reaction exclusion** | Warlock chooses Rebuke or Shared Ward per applicable damage event |
| 15 | Protection of the Talisman | **retain source and separate uses** | Separate PB/LR savings pool |
| 16 | Bond of the Talisman | **retain source and one shared directional pool** | Separate PB/LR teleport uses |

**Cross-feature proposed (not an additional Invocation):** **Chain baseline** familiar Attack-action command through Warlock Attack-action sacrifice, with Investment Bonus-Action alternate route, shared initiative and at most one commanded familiar Attack action per turn. This is the highest-impact owner decision in the set.

## Consequences if owner approves

- **16 options have dispositions**, but **15 remain possible selectable Invocations** and **Thirsting Blade is historical-only**. The seven non-SRD options are still *candidate source references* and require appropriate independent native mechanical conversion before becoming executable. The total 15 is a planned **catalog disposition**, **not** an increase to the current SRD-first runtime source budget.
- Existing **Pact first-choice entitlement** and **all four intrinsic level-5 benefits** unchanged.
- No new source-license assumptions, no 3.5e catalog import, and **no Invocation selections/level progression schedule adopted**.
- Once approvals are explicit, implement one source-neutral native effect per adopted Invocation and enforce all source/Pact/Patron eligibility gates; preserve historical sources and all original candidate definitions for audits.
- Tests should verify: 2 vs 3 attack totals; no duplicate Thirsting selection; chain familiar one attack action with one/two attacks by level; Investment Bonus Action and simultaneous Warlock spellcasting; ritual inscription persistence and cost; Lifedrinker use rate and Hit Dice; 2024 CHA vs Tasha PB Tome name limits; independent Talisman resource ledgers; mutually exclusive Reactions; Pact suspension/deactivation on severance.

## References

- Original first-pass Pact-specific source-backed catalog: `data/reference/warlock-pact-specific-invocations.json`.
- Codification report: `docs/rewrite/warlock-pact-specific-invocation-codification.md`.
- Imported 2014 and 2024 features (including all nine Pact-specific SRD variants): `data/reference/srd/structured/optionalfeatures.json`, **source-preserved**.
- Approved four-Pact rules: `docs/rewrite/warlock-pact-pillar-reconciliation.md`, `docs/rewrite/warlock-talisman-pact-strengthening-proposal.md`.
- Fiend/Invocation boundary: `docs/rewrite/warlock-fiend-patron-consolidated-decision.md`.
- Other published references: *Xanathar's Guide to Everything* (2017) and *Tasha's Cauldron of Everything* (2020), source metadata only.
- 2024 basic Warlock source: https://www.dndbeyond.com/classes/2190885-warlock

**Owner approval received (2026-10-25): all 16 dispositions, including the Chain command adaptation, are approved as native design requirements.** Source texts remain archived; executable implementation, full regression testing, and any actual deployment still remain to be completed on `feature/rules-core-rewrite`. No Invocation selection-count or 3.5e catalog decision is implied.
