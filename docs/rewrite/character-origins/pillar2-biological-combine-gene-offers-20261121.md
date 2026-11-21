# Pillar 2 — Biological Origination: Combine gene offers

**Date:** 2026-11-21  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Scope:** First biological Combine increment; source-preserving read-only construction output  
**Status:** Combine projection and optional explicit transmission selection implemented on a feature branch, subject to tests and review. Not final genetic transmission, latent expression, or balance policy.

## Boundary in the universal procedure

The universal Origin sequence remains **Deconstruct → Combine → Reconcile ↔ Manifest → Reconstruct**. Biological reproduction supplies an eligible pool of canonical genes and intrinsic physical characteristics to the **same** universal Origin construction engine. The existing engine already validates contributor eligibility, selects actual manifestations, resolves permitted compound parameters, reconciles overlaps, binds dependent traits and resources, and reconstructs effective grants.

This increment exposes the intervening **Combine** evidence as `NativeOriginConstructionResult.BiologicalCombination`; it does not create a second biological grant evaluator.

For each canonical gene (or physical intrinsic slot), the combination contains:

- A group keyed by mechanical definition ID or physical intrinsic slot, **not** by a particular Species/Ancestry name.
- Separately identified contributor-local offers, retaining their complete native candidate identity, configured parameters, source occurrence, required choices, and verified eligibility.
- Manifestation **attempt IDs** when the player selected a candidate. These remain attempts until authorized.
- References to the **effective expression** grants which survive universal reconciliation, dependent binding and requirement closure. A grant may list multiple source candidates without multiplying its mechanics; its `ParameterSources`, `ResourceOwners`, and `InheritedFrom` evidence remain available in the effective grant.
- An empty expression list when a candidate was not chosen or did not survive validation. **This is not a latent or inheritable suppressed gene.**

Only first-party biologically eligible or authenticated table-authorized contributor offers enter this pool. Its absence for other methods is intentional; Construct Assembly retains its own procedure. The pool does not grant custom content by mere discovery.

## Example: biological gene pool

For Red Dragonborn and Bronze Dragonborn contributors:

| Element | Red contributor | Bronze contributor |
| --- | --- | --- |
| Canonical characteristic | `trait.breath-weapon` | `trait.breath-weapon` |
| Offer identity | Red source occurrence and contributor ID | Bronze source occurrence and contributor ID |
| Configured evidence | Red's offered damage type and complete Breath Weapon area | Bronze's offered damage type and complete Breath Weapon area |
| Before player choice | Eligible offer, not an effective grant | Eligible offer, not an effective grant |

The group contains **two evidence-bearing offers of one canonical gene**, not two automatic Breath Weapon uses. The separately approved `originComposition.recombinableParameters` rules control whether offered `damageType` and whole `area` may be chosen across contributors. Action economy, saving throw, usage pool, recharge and progression remain properties of the complete Breath Weapon definition. The pool itself does not independently unlock those parameters, authorize a new grant, or force any cross-gene resistance/warding alignment.

Two Orc contributors similarly produce two distinct offers of the shared `trait.powerful-build` gene. They do not automatically grant Powerful Build twice. A verified mixed contributor can only offer its actual current inherent grants; grandparent identities alone do not add candidates.

## Transmission and expression remain distinct

The next procedure-specific contract must decide **what is transmitted** separately from **what is expressed**:

1. **Available:** A verified contributor offers a configured gene or physical intrinsic.
2. **Proposed transmission:** A player selects a transmitted genetic configuration under an authored or verified table policy. This is not interchangeable with an existing manifestation attempt.
3. **Expression:** Transmission and the configuration's permitted dependencies/variants resolve to a valid manifested characteristic. Reconciliation and manifestation may iterate.
4. **Suppressed / unexpressed:** The model may eventually record inherited material without an effective grant, but this is not authorized by the current default. The absence of an effective grant is not proof that latent hereditary material exists.
5. **Reconstruction:** The resulting current Origin records only authorized effective characteristics; verified lineage remains audit evidence, not an independent gene source.

