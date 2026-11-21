# Construct components

Component definitions are stored as one JSON object per file, grouped by
**functional component type**. These groups are an authoring/navigation aid,
not a mandatory anatomy, additional game-mechanical category, or restriction
on compatible parts.

Path contract:

```text
origination/construct-assembly/components/<type>/<component-slug>.json
```

The loader derives `kind: "construct-component"` and
`id: "construct-component.<component-slug>"` from that path before ordinary
catalog validation. Component IDs and all existing references remain stable;
moving a component between type directories does not change its ID.
A component filename must be globally unique across all type directories.

The JSON file retains its display `name`, explicit capabilities, requirements,
material slots, recipes, operating values, and review status. These are not
derivable from the path. Existing crafting recipes, designs, enhancements,
materials, and power sources remain in their respective catalogs.

The loader rejects malformed component paths and conflicting explicitly
authored identity metadata. Legacy component JSON arrays outside the new
directory remain supported for isolated test fixtures and other rule packages.
