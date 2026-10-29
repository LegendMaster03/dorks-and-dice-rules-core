# Cleric — Band 1 native reconciliation (implemented)

**Approval:** User authorized the previously proposed full-list spell access, unified Channel Divinity effects, and dual Divine Intervention on 2026-10-19. **Scope:** Cleric class levels 1–20 only; no domains, subclasses, prestige classes or Band 2 rules.

## Source and policy

- Original 3.0 Cleric: https://www.dragon.ee/30srd/cleric.htm (independent 3.0 SRD mirror).
- 3.5 SRD: `data/reference/3x/band1-baseclasses/srd35/classes-i.md` from the separately staged `feature/band1-baseclass-srd-sources` branch (OGL provenance; source pack is not silently merged).
- 5e SRD 5.1 and 5.5e SRD 5.2.1: `data/reference/srd/structured/class/class-cleric.json`.
- Dorks & Dice standard: native common spell slots/points and no daily spell preparation; class alignment and permanent attribute-score restrictions removed; Epic Boons eligible only starting **total-character** level 21. A Cleric may be devoted to a deity, pantheon, principle, or other eligible spiritual conviction without mandatory alignment. Class-base mechanics do not force a particular domain or subclass.

## Native progression and grants

`data/native/classes/base/cleric.json` is the source-neutral authority. It contains levels 1–20, modern full nine-spell-level slot progression, at-will Cleric cantrips following SRD 5.x (3/4/5 at class levels 1/4/10), historical 3/4 BAB with optional iterative procedure, strong Fortitude/Will and poor Reflex 3.x saves, and modern Wisdom/Charisma save proficiencies.

| Level | Unconditional base-class grants and improvements (excluding subclasses/domains) |
| --- | --- |
| 1 | Spellcasting, Divine Order (choose **one** Protector/Thaumaturge), historical Turn/Rebuke/Command/Bolster/Dispel options, first unified Channel Divinity access |
| 2 | Channel Divinity (modern effects become available), Turn Undead (modern Wisdom-save), Divine Spark |
| 3 | Standard progression only |
| 4 | ASI/eligible feat; Cleric cantrips known 4 |
| 5 | Sear Undead, Destroy Undead CR 1/2 |
| 6 | Channel Divinity pool increases to 3 |
| 7 | Blessed Strikes (**choose one** of Divine Strike/Potent Spellcasting), Divine Spark improves to 2d8 |
| 8 | ASI/eligible feat; Destroy Undead CR 1 |
| 9 | Standard progression only |
| 10 | Divine Intervention (Direct Intervention and Divine Supplication), Cleric cantrips known 5 |
| 11 | Destroy Undead CR 2 |
| 12 | ASI/eligible feat |
| 13 | Divine Spark improves to 3d8 |
| 14 | Improved Blessed Strikes applies **only** to selected L7 option; Destroy Undead CR 3 |
| 15 | Standard progression only |
| 16 | ASI/eligible feat |
| 17 | Destroy Undead CR 4 |
| 18 | Channel Divinity pool increases to 4; Divine Spark improves to 4d8 |
| 19 | ASI/eligible ordinary feat (Epic Boon only if total character level 21+) |
| 20 | Divine Intervention automatic success for **Supplication**; Greater Divine Intervention unlocks **Wish** for Direct Intervention |

Protector and Thaumaturge, Divine Strike and Potent Spellcasting are defined in their own reconciled feature records, **but are choice-only entitlements** and not all granted unconditionally.

## Spellcasting

The Cleric has access to the **entire eligible Cleric spell list** for spell levels they are able to cast, using the approved spell slots or spell points alternative. Spell preparation is removed; no Wisdom-modifier-plus-level or separate 2024 prepared-list limit, finite leveled spells-known limit, or daily replacement routine is imposed. Spell attack/DC remain Wisdom-based. Eligible spells still require their normal components, targets, casting times and resources. Ritual casting uses the shared native rules. Historical spontaneous prepared-spell conversions into cure/inflict spells grant nothing additional because those spells are already accessible. The old **domain-only** additional slots and domain-list effects are **not part of the base class** while domain reconciliation is deferred.