Player-directed choice is the current biological selection policy. No random inheritance, fixed parent count, mandatory primary Species, percentage mixture, gene quota, point allowance, or automatic latent transmission is imposed. One contributor may have a nested mixed Origin, but only the authenticated **current** hereditary profile may supply new offers.

A future gene-transmission model should use these stable contributor/occurrence identities and explicit dependencies, not infer independent inheritance of arbitrary sub-effects. The previously agreed Daggerheart comparison informs how linked sub-traits and shared parameter values remain coherent; it does **not** require a shallow gene model or immediate UI simplification.

## Explicit biological transmission selection (increment 2)

The optional `NativeOriginProposal.BiologicalTransmission` contains a
`NativeOriginBiologicalTransmissionPlan` with exact contributor-local
`CandidateIds`. It is a player **proposal**, not a certified genetic
record or an independent grant source. It is distinct from each
`NativeOriginManifestation`, which remains responsible for expression
parameters, choice values and actual grant construction.

- **Null plan:** existing manifestation-only proposals continue working,
  including saved drafts and nonbiological methods. This compatibility mode
  does not infer or persist any hidden transmitted state.
- **Explicit nonnull plan:** only listed, catalog-eligible biological or
  appropriately authenticated table-authorized contributor offers may be
  expressed. A manifestation without a corresponding selected source is
  unresolved and cannot mint a grant.
- **Authored reconciliation still controls expression:** selecting a source
  is not selecting parameters freely, satisfying dependencies, or authorizing
  stacking. The construction engine still processes relationships, choices,
  dependent resource owners and the final effective grant closure.
- **Unexpressed selection:** because no rule authorizes retaining latent genes,
  any selected offer that does not participate in an effective inherent grant
  remains unresolved. It does not enter a playable Origin as hidden heredity.
  Derived synergies do not count as expression of their input genes.
- **Provenance and review:** `BiologicalCombination` marks an offer
  `ProposedForTransmission`, explicitly labeling client intent rather than
  authority. Invalid candidates, duplicates, method mismatches and unsupported
  sources cannot authorize a profile.
- **Persistence boundary:** the plan serializes as part of the **evaluated
  proposal**, not as new hereditary state inside `NativeOriginProfile`.
  Effective inherent grants remain the authoritative inheritable state
  when they are verified in later generations.

The opt-in plan is the minimal next sequencing contract. It does **not**
establish an inheritance quota, a parent/allele ratio, dominant/recessive
expression, trait-level latent storage, genetic randomization, or a final
allocation cost. The exact number of transmitted genes is still subject
to later balance and procedure design. A player may transmit multiple offers
of the same canonical gene when needed for a separately authored recombination,
but identical evidence is not multiplied into independent effects.

Focused tests cover the match between plan and manifestation, missing
transmission, unused transmission, forged/duplicate source IDs, nonbiological
methods, backward compatibility, and unauthorized historical contributors.

## Implementation and validation

- `NativeOriginBiologicalCombination` is a read-only, serialized result alongside `EvaluatedProposal` and `Profile`.
- The projector operates **after** final construction, so it can distinguish attempted manifestations from actual surviving effective grants.
- It does not write to `NativeOriginProfile`, reinterpret source parameters, add grants, or change the outcome's authorization status.
- Focused tests cover two differently configured donors offering the same canonical gene, selection/expression, identical donors, nonbiological methods, and unverified historical ancestry.

**Next actionable design choice:** Decide whether any transmitted-but-unexpressed material may persist and how an authored transmission-capacity policy would apply. The model must preserve linked gene parameters, source-contributor scopes, authored dependencies, and the complete physical foundation while leaving numeric allocation and UI for their separate passes. This read-only Combine increment does not settle that choice.

**Integration dependency:** This branch starts from draft PR #180's source-neutral flat gene catalog; PR #179's linked-parameter finalization is a separate draft change. They should be integrated and tested together before treating cross-gene expression closure as finished. Do not merge into `main` without approval.
