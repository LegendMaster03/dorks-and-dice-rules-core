"""Source-neutral proposed characteristic selection reconciliation study.

This module takes a set of explicitly *proposed candidate IDs* from the
read-only characteristic identity projection. It reports pair relationships
that require policy decisions before reconstruction. A candidate is not a
grant, and a report with no detected relationships is NOT authorization.

Identity equality, repeated occurrences, overlapping effects, competing
intrinsics, and choice alternatives are distinguished. No relation here
automatically stacks, suppresses, upgrades, replaces or awards a feature.
The input candidate IDs are ephemeral preview identifiers, not persistent
character/grant identities.
"""
import argparse
import copy
import itertools
import json

from character_origins_characteristic_identity import (
    project_characteristic_identity_evidence, read_identity_mapping,
)
from character_origins_compound_traits import read_contracts
from character_origins_curated_identity_review import read_curated_evidence
from character_origins_identity_compatibility_audit import (
    COMPONENT_FIELDS, MOVEMENT_ROOT_FIELDS,
)
from character_origins_manifestation_inventory import characteristic_candidate_pool
from character_origins_parameter_inheritance import read_blueprints
from character_origins_source_inventory import read_catalog


class ReconciliationReviewError(ValueError):
    """Invalid proposal, untrusted projection, or missing candidate evidence."""


def _key(value):
    try:
        return json.dumps(value, sort_keys=True, separators=(",", ":"),
                          ensure_ascii=False, allow_nan=False)
    except (ValueError, TypeError) as exc:
        raise ReconciliationReviewError(
            "Proposed characteristic values must be valid JSON"
        ) from exc


def _qualified_local_trait(identity):
    if not isinstance(identity, str) or "#trait:" not in identity:
        return None
    return identity.rsplit("#trait:", 1)[1]


def _same_source_choice_occurrence(first, second):
    """Check authored choice alternatives rather than conflicting Species."""
    source_a, source_b = first["originalOffer"], second["originalOffer"]
    pa = first["contributorProvenance"]
    pb = second["contributorProvenance"]
    return (
        source_a.get("sourceOccurrenceId") is not None
        and source_a.get("sourceOccurrenceId")
        == source_b.get("sourceOccurrenceId")
        and pa.get("kind") == pb.get("kind") == "native-source"
        and pa.get("contributorId") == pb.get("contributorId")
    )


def _trait_relation(first, second):
    config_equal = _key(first["parameters"]) == _key(
        second["parameters"]
    )
    configuration = (
        "same-configuration" if config_equal else "different-configurations"
    )
    a, b = (
        first["sourceTraitOrFieldId"], second["sourceTraitOrFieldId"]
    )
    if a == b:
        return "repeated-source-trait-" + configuration, (
            "repeat-offer-not-repeat-grant"
        )
    definition_a = first.get("mechanicDefinitionId")
    definition_b = second.get("mechanicDefinitionId")
    if definition_a and definition_a == definition_b:
        return "explicit-identity-mapping-" + configuration, (
            "mapping-evidence-not-stacking-policy"
        )
    literal_a = first.get("literalAuthoredIdentityCandidateId")
    literal_b = second.get("literalAuthoredIdentityCandidateId")
    if literal_a and literal_a == literal_b:
        return "identical-authored-content-" + configuration, (
            "literal-evidence-not-semantic-or-grant-policy"
        )
    concept_a, concept_b = (
        first.get("traitConceptId"), second.get("traitConceptId")
    )
    if concept_a and concept_a == concept_b:
        return "related-trait-concept-distinct-definitions", (
            "conceptual-family-does-not-establish-upgrade"
        )

    raw_a = first["originalOffer"].get("authoredDefinition") or {}
    raw_b = second["originalOffer"].get("authoredDefinition") or {}
    common = [
        field for field in COMPONENT_FIELDS
        if field in raw_a and field in raw_b
        and _key(raw_a[field]) == _key(raw_b[field])
    ]
    if common:
        return "shared-component-possible-overlap", (
            "partial-effect-overlap-requires-review"
        )
    local_a, local_b = _qualified_local_trait(a), _qualified_local_trait(b)
    if local_a and local_a == local_b:
        return "same-local-trait-id-unknown-equivalence", (
            "source-names-are-not-definition-identities"
        )
    return None, None


def _intrinsic_and_trait(first, second):
    intrinsic = first if first["kind"] == "intrinsic" else second
    trait = first if first["kind"] == "trait" else second
    authored = trait["originalOffer"].get("authoredDefinition") or {}
    mode = authored.get("movementMode")
    dimension = MOVEMENT_ROOT_FIELDS.get(mode) if isinstance(
        mode, str
    ) else None
    if (
        dimension == intrinsic["intrinsicSlotId"]
        and "speedFeet" in authored
    ):
        return (
            "trait-and-intrinsic-movement-same-value"
            if _key(authored["speedFeet"]) == _key(intrinsic["value"])
            else "trait-and-intrinsic-movement-different-values",
            "possible-mirrored-property-not-a-second-grant",
        )
    return None, None


