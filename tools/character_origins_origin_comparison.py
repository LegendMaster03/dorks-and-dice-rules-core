"""Compare a prospective Heritage's candidates against a standard Origin.

A read-only Pillar 2 source/manifestation *coverage* test, not a balance score.
It neither grants target traits nor treats a related mechanical component as
an eligible parameter value or a complete equivalent trait.
"""
import argparse
import copy
import json

from character_origins_compound_traits import read_contracts
from character_origins_manifestation_inventory import characteristic_candidate_pool
from character_origins_parameter_inheritance import _value_key, read_blueprint
from character_origins_source_inventory import read_catalog


def _same_json(a, b):
    return _value_key(a) == _value_key(b)


def _related_trait_evidence(target, offer):
    """Only explicit shared fields; never infer missing effects from prose."""
    definition = target.get("authoredDefinition") or {}
    proposed = offer.get("authoredDefinition") or {}
    shared = {}
    for field in ("sense", "rule", "spell", "movementMode"):
        if field in definition and field in proposed and _same_json(
            definition[field], proposed[field]
        ):
            shared[field] = copy.deepcopy(definition[field])
    return shared


def compare_origin_target(contributors, target_source_id, catalog, contracts,
                          blueprints, registry=None):
    """Explain exact matches, distinct related traits, and missing options.

    The target is a *directly selectable* reference configuration. Source
    records remain authoritative for what they actually offer. An exact
    intrinsic match means identical JSON value for one dimension, not full
    character equivalence. A matching trait identity also needs its full
    parameter configuration; partial matches are not counted as exact.
    """
    registry = {} if registry is None else registry
    target = characteristic_candidate_pool([
        {"contributorId": "reference", "selectedSourceId": target_source_id}
    ], {}, blueprints, catalog, contracts)
    pool = characteristic_candidate_pool(
        contributors, registry, blueprints, catalog, contracts
    )
    comparisons = []
    for needed in target["offers"]:
        kind, identity = needed["kind"], needed["identity"]
        peer = [offer for offer in pool["offers"]
                if offer["kind"] == kind and offer["identity"] == identity]
        result = {
            "kind": kind,
            "identity": identity,
            "targetSourceOccurrenceId": needed["sourceOccurrenceId"],
        }
        if kind == "intrinsic":
            expected = needed["value"]
            exact = [offer for offer in peer
                     if _same_json(offer["value"], expected)]
            result["referenceValue"] = copy.deepcopy(expected)
            if exact:
                result["status"] = "exact-candidate"
                result["availableFrom"] = [
                    copy.deepcopy(offer["provenance"]) for offer in exact
                ]
            elif peer:
                result["status"] = "different-intrinsic-value"
                result["availableValues"] = [
                    {"value": copy.deepcopy(offer["value"]),
                     "source": copy.deepcopy(offer["provenance"])}
                    for offer in peer
                ]
            else:
                result["status"] = "not-offered"
        else:
            expected = needed.get("parameters", {})
            exact = [offer for offer in peer if _same_json(
                offer.get("parameters", {}), expected
            )]
            result["referenceParameters"] = copy.deepcopy(expected)
            if exact:
                result["status"] = "exact-source-trait-candidate"
                result["availableFrom"] = [
                    copy.deepcopy(offer["provenance"]) for offer in exact
                ]
            elif peer:
                # Same complete trait identity but a different configuration;
                # a later independent-parameter composition might make the
                # target eligible, but that requires separate validation.
                result["status"] = "same-trait-different-parameters"
                result["availableConfigurations"] = [
                    copy.deepcopy(offer.get("parameters", {}))
                    for offer in peer
                ]
            else:
                related = []
                for offer in pool["offers"]:
                    if offer["kind"] != "trait":
                        continue
                    shared = _related_trait_evidence(needed, offer)
                    if shared:
                        related.append({
                            "identity": offer["identity"],
                            "sharedFields": shared,
                            "authoredDefinition": copy.deepcopy(
                                offer.get("authoredDefinition")
                            ),
                            "provenance": copy.deepcopy(offer["provenance"]),
                        })
                if related:
                    result["status"] = "related-trait-not-equivalent"
                    result["relatedOffers"] = related
                    result["referenceDefinition"] = copy.deepcopy(
                        needed.get("authoredDefinition")
                    )
                else:
                    result["status"] = "not-offered"
        comparisons.append(result)

    return {
        "status": "origin-target-coverage-evidence-only",
        "targetSelectedSourceId": target_source_id,
        "targetCharacteristics": comparisons,
        "candidateReviewFlags": copy.deepcopy(pool["reviewFlags"]),
        "candidateDeferredSourceFields": copy.deepcopy(
            pool["deferredSourceFields"]
        ),
        "targetDeferredSourceFields": copy.deepcopy(
            target["deferredSourceFields"]
        ),
        "candidateCharacteristicIds": [
            {"kind": offer["kind"], "identity": offer["identity"]}
            for offer in pool["offers"]
        ],
        "balanceEvaluated": False,
        "manifestationAuthorized": False,
    }


def main():
    parser = argparse.ArgumentParser(
        description="Compare Origin candidate coverage without scoring power"
    )
    parser.add_argument("--target", required=True)
    parser.add_argument("--contributors", nargs="+", required=True)
    args = parser.parse_args()
    blueprint = read_blueprint()
    result = compare_origin_target([
        {"contributorId": f"source-{i + 1}", "selectedSourceId": source}
        for i, source in enumerate(args.contributors)
    ], args.target, read_catalog(), read_contracts(),
        {blueprint["definitionOccurrenceId"]: blueprint})
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
