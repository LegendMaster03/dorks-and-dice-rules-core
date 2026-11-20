# Pillar 2 — Construct Assembly initial implementation increment

**Branch:** `feature/pillar2-construct-assembly-20261119`
**Baseline:** `4bb764f702bec348c111603c3accd18f48e50e0e`
**Scope:** Construct-specific component catalog and a bounded diagnostic assembly evaluator.

## Design boundaries

- No predefined anatomy, size, shape, number of limbs, fixed root frame, or mandatory power-manager part.
- Components are instances of selected native definitions. Each instance can connect to any other instance; material assignments are per component slot. Connections are undirected functional links. The initial evaluator treats each connected network as one distribution domain; richer directional and mechanical interfaces may be added as authored rules require.
- Material definitions are independent of component definitions. Authored per-slot material costs contribute to initial construction cost; missing material costs remain unresolved instead of silently becoming free. The initial records do not grant material bonuses or manufacture species traits.
- This bounded first slice does not yet implement physical quantities, size-dependent cost scaling, or material amount calculations. A per-slot cost is only a test fixture convention, not an approved economic formula.
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

## Follow-up: trusted passive-trait projection

The procedure-specific `NativeConstructOriginConstructionService` now provides
a narrow bridge from costed and validated native component instances to the
existing universal `NativeOriginConstructionEngine`:

1. A Construct Assembly request must use the selected-native `construct-assembly` method.
2. The configuration evaluator validates component identity, material slots,
   dependencies, cost and simultaneous power. An incomplete, invalid or
   overallocated assembly is **never** promoted into trusted Origin offers.
3. Only an explicit `installedOriginTraitIds` array authored on a native
   `construct-component` is projected. Each reference must resolve to a
   canonical `origin-trait` with executable mechanical fields. The catalog
   currently leaves this field empty until first-party Construct traits are
   individually approved and balanced.
4. The procedure adapter creates exact instance-qualified native candidates,
   which the common engine reconciles and manifests. The ordinary public Origin
   entrypoint still rejects fabricated native component candidates.
5. Client-supplied canonical candidate values cannot replace verified source
   values. The internal trusted-offer pathway is not exposed through the
   public source deconstruction API.
6. The initial mapping intentionally covers **permanent, passive** traits only.
   Powered or mode-dependent benefits require a separate runtime applicability
   layer; toggling operational power must not rewrite underlying Origin grants.
7. Separate instances retain distinct offer identities even if they reference
   the same canonical trait. A duplicate mechanical outcome is not silently
   stacked; the existing reconciliation rules determine whether additional
   authoring or table adjudication is required.
8. An incomplete custom Origin may preview authorized installed characteristics,
   but does not become playable without an effective creature type and game-size
   grant. No conventional Species or humanoid anatomy is made mandatory.

The included synthetic tests exercise positive authorization, forgery
rejection, overload failure, powered-state independence, repeated source
occurrences, choice-binding deferral, and custom foundation incompleteness.

**Not yet implemented:** authorized installed-component trait definitions in
the first-party catalog, powered-characteristic runtime projection, per-mode
behavior, intrinsic-supply authoring for a fully custom Construct body, design
and enhancement composition, quantities/size scaling, or progression and
installation cost policies. This bridge is not a complete playable procedure.

## Allowance authorization boundary

The passive Origin adapter requires an exact selected-native `initialConstructionAllowance`
on the `construct-assembly` method. An allowance provided by the client is
not trusted without that authored equality check. The real first-party method
has not received an approved allowance, so the current reference catalog
continues to fail closed until balancing is authorized. Synthetic tests use
fixture-only numbers.

The evaluator still supports `InitialConstruction: false` for gameplay
modification **preview**, but the Origin adapter refuses to turn that preview
into a permanent grant until an authorized acquisition and installation
procedure exists. Ordinary operating-state changes must not reconstruct the
permanent Origin.

## Runtime allocation and operating applicability increment

The Construct adapter now recognizes two distinct, catalog-authored ways for an installed component to supply an inherent Origin entitlement:

