"""Read-only shared-Species foundation and Ancestry evidence audit.

The source-neutral Origin candidate pool remains authoritative for eligibility
*evidence*. This projection groups equal source nodes and equal configured
offers for review without granting them, stacking them, selecting an origin,
or assuming that distinct Species are incompatible or comparably balanced.

This first study accepts direct standard source contributors. Nested/created
Heritage contributors must use the separate verified manifestation pipeline;
classifying those as ordinary source roots would misrepresent their history
as a manifested mechanical foundation.
"""
import argparse
import copy
import json

from character_origins_compound_traits import read_contracts
from character_origins_manifestation_inventory import (
    characteristic_candidate_pool,
)
from character_origins_parameter_inheritance import (
    ParameterInheritanceError, _value_key, read_blueprints,
)
from character_origins_source_inventory import (
    read_catalog, source_inventory,
)


def audit_direct_foundations(contributors, catalog, contracts, blueprints):
    """Group shared source definitions; preserve distinct contributor evidence.

    A source node is represented once in the node index no matter how many
    contributors reference it. The same complete configured trait is one
    grouped candidate with multiple provenance records, not multiple grants.
    Different parameter configurations remain separate candidates.

    This is NOT a default foundation merger, compatibility verdict, origin
    trait allocation, or mixed-Heritage character.
    """
    if not isinstance(contributors, list) or not contributors:
        raise ParameterInheritanceError(
            "At least one source contributor is required"
        )
    for contributor in contributors:
        if not isinstance(contributor, dict) or (
            set(contributor) != {"contributorId", "selectedSourceId"}
        ):
            raise ParameterInheritanceError(
                "Shared-foundation audit accepts only direct source contributors; "
                "nested profiles require verified manifestation evidence"
            )

    # The shared candidate evaluator handles contributor ID validity,
    # selectable sources, all authored fields and explicit bindings.
    pool = characteristic_candidate_pool(
        contributors, {}, blueprints, catalog, contracts
    )
    source_nodes = {}
    source_lineages = []
    roots = {}
    for contributor in contributors:
        cid, selected = (
            contributor["contributorId"], contributor["selectedSourceId"]
        )
        inventory = source_inventory(selected, catalog)
        lineage = inventory["lineage"]
        root = lineage[0]["sourceId"]
        roots.setdefault(root, []).append(cid)
        source_lineages.append({
            "contributorId": cid,
            "selectedSourceId": selected,
            "rootSpeciesSourceId": root,
            "sourceLineage": [node["sourceId"] for node in lineage],
        })
        for node in lineage:
            sid = node["sourceId"]
            if sid not in source_nodes:
                source_nodes[sid] = {
                    "sourceId": sid,
                    "sourceKind": node["sourceKind"],
                    "intrinsicFields": copy.deepcopy(node["intrinsicFields"]),
                    "traitOccurrenceIds": [
                        t["occurrenceId"] for t in node["traits"]
                    ],
                    "contributedBy": [],
                }
            source_nodes[sid]["contributedBy"].append({
                "contributorId": cid,
                "selectedSourceId": selected,
            })

    # Group only offers that refer to the same source-qualified characteristic
    # AND the same complete candidate value/configuration. Do not merge two
    # distinct mechanics merely because their names or values happen to match.
    offers_by_key = {}
    for offer in pool["offers"]:
        value_key = (
            offer["value"] if offer["kind"] == "intrinsic"
            else offer["parameters"]
        )
        key = (
            offer["kind"], offer["identity"],
            offer["sourceOccurrenceId"], _value_key(value_key),
        )
        if key not in offers_by_key:
            record = {
                key: copy.deepcopy(value)
                for key, value in offer.items() if key != "provenance"
            }
            record["contributedBy"] = []
            offers_by_key[key] = record
        offers_by_key[key]["contributedBy"].append(
            copy.deepcopy(offer["provenance"])
        )

    is_same_species = len(roots) == 1
    shared_node_ids = [
        sid for sid, node in source_nodes.items()
        if len(node["contributedBy"]) > 1
    ]
    return {
        "contributorLineages": source_lineages,
        "speciesFoundations": [
            {"rootSpeciesSourceId": root, "contributorIds": copy.deepcopy(cids)}
            for root, cids in roots.items()
        ],
        "sourceNodes": list(source_nodes.values()),
        "sharedSourceNodeIds": shared_node_ids,
        "sharedSpeciesFoundationSourceId": (
            next(iter(roots)) if is_same_species else None
        ),
        "compositionEvidence": (
            "same-species-shared-foundation"
            if is_same_species
            else "distinct-species-foundations-require-review"
        ),
        "distinctConfiguredOffers": list(offers_by_key.values()),
        "deferredSourceFields": copy.deepcopy(
            pool["deferredSourceFields"]
        ),
        "reviewFlags": copy.deepcopy(pool["reviewFlags"]),
        "foundationGrantStatus": "no-characteristics-granted",
        "compatibilityStatus": "not-evaluated",
        "balanceStatus": "not-evaluated",
        "authority": "design-evidence-only",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Inspect shared Species foundation and distinct Ancestry evidence"
    )
    parser.add_argument(
        "--contributors", nargs="+", required=True,
        metavar="SELECTABLE_SOURCE_ID",
    )
    args = parser.parse_args()
    contributors = [
        {"contributorId": f"contributor-{index + 1}",
         "selectedSourceId": source}
        for index, source in enumerate(args.contributors)
    ]
    print(json.dumps(audit_direct_foundations(
        contributors, read_catalog(), read_contracts(), read_blueprints(),
    ), indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
