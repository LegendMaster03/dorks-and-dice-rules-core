# Construct components

One canonical component definition is stored per JSON file:

```text
origination/construct-assembly/components/<category>/<component>.json
```

The folder supplies `componentCategory`; the filename supplies the stable
`id: "construct-component.<component>"`. The loader also supplies the
general `kind: "construct-component"`. These fields are available in the
loaded catalog but are not repeated in individual files.

## Functional categories

These directories follow the roles already described by Construct Assembly's
component capability and reference catalog. The category supports catalog
discovery and future UI filtering; it does **not** grant capabilities, supply
power, establish prerequisites, or prescribe anatomy.

| Category | Purpose |
| --- | --- |
| `structure` | Load-bearing frame, limb, and wing assemblies |
| `articulation` | Joints and articulated structural links |
| `actuation` | Actuators, circulation, and mechanical motion transmission |
| `locomotion` | Locomotion hardware |
| `manipulation` | Manipulatory interfaces |
| `control` | Primary and auxiliary controllers and coordination interfaces |
| `sensing` | Sensory and visual hardware |
| `power` | Power-source housing and distribution interfaces |
| `enclosure` | Casing and protective coverings |
| `equipment-mounts` | General equipment and shield mounting interfaces |
| `integrated-weapon-mounts` | Specialized melee, projectile, and energy weapon mounts |
| `storage-and-transport` | Storage compartments and passenger accommodation |
| `utility` | Maintenance ports and auxiliary utility mechanisms |
| `weapon-systems` | Operational projectile and energy-delivery hardware, distinct from mounts |
| `defense` | Active defensive field mechanisms |

This classification follows the component's primary role. Components may
expose capabilities spanning multiple roles: `provides`, `requiresAnyOf`,
material slots, attachments, and power networks remain independently authored.
The category is not inferred from a single capability tag and does not replace
any mechanical compatibility check.

Stable component IDs and recipe links do not contain the folder category.
Changing a category does not rename a component ID, although it changes
the catalog's categorized content and source path. Filenames must remain unique
across categories. The loader rejects invalid paths and conflicting identity or
category metadata.

Each file retains its display name, description, explicitly authored mechanics,
crafting reference, and provisional review status. Other native Construct
catalogs (materials, designs, enhancements, recipes, and power sources) remain
independent. Legacy component arrays outside this dedicated directory continue
to load for isolated fixtures and other compatible rule packages.

## Candidate extensions (November 2026)

Fifteen additional component definitions cover climbing, flight, excavation,
aquatic propulsion, projectile launchers, substance delivery, energy discharge,
energy accumulation, protective fields, spell storage, repair, restraints,
external control, operator stations, and deployable-unit bays. The four movement components refer to canonical
`origin-trait` movement-mode candidates. No positive numerical movement speeds
have yet been authored, so these traits cannot currently authorize movement.

These new components intentionally have **no manufacturing recipes or power
demands yet**. The assembly evaluator reports those missing values as
unresolved rather than pricing the parts at zero or treating unknown power
use as zero demand. Capability identifiers are operational interfaces,
**not automatic attacks, conditions, defenses, healing, resource creation,
or new character actions**. See
`docs/rewrite/character-origins/pillar2-construct-component-expansion-20261121.md`.

## Related authored options and materials

The stat-block survey also identified variation within existing component
families. Instead of duplicating entire components for each use, the
first-party catalog now includes **eight candidate designs** for wheel,
track, multi-legged and rolling-body locomotion, compliant manipulation,
layered plating, distributed control, and multiple-operator control.

**Seventeen candidate enhancements** add interfaces for vibration,
active ranging, magic detection, targeting, claw or precision grips, tool
couplings, hazard barriers, energy absorption, coverage, programmable
instructions, supervisory and remote commands, and occupant facilities.

**Ten candidate construction materials** extend the material catalog to
clay, terracotta, ceramic, glass, bone, preserved tissue, amber, wax,
paper, and wicker. Existing cloth and alchemical fibers already support
the previously identified textile/fiber family. New materials do not
specify recipe unit mass or initial construction prices, so their
physical load and ingredient cost remain unresolved rather than free.

These designs and enhancements are catalog-authored selections of
existing components; capability tags are not mechanical entitlements.
The `componentCategory` directory grouping does not classify materials
or equipment options. Nothing in these additions forces a body shape,
prescribed ancestry, control hierarchy, or magical animation method.