- `installedOriginTraitIds`: an installed, passive characteristic, independent of the current power allocation.
- `operationalOriginTraitIds`: an inherent characteristic acquired through installation but only **applicable** while its supplying component is operational.

Both fields must contain exact references to canonical native `origin-trait` definitions. The universal Origin engine remains responsible for issuing the effective grant and recording provenance; the Construct adapter does not mint one. Neither field authorizes an unreviewed mechanic, a new starting budget, or a choice-bearing trait. The first-party component inventory currently contains no published active trait mappings or balancing figures.

An **authorized initial** Construct Origin evaluation may return an in-memory `NativeConstructOperatingSession`. It holds a defensive copy of the validated component instances, connections, materials and already-issued Origin grant IDs. The session is explicitly excluded from JSON serialization and must be re-established at a trusted application boundary when loading persisted characters; it is not a serializable bearer token or independent proof of entitlement.

`EvaluateAllocation` accepts only overrides of component enabled states for **already installed** instance IDs. It rechecks dependency closure, power-network output and shutdown consequences using the assembly evaluator. The authoritative permanent Origin profile is not rebuilt or modified when power allocation changes. Unknown instance IDs fail closed without any applicable grants.

The resulting transient applicability view contains:

- `ApplicableOriginGrantIds`: inherently authorized grants usable under the current allocation, including passive installed grants.
- `InactiveOriginGrantIds`: inherently authorized grants temporarily unavailable from power, dependencies or upstream synergy suppression.
- The full assembly diagnostic, issue list and `IsOperatingConfigurationValid` status.

The common characteristic-synergy rules are **not** executed a second time. Instead, an already-issued synergy is temporarily inapplicable if one of its input grants is inapplicable; this propagates transitively through later synergies. Neither synergy definitions nor Origin grant records change during switching. If an allocation is incomplete or over capacity, no powered grant becomes applicable; consumers must check the overall operating configuration status rather than applying partial effects as though the configuration were approved.

These runtime results are *views*, not new gameplay entitlements or permanent character mutations. Persistent character modifications, powered abilities requiring configured choices, adjustable power distribution without global network overload rejection, authored operating modes, finite reserves/recharge, and mechanical consequences beyond existing shutdown identifiers are further procedure work. General source-neutral synergy evaluation remains in the shared Origin foundation.

### Synthetic acceptance additions

`NativeConstructOriginProjectionTests` now includes fixture-only tests for retained powered entitlements, active/inactive allocation, transitive synergy applicability, loss of supply, excess simultaneous demand, rejection of unknown component IDs, caller-side input mutation after authorization, and stable permanent Origin serialization.

## Authored operating modes (third implementation increment)

Operating modes are **construct-specific** runtime configurations, not new
universal Origination procedures or independently acquired characteristics.
Each installed native component may optionally declare an `operatingModes`
array. The selected mode is stored on that component **instance** as
`ModeId`, allowing two identical components to operate in different modes.

Each authored mode supports:

| Field | Behavior |
|---|---|
| `id` | Unique, path-safe identifier within that component |
| `powerDemand` | Overrides the component's general demand when present |
| `powerOutput` | Overrides the component's general output when present |
| `provides` | Replaces the component's available capabilities when present |
| `requiresAnyOf` | Replaces the component's functional requirements when present |
| `operationalOriginTraitIds` | Exact operational traits allowed to apply in this mode |

If an operating mode declares `provides` as an empty array, it provides no
functional capabilities in that mode. If `requiresAnyOf` is an empty array,
it has no mode-specific capability prerequisites. If either field is absent,
the component's general value is inherited. Power demand/output also inherit
general values unless a mode supplies a corresponding number.

Each `operationalOriginTraitIds` list on a mode must be a subset of the
component's catalog-authored operational entitlement list. Missing lists
activate **no** powered traits. An installed component may retain multiple
canonical Origin entitlements while only the currently selected mode makes
a subset usable. An operating mode never creates an entitlement.

**Example (synthetic design only):** A multifunctional arm may use a
`stowed` mode (no operating demand or specialized capability), a
`tether` mode (tether operation and appropriate demand), or a `blast`
mode (ranged mechanism and its own demand). The same assembly is present
in all three modes. No attack, movement bonus, or official item is granted
merely by defining these labels.

