# Pillar 2 — curated native trait identity evidence and drift guard

**Date:** 2026-11-11  
**Status:** Read-only curated evidence against the active native Species/Ancestry corpus. No first-party mechanic identity, automatic grant, duplicate-stacking policy, inheritance permission, or balance judgment is approved.

**Tool:** `tools/character_origins_curated_identity_review.py`  
**Fixture:** `pillar2-curated-native-identity-evidence-20261111.json`  
**Tests:** `tests/test_character_origins_pillar2_curated_identity_review.py`

## Purpose

The [cross-source relationship audit](pillar2-definition-relationship-review-20261111.md) automatically identifies plausible matches and differences. The next general step is to **curate a small number of concrete source-occurrence relationships**, with explicit evidence and a validation guard that detects subsequent content changes.

This is an **identity-evidence registry**, not the future authorized canonical identity registry. It is deliberately separated from source-specific inheritance policies, manifestation decisions and grant behavior.

## Current exact authored-definition groups

These are the first groups whose complete native definitions, **including local ID, name, prose and structured fields**, can be verified as identical across their listed source occurrences.

| Evidence candidate | Occurrences | Verification |
| --- | --- | --- |
| Amphibious | Black, Green, Bronze and Gold Dragonborn | All four authored definitions are identical, including air/water breathing and the explicit absence of a granted swim speed |
| Low-Light Vision | Dragonborn, Elf, Gnome and Orc | All four authored definitions are identical, including the shared rule reference and explanation |

**Important distinction:** An exact authored-text match is stronger evidence than a repeated local trait ID, but it still does **not** determine how the character gains or stacks the ability. In a future approved mapping, two sources could share one mechanical definition with separate source occurrences and contributor provenance. That does not automatically mean two character grants.

The identifiers in the evidence JSON are prefixed `study.definition.` and are **not** active Rules Core mechanic identifiers.

## Near matches retained as unresolved reviews

| Review case | Evidence found | Why it remains unresolved |
| --- | --- | --- |
| Tiefling Low-Light Vision versus Orc | Same structured `rule.low-light-vision` reference; descriptions differ | Strong candidate for semantic identity, but the final rule and explanatory wording should be checked before promoting it |
| Red versus Copper Natural Climber | Same complete structured mechanics: Climb Speed 15 feet, with identical explicit exclusions; descriptions differ slightly | Likely editorial wording variation; requires a reviewed equivalence decision rather than an inferred grant merge |
| Orc versus Goliath Powerful Build | Same local ID but different structured behavior | **Not fully equivalent:** Orc includes carrying/pushing/dragging/lifting adjustments; Goliath includes grapple-escape advantage and carrying capacity. Both must remain distinguishable; any shared-effect handling requires a separate component-overlap rule |

These are **review recommendations**, not source changes or newly approved cross-source identities. Broader cases such as partially overlapping senses, resistances, and movement effects are not inferred from superficial field matches.

## Drift guard behavior

The validation tool checks each curated occurrence against the **current native source definitions**:

- A literal group must contain at least two distinct, still-existing source occurrences whose **complete authored JSON definitions are identical**. If even prose changes, the group is invalidated for review.
- A near-match review must continue to have its explicitly recorded comparison type (identical nonempty structured mechanics but different prose, or same local ID with different structured mechanics).
- Candidate/review IDs must be unique; conflicting literal-group membership, malformed records, retired source occurrences and unsupported evidence claims fail instead of silently changing their meaning.
- New native Species or Ancestries are not blocked merely because a trait count changes. They are **not implicitly added to existing groups**; new occurrences need review and updated evidence.

The output contains **`semanticEquivalence: not-promoted`** for exact authored groups, **`semanticEquivalence: unresolved`** for near matches, and **`grantOrDuplicatePolicy: not-evaluated`** for all entries. No characteristic is selected or granted.

## Verified evidence overlay for current native offers

The existing `character_origins_characteristic_identity.py` evidence projection can optionally use this curated registry **after** validating it against the current native catalog:

```bash
python3 tools/character_origins_characteristic_identity.py \
  --contributors ancestry.dragonborn.black ancestry.dragonborn.green \
  --curated-native-evidence
```

A trusted direct native-source trait offer matching a curated literal group receives `literalAuthoredIdentityCandidateId` and `literalAuthoredIdentityStatus: verified-native-authored-content-only`, while retaining the entire original offer, its configured parameters and its independent contributor provenance. **The candidate is still unselected and unauthorized.**

The projection **checks the offer's actual authored definition against the catalog**. A caller cannot obtain a verified native identity annotation merely by assigning a matching string to an unrelated custom trait. Nested manifested profiles continue preserving their historical source identity but are **not automatically promoted** as newly verified native-source offers; their current provenance must be validated through the separate nested-profile pipeline. An unrecognized or changed native definition requires fresh review.

This bridges curated real-world source evidence into the universal candidate representation without creating a duplicate rule, manifested grant, inheritance permission or player-selectable custom catalog.

## Relationship to the three-stage Origin workflow

This evidence supports **deconstruct and assemble**: a trait definition can be identified precisely, and the origin of each contributor's offer can be preserved. It does not yet implement **reconcile and manifest**; the rules for duplicate grants, upgrades, overlapping mechanics, prerequisites, incompatibilities and selection permissions remain to be written.

A nested mixed parent can eventually provide a previously manifested copy of a mechanic even when its original Species differs from the new character. The system must preserve that **actual contributor's state** without reconstructing a full ancestor's package or inventing a grant.

A fully custom character may someday select a permitted mechanic without a biological contributor. That is an **eligibility/authorization policy**, not a second trait-definition system. The current curated registry does not authorize custom selection.

## Initial general reconciliation preview

The companion [general proposed-characteristic reviewer](pillar2-general-reconciliation-preview-20261111.md) can consume the read-only identity projection, including curated literal identity annotations. It identifies when two proposed Amphibious offers use identical authored content and when comparable candidate definitions have distinct parameter configurations. **This only produces unresolved review relationships**. It does not modify the curated semantic review queue, turn a provisional definition ID into a grant, or select a duplicate-handling policy.

## Acceptance and next design work

1. Continue **careful canonical trait review** of complete authored effects, particularly cases whose names or structures look similar but whose restrictions differ.
2. Define the authoritative **definition ID** contract so the exact same reusable definition can appear under different source occurrences without merging source provenance.
3. Define general **duplicate grant**, **overlap**, **upgrade** and **replacement** relationships *separately* from identity. Sharing a definition is not, by itself, an instruction to stack, suppress or modify abilities.
4. Demonstrate an unchanged standard Species/Ancestry **round trip** through the common characteristic representation before approving mixed-Species reconstruction.
5. Keep the intended cross-Species compatibility disclaimer, separate cultural Upbringing, and full Species/Ancestry replacement semantics for Rites of Rebirth intact.

### CLI

```bash
python3 tools/character_origins_curated_identity_review.py
python3 -m unittest discover -s tests \
  -p 'test_character_origins_pillar2_curated_identity_review.py' -v
```

**Scope:** Research-only characteristic identity evidence on the existing Pillar 2 feature branch, not a new native gameplay record or player-facing rule.
