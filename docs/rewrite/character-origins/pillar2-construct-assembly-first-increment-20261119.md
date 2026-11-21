# Pillar 2 — Construct Assembly initial implementation increment


> **Current playtest follow-up (2026-11-20):** The earlier statements below that no starting allowance exists describe the pre-playtest state. The working branch now sets a **provisional 200-point** allowance and provides a [163-point basic starting Construct](pillar2-construct-basic-playtest-20261120.md). This does not approve the remaining recipe/material values for production.

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

- `components/<functional-type>/<component>.json`: 31 component candidates (previously one `components.json` catalog). The canonical ID and kind are now path-derived without changing their mechanical meaning. covering structural support,
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

## Power-network accounting and operational telemetry

Every evaluated assembly now reports an ordered `PowerNetworks` collection,
one entry per independently connected distribution circuit. Each entry
identifies its installed component instance IDs and distinguishes:

- **RequestedDemand:** sum of enabled, prerequisite-eligible demands before
  overload rejection (known subtotal only when demand is unresolved).
- **AvailableOutput:** sum of enabled, prerequisite-eligible source output,
  independent of the power an activated load actually draws.
- **OperationalDemand** and **OperationalOutput:** sums from the components
  that remain operational after overload and dependent-failure propagation.
- **RequestedHeadroom:** output minus demand, including negative values for
  a known overload. This is `null` if demand or source output is unresolved.
- **IsOverallocated:** a nullable overload conclusion. Unresolved demands or
  source output yield `null`, never a fabricated conclusion that power
  capacity is sufficient.
- **HasUnresolvedDemand** and **HasUnresolvedOutput:** explicit uncertainty
  markers, even when known subtotals are available.

The network ID is the lowest ordinal component instance ID in the circuit,
not a permanent identity to retain across modifications. Accounting uses
simultaneous power values only and does **not** assume storage, fuel,
recharge, unlimited endurance, transfer efficiency, or automatic load
shedding. The ledger is an evaluation result, not an independent authority
for Origin grants or an approved first-party balancing policy.

Synthetic tests cover a powered network with headroom, an overloaded
network with shutdown effects, isolated power networks, and unknown
catalog-authored source output. Existing manual allocation and runtime
session boundaries are unchanged.

## Continuous-power metadata and advisory priority load shedding

Native components may author a boolean `requiresContinuousPower`; malformed
flags are invalid. The flag is visible in evaluated component states and
identifies an installation that should not be shed by an automatic plan.
It does not make interruption physically impossible, grant limitless energy,
or specify a universal consequence. Existing native `shutdownConsequence`
identifiers apply to both requested and unexpected shutdowns.

`NativeConstructOperatingSession.PreviewPriorityShedding` accepts an ordered
list of optional component instances to shed (lowest preferred first) and
optional requested enabled states and operating modes. The simulation
only considers candidates in overloaded circuits. Each accepted shutdown
must reduce a *known* deficit and avoid taking a currently operating
continuous-power component offline, even through other dependencies.
Unknown, repeated, or protected shutdown candidates are rejected.
Unresolved native power quantities and invalid mode selections prevent
successful planning, rather than becoming hypothetical free energy.

The result contains the regular trusted operating-session evaluation,
suggested shutdown IDs, unpowered continuous-load IDs and planning issues.
It is **advisory only**: it does not mutate installation, execute a switch,
persist a policy, issue permanent traits, alter provenance, or imply that
a physical control system exists. Players retain the manual ability to
disable a critical system and face its authored consequences. Applying
any preview requires separately authorized runtime action and timing.
No first-party balance figures, storage/fuel, automatic controller devices,
or global Construct shutdown policy are introduced. Synthetic tests cover
load ranking, continuity protection, manual failure, insufficiency, forged
inputs, authored modes, synergy applicability and immutable Origin state.

## Externally serializable operating presets and recovery transitions

