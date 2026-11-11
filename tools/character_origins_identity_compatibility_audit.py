"""Pillar 2 read-only evidence for characteristic identity and compatibility.

Source occurrence ID, mechanical identity, and parameter configuration are
different concepts. This report locates shared references, overlapping
capabilities, and competing values, without merging traits, stacking effects,
or deciding ancestry replacement/selection rules.

No combat or other mechanics are executed. All relationships remain
design-review findings, not approved grants or rules.
"""
import argparse
import copy
import json
from collections import defaultdict

from character_origins_compound_traits import read_contracts
from character_origins_manifestation_inventory import characteristic_candidate_pool
from character_origins_parameter_inheritance import _value_key, read_blueprint
from character_origins_source_inventory import read_catalog


# Only these fields supply substantive *component* evidence. Generic fields
# such as level, uses, or activation are not evidence of mechanic equivalence.
COMPONENT_FIELDS = (
    "effectiveSizeCategoriesHigherForCarryingCapacity",
    "effectiveSizeCategoriesHigherForPushingDraggingLifting",
    "advantageOnAbilityChecksToEndCondition",
    "rule",
    "skillBonuses",
    "canBreathe",
)
MOVEMENT_ROOT_FIELDS = {
    "climb": "movement.climb.speed",
    "swim": "movement.swim.speed",
    "burrow": "movement.burrow.speed",
}


def _distinct_occurrences(entries):
    return {entry["identity"] for entry in entries}


def _stable_value_groups(entries, field):
    groups = defaultdict(list)
    for entry in entries:
        if field in entry:
            groups[_value_key(entry[field])].append(entry)
    return list(groups.values())


def _relationship(kind, entries, **details):
    return {
        "kind": kind,
        "evidenceOnly": True,
        "sourceOccurrences": sorted(_distinct_occurrences(entries)),
        "contributorOccurrences": sorted({
            e["provenance"]["contributorId"] for e in entries
        }),
        **details,
    }


