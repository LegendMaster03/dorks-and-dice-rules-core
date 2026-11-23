# Pillar 2 — Dwarf Fortitude and Vitality allocation (2026-11-23)

**Status:** owner-approved biological and valuation change. Authored on an unmerged feature branch, based on PR #201.

## Approved playable Dwarf foundation

| Effective biological characteristic | Previous trial cost | Approved provisional cost |
| --- | ---: | ---: |
| 60-foot Darkvision | 3 | 3 |
| Level-Based Vitality (+1 maximum HP per character level) | 1 | **3** |
| Poison Resilience (poison-save advantage and 50% poison resistance) | 4 | 4 |
| Stonebound Tremorsense | 3 | 3 |
| **Dwarven Fortitude** | — | **4** |
| **Directly selectable base Dwarf** | **11** | **17 / 18** |
| Hill Ancestry: Stability | 1 | 1 |
| **Hill Dwarf** | **12** | **18 / 18** |

Dwarf remains directly selectable without an Ancestry. Hill Dwarf retains its
one Stability characteristic as an additive Ancestry option. No universal
Species-to-Ancestry replacement policy or new Dwarf Ancestry is introduced.

## Dwarven Fortitude — one inherent biological gene

Adapted from *Xanathar's Guide to Everything*, excluding the original feat's
ability-score increase. **When the Dwarf takes the Dodge action in combat,**
the character may spend exactly one **available Hit Die** to heal. Roll that
Hit Die, add the Constitution modifier, and regain the result (minimum 1 HP),
subject to the normal maximum HP. No free Dodge action, extra Hit Die,
additional healing pool, or special recharge is conferred.

This is an alternative **time** to spend an existing Hit Die; its value
depends on the ordinary action economy and available Hit Dice. It is not
an unlimited restoration effect or a replacement for ordinary Hit Dice
recovery.

The canonical gene is `trait.dwarven-fortitude`, with one normalized
`hitDieHealing` mechanic. Its provisional valuation is 4 points.

## Vitality price correction

`trait.level-based-vitality` rises from 1 to 3 points in the versioned
first-party trial pricing table. **The mechanic is unchanged:** +1 maximum
HP per character level, with no new healing, extra Hit Dice, or additional
level progression. The pricing policy increments from
`biological-pricing-20261123-v4` to `biological-pricing-20261123-v5`.

## Existing Hill Stability normalization

The preexisting Hill-only `trait.stability` was descriptive-only, making its
automatic selection incomplete in the native Origin constructor. This
increment records the *already-approved* behavior in a structured
`conditionalAdvantage` object without changing its effect: while on stable
ground, advantage on checks or saves against being forcibly pushed or knocked
Prone, excluding pulling; no immunity granted. Its price remains 1 point.

## Pending / boundaries

- **Squat Nimbleness:** needs review and approval. It is not granted.
- **Dwarven Fortitude** is now approved, superseding the earlier pending-feat
  candidate note in independent draft PR #199. Reconcile that note before
  integration so the native record does not retain contradictory approval labels.
- **NPC/monster application** of Level-Based Vitality remains unresolved.
- **Runtime execution:** Native data captures conditional Hit Die spending
  and the grounded Stability advantage, but this increment does not implement
  combat Dodge-triggered healing or full active combat execution.
- Added C# integration tests for 17-point Dwarf, 18-point Hill Dwarf,
  lower-budget rejection, the spend-one-Hit-Die semantics, unchanged
  HP progression, and Hill Stability's grounded scope.
- No change to `main`; branch remains unmerged.
