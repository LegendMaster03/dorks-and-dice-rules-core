# Pillar 2 — explicit reconciliation decision contract

**Date:** 2026-11-11  
**Status:** Read-only study vocabulary, not an approved Character Origins composition policy, an authorized DM decision, a gameplay rules engine, or a completed mechanical Heritage profile.

**Tool:** `tools/character_origins_reconciliation_decisions.py`  
**Tests:** `tests/test_character_origins_pillar2_reconciliation_decisions.py`

## Purpose

The [general reconciliation preview](pillar2-general-reconciliation-preview-20261111.md) identifies relationships among **proposed characteristic offers**. It does not decide what happens to them. A correct universal Origin engine needs a separate, auditable boundary between **observed interaction** and an **explicit, attributable resolution decision**, because the same characteristic identity relationship might have different outcomes under different rules, table customizations or playtest policies.

This first contract is deliberately **a proposal-of-decisions study**, not the final resolution engine. Even where the proposed decisions are complete and internally consistent, it does **not** transform candidates into grants or assert that the resulting Heritage is compatible or balanced.

## Proposed record structure

A decision document contains a `status: experimental-decision-proposal`, a `proposalFingerprint` and a list of `decisions`.

A decision records:

- **Exact relationship:** the first and second ephemeral candidate IDs **and** the relationship kind from the reviewed proposal. A name, Species ID, or rule reference alone cannot substitute for this source-backed observation.
- **Proposed disposition:** `preserve-both`, `choose-first`, `choose-second`, `consolidate-evidence`, or `defer`.
- **Proposed authority:** `design-hypothesis` or `table-proposal` with a nonempty reference. These are **claims of provenance for consideration**, not actual authority to override rules or grant an ability.
- **Rationale:** human-readable explanation for why the disposition should be considered.

The fingerprint is computed from the complete reconciliation preview, including selected candidate evidence, relation kinds, configured values, and source diagnostics. It ensures a decision cannot silently transfer to an unrelated or updated preview simply because both happen to number their candidates `evidence-1` and `evidence-2`. These ephemeral candidate IDs must **not** be persisted as permanent grant IDs.

### What a disposition means at this stage

| Disposition | Study-level meaning | Not implied |
| --- | --- | --- |
| `preserve-both` | Propose keeping two candidates for later mechanical adjudication | That their effects stack or are compatible |
| `choose-first` | Propose retaining the first and suppressing the second | An approved rule allowing that replacement |
| `choose-second` | Propose retaining the second and suppressing the first | An approved rule allowing that replacement |
| `consolidate-evidence` | Propose one candidate with provenance from both only when identity evidence supports the **same configured trait** or identical intrinsic value | Grant merging, resource-pool merging, mechanical equivalence, or an upgrade |
| `defer` | Leave the relationship explicitly unresolved | Permission to ignore it |

`consolidate-evidence` is **rejected** for different Breath Weapon parameter configurations, conceptual kinship alone (for example, Orc and Goliath Powerful Build), coincidentally shared names, or a partial component overlap. This restriction protects source identity: a future policy may allow a richer composition, but the study may not invent it.

A proposal to preserve **both differing values of one intrinsic slot** (such as creature type) or both options in a single authored size choice is reported as requiring an **additional intrinsic-slot policy**. It is not silently accepted as an ordinary, valid Origin state. A DM customization may still be designed later with explicit constraints.

## Whole-proposal consistency, not just pairwise review

A candidate can appear in several relationships. Independently accepting each pair can produce an impossible overall selection. For example, one relationship might propose preserving a candidate while another proposes suppressing that same candidate.

The validator reports `globalContradictions` when a candidate is explicitly both retained and suppressed across decisions. It also reports all `unreviewedRelationships`, `explicitlyDeferredRelationships`, and `intrinsicSlotPolicyGaps`. No unrelated relationships are manufactured and no implicit decisions are inserted.

Possible `reviewStatus` outputs:

- `contradictory-proposal`: mutually inconsistent proposed keep/suppress decisions.
- `incomplete-relation-decisions`: at least one relationship is undecided or explicitly deferred.
- `requires-additional-intrinsic-policy`: an otherwise complete proposal would preserve incompatible alternatives for one standard single-valued intrinsic slot.
- `complete-proposal-not-authorized`: every observed pair was addressed without the specific contradictions checked here. **This is not a statement that the character is valid, fully specified, or even mechanically compatible.**

Every result keeps `fullDependencyClosure: not-evaluated`, `selectionEligibility: not-evaluated`, `authorization: not-authorized`, `balance: not-evaluated`, and `grantsIssued: false`.

The validator retains **source-wide diagnostics separately** from conflicts in the actual proposed selection. A Wood Elf source-level movement discrepancy does not become a Druidcraft-only proposed-selection conflict.

## Current general coverage

The tests include Human + High Elf competing creature types, Human Small/Medium alternatives, repeated Red Dragonborn Breath Weapon, differently configured Red/Bronze Breath Weapons, literal identical Amphibious, Orc/Goliath Powerful Build, unrelated characteristics, nested mixed contributor provenance, and a synthetic three-contributor contradiction.

These examples cover **standard, mixed Ancestry, mixed Species, nested Heritage, and candidate-level custom policy concepts** using one general contract. None of them authorizes a particular mixed-Species combination or establishes an actual DM override.

## What must follow before reconstruction

1. **Define authoritative rule and table-decision provenance.** A `table-proposal` string is not proof of the DM's approval or authorization of an action. Trusted Rules Core must eventually validate actual rule IDs/versions and explicitly authorized table decisions.
2. **Define candidate selection eligibility.** A selected characteristic must be demonstrably eligible under the selected standard, inherited, partially custom, or fully custom policy. Catalog presence or a previous ancestor's Species name is insufficient.
3. **Define whole-profile dependency closure.** Trait prerequisites, mutually exclusive grants, linked parameters, embedded choices, resources, level gates and character-wide invariants must all be evaluated **after** tentative reconciliation, not inferred solely from pairwise relationships.
4. **Define canonical current grant instances and provenance.** Consolidating evidence must not merge distinct finite-use resource pools or lose which contributor offered each candidate; the resulting representation must support nested inheritance.
5. **Prove unchanged standard Species/Ancestry round-trip equivalence** through this same universal pipeline before permitting mixed-Species profiles to become authoritative.
6. **Evaluate balancing separately.** A logically reconciled profile is not necessarily balanced. The accepted mixed-Species limitations and DM customization framework remain applicable.

The **three workflow stages** remain Deconstruct/Assemble, Reconcile/Manifest (iterative), and Reconstruct. The five internal operations remain available for validation and implementation. This document models the **decision-recording boundary inside Stage 2** only.

## Commands

```bash
python3 -m unittest discover -s tests \
  -p 'test_character_origins_pillar2_reconciliation_decisions.py' -v

# Candidate IDs are positional; inspect the current proposal before using them.
python3 tools/character_origins_reconciliation_review.py \
  --contributors species.human ancestry.elf.high \
  --select evidence-1 evidence-10
```

This is a **design-only branch study**. No native first-party Origin rules, trusted grants, Character Sheet APIs, or `main` branch data are modified by the proposed contract.
