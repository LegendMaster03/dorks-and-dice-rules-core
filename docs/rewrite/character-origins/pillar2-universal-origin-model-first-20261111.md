# Pillar 2 — establish the universal Origin model before Species-specific mixed-Ancestry rules

**Date:** 2026-11-11  
**Status:** Architectural sequencing decision and proposed general contract. Not approval of a final rules schema, individual inheritance policies, an automated character-creation permission, or gameplay implementation.

## Priority established by the owner

**Finish the general Character Origins inheritance and manifestation system before designing or tuning combinations within any particular Species.** The formulaic nature of Dragonborn makes eventual same-Species mixed-Ancestry support tractable, but it must **test** a reusable framework, not define that framework.

The recently added read-only shared-foundation audit, same-Species pair matrix, and synthetic compatibility-contract evaluator are **provisional study tools**. Their existence is not authorization to develop actual Dragonborn category/material mixing policies, ancestry-pair exceptions, or source-specific selection rules now.

## Fundamental operation: decompose, mix, reconcile, manifest, reconstruct

**Owner's conceptual model:** Mixed Species must be constructed by **breaking down each contributor's Species/Ancestry configuration into its constituent characteristics**, combining what those contributors can offer, **reconciling those components under an explicit general rule system**, and reconstructing a new coherent configuration. A Species is therefore mechanically a **composition of characteristics**, not an indivisible inherited label or an all-or-nothing package.

1. **Deconstruct source configurations losslessly.** Inventory intrinsic properties, mechanical traits, reusable effect identities, trait parameters, restrictions, choice dependencies and source provenance. Retain source-level semantics even if the generic model does not yet know how to interpret them. A contributing standard Species/Ancestry must be reconstructible from its parts without silently losing behavior.
2. **Combine eligible candidates.** Pool source-qualified characteristics and available parameter values, retaining duplicate offers and contributor lineage as evidence **without multiplying grants**. Distinguish each contributor's actual manifested state (when already mixed) from merely historical or potentially latent ancestry; the transmissibility policy is still open.
3. **Reconcile characteristics by general rules.** Evaluate semantic equivalence, duplication, alternatives, replacement, prerequisite/dependency relationships, linked values, mutually exclusive intrinsic properties and explicit permitted exceptions. Unknown mechanics and unresolvable conflicts stay visible. No attempt to infer an automatically balanced or universally compatible mix is authorized.
4. **Determine the manifested configuration through choices and adjudication.** Select actual traits and values within the candidate and policy boundaries, record DM-approved variations distinctly, and validate the resulting *whole character*. In practice, **reconciliation and manifestation are iterative**: a choice may reveal dependencies or new conflicts, and a conflict may require a different choice. Neither step should be specified as an irreversible one-pass filter.
5. **Reconstruct one current Origin configuration.** Produce a source-conserving, verifiable profile of the final manifested intrinsic characteristics, traits and parameters, preserving complete operational definitions and grant provenance for later trusted Rules Core integration. **Historical contributors are not extra current Species packages**.

**Standard round-trip invariant:** Applying this process to a directly selected standard Species/Ancestry should reproduce its authored configuration and required player choices through the same machinery as mixed Heritage. If decomposition cannot support reconstruction of an unchanged standard source, the model is incomplete; it should not be considered ready to authorize mixed Species.

**Scope of this abstraction:** It handles shared-Species mixed Ancestry, different-Species mixtures, nonbiological origination, nested Heritage and custom/homebrew sources by the same rules. Species-specific patterns and exceptions are **authored data for later study**, not special resolution engines. Separately selected cultural Background/Upbringing is not treated as inherited Species material. A later Rite of Rebirth is a **complete configuration replacement operation**, not a contributor mixture.

## One pipeline, multiple source situations

