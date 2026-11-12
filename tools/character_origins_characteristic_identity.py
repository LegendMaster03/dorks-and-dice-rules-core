"""Read-only characteristic identity and provenance evidence for Pillar 2.

A mechanical dimension, a conceptual trait family, an exact trait definition,
an authored occurrence, a contributor's offer, and a character's manifested
grant are different identities. This study makes the first five inspectable,
but deliberately NEVER creates the sixth.

Exact trait-definition relationships require an explicit mapping; neither
matching local IDs nor identical rule references prove equivalence. Source
offers are not automatically selectable, earned, authorized, or balanced.
"""
import argparse
import copy
import json
from pathlib import Path

from character_origins_compound_traits import read_contracts
from character_origins_manifestation_inventory import characteristic_candidate_pool
from character_origins_parameter_inheritance import read_blueprints
from character_origins_source_inventory import read_catalog


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MAPPING = (
    ROOT / "docs/rewrite/character-origins/"
    "pillar2-synthetic-characteristic-identity-mapping-20261111.json"
)


class IdentityEvidenceError(ValueError):
    """Malformed proposed identity evidence or unsupported offer shape."""


def _nonblank(value):
    return isinstance(value, str) and bool(value.strip())


def validate_identity_mapping(mapping):
    """Validate a wholly explicit, provisional cross-source identity map."""
    if not isinstance(mapping, dict) or set(mapping) != {"status", "traits"}:
        raise IdentityEvidenceError("Identity mapping envelope is invalid")
    if mapping["status"] != "synthetic-design-evidence-only":
        raise IdentityEvidenceError("Identity map must be marked synthetic")
    if not isinstance(mapping["traits"], list):
        raise IdentityEvidenceError("Trait identity map must be a list")
    entries = {}
    concepts_by_definition = {}
    for entry in mapping["traits"]:
        if not isinstance(entry, dict) or set(entry) != {
            "sourceTraitId", "conceptId", "definitionId"
        }:
            raise IdentityEvidenceError("Invalid identity-map entry")
        if not all(_nonblank(entry[field]) for field in entry):
            raise IdentityEvidenceError("Identity-map identifiers must be nonempty")
        source_id = entry["sourceTraitId"]
        if source_id in entries:
            raise IdentityEvidenceError(
                f"Duplicate mapped source trait: {source_id}"
            )
        definition_id = entry["definitionId"]
        concept_id = entry["conceptId"]
        if (definition_id in concepts_by_definition
                and concepts_by_definition[definition_id] != concept_id):
            raise IdentityEvidenceError(
                "One definition can not belong to conflicting concepts"
            )
        concepts_by_definition[definition_id] = concept_id
        entries[source_id] = {
            "conceptId": concept_id,
            "definitionId": definition_id,
        }
    return entries


def read_identity_mapping(path=DEFAULT_MAPPING):
    result = json.loads(Path(path).read_text(encoding="utf-8"))
    validate_identity_mapping(result)
    return result


def _source_identity(offer):
    """A nested profile retains its originating trait identity even if it
    no longer has the original source occurrence at the immediate contributor.
    """
    source = offer.get("sourceOccurrenceId")
    if source is not None and not _nonblank(source):
        raise IdentityEvidenceError("Invalid source occurrence identity")
    return source if source is not None else offer["identity"]


def _check_offer(offer):
    if not isinstance(offer, dict) or offer.get("kind") not in (
        "intrinsic", "trait"
    ) or not _nonblank(offer.get("identity")):
        raise IdentityEvidenceError("Unsupported characteristic offer")
    if not isinstance(offer.get("provenance"), dict):
        raise IdentityEvidenceError("Offer requires distinct provenance")
    provenance = offer["provenance"]
    if not _nonblank(provenance.get("contributorId")) or (
        provenance.get("kind") not in (
            "native-source", "manifested-profile", "custom-proposal"
        )
    ):
        raise IdentityEvidenceError("Invalid contributor occurrence provenance")
    if provenance["kind"] == "native-source":
        if (not _nonblank(provenance.get("selectedSourceId"))
                or not _nonblank(provenance.get("sourceOccurrenceId"))):
            raise IdentityEvidenceError("Native offer lacks source evidence")
        if provenance["sourceOccurrenceId"] != offer.get("sourceOccurrenceId"):
            raise IdentityEvidenceError("Mismatched authored source occurrence")
    elif provenance["kind"] == "manifested-profile":
        if not _nonblank(provenance.get("profileId")):
            raise IdentityEvidenceError("Nested offer lacks profile evidence")
    elif not _nonblank(provenance.get("proposedAuthorityRef")):
        raise IdentityEvidenceError(
            "Custom offer needs an explicit *proposed* policy source"
        )
    field = "value" if offer["kind"] == "intrinsic" else "parameters"
    if field not in offer or (
        offer["kind"] == "trait" and not isinstance(offer[field], dict)
    ):
        raise IdentityEvidenceError("Offer lacks its complete value/parameters")
    # Ensure no NaN, infinity, or non-JSON values can leak into a projection.
    try:
        json.dumps(offer[field], allow_nan=False)
    except (ValueError, TypeError) as exc:
        raise IdentityEvidenceError("Offer values must be valid JSON") from exc


