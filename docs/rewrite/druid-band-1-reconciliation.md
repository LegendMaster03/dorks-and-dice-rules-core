# Druid — Band 1 base-class reconciliation (approved and implemented)

**Status:** Owner-approved source-neutral native class and base-class feature records implemented 2026-10-20 on `feature/rules-core-rewrite`. **Scope:** levels 1–20; no Druid Circles, subclasses, prestige classes, or automatic Epic Boons. Gameplay consumer runtime is not yet fully implemented.

## Sources

- 3.5e SRD original source staged at `data/reference/3x/band1-baseclasses/srd35/classes-i.md` on `feature/band1-baseclass-srd-sources`; historical animal companion progression, Wild Shape, Nature Sense, Wild Empathy, Woodland Stride, Trackless Step, Resist Nature's Lure, Venom Immunity, A Thousand Faces, Timeless Body.
- SRD 5.1/5.2.1 staged at `data/reference/srd/structured/class/class-druid.json`; modern Wild Shape, Wild Companion, Wild Resurgence, Primal Order, Elemental Fury, Beast Spells, and Archdruid.
- 3.0 was consulted during comparison; the authoritative persisted historical mechanical data here is the staged 3.5e source. Dorks & Dice's owner-specific changes are explicitly marked, not attributed to SRDs.

## Native base-class capabilities

Druid has d8 HD, Wisdom full 9-level spellcasting, modern Intelligence/Wisdom saving throw proficiencies, 3.x 3/4 BAB and strong Fortitude/Will saves. Cantrips known are 2 at level 1, 3 at level 4, and 4 from level 10 (plus an extra cantrip **only if Magician is chosen**). Access to all eligible Druid spells of castable spell level is available without daily preparation, using spell slots or spell points; no second historical spell-slot pool or spontaneous-preparation-conversion subsystem is created.

Primal Order offers **one** of Magician/Warden at level 1. Elemental Fury offers **one** of Potent Spellcasting/Primal Strike at level 7, with only the selected option upgraded at level 15. Ranked class skills and the historical 4 + Intelligence modifier skill points (x4 at first level) are preserved distinctly from modern skill proficiencies. Druidic includes secret language/messages and extra access to Speak with Animals without introducing preparation.

Retained historical features: Nature Sense (+2 Nature/Survival), Wild Empathy, Woodland Stride (natural undergrowth), Trackless Step, Resist Nature's Lure (+4 against fey spell-like abilities), Venom Immunity, A Thousand Faces, and historical aging resistance at level 15. Longevity is separately added at level 18 (10 years pass per one year of aging); Archdruid's matching longevity does **not** stack.

## Wild Shape: approved HP house rule

**Use the transformed creature's separate Hit Point pool, as in the 2014-style transformation.** Keep the Druid's regular HP unchanged while transformed. When form HP reaches 0, revert immediately to normal shape and take **no overflow damage to normal HP**. Each new eligible transformation uses a new form-HP pool. Do **not** also give the 2024 Wild Shape temporary-HP grant.

**Source accuracy:** Published 2014 5e says damage exceeding a form's remaining HP *does carry over* to the Druid. **No overflow** is therefore an explicit **Dorks & Dice owner-approved house rule**, not a verbatim 2014 rule.

Other modern form transformation procedures remain: use a Bonus Action to transform and revert; duration floor(Druid level / 2) hours; preserve personality, mental attributes, memories, proficient skills, class features and concentration on preexisting spells; use the chosen creature's physical form capabilities and appropriate equipment handling. Do not gain creature legendary/lair actions. Spellcasting while transformed remains disallowed until Beast Spells at level 18; further normal casting/component improvements apply at Archdruid level 20.

### Form eligibility (size is ordinary, not optional)

A form is eligible under **either** independent source-backed avenue:

1. **Modern known Beast path:** Start at level 2 with 4 known forms, CR 1/4, no flight; at level 4, 6 known forms and CR 1/2; at level 8, 8 known forms, CR 1 and flight enabled. 5.5e permits swimming forms earlier than 2014. One modern known form can change at each Long Rest. Size and stat-block anatomy remain fundamental properties, but this modern path does not impose the 3.5e level-5 size gate on a valid modern form.
2. **Historical familiar-form path:** Starting level 5, add familiar animal forms subject to historical creature type, Hit Dice no more than Druid level, and **Small/Medium** size. **Large** at level 8, **Tiny** at 11, qualifying **plant creatures** at 12, **Huge** at 15, eligible **elementals** at 16 (Small, Medium or Large), and **Huge elementals** at 20. These historical forms need not occupy a modern known-form slot or satisfy modern CR caps, since doing so would silently erase the retained source-granted forms. A form must actually have an eligible creature stat block; an arbitrary inanimate plant is not a plant creature.

