"""Read-only coverage matrix for directly selectable Ancestries of one Species.

A Species' known shared foundation and Ancestry dimensions permit systematic
*review*, but this module does not invent compatibility or balance rules.
Never enumerate all cross-Species combinations as a default requirement.
Every pair is evaluated with the existing source-neutral candidate engine,
and source-intrinsic review flags are distinguished from *new* pair evidence.

The eventual compatibility contracts/exception policies are not implemented.
"""
import argparse
import copy
import itertools
import json

from character_origins_compound_traits import read_contracts
from character_origins_manifestation_inventory import characteristic_candidate_pool
from character_origins_parameter_inheritance import _value_key, read_blueprints
from character_origins_source_inventory import read_catalog, source_inventory
from character_origins_shared_foundations import audit_direct_foundations


def _source(key, source):
    return {"contributorId": key, "selectedSourceId": source}


def audit_same_species_pairs(catalog, contracts, blueprints):
    """Enumerate all current direct-source pairs within each Species only.

    Include two contributors of the *same* selected Ancestry to verify that
    repeated authorship can be represented without granting a duplicate.
    Do not convert a lack of observed conflicts into a compatibility verdict.
    """
    grouped = {}
    for sid in sorted(catalog):
        if catalog[sid].get("selectable") is not True:
            continue
        inv = source_inventory(sid, catalog)
        root = inv["lineage"][0]["sourceId"]
        grouped.setdefault(root, []).append(sid)

    baseline_flags = {}
    for sid in sorted(catalog):
        if catalog[sid].get("selectable") is not True:
            continue
        baseline = characteristic_candidate_pool(
            [_source("single", sid)], {}, blueprints, catalog, contracts
        )
        baseline_flags[sid] = {
            _value_key(flag) for flag in baseline["reviewFlags"]
        }

    species = []
    for root, leaves in grouped.items():
        pairs = []
        for first, second in itertools.combinations_with_replacement(
            leaves, 2
        ):
            combined = audit_direct_foundations(
                [_source("a", first), _source("b", second)],
                catalog, contracts, blueprints
            )
            baseline = baseline_flags[first] | baseline_flags[second]
            newly_visible = [
                copy.deepcopy(flag) for flag in combined["reviewFlags"]
                if _value_key(flag) not in baseline
            ]
            pairs.append({
                "firstSourceId": first,
                "secondSourceId": second,
                "sameSelectedSource": first == second,
                "sharedSourceNodeIds": copy.deepcopy(
                    combined["sharedSourceNodeIds"]
                ),
                "sourceReviewFlags": copy.deepcopy(
                    combined["reviewFlags"]
                ),
                "newPairReviewFlags": newly_visible,
                "deferredEvidenceCount": len(
                    combined["deferredSourceFields"]
                ),
                "defaultCompatibility": "not-evaluated",
                "balance": "not-evaluated",
            })
        species.append({
            "rootSpeciesSourceId": root,
            "selectableSourceIds": copy.deepcopy(leaves),
            "sameSpeciesPairCount": len(pairs),
            "pairs": pairs,
            "compatibilityPolicy": "not-yet-authored",
        })

    return {
        "species": species,
        "sameSpeciesPairCount": sum(
            record["sameSpeciesPairCount"] for record in species
        ),
        "crossSpeciesPairsEnumerated": 0,
        "status": "same-species-compatibility-coverage-evidence-only",
        "authority": "not-authorized",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Audit source-known same-Species pairs without deciding compatibility"
    )
    parser.add_argument(
        "--species", metavar="SPECIES_SOURCE_ID",
        help="Limit the displayed coverage matrix to one root Species.",
    )
    args = parser.parse_args()
    report = audit_same_species_pairs(
        read_catalog(), read_contracts(), read_blueprints()
    )
    if args.species:
        selected = [
            record for record in report["species"]
            if record["rootSpeciesSourceId"] == args.species
        ]
        if not selected:
            parser.error("Unknown or unselectable Species root")
        report = {
            **report, "species": selected,
            "sameSpeciesPairCount": selected[0]["sameSpeciesPairCount"],
        }
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
