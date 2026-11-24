# Pillar 2 — Chthonic Tiefling Life Siphon (2026-11-23)

**Status:** Approved Chthonic-only biological increment, staged in a draft
feature branch based on Infernal Tiefling draft PR #205. Not merged.

## Approved biological characteristic

Add one canonical inherited gene, `trait.chthonic-life-siphon`, to the
selectable `ancestry.tiefling.chthonic`, with a provisional price of
**4 points**.

**Chthonic Life Siphon:** Once per turn, when the character deals
**Necrotic damage to another creature**, they may regain Hit Points
equal to their **Proficiency Bonus**, capped by the Necrotic damage
that creature **actually takes**. They can use the characteristic
**Proficiency Bonus times per Long Rest**, restoring all uses upon
finishing a Long Rest. Ordinary maximum Hit Point restrictions apply.

- **Once per turn:** A use can be triggered on any turn, but not more
  than once during that turn; separate Necrotic damage instances on
  the same turn do not grant additional activations.
- **Actual damage:** Damage must reach the other creature after
  relevant defenses. Zero actual Necrotic damage grants zero healing.
  Damage immunity or resistance may reduce the healing ceiling.
- **Source neutrality:** Any eligible Necrotic damage the character
  personally deals can qualify, including the inherited Chill Touch
  spell; no additional damage, resistance penetration, or damage-type
  conversion is granted.
- **Healing:** Recover at most the lesser of Proficiency Bonus,
  actual Necrotic damage taken, and missing Hit Points. It is
  ordinary healing, not Temporary Hit Points.
- **Uses:** Proficiency Bonus uses, refreshed on a Long Rest.
  The damage trigger itself does not provide extra uses.

The approved design does not modify action or spellcasting rules.
It does not automatically grant Vampiric Touch. That spell remains
pending reconciliation in `working/`.

## Allocation

| Direct Tiefling Ancestry | Previous | Updated |
| --- | ---: | ---: |
| Abyssal, Poison | 13 | **13 / 18**, unchanged |
| Chthonic, Necrotic | 13 | **17 / 18** |
| Infernal, Fire | 18 | **18 / 18**, unchanged |

The shared Tiefling Species (Fiend creature type, Low-Light Vision,
Thaumaturgy, mortal needs, size choice) is unchanged. Chthonic still
grants 50% Necrotic resistance, Chill Touch, False Life at character
level 3, and Ray of Enfeeblement at character level 5, with the
original casting-ability choice and existing Long Rest spell uses.

The previously proposed Abyssal Fortitude, Poison Tolerance and
Potent Venom package has not been approved or implemented. The earlier
Necrotic conversion/resistance bypass proposals have been rejected
and are not implemented here.

## Native contract and integration boundaries

The `lifeSiphon` payload binds a Necrotic-damage event on another
creature to self-healing of Proficiency Bonus, capped by actual damage
dealt and ordinary maximum HP. Root `oncePerTurn` and
`uses: { value: "proficiency-bonus", recharge: "long-rest" }`
encode independent turn and Long Rest limits. The biological trait
shape recognizes this payload explicitly.

The pricing revision advances
`biological-pricing-20261123-v8` →
`biological-pricing-20261123-v9`. The catalog contains 90 canonical
genes and 95 pricing entries, including five intrinsic slots.

Focused xUnit tests validate the authored parameters, whole direct
Tiefling-Ancestry trial totals and grants, and unchanged Chthonic
Necrotic resistance and spells. **These tests are not gameplay
execution tests.** Actual damage event dispatch, turn activation
tracking, spent-use persistence, Long Rest refresh, HP healing
application and class/condition interactions remain executor
integration work. No .NET runtime test result is asserted without
a successful test run.

Do not merge this draft or modify `main` without approval.
