"""Review explicit Origin reconciliation decisions without granting abilities.

A reconciliation *relationship* is evidence, not a rule. This module models
proposed dispositions and their source of authority, checks that each targets
an actual relationship in an exact candidate snapshot, and catches
cross-relationship contradictions. It cannot establish whether a decision
is permitted by Rules Core, balance policy, or a DM; no grants are issued.

Provisional dispositions are only study vocabulary. They are not default
inheritance, stacking, substitution, or trait-upgrade rules.
"""
import argparse
import copy
import hashlib
import json

from character_origins_characteristic_identity import (
    project_characteristic_identity_evidence, read_identity_mapping,
)
from character_origins_compound_traits import read_contracts
from character_origins_curated_identity_review import read_curated_evidence
from character_origins_manifestation_inventory import characteristic_candidate_pool
from character_origins_parameter_inheritance import read_blueprints
from character_origins_reconciliation_review import review_proposed_manifestation
from character_origins_source_inventory import read_catalog


DISPOSITIONS = frozenset((
    "preserve-both", "choose-first", "choose-second",
    "consolidate-evidence", "defer",
))
PROPOSAL_AUTHORITIES = frozenset(("design-hypothesis", "table-proposal"))


class ReconciliationDecisionError(ValueError):
    """Decision record is stale, ambiguous, inconsistent or unsupported."""


def _canonical(value):
    try:
        return json.dumps(value, sort_keys=True, ensure_ascii=False,
                          separators=(",", ":"), allow_nan=False)
    except (TypeError, ValueError) as exc:
        raise ReconciliationDecisionError(
            "Decision data must be valid JSON"
        ) from exc


def _nonempty(value):
    return isinstance(value, str) and bool(value.strip())


def proposal_fingerprint(review):
    """Bind provisional decisions to the entire observed candidate snapshot.

    IDs such as evidence-1 are positional preview IDs, not stable character
    identifiers. A snapshot fingerprint prevents applying decisions to a
    different proposal that happens to reuse those IDs.
    """
    if not isinstance(review, dict) or review.get("status") != (
        "draft-reconciliation-evidence-only"
    ) or review.get("authorization") != "not-authorized" or (
        review.get("traitsGranted") is not False
    ) or not isinstance(review.get("pairwiseRelationships"), list) or (
        not isinstance(review.get("selectedCandidateEvidence"), list)
    ) or not isinstance(review.get("proposedCandidateIds"), list):
        raise ReconciliationDecisionError(
            "Expected a non-authoritative reconciliation preview"
        )
    return hashlib.sha256(_canonical(review).encode("utf-8")).hexdigest()


def _is_consolidation_evidence(relation):
    kind = relation["relation"]
    # The same complete configured trait or exact intrinsic value can be
    # represented with one *proposed* result carrying two provenances.
    # Mere conceptual kinship, local name or partial overlap cannot be
    # consolidated without an explicit future mechanical policy.
    return (
        kind == "same-intrinsic-slot-and-value" or
        kind in (
            "repeated-source-trait-same-configuration",
            "explicit-identity-mapping-same-configuration",
            "identical-authored-content-same-configuration",
        )
    )


def _validate_decision(decision, relation):
    if not isinstance(decision, dict) or set(decision) != {
        "firstCandidateId", "secondCandidateId", "relation",
        "disposition", "authority", "rationale",
    }:
        raise ReconciliationDecisionError("Malformed decision entry")
    if any(decision[key] != relation[key] for key in (
        "firstCandidateId", "secondCandidateId", "relation"
    )):
        raise ReconciliationDecisionError(
            "Decision does not match the exact observed relationship"
        )
    disposition = decision["disposition"]
    if disposition not in DISPOSITIONS:
        raise ReconciliationDecisionError("Unknown disposition")
    authority = decision["authority"]
    if not isinstance(authority, dict) or set(authority) != {
        "kind", "reference"
    } or authority["kind"] not in PROPOSAL_AUTHORITIES or not _nonempty(
        authority["reference"]
    ):
        raise ReconciliationDecisionError(
            "Only explicit proposed authority references are allowed"
        )
    if not _nonempty(decision["rationale"]):
        raise ReconciliationDecisionError("A recorded decision needs rationale")
    if disposition == "consolidate-evidence" and not (
        _is_consolidation_evidence(relation)
    ):
        raise ReconciliationDecisionError(
            "Insufficient identity evidence for provisional consolidation"
        )