## Channel Divinity — shared resource

**Approved resource reconciliation:** One `cleric.channel-divinity` pool, **two uses from level 1**, three at level 6, four at level 18; recover one use on a Short Rest and all on a Long Rest. The 3.x pool of `3 + Charisma modifier` daily turning attempts is preserved in source provenance but **does not form a second resource** or increase the native total. Level-one resource availability is necessary to preserve 3.x level-one Turn/Rebuke functionality; modern Divine Spark and save-based Turn Undead unlock at L2. Channel Divinity's save DC is the Cleric spell save DC.

**Modern procedure:** Present sacred focus and apply a Wisdom save to chosen undead within 30 feet; failing imposes Frightened and Incapacitated for up to 1 minute while the undead moves away. The effect ends on other damage, Cleric incapacity/death; Sear Undead damage **does not** end this effect. Sear adds Wisdom modifier (minimum one) d8 radiant damage on failed saves starting at L5. An undead creature failing this save is instead destroyed when its CR meets the old Destroy Undead threshold (CR 1/2 at L5, then 1 at L8, 2 at L11, 3 at L14, 4 at L17). Do not double-apply Sear damage if immediately destroyed.

**Historical 3.x procedure, alternative to modern save:** A 60-foot Charisma turning check sets maximum undead HD using the 3.x lookup table, and `2d6 + Cleric class level + Charisma modifier` sets total undead HD affected. Knowledge (religion) ranks 5+ add +2 to that check, and the nearest eligible undead are evaluated first. The traditional Turn option drives undead away, while Rebuke causes them to cower for 10 rounds. At sufficiently high Cleric level (at least twice target HD), historical turning may destroy and rebuking may Command, subject to total commanded undead HD not exceeding Cleric level. Bolster and dispel historical turning remain conditional options, likewise costing one shared use. **No moral/alignment prerequisite** restricts any of these effects. Historical check-mode destruction is distinct from the modern saving-throw/CR-based destruction; do not combine the two resolution formulas for a single effect.

The two procedures are alternatives, not sequential automatic effects of one use, and they do not grant two independent Channel Divinity pools.

## Divine Intervention — two methods under one feature

- **Direct Intervention (5.5e, L10):** One Magic Action to cast an eligible Cleric spell of spell level at most 5 without expending a slot/point or providing material components; the chosen spell must not have Reaction casting time. Returns on a Long Rest. At L20 Greater Divine Intervention may choose **Wish**, in which case this direct procedure is unavailable until after **2d4 Long Rests**.
- **Divine Supplication (5e, L10):** One Action requesting the DM's divine help, succeeding on `d100 <= Cleric class level` through L19. Successful supplication has a **7-day** cooldown; failed supplication returns on a Long Rest. At L20 the supplication succeeds automatically, preserving the seven-day post-success delay.
- The two selectable methods retain their **own** cooldown and resource rules. Each activation uses one method, not both. A deity is not required; the character's chosen divine power or spiritual tradition is sufficient where narrative adjudication applies.

## Deferred boundaries

**Ten** staged source candidates (older Turn Undead variant, historical domain-feature placeholders, subclass placeholders, and automatic level-19 Epic Boon candidate) stay non-executable and do not appear in `progression[].featureGrants`. Domain spell grants, domain selection/count/timing, and class-specific domain channeling effects are deliberately deferred. A small class-data schema describes these rules; it is **not proof every gameplay consumer can already execute every spell, undead condition, or intervention**. Keep release/access behind established gates and add runtime acceptance testing separately.

Regressions are covered by `tests/test_cleric_band1.py`; source coverage/count bookkeeping is updated without deleting original SRD references.