def audit_characteristic_identity(contributors, catalog, contracts, blueprints,
                                  registry=None):
    """Review candidate relationships from the common Pillar 2 source pool.

    Direct and nested Origin contributors use the same candidate inventory.
    A nested profile must first be consistent with its recorded sources.
    """
    pool = characteristic_candidate_pool(
        contributors, {} if registry is None else registry, blueprints,
        catalog, contracts,
    )
    offers = pool["offers"]
    intrinsics = [x for x in offers if x["kind"] == "intrinsic"]
    traits = [x for x in offers if x["kind"] == "trait"]
    findings = []

    by_dimension = defaultdict(list)
    for item in intrinsics:
        by_dimension[item["identity"]].append(item)

    for dimension, rows in sorted(by_dimension.items()):
        value_groups = _stable_value_groups(rows, "value")
        if len(rows) > 1 and len(value_groups) == 1:
            findings.append(_relationship(
                "same-intrinsic-value-from-multiple-occurrences", rows,
                characteristicDimension=dimension,
                authoredValue=copy.deepcopy(rows[0]["value"]),
                disposition="same-value-does-not-mean-additive-grants",
            ))
        elif len(value_groups) > 1:
            # A single native sizeChoice explicitly gives alternatives.
            # Their existence alone is not a contradiction.
            if all(row.get("authoredField") == "sizeChoice"
                   and row.get("sourceOccurrenceId") ==
                   rows[0].get("sourceOccurrenceId")
                   for row in rows):
                findings.append(_relationship(
                    "authored-intrinsic-choice-alternatives", rows,
                    characteristicDimension=dimension,
                    offeredValues=[copy.deepcopy(group[0]["value"])
                                   for group in value_groups],
                    disposition="select-one-option-not-a-source-conflict",
                ))
            else:
                findings.append(_relationship(
                    "competing-intrinsic-values", rows,
                    characteristicDimension=dimension,
                    offeredValues=[copy.deepcopy(group[0]["value"])
                                   for group in value_groups],
                    disposition="requires-overlap-replacement-or-selection-review",
                ))

    by_occurrence = defaultdict(list)
    for item in traits:
        by_occurrence[item["identity"]].append(item)
    for occurrence, rows in sorted(by_occurrence.items()):
        if len(rows) < 2:
            continue
        # One mechanic definition can be contributed with *different*
        # parameter configurations (Red and Bronze Breath Weapon). This is
        # not an accidentally duplicated full character grant.
        configurations = _stable_value_groups(rows, "parameters")
        findings.append(_relationship(
            "repeated-source-trait-offer", rows,
            occurrenceId=occurrence,
            configurationRelationship=(
                "same-parameters" if len(configurations) == 1
                else "different-parameter-configurations"
            ),
            disposition="multiple-candidate-sources-not-multiple-grants",
        ))

    # Source-qualified trait occurrences are never collapsed by local ID,
    # shared spell reference, or overlap of a single mechanical field.
    for i, first in enumerate(traits):
        a = first.get("authoredDefinition") or {}
        for second in traits[i + 1:]:
            if first["identity"] == second["identity"]:
                continue
            b = second.get("authoredDefinition") or {}
            if not a or not b:
                continue
            local_a = a.get("id")
            local_b = b.get("id")
            if local_a and local_a == local_b:
                findings.append(_relationship(
                    "same-local-trait-id-distinct-source-occurrences",
                    [first, second],
                    localTraitId=local_a,
                    definitionRelationship=(
                        "identical-authored-definition"
                        if _value_key(a) == _value_key(b)
                        else "different-authored-definitions"
                    ),
                    disposition="do-not-equate-by-local-id",
                ))
            if a.get("rule") and a.get("rule") == b.get("rule"):
                findings.append(_relationship(
                    "shared-explicit-rule-reference", [first, second],
                    ruleRef=a["rule"],
                    disposition="candidate-shared-behavior-not-automatic-merge",
                ))
            if a.get("spell") and a.get("spell") == b.get("spell"):
                findings.append(_relationship(
                    "same-spell-distinct-trait-grants", [first, second],
                    spellRef=a["spell"],
                    definitionRelationship=(
                        "identical-authored-definition"
                        if _value_key(a) == _value_key(b)
                        else "different-authored-definitions"
                    ),
                    disposition="spell-identity-does-not-equate-entitlements",
                ))
            common = [
                field for field in COMPONENT_FIELDS
                if field in a and field in b
                and _value_key(a[field]) == _value_key(b[field])
            ]
            if common:
                findings.append(_relationship(
                    "shared-mechanical-components", [first, second],
                    sharedFields=common,
                    disposition="partial-overlap-not-whole-trait-equivalence",
                ))

    # A source record can redundantly express a movement mode at the
    # top level and inside an authored trait. One value is not twice the
    # speed. An unequal pair needs an explicit content review.
    for item in traits:
        definition = item.get("authoredDefinition") or {}
        mode = definition.get("movementMode")
        if mode not in MOVEMENT_ROOT_FIELDS or "speedFeet" not in definition:
            continue
        dimension = MOVEMENT_ROOT_FIELDS[mode]
        for intrinsic in by_dimension.get(dimension, []):
            if (intrinsic.get("sourceId") is None
                or intrinsic.get("sourceId") != item.get("sourceId")):
                continue
            same = _value_key(definition["speedFeet"]) == _value_key(
                intrinsic["value"]
            )
            findings.append(_relationship(
                "trait-and-intrinsic-movement-overlap",
                [item, intrinsic],
                characteristicDimension=dimension,
                traitSpeed=copy.deepcopy(definition["speedFeet"]),
                intrinsicSpeed=copy.deepcopy(intrinsic["value"]),
                matching=same,
                disposition=(
                    "same-source-mirror-not-double-grant" if same
                    else "conflicting-authored-values-require-review"
                ),
            ))

    lowlight = [
        item for item in traits
        if (item.get("authoredDefinition") or {}).get("rule")
        == "rule.low-light-vision"
    ]
    darkvision = by_dimension.get("sense.darkvision.range", [])
    if lowlight and darkvision:
        findings.append(_relationship(
            "low-light-and-darkvision-relationship", lowlight + darkvision,
            disposition="coexist-replace-or-interact-not-decided",
        ))

    return {
        "status": "characteristic-identity-evidence-only",
        "traitOccurrences": [
            {"identity": row["identity"],
             "sourceOccurrenceId": row.get("sourceOccurrenceId"),
             "contributorId": row["provenance"]["contributorId"],
             "explicitRuleRef": (row.get("authoredDefinition") or {}).get("rule"),
             "parameterConfiguration": copy.deepcopy(row.get("parameters", {}))}
            for row in traits
        ],
        "findings": findings,
        "unresolvedSourceEvidence": copy.deepcopy(pool["deferredSourceFields"]),
        "existingCandidateReviewFlags": copy.deepcopy(pool["reviewFlags"]),
        "effectsEvaluated": False,
        "duplicatesMerged": False,
    }


def main():
    parser = argparse.ArgumentParser(
        description="Audit Origin trait identities without resolving conflicts"
    )
    parser.add_argument("--selected", nargs="+", required=True)
    args = parser.parse_args()
    blueprint = read_blueprint()
    data = audit_characteristic_identity([
        {"contributorId": f"contributor-{i+1}", "selectedSourceId": sid}
        for i, sid in enumerate(args.selected)
    ], read_catalog(), read_contracts(),
        {blueprint["definitionOccurrenceId"]: blueprint})
    print(json.dumps(data, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
