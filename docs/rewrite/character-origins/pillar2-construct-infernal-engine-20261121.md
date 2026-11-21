# Pillar 2 — Infernal Engine: ordinary sustenance and Overdrive fuel

**Status:** Owner-approved operating direction (November 21, 2026), with
specific instability consequences, fuel availability, and Overdrive
duration unresolved. Candidate first-party implementation awaits
integrated acceptance.

The Infernal Engine draws inspiration from *Baldur's Gate 3* and
*Baldur's Gate: Descent into Avernus*. Their fuel and temperature
mechanics are reference ideas, **not** mandatory Dorks & Dice rules,
lore, or text.

## Approved operating principles

| Property | Decision |
| --- | --- |
| Normal output | 8 power units |
| Shared Overdrive output | 12 power units (+50%) |
| Ordinary fuel | Sustaining infernal fuel, consumed at a long rest |
| Ordinary fuel accounting | One normalized infernal-engine fuel charge per long rest |
| If ordinary fuel is unavailable | Continues supplying normal output, with an instability/overheating consequence |
| Activating Overdrive | Requires additional deliberately supplied infernal fuel |
| Overdrive duration and quantity | Unresolved; do not derive from soul coins or other external material |
| Instability/overheating severity and progression | Unresolved; to be separately designed and balanced |
| Ordinary fuel types | Campaign-adjudicated; soul coins are an optional example, not a requirement |

The single normalized sustaining-fuel charge is a bookkeeping unit for
**one long-rest supply**; it is not a claim about the physical quantity,
material recipe, or market price of infernal fuel. If the fuel was not
available at the long rest, the engine **does not shut down or lose
normal power**. The game host must apply the associated drawback, but
its exact numerical effect is deliberately not invented in this pass.

Overdrive requires an **additional fuel payment**, separate from the
long-rest sustenance. The host must authorize that expenditure through
actual inventory transactions. A declaration that fuel has been
provided is not an inventory mutation, and no quantity, duration, or
cooldown is defined before those values are approved.

The potential heat problem provides a useful interaction with cooling,
containment, and heat-resistant materials in Construct Assembly, but
those possible mitigations confer **no automatic benefit** until they
receive separate rules and balance approval.

## Gameplay consequences and source distinction

The Infernal Engine's operator controls whether to supply sustaining
fuel and when to spend additional fuel on Overdrive. The engine's
instability can become a problem when its sustenance is neglected.

The Solar Collector instead uses environmental light, a finite battery,
and an exhaustion consequence after the available energy is
insufficient. The Soul-Powered Core retains an entirely **separate
narrative tradeoff**: involuntary soul capture and imprisonment,
campaign-defined soul consumption, and the approved soul-extraction
procedure. Do not assign the soul core ordinary starvation,
exhaustion, or infernal instability as an attempt to standardize all
generators.

## Candidate implementation boundaries

- First-party source content declares long-rest sustenance, normal
  output, Overdrive output and the obligation to adjudicate instability
  after missed fuel. The manufacturing recipe stays unchanged.
- `PreviewInfernalOperation` takes externally verified sustaining-fuel
  and additional-Overdrive-fuel flags. It reports source output and
  whether instability adjudication is required.
- A source lacking an authenticated current operating context cannot
  acquire a valid runtime power grant through a generic evaluation.
  This validation failure **is not** the mechanical effect of fuel
  shortage: an explicitly unfueled but adjudicated source still
  contributes 8 normal power units.
- The preview does not spend fuel, impose a specific heat penalty,
  schedule the next long rest, or keep Overdrive active for an invented
  number of rounds.
- The host must prevent player-supplied booleans from bypassing actual
  fuel inventory, long-rest events, or instability consequences.
- No change is made to the Soul-Powered Core's capture, custody,
  consumption, or extraction rules.

## Remaining decisions and tests

1. Define what counts as infernal fuel, whether different fuels are
   interchangeable, and how their availability and crafting work.
2. Establish the real gameplay effect of insufficient long-rest
   sustenance, including whether heat-resistance/cooling can mitigate it.
3. Set the exact additional fuel cost and duration of Overdrive.
4. Test the source against the existing power-network, multi-source
   dispatch, inventory, rest, and Construct Assembly infrastructure
   after the stack is integrated.

No final Overdrive timer, heat progression formula, or inferred soul
fuel requirement is part of this approval.