```text
  Standard source     Mixed direct sources     Previously mixed sources
         |                    |                          |
         +--------------------+--------------------------+
                              |
                    Resolve source evidence
             (including verified provenance)
                              |
                    Candidate characteristic set
             (intrinsics, traits, parameter offerings)
                              |
                   Manifestation + reconciliation
               (explicit choices, general interactions,
               dependencies, conflicts, adjudication;
                 repeat review as choices evolve)
                              |
                     Authorization/adjudication
             (standard, unresolved, DM custom decision)
                              |
                     Proposed Origin profile
                              |
               Later trusted Rules Core integration
```

An ordinary direct standard Species/Ancestry may be a **preset selection** in this model. It is not a second implementation that bypasses source identity, dependencies, or mechanics. The same pipeline must also work when contributors share a Species, come from different Species, or are independently authored homebrew.

**Common source lineage is an optimization of evidence representation, not a grant rule.** It does not remove the need for a manifestation decision, and a character need not have biological parent contributors.

## Universal contracts to establish, in dependency order

| Boundary | Required general behavior | Current evidence and unresolved work |
| --- | --- | --- |
| 1. Contributor references | Identify each contributor occurrence and the content it actually offers; separate provenance history from selectable/manifested characteristics | Direct-source and nested-profile studies exist. Whether nested Heritage transmits only manifested characteristics (excluding latent ancestry) is still a hypothesis, not final policy. |
| 2. Characteristic identity | Distinguish the definition of a mechanic from its source occurrence and selected parameter values; preserve source-authored constraints | Source-qualified IDs and some binding contracts exist. Cross-source **semantic identity** is incomplete; similarly named traits can be different abilities. |
| 3. Candidate availability | Record which characteristics and parameter values could be chosen and why, including unknown/unbound source evidence | Generic candidate pool exists. Eligibility is not manifestation, and a positive parameter candidate test is not permission to use an ability. |
| 4. Manifestation decisions | Express actual intrinsic characteristics, whole traits, parameter selections and dependent choices using one representation | Draft profiles exist but are not authorized states. Choice groups, selection timing, traits with embedded spell/feat/skill choices, and completeness checks need general treatment. |
| 5. Characteristic composition | Define general relationships for distinct traits, equivalence, duplicates, upgrades/replacements, dependencies, exclusions and mutually exclusive intrinsically physical values | Generic review flags and a **synthetic** declarative constraint evaluator exist. No universal approved composition/override rule exists; parent–descendant conflicts such as Elf/Wood Elf movement remain open. |
| 6. Conflict and authority | Distinguish unresolved, incompatible **default**, compatible-under-known-rules, and explicitly adjudicated outcomes without implying balance certification | A proposed mixed-Species disclaimer exists. An exception must have identifiable scope and authority; source-level eligibility and DM customization remain distinguishable. |
| 7. Profile and verification | Return a source-conserving Origin representation that can reconstruct a standard preset and an explicitly selected mixed Origin with identical mechanisms | Profile prototypes exist, but no final schema/authorization, complete standard round trip, or runtime integration is approved. |

Each boundary should be testable in isolation. Do **not** promote a prototype's output status, placeholder field name, or synthetic test constraint into a normative gameplay rule without review.

## General behavior versus optional content policies

**Universal engine responsibilities:** source identification, provenance, canonical characteristic identity, candidate values, parameter binding, manifestation, choice completion, interaction and conflict review, explicit adjudication and immutable/profile output.

**Authored content responsibilities:** definitions of Species/Ancestry characteristics, supported trait parameters, family/category relationships, distinctive behavior, and any narrowly justified compatibility constraints or exceptions.

The **engine** must not contain `if Dragonborn`, `if mixed Species`, or a list of specific ancestry pairings. It should also not assume every Species uses Dragonborn's category/skin-material/damage-type formula. A first-party Species policy and a homebrew Species policy should be interpreted by the **same** general rules. Extra detail for a formulaic Species may be authored **after** the general contracts are sound.