Both avenues feed **the same** Wild Shape action, HP treatment, attributes, duration, equipment, spellcasting and native form statistics; this is **one transformation engine**, not an advanced opt-in Wild Shape module.

### Uses and level-20 conversion

| Druid class level | Finite Wild Shape charges | Historical elemental quota per Long Rest | Transformation permission |
| --- | ---: | ---: | --- |
| 1 | 0 | 0 | Not yet unlocked |
| 2–5 | 2 | 0 | Spend one charge |
| 6–15 | 3 | 0 | Spend one charge |
| 16–17 | 3 | 1 | Spend one charge; elemental also consumes a quota |
| 18–19 | 4 | 2 | Spend one charge; elemental also consumes a quota |
| 20 | 4 tracked finite charges | 3 | **Unlimited transformations**, but elemental quota still applies |

One finite use returns on a Short Rest; all return on a Long Rest. Historical 3.5e animal Wild Shape daily-use milestones are retained as source provenance, **not added as a second resource or extra modern charges**.

Wild Resurgence at level 5 permits expending one spell slot (native point equivalent when applicable) to recover one finite Wild Shape use when empty, once per turn, and converting one finite charge into a level-1 spell slot once per Long Rest. Archdruid's 2024 Nature Magician conversion consumes only actually **tracked finite charges**, producing a slot of two levels per charge, once per Long Rest. The level-20 unlimited **permission to transform** is not a refillable or expendable resource for Nature Magician, Wild Resurgence or Wild Companion. Archdruid also restores one finite charge upon rolling initiative when empty.

## Persistent Animal Companion and Wild Companion

The Druid has **one persistent 3.5e Animal Companion from level 1**, not a summoned beast that vanishes after rest. Its rank table is preserved as native data with the following milestones:

| Effective Druid companion levels | Bonus HD | Natural armor | Str/Dex | Bonus tricks | New abilities |
| --- | ---: | ---: | ---: | ---: | --- |
| 1–2 | 0 | 0 | 0 | 1 | Link, Share Spells |
| 3–5 | 2 | 2 | +1 | 2 | Evasion |
| 6–8 | 4 | 4 | +2 | 3 | Devotion |
| 9–11 | 6 | 6 | +3 | 4 | Multiattack |
| 12–14 | 8 | 8 | +4 | 5 | — |
| 15–17 | 10 | 10 | +5 | 6 | Improved Evasion |
| 18–20 | 12 | 12 | +6 | 7 | — |

Higher-level alternative animals unlock at Druid levels 4/7/10/13/16, with effective companion level penalties 3/6/9/12/15. Do not apply the full-level progression to a stronger alternative as if the penalty were absent. Environmental choices apply to eligible aquatic campaigns. A dismissed or lost companion is replaced through the historical **24 uninterrupted hour** ceremony. Companion spell sharing, commands, attack progression and control use the original feature constraints and shared native creature rules.

**Wild Companion** at level 2 is a **separate temporary Fey familiar** summoned via Find Familiar, expending a spell slot or one **finite** Wild Shape charge. It disappears at the next Long Rest and **may coexist** with the persistent companion. Both act on the controller's initiative under the native controlled-creature initiative convention; companion presence does not create free extra turns or attacks beyond their respective rules.

## Metal equipment and identity

The 3.5e nonmetal-armor/shield restriction is not mandatory in native. A Druid wearing otherwise proficient metal armor or shields does **not** lose magic or class features. Nonmetal preferences are optional roleplay traditions. There is no native alignment requirement or automatic loss of Druid abilities for alignment, devotion or teaching Druidic.

## Non-grants and runtime boundary

The **nine** historical circle/subclass placeholders and level-19 Epic Boon candidate remain staged, not active and not granted from this base class. Level 19 is an ordinary ASI/eligible feat; Epic Boons require total character level 21 or higher.

The class and **22** modern base-class feature records are reconciled with **12** historical inline feature records. Native data validation and regression tests are provided in `tests/test_druid_band1.py`. The generic consumer still needs execution support for form-HP pools, companion progression, form eligibility, resource exchange and commands before these declarative mechanics can be used in live gameplay.
