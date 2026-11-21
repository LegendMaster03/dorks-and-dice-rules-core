# Pillar 2 — basic Construct playtest reference (2026-11-20)

**Branch:** `feature/construct-basic-character-20261120`  
**Parent feature:** `feature/construct-recipe-derived-starting-cost-20261120`  
**Status:** Provisional first-party playtest authoring; not a production balance approval.

## Approved decisions represented

- A basic mechanical engine supplies **8 simultaneous power units**, not unlimited fuel.
- The **200-point starting allowance** is the current provisional balancing target. Points purchase crafting ingredients, not finished component prices; the allowance applies only at initial construction.
- Basic starting functionality is ordinary movement, sight, communication, and object manipulation simultaneously. No fixed humanoid anatomy or required component count is imposed.
- An operating mechanical engine ordinarily consumes **one fuel charge per adventuring day**, accounted for near a Long Rest. Short Rests do not add a charge. Skipping a Long Rest does not eliminate daily consumption.
- Missing an ordinary fuel charge uses **2014 food-deprivation progression**: permitted days without a ration = max(1, 3 + Constitution modifier), then one level of the existing `condition.exhaustion` for each further day. Supplying the full daily charge resets the missed-days counter. It does not by itself remove existing Exhaustion.
- Fuel deprivation must **not** be modeled as instantaneous power-source depletion. The latter remains a distinct operating-circuit failure; the Construct should receive the same starvation consequence as a biological character missing its food.
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

- `NativeDailyRationEvaluator.AssessDay` performs the **shared 2014 daily-deprivation calculation** (pure read-only outcome). It references the existing Exhaustion condition ID and does not duplicate Exhaustion tiers.
- `NativeConstructOperatingSession.PreviewDailyFuel` verifies that a source is installed and has native daily-fuel metadata. It returns a proposed one-charge expenditure and the shared daily-deprivation outcome. It does **not** accept client fuel claims as proof, advance calendar time, mutate inventory, grant a refill, or apply a condition.
- The fuel requirement belongs to the engine's **daily ration**, not its existing `resourceUsePerStep` finite-energy model. Current finite-resource steps suppress a depleted source's output and are **not appropriate for routine food-equivalent deprivation**.
- The full reference manifest is exercised through the common Origin construction engine, rather than creating a parallel Construct Origin path.

**Still required before declaring a playable production species:**

1. The authenticated host must spend a real daily fuel item, store the missed-day count, and apply/remove Exhaustion through the shared character state. A Long Rest alone must not refill fuel.
2. The ordinary-ground-movement grant must be bound into general Character Sheet walking-speed mechanics. The existing baseline speed adapter expects an authoritative walking-speed **intrinsic**; this first-party movement **trait** does not yet provide that adapter input.
3. The 30-foot movement candidate and actual locomotion capacity must be reconciled with body mass. No movement/load threshold has been approved, and no retired 30/15/0 tier is restored.
4. A Construct's autonomy/sustaining identity is an origination question, not a free battery or a required artificial-life engine.
5. The values of material ingredients, power-consuming components, and the starting allowance need integrated playtesting before promotion.

No extra mandatory component families, independent fuel inventory, short-rest charges, or movement penalties are introduced.
