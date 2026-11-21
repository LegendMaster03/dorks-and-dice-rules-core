# Pillar 2 — basic Construct playtest reference (2026-11-20)

**Branch:** `feature/construct-basic-character-20261120`  
**Parent feature:** `feature/construct-recipe-derived-starting-cost-20261120`  
**Status:** Provisional first-party playtest authoring; not a production balance approval.

## Approved decisions represented

- A basic mechanical engine supplies **8 simultaneous power units**, not unlimited fuel.
- The **200-point starting allowance** is the current provisional balancing target. Points purchase crafting ingredients, not finished component prices; the allowance applies only at initial construction.
- Basic starting functionality is ordinary movement, sight, communication, and object manipulation simultaneously. No fixed humanoid anatomy or required component count is imposed.
- The basic mechanical engine uses the **ordinary daily ration rules**, substituting **one mechanical-fuel charge per adventuring day** for a food ration. Routine consumption is accounted for near a Long Rest. Short Rests do not add another ration, and skipping a Long Rest does not eliminate daily consumption.
- **All ordinary survival consequences are identical to those for food**, under the shared 2014 rules: permitted days without a ration = max(1, 3 + Constitution modifier), followed by one level of existing `condition.exhaustion` for each further missed day. A full ration of the required resource resets the missed-day counter; it does not automatically remove existing Exhaustion.
- Missing routine fuel must **not** be modeled as instantaneous power-source depletion. Physical damage, deliberate shutdown, and separate specialized energy mechanisms can still interrupt power. The basic engine changes the **ration resource**, not the survival rules.
- Optional Overdrive remains **deferred**: no approved output bonus, duration, or expenditure quantity.
- Consciousness/animation is distinct from operating power; the artificial-life core is **not** the default unlimited starter source.

## First-party reference build

`data/reference/construct-basic-starting-build-20261120.json` holds a **Medium** reference assembly:

| Hardware | Candidate material | Ingredient points |
| --- | --- | ---: |
| Medium structural frame | Steel | 32 |
| Mechanical engine + source housing | Iron mechanisms; wood housing | 31 |
| Control module | Wood | 10 |
| Alchemical circulation and actuator | Steel | 43 |
| Manipulator | Steel | 14 |
| Locomotion unit | Steel | 22 |
| Crystal eye | Crystal | 11 |
| **Total** | | **163** |

The candidate starting allowance leaves **37 points** for alternative material choices and modest customizations. Current active hardware demands **7** simultaneous power units from an **8-unit** engine. The reference is not a mandatory body template.

The control module is the canonical source of an ordinary auditory-communication trait, without granting languages. The drive is the source of a candidate **30-foot ordinary-ground-movement trait**; it does not override all other movement or physical-support rules. The eye and manipulator retain their existing sourced traits. All four are operational grants: disabling their providers suppresses applicable abilities, rather than removing installed hardware.

## What is implemented and what is not

- `NativeDailyRationEvaluator.AssessDay` is a **shared, resource-agnostic 2014 daily-deprivation calculation**. A `NativeDailyRationRequirement` identifies the required daily resource and amount; either ordinary food or mechanical fuel produces identical survival outcomes when supplies are equivalent. The evaluator references the existing Exhaustion condition ID and does not duplicate Exhaustion tiers.
- The basic engine declares `dailySustenance.resourceKind` and `dailySustenance.unitsPerDay` in the first-party catalog. It does **not** declare a separate deprivation rule or special recovery schedule. The former Construct-only `PreviewDailyFuel` operation has been removed; routine survival is evaluated through the same ration API for all characters.
- The basic engine's `dailySustenance` is a **ration substitution**, not the `resourceUsePerStep` finite-energy model. Specialized source-resource depletion can immediately suppress power; ordinary hunger-equivalent deprivation does not.
- The full reference manifest is exercised through the common Origin construction engine, rather than creating a parallel Construct Origin path.

**Still required before declaring a playable production species:**

1. The authenticated host must connect the native `dailySustenance` requirement to the **same general daily-ration transaction** used for food: verify the selected resource in inventory, consume it once per elapsed adventuring day, track deprivation, and apply/recover Exhaustion through the shared character state. A Long Rest does not create fuel.
2. The ordinary-ground-movement grant must be bound into general Character Sheet walking-speed mechanics. The existing baseline speed adapter expects an authoritative walking-speed **intrinsic**; this first-party movement **trait** does not yet provide that adapter input.
3. The 30-foot movement candidate and actual locomotion capacity must be reconciled with body mass. No movement/load threshold has been approved, and no retired 30/15/0 tier is restored.
4. A Construct's autonomy/sustaining identity is an origination question, not a free battery or a required artificial-life engine.
5. The values of material ingredients, power-consuming components, and the starting allowance need integrated playtesting before promotion.

No extra mandatory component families, independent fuel inventory, short-rest charges, or movement penalties are introduced. The actual inventory identifier, acquisition, and cost of a standard mechanical fuel charge still need normal equipment authoring. No engine-specific recovery exception is authorized.

## Follow-up: expanded component playability (2026-11-21)

The later Construct catalog expansion now gives all fifteen added physical
mechanisms candidate recipes and operating power demand, and gives the four
new locomotion modes candidate positive speeds. These changes do not revise
the original 163-point example or its 7-of-8-power baseline.

The candidate movement-mode projector accepts authorized custom Construct
operating sessions, separate from Biological Species/Ancestry projection.
Integration with Character Sheet walking and special movement speeds,
physical support, and resource transaction persistence remains necessary.

The selected stopping condition for Construct Assembly now requires **all
46 components to have gameplay-usable intended behavior**, not merely
installable catalogs and valid power totals. The operating parameters and
advanced combat/sensing/energy/repair/control effects are still candidates
until that integration is completed and tested.