**Mixed-Species coverage remains best-effort**, not an exhaustive pairwise matrix; the accepted [mixed-Species limitation and draft disclaimer](pillar2-heritage-manifestation-scope-20261111.md#mixed-species-limits-balance-expectations-and-table-authority) remain unchanged. DM/table customization is a standing principle of the entire ruleset. An unresolved default does not imply an impossible fictional character.

## Order of proof, before Species-specific tuning

1. **Direct standard Origin:** Reconstruct representative standard Human, Elf and Dragonborn configurations without guessing dependent choices, stacking shared definitions, or losing source-authored behavior. A standard Wood Elf movement value must use an approved standard replacement rule, not an arbitrary high-value or player-choice assumption.
2. **Same-source contributors:** Show that two contributors of the same configuration may produce one equivalent standard manifestation, without multiplying grants. This must hold for at least one unrelated Species, not just Dragonborn.
3. **Distinct characteristics, same Species:** Show that the general candidate/manifestation representation can retain both sets of offered values, report collisions and require only explicit decisions. Do **not** enforce an unapproved ancestry-family selection relationship.
4. **Different Species:** Use exactly the same pipeline; retain competing creature type, size, movement, traits and dependencies, and report missing policy rather than automatically combining or disallowing them.
5. **Nested mixed contributor:** Preserve validated **actual** manifestation evidence separately from historical source lineage; do not accidentally restore whole ancestral source packages. Whether additional latent characteristics can transmit requires a distinct rules decision.
6. **Homebrew/generalized content extension:** Add an independent synthetic Species and a previously unknown Ancestry through content records without adding a new evaluator or assuming a Dragonborn vocabulary.
7. **Unmodeled effect and adjudication:** Unknown mechanics or relationships must be carried forward for review. An explicit and provenance-bearing DM adjustment may resolve an issue, but must not masquerade as an existing first-party effect or silently waive unrelated constraints.

**Important:** These are distinct acceptance scenarios, not a mandate to implement every hypothetical mixed-Species pairing. They must not depend on hard-coded counts of an actively evolving corpus.

## Decisions that must not be preempted

- Whether a nested contributor transmits only **manifested** characteristics or may offer an explicitly modeled latent subset.
- Whether a descendant's authored intrinsic replacement takes precedence over an ancestor's value for **direct standard** selection; this is not necessarily the same question as mixed-contributor selection.
- The universal vocabulary for **equivalence**, duplication, upgrade, substitution and conflicts, including how independent dependent choices are recorded.
- How manifestation budgets or other limits are evaluated, if such limits are introduced. The system should remain capable of testing alternate duplicate-competency policies without inventing a default.
- How a known interaction rule becomes authoritative, how a table override is recorded, and what unresolved status prevents automatic trusted grants.
- Whether ancestry-specific multidimensional choices should be linked, independent or conditionally linked. Dragonborn category/skin material and cross-trait elemental affinity remain **out of scope until the general system** is established.

## Implications for existing Pillar 2 studies

- **Preserve** source inventory, compound binding, candidate pool, draft manifestation profiles and standard-preset readiness as generic research evidence.
- **Preserve but do not prioritize** the direct-source same-Species foundation audit, exhaustive within-Species pair matrix and synthetic compatibility fixture. These are **later validation tools** for content-specific policies and must not become architectural dependencies of the universal engine.
- **Do not add** native Dragonborn mixed-Ancestry exceptions, skin-material mechanics, new ancestry categories, or cross-trait coupling rules in this pass.
- **Keep** Pillar 3 Background/Upbringing cultural selection independent of inherent characteristics.
- **Do not integrate** unfinished Origin grants with the trusted Rules Core runtime, Character Sheet or combat systems merely to test the model.

**Immediate next work:** review the shared characteristic and contributor model for gaps that affect **all** Origins, especially identity, nested-source provenance, dependent choices and conflict semantics. Make source-neutral design proofs and regression tests for those gaps **before** authoring any real Species-specific compatibility contracts.