def project_characteristic_identity_evidence(candidate_pool, mapping):
    """Annotate candidates without combining, validating, or granting them.

    Candidate IDs are ephemeral positions in this read-only projection,
    NOT stable persisted grant IDs. The original provenance is kept intact.
    """
    by_source = validate_identity_mapping(mapping)
    if (not isinstance(candidate_pool, dict)
            or candidate_pool.get("status")
            != "origin-characteristic-candidates-only"
            or not isinstance(candidate_pool.get("offers"), list)
            or not isinstance(candidate_pool.get("reviewFlags"), list)
            or not isinstance(candidate_pool.get("deferredSourceFields"), list)):
        raise IdentityEvidenceError("Expected a candidate-only pool")

    observations = []
    definition_groups = {}
    unmapped = []
    for index, offer in enumerate(candidate_pool["offers"]):
        _check_offer(offer)
        kind = offer["kind"]
        evidence_id = _source_identity(offer)
        candidate_id = f"evidence-{index + 1}"
        item = {
            "candidateId": candidate_id,
            "kind": kind,
            "sourceTraitOrFieldId": evidence_id,
            "contributorProvenance": copy.deepcopy(offer["provenance"]),
            "candidateEligibility": "offered-not-authorized",
            "manifestation": "not-selected",
            "grantInstanceId": None,
        }
        if kind == "intrinsic":
            # Intrinsic slots such as creature.type or movement.walk.speed
            # may have several competing VALUES and never auto-stack.
            item.update({
                "intrinsicSlotId": offer["identity"],
                "traitConceptId": None,
                "mechanicDefinitionId": None,
                "identityStatus": "known-intrinsic-dimension",
                "value": copy.deepcopy(offer["value"]),
            })
        else:
            # Source-qualified identity is *not* a universal definition ID.
            # A mapping may be absent even for a well-formed, real ability.
            related = by_source.get(offer["identity"])
            item.update({
                "intrinsicSlotId": None,
                "traitConceptId": (
                    related["conceptId"] if related else None
                ),
                "mechanicDefinitionId": (
                    related["definitionId"] if related else None
                ),
                "identityStatus": (
                    "explicit-synthetic-mapping" if related
                    else "cross-source-identity-unresolved"
                ),
                "parameters": copy.deepcopy(offer["parameters"]),
                "reviewRequired": copy.deepcopy(
                    offer.get("reviewRequired", [])
                ),
            })
            if related:
                definition_groups.setdefault(
                    related["definitionId"], []
                ).append(candidate_id)
            elif offer["identity"] not in unmapped:
                unmapped.append(offer["identity"])
        observations.append(item)

    return {
        "candidates": observations,
        "explicitDefinitionGroups": [
            {"mechanicDefinitionId": definition,
             "candidateIds": copy.deepcopy(occurrences),
             "effect": "identity-evidence-only-no-stacking-or-grants"}
            for definition, occurrences in definition_groups.items()
        ],
        "unmappedTraitEvidenceIds": unmapped,
        "deferredSourceFields": copy.deepcopy(
            candidate_pool["deferredSourceFields"]
        ),
        "reviewFlags": copy.deepcopy(candidate_pool["reviewFlags"]),
        "status": "characteristic-identity-evidence-only",
        "authority": "not-authorized",
        "balance": "not-evaluated",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Inspect source-qualified vs. explicitly mapped mechanics"
    )
    parser.add_argument(
        "--contributors", required=True, nargs="+",
        metavar="SELECTABLE_SOURCE_ID",
    )
    parser.add_argument("--mapping", type=Path, default=DEFAULT_MAPPING)
    args = parser.parse_args()
    pool = characteristic_candidate_pool(
        [{"contributorId": f"source-{n + 1}",
          "selectedSourceId": sid}
         for n, sid in enumerate(args.contributors)],
        {}, read_blueprints(), read_catalog(), read_contracts()
    )
    result = project_characteristic_identity_evidence(
        pool, read_identity_mapping(args.mapping)
    )
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