def examine_reconciliation_decisions(review, decision_contract):
    """Check proposed relation decisions and global keep/suppress consistency.

    A fully covered and internally consistent *proposal* is still not an
    authorized or playable Origin. Required characteristics, choice closure,
    mechanics, inheritance eligibility and balance await later rules.
    """
    fingerprint = proposal_fingerprint(review)
    if not isinstance(decision_contract, dict) or set(decision_contract) != {
        "status", "proposalFingerprint", "decisions",
    } or decision_contract["status"] != "experimental-decision-proposal":
        raise ReconciliationDecisionError("Invalid decision contract envelope")
    if decision_contract["proposalFingerprint"] != fingerprint:
        raise ReconciliationDecisionError(
            "Decision references a stale or different candidate snapshot"
        )
    decisions = decision_contract["decisions"]
    if not isinstance(decisions, list):
        raise ReconciliationDecisionError("Decision records must be a list")

    indexed = {}
    for item in review["pairwiseRelationships"]:
        if not isinstance(item, dict) or not all(
            _nonempty(item.get(key)) for key in (
                "firstCandidateId", "secondCandidateId", "relation"
            )
        ) or item.get("resolution") != "unresolved-no-grants":
            raise ReconciliationDecisionError(
                "Reconciliation preview contains unsupported relationship state"
            )
        pair = (item["firstCandidateId"], item["secondCandidateId"])
        if pair in indexed or pair[0] == pair[1]:
            raise ReconciliationDecisionError(
                "Repeated or self-referential relationship in preview"
            )
        indexed[pair] = item
    proposals = review["proposedCandidateIds"]
    if len(set(proposals)) != len(proposals) or any(
        pair[0] not in proposals or pair[1] not in proposals for pair in indexed
    ):
        raise ReconciliationDecisionError(
            "Relationships refer to unselected or duplicate candidates"
        )

    recorded = {}
    considered = []
    kept_by = {}
    excluded_by = {}
    for decision in decisions:
        if not isinstance(decision, dict):
            raise ReconciliationDecisionError("Malformed decision record")
        pair = (
            decision.get("firstCandidateId"),
            decision.get("secondCandidateId"),
        )
        if not all(_nonempty(value) for value in pair):
            raise ReconciliationDecisionError(
                "Decision references malformed candidate identifiers"
            )
        if pair not in indexed:
            raise ReconciliationDecisionError(
                "Decision does not refer to an observed relationship"
            )
        if pair in recorded:
            raise ReconciliationDecisionError(
                "Relationship received multiple competing decisions"
            )
        relation = indexed[pair]
        _validate_decision(decision, relation)
        recorded[pair] = copy.deepcopy(decision)
        disposition = decision["disposition"]
        first, second = pair
        if disposition == "preserve-both":
            for candidate in pair:
                kept_by.setdefault(candidate, []).append(pair)
        elif disposition in ("choose-first", "consolidate-evidence"):
            kept_by.setdefault(first, []).append(pair)
            excluded_by.setdefault(second, []).append(pair)
        elif disposition == "choose-second":
            kept_by.setdefault(second, []).append(pair)
            excluded_by.setdefault(first, []).append(pair)
        considered.append({
            "firstCandidateId": first,
            "secondCandidateId": second,
            "relation": relation["relation"],
            "proposedDisposition": disposition,
            "proposedAuthority": copy.deepcopy(decision["authority"]),
            "rationale": decision["rationale"],
            "status": (
                "explicitly-deferred" if disposition == "defer"
                else "pending-actual-policy-authorization"
            ),
        })

    # Multi-contributor reconciliations cannot be validated one pair at a
    # time. A candidate explicitly kept for one relation cannot be removed
    # by another without a further, explicit whole-profile rule.
    contradictions = [
        {
            "candidateId": candidate,
            "retainedBy": copy.deepcopy(kept_by[candidate]),
            "suppressedBy": copy.deepcopy(excluded_by[candidate]),
            "status": "contradictory-proposed-dispositions",
        }
        for candidate in proposals
        if candidate in kept_by and candidate in excluded_by
    ]
    uncovered = [
        {
            "firstCandidateId": relation["firstCandidateId"],
            "secondCandidateId": relation["secondCandidateId"],
            "relation": relation["relation"],
            "status": "unreviewed",
        }
        for pair, relation in indexed.items() if pair not in recorded
    ]
    deferred = [
        x for x in considered if x["status"] == "explicitly-deferred"
    ]
    # These can be recorded for discussion, but remain logically unresolved
    # in the general single-valued slot model without a different approved
    # model for the intrinsic dimension.
    slot_conflicts = [
        {
            "firstCandidateId": item["firstCandidateId"],
            "secondCandidateId": item["secondCandidateId"],
            "relation": item["relation"],
            "status": "requires-special-intrinsic-slot-policy",
        }
        for item in considered
        if item["proposedDisposition"] == "preserve-both"
        and indexed[(item["firstCandidateId"],
                     item["secondCandidateId"])]["relation"] in (
            "competing-intrinsic-slot-values",
            "alternatives-in-one-authored-intrinsic-choice",
        )
    ]
    review_status = (
        "contradictory-proposal" if contradictions
        else "incomplete-relation-decisions"
        if uncovered or deferred
        else "requires-additional-intrinsic-policy"
        if slot_conflicts else "complete-proposal-not-authorized"
    )

    return {
        "proposalFingerprint": fingerprint,
        "proposedCandidateIds": copy.deepcopy(proposals),
        "reviewedDecisions": considered,
        "unreviewedRelationships": uncovered,
        "explicitlyDeferredRelationships": copy.deepcopy(deferred),
        "globalContradictions": contradictions,
        "intrinsicSlotPolicyGaps": slot_conflicts,
        "sourceReviewFlags": copy.deepcopy(review["sourceReviewFlags"]),
        "deferredSourceFields": copy.deepcopy(
            review["deferredSourceFields"]
        ),
        "candidatePoolEvidenceStatus": review.get(
            "candidatePoolEvidenceStatus",
            "not-reported",
        ),
        "reviewStatus": review_status,
        "fullDependencyClosure": "not-evaluated",
        "selectionEligibility": "not-evaluated",
        "grantsIssued": False,
        "authorization": "not-authorized",
        "balance": "not-evaluated",
        "status": "proposed-reconciliation-decisions-only",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Evaluate provisional source-neutral Origin decisions"
    )
    parser.add_argument("--contributors", nargs="+", required=True)
    parser.add_argument("--select", nargs="+", required=True)
    parser.add_argument(
        "--decisions-json", required=True,
        help="JSON array of tentative decision entries",
    )
    args = parser.parse_args()
    catalog = read_catalog()
    pool = characteristic_candidate_pool(
        [{"contributorId": f"contributor-{n + 1}",
          "selectedSourceId": source}
         for n, source in enumerate(args.contributors)],
        {}, read_blueprints(), catalog, read_contracts()
    )
    projected = project_characteristic_identity_evidence(
        pool, read_identity_mapping(),
        curated=read_curated_evidence(), catalog=catalog,
    )
    review = review_proposed_manifestation(projected, args.select)
    try:
        records = json.loads(args.decisions_json)
    except ValueError as exc:
        parser.error(f"Invalid decisions JSON: {exc}")
    report = examine_reconciliation_decisions(
        review, {
            "status": "experimental-decision-proposal",
            "proposalFingerprint": proposal_fingerprint(review),
            "decisions": records,
        }
    )
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
