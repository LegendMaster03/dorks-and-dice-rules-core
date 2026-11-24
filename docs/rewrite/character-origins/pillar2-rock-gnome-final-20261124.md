# Pillar 2 — final Rock Gnome ancestry (2026-11-24)

Owner-approved content implemented on the existing Biological/Construct
Origination integration branch. No new branch or merge to main.

## Approved characteristics

- The Rock Gnome inherits the 9-point shared Gnome foundation unchanged.
- The existing Natural Climber gene grants a 15-foot Climb Speed (2 points).
- The existing canonical Stonebound Tremorsense gene now uses source-bound
  `tremorsenseSubTrait` range and contact-material configuration, not
  ancestry-specific duplicate definitions. Dwarves require stone; Rock
  Gnomes accept earth or stone. Both have 60-foot range, Bonus Action
  activation, 10-minute duration, and Proficiency Bonus uses per Long Rest.
  Stone-only remains 3 points, earth-or-stone is 4 points. Unpriced ranges
  or configurations require an explicit policy revision; there is no
  unrecognized-configuration pricing fallback.
- Natural Trickster grants one persistent choice: Disguise Self, Silent
  Image, or Ventriloquism. It is an innate first-level illusion, available
  from character level 1, with one free use per Long Rest (2 points).
  This does not grant class slots or additional slot casting. The native
  first-party catalog has working spell evidence for Disguise Self and
  Silent Image, but not a Ventriloquism spell definition. The entitlement
  carries its approved spell identifier; actual spell execution requires
  the corresponding spell content and runtime.
- Intelligence, Wisdom or Charisma is selected using the existing
  source-scoped innate casting ability rule (1 additional point).

## Trial allocation

| Characteristic | Points |
| --- | ---: |
| Shared Gnome | 9 |
| Tremorsense (earth or stone) | 4 |
| Natural Climber | 2 |
| Natural Trickster | 2 |
| Spellcasting ability | 1 |
| **Rock Gnome** | **18/18** |

The proposed 17-point subtotal excluded the existing one-point
spellcasting-ability intrinsic. Forest Gnome remains 18/18; Dwarf
and Hill Dwarf retain their stone-only 17/18 and 18/18 totals.
Policy revision `biological-pricing-20261124-v14`: 97 genes,
102 entries including five intrinsic slots, allowance unchanged.

## Implementation boundaries

Finite authored spell menus require a single persistent creation choice.
The projection verifies the canonical menu and the actual selected
manifestation or authenticated parent's committed choice. Open-list
spell selection is not implicitly authorized. Tremorsense projection
verifies the exact configured source evidence and preserves the
canonical identity, use pool and activation. The contact-material
array is available to consumers alongside the existing display field.
Older profiles with literal stone-only payloads must be re-evaluated
against the revised canonical definition; migration does not silently
change a stored grant.

Regression tests cover the allocation, both source configurations,
all three offered spell choices, one free use, and rejected forged
parameters or spell choices. Read-only research tools report pending
spell choices and interpret authored range/material configurations.
Live combat, spell effects, terrain detection, persistent resources,
and full integrated CI require separate execution/verification.
