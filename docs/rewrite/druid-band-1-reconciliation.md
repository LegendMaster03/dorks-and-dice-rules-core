# Druid — Band 1 base-class reconciliation (working record)

**Scope:** Druid class levels 1–20, drawing on the 3.0, 3.5e, SRD 5.1 and SRD 5.2.1 baselines. **Status:** two principles approved by the owner on 2026-10-19; the remaining procedures are proposals awaiting a ruling. No native Druid class or feature records are activated in this document. Druid Circles, subclasses, prestige classes and Band 2 are excluded.

## Approved: creature size is ordinary form eligibility

The physical **size of a creature is a fundamental property** of Wild Shape and similar transformations, not an optional advanced mechanic. A player's permitted form is checked against the actual stat block and the base-class permissions that apply at the character's level.

The 3.5e source establishes animal Wild Shape Small/Medium at Druid 5, Large at 8, Tiny at 11 and Huge at 15; earlier modern Wild Shape unlocks at Druid 2, with CR and movement-mode gates. Later 3.5e source additions include plant creatures at 12 and elemental creatures at 16, with elemental size improvements at 20. Track ordinary **size/type/movement/CR/Hit Dice** eligibility as first-class data, not as an advanced transformation toggle. The exact composition of historical HD-based and modern CR-based restrictions is **still unresolved**: do not silently apply both caps or remove either source-derived capability.

## Approved: persistent 3.5e Animal Companion

The native Druid gains **one persistent base-class Animal Companion** at level 1 under the 3.5e model. It is a continuing creature, not a short-rest or long-rest summon and not a subclass feature. It progresses with effective Druid companion level according to the original 3.5e table:

| Druid effective companion level | Bonus Hit Dice | Natural armor | Strength/Dexterity adjustment | Bonus tricks | Additional abilities |
| --- | ---: | ---: | ---: | ---: | --- |
| 1–2 | +0 | +0 | +0 | 1 | Link; Share Spells |
| 3–5 | +2 | +2 | +1 | 2 | Evasion |
| 6–8 | +4 | +4 | +2 | 3 | Devotion |
| 9–11 | +6 | +6 | +3 | 4 | Multiattack |
| 12–14 | +8 | +8 | +4 | 5 | — |
| 15–17 | +10 | +10 | +5 | 6 | Improved Evasion |
| 18–20 | +12 | +12 | +6 | 7 | — |

Alternative companion forms are permitted at their 3.5e class-level gates, with effective-level adjustments, not automatically scaled as full-level companions. The historical 24-hour ceremony governs release/replacement of a dismissed or lost companion. The animal follows shared creature-control and initiative rules, with its historical Link, Share Spells, Evasion, Devotion, Multiattack and Improved Evasion progression preserved.

**Wild Companion is a different ability:** the 5.5e nature-spirit familiar at level 2, summoned using a spell slot or Wild Shape use. It must not replace, delete or make the persistent Animal Companion expire on a rest. Whether both may be active at once and their action economy/command interactions remain a separate rule question.

## Remaining base-class decisions

1. **Wild Shape HP and form statistics:** 2014 replacement HP/overflow versus 2024 retention of Druid HP plus temporary HP; these cannot silently stack. One common engine may host profile choices, but the native default still needs a ruling.
2. **Wild Shape uses, recovery and capstone:** modern 5.5e uses/Short Rest/Long Rest, historical expansion and special elemental uses, and level-20 unlimited transformation permission. If unlimited forms are retained, Wild Shape-to-spell-slot conversion must never become an unlimited casting resource.
3. **Form gate composition:** how historically permitted plant, elemental, Huge/Tiny and dire animal forms combine with modern CR, HD, learned form and movement rules, while treating creature size as ordinary eligibility. The later subclass pass cannot be required merely to unlock source-granted base-class forms.
4. **Wild Companion interaction:** the temporary 5.5e familiar is distinct from the approved persistent companion; decide simultaneous control and resource/action implications.
5. **Armor tradition:** whether historical nonmetal armor/shield restrictions have mandatory mechanical effects or remain roleplay/optional commitments under native rules.

## Compatible base-class contributions awaiting activation

Druidic language/secret messages and Speak with Animals; 3.x Nature Sense, Wild Empathy, Woodland Stride, Trackless Step, Resist Nature's Lure, Venom Immunity, A Thousand Faces and Timeless Body; modern Primal Order (choose one of Magician/Warden), Wild Companion, Wild Resurgence, Elemental Fury (choose Potent Spellcasting/Primal Strike), Improved Elemental Fury, Beast Spells, and Archdruid. Reuse Cleric's full-list, Wisdom-based spell access (no daily preparation, slots or points), full modern spell-level progression, and shared Level Band/feat rules. Druid-specific details require feature-by-feature conversion and tests.

**Sources:** 3.5e staged `data/reference/3x/band1-baseclasses/srd35/classes-i.md` on branch `feature/band1-baseclass-srd-sources`, and modern `data/reference/srd/structured/class/class-druid.json` on the rewrite branch. Original 3.0 remains for cross-check; no unverified rule is approved from that source.

**Next:** resolve the remaining questions before promoting `data/native/classes/base/druid.json` and its relevant base-class features. Subclass records remain untouched.
