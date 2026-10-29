# Warlock Invocation progression — retained Band 1 testing model

**Branch:** `feature/rules-core-rewrite`  
**Date:** October 25, 2026  
**Status:** **Owner-selected provisional testing baseline; no final Invocation progression/balance approval.**

The owner explicitly chose to **retain the original 3.5e/5.5e hybrid** for initial testing instead of adopting the later no-level-1 alternative. Do not silently change the level-1 Invocation point to zero. Preserve the comparative evidence so progression can be revisited while balancing the native Warlock.

## Current initial-testing model

- At **Warlock 1**, grant **one elective Invocation** *and* the independently acquired first Pact. The Pact costs **zero** Invocation selections; every Invocation still attaches to one actual Pact and requires that Pact's Patron to accept it.
- At **Warlock 20**, the test model provides **twelve elective Invocations**, plus the separately granted Pact.
- The level-1-zero model is retained **only as a counterfactual** for balance testing, not as the selected model.
- No native Warlock class record, executable selection budget, 3.5e grade prerequisite, or Band 2 feature is changed by this document.

The exploratory rule is `hybrid(level) = max(3.5e invocation count, 2024 invocation budget - 1)`. The `-1` makes an **illustrative** 2024 character that spent exactly one selection on a Pact comparable to our free first Pact. It does **not** mean that all 2024 characters always spend exactly one selection on a Pact.

**Elective Invocation acquisition levels (selected test model):** 1, 2, 4, 5, 7, 9, 11, 13, 15, 16, 18, 20.  
**3.5e acquisition levels:** 1, 2, 4, 6, 8, 10, 11, 13, 15, 16, 18, 20.  
The hybrid moves only Invocation acquisitions **#4, #5, and #6** from 3.5e levels **6/8/10** to **5/7/9**.

## Full comparison — keep 5e (2014) visible

| Level | 3.5e | 5e (2014) | 5.5e (2024), raw | 5.5e less 1 Pact | D&D provisional hybrid |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1 | 0 | 1 | 0 | **1** |
| 2 | 2 | 2 | 3 | 2 | **2** |
| 3 | 2 | 2 | 3 | 2 | **2** |
| 4 | 3 | 2 | 3 | 2 | **3** |
| 5 | 3 | 3 | 5 | 4 | **4** |
| 6 | 4 | 3 | 5 | 4 | **4** |
| 7 | 4 | 4 | 6 | 5 | **5** |
| 8 | 5 | 4 | 6 | 5 | **5** |
| 9 | 5 | 5 | 7 | 6 | **6** |
| 10 | 6 | 5 | 7 | 6 | **6** |
| 11 | 7 | 5 | 7 | 6 | **7** |
| 12 | 7 | 6 | 8 | 7 | **7** |
| 13 | 8 | 6 | 8 | 7 | **8** |
| 14 | 8 | 6 | 8 | 7 | **8** |
| 15 | 9 | 7 | 9 | 8 | **9** |
| 16 | 10 | 7 | 9 | 8 | **10** |
| 17 | 10 | 7 | 9 | 8 | **10** |
| 18 | 11 | 8 | 10 | 9 | **11** |
| 19 | 11 | 8 | 10 | 9 | **11** |
| 20 | 12 | 8 | 10 | 9 | **12** |

The raw 2024 count includes selections that can buy a Pact. By contrast, 2014's Pact is independent of Invocations and acquired at Warlock level 3. In native Dorks & Dice the first Pact is independent and granted at level 1.

## Balance observations to retain

1. **Level 1:** 3.5e grants one Invocation; 2014 grants zero; 2024 grants one selection that can instead buy a Pact. Our proposal grants **one elective Invocation plus a free Pact**, intentionally more permissive than the 2024 one-Pact comparison at level 1.
2. **Level 2:** Two elective Invocations in both 2014 and the 2024 one-Pact illustration. Our hybrid matches two.
3. **Levels 5, 7, 9:** The Pact-adjusted 2024 progression temporarily leads 3.5e by one, moving the fourth, fifth, and sixth hybrid selections earlier; pay special attention to **level-5 intrinsic Pact upgrades**.
4. **Levels 11–20:** 3.5e becomes the maximum curve, resulting in continuing selectable specialization up to twelve at level 20.
5. **Edition differences:** 3.5e Invocations are not interchangeable in strength with 2014/2024 Invocations. The mathematical maximum is a **candidate test schedule, not evidence of balance**.
6. **Historical grades:** Least, Lesser, Greater, and Dark unlocking in 3.5e does not itself adopt those grades or prerequisite levels into the native class.
7. **Scope:** The level-21–40 comparison was exploratory only. Band 2 is explicitly **out of scope**.

## Suggested initial tests

Run builds at levels **1, 2, 4, 5, 7, 9, 11, 13, 15, 16, 18 and 20** with the owner-approved Fiend Patron and each available Pact. Compare actual viable options and effective strength rather than counting Invocations alone. Consider Pact Magic, intrinsic Pact features, independent spell grants, action/reaction conflicts, Invocation prerequisites, Pact severance, and source-specific effects. Use the source curves and the no-level-1 counterfactual as sensitivity checks rather than silently updating the selected model.

**Machine-readable data:** [Warlock Band 1 Invocation progression comparison](../../data/reference/warlock-invocation-progression-band1-comparison.json).  
**Historical source:** [3.5e/2014/2024 research index](../../data/reference/3x/warlock-invocations-research-index.json).  
**Prerequisite and source audit:** [Invocation reconciliation audit](warlock-eldritch-invocation-reconciliation-audit.md).

**Implementation boundary:** A data-only analysis record. The native Warlock remains `pending-native-reconciliation`; no character builder or gameplay engine should treat this candidate as the final adopted entitlement.
