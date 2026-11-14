# Pillar 2 — cross-source mechanical definition relationship review

**Date:** 2026-11-11  
**Status:** Read-only general design evidence. No first-party canonical trait mapping, definition equivalence, duplicate-grant policy, upgrade, inherited trait eligibility, or runtime character behavior is approved by this audit.

**Tool:** `tools/character_origins_definition_relationship_review.py`  
**Tests:** `tests/test_character_origins_pillar2_definition_relationship_review.py`  
**Input:** Currently authored native Species and Ancestry trait definitions, including nonselectable family nodes.

## Why this follows characteristic identity

The [characteristic identity study](pillar2-characteristic-identity-boundaries-20261111.md) distinguishes **trait concept**, **complete mechanical definition**, **source occurrence**, **contributor offer**, and **current manifested grant**. It intentionally does not identify which existing native definitions are actually equivalent across sources.

The next dependency is **auditing that relationship without guessing**. The present read-only review considers every source-authored trait exactly once, retains the complete original JSON and qualified occurrence, and surfaces only grounded comparison candidates. It does **not** modify definitions or automatically assign global mechanic IDs.

### Candidate triggers

Two occurrences are compared for review when at least one of the following holds:

- **Same local trait ID** in different sources. This is a name/identifier coincidence, not proof of mechanics.
- **Same explicit rule reference** where both definitions declare one. Their other fields, levels, restrictions, parameters, and prose can still differ.
- **Identical complete nonempty structured mechanical fields**, excluding only `id`, `name`, and `description` from the comparison. Exact match is still not certified semantic identity, since prose can contain essential restrictions and symbolic bindings can resolve through different source lineages.

A matching **single field** (for example, two traits both containing `speedFeet: 30`) is deliberately insufficient to propose a semantic identity between otherwise unrelated abilities.

### Relationship classifications (not rules)

| Evidence | Classification | Meaning |
| --- | --- | --- |
| Complete nonempty structured fields identical | `identical-structured-fields-review` | Stronger mechanical evidence, but prose, source context and bindings must be reviewed |
| One complete field set is a proper subset of another, with a shared ID/rule | `first-structure-subset-of-second-review` or reverse | Possible extension for review, **not** an automatically approved upgrade |
| Related by ID or rule, but structured definitions diverge | `different-structured-fields-review` | Distinct authored behavior must remain distinguishable |
| One or both definitions have no structured mechanical fields | `insufficient-structured-evidence` | Prose/manual content analysis is required; identical empty JSON objects do not establish equivalence |

The report also records each matching/different mechanical field name, whether authored prose differs, and whether symbolic source-dependent bindings occur. For example, two templates referencing `selected-element` are not automatically the same **configured** ability when sourced from different ancestries.

**Every candidate returns** `mechanicalEquivalence: not-established`, `relationshipAuthority: requires-manual-review`, and `grantsOrInheritance: not-evaluated`.

## Representative acceptance evidence

- **Orc versus Goliath Powerful Build:** Same local trait ID, but different structured mechanical behavior. The review must surface a difference rather than declaring equivalence or stacking both.
- **Different source occurrences with identical structured fields:** A plausible same-definition **review** candidate, preserving each original occurrence and prose difference; still no grant merging.
- **Possible source-level extension:** A trait with an additional field can be reviewed as a possible extension only when there is a stronger anchor (same local ID or rule reference). Subset inclusion by itself is not an upgrade decision.
- **Prose-only definitions:** Retained in full and explicitly flagged as lacking sufficient structured proof.
- **Source-dependent symbolic parameters:** Identical templates with unresolved symbolic references require source-context binding review.
- **New homebrew trait:** Appears in the same audit without a Species-specific switch or enumerated source count.
- **Whole first-party corpus:** Every source-authored trait, including currently unselectable family records, remains intact; no authoritative mechanics are changed.

## Next decisions before canonical identity promotion

A manual or separately approved rules-content reconciliation must classify candidate pairs using **complete actual mechanics**, not JSON matching alone. The final first-party registry should support:

1. **Same mechanical definition**, possibly obtained from several distinct source occurrences.
2. **Related but different definitions**, including one ability that adds a distinct effect or changes restrictions.
3. **Overlapping mechanics without full equivalence**, requiring separately authored coexistence/replacement/upgrade semantics.
4. **Unrelated traits sharing a name or field**, which must not collapse.
5. **Unknown or prose-only semantics**, preserving complete source evidence for DM review rather than inventing a mechanical implementation.

**Do not implement general duplicate or replacement rules until the distinction between same definition, overlap and different definition is represented.** A shared concept ID alone is insufficient to determine stacking or grant removal. Rebirth's source-qualified *complete configuration replacement* is a different operation from mixed Heritage characteristic reconciliation.

## CLI

```bash
python3 tools/character_origins_definition_relationship_review.py --summary
python3 tools/character_origins_definition_relationship_review.py --local-id powerful-build
python3 -m unittest discover -s tests -p 'test_character_origins_pillar2_definition_relationship_review.py' -v
```

**Scope:** Experimental design evidence on PR #141 only. This audit intentionally performs no combat mechanics, no automatic Species or Ancestry mixture, no authoritative grants and no balancing. The existing standard/preset and nested-Heritage pipelines remain distinct proofs to be reconciled in the general model.
