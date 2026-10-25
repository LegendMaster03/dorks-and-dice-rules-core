# Fiend Patron — consolidated owner decision and Invocation-pillar handoff

**Decision record date:** 2026-10-24  
**Branch:** `feature/fiend-infernal-retaliation-unified`  
**Scope:** Final consolidation of **owner-approved Fiend Patron design directions**, not a claim that all native source-version disputes or runtime definitions are complete.

## Summary

The native Fiend is a Warlock Patron acquired at **Warlock class level 1**. It provides Dark One's Blessing, Patron spell access, Dark One's Own Luck, one two-mode Infernal Retaliation feature with upgrades, a separate level-10 damage-resistance feature, and Hurl Through Hell.

**Hellfire Shield is excluded from the Fiend's normal automatic features and from Infernal Retaliation. Its acquisition decision is deferred to the Eldritch Invocations pillar.** The three possible Fiend-restricted routes—automatic Patron grant, Patron choice, or Invocation with Fiend prerequisite—are candidates, not a completed ruling.

**One core Fiend mechanic remains open:** the level-10 damage-resistance feature, which the owner is considering replacing with the newer defensive mechanics. The spell roster and entitlement rules are now approved: default C, optional A/B. Keep the native Fiend subclass `pending-native-reconciliation` until these and any required effect-resolution details are adjudicated. A consolidated decision is not an executable package.

## Approved or partially approved advancement

| Warlock class level | Feature | Approved native direction | Remaining qualification |
| --- | --- | --- | --- |
| **1** | Fiend Patron identity | Patron source/identity begins at Warlock level 1; do not defer identity to 2024 level 3 | No second Patron grant when reaching level 3 |
| **1** | Dark One's Blessing | 2024 cooperative defeat trigger; temporary HP = max(1, Charisma modifier + Warlock level) | Standard nonstacking temporary HP |
| **1** | Patron spell access | Default automatically grants 13 spells at 3/5/7/9, including the three restored 2014-only spells | Optional 2014-style expanded choices from the same 13-spell list |
| **6** | Dark One's Own Luck | Add 1d10 after an ability check/save roll, before resolution, at most once per roll; uses = max(1, Charisma modifier), recovered on Long Rest | Approved |
| **8** | Infernal Retaliation | Acquire one Fiend Patron feature with **two** alternative Reaction modes: Fiendish Resilience fast healing and Fiendish Flamewreath; shared 1 use/Long Rest | Tick and damage-event implementation details below |
| **10** | Fiendish Resilience (damage resistance) | Distinct, source-attested Fiend Patron resistance feature at class level 10 | Damage-type eligibility and bypass **unresolved** |
| **13** | Infernal Retaliation improvement | Increase fast healing to 2 HP/round and Flamewreath to 2d6 Fire retaliation | No extra feature use |
| **14** | Hurl Through Hell | Approved 2024-style save, damage timing, return, condition, and recovery | Details below |
| **18** | Infernal Retaliation improvement | Increase fast healing to 5 HP/round and Flamewreath to 5d6 Fire retaliation | No extra feature use |

**Base Warlock distinction:** Eldritch Blast is an automatic level-1 Warlock class feature rather than a selected cantrip. Its Patron Imprint and optional advanced enhancements remain three-layer, cross-pillar design work; they do not constitute an extra Fiend subclass entitlement here.

## Dark One's Blessing

At Fiend Patron level 1, the Warlock gains temporary hit points of **max(1, Charisma modifier + Warlock level)** if:

1. The Warlock reduces a hostile creature to 0 HP, **or**
2. Another creature reduces an enemy within 10 feet **of the Warlock** to 0 HP.

Temporary HP do not stack. This adopts the wider cooperative 2024 trigger while retaining the approved level-1 native grant.

## Dark One's Own Luck

At Warlock level 6, after making an ability check or saving throw, but **before the outcome/effects are resolved**, add **1d10**. Use at most once per roll. Total uses are **max(1, Charisma modifier)**, and all expended uses return after a **Long Rest**.

## Infernal Retaliation: one Fiend feature, exactly two modes