### Validation and runtime integration

- A component with authored operating modes requires an explicit selected
  mode ID in its installation. A component without such definitions rejects
  mode IDs. Mode IDs are unique, path-safe and bounded to 32 per component.
- The assembly evaluator validates **every authored mode's** trait list
  against the native component, including modes not currently selected.
  Undeclared operational traits fail closed.
- The evaluator resolves power, capability providers, requirements, and
  dependency closure from the chosen mode before computing operational state.
  Its component state records the mode and currently active canonical trait IDs.
- `NativeConstructOperatingSession.EvaluateConfiguration` accepts **only**
  per-instance enabled-flag and selected-mode overrides for a previously
  authorized construction snapshot. It does not add parts or change their
  materials, connections, configuration costs, source identities, or installed
  Origin entitlements.
- The existing `EvaluateAllocation` remains a compatibility shorthand for
  enabled-flag changes. Neither method persists a new Origin profile.
- No inherent grant is active solely because its canonical trait appears
  in another mode. Already-issued synergies lose runtime applicability when
  their authorized powered inputs become inapplicable. Origin provenance
  and historical construction records do not change.

### Open design boundaries

These are deliberately not approved or implemented here: required actions
or time to switch modes; operating-mode cooldowns; limited activation uses;
deploy/retract animations; authored constraints spanning multiple components'
mode selections; energy storage and recharge; full power management with
automatic load shedding; installation/modification economy; material quantity
scaling; and actual first-party mode power values or gameplay effects.

First-party reference components continue to have no approved balance values.
All mode behavior is demonstrated through isolated synthetic test fixtures.

## Reusable designs, enhancements, and power-source inventory

This increment expands the procedure's *reference catalog* while maintaining
the separation of component, material, design, enhancement and source.

- `components.json`: 31 component candidates covering structural support,
  articulation, actuation, manipulators, controls, sensory interfaces,
  equipment mounts, integrated weapon mounting, storage and transport.
  Names are design candidates; neither descriptions nor functional tags alone
  grant mechanical attacks, senses, armor, movement speeds, or other traits.
- `designs.json`: 4 reusable construction arrangements. A component instance
  can choose one optional design through `DesignId`.
- `enhancements.json`: 5 optional improvements. A component may select
  multiple **distinct** enhancement IDs through `EnhancementIds`, subject
  to the current bounded evaluator. No design or enhancement is duplicated
  for each possible material.
- `power-sources.json`: 6 previously discussed working concepts:
  artificial-life core, solar/rechargeable crystal, soul gem, Infernal Engine,
  arcane-consuming core and mechanical engine. A `powerSourceSlot` on a
  component houses one selected `PowerSourceId`. The housing and power
  mechanism are different definitions. A source must match its housing's
  authored compatible component capabilities.

### Executable interpretation

A design or enhancement must have an authored
`compatibleComponentCapabilities` list matching at least one of its
component's **base** capabilities. Unrecognized, mismatched, duplicate
enhancements and wrong catalog entity types are invalid, never automatically
treated as valid empty options.

An approved option may contribute additive `provides`, `requiresAnyOf`
and `powerDemand` values to the selected component. These are procedure-level
*functional* tags and dependencies, not canonical Origin traits; adding them
does not mint gameplay benefits. Selected modes still determine which base
capabilities and demands are active. Add-on power demand is incremental to
the selected mode. Power-source output comes from the catalog-selected
mechanism, rather than an amount submitted in the instance.

All installed component, material, design, enhancement and source costs count
toward the **starting construction allowance**, whether currently powered
or not. Every unresolved initial construction price remains unresolved,
not zero. Power-source output also remains unresolved until explicitly
authored; fuel supply, endurance and storage are **not** inferred from output.

Initial reference catalog definitions deliberately omit balancing prices,
source outputs, and most operating demands. The first-party catalog remains
nonplayable until those values and canonical characteristic mappings are
approved. Synthetic fixtures use explicitly **test-only** values.

