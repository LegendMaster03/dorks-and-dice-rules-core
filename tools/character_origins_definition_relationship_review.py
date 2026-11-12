"""Read-only, source-neutral cross-definition relationship audit for Pillar 2.

This groups evidence that might justify future canonical mechanical identities
without assuming that matching local IDs, rule references, or identical JSON
fields prove semantic identity. It does not merge definitions, set grants,
authorize choices, or resolve inheritance or upgrade relationships.

The audit reviews native source *definitions*, not selected characters. Fields
are compared as complete typed JSON values, preserving original authored data
and explicit source occurrence provenance. Prose-only definitions and unresolved
symbolic bindings require further manual review.
"""
import argparse
import copy
import itertools
import json

from character_origins_source_inventory import read_catalog


META_FIELDS = frozenset(("id", "name", "description"))


class DefinitionReviewError(ValueError):
    """An invalid source definition prevents a reliable comparison."""


def _json_key(value):
    try:
        return json.dumps(value, sort_keys=True, separators=(",", ":"),
                          ensure_ascii=False, allow_nan=False)
    except (TypeError, ValueError) as exc:
        raise DefinitionReviewError(
            "Trait definitions must contain valid JSON"
        ) from exc


def _mechanics(trait):
    return {
        key: copy.deepcopy(value)
        for key, value in trait.items() if key not in META_FIELDS
    }


def _occurrences(catalog):
    """List authored trait occurrences without deriving equivalence by name."""
    if not isinstance(catalog, dict):
        raise DefinitionReviewError("Source catalog must be an object")
    result = []
    for sid in sorted(catalog):
        source = catalog[sid]
        if (not isinstance(source, dict) or source.get("id") != sid
                or not isinstance(source.get("traits", []), list)):
            raise DefinitionReviewError(f"Malformed source: {sid}")
        local_ids = set()
        for trait in source.get("traits", []):
            if not isinstance(trait, dict) or not isinstance(
                trait.get("id"), str
            ) or not trait["id"]:
                raise DefinitionReviewError(f"Malformed trait in {sid}")
            if trait["id"] in local_ids:
                raise DefinitionReviewError(
                    f"Duplicate local trait ID in {sid}: {trait['id']}"
                )
            local_ids.add(trait["id"])
            mechanical = _mechanics(trait)
            _json_key(mechanical)
            result.append({
                "sourceOccurrenceId": f"{sid}#trait:{trait['id']}",
                "sourceId": sid,
                "localTraitId": trait["id"],
                "authoredTrait": copy.deepcopy(trait),
                "mechanicalFields": mechanical,
                "ruleReference": (
                    trait["rule"] if isinstance(trait.get("rule"), str)
                    and trait["rule"] else None
                ),
            })
    return result


def _relation(first, second):
    """Compare evidence only; never infer a canonical definition or upgrade."""
    a, b = first["mechanicalFields"], second["mechanicalFields"]
    same_id = first["localTraitId"] == second["localTraitId"]
    same_rule = (
        first["ruleReference"] is not None
        and first["ruleReference"] == second["ruleReference"]
    )
    same_structure = bool(a) and bool(b) and _json_key(a) == _json_key(b)
    if not (same_id or same_rule or same_structure):
        return None

    keys = sorted(set(a) | set(b))
    common = [key for key in keys if key in a and key in b
              and _json_key(a[key]) == _json_key(b[key])]
    different = [key for key in keys if key not in a or key not in b
                 or _json_key(a[key]) != _json_key(b[key])]
    if not a or not b:
        classification = "insufficient-structured-evidence"
    elif same_structure:
        classification = "identical-structured-fields-review"
    elif len(a) < len(b) and all(
        key in b and _json_key(value) == _json_key(b[key])
        for key, value in a.items()
    ):
        classification = "first-structure-subset-of-second-review"
    elif len(b) < len(a) and all(
        key in a and _json_key(value) == _json_key(a[key])
        for key, value in b.items()
    ):
        classification = "second-structure-subset-of-first-review"
    else:
        classification = "different-structured-fields-review"

    reasons = []
    if same_id:
        reasons.append("same-local-trait-id")
    if same_rule:
        reasons.append("same-rule-reference")
    if same_structure:
        reasons.append("same-complete-structured-fields")
    prose_differs = (
        first["authoredTrait"].get("description") !=
        second["authoredTrait"].get("description")
    )
    return {
        "firstOccurrenceId": first["sourceOccurrenceId"],
        "secondOccurrenceId": second["sourceOccurrenceId"],
        "candidateReasons": reasons,
        "classification": classification,
        "matchingMechanicalFieldNames": common,
        "differingMechanicalFieldNames": different,
        "proseDescriptionsDiffer": prose_differs,
        "mechanicalEquivalence": "not-established",
        "relationshipAuthority": "requires-manual-review",
        "grantsOrInheritance": "not-evaluated",
    }


def review_definition_relationships(catalog):
    """Inventory all definitions, and review pairs with grounded similarities.

    Identical full structured fields are *review candidates* even when a
    local trait ID differs. A repeated name or common rule reference can
    also trigger review. Partial overlap ALONE is too broad to infer a
    related mechanic; reviewed subsets must share a name or rule reference.
    """
    traits = _occurrences(catalog)
    relationships = []
    counts = {}
    for first, second in itertools.combinations(traits, 2):
        candidate = _relation(first, second)
        if candidate is None:
            continue
        relationships.append(candidate)
        name = candidate["classification"]
        counts[name] = counts.get(name, 0) + 1

    return {
        "sourceTraitDefinitions": traits,
        "relationshipCandidates": relationships,
        "countsByClassification": counts,
        "traitDefinitionCount": len(traits),
        "relationshipCandidateCount": len(relationships),
        "semanticEquivalenceStatus": "not-evaluated",
        "status": "definition-relationship-evidence-only",
        "authority": "not-authorized",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Review matching/overlapping Species trait definitions."
    )
    parser.add_argument(
        "--local-id", metavar="TRAIT_ID",
        help="Show candidates involving a particular authored local ID.",
    )
    parser.add_argument(
        "--summary", action="store_true",
        help="Show counts without the full authored trait definitions.",
    )
    args = parser.parse_args()
    result = review_definition_relationships(read_catalog())
    if args.local_id:
        wanted = {
            item["sourceOccurrenceId"]
            for item in result["sourceTraitDefinitions"]
            if item["localTraitId"] == args.local_id
        }
        result = {
            **result,
            "sourceTraitDefinitions": [
                x for x in result["sourceTraitDefinitions"]
                if x["sourceOccurrenceId"] in wanted
            ],
            "relationshipCandidates": [
                x for x in result["relationshipCandidates"]
                if x["firstOccurrenceId"] in wanted
                or x["secondOccurrenceId"] in wanted
            ],
        }
        result["relationshipCandidateCount"] = len(
            result["relationshipCandidates"]
        )
    if args.summary:
        result.pop("sourceTraitDefinitions")
        result.pop("relationshipCandidates")
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