`NativeConstructOperatingPreset` stores a user-named set of operating
preferences as a complete, ordered list of installed instance IDs, native
definition IDs, enabled flags, and selected operating modes. It is plain
serializable data, **not** an Origin profile or an authority token.
`NativeConstructOperatingSession.CapturePreset` captures only configurations
which currently pass the native operating evaluator. A preset can be saved
outside the session by a caller, but there is no implied persistence service,
embedded Origin state, or server-side preset registry.

`EvaluatePreset` revalidates an imported preset against the session's
approved installation, requiring exact instance coverage, unique IDs,
the installed definition identity, and valid native operating modes.
Missing, duplicate, substituted, or otherwise invalid entries do not
receive baseline applicable-grant output. A structurally correct but
underpowered configuration may still be evaluated as an inoperative
state; it is not an authorized target configuration.

`PreviewPresetTransition` compares two independently revalidated
operating states. It reports newly restored/inoperative component
instances, gained/lost applicability of *previously authorized* Origin
grants, newly exposed native shutdown consequence identifiers, and
continuous-power interruptions/restorations. An underpowered source
state is allowed, so the preview can describe recovery from a real
shutdown without promoting that source state to a valid target.
Invalid or stale presets produce no actionable delta. The target
validity flag must be checked before use.

**Recovery of power is not recovery from every consequence.**
A restoration means that a component can function again; accumulated
damage, depleted resources, delayed shutdown effects, installation
changes, and other persistent game state are not reversed automatically.
The preview neither enforces actions or timing nor mutates the Origin.
Gameplay must still authorize any real operating transition. Synthetic
tests cover JSON serialization, exact installation rebinding, stale or
forged configurations, operating mode changes, restoration, critical
power loss, and nonmutation of permanent traits and synergies.

## Finite source resource-consumption preview

The procedure can now evaluate **one discrete resource-consumption step**
for power sources with native-authored `resourceKind`,
`resourceCapacity`, and `resourceUsePerStep`. The source's instantaneous
`powerOutput` remains independent of its resource units and remaining
endurance. These new fields are not assigned unapproved values in the
first-party power-source inventory.

`NativeConstructOperatingSession.PreviewResourceStep` receives a hypothetical
current balance for each installed source housing. Its native source must
declare a nonempty resource kind, positive finite capacity, and positive
per-step cost no greater than its capacity. A balance must be present for
each exact installed instance, range-bound to the catalog-authored capacity,
and contain no additional IDs. Missing resource authoring is unresolved;
it is **not** an implicit grant of infinite power. Sources with a different
operating principle that cannot be described by finite units require a
separate operating model.

Insufficient resource suppresses the selected source's output and
source-specific functional capabilities without uninstalling its housing.
The existing dependency, overloaded-circuit, critical shutdown, and runtime
Origin grant-applicability machinery then evaluates the consequences.
`UnavailablePowerSourceInstanceIds` is an opt-in operating-state
suppression overlay, validated against installed source housings; authored
operating modes cannot restore the output of an unavailable source.

A valid operating configuration consumes the exact native per-step amount
for each operational source. Disabled sources consume no resources. A
source that reaches zero at the end of a successful step becomes unavailable
*next* step. An invalid operating configuration does not consume resources
and cannot advance. `NativeConstructResourceStepPreview` provides
per-instance before/consumed/after values, any unavailable source IDs,
the resulting operating evaluation, and an explicit `CanAdvance` flag.

The resource balances are **caller-owned hypothetical state**, not trusted
proof of resource acquisition, past consumption, fuel replenishment, or
an authoritative character sheet ledger. No preview commits its output
or establishes time units, recharge, energy efficiency, refueling, automatic
redistribution, or delayed failure. Synthetic acceptance fixtures cover
consumption, the last available step, depletion effects and Origin grant
suppression, disabled sources, invalid balances, unbalanced source duration,
and forged outage references.

### Finite-resource acceptance boundaries

