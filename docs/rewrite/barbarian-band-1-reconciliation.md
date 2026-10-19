# Barbarian — Band 1 base-class reconciliation (working draft)

**Scope:** Class levels 1–20 only. No subclasses, prestige classes, or Band 2 progression. One native Dorks & Dice base class, not parallel editions. **Status: review draft; not authorization to activate native candidate definitions.**

## Evidence and established decisions

- **3.0:** [Barbarian original SRD mirror](https://www.dragon.ee/30srd/barbarian.htm). Verifiable differences include Rage/Greater Rage at levels 1/15, DR at 11/14/17/20, trap protection at 10/13/16/19, and fatigue removal at 20. This is a preserved mirror; it is not yet a locally imported complete 3.0 source pack.
- **3.5:** [Classes I Markdown source](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/feature/band1-baseclass-srd-sources/data/reference/3x/band1-baseclasses/srd35/classes-i.md), copied with [OGL notice](https://github.com/LegendMaster03/dorks-and-dice-rules-core/blob/feature/band1-baseclass-srd-sources/data/reference/3x/band1-baseclasses/srd35/legal.md) in a separate unmerged reference branch. Original documents: https://archive.org/details/dnd35srd.
- **5e/5.5e:** `data/reference/srd/structured/class/class-barbarian.json` (SRD51, SRD52) and accompanying staged native class features.
- **Authoritative gameplay policy:** `docs/rewrite/native-rules-design-specification.md`. No automatic edition priority. Compatible features remain, duplicates merge, quantitative/procedural conflicts require an individual ruling.
- **Already approved:** distinct **Battle Rage** and **Primal Rage**, with **Greater** and **Mighty** names for both; one shared Rage resource; no alignment or illiteracy requirements; no permanent base-class ability-score ceilings; no Epic Boon eligibility until total character Band 2; established critical-escalation, movement, attack, defense, skill, and fatigue systems.

## Proposed single Band 1 progression

The table below applies the existing additive-policy conclusions and lists the source-supported acquisition levels. It is a **class draft**, not twelve parallel source tables. Base Attack Bonus and Fortitude/Reflex/Will progression are retained through the established 3.x/native progression mechanics rather than conflated with the 5.x Strength/Constitution saving-throw proficiency grants.

| Class level | Base-class grants or improvements |
| --- | --- |
| 1 | Battle Rage or Primal Rage (each activation); shared Rage uses; Unarmored Defense; Fast Movement (+10 ft, source restrictions); Weapon Mastery; class hit die d12 and source-compatible proficiencies |
| 2 | Reckless Attack; Danger Sense; Uncanny Dodge |
| 3 | Primal Knowledge; Trap Sense +1 |
| 4 | Ability Score Improvement or qualifying ordinary feat |
| 5 | Extra Attack; Improved Uncanny Dodge |
| 6 | Trap Sense +2 |
| 7 | Damage Reduction 1/−; Feral Instinct; Instinctive Pounce |
| 8 | Ability Score Improvement or qualifying ordinary feat |
| 9 | **Greater Battle Rage** (+3 Rage damage); Brutal Critical (1 extra weapon die); Brutal Strike (1d10 and eligible tactical effect); Trap Sense +3 |
| 10 | Damage Reduction 2/− |
| 11 | **Greater Primal Rage** (+6 Strength and Constitution; +3 Will); Relentless Rage |
| 12 | Ability Score Improvement or qualifying ordinary feat; Trap Sense +4 |
| 13 | Damage Reduction 3/−; Brutal Critical (2 extra weapon dice); Improved Brutal Strike (additional effect choices) |
| 14 | Indomitable Will |
| 15 | Persistent Rage; Trap Sense +5 |
| 16 | **Mighty Battle Rage** (+4 Rage damage); Ability Score Improvement or qualifying ordinary feat; Damage Reduction 4/− |
| 17 | Tireless Rage; Brutal Critical (3 extra weapon dice); Improved Brutal Strike (2d10; two distinct effects) |
| 18 | Indomitable Might; Trap Sense +6 |
| 19 | Damage Reduction 5/−; Ability Score Improvement or qualifying ordinary feat (**not a Band 1 Epic Boon**) |
| 20 | **Mighty Primal Rage** (+8 Strength and Constitution; +4 Will); Primal Champion (+4 permanent Strength and Constitution, **no permanent maximum**); review level-20 Rage uses |

**Once, not twice:** Unarmored Defense, Fast Movement, Extra Attack, and other semantically identical features are one grant, even when the same feature exists in several source editions. Neither Greater nor Mighty Rage grants another activation or separate pool.

### Rage activation profiles

| Property | Battle Rage | Primal Rage |
| --- | --- | --- |
| Basic benefit | Advantage on Strength checks and saves; Resistance to bludgeoning, piercing, slashing; no spells or Concentration | Same shared Rage foundation |
| Offensive benefit | Rage damage +2 from levels 1–8, +3 from 9–15, +4 from 16–20 on qualifying Strength attacks | Temporary Strength +4/+6/+8 at 1/11/20. Its resulting Strength modifier contributes ordinarily; **do not also add Battle Rage's damage bonus** |
| Defensive/other benefit | Physical damage Resistance; ordinary Rage restrictions | Temporary Constitution +4/+6/+8 and Will bonus +2/+3/+4; −2 AC; physical Resistance retained |
| Activation | One Bonus Action and one use of the common Rage pool | Same activation and pool, not an additional independent Rage use |
| Normal duration | 5.5e extension model: continue by attacking, provoking a qualifying save, or using Bonus Action; up to 10 minutes | 3.x `3 + modified Constitution modifier` rounds; may end voluntarily; normally once per encounter |
| Aftereffect | No automatic fatigue | Native `condition.fatigued` after Rage until encounter ends, removed by Tireless Rage at 17 |
| Recovery | Pool replenishment shared between both Rage forms | Same pool, with its once-per-encounter restriction retained |

Temporary Constitution changes modify effective Constitution and derived HP for their duration under the common native ability-score rules. They **do not** become a second pool of Temporary Hit Points. An already-active Rage is not silently both forms; choosing a form determines which type-specific effects and restrictions apply.

**Persistent Rage at 15:** Persistent Rage is a base-class feature that applies to **Rage**, not just one form. At level 15, both Battle Rage and Primal Rage become self-maintaining for up to 10 minutes under the same feature. This deliberately upgrades Primal Rage's shorter initial duration; its other restrictions and post-Rage fatigue continue until changed by their own features. The 5.5e once-per-Long-Rest initiative restoration replenishes the **shared Rage pool**. This follows the already approved rule of applying compatible class-wide upgrades to their actual eligible mechanic, rather than artificially reserving the upgrade for one Rage type.

### Other compatible grants and conversions

- **Uncanny Dodge** preserves an applicable Dexterity contribution against flat-footed or invisible attacks (subject to genuine immobilization restrictions). **Improved Uncanny Dodge** protects against flanking and retains the 3.x higher-level Rogue exception when that attack procedure is supported. They are not duplicates of Danger Sense or a Rogue's differently defined reaction.
- **Danger Sense** and **Trap Sense** represent distinct grants. The former changes qualifying saving-throw roll selection; the latter grants scaling trap-specific Reflex-save and dodge-AC modifiers. Shared effect ownership prevents converting the same source effect twice.
- **Fast Movement** grants +10 feet at level 1. Apply the 3.x heavy-load restriction while applicable; the 5.x level-5 wording permits the benefit while not wearing heavy armor. This is one speed bonus with potentially broadened eligibility at level 5, not +20 feet.
- **Damage Reduction** is an always-available flat defense, distinct from the damage-type Resistance active during Rage. Calculate interactions through shared native damage-resolution order, without a Barbarian-only formula.
- **Reckless Attack, Brutal Strike, Brutal Critical, Weapon Mastery, Primal Knowledge, Instinctive Pounce, Feral Instinct, Indomitable Will, Indomitable Might** remain independently granted where conditions are met. Brutal Critical must use Dorks & Dice's critical-escalation dice procedure rather than reverting to the original 5e critical calculation.
- **Relentless Rage** uses a Constitution save beginning at DC 10, increasing by 5 after each use and resetting after a Short or Long Rest. The amount of HP on success conflicts (5e = 1; 5.5e = twice Barbarian level), requiring a specific numerical ruling.
- **Primal Champion** permanently increases Strength and Constitution by 4 each at level 20, regardless of the active Rage type, without establishing a permanent maximum. Mighty Primal Rage remains a temporary *additional* ability-score modification.
- Do not impose the 3.x class alignment or automatic illiteracy restrictions. Skills, competencies, saves, BAB-derived iterative attacks, armor/weapon proficiency, multiclass entitlement and Rage action costs map to their already established native subsystems. Do not invent extra feat/skill entitlements from mere differences in edition table format.
- Subclass entries in the 5.x class tables are **excluded**, not treated as missing base-class features.

## Small remaining decision queue

The project's approved policy forbids silently choosing changed numerical values. The remaining choices below are **actual residual class-specific conflicts**, not questions about the overall rules framework:

1. **Rage uses at level 20:** SRD 5.1 says **Unlimited**; SRD 5.2.1 says **6**. **Recommendation:** retain Unlimited at level 20 (common pool), while Primal Rage remains once per encounter. Ordinary earlier use progression follows the 5.x pool of 2 at 1, 3 at 3, 4 at 6, 5 at 12, 6 at 17, with one use recovered on a Short Rest and all on a Long Rest. This is a numerical choice requiring approval.
2. **Relentless Rage result:** 5e leaves the Barbarian at **1 HP**; 5.5e leaves **twice Barbarian level** HP. **Recommendation:** twice Barbarian level; identical save and escalating DC mechanism. This is a numerical choice requiring approval.
**Resolved by the existing class-wide feature rule:** Persistent Rage applies to both types at level 15 and increases their maximum duration to 10 minutes without requiring ordinary round-by-round extension. Its recovery applies to the shared pool. Primal Rage retains the once-per-encounter restriction and its fatigue aftereffect until Tireless Rage at 17.

**Nonblocking integration checks:** Confirm the unified damage ordering for DR + Resistance, precise treatment of extra critical dice, source-specific surprise wording after native surprise is finalized, and temporary Constitution/HP lifecycle. These are existing cross-cutting subsystem contracts, not new reasons to revisit Barbarian's class identity. Broader Reckless Attack and Danger Sense targeting from SRD 5.2.1 should be tested against native roll/condition eligibility during implementation.

## Readiness

All base-class features are accounted for in the Band 1 draft, and subclass features remain excluded. **Do not replace** `data/native/classes/base/barbarian.json` or mark class-feature candidate documents active until the two residual numerical decisions above are settled and associated native feature records can be updated coherently. This review draft contains no runnable class implementation or conversion.
