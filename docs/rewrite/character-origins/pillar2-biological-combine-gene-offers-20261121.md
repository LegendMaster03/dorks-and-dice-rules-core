# Pillar 2 — Biological Origination: Combine, Transmission, and Expression

**Date:** 2026-11-21  
**Repository:** `LegendMaster03/dorks-and-dice-rules-core`  
**Branch:** `feature/biological-combine-gene-offers-20261121` (stacked on draft PR #180)  
**Status:** Implemented as an opt-in construction contract; build and integrated acceptance still required.

## Universal Origination boundary

Biological reproduction is one method within **Deconstruct → Combine → Reconcile ↔ Manifest → Reconstruct**. Its genes use the same mechanical identities, manifestations, linked parameters, resource binding, reconciliation, and grant authorization as other Origin methods.

- **Available:** Source-neutral, catalog-verified contributor offers. Each offer retains its exact Species/Ancestry occurrence, contributor ID, parameters, and choices.
- **Inherited:** A player explicitly selects candidate instances in `NativeOriginProposal.BiologicalTransmission.CandidateIds`. That selection is intent until the engine validates its contributor and completes construction.
- **Expressed:** An inherited instance contributes to an effective, reconciled, dependency-closed inherent grant.
- **Latent:** An inherited instance contributes to no effective grant. Latency is **derived**, not independently stored, granted, or capped. An inherited gene never provides passive abilities, active traits, synergy inputs, or prerequisites merely by existing.
- **Reconstructed:** The completed underlying configuration carries both `InherentGrants` (actual mechanics) and `BiologicalGenes` (configured hereditary instances). This avoids treating latent genes as entitlements.

The existing `NativeOriginConstructionResult.BiologicalCombination` still exposes canonical identity groups with exact offers, proposed transmission flags, manifestation attempts and effective grant references. Its `InheritedInstances` projection derives `IsLatent` from the final grant set. The persistent `NativeOriginBiologicalGeneInstance` itself has **no latent Boolean or independent expression authorization**.

## Approved default capacities

`data/rulesets/first-party-001/native/rules/creature-origination.json` authors:

| Setting | Default |
| --- | ---: |
| `maxInheritedInstancesPerKind` | 2 |
| `maxExpressedInstancesPerKind` | 1 |

A kind is one canonical gene ID (for example, `trait.breath-weapon`) or one heritable intrinsic slot. Red and Bronze Breath Weapon configurations are **two distinct source instances of the same kind**. The limits apply **per kind**, not across all genes. There is **no** separate maximum latent count; any inherited instances not represented in effective grants are latent. Neither slot must be filled, and no expression is required for an otherwise valid inherited gene. Numeric defaults can be changed in the native rule during balance work; an authenticated, scoped `biological-inheritance` table decision can replace both limits in an individual construction.

No dominant/recessive labels, allele probabilities, random inheritance, global inheritance budget, fixed parent count, primary Species, or automatic expression are imposed. Two identical offers remain two sources of genetic evidence, not duplicate mechanical benefits.

### Example: two Orc contributors

A character inherits two `trait.powerful-build` instances, one from each contributor, but manifests only the second. The final configuration contains:

- Two records in `BiologicalGenes`, with their distinct source candidate IDs.
- One effective `trait.powerful-build` grant in `InherentGrants`.
- One expressed gene instance and one derived latent gene instance.

Alternatively the character may manifest neither instance, leaving **both latent**; this does not require an additional latent-capacity parameter.

### Example: differing Breath Weapon configurations

Red and Bronze Dragonborn contribute separate `trait.breath-weapon` instances, each with its own offered damage and area parameters. Those configurations remain separate hereditary evidence. Any approved parameter recombination still belongs to existing authored reconciliation rules; a combined phenotype does not automatically erase the original source configurations or create a second effective Breath Weapon.

## Multigenerational inheritance

A completed Origin's `Underlying.BiologicalGenes` survives JSON serialization. An application-authenticated current underlying profile may supply both latent and expressed instances to a descendant. The contributor authority checks the profile identity, revision, baseline grant authorization, and basic gene-record consistency. Only the **immediate verified current configuration** is eligible; unverified user snapshots, unrelated active forms, grandparent identifiers, and historical ancestry do not create new gene offers.

A descendant can select and manifest a gene that was latent in a verified parent. Parent choices are retained, not reopened by arbitrary client selections. The child's new instance records the verified parent profile ID, revision, source instance ID and whether the source was a legacy effective grant or a stored biological gene. This does not embed an unbounded pedigree. Previously completed profiles without a `BiologicalGenes` field remain readable and still expose authorized effective inherent grants.

Latent native instances requiring persistent creation choices can provide `LatentConfigurations` containing only validated authored choices. Unsupported choices, incomplete required options, open-catalog choices without trusted evidence, and unconfigured physical values do not become valid hereditary records. A previously configured inherited gene can not silently change its choices.

## Boundaries and unresolved work

An explicit transmission plan is opt-in for compatibility. When absent, legacy manifestation-only proposals retain their existing behavior and do not retroactively invent a latent genotype. Direct and nonbiological methods are unaffected by biological capacity enforcement.

The first-party biological gene catalog still supplies verified eligibility. A player can not manufacture arbitrary canonical genes from historical ancestry or an untrusted nested profile. A selected candidate with invalid authoring dependencies cannot become a valid genotype just because it was not manifested.

The instance model retains source configuration when an expressed phenotype combines parameters across contributors. The precise hereditary treatment of **newly recombined** configurations, if different from their source instances, is a separate policy question. Likewise, a final global species-power budget and UI integration remain outside this increment.

**Testing:** Focused real-catalog regression cases cover two-source inheritance, either instance's expression, both-latent behavior, per-kind overcapacity, verified latent-to-expressed descendants, source provenance, historical/unverified rejection, and serialization. The tests still need to be run with the repository's .NET 10 SDK. PR #179's separately implemented linked-parameter finalization must be integrated and retested with PR #180 and this branch before declaring the full biology pipeline complete.

**Merge policy:** This is a draft stacked PR. Do not change `main` without explicit approval.
