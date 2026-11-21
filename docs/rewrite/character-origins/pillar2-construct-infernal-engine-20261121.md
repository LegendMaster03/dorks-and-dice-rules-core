# Pillar 2 — Infernal Engine: Sustenance and Infernal Empowerment

**Status:** Owner-approved fuel model and once-per-turn fire-damage
Empowerment direction (November 21, 2026). The damage quantity,
qualifying attack types, and instability penalty remain undecided.
Candidate implementation requires integrated acceptance and
authoritative game-host fuel and rest tracking.

*Baldur's Gate 3* and *Baldur's Gate: Descent into Avernus* are
conceptual inspiration. No external setting's wording or lore is
incorporated as a mandatory Dorks & Dice rule.

## Approved operating principles

| Property | Design |
| --- | --- |
| Normal output | **8 power units** |
| General power Overdrive | **None** for this source |
| Ordinary sustenance | Infernal fuel paid at each long rest |
| Sustenance accounting | One normalized infernal-fuel charge per long rest |
| Missing sustenance | **Normal 8-unit output continues** with a separate instability/overheating drawback |
| Infernal Empowerment | Activated through **additional, deliberately consumed infernal fuel** |
| Empowerment duration | **Until the next long rest** |
| Empowerment effect | Bonus **fire damage**, at most **once per turn** |
| Empowerment damage die / amount | Not yet specified |
| Qualifying attack types / triggers | Not yet specified |
| Empowerment power-output bonus | **0** |
| Additional fuel cost/quantity | Not yet specified |
| Instability/overheating consequence | Not yet specified |

The single sustaining-fuel charge represents the normalized supply
needed at a long rest; the specific substance, quantity, recipe and
price have not been balanced. A missed sustaining-fuel payment does
not shut down the engine. The host must adjudicate an instability or
overheating drawback, whose actual game effect remains undecided.

**Infernal Empowerment replaces the earlier Infernal Overdrive
proposal.** No extra fuel increases general available power from 8
to 12 units. Additional fuel instead confers a temporary condition
lasting until the next long rest. The approved combat-effect direction is **bonus fire damage at most once
per turn**, avoiding an unrestricted bonus on every attack in a
multiattack sequence. The exact damage value and the attacks that
qualify are not approved, so the gameplay modifier remains
unimplemented. It must not silently grant +1d4 fire damage, extra
attacks, an additional power output, or any other unapproved benefit.

The enhancement's activation is a real fuel expenditure to be
authorized by the host against inventory. The operating preview
accepts an already verified **active-empowerment condition**, not a
new free fuel payment whenever power is evaluated. The next long
rest ends that condition; the engine's normal sustaining-fuel cost
for that rest is a separate transaction.

## Narrative and engineering distinction

Karlach's Infernal Engine in *Baldur's Gate 3* is an example of
infernal machinery requiring stabilization. Its soul-coin-powered
temporary enhancement and the demon-ichor boost for infernal war
machines in *Descent into Avernus* are different inspirations for
a temporary ability. They do **not** establish an obligation to use
soul coins as the only fuel or to add a random catastrophe table.

Cooling systems, insulation, engine metallurgy, and infernal heat
resistance are potential future integration points with Construct
Assembly. None automatically removes the unspecified missing-fuel
penalty or changes the empowerment effect without approval.

The Solar Collector has its separate exhaustion drawback after a
battery deficit. The Soul-Powered Core retains its **different
narrative tradeoff** of involuntary capture and imprisonment; it
does not inherit a fuel-shortage or overheating penalty.

## Candidate implementation and trust boundary

- The native source definition contains only 8 general power units.
  It has no `overdrivePowerOutput` field.
- `infernalEmpowerment` declares activation through authenticated
  additional fuel, an end point of `until-next-long-rest`, zero
  generic output bonus, and the approved fire-damage / once-per-turn
  effect shape. The specific damage and triggering attacks are explicit
  unresolved parameters.
- `PreviewInfernalOperation` reports ordinary fuel compliance,
  host-verified active empowerment, engine output, and whether the
  unfinalized stability penalty must be adjudicated.
- The evaluator **rejects** an infernal output override that claims
  more than its fixed normal rating; other generators retain their
  separate Overdrive models.
- A no-context generic runtime evaluation cannot certify infernal
  fuel and instability state. The 8-unit rating is still available
  when the host confirms that ordinary sustenance was missed.
- The host must persist the temporary condition, expire it at the
  next long rest, validate the fuel expenditure, and enforce the
  resulting combat effects once separately approved.
- Preview interfaces neither spend fuel nor apply any combat bonus.
  No fuel conversion, duration in rounds, or heat-damage formula
  is being invented.
- The existing ingredient-based manufacturing recipe is unchanged.

## Outstanding design choices

1. The **damage amount** of Infernal Empowerment and its
   **qualifying attacks/triggers**. Fire damage with a maximum of one
   application per turn is already approved. The system must not
   grant damage until the missing parameters are defined.
2. Acceptable infernal fuels and the exact **additional** fuel cost
   per empowerment activation.
3. The concrete instability/overheating penalty and how cooling
   and materials may modify it.
4. Host integration for inventory consumption, long rests, temporary
   effects, and recovery, followed by WorkChat acceptance tests.

The common +50% Overdrive principle remains available to other
power sources, but it is not applied to the Infernal Engine.
