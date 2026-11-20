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
