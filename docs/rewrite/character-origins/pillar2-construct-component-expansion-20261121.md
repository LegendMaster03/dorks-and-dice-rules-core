# Pillar 2 — Construct component expansion (November 21, 2026)

**Status:** Native catalog implementation, candidate component mechanics.
**Base:** `refactor/construct-component-type-directories-20261120`.
**Scope:** All 13 reusable component concepts identified by the Construct
creature survey, including swimming propulsion subsequently approved.

## Native additions

| Category | Component | Provides | Canonical movement trait |
| --- | --- | --- | --- |
| `locomotion` | `construct-component.climbing-attachment` | `climbing-interface` | `trait.construct.climbing-movement` |
| `locomotion` | `construct-component.flight-propulsion` | `flight-propulsion` | `trait.construct.flying-movement` |
| `locomotion` | `construct-component.excavation-drive` | `excavation-interface` | `trait.construct.burrowing-movement` |
| `locomotion` | `construct-component.aquatic-propulsion` | `aquatic-propulsion` | `trait.construct.swimming-movement` |
| `weapon-systems` | `construct-component.projectile-launcher` | `projectile-launcher` | — |
| `utility` | `construct-component.substance-projector` | `substance-projection-interface` | — |
| `weapon-systems` | `construct-component.energy-discharge-unit` | `energy-discharge-interface` | — |
| `power` | `construct-component.energy-accumulator` | `energy-storage-interface` | — |
| `defense` | `construct-component.protective-field-projector` | `protective-field-interface` | — |
| `utility` | `construct-component.spell-storage-module` | `spell-storage-interface` | — |
| `utility` | `construct-component.repair-assembly` | `repair-interface` | — |
| `manipulation` | `construct-component.restraint-mechanism` | `restraint-interface` | — |
| `control` | `construct-component.external-control-interface` | `external-control-interface` | — |

New `weapon-systems/` and `defense/` directories distinguish operational
weapons and active defenses from preexisting *mounts* and passive *enclosure*.
Other parts use existing categories. Stable IDs are derived from the filename,
while `componentCategory` derives from the directory, through the existing
native catalog loader. The general `kind` is `construct-component`.

## Movement contract

Climbing, flying, burrowing, and swimming components each link one distinct
canonical `origin-trait` with `movementMode` set to `climb`, `fly`,
`burrow`, or `swim`. Their speed in feet is intentionally **not yet
authored**. A new Construct Origin compiler check flags missing or malformed
`speedFeet` as unresolved evidence; these cannot currently grant a usable
movement speed. Movement projection into Character Sheet mechanics and
physical load calibration also remain outstanding.

Operating one such part never grants walking speed, hover, breathable water,
permanent tunnels, or universal terrain traversal. Multiple installed
locomotion systems do not add speed values together. They remain governed
by general component power, connectivity, and authorization rules.

## Limits of this implementation

- No new numerical component cost, manufacturing recipe quantity, power
  demand, movement speed, range, damage, Armor Class, healing, charge
  capacity, recharge rate, or saving throw DC is invented.
- None of the new components specifies `craftingRecipeId` or `powerDemand`.
  With the first-party recipe-input cost model, attempting to include one
  in an initial Construct build produces the existing unresolved
  manufacturing and power diagnostics; no zero-cost or zero-demand fallback.
- The advanced physical components expose distinct `provides` tags for
  future designs and dependency rules, but do not issue unsupported combat,
  spellcasting, damage, defense, healing, or control grants.
- An energy accumulator does not output power until a source-authorized
  charging/discharging contract exists. The projectile launcher is different
  from an equipment mount, and a substance projector need not be a weapon.
- Existing component, material, power-source, enhancement, and design
  catalogs remain unchanged. No fixed body topology is introduced.

## Follow-up authoring

Complete manufacturing recipes and power requirements with sourced material
and competency choices, then evaluate candidate movement speeds against body
mass and movement support. Only after mechanics are approved should the
movement modes become effective Character Sheet grants. Advanced effect
integration into shared combat, equipment, spell, repair, and control
procedures remains separate work.

This catalog pass adds reusable physical design primitives; it does not
claim that all thirteen are fully playable at character creation.