Neither the number of structural components nor their arrangement is
prescribed. This increment does not define shape, size-dependent material
quantities, installation time, crafting access, default power-management
intelligence, or switching actions. More specialized compatibility requirements
may be added later without imposing a universal body plan.

### Test evidence

`NativeConstructCatalogCompositionTests` tests combined option and material
costs, independent design/enhancement selection, separate source output,
disconnected requirements, options when shut down, missing or mismatched
catalog references, duplicate enhancement rejection, unpriced options, and
unauthored power output. No test-only prices are published as first-party rules.

## Explicit power wiring and cross-network failure propagation

The assembly request can now optionally provide `PowerConnections` as
undirected, passive links between distinct **directly physically connected**
component instances. `ConnectedTo` continues to describe physical/functional
connectivity and authorizes capability dependencies across a connected assembly.
An explicit power link does **not** supply any capability, grant an Origin trait,
or imply an anatomical parent.

- `PowerConnections: null` preserves earlier assembly behavior: physical
  connections also carry power. This compatibility rule must not be mistaken
  for a general published rule that every attachment conducts power.
- `PowerConnections: []` explicitly isolates every component's power
  circuit. A nonempty list defines separate shared-output networks, using
  only the specified physical edges.
- Unknown endpoints, self-links, nonphysical links, reverse duplicates and
  excessive link counts fail closed. There is no implicit long-range power
  transfer between otherwise connected groups.
- Network overload is evaluated per **power** network rather than per
  functional network. Overload does not silently shed load or borrow energy
  from a disconnected source.
- After power evaluation, failed providers make dependent components
  inoperative, including dependents supplied by a separate power network.
  Those components remain installed; their Origin entitlements are unchanged.
  The transient operating session retains the approved wiring snapshot.

These links model **distribution connectivity only**. They do not introduce
energy storage, generation efficiency, voltage, directionality, fuel, energy
reserves, programmable priorities, or breaker/control behavior. In this
increment, a link itself is passive even if the connected component is off;
authoring powered switches or interruptible distribution is future work.
The feature makes no claim about published power quantities or balance.

Synthetic acceptance tests cover explicit and legacy connections, deliberately
unwired assemblies, invalid or duplicate paths, and dependency failure across
separate power networks. CI, rather than the local runtime, must validate
the full .NET test suite after the branch commit.

## Optional subcomponent composition and authored mounting interfaces

A Construct can be formed from any number of independent native component
instances and optional nested assemblies. `ParentInstanceId` and
`MountInterfaceId` are optional installation-time fields on a component
instance. Together they designate a single immediate containing instance and
one selected installation interface. They do not require a top-level root,
humanoid frame, particular number of components, or a fixed parent hierarchy.

Native component definitions may separately author `mountInterfaces` (usable
interfaces of a subcomponent) and `acceptsMountInterfaces` (interfaces a
parent can receive). A relationship is valid only when the exact selected
interface appears on both sides. An absent selected interface is incomplete;
unknown parents, unpaired selections, incompatible interfaces, and
containment cycles are invalid. Mounting capacity/weight restrictions are not
yet approved and are not inferred from interface names.

Mounting creates a physical edge in the existing undirected component graph,
so functional prerequisites can traverse a composite assembly. **It does not
imply an electrical connection** when explicit `PowerConnections` are
provided. Legacy requests without explicit power wiring continue to treat
physical connections as power paths for backward compatibility. Every
subcomponent retains its own native definition, material slots, design,
enhancements, construction costs, operating state, and exact instance
provenance. A powered-off parent remains a physical mounting point; functional
operation still depends on authored prerequisites.

The trusted operating session snapshots installation relationships alongside
components and wiring; enabling, disabling, and changing modes does not allow
a component to move between assemblies or reconstruct permanent Origin grants.
The first-party component reference catalog is unchanged; fixtures demonstrate
a configurable composite with multiple actuators, casing, structural segments
and independent materials. The selection of a mounting interface grants no
movement, extra attack, armor value, or other canonical characteristic.

Synthetic tests cover multi-subcomponent construction, repeated compatible
child instances, nested components, separate material costs, automatic
structural connectivity, explicit independent power wiring, invalid or absent
interfaces, missing parents, and cyclic containment.
