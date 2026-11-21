# Pillar 2 — Construct component expansion (November 21, 2026)

**Status:** Native catalog implementation, candidate component mechanics.
**Base:** `refactor/construct-component-type-directories-20261120`.
**Scope:** The complete stat-block-survey candidate inventory:
15 new physical components, 8 additional construction designs,
17 component enhancements, and 10 construction materials.
Includes the four previously approved movement forms.

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
| `control` | `construct-component.operator-station` | `operator-station-interface` | — |
| `storage-and-transport` | `construct-component.deployable-unit-bay` | `deployable-unit-interface`, `transport-interior` | — |

New `weapon-systems/` and `defense/` directories distinguish operational
weapons and active defenses from preexisting *mounts* and passive *enclosure*.
Other parts use existing categories. Stable IDs are derived from the filename,
while `componentCategory` derives from the directory, through the existing
native catalog loader. The general `kind` is `construct-component`.

## Existing component families: reviewed specializations

New components are reserved for mechanisms with a distinct physical role.
The following stat-block-derived variants belong to the already implemented
design/enhancement architecture rather than becoming independent anatomy:

| Existing family | New candidates | Native representation |
| --- | --- | --- |
| Sensing | Vibration detection, active ranging, magical detection, targeting | 4 enhancements |
| Manipulation | Compliant arrangement, claws, precision, interchangeable tools | 1 design and 3 enhancements |
| Ground locomotion | Wheeled, tracked, multi-legged, rolling-body configurations | 4 designs |
| Protective surfaces | Layered plating, hazard-adapted lining, energy absorption, coverage adjustment | 1 design and 3 enhancements |
| Control | Distributed arrangement, programmed instructions, supervisory link, remote commands | 1 design and 3 enhancements |
| Crew and operators | Multi-operator arrangement, protection, access, environmental support, operator controls | 1 design and 4 enhancements |

The physical operator station and deployable-unit bay are independently
installable hardware. They remain distinct from a module for consciousness
or a new player-controlled creature. Remote signals, independent actions,
control scope, occupant capacity, and active effects are not inferred
from their mere installation.

All new options declare compatible **existing base component capabilities**
so that the ordinary evaluator can validate their applicability. They
remain candidates without approved crafting recipes or final power demands.

## Additional material candidates

The catalog now includes clay, terracotta, ceramic, glass, bone,
preserved tissue, amber, wax, paper, and wicker. These are added to
`materials.json` with qualitative physical profiles but without a
fictitious unit mass or purchase price. Their use in recipes
must preserve unresolved ingredient pricing and physical-mass reporting
until those values are authored.

Existing `construct-material.cloth` and
`construct-material.alchemical-fiber` already represent the textile
and flexible-fiber family, so duplicate aliases were not introduced.

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
- The original 31 component payloads and the six power-source definitions
  remain unchanged. This pass extends the component, material, enhancement,
  and design catalogs without introducing a mandatory body topology.

## Follow-up authoring

Complete manufacturing recipes and power requirements with sourced material
and competency choices, then evaluate candidate movement speeds against body
mass and movement support. Only after mechanics are approved should the
movement modes become effective Character Sheet grants. Advanced effect
integration into shared combat, equipment, spell, repair, and control
procedures remains separate work.

This catalog pass adds reusable physical design primitives and compatible
configuration options; it does not claim that all fifteen new components
or the new options are fully playable at character creation.