def review_proposed_manifestation(identity_report, selected_candidate_ids):
    """Identify potential interactions among selected *candidate evidence*.

    The output remains untrusted until a future authorized eligibility,
    manifestation and reconciliation policy validates the complete result.
    """
    if (not isinstance(identity_report, dict)
            or identity_report.get("status")
            != "characteristic-identity-evidence-only"
            or identity_report.get("authority") != "not-authorized"
            or not isinstance(identity_report.get("candidates"), list)
            or not isinstance(identity_report.get("reviewFlags"), list)
            or not isinstance(identity_report.get("deferredSourceFields"), list)):
        raise ReconciliationReviewError(
            "Expected a complete read-only characteristic identity projection"
        )
    by_id = {}
    for record in identity_report["candidates"]:
        if not isinstance(record, dict) or not isinstance(
            record.get("candidateId"), str
        ) or not record["candidateId"] or (
            record["candidateId"] in by_id
        ) or record.get("manifestation") != "not-selected" or (
            record.get("candidateEligibility") != "offered-not-authorized"
        ) or record.get("grantInstanceId") is not None:
            raise ReconciliationReviewError(
                "Projection contains malformed or already granted candidate evidence"
            )
        by_id[record["candidateId"]] = record
    if not isinstance(selected_candidate_ids, list) or any(
        not isinstance(cid, str) or cid not in by_id
        for cid in selected_candidate_ids
    ) or len(set(selected_candidate_ids)) != len(
        selected_candidate_ids
    ):
        raise ReconciliationReviewError(
            "Proposals must reference distinct offered candidate IDs"
        )
    proposals = [by_id[cid] for cid in selected_candidate_ids]
    relations = []
    for first, second in itertools.combinations(proposals, 2):
        a, b = first["kind"], second["kind"]
        if a == b == "intrinsic":
            if first["intrinsicSlotId"] != second["intrinsicSlotId"]:
                continue
            same_value = _key(first["value"]) == _key(second["value"])
            if same_value:
                relation, explanation = (
                    "same-intrinsic-slot-and-value",
                    "same-value-does-not-mean-additive-grants",
                )
            elif _same_source_choice_occurrence(first, second):
                relation, explanation = (
                    "alternatives-in-one-authored-intrinsic-choice",
                    "choose-one-option-under-applicable-policy",
                )
            else:
                relation, explanation = (
                    "competing-intrinsic-slot-values",
                    "requires-general-selection-or-replacement-policy",
                )
        elif a == b == "trait":
            relation, explanation = _trait_relation(first, second)
        else:
            relation, explanation = _intrinsic_and_trait(first, second)
        if relation is not None:
            relations.append({
                "firstCandidateId": first["candidateId"],
                "secondCandidateId": second["candidateId"],
                "relation": relation,
                "reasonForReview": explanation,
                "resolution": "unresolved-no-grants",
                "authoritativePolicyId": None,
            })

    return {
        "proposedCandidateIds": copy.deepcopy(selected_candidate_ids),
        "selectedCandidateEvidence": copy.deepcopy(proposals),
        "pairwiseRelationships": relations,
        "sourceReviewFlags": copy.deepcopy(identity_report["reviewFlags"]),
        "deferredSourceFields": copy.deepcopy(
            identity_report["deferredSourceFields"]
        ),
        # Diagnostics from all offered sources may describe candidates the
        # player did not propose to manifest. Preserve those diagnostics,
        # but do not incorrectly label them as conflicts in this selection.
        "reconciliationStatus": (
            "relationships-require-general-policy"
            if relations else "no-identified-relations-not-certified"
        ),
        "candidatePoolEvidenceStatus": (
            "contains-unresolved-source-evidence"
            if (identity_report["reviewFlags"]
                or identity_report["deferredSourceFields"])
            else "no-reported-source-evidence-gaps"
        ),
        "traitsGranted": False,
        "authorization": "not-authorized",
        "balance": "not-evaluated",
        "status": "draft-reconciliation-evidence-only",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Inspect explicitly proposed characteristic candidates"
    )
    parser.add_argument(
        "--contributors", nargs="+", required=True,
        metavar="SELECTABLE_SOURCE_ID"
    )
    parser.add_argument(
        "--select", nargs="+", required=True,
        metavar="PREVIEW_CANDIDATE_ID"
    )
    args = parser.parse_args()
    catalog = read_catalog()
    pool = characteristic_candidate_pool(
        [{"contributorId": f"source-{i + 1}",
          "selectedSourceId": sid}
         for i, sid in enumerate(args.contributors)],
        {}, read_blueprints(), catalog, read_contracts(),
    )
    identity = project_characteristic_identity_evidence(
        pool, read_identity_mapping(),
        curated=read_curated_evidence(), catalog=catalog,
    )
    result = review_proposed_manifestation(identity, args.select)
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
