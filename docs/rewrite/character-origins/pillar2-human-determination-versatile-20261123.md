# Pillar 2 — Human Determination and Versatile valuation (2026-11-23)

**Status:** approved native biological increment, staged on an unmerged feature
branch stacked on Goliath pricing PR #203.

## Approved Human foundation

Human remains a selectable base Species without a required Ancestry or
Upbringing. The four benefits are **cumulative**.

| Canonical characteristic | Previous trial load | Approved provisional load |
| --- | ---: | ---: |
| Skillful — one additional Skill proficiency | 1 | 1 |
| Adaptable Learning — four extra ranks at level 1, plus one each later level | 6 | 6 |
| Versatile — choose one Origin feat at creation | **10** | **6** |
| Human Determination — once per rest pre-roll Advantage | — | **4** |
| **Directly selectable Human total** | **17** | **17 / 18** |

No change to the Human Species' humanoid type, 30-foot walking Speed, or
Small/Medium choice, and no additional ability-score increase.

## Human Determination

Adapted from *Unearthed Arcana: Feats for Races* (2017, p. 3), a playtest
source rather than a subsequently finalized feat. The original +1
ability-score increase is **not** granted. The approved standalone inherent
gene is `trait.human-determination`:

- **Trigger/decision:** Before making an attack roll, an ability check,
  or a saving throw, declare use of Human Determination.
- **Effect:** Make that particular roll with Advantage. It does not
  stack with already applicable Advantage.
- **Resource:** One use, refreshed after a Short or Long Rest.
- **Action economy:** No separate Action or Reaction is spent. The
  player must choose before rolling, not retroactively after seeing the result.

The source represents an exceptional inherited capacity for effort rather
than a mandatory personality or moral trait. Other Species are not described
as less determined.

The `rollAdvantage` data shape is explicitly typed in the canonical
biological trait validator. This is a data/creation-contract increment, not
a claim that a combat or dice-roll executor already applies the result,
tracks rest resources, or prevents retroactive use in gameplay.

## Versatile repricing and deferred feat catalog

`trait.versatile` keeps its existing mechanical rule: choose **one
additional Origin feat** at character creation, independently of the
other Human benefits. Only its provisional price changes, from 10 to 6.

The choice's actual power varies by the eligible feat and class/build
synergy. Six is a working holistic value for player choice, not a
determination that every Origin feat costs exactly six points. The
first-party feat catalog and qualification checks are incomplete, so
final valuation remains open. The existing test-only authority used in
C# regression tests can authorize a dummy Origin feat for testing; it
does not make that feat publicly selectable.

## Explicitly deferred

- Resourceful/Heroic Inspiration awaits the general Inspiration rule pass.
- Additional skill-rank allocation and legal rank caps await advancement/
  character-sheet integration. This increment does not alter either entitlement.
- No invented Human Ancestry, alternative feat-entitlement mechanics,
  ability-score increase, or extra Inspiration rule is introduced.

## Verification and integration

The trial pricing policy advances
`biological-pricing-20261123-v6` → `biological-pricing-20261123-v7`.
There are 88 canonical genes and 93 policy entries (including five
intrinsic slots). New tests cover complete 17/18 direct Human,
budget enforcement, pre-roll/once-per-rest effect, and unchanged
Origin feat, Skill proficiency, and rank entitlements.

The PR is stacked on Goliath #203. No merge or change to `main`.
Full .NET/CI execution and roll-engine gameplay tests remain outstanding.