A resource preview can **not** clear an unavailable-source state previously
captured in an authorized operating-session snapshot. Such a source remains
unavailable regardless of the hypothetical balance supplied to a preview.
Restoring a previously unavailable source requires a separate trusted
recovery or resource-acquisition operation, not a different caller balance.

A Construct without any selected resource-modeled power source cannot
obtain a finite-endurance claim from `PreviewResourceStep`. Such a
simulation reports unresolved source-resource authoring instead of
interpreting a different, legacy source as unlimited endurance. Ordinary
instantaneous power evaluation remains supported.

## Conservative recovery through direct reservoir transfers

Finite native sources can optionally author two independent boolean
properties: `resourceTransferOut` and `resourceTransferIn`. These are
endpoint capabilities, not automatic resource regeneration. An imported
resource can only enter a reservoir of the **same exact resourceKind**.
Malformed transfer-port declarations invalidate a resource preview; missing
ports default to unavailable and do not imply an unrestricted container.

`NativeConstructOperatingSession.PreviewResourceTransfer` models the
hypothetical movement of a positive quantity between two **distinct,
directly physically attached** installed source housings. Either a native
parent/child attachment or an explicitly selected `ConnectedTo` link
establishes physical contact. The exporter must authorize transfer out;
the recipient must authorize transfer in. The source must hold sufficient
resource, the destination must have sufficient capacity, and resource kind
must match exactly. A disabled source can still contain a reservoir;
changing its contents does not turn its power output on.

The result lists every installed source with before, signed delta, and
after balance. **The sum of all balances is conserved.** The optional
`NextStep` contains a separate finite-resource operating preview using
the hypothetical balances after transfer. A transfer can therefore be
internally valid even when the resulting configuration remains
inoperative. Actual access to a transfer path, resource handling,
interaction timing, and any required equipment still require an
authorized gameplay procedure. Physical adjacency and transfer ports
are necessary modeling constraints, not permission to perform an action.

Resource movement does **not** clear an unavailable-source condition
previously captured in a trusted installation/operating-session snapshot.
Restoration of such a condition must occur through another authorized
procedure, not by submitting a larger hypothetical balance. Neither
`CanTransfer` nor `NextStep.CanAdvance` persists a resource ledger
or permits refueling from an external supply. This pass introduces no
external fuel creation, source-to-source energy conversion, recharge
efficiency, time units, or transfer-cost balancing.

Synthetic acceptance tests cover conservation and post-transfer operation,
nonmutation, underflow/overflow and forged inputs, required physical
contact, incompatible resource kinds, sealed and malformed transfer
ports, and preservation of trusted source outages.

## In-process trusted resource ledger and revision-checked commits

An internal `NativeConstructResourceLedger` now owns a private copy of
installed finite-resource balances. It can only be opened through the
trusted operating session's internal `OpenTrustedResourceLedger` entrypoint
after the host has separately authenticated the initial resource balances.
All balances are checked against the same native source definitions and
capacity restrictions as the existing previews. Client-supplied preview
balances are **not** accepted as authoritative state.

The ledger provides revision-numbered detached snapshots and two internal
atomic operations, `TryCommitStep` and `TryCommitTransfer`. Both compare
the caller's expected revision with the current in-process revision under a
single lock and recompute the full operation against the ledger's current
balances. A successful step consumes only the native-authored quantities
of operable power sources. A successful transfer conserves units and
does **not** also advance or consume the optional `NextStep` preview.
Unknown instances, exhausted sources, insufficient capacity, invalid
configurations, or stale revisions fail without changing any balance.
Each successful operation produces a receipt with the operation kind,
previous/new revision, and complete per-instance before/after deltas.

**Scope and authorization boundary:** the mutable ledger and its creation
and commit operations are internal Mechanics APIs, not client endpoints.
They require an already-authorized Construct operating session and an
external trusted host to validate the initial balances and gameplay
permission for each operation. The in-process lock prevents concurrent
double-spending within **one ledger instance**, but the revision is not
durable or globally unique. There is no database, no hosted persistence,
no cross-process transaction, and no authorization of physical interaction
or time advancement in this increment. A future host integration must
persist the ledger revision and balances atomically and bind them to the
authorized character/build; it must never construct independent writable
ledgers from unauthenticated client state.

