# Pillar 2 — Abyssal Tiefling: Fortitude and Poison specialization (2026-11-23)

**Status:** Owner-approved biological increment, draft-staged on the Chthonic
Life Siphon PR #206 branch. Not merged.

## Three approved characteristics

The biological ancestry `ancestry.tiefling.abyssal` receives exactly
three new traits, priced individually, in addition to its existing
50% Poison damage resistance, Poison Spray, level-3 Ray of Sickness,
level-5 Hold Person, and inherited Low-Light Vision and Thaumaturgy.

| New characteristic | Trial cost | Effect |
| --- | ---: | --- |
| **Abyssal Fortitude** | 2 | Increase total maximum HP by `max(1, floor(total character level / 2))` |
| **Poison Tolerance** | 1 | Advantage on saving throws to **avoid or end Poisoned** |
| **Potent Venom** | 2 | Reduce effective target Poison **resistance by 25 percentage points**, minimum 0%, for Poison damage this character deals; immunity stays unchanged |
| **Total new** | **5** | **Abyssal 13 → 18/18** |

### Source basis

Abyssal Fortitude adapts the 2015 Wizards of the Coast
*Unearthed Arcana: That Old Black Magic*, p. 2
(https://media.wizards.com/2015/downloads/dnd/07_UA_That_Old_Black_Magic.pdf).
The source increases the total HP maximum by half of character level,
minimum 1. It does **not** provide half-level HP on each level-up.
No Abyssal Arcana spell lottery or extra languages are inherited.

Poison Tolerance isolates the condition-specific saving throw benefit
from the Tiefling *Infernal Constitution* feat (*Xanathar's Guide to
Everything*), instead of giving Abyssal the feat's additional ability-score
increase or Cold resistance. The native condition key is
`condition.poisoned`; it does not apply to every poison-related saving
throw and does not grant condition immunity.

Potent Venom adapts the concept of resistance penetration, but **not**
the complete resistance-ignore effect from the Poisoner feat.
The approved replacement is a bounded, percentage-point reduction.

### Potent Venom interpretation

The same Poison damage instance uses the original target resistance
and determines effective resistance as follows:

- If the target is **immune** to Poison damage, resistance is unchanged.
  This includes the 100% immunity threshold and any greater immunity
  notation the system supports.
- Otherwise, effective Poison resistance is
  `max(0, original Poison resistance percentage - 25)`.
- No Poison resistance reduction stacks with another application
  of **this characteristic**; it is evaluated once for each relevant
  damage instance, not accumulated on the target.
- The reduction applies only to Poison damage dealt by this character,
  regardless of whether its source is a spell, weapon, or other ability.
  It does not change other damage types and cannot create vulnerability.
- It is **25 percentage points**, not 25% of the target's resistance.

| Initial Poison resistance | Effective for damage dealt |
| ---: | ---: |
| 0% | 0% |
| 25% | 0% |
| 50% | 25% |
| 75% | 50% |
| 100% immunity | 100% immunity (unchanged) |

The reduction is applied in damage resistance evaluation, not as
persistent modification to a target's defenses.

## Three Tiefling Ancestries after this increment

| Direct Ancestry | Cost | Specialization |
| --- | ---: | --- |
| Abyssal | **18/18** | Poison defense, Poisoned saves, Potent Venom, Fortitude |
| Chthonic | **17/18** | Necrotic resistance and life siphoning |
| Infernal | **18/18** | Fire resistance and Flames of Phlegethos |

No updates to shared Tiefling Species, Chthonic, or Infernal feature
payloads, and no changes to the existing innate spells or spell
resources. Neither of the previously rejected Chthonic damage
conversion proposals is introduced.

## Implementation scope and verification

- Three canonical genes are explicitly priced at **2, 1 and 2**
  points; the trial policy version increments
  `biological-pricing-20261123-v9` →
  `biological-pricing-20261123-v10`.
- Canonical gene/price coverage rises from **90 genes / 95 price
  entries** to **93 genes / 98 price entries**, including five
  intrinsic slots.
- `NativeOriginBiologicalTraitShape` recognizes
  `hitPointMaximumBonus` and `damageResistanceReduction` as typed
  native characteristic payloads. Existing saving-throw projection
  handles the condition-specific Poison Tolerance advantage.
- `NativeOriginHitPointProgressionProjector` reads and validates
  the canonical half-level formula, returning the **total** level-based
  maximum-HP increase. It retains the existing linear Dwarven
  Toughness support. The read-only projector returns a preview; it
  does not mutate a persisted character.
- Focused xUnit tests validate the three direct Tiefling budgets,
  source-specific grants, level-derived HP values, Poisoned save scope,
  Potent Venom's percentage-point configuration and immunity cap,
  and preservation of Abyssal spell and Poison resistance grants.
  Existing Chthonic and Infernal allocation expectations are updated.
- **Automatic combat execution of Potent Venom remains integration
  work:** this increment does not install a runtime damage-application
  hook or change immunity calculation in combat. Similarly, saving-throw
  execution should consume the entitlement via the appropriate
  preexisting bridge; authored grant/projection is not proof of a
  live roll modifier.

Draft only. Do not merge or modify `main` without explicit approval.
