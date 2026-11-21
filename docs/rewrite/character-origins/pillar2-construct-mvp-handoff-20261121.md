# Construct Assembly — Character Origins MVP handoff

**Status:** Provisional Construct procedure slice for the Character Origins
tester release, November 21, 2026. This is **not** the criterion for
completion of the complete 46-component gameplay catalog or full
Construct production release.

**Scope:** Test the Construct Assembly Origination procedure inside the
existing **universal Character Origins engine**. Biological Origination
and the unified release workflow are owned by the separate integration
effort. Do not branch off to create a new Character Origins framework,
separate crafting system, or complete Construct-specific gameplay UI.

## Purpose of the MVP

A tester should be able to make meaningful **initial construction
choices** (component selection, materials, power sources, designs,
enhancements and mounting where supported), see an ingredient quotation
and the installed/operating distinction, resolve constraint issues,
manifest source-verified traits, and reconstruct a coherent Construct
Origin using exactly the same Origin construction mechanism as other
Origination procedures.

The MVP **does not need** all 46 cataloged components to have their
advanced combat or spellcasting abilities implemented. Unsupported
components may remain in the full catalog but should not be represented
as fully playable within this initial tester walkthrough.

## Four concrete construction examples

The test manifest is
`data/reference/construct-origination-mvp-20261121.json`.

| Reference | Provisional points | Functional contrast |
| --- | ---: | --- |
| `construct-basic-starting-build-20261120.json` | 163 / 200 | Medium mechanical Construct, 7 power demand / 8 supply; source-granted ordinary communication, optical sight, manipulation and nominal 30-foot walking |
| `construct-small-starting-build-20261121.json` | 147 / 200 | Small frame recipe variant instead of Medium; 16 fewer candidate points and 28 lb less known retained mass; changes source-verified `size` intrinsic |
| `construct-lightweight-starting-build-20261121.json` | 187 / 200 | Replace eight steel frame-material units with darkwood; recipe cost increases by 24 points, retained-material mass **lower bound** decreases by 40 lb |
| `construct-armored-starting-build-20261121.json` | 187 / 200 | Add one passive steel protective-plating component assigned to `front-hull`; recipe cost increases by 24 points and known retained mass increases by 42 lb |

**Important:** Those amounts come from the **current candidate native
ingredient quotes**. The body-wide total mass can remain incomplete.
A passive protective surface has provenance and a material identity,
**not** an automatic AC, damage reduction, hit-location bonus, or
protection-stack entitlement. The material comparison does not
establish final speed or carrying capacity.

The standard ground locomotion trait already authored a **nominal
30-foot walking speed**. `NativeOriginMovementModeProjector.ProjectConstruct`
now reports that walking mode from the verified, *currently active*
component grant using the same view shape as the other Construct
movement modes. Disabling the drive removes its current walking
availability but does not delete installed equipment or rewrite the
permanent Origin grant. This remains a read-only Character Origins
projection, **not** the final Character Sheet movement adapter.

## Supported source scope

- **Mechanical Engine:** Preferred self-contained MVP power source:
  8 normal output, one daily mechanical-fuel charge using the existing
  general ration/deprivation evaluator. The native assembly and
  operational trait views work now.
- **Solar Collector:** DM-guided optional comparison; the source requires
  externally verified light/storage state and the exhaustion fallback
  recorded in the source's approved design.
- **Soul-Powered Core:** DM-guided optional comparison; the DM determines
  usable soul energy. Automatic involuntary capture and soul custody
  are separate from initial assembly. No fixed soul-consumption ledger
  or storage cap should be invented.
- **Infernal Engine:** May be displayed as a candidate source with
  an 8-unit baseline and long-rest fuel requirement. Empowerment's
  once-per-turn fire-damage **amount and qualifying attacks** and the
  ordinary fuel-shortage penalty remain unfinalized and must not
  become an automatic combat bonus.
- **Arcane-Consuming Core:** Exclude from the unguided tester slice
  until its fuel and operating requirements are designed.

Source-specific runtime context must not be bypassed by treating the
first-party `powerOutput` catalog value as proof of live fuel.