Infernal Retaliation is **not** a shared base Warlock feature. It is acquired at Fiend Warlock class level **8**, improved at **13** and **18**.

| Warlock level | Fiendish Resilience fast healing | Fiendish Flamewreath Fire retaliation |
| --- | --- | --- |
| **8** | **1 HP/round** | **1d6** |
| **13** | **2 HP/round** | **2d6** |
| **18** | **5 HP/round** | **5d6** |

- At activation, the player chooses **one** of the two modes.
- Both are **Reactions**, and **native Dorks & Dice Reactions do not require external triggers**. Player chooses when to activate an available Reaction.
- Both sustained effects last **20 rounds (2 minutes)**. Their ongoing operation does not consume additional Reactions merely to continue.
- Using either mode consumes **one single shared use per Long Rest**. Improvements do not restore/expand this resource or grant more than one feature.
- **Fast healing at 0 HP:** If the Warlock is reduced to 0 HP **while this mode is already active**, the Warlock **stabilizes once** without recovering any HP or consciousness. The mode does **not** heal while the Warlock remains at 0 HP, restore consciousness, revive a dead creature, or automatically stabilize again after subsequent damage. Further damage at 0 HP applies normal damage/death-saving-throw and stabilization rules. A Warlock already unconscious ordinarily can not activate its Reaction to start this mode.
- **Remaining procedural implementation:** Determine exact per-round healing tick timing above 0 HP, plus when Flamewreath retaliation damage applies during the ongoing effect. Do not confuse these event-resolution details with a requirement for an external trigger to activate the Reaction.

**Name collision:** The 3.5e healing effect called *Fiendish Resilience* (now an Infernal Retaliation mode) is **not** the 5.x **level-10 Fiendish Resilience damage-resistance** feature. They must have separate native feature identities.

## Hurl Through Hell

At Warlock level **14**, once per turn on a **qualifying attack-roll hit**, expend the feature's use and force a target Charisma saving throw versus the Warlock spell save DC.

- **Failed save:** Target travels through the Lower Planes, takes **8d10 Psychic damage immediately if it is not a Fiend**, becomes **Incapacitated**, and returns at the **end of the Warlock's next turn**.
- **Successful save:** The attempt still expends the use; no refund.
- **Recovery:** A use returns on a **Long Rest**, or the Warlock may **spend a Pact Magic spell slot to restore a use**.

This is the owner-approved coherent SRD 5.2.1-style rule rather than the 2014 no-save, 10d10-on-return version.

## Fiend Patron spell access — approved native 2024 automatic grants with 2014 restorations

The Fiend Patron and spell-access feature begin at **Warlock level 1**, while **automatic Patron spell grants begin at Warlock level 3**, following the 2024 class-level schedule. **All 13 spells** in the 2014/2024 union become automatically known, without consuming regular Warlock spell selections.

### Native default

| Warlock level | Automatically known Fiend spells |
| --- | --- |
| **1** | None yet; Patron identity and spell-access feature granted |
| **3** | Burning Hands; Command; Scorching Ray; Suggestion; **Blindness/Deafness** |
| **5** | Fireball; Stinking Cloud |
| **7** | Fire Shield; Wall of Fire |
| **9** | Geas; Insect Plague; **Flame Strike; Hallow** |

**Bold spells** are the three restored from the 2014 Fiend list. Their automatic grant levels follow their spell levels: Blindness/Deafness (2nd) at Warlock 3; Flame Strike and Hallow (5th) at Warlock 9. The default adds **5/2/2/4** automatic spells at Warlock 3/5/7/9. It does **not** accelerate first-level Fiend spells to Warlock level 1.

### Two playable spell-access modes — one merged repertoire

| Campaign mode | Fiend spells available | Acquisition rule |
| --- | --- | --- |
| **Native Dorks & Dice (default)** | All **13 merged** spells | All thirteen automatically known at Warlock levels 3/5/7/9 |
| **2014-style expanded access (optional)** | The **same 13 merged** spells | All thirteen may be chosen using normal Warlock spell selections; none is automatically learned |

