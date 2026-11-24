# Pillar 2 — approved Goliath pricing reconciliation (2026-11-23)

**Status:** approved trial valuation changes, staged on an unmerged feature
branch stacked on the Dwarf reconciliation PR #202.

## Decision

Preserve the six native Giant Ancestries and all of their existing mechanical
effects. Reprice **six already-existing canonical genes** under the versioned
first-party biological policy. This is **not** approval to grant additional
Bigby's feats, new Goliath traits, or a selectable base Goliath without an
Ancestry.

| Existing gene | Previous provisional load | Approved provisional load | Difference |
| --- | ---: | ---: | ---: |
| Large Form (shared Species foundation) | 1 | **4** | +3 |
| Searing Ignition (Fire, unlock level 4) | 1 | **4** | +3 |
| Frigid Retaliation (Frost, unlock level 4) | 1 | **4** | +3 |
| Bulwark (Hill, unlock level 4) | 1 | **3** | +2 |
| Stone Throw (Stone, unlock level 4) | 1 | **3** | +2 |
| Maelstrom Aura (Storm, unlock level 4) | 2 | **5** | +3 |

Large Form was previously charged as a one-point benefit despite providing
one actual size transformation per Long Rest for ten minutes, increased
walking speed, and Advantage on Strength checks (beginning at level 5).
The advanced traits were separately undervalued compared with their
damage, defense, action, and control effects. They are intentionally priced
as **whole abilities**, including their level gates and resource restrictions.

## Resulting native Giant Ancestry allocations

All six inherit the same Species foundation; the foundation rises from
five points to eight with the Large Form correction.

| Playable Ancestry | Before | After | Trial allowance |
| --- | ---: | ---: | ---: |
| Cloud Goliath | 14 | **17** | 18 |
| Fire Goliath | 12 | **18** | 18 |
| Frost Goliath | 12 | **18** | 18 |
| Hill Goliath | 12 | **17** | 18 |
| Stone Goliath | 13 | **18** | 18 |
| Storm Goliath | 11 | **17** | 18 |

Cloud and all six Ancestries retain their existing package construction:
the common foundation is not a directly selectable Species by itself, and
the inherited Giant Ancestry aptitude remains an explicit Strength or
Constitution character-creation selection. The 18-point budget is
provisional and these equivalences are **not** proof of equal play power.

## Deliberately unchanged

- Every Species and Ancestry JSON definition, including the six leaf traits,
  their use pools, action requirements, saving throws, and trigger conditions.
- Level 4 unlocks for advanced powers and level 5 unlock for Large Form.
- Shared early Strike modes, their resource ownership, and the prior
  no-duplicate-damage decisions.
- All other biological prices; particularly Cloudy Escape (4), Fire's Burn (3),
  Frost's Chill (3), Hill's Tumble (5), Stone's Endurance (4), and Storm's
  Thunder (2).
- No extra ecological traits, rune features, cultural background features,
  or unapproved Ancestry substitution model.

## Scope and verification

The pricing policy increments from
`biological-pricing-20261123-v5` to
`biological-pricing-20261123-v6`, with the same 87 canonical genes and 92
pricing entries. New xUnit tests require the complete six-choice native
Goliath construction (including selection of its ancestral aptitude),
check each expected creation allocation, and ensure Large Form and advanced
level gates remain intact. These are allocation/catalog tests, not combat
runtime or encounter balance tests.

Full .NET/CI gameplay execution requires a separate test run before
operational or balance claims. No change to `main` and no merge.
