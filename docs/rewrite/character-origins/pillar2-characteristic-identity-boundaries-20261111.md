# Pillar 2 — characteristic identity, source offers and manifestation boundaries

**Date:** 2026-11-11  
**Status:** General model study and read-only prototype; no canonical trait-equivalence decisions, new grants, inherited selection policy, custom character creation mode or trusted runtime state is authorized.

**Tool:** `tools/character_origins_characteristic_identity.py`  
**Fixture:** `pillar2-synthetic-characteristic-identity-mapping-20261111.json`  
**Tests:** `tests/test_character_origins_pillar2_characteristic_identity.py`

## The distinction the universal engine must make

A standard Species/Ancestry, a previously mixed contributor, and a freely authored custom source must all be representable without forcing them into the same **eligibility** category. Their mechanical characteristic **identities**, however, must be described by the same general vocabulary.

| Identity layer | What it represents | Must not be confused with |
| --- | --- | --- |
| **Characteristic dimension / slot** | A property that can be configured, such as `creature.type` or `movement.walk.speed` | A selected value for that property or an entire Species package |
| **Trait concept** | A broad family of related abilities (for example, two distinct implementations of Powerful Build) | Proof that their full mechanical definitions are equivalent |
| **Mechanical definition** | The exact authored trait behavior, including constraints, effects, progression and parameter slots | The source that grants it or a particular selected configuration |
| **Authored source occurrence** | A definition's placement within a particular Species/Ancestry source chain | A different source that happens to use the same name or mechanic |
| **Contributor offer** | A characteristic identity and complete candidate values from a particular contributor; this is eligibility **evidence** | A character's authorized or acquired ability |
| **Manifested/acquired grant** | An actual selected and authorized characteristic instance in the new configuration, with grant provenance | A source's unused offer or a contributor's historical genealogy |

**Parameters are a distinct axis**, not automatically different trait definitions: one Dragonborn Breath Weapon definition may have several offered damage-type or complete-area configurations. Conversely, distinct authored abilities may have identical local names or some similar facets while remaining mechanically different.

## Current prototype

The new read-only projection consumes the existing `characteristic_candidate_pool`, preserving each original offer verbatim along with all deferred source evidence and review flags. It adds annotations for intrinsic slot identity, an **optional explicit** trait concept, an **optional explicit** mechanical definition identity, and contributor provenance. It leaves `grantInstanceId: null`, `manifestation: not-selected`, and `authority: not-authorized` for every candidate.

There is deliberately **no automatic semantic identity inference** from local trait name, prose wording, matching JSON fields, rule reference, Species ancestry, or coincidentally equal parameters.

- Intrinsic fields with existing recognized dimensions expose their shared **slot ID** and distinct offered values.
- Traits without explicit mappings retain their original source-qualified evidence ID and show `cross-source-identity-unresolved`. This is not an error or a reason to discard them.
- Explicit mapped trait IDs can share a **concept** while remaining different **mechanical definitions**.
- Explicit mapped traits can share a **mechanical definition** but still appear as **separate contributor offers**, without acquiring two grants or merging activation/resources.
- A nested contributor's previously selected source trait remains identifiable through its original trait identity, while its immediate evidence identifies the **manifested profile contributor**. Revalidation and transmit-eligibility are handled by the existing separate prototype.
- A proposed fully custom characteristic may be represented as an offer **without a fabricated parent or standard Species** when it explicitly carries a proposed authoring-policy reference. The prototype does **not** approve, select, validate, price or grant such custom content.

All trait equivalences in the accompanying mapping fixture are prefixed `study.` and are **synthetic demonstrations**, not adopted first-party identities. In particular, Orc and Goliath Powerful Build are placed in one conceptual family with **different** mechanical definition IDs; Orc and Elf Low-Light Vision demonstrate how two occurrences *could* share one explicitly mapped definition without becoming a double grant. Neither mapping automatically changes the active rules.

## Why this comes before mixed Species

The common engine cannot correctly decide duplicates, inheritance, replacements, or synergistic traits until it distinguishes:

1. **Same slot, different value:** e.g., an Elf and Human offering different creature types. The conflict is about the selected property, not two conflicting definitions of a named trait.
2. **Same concept, different behavior:** e.g., the Orc and Goliath Powerful Build variants. Grouping by name would silently grant or erase actual benefits.
3. **Same definition, different sources:** A standard or mixed contributor can provide an identical defined mechanic, but the system must retain grant provenance and explicitly decide how duplicates behave.
4. **Same definition, configurable values:** Two contributors may offer one complete trait with different eligible parameter choices, yet a manifested instance must carry its own selected values and provenance.
5. **Unrecognized/new mechanic:** Homebrew, future content and partially modeled native prose cannot be treated as automatically executable or mechanically equivalent to something merely similar.

A **reconstructed Origin profile** is the result of manifestation, reconciliation and authorization. The identity projection is not that result: it is a lossless evidence layer that supplies the raw distinctions the eventual general composer needs.

## Acceptance examples and boundaries

The regression suite uses:

- Human + High Elf to demonstrate one intrinsic slot with competing `humanoid` and `fey` values.
- Two identical Red Dragonborn source contributors to preserve separate offers without duplicating a complete Breath Weapon grant.
- Orc + Cloud Goliath Powerful Build to distinguish conceptual kinship from mechanical equivalence.
- Orc + High Elf Low-Light Vision to demonstrate explicit, provisional definition grouping without collapsing source grants.
- A verified nested Elf contributor to preserve immediate profile provenance without restoring unmanifested source traits.
- A synthetic source-free custom proposal to distinguish explicit proposed table-authority provenance from fictitious Species lineage.
- Every currently selectable source in the native catalog to check source-offer conservation and no loss of earlier review flags.

**No guarantee is made about final mixed-Heritage compatibility or balance.** The same generic identity rules apply whether a character uses one Species, mixed Ancestries, mixed Species or fully custom construction.

## Cross-source relationship audit

The companion [definition relationship review](pillar2-definition-relationship-review-20261111.md) now audits all authored Species/Ancestry trait definitions, including unselectable parent families, for repeated local trait IDs, shared explicit rule references and identical complete structured fields. It flags possible structural extension relationships, differing prose and unresolved symbolic bindings. **None of these candidates is assumed equivalent or automatically converted into an upgrade or additional grant.** The result is a review queue for future canonical-definition decisions, not a substitute for them.

## Next substantive requirements

1. **Review canonical mechanical identity assignments.** A final authoritative registry must be grounded in complete mechanic behavior and constraints, not simply a matching name or field. Where exact equivalence is unknown, leave it unresolved.
2. **Represent authoring and grant provenance fully.** A selected characteristic needs an authoritative definition, an eligibility/selection basis, and a distinct actual grant occurrence. These layers are not interchangeable.
3. **Define general duplicate/overlap/replacement behavior.** The identity mapping deliberately does not choose whether two grants coexist, overlap, replace one another, or form an upgrade.
4. **Finalize contributor transmission and custom-selection eligibility.** A mixed parent may carry manifested traits and potentially explicitly modeled latent inheritance; a fully custom catalog may expose a different permitted choice set. Neither path should bypass validation.
5. **Prove lossless standard reconstruction**, then introduce mixed characteristic composition through the same engine. Source-neutral reconciler tests should come before Dragonborn or other Species-specific pairing rules.

This remains a **research artifact** on the Pillar 2 feature branch, not a final native JSON schema or trusted profile implementation. The standing mixed-Species disclaimer and separate Background/Upbringing selection remain unchanged.
