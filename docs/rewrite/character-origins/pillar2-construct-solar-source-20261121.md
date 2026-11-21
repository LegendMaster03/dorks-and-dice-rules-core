# Pillar 2 — Solar-based Construct power source

**Status:** Owner-approved source design and candidate first-party implementation
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
  Source output is limited by the normal/Overdrive ceiling and by how much
  energy is sustainable across the requested interval.
- A fully charged battery can sustain the full 8-unit normal output for
  **12 hours** without incoming sunlight.
- The existing 7-unit reference Construct can run for approximately
  **13.7 hours** without incoming sunlight.
- In direct sunlight at a 7-unit load, 9 units replenish storage each hour;
  a fully discharged battery needs **10 hours 40 minutes** of those
  conditions to reach full charge.
- In concentrated sunlight, the collector can produce 24 units but may
  deliver at most 12 units simultaneously to active components.
- A full battery wastes unused incoming energy. Time advancement without
  sunlight consumes only the actually assigned source contribution.

The source-specific operating preview computes the sustainable output
for the **requested interval**, passes that output to the ordinary
component/dependency/power-network evaluator, then calculates the next
battery balance. A failed or overallocated configuration does not commit
consumption or charging.

## Authorization boundaries

`NativeConstructOperatingSession.PreviewSolarOperation` validates the
source's first-party output and storage authoring, the exact installed
source occurrence, battery bounds, exposure kind, interval duration, and
active power allocation. It is an advisory preview, not authorization for a
player to assert direct or concentrated sunlight.

`OpenTrustedSolarLedger` accepts balances that a host has already
authenticated. The in-process `NativeConstructSolarEnergyLedger` rechecks
all capacity and time calculations under a revision lock, and requires an
`INativeConstructSolarIntervalAuthority` confirmation before a transition
can commit. That authority represents the DM's validated exposure and game
time. A replay with a stale ledger revision cannot refill or spend energy.

The general no-context `EvaluateConfiguration` path will not claim a
solar-powered operating configuration valid merely from its static native
8-unit rated output. The solar preview supplies the necessary
time-and-battery context. Initial construction can still evaluate normal
rated output for selecting and pricing the installed components.

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
1. Connect the host's elapsed-time/environment authority and durable
   battery balances to gameplay state; neither preview nor in-process
   revision receipt is a persistent game transaction.
2. Confirm deterministic allocation priority where multiple powered sources
   share a network. The current source preview conservatively credits
   other active generators first when estimating solar storage use; this is
   an implementation detail, **not an owner-approved fuel-priority rule**.
3. Integrate optional external battery components after their capacities
   and transfer behavior have been separately approved.
4. Run the .NET suite and integrated acceptance against the stacked
   Construct branch.

No changes to the soul, infernal or arcane power-source policies are
authorized by this solar-source approval.
