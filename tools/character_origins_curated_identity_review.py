"""Drift-checked native trait identity evidence, not approved equivalence rules.

Curated groups reference actual first-party source occurrences. An
identical-authored group must still have the same COMPLETE definition,
including prose; a near-match review must still have its expected structural
relationship. Source edits invalidate stale judgments rather than quietly
granting equality or replacing earlier behavior.

This is deliberately not a canonical identity registry, mechanical resolver,
inheritance policy, grant merger, or user-facing authoring whitelist.
"""
import argparse
import copy
import json
from pathlib import Path

from character_origins_definition_relationship_review import (
    _occurrences, _json_key,
)
from character_origins_source_inventory import read_catalog


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_EVIDENCE = (
    ROOT / "docs/rewrite/character-origins/"
    "pillar2-curated-native-identity-evidence-20261111.json"
)
EVIDENCE_TYPES = frozenset((
    "same-structure-different-prose",
    "same-local-id-different-structure",
))


class CuratedIdentityReviewError(ValueError):
    """Curated identity claims no longer match their native source evidence."""


def _nonblank(value):
    return isinstance(value, str) and bool(value.strip())


def read_curated_evidence(path=DEFAULT_EVIDENCE):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def audit_curated_identity_evidence(catalog, evidence):
    """Check explicit identity evidence against current source definitions.

    Successful structural validation never creates a mechanical identity,
    selects/grants a trait, verifies a playable Origin, or certifies balance.
    """
    if not isinstance(evidence, dict) or set(evidence) != {
        "status", "identicalAuthoredGroups", "semanticReviewGroups"
    } or evidence["status"] != "review-evidence-only":
        raise CuratedIdentityReviewError("Invalid curated review envelope")

    exact_groups = evidence["identicalAuthoredGroups"]
    review_groups = evidence["semanticReviewGroups"]
    if not isinstance(exact_groups, list) or not isinstance(
        review_groups, list
    ):
        raise CuratedIdentityReviewError("Review groups must be lists")

    occurrences = _occurrences(catalog)
    indexed = {record["sourceOccurrenceId"]: record for record in occurrences}
    used_literal_occurrences = set()
    used_ids = set()
    literal_results = []
    review_results = []

    def require_occurrences(ids):
        if (not isinstance(ids, list) or len(ids) < 2
                or any(not _nonblank(s) for s in ids)
                or len(set(ids)) != len(ids)):
            raise CuratedIdentityReviewError(
                "Evidence requires at least two distinct occurrences"
            )
        missing = [sid for sid in ids if sid not in indexed]
        if missing:
            raise CuratedIdentityReviewError(
                "Unknown or retired source trait occurrence: " +
                ", ".join(missing)
            )
        return [indexed[sid] for sid in ids]

    for group in exact_groups:
        if not isinstance(group, dict) or set(group) != {
            "candidateDefinitionId", "sourceOccurrenceIds", "evidence"
        } or group.get("evidence") != "same-complete-authored-definition":
            raise CuratedIdentityReviewError("Invalid literal-identity group")
        identity = group["candidateDefinitionId"]
        if not _nonblank(identity) or identity in used_ids:
            raise CuratedIdentityReviewError(
                "Duplicate or empty candidate identity"
            )
        used_ids.add(identity)
        records = require_occurrences(group["sourceOccurrenceIds"])
        if any(
            row["sourceOccurrenceId"] in used_literal_occurrences
            for row in records
        ):
            raise CuratedIdentityReviewError(
                "An authored trait has conflicting literal-identity groups"
            )
        original = _json_key(records[0]["authoredTrait"])
        if any(
            _json_key(record["authoredTrait"]) != original
            for record in records[1:]
        ):
            raise CuratedIdentityReviewError(
                "Complete authored definition drift in " + identity
            )
        used_literal_occurrences.update(
            row["sourceOccurrenceId"] for row in records
        )
        literal_results.append({
            "candidateDefinitionId": identity,
            "sourceOccurrenceIds": copy.deepcopy(group["sourceOccurrenceIds"]),
            "verifiedEquality": "identical-complete-authored-content",
            "semanticEquivalence": "not-promoted",
            "grantOrDuplicatePolicy": "not-evaluated",
        })

    for group in review_groups:
        if not isinstance(group, dict) or set(group) != {
            "reviewId", "sourceOccurrenceIds", "expectedEvidence", "notes"
        }:
            raise CuratedIdentityReviewError("Invalid semantic review entry")
        identity = group["reviewId"]
        if (not _nonblank(identity) or identity in used_ids
                or not _nonblank(group["notes"])
                or group["expectedEvidence"] not in EVIDENCE_TYPES):
            raise CuratedIdentityReviewError(
                "Malformed or duplicate semantic review"
            )
        used_ids.add(identity)
        if len(group["sourceOccurrenceIds"]) != 2:
            raise CuratedIdentityReviewError(
                "A near-match review must compare exactly two occurrences"
            )
        first, second = require_occurrences(group["sourceOccurrenceIds"])
        same_structure = (
            bool(first["mechanicalFields"])
            and _json_key(first["mechanicalFields"])
            == _json_key(second["mechanicalFields"])
        )
        prose_different = (
            first["authoredTrait"].get("description")
            != second["authoredTrait"].get("description")
        )
        same_local_id = first["localTraitId"] == second["localTraitId"]
        if group["expectedEvidence"] == "same-structure-different-prose":
            valid = same_structure and prose_different
        else:
            valid = same_local_id and not same_structure
        if not valid:
            raise CuratedIdentityReviewError(
                "Near-match evidence changed for " + identity
            )
        review_results.append({
            "reviewId": identity,
            "sourceOccurrenceIds": copy.deepcopy(group["sourceOccurrenceIds"]),
            "observedEvidence": group["expectedEvidence"],
            "reviewNotes": group["notes"],
            "semanticEquivalence": "unresolved",
            "grantOrDuplicatePolicy": "not-evaluated",
        })

    return {
        "identicalAuthoredGroups": literal_results,
        "semanticReviewQueue": review_results,
        "sourceTraitDefinitionCount": len(occurrences),
        "literalOccurrenceCoverageCount": len(used_literal_occurrences),
        "remainingSourceTraitDefinitions": (
            len(occurrences) - len(used_literal_occurrences)
        ),
        "status": "native-identity-evidence-verified-no-semantic-promotion",
        "authority": "not-authorized",
        "balance": "not-evaluated",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Validate curated native identity evidence without grants"
    )
    parser.add_argument(
        "--evidence", type=Path, default=DEFAULT_EVIDENCE
    )
    args = parser.parse_args()
    result = audit_curated_identity_evidence(
        read_catalog(), read_curated_evidence(args.evidence)
    )
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
