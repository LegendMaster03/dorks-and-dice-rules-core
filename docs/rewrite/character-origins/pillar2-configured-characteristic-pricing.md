# Pillar 2 — Configured Characteristic Pricing

**Status:** Allocation mechanism; no approved numerical allowance or characteristic prices.

## Scope

The initial-creation allocation ledger accepts one host-owned policy for direct Species presets and mixed/custom biological Origins. It evaluates the effective grants in the reconstructed baseline, not donor offers or latent inherited genes. Existing baseline revisions and later gameplay changes do not retroactively debit the initial allowance. The allocation gate, its authenticated DM rulings, and the universal Origin construction workflow are unchanged.

A price entry owns one canonical member key (\`intrinsic:size\`, \`trait:trait.some-id\`) or an inseparable same-contributor bundle. The new optional \`Configurations\` list permits the same entry to select **one replacement load** based on actual effective configuration. Variant loads are never additive to their containing base load. A separate \`InteractionPrices\` entry still adds a declared cross-characteristic premium exactly once when all required effective keys are present.

### Authoring contract

\`\`\`csharp
new NativeOriginAllocationPrice(
    "size", 1, ["intrinsic:size"],
    Configurations: [
        new("small", 2, 10, [
            new("intrinsic:size", "value",
                NativeOriginAllocationComparison.Equals,
                JsonSerializer.SerializeToElement("Small"))
        ]),
        new("medium", 3, 10, [
            new("intrinsic:size", "value",
                NativeOriginAllocationComparison.Equals,
                JsonSerializer.SerializeToElement("Medium"))
        ])
    ]);
\`\`\`

All numbers above are illustrative test values, not approved balance decisions.

A condition specifies a member of the price entry, a slash-separated selector path, a comparison (\`Equals\`, \`Present\`, or \`Absent\`), and an expected JSON value only for \`Equals\`. The path roots are:

| Root | Read from | Example |
| --- | --- | --- |
| \`value\` | Effective grant's final \`EffectiveValue\` | \`value\` (size or speed), \`value/uses/recharge\`, \`value/duration\`, \`value/movementMode\` |
| \`parameters\` | Final bound grant parameters | \`parameters/damageType\`, \`parameters/area\` |
| \`choices\` | Resolved choice IDs mapped to arrays of selected option IDs | \`choices/selection\` |
| \`definition\` | Native canonical \`origin-trait\` definition's complete \`trait\` object | \`definition/damage/progression/0/dice\` |
| \`resourceOwners\` | Effective resource binding names, with boolean \`true\` values | \`resourceOwners/uses\` |

Path tokens use JSON pointer escapes (\`~1\` for \`/\`, \`~0\` for \`~\`). Array segments use zero-based indices. Equality compares the complete JSON value at the selected path. \`Present\` and \`Absent\` distinguish missing fields, such as an activation limit absent from a permanent ability; they do **not** compare against JSON null. Canonical definitions describe authored complete progression. Configurable source bindings and actual selected choices must be matched through \`parameters\` and \`choices\`, not inferred from a canonical template.

A configuration matches only if **all** its conditions hold. The unique matching configuration with the highest explicit \`Priority\` wins. Two matching configurations at the highest priority make the policy **invalid**, even if their loads happen to agree. Duplicate conditions on the same member/path, unknown roots, invalid expected values, duplicate configuration IDs, and nonpositive loads also invalidate a price entry. Priorities are policy-author-defined; no hidden specificity scoring occurs.

**No implicit fallback:** if \`Configurations\` is present and no configuration matches, the grant or bundle is unresolved, and a required creation gate fails closed. \`AllowUnmatchedConfigurationFallback: true\` explicitly permits use of the entry's ordinary \`Load\`; its use should be reviewed whenever that fallback may underprice a configuration. Omit \`Configurations\` entirely for an unconditional fixed-price entry. An empty list is invalid, not equivalent to an unconditional entry.

### Complete abilities and accounting

The policy author must price the **whole effective characteristic**: immediate and future unlocks, action economy, usage/recharge, duration, restrictions, resource relationships, and any meaningful selected spell or feature packages. The selector mechanism does not infer these factors' economic values. A native definition plus configuration paths may distinguish packages without introducing Species-specific price tables. Unrecognized or unresolved mechanics remain unresolved even if someone supplies a numeric price. Only effective grants receive charges; multiple donor offers for one consolidated grant are not multiple benefits.

Bundles require one grant per member key from a shared contributor; their selected configuration price covers the whole bundle. Optional interaction charges remain explicit and are evaluated separately. Disadvantages do not automatically generate negative prices or refunded points. Policy authors must decide whether a limitation belongs inside a whole-characteristic price or an inseparable bundle rather than mechanically awarding points.

No prices or allowance in this document are a gameplay balance decision. Numerical calibration, gameplay impact estimation, and broad acceptance testing belong to a later WorkChat analysis.
