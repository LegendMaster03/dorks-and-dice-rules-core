# Fatigued and the historical 3.x Exhausted distinction

**Scope:** Native-data reconciliation of **Fatigued**. This follows PR #102, which follows PRs #100 and #99. It is not a merge into the rewrite branch.

3.x Fatigued differs from the Dorks & Dice six-level Exhaustion mechanic, and the historical 3.x **Exhausted** rule differs from any individual SRD 5.1 Exhaustion tier.

| Historical concept | Effects | Native decision |
| --- | --- | --- |
| **3.x Fatigued** | −2 effective Strength and Dexterity, cannot Run or Charge, generally cured by eight hours of complete rest | Add a separate native `condition.fatigued` with exactly these effects. |
| **3.x Exhausted** | −6 effective Strength/Dexterity, half Speed; one hour complete rest changes the condition to Fatigued | Preserve the historical distinction as an unresolved compatibility mapping. Do not create a second native Exhaustion variant or silently equate it to one tier. |
| **Dorks & Dice Exhaustion (SRD 5.1)** | Six cumulative levels controlling checks, Speed, attacks, saving throws, HP maximum, and death | **Already approved, unchanged.** |

**Stacking conflict not silently resolved:** The 3.x rule escalates another application of fatigue to Exhausted. The unified 5.x-derived condition framework does not automatically stack repeated applications of a single condition. Rather than invent a new Exhaustion level conversion, the native Fatigued definition is usable for a single application, while the source-specific escalation remains pending an explicit decision.

A creature may simultaneously be Fatigued and have native Exhaustion when those effects are independently present, but the eventual runtime must combine actual ability-score and other impairments using the shared rules. This is a distinct question from automatically converting repeated Fatigue to Exhaustion.

The source evidence and deferred mapping are recorded in `data/reference/3x/fatigue-exhaustion-reconciliation.json`. The original 5.x SRD and six-level Exhaustion record are unchanged.

Sources: [3.5 Condition Summary](https://www.d20srd.org/srd/conditionSummary.htm) and [3.0 Condition Summary transcription](https://opengamecontent.fandom.com/wiki/D20_System_v3.0_SRD/Condition_Summary). Historical Open Game Content provenance is separate from the 5.x CC BY source corpus.

This is a data contract and regression suite, not the finished condition evaluator or rest scheduler.