## Minimal crafting/modification demonstration

Use the **existing crafting recipes and resource quoter**. The
assembly evaluator already returns
`RequiredConstructionResources` and `KnownConstructionCost`,
and validates recipe-bound variants, materials, designs, enhancements,
and power-source choices. The 200-point allowance is a **creation**
constraint on ingredient purchases, not a lifetime spending cap.

The four references may be re-evaluated as alternate initial
assemblies to demonstrate different quotations and Origin effects.
For a DM-guided gameplay modification, evaluate the proposed
post-creation assembly using the existing non-initial construction
preview, then independently account for the **actual new component
recipes and acquired items**. Do **not** subtract whole-body before
and after ingredient totals as a fabrication or salvage rule. A
preview does not authorize inventory debits, duplicate parts,
persistent installation, or new Origin grants. Those transactions
must be handled by the shared host as integration progresses.

Advanced workshops, build time, material recovery, part salvage and
repair economics belong in a later general crafting pass.

## Boundaries deliberately left unresolved

1. **Gross load and movement penalties:** The common physical-load
   evaluator can distinguish evidence, but the first-party
   locomotion support capacity has not been approved. Do not
   reinstate the withdrawn 300-lb drive allowance or 30/15/0
   movement tiers. When exact loading cannot be established, show
   the unresolved status and ask for table adjudication.
2. **Material protection:** Material and region evidence do not mint
   AC, resistance, DR, free armor or fixed hit-location behavior.
3. **Advanced modules:** The weapon systems, accumulators, protective
   fields, spell stores, repairs, deployable units and control
   arrangements have native interfaces/recipes, not all resulting
   gameplay actions.
4. **Persisted gameplay effects:** Initial Origination views are
   not a replacement for the Character Sheet's authoritative
   movement, inventory, condition, rest, and crafting transactions.
5. **Balance:** The reference budget, material quantities and costs
   are provisional and intended to produce targeted tester feedback.

## Candidate recipe quotation review

The four manifests were independently recalculated against the current
first-party manufacturing recipes, ingredient unit prices, and stage
dependencies (including intermediate outputs consumed by later stages):

| Build | Quoted external ingredient points | Recipe stages reviewed |
| --- | ---: | ---: |
| Standard Medium | 163 | 10 |
| Small | 147 | 10 |
| Lightweight | 187 | 10 |
| Steel-plated | 187 | 11 |

All match their expected values and fall within the provisional
200-point creation allowance. An intermediate such as
`crafting-material.activated-medium` is produced by an earlier
manufacturing stage; it is not an unpriced external purchase.

**This was a source-data consistency audit**, not a compiled execution
of `NativeCraftingResourceQuoter` or the C# Origination services.
The actual test suite and WorkChat acceptance remain pending.

## Focused acceptance checklist

1. Load the four first-party example manifests and confirm the
   construction allowance, source-bound material quotations, and
   valid canonical Origin reconstruction.
2. Confirm the Small frame variant reconstructs the sourced `size` intrinsic as `small`, then verify that standard walking (30-foot candidate) is visible while
   its powered locomotion source is active, and absent when disabled.
   Existing climb, fly, swim and burrow traits must continue using
   the same verified movement-mode projector.
3. Confirm optical sight, communication and manipulation are only
   currently usable while their underlying providers operate.
4. Confirm that disconnected or overallocated power, unauthored
   material/variant selections and excessive creation costs are
   rejected or reported, not silently repaired.
5. Confirm that the lightweight and armored variants produce
   different resource quotations and mass/surface evidence without
   unsupported speed or defense bonuses.
6. Run the C# tests and first-party catalog checks, then conduct a
   Construct-only workflow acceptance through WorkChat before
   passing this procedure to the wider Character Origins release.

**No blanket guarantee of full gameplay integration:** These
examples exercise creation and reconstruction. Saved character-sheet
state, inventory debiting, network dispatch across multiple
independent generators, and the numeric effects of advanced modules
remain separate integration items. The user-facing combined
Character Origins tester flow is owned by the other chat.
