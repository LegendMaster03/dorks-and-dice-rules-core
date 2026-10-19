# Monster reconciliation: field-level audit

**Status:** Diagnostic review; no new gameplay ruling made and no monster definition changed.

The initial reconciliation pass treated a creature as resolved only when its complete source definitions were identical after minimal source-reference normalization. This is too conservative for ordinary formatting updates and for compatible added capabilities, but it also avoids inventing an answer for incompatible mechanical revisions.

## Correct baseline denominator

The native coverage manifest tracks **2,522 logical entries**. After the approved general reconciliation pass, **1,358** retain multiple candidates and **590** retain one candidate, for **1,948 pending records**. Before that pass, **1,422** had multiple candidates and **1,057** had one candidate, totaling **2,479 pending records**. The backlog *decreased by 531 records*, rather than increasing from 1,422 to 1,948. Different denominators caused the misleading comparison.

## Pinned monster source comparison

This audit compares the **285** monster names present in both structured SRD source sets, case-insensitively. Counts measure *at least one instance* of a difference and are not exclusive categories.

| Difference | Of 285 paired monsters |
| --- | ---: |
| Challenge rating differs | 0 |
| Headline HP average differs | 121 |
| At least one of six ability scores differs | 73 |
| Numerical armor class differs | 48 |
| Identical headline AC, HP, CR, speed object, and six ability scores | 94 |
| Different action content/markup | 285 |
| At least one shared named action has different hit modifier, listed damage dice, or listed DC | 178 shared *actions*, not 178 monsters |
| Some additional action names | 170 monsters |
| Some action names removed or renamed | 171 monsters |
| Additional trait names | 82 monsters |
| Trait names removed or renamed | 173 monsters |
| Monsters with listed legendary actions in older SRD | 28 |
| Monsters with listed legendary actions in newer SRD | 28 |
| Legendary-action monsters losing legendary actions **entirely** | 0 |
| Newer entries declaring a lair-specific legendary-action count | 25 |

**Interpretation constraints:** These statistics are based on the pinned third-party structured SRD pack; they do not verify official-PDF fidelity. An action/trait name disappearing may mean it was renamed, folded into Multiattack, replaced by another action, or omitted; name comparison alone is not proof of a deleted mechanical capability. AC equality is numeric only and can conceal changed AC contributors. The category 'same headline stats' does not verify that attacks, saving throws, senses, creature size, skill bonuses, or special traits are identical. The changed-action count includes purely presentational changes. The shared-action hit/damage/DC count is based on raw tagged numerical expressions, not fully interpreted attack semantics.

## Examples that motivate different treatments

- **Baboon:** Pack Tactics and Bite are mechanically unchanged despite rewritten tags and terminology. Appropriate for semantic deduplication, subject to final detailed inspection.
- **Commoner:** Training is newly present and should be considered under the approved compatible-addition policy, but the creature also changes size range, alignment, and language defaults. Those fields can affect native creature identity and world assumptions.
- **Bandit:** The basic attack hit modifiers and damage are unchanged, but size, alignment and language options also change. Not simply an identical record.
- **Zombie:** HP average changes from 22 to 15 and Slam dice from 1d6+1 to 1d8+1. This is a numerical-policy question, not additive ability merging.
- **Ape:** Fist and Rock attack damage changes, and Rock gains a recharge mechanic. These are mechanically significant.
- **Adult White Dragon:** The newer entry replaces three physical attacks with Rend, changes Multiattack, rearranges Frightful Presence, and replaces legendary actions. It also adds a lair-specific legendary-action count. Combining every old attack and every new attack without a policy would overgrant offensive options.
- **Lich:** HP average changes from 135 to 315 and multiple features, spellcasting and legendary actions are redesigned. This is not a cosmetic merge.

## Recommended next general ruling (not approved)

A policy for **which numerical stat block and which replacement action suite serves as the default when both editions materially differ** would resolve many monsters as a family.

One coherent possible policy: use the newer 5.2.1 numerical stat block and redesigned basic/legendary actions as the baseline, and restore older unique features only if they have no mechanical duplicate, contradiction, or cost/action incompatibility. Keep genuine lair/legendary mechanics eligible under their stated conditions; do not merely concatenate old/new attack options, and do not turn changes to hit points or attack dice into cumulative bonuses. This is a **proposal**, not an inferred Owner approval. A different Owner choice is valid.

Existing five approved general rules already allow semantic deduplication and compatible feature additions; they do not choose one edition's conflicting monster values. Owner approval is required before applying a numerical/structural precedence policy across the monster category.
