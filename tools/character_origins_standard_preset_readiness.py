"""Pillar 2 read-only readiness audit for direct standard Origin presets.

Build a proposed set of existing characteristic selections, not an authorized
character or an executable native ruleset. A source-authored trait entitlement
can be proposed even when its internal choice has not yet been made. Competing
intrinsics, explicit choices and unresolved symbolic bindings remain pending.

No general leaf-overrides-parent policy, grant budget, or homebrew denial is
introduced. The output uses the SAME proposed selection shape as the
multi-characteristic Origin-profile study, without making selections for the
player or DM.
"""
import argparse
import copy
import json
from collections import defaultdict

from character_origins_compound_traits import read_contracts
from character_origins_manifestation_inventory import (
    characteristic_candidate_pool,
)
from character_origins_parameter_inheritance import _value_key, read_blueprints
from character_origins_source_inventory import (
    read_catalog, source_lineage,
)


def _choice_observations(trait):
    """Report concrete authoring choices, without inventing an answer.

    This is a conservative first pass. Unrecognized choice schemes are
    retained in the underlying native source and need their own review.
    """
    observations = []
    choice_keys = ("chooseFrom", "choiceCount", "featCategory")
    if any(key in trait for key in choice_keys):
        observations.append({
            "kind": "trait-entitlement-choice",
            "authoredFields": {
                key: copy.deepcopy(trait[key]) for key in choice_keys
                if key in trait
            },
        })
    if "spellList" in trait and "spell" not in trait:
        observations.append({
            "kind": "spell-from-list-choice",
            "authoredFields": {
                key: copy.deepcopy(trait[key])
                for key in ("spellList", "replacement", "level")
                if key in trait
            },
        })
    if trait.get("allocation") == "player-choice":
        observations.append({
            "kind": "player-allocation-choice",
            "authoredFields": {
                "allocation": "player-choice",
            },
        })
    if "abilityChoice" in trait or "spellcastingAbilityChoice" in trait:
        observations.append({
            "kind": "ability-choice-in-trait",
            "authoredFields": {
                key: copy.deepcopy(trait[key])
                for key in ("abilityChoice", "spellcastingAbilityChoice")
                if key in trait
            },
        })
    return observations


