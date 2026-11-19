# Pillar 2 — Construct Assembly initial implementation increment

**Branch:** `feature/pillar2-construct-assembly-20261119`
**Baseline:** `4bb764f702bec348c111603c3accd18f48e50e0e`
**Scope:** Construct-specific component catalog and a bounded diagnostic assembly evaluator.

## Design boundaries

- No predefined anatomy, size, shape, number of limbs, fixed root frame, or mandatory power-manager part.
- Components are instances of selected native definitions. Each instance can connect to any other instance; material assignments are per component slot. Connections are undirected functional links. The initial evaluator treats each connected network as one distribution domain; richer directional and mechanical interfaces may be added as authored rules require.
- Material definitions are independent of component definitions. The initial records do not grant material bonuses or manufacture species traits.
- Initial assembly allowance is separate from operation. Known component construction costs accumulate independently of operating state. Initial costs and an allowance must be authored before an initial configuration can be authorized. Later modification previews omit the initial allowance; gameplay acquisition, crafting and progression policies remain to be authored.
- Installed parts can remain disabled. A network may install more demand than its output can support, but the requested simultaneous operating configuration must fit its connected sources. No automatic load shedding or implicit infinite energy is assumed.
- Component prerequisites are conjunctions of groups of explicitly authored alternative capabilities. The evaluator supports chained dependencies without inventing canonical trait equivalence. An unpowered or disconnected provider ceases to satisfy dependent functions. Optional shutdown consequences are authored identifiers, not hard-coded character death or loss of consciousness.
- Components with missing construction cost or power demand remain unresolved for those decisions. No balance values have been invented for the initial first-party reference records.
- A mechanical part, its materials and its functional operating state are distinct from an effective inherent Origin grant. This increment does **not** issue grants or treat component capabilities as character traits. An explicit canonical characteristic authoring and a secure procedure-to-Origin integration pass are required before claiming playable construction.
- The universal `NativeOriginSynergyEvaluator` already exists in the merged foundation. Its derived characteristics must be based on authorized manifested grants, not on arbitrary component labels.
- Power output and finite energy storage remain distinct; this initial evaluator covers simultaneous output only and does not impose unapproved reserve/recharge formulas.
- General operating mode interference and comprehensive construction balance remain subsequent module authoring, not implicit new universal rules.

## Catalog

`native/origination/construct-assembly/` contains independent `construct-component` and `construct-material` native reference definitions. Their use as native references is selected; their numerical gameplay costs, effects and full creation readiness are **not** approved. These entries are intentionally nonplayable until missing mechanical policy is authored.

## Acceptance evidence

The initial evaluator and its test fixtures are intended to verify:

1. Arbitrary component numbers and connected networks without a humanoid template.
2. Costs and starting allowance applying to installed parts rather than only powered parts.
3. Ability to install more demand than can operate simultaneously, and reallocate by changing requested operating states.
4. No implicit connection between physically separate networks.
5. Dependency closure, shutdown consequences, material slot validation, and later modification preview.
6. Missing balance values remain explicit unresolved cases instead of free components.

## Integration boundary

The next implementation increment must map selected operational modules into **catalog-authorized Origin candidates and manifestations** using the merged common engine; it must not bypass that engine with independent grants or untrusted custom proposals. This preview alone is not a complete or playable Origination procedure. 
