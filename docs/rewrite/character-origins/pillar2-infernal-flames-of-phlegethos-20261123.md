# Pillar 2 — Infernal Tiefling, Flames of Phlegethos (2026-11-23)

**Status:** approved Infernal-only biological increment, staged as a draft
stacked on Human revision PR #204. Not merged.

## Decision and allocation

Grant `trait.flames-of-phlegethos` to the selectable
`ancestry.tiefling.infernal` as one inherited characteristic at **4 points**.
It is adapted from the Tiefling feat in *Xanathar's Guide to Everything*,
pp. 74–75, with the feat's ability-score increase expressly **omitted**.

| Source | Old total | New total |
| --- | ---: | ---: |
| Abyssal Tiefling — Poison | 13 | **13 / 18, unchanged** |
| Chthonic Tiefling — Necrotic | 13 | **13 / 18, unchanged** |
| Infernal Tiefling — Fire | 14 | **18 / 18** |

The shared Tiefling Species remains nonselectable independently and has
unchanged creature type, senses, Thaumaturgy, biological needs, and size.
Infernal retains 50% Fire resistance, Fire Bolt, level-3 Hellish Rebuke
(second-level free cast), level-5 Darkness, and the original selected
spellcasting ability. Those spell grants and resources are unchanged.

## Approved mechanical characteristic

Two linked effects are authored in one biological gene, not separately
purchased features:

1. **Fire spell damage:** When rolling Fire damage dice for a spell
   this character cast, each die showing 1 may be rerolled once. The
   new result must be used, including another 1. This applies to eligible
   Fire damage from any spell the character casts, not only inherited spells.
2. **Protective flames:** Whenever the character casts a spell that deals
   Fire damage, they may become wreathed in harmless flames until the
   **end of their next turn**. The flames shed Bright Light to **30 feet**
   and Dim Light an **additional 30 feet**. While active, any creature
   within **5 feet** that hits the character with a melee attack
   takes **1d4 Fire damage**. The retaliation requires no extra
   Action or Reaction and does not injure the character or their possessions.

Neither effect grants an ability-score increase, a separate spell, or a
limited-use resource. An Infernal Tiefling casting the inherited Hellish
Rebuke can trigger protective flames; the retaliation only applies to
melee hits that occur while they are active, not retroactively to the
attack which prompted that spell.

This is a **Fire-specialized** Ancestry feature and is not a shared
Tiefling characteristic. It does not grant Cold/Poison resistance or
features proposed for Abyssal and Chthonic.

## Implementation boundary

- Native catalog stores the two effects in `fireSpellDamageReroll` and
  `protectiveFlames`, both explicitly recognized by the biological
  trait shape validator.
- First-party trial policy advances from
  `biological-pricing-20261123-v7` to
  `biological-pricing-20261123-v8`.
- Canonical biological catalog now contains 89 priced genes and
  94 price entries, including five intrinsic entries.
- Focused xUnit tests check full direct Ancestry allocations, only
  Infernal's manifestation, exact reroll/retaliation/illumination
  parameters, no added ability-score increase or limited uses, and
  preservation of the Infernal spell progression.
- The current increment **does not** supply a runtime dice-roll
  interceptor, combat retaliation trigger, lighting system, or proof
  of executed gameplay behavior. Testing those integrations belongs to
  their respective gameplay systems.
- Infernal is provisionally balanced; the Abyssal proposal is not yet
  approved, and Chthonic remains to be designed.

No changes to the other Ancestries, general Feats catalog, or `main`.
