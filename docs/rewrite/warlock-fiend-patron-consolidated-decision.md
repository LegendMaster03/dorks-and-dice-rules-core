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
| **1** | Patron spell access | At Patron level 1, 13 spells become eligible; default C grants ten automatically at class levels 3/5/7/9 | Optional campaign rules A (expanded only) and B (all 13 automatically) |
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

## Fiend Patron spell access — owner-approved native rule C; optional A/B

**The Fiend spell-access feature begins at Warlock level 1.** It does not itself give free castings or additional Pact Magic slots. Three **mutually exclusive campaign-rule modes** share one native spell-access feature and one 13-spell eligible roster; they are alternatives, **not cumulative grants**.

| Mode | Rule | Classification |
| --- | --- | --- |
| **C — Reconciled** | Expand eligible spells to all **13** at Patron level 1, and automatically know the **ten 2024 spells** at Warlock levels 3, 5, 7, 9 without consuming regular choices; the **three 2014-only spells** remain eligible normal selections | **DEFAULT native** |
| **A — Expanded only** | All **13** are eligible normal spell selections; **none** is automatically learned. Each selected spell consumes a regular Warlock spell choice | **Optional campaign rule** |
| **B — All automatically known** | Automatically learn **all 13**, without consuming regular Warlock selections, at eligible Warlock class levels **1, 3, 5, 7, 9** according to their spell levels | **Optional campaign rule** |

### Eligible Fiend spell roster — identical in all modes

| Spell level | Full 13-spell union |
| --- | --- |
| **1st** | Burning Hands; Command |
| **2nd** | Blindness/Deafness; Scorching Ray; Suggestion |
| **3rd** | Fireball; Stinking Cloud |
| **4th** | Fire Shield; Wall of Fire |
| **5th** | Flame Strike; Hallow; Geas; Insect Plague |

### Automatic-grant schedules by mode

| Warlock class level | **C (default)** — automatic grants | **B (optional)** — automatic grants |
| --- | --- | --- |
| **1** | None; Patron expands available options | Burning Hands; Command |
| **3** | Burning Hands; Command; Scorching Ray; Suggestion | Blindness/Deafness; Scorching Ray; Suggestion |
| **5** | Fireball; Stinking Cloud | Fireball; Stinking Cloud |
| **7** | Fire Shield; Wall of Fire | Fire Shield; Wall of Fire |
| **9** | Geas; Insect Plague | Flame Strike; Hallow; Geas; Insect Plague |

**Mode A has no automatic Patron spell grants** at any level. B grants all 13, while C grants ten automatically and keeps the other three eligible as ordinary learned-spell choices.

**Common entitlement/casting rules:** Native Warlocks do not prepare spells daily. An automatically known Patron spell is available without a learned-spell selection but **still requires Pact Magic slots or another valid casting resource** and obeys normal spell-level eligibility; the grants supply no free casts or slots. A spell already known must not be granted twice. If a spell was taken with a normal Warlock selection and later becomes an automatic Patron spell (for example, under C), **refund/replace the original selection** with another legal spell rather than lose the selection entitlement. The optional rule choice is configured at campaign level, and changing modes midcampaign requires a de-duplicated, entitlement-preserving migration.

**Provenance:** SRD 5.1 expanded list contributes the original three extra options; SRD 5.2.1 contributes the ten automatic spell grants. The no-daily-preparation native casting system controls both. Non-SRD Fiendish Heritage features remain research-only and are not added to this spell feature.

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

- **Settled:** Fiend identity at level 1; **spell-access default C with A/B optional modes**; Blessing; Luck; Hurl Through Hell; one two-mode Infernal Retaliation at 8/13/18 with 20-round duration, shared one-use Long Rest recovery, and the once-only stabilization rule; Shield excluded and deferred to the Invocation pillar.
- **Not settled:** Level-10 Fiend damage resistance, currently under consideration for supersession by the improved native defensive mechanics. Some Infernal Retaliation effect resolution still needs shared engine mapping.
- **Native activation:** Do not overwrite `data/native/classes/subclasses/warlock/fiend.json`'s `pending-native-reconciliation` candidate staging or claim a playable vertical slice until remaining mechanics are resolved and tests validated.
- **Workflow:** Resolve the remaining level-10 damage-resistance disposition, then proceed through **Pacts** and **Eldritch Invocations**; address deferred Hellfire Shield routes in the **Invocations pillar**, without reopening Infernal Retaliation.

Authoritative sources: `data/reference/srd/warlock-fiend-patron-crosswalk.json`; `data/reference/srd/class-reconciliation-decisions.json`; `data/reference/3x/warlock-band1-base-comparison.json`; original SRD 5.1 and 5.2.1 source candidates preserved under `data/native/classes/subclasses/warlock/fiend.json`.