Test-only friend assembly visibility permits Mechanics acceptance tests
without publishing a general resource-write surface. Synthetic tests
cover detached snapshots, repeated/stale revision conflicts, successful
consumption and conserved transfer, transfer without accidental step
consumption, invalid starting balances, forged/over-capacity transactions,
continuing authorized source outages, Origin immutability, and concurrent
attempts with a shared expected revision.


## Construct Assembly origination completion integration (2026-11-20)

This section supersedes earlier interim descriptions that deferred intrinsic
authoring, configured characteristic selection, and option-derived trait
projection. It describes the integrated mechanical procedure; it does **not**
approve first-party numeric balance or publish a playable Construct.

### Trusted integration with the universal Origin procedure

1. Select the existing native `construct-assembly` method. Its native
   `initialConstructionAllowance` must be an authored nonnegative number
   exactly matching the initial build's requested allowance. A draft or an
   externally asserted budget never becomes an authorized first-party Origin.
2. Assemble any number of component instances with independently selected
   materials, optional design, distinct compatible enhancements, selected
   native power source, optional mounting hierarchy and physical/power
   connections. The existing evaluator validates compatibility, dependencies,
   total installed costs, power and initial allowance before authorizing
   any Origin candidates. A component need not be powered to count toward
   the construction budget.
3. For a custom Origin, the method may author `constructIntrinsics` containing
   a native `creatureType` and either a fixed `size` or a finite `sizeChoice.from`
   array. The type must resolve to a native creature type allowing this
   origination method. These become trusted *intrinsic candidates* in the
   universal engine, with normal manifestations, finite choices, reconciliation,
   provenance and effective grant records. A process-type assertion alone
   is still not an intrinsic grant; no anatomy or size is inferred from parts.
   Without these or other approved intrinsic sources, the custom procedure
   remains incomplete.
4. Exact `installedOriginTraitIds` and `operationalOriginTraitIds` on native
   component, material, design, enhancement and power-source definitions
   produce instance-qualified **canonical** Origin trait candidates. Each
   candidate records the actual selected native source, its occurrence within
   the installation, and its canonical mechanical definition. Descriptive
   capability tags do **not** become traits automatically.
5. Supported finite `abilityChoice` and open-catalog `chooseFrom` selections
   are presented through existing `NativeOriginChoiceRequirement` and
   `NativeOriginManifestation` contracts. Finite bounds and source identity
   are checked by the common engine; open catalogs still need trusted
   eligibility evidence. Supported `originBindings` use the existing
   dependent binder; Construct instance identities prevent choices from
   being borrowed across distinct installed components. Unsupported
   authoring remains unresolved, never treated as a silent free grant.
6. Once the universal Origin engine authorizes the resulting profile,
   an internal operating session keeps a defensive snapshot of installed
   parts and permanent grants. It uses verified operational candidate IDs
   to suppress only the applicable effects while power, operating mode or
   functional dependencies change. Changing modes, disabling a source,
   or transferring resources does not rewrite permanent Origin entitlements.

### Distinct source/property categories

- **Construction only:** material/component/option prices, installation
  allowance, assembly attachment and cost accounting.
- **Structural/functional:** explicitly authored `provides`,
  `requiresAnyOf`, compatibility, mounting interfaces and power networks.
- **Permanent Origin entitlements:** traits and intrinsically selected type
  or size manifested and authorized through the universal Origin engine.
- **Operational applicability:** native traits whose permanent entitlement
  remains installed but whose active effects require an operational provider.

A gameplay modification can still be previewed without initial allowance,
but does not issue an updated permanent Origin; a separately authenticated
acquisition and installation procedure is required.

### Synthetic acceptance and unresolved authoring

