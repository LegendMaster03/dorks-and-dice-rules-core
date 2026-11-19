"""Compare an Origin's candidate sources against a standard target preset.

This is a Pillar 2 *coverage audit*: exact source-qualified traits and
intrinsic values are distinguished from similar-but-different effects.
It does not award traits, assign costs, resolve conflicts, infer biological
inheritance, or change the target. Upbringing is an independent selection.
"""
import argparse
import copy
import json

from character_origins_compound_traits import read_contracts
from character_origins_manifestation_inventory import (
    characteristic_candidate_pool,
)
from character_origins_parameter_inheritance import _value_key, read_blueprint
from character_origins_source_inventory import read_catalog


def _same(a, b):
    return _value_key(a) == _value_key(b)


def _related_traits(target, offers):
    """Find evidence for related mechanics; never call them equivalent."""
    definition = target.get("authoredDefinition") or {}
    related = []
    for offer in offers:
        if offer["kind"] != "trait" or (
            offer["identity"] == target["identity"]
        ):
            continue
        other = offer.get("authoredDefinition") or {}
        # These references are evidence of a shared *component*, not
        # proof that the complete features are interchangeable.
        shared = {
            key: copy.deepcopy(definition[key])
            for key in ("sense", "spell", "rule")
            if key in definition and key in other
            and _same(definition[key], other[key])
        }
        if shared:
            related.append({
                "candidateOccurrenceId": offer["identity"],
                "sharedComponents": shared,
                "candidateDefinition": copy.deepcopy(other),
                "source": copy.deepcopy(offer["provenance"]),
                "relationship": "related-not-equivalent",
            })
    return related


def compare_standard_target(target_source_id, contributors, catalog,
                            contracts, blueprints, registry=None):
    """Report exact candidate coverage without suggesting any auto-grants."""
    registry = {} if registry is None else registry
    target = characteristic_candidate_pool(
        [{"contributorId": "target-reference",
          "selectedSourceId": target_source_id}],
        {}, blueprints, catalog, contracts,
    )
    candidate = characteristic_candidate_pool(
        contributors, registry, blueprints, catalog, contracts,
    )

    target_offers = target["offers"]
    offers = candidate["offers"]
    rows = []
    for required in target_offers:
        if required["kind"] == "intrinsic":
            matches = [
                item for item in offers
                if item["kind"] == "intrinsic"
                and item["identity"] == required["identity"]
                and _same(item["value"], required["value"])
            ]
            alternatives = [
                item for item in offers
                if item["kind"] == "intrinsic"
                and item["identity"] == required["identity"]
                and not _same(item["value"], required["value"])
            ]
            related = []
        else:
            matches = [
                item for item in offers
                if item["kind"] == "trait"
                and item["identity"] == required["identity"]
                and _same(item["parameters"], required["parameters"])
            ]
            alternatives = [
                item for item in offers
                if item["kind"] == "trait"
                and item["identity"] == required["identity"]
                and not _same(item["parameters"], required["parameters"])
            ]
            related = _related_traits(required, offers) if not matches else []

        rows.append({
            "kind": required["kind"],
            "identity": required["identity"],
            "targetValue": copy.deepcopy(
                required["value"] if required["kind"] == "intrinsic"
                else required["parameters"]
            ),
            "sourceOccurrenceId": required["sourceOccurrenceId"],
            "coverage": (
                "exact-candidate" if matches
                else "related-but-not-equivalent" if related
                else "different-configuration" if alternatives
                else "not-offered"
            ),
            "exactCandidateProvenance": [
                copy.deepcopy(item["provenance"]) for item in matches
            ],
            "alternativeValues": [
                copy.deepcopy(
                    item["value"] if item["kind"] == "intrinsic"
                    else item["parameters"]
                ) for item in alternatives
            ],
            "relatedTraitEvidence": related,
        })

    target_source = catalog[target_source_id]
    exact = sum(x["coverage"] == "exact-candidate" for x in rows)
    target_keys = {
        (x["kind"], x["identity"],
         _value_key(x["value"] if x["kind"] == "intrinsic"
                    else x["parameters"])) for x in target_offers
    }
    extras = [
        {
            "kind": item["kind"],
            "identity": item["identity"],
            "value": copy.deepcopy(
                item["value"] if item["kind"] == "intrinsic"
                else item["parameters"]
            ),
            "provenance": copy.deepcopy(item["provenance"]),
        }
        for item in offers
        if (item["kind"], item["identity"],
            _value_key(item["value"] if item["kind"] == "intrinsic"
                       else item["parameters"])) not in target_keys
    ]
    return {
        "target": target_source_id,
        "contributors": copy.deepcopy(contributors),
        "totalModeledTargetCharacteristics": len(rows),
        "exactCandidateMatches": exact,
        "comparison": rows,
        "candidateOnlyExtras": extras,
        "candidateReviewFlags": copy.deepcopy(candidate["reviewFlags"]),
        "targetDeferredEvidence": copy.deepcopy(
            target["deferredSourceFields"]
        ),
        "candidateDeferredEvidence": copy.deepcopy(
            candidate["deferredSourceFields"]
        ),
        "suggestedUpbringing": target_source.get("defaultUpbringing"),
        "upbringingStatus": "separate-cultural-choice-not-inherited",
        "canExactlyReconstructAllModeledTargetCharacteristics": (
            exact == len(rows) and not target["deferredSourceFields"]
        ),
        "status": "origin-reconstruction-candidate-evidence-only",
        "limitation": (
            "Matching candidate source facts are not manifested grants. "
            "This count excludes deferred/unmodeled target fields, "
            "conflict adjudication, trait budgets and cultural Upbringing."
        ),
    }


def main():
    parser = argparse.ArgumentParser(
        description="Compare Origin source candidates against a standard preset"
    )
    parser.add_argument("--target", required=True)
    parser.add_argument("--contributors", nargs="+", required=True)
    args = parser.parse_args()
    blueprint = read_blueprint()
    report = compare_standard_target(
        args.target,
        [
            {"contributorId": f"contributor-{i+1}",
             "selectedSourceId": source}
            for i, source in enumerate(args.contributors)
        ],
        read_catalog(), read_contracts(),
        {blueprint["definitionOccurrenceId"]: blueprint},
    )
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
