# Pillar 2 — approved Orc foundation reconciliation (2026-11-23)

Status: approved biological increment, staged in an unmerged feature branch.

## Approved selectable base Species

Orc remains independently playable without an Ancestry. It has seven canonical
biological genes with the following *provisional* loads:

| Gene | Points |
| --- | ---: |
| Low-Light Vision | 2 |
| Adrenaline Rush | **4** (previously 2) |
| Relentless Endurance | 4 |
| Powerful Build | 1 |
| Savage Attacks | 1 |
| Menacing Presence | 1 |
| **Orcish Fury** | **4** (new) |
| **Complete Orc foundation** | **17 / 18** |

The 18-point trial allowance, physical intrinsics, current trait identities, and
the other six genes' mechanics are unchanged. No compulsory Upbringing or Orc
Ancestry has been introduced.

## Orcish Fury: one gene, two effects

Adapted from the Half-Orc feat in *Xanathar's Guide to Everything*, without
granting the original feat itself or its Strength/Constitution increase.

1. **Extra weapon damage:** Once per Short or Long Rest, after hitting with a
   Simple or Martial weapon attack, add one die of that weapon's damage.
2. **Endurance-triggered attack:** Immediately after using the independently
   granted `trait.relentless-endurance`, the Orc can use their ordinary Reaction
   to make one weapon attack. This ability is conditional on having and using
   that trait; it grants no new use of Relentless Endurance, no additional
   Reaction, and no separate free pool of reaction attacks.

These modes are parts of a *single* canonical biological characteristic. Do
not split them into two independently priced genes or duplicate the base
Relentless Endurance grant. Other rules for weapon damage dice, ordinary
Reactions, and the action economy remain authoritative.

## Future Orc Ancestries

A full 17-point Species foundation makes an ordinary Orc playable even if no
Orc Ancestry is ever authored. Future Ancestries **may** customize that
foundation by substituting comparable traits, rather than automatically
adding a new package on top. This is a design direction only: no universal
Species-to-Ancestry override, suppression, or replacement policy is approved
or implemented. Every actual substitution requires its own explicit authored
resolution and pricing review.

Historical Headlong Rush is recorded on the Orc Species as **Needs review;
pending approval**. It is not included in the 17-point package.

## Tests and scope

- New `NativeOriginOrcFoundationTests` cover direct, ancestry-free selection;
  authorized 17/18 allocation; over-allocation at 16; the two Orcish Fury modes
  as one trait; and unchanged Adrenaline Rush effects.
- Existing Python Orc content regression now checks the new gene's data shape.
- The first-party pricing schedule is revision v4 (86 genes plus five intrinsic
  slots); all other entries retain the prior revision's load.
- No full real-time gameplay execution of the new weapon-damage trigger,
  conditional Reaction attack, or rest/use scheduling is added here. Independent
  acceptance testing remains necessary before claiming operational coverage.
- No main-branch changes or merge.
