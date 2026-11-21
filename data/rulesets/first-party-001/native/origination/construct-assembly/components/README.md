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
| `integrated-weapons` | Specialized melee, projectile, and energy weapon mounts |
| `storage-and-transport` | Storage compartments and passenger accommodation |
| `utility` | Maintenance ports and auxiliary utility mechanisms |

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
