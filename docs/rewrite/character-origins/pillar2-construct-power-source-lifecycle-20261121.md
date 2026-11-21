# Pillar 2 — Construct power-source lifecycle

**Status:** Candidate source-specific operating contracts; numeric reserve sizes,
replenishment quantities and power outputs remain provisional for playtesting.
**Branch:** `feature/construct-power-source-lifecycle-20261121`.
**Dependency:** Existing Construct Assembly, native operating session,
resource ledger and ordinary ration-survival rules.

## Boundary

The Construct's component arrangement, construction recipes, initial point
allowance, material mass, output allocation, and functional dependencies are
unchanged. A power-source housing installs one catalog-defined source.
Power output is **simultaneous capacity**, not stored fuel or free energy.
An arbitrary number of components may be installed, but unsupported loads
cannot operate simultaneously. Source failure uses the existing operating
session and dependency/shutdown machinery without rewriting Origin traits.

Two source models are intentionally distinct:

- `unmetered` supplies its authored output without an additional finite
  **operating-step** reserve. This never means unlimited power output.
  It also never cancels separately authored daily sustenance.
- `finite` stores a bounded number of source-specific resource units.
  `resourceUsePerStep` is charged only when that source is actually
  supplying output in a valid operating configuration. Below the
  per-step requirement, its output is unavailable; connected dependent
  functions and operating trait applicability are recalculated.

An **operating step** is the existing abstract resource-system step. It is
not automatically one combat round, minute, watch, day, or Long Rest.
Its scheduling must be supplied by the eventual gameplay clock; this
content does not invent a duration or double-charge against daily sustenance.

## Six candidate source policies

| Source | Model | Candidate reserve/use | Sustaining or replenishment contract |
| --- | --- | --- | --- |
| Artificial-life core | Unmetered | No reserve or per-step consumption | Intrinsically self-sustaining; ordinary construction, operation and power limits still apply |
| Solar/rechargeable crystal | Finite | 8 solar-charge units, 1 per step | A verified sunlight-exposure event replenishes up to 2 units, bounded by reservoir capacity |
| Soul gem | Finite | 6 soul-energy units, 1 per step | Verified soul-energy binding replenishes up to 3 units per event; no souls are created by a preview |
| Infernal Engine | Finite | 8 infernal-fuel units, 2 per step | A verified infernal-fuel offering replenishes up to 2 units per event |
| Arcane-consuming core | Finite | 6 arcane-charge units, 1 per step | A verified arcane-energy transfer replenishes up to 1 unit per event; a spell slot is not automatically spent |
| Mechanical engine | Unmetered | No finite operating-step reserve | One mechanical-engine-fuel charge **per day** replaces one ordinary food ration under the shared survival model |

The reserves and rates are **illustrative playtest numbers**, not approved
balance decisions. Different resource kinds are not interchangeable,
and a refill method never substitutes for its required external item,
spell resource, environmental condition or other verified source.

## Source-specific state and recovery

The existing `NativeConstructOperatingSession.PreviewResourceStep`
now accepts installed unmetered sources **without inventing zero-capacity
balances**. The finite-source rules, overload checks, per-instance
resource depletion, existing transfer ports, and output shutdown remain
unchanged. Mixed assemblies need balances for every finite source and
**no** balance for unmetered sources.

`PreviewReplenishment` checks the exact installed source,
the authored replenishment method, the per-event limit, and remaining
capacity, and reports a hypothetical next operating step. This is a
**preview**, not a claim that the triggering event occurred or that
fuel/spell resources were spent.

The internal `NativeConstructResourceLedger.TryCommitReplenishment`
rechecks the source, method, amount and expected revision, then requires
`INativeConstructReplenishmentAuthority` to authorize the externally
verified event. A denied event or stale revision leaves all balances
unchanged. A successful in-process commit supplies a receipt; it does not
provide persistence or transactional coordination with a future
Character Sheet inventory, spell-resource ledger or environmental clock.
The host must coordinate these atomically to prevent duplicate expenditure.

A source marked unavailable in the authorized installation is not
reactivated merely because a replenishment preview predicts enough
fuel. A successful refuel may allow a previously depleted source to
resume operation when the next trusted operating evaluation permits it.

## Mechanical engine and ordinary survival

The existing `dailySustenance` declaration remains unchanged:
`mechanical-engine-fuel-charge`, one unit per day. The same ordinary
ration-deprivation and recovery policy applies; there is no separate
Construct-only starvation ladder. Missing a daily ration is not an
instantaneous source-output loss or a second per-step fuel deduction.

## Deferred outside Origination

- Authenticated inventory/spell/environment spending and durable
  resource persistence; the source preview and ledger do not authorize
  game-world events
- The universal time scheduler mapping an operating step to elapsed play
- Final balancing of candidate reserve sizes, recharge rates, source
  outputs, and ingredient budgets
- Character Sheet movement, combat, special effects, and other shared
  component-system integration
- Integrated end-to-end acceptance before merging the stacked PR

The power-source definitions and source-specific operating contracts
are now authored for all six candidate mechanisms. This is a bounded
Origination increment, **not a claim that external transaction or
Character Sheet integration is already implemented**.