Both modes use one reconciled spell list. The only difference is how the Warlock acquires its spells. The optional rule uses 2014-style expanded-list mechanics **without reverting to the original ten-spell list**. The redundant unmodified 2024 optional rule is removed. Original 2014 and 2024 lists remain in historical source projections, not as playable Patron spell rosters. Both modes are mutually exclusive, not cumulative.

### Shared casting and entitlement rules

No daily preparation; automatically acquired Patron spells are known and accessible. They do not consume normal Warlock spell selections, grant extra Pact Magic slots, or grant free castings. The Warlock must still satisfy normal spell-level eligibility and expend the appropriate valid casting resource. Do not duplicate already known spells. If a regular Warlock spell choice later becomes automatically known, **restore that normal choice** for another eligible spell. Campaign-profile migration must preserve entitlements and avoid duplicates.

The original SRD lists remain preserved as historical source projections rather than playable roster restrictions. Non-SRD Fiendish Heritage is not automatically introduced. This approval supersedes the previous C default (ten automatic plus three optional choices) and the previous level-1 all-automatic B schedule.

## Remaining Fiend Patron mechanic review

### 2. Fiendish Resilience: damage resistance at level 10

The **feature slot and separate identity** are retained; the final rule for damage type selection is **not yet approved**.

- **2014 source:** Choice of damage resistance after a Short or Long Rest, with a magical/silvered-weapon bypass.
- **2024 source:** Choice excludes Force damage, and the earlier magical/silvered-weapon bypass is absent.

The owner must decide Force eligibility, selection-change cadence, and any bypass behavior. This does **not** reopen the separately approved fast-healing mode.

## Hellfire Shield — deferred explicitly to Eldritch Invocations pillar

Hellfire Shield is **not** a third Infernal Retaliation effect. It does **not** consume the shared Infernal Retaliation Long Rest charge, nor is it automatically granted at Warlock level 8.

The next pillar will choose **one** approach:

1. An **automatic Fiend Patron grant** at a defined level;
2. A **selectable Fiend Patron feature** with an explicit choice entitlement/budget; or
3. A **Fiend-prerequisite Eldritch Invocation**, consuming an ordinary Invocation selection.

Whichever path is later chosen must reuse **one Hellfire Shield ability definition**, avoiding duplicate benefits across guided and custom builds.

The approved design direction outside Infernal Retaliation remains **player-chosen Reaction** and **derived damage from Eldritch Blast**, including an eligible separately acquired Hellfire Blast enhancement. Shield's own resource/cost, original Constitution damage, exact multi-beam/critical interaction, prerequisite level, and target resolution remain **unresolved for the next pillar**. The Hellfire enhancement must not be automatically granted simply by selecting Fiend.

## Completion and implementation gate

- **Settled:** Fiend identity at level 1; **native automatic 13-spell access with optional 2014-style selection from the same 13-spell list**; Blessing; Luck; Hurl Through Hell; one two-mode Infernal Retaliation at 8/13/18 with 20-round duration, shared one-use Long Rest recovery, and the once-only stabilization rule; Shield excluded and deferred to the Invocation pillar.
- **Not settled:** Level-10 Fiend damage resistance, currently under consideration for supersession by the improved native defensive mechanics. Some Infernal Retaliation effect resolution still needs shared engine mapping.
- **Native activation:** Do not overwrite `data/native/classes/subclasses/warlock/fiend.json`'s `pending-native-reconciliation` candidate staging or claim a playable vertical slice until remaining mechanics are resolved and tests validated.
- **Workflow:** Resolve the remaining level-10 damage-resistance disposition, then proceed through **Pacts** and **Eldritch Invocations**; address deferred Hellfire Shield routes in the **Invocations pillar**, without reopening Infernal Retaliation.

Authoritative sources: `data/reference/srd/warlock-fiend-patron-crosswalk.json`; `data/reference/srd/class-reconciliation-decisions.json`; `data/reference/3x/warlock-band1-base-comparison.json`; original SRD 5.1 and 5.2.1 source candidates preserved under `data/native/classes/subclasses/warlock/fiend.json`.
