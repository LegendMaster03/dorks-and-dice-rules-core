# Pillar 2 — Solar-based Construct power source

**Status:** Owner-approved source design and exhaustion fallback candidate
(November 21, 2026); integrated acceptance is still required.

**Reference:** `construct-power-source.solar-crystal` is a stable native
identifier, **not** a physical requirement that the mechanism be a crystal.
*Fool's Gold* Foreclaimer technology is conceptual inspiration, not
a rules adaptation.

## Owner-approved operating model

A solar-inspired magical generator with an attached rechargeable battery.
The collector produces energy when suitable sunlight is available, feeds the
existing connected power network, and stores surplus energy. The battery
supplies any deficit between current collection and actual assigned output.
Generation, storage capacity and maximum simultaneous output are distinct.

| Parameter | Value |
| --- | ---: |
| Normal simultaneous output | 8 power units |
| Overdrive simultaneous output | 12 power units |
| Shared Overdrive increase | 50%, rounded down to whole units |
| Direct sunlight collection | 16 power units |
| Indirect daylight/substantial shade collection | 8 power units |
| Darkness/no suitable light collection | 0 |
| Concentrated sunlight collection | 24 power units |
| Starting integrated battery | 96 power-unit-hours |

**Overdrive condition:** Concentrated sunlight enables Overdrive, and
Overdrive lasts **only while that condition continues**. The DM determines
what qualifies as concentrated sunlight and how it can be obtained. It is not
automatically a carried accessory or a compulsory workshop device. Stored
energy alone cannot enable Overdrive.

The 50% increase is the agreed common baseline for future power sources.
Other sources may have different activation, fuel cost, shutdown and
duration rules. This pass **does not** introduce an unapproved mechanical
engine Overdrive fuel cost or duration. The completed mechanical engine
still has 8 normal output and one daily mechanical fuel charge as ordinary
sustenance.

## Collection, discharge and time

Simultaneous power and stored energy are not interchangeable quantities.
The runtime expresses the 96-power-unit-hour integrated battery as
**5,760 power-unit-minutes** so it can accurately handle elapsed play time.

- If collection exceeds the solar generator's assigned demand, surplus
  energy charges the battery up to its rated capacity.
- If collection falls short, available stored energy supplies the deficit.
  If available collection and storage cannot meet demand, the source
  **continues at its rated normal output**, and the Construct incurs the
  separately tracked exhaustion penalty. This does not grant extra battery
  energy or exceed the 8-unit normal limit.
- A fully charged battery can sustain the full 8-unit normal output for
  **12 hours before an energy-deficit exhaustion penalty begins**, without incoming sunlight.
- The existing 7-unit reference Construct can run for approximately
  **13.7 hours before energy-deficit exhaustion begins** without incoming sunlight.
- In direct sunlight at a 7-unit load, 9 units replenish storage each hour;
  a fully discharged battery needs **10 hours 40 minutes** of those
  conditions to reach full charge.
- In concentrated sunlight, the collector can produce 24 units but may
  deliver at most 12 units simultaneously to active components.
- A full battery wastes unused incoming energy. Time advancement without
  sunlight consumes only the actually assigned source contribution.

The source-specific operating preview provides normal output even when
energy generation and storage cannot fund the **entire requested interval**.
It passes the source rating to the ordinary component/dependency/power-network
evaluator, then calculates remaining battery energy and the exact unfunded
power-unit-minutes and insufficient-energy minutes. A failed or
overallocated configuration does not commit consumption, charging, or
exhaustion consequences.

### Approved energy-deficit fallback

- The Construct gains **one exhaustion level when it begins operating
  without sufficient solar energy**, even if its components remain powered.
- It gains **one additional level per 24 hours of continued operation
  under that deficit**. Splitting one period into smaller time increments
  must not grant repeated first levels.
- Exhaustion caused by inadequate solar energy **cannot be recovered
  while the energy shortage continues**.
- Adequate incoming sunlight or restored usable energy ends the active
  deficiency. Accrued exhaustion remains until recovered through
  the applicable recovery rules; sunlight does not instantly cure it.
- The generator has **no independent energy-deficit shutdown**. It
  remains limited to 8 normal units. Solar Overdrive still requires
  DM-confirmed concentrated sunlight.

The revision-checked ledger tracks insufficient-energy operating minutes
and reports each newly accrued exhaustion level. This is an
**exhaustion consequence receipt**, not an authorization to mutate
the character's condition. The host must apply the receipt to the
Construct, enforce inability to recover exhaustion while deficiency
continues, and persist both the exhaustion and deficit progression.

**Reopening a ledger:** A host restoring a previous session must supply its
authenticated deficit-minute and exhaustion-event counters, not silently
reset them by opening a fresh ledger with starting zero values. Fresh
creation can start at zero only if the character has no prior intervals.

## Authorization boundaries

`NativeConstructOperatingSession.PreviewSolarOperation` validates the
source's first-party output and storage authoring, the exact installed
source occurrence, battery bounds, exposure kind, interval duration, and
active power allocation. It is an advisory preview, not authorization for a
player to assert direct or concentrated sunlight.

`OpenTrustedSolarLedger` accepts balances and, when restoring an existing
session, authenticated deprivation and exhaustion-event counters. The
in-process `NativeConstructSolarEnergyLedger` rechecks capacity, time
calculations and exhaustion accrual under a revision lock, and requires an
`INativeConstructSolarIntervalAuthority` confirmation before a transition
can commit. That authority represents DM-validated exposure and game time.
A replay with a stale ledger revision cannot refill, spend, or double-charge
exhaustion events.

The general no-context `EvaluateConfiguration` path will not claim a
solar-powered configuration valid without authenticated operating context.
This is an **authorization boundary**, not a rule that the solar generator
stops working without light or stored energy. The solar preview provides
rated output and identifies the exhaustion consequence. Initial
construction can still evaluate normal rated output for selection and
pricing.

## Related components and limits

Additional storage components are an allowed extension of the system,
not a mandatory source replacement. The existing energy accumulator
catalog exposes a storage interface but has **no approved storage capacity,
charge rate or discharge limit yet**; this pass does not invent those
numbers or silently grant free storage. Future authored storage modules
can extend battery capacity through the general connected power system.

The first-party solar source retains its existing ingredient-based
manufacturing recipe and stable source ID; neither its material
composition nor its recipe cost was redesigned in this source mechanics
pass.

**Remaining before full production acceptance:**
1. Connect the host's elapsed-time/environment authority, battery and
   deficit counters, and exhaustion/recovery enforcement to gameplay state;
   neither preview nor in-process receipt is a persistent game transaction.
2. Establish a source-dispatch policy when solar and non-solar
   generators simultaneously supply the same active network. That
   configuration is explicitly unresolved by the solar preview until
   a priority or allocation rule is approved, rather than introducing
   an unapproved implicit fuel-priority rule.
3. Integrate optional external battery components after their capacities
   and transfer behavior have been separately approved.
4. Run the .NET suite and integrated acceptance against the stacked
   Construct branch.

The Soul-Powered Core retains its separate narrative tradeoff, with no
mandatory exhaustion or infernal-fuel adjustment. Infernal fuel decisions
are documented independently. No arcane source changes are authorized.