def propose_standard_preset(selected_source_id, catalog, contracts,
                            blueprints):
    """Return an explicit standard-characteristic proposal and blockers.

    Intrinsic values are selected only if every applicable authored record
    agrees on one value. Multiple offered values are *not* silently resolved
    by selecting the leaf, maximum, or first source. The order of traits
    follows authored lineage order for review convenience only.
    """
    lineage = source_lineage(selected_source_id, catalog)
    contributor = {
        "contributorId": "standard-source",
        "selectedSourceId": selected_source_id,
    }
    pool = characteristic_candidate_pool(
        [contributor], {}, blueprints, catalog, contracts,
    )
    intrinsic = defaultdict(list)
    trait_offers = []
    for item in pool["offers"]:
        if item["kind"] == "intrinsic":
            intrinsic[item["identity"]].append(item)
        elif item["kind"] == "trait":
            trait_offers.append(item)

    proposed_selections, unresolved = [], []
    rationale = []
    for dimension, rows in intrinsic.items():
        unique = {}
        for row in rows:
            unique.setdefault(_value_key(row["value"]), []).append(row)
        if len(unique) == 1:
            value = rows[0]["value"]
            proposed_selections.append({
                "kind": "intrinsic",
                "identity": dimension,
                "value": copy.deepcopy(value),
            })
            rationale.append({
                "kind": "intrinsic-same-value",
                "identity": dimension,
                "value": copy.deepcopy(value),
                "sourceOccurrences": [
                    row["sourceOccurrenceId"] for row in rows
                ],
            })
        else:
            choices = [
                {"value": copy.deepcopy(matching[0]["value"]),
                 "sourceOccurrences": [
                     row["sourceOccurrenceId"] for row in matching
                 ]}
                for matching in unique.values()
            ]
            is_single_authored_choice = (
                len({row.get("sourceOccurrenceId") for row in rows}) == 1
                and all(row.get("authoredField") == "sizeChoice"
                        for row in rows)
            )
            unresolved.append({
                "kind": (
                    "explicit-intrinsic-choice-required"
                    if is_single_authored_choice
                    else "competing-intrinsic-values-require-policy"
                ),
                "identity": dimension,
                "options": choices,
            })

    for item in trait_offers:
        proposed_selections.append({
            "kind": "trait",
            "identity": item["identity"],
            "parameters": copy.deepcopy(item["parameters"]),
        })
        definition = item.get("authoredDefinition") or {}
        choices = _choice_observations(definition)
        if choices:
            unresolved.append({
                "kind": "dependent-trait-choices-not-selected",
                "identity": item["identity"],
                "choices": choices,
            })
        if item.get("reviewRequired"):
            unresolved.append({
                "kind": "trait-effect-semantics-require-review",
                "identity": item["identity"],
                "reviewRequired": copy.deepcopy(item["reviewRequired"]),
            })

    deferred = []
    for item in pool["deferredSourceFields"]:
        classification = item.get("classification", item.get(
            "reason", "unclassified-evidence"
        ))
        deferred.append(copy.deepcopy(item))
        if classification in ("trait-parameter-source", "source-selection-control"):
            # Retain source field as evidence, but it is not by itself a
            # characteristic or a missing whole trait.
            continue
        unresolved.append({
            "kind": (
                "dependent-source-choice-not-selected"
                if classification == "dependent-choice"
                else "source-characteristic-not-yet-modeled"
            ),
            "sourceId": item["sourceId"],
            "field": item["field"],
            "classification": classification,
        })

    # Cultural suggestions do not become Origin traits or inherited grants.
    upstream_defaults = [
        {"sourceId": node["id"],
         "upbringingId": node["defaultUpbringing"]}
        for node in lineage if "defaultUpbringing" in node
    ]
    chosen_upbringing = (
        copy.deepcopy(upstream_defaults[-1])
        if upstream_defaults else None
    )
    return {
        "selectedSourceId": selected_source_id,
        "sourceLineage": [node["id"] for node in lineage],
        "standardSource": contributor,
        "proposedSelections": proposed_selections,
        "sourceRationale": rationale,
        "unresolvedOriginDecisions": unresolved,
        "deferredSourceEvidence": deferred,
        "inheritedUpbringingSuggestions": upstream_defaults,
        "nearestSuggestedUpbringing": chosen_upbringing,
        "upbringingStatus": "independent-culture-not-manifested",
        "candidateOfferCounts": {
            "intrinsicOffers": sum(len(rows) for rows in intrinsic.values()),
            "intrinsicDimensions": len(intrinsic),
            "traitOffers": len(trait_offers),
        },
        "status": "standard-origin-template-proposal-not-authorized",
        "templateComplete": len(unresolved) == 0,
        "caveat": (
            "Unambiguous traits are proposed as authored entitlements, "
            "not exercised choices, earned grants, or final compatible state. "
            "A completed proposal needs explicit decisions and validation."
        ),
    }


def audit_standard_corpus(catalog, contracts, blueprints):
    """Inspect all selectable entries without freezing corpus counts."""
    entries = [
        propose_standard_preset(identifier, catalog, contracts, blueprints)
        for identifier in sorted(catalog)
        if catalog[identifier].get("selectable") is True
    ]
    issues = defaultdict(int)
    for row in entries:
        for problem in row["unresolvedOriginDecisions"]:
            issues[problem["kind"]] += 1
    return {
        "status": "standard-origin-corpus-readiness-evidence-only",
        "selectableSourceCount": len(entries),
        "proposedSelectionsTotal": sum(
            len(item["proposedSelections"]) for item in entries
        ),
        "sourceIssuesByKind": dict(sorted(issues.items())),
        "entries": entries,
    }


def main():
    parser = argparse.ArgumentParser(
        description="Audit standard Species/Ancestry Origin-profile readiness"
    )
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--selected", help="One selectable source ID")
    group.add_argument("--all", action="store_true",
                       help="All currently selectable native sources")
    args = parser.parse_args()
    catalog = read_catalog()
    blueprints = read_blueprints()
    data = (
        audit_standard_corpus(catalog, read_contracts(), blueprints)
        if args.all else
        propose_standard_preset(args.selected, catalog, read_contracts(),
                                blueprints)
    )
    print(json.dumps(data, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