Fixture-only tests exercise a custom Construct with material, design,
enhancement, power source, configured traits, intrinsic size choice and
component-level provenance; verify cost against an explicitly fixture-authored
allowance; reject unauthorized choices, forged references and incompatible
states; and assert that changing power does not alter permanent grants.
Additional focused tests verify configured trait dependencies do not
cross into another component instance.

**Remaining content authoring:** Reviewed canonical traits and compatible
mechanical effects for the first-party component/material/design/enhancement/
source catalog; first-party `constructIntrinsics` policy or another approved
source of effective creature type and game size; published examples and
guidance on compatible assembly configurations.

**Unresolved numerical balancing:** Initial construction allowance,
component/material/design/enhancement/power-source construction costs,
source outputs, operating demands, and mechanical trait values/limits.
The published candidate catalog remains intentionally unresolved and
must not be authorized by inserting fixture-only values.

**Separate future gameplay dependencies:** Durable resource persistence and
host authorization, post-creation crafting/acquisition/installing, additional
power-management policies, injury and delayed shutdown consequences, and
the unified Character Origins UI. None are prerequisites to demonstrating
mechanical initial origination with synthetic native definitions.

This increment stops at mechanically integrated creation and acceptance
coverage. It does not implement the complete Construct gameplay simulation.


## Recipe-derived starting-resource quotation (candidate integration, 2026-11-20)

The native \`construct-assembly\` method can opt into
\`initialConstructionCostModel: "recipe-inputs"\` and author a
\`startingResourceUnitCosts\` object (native material IDs to nonnegative
integer values). This is a **synthetic-tested infrastructure option**, not an
approved first-party allowance or resource pricing schedule. The current
first-party definitions intentionally do not select this mode or invent values.

In this mode each installed component, chosen design/enhancement and selected
power source resolves its exact native \`craftingRecipeId\`. The referenced
native \`crafting-recipe\` definition must identify the corresponding
\`producesDefinitionId\`. Recipes may author flat \`inputs\`, or \`stages\` with
\`stepId\`, \`competencyId\`, \`minimumRanks\`, \`dependencies\`, \`inputs\`,
and \`outputs\`. Each recipe material entry has a positive \`amount\` and
either a fixed \`materialId\` or a \`materialSlot\` resolved from its installed
component selection. The shared \`NativeCraftingProcess\` contract validates
stage dependencies and cycles. Resource quotation processes those stages in
dependency order and credits an intermediate output only against later inputs
of **the same recipe invocation**. It does not also charge the finished part's
legacy \`constructionCost\` or the selected material's legacy per-slot price.
Different installed instances repeat their own recipes.

The resulting external resource quantities are multiplied by the native
method's starting values and checked against its existing trusted
\`initialConstructionAllowance\`. Missing recipes, missing input amounts,
unpriced required inputs, invalid products, or malformed stage graphs cannot
authorize creation. An incomplete quote is not assumed to be free.

**Boundaries:** A quote is neither inventory authorization nor a completed
manufacturing check. Starting-character history need not simulate every NPC
craft roll. The creator's verified expertise, recipe knowledge, facilities
and material acquisition remain separate responsibilities of the ordinary
crafting system. Later gameplay modification still requires an independently
authorized acquisition, crafting and installation workflow. Neither the
initial allowance nor recipe quotation is a permanent upgrade ceiling.
No Construct-specific crafting engine, persisted inventory mutation, or
first-party numerical balance values are introduced by this integration.

The assembly result exposes a detached `RequiredConstructionResources` bill
for recipe-input starting construction. It is not an authorized inventory debit.

A later-modification assembly preview in recipe-input mode may also quote
required ingredients, but does **not** apply the historical starting allowance
or authorize ownership, manufacture, installation, or new permanent traits.

The selected material for every component material slot must be represented
by at least one manufacturing input through its `materialSlot` binding;
omitting a slot cannot create a free material-derived characteristic.
