"""Read-only multi-characteristic Heritage and manifestation inventory.

Generalizes Pillar 2 beyond one configured trait: intrinsic properties, choice
options, and complete source-qualified traits can be selected explicitly.
Nested contributors export only their manifested values (a design hypothesis,
NOT a finalized prohibition on latent inherited characteristics).

Nothing here earns a trait, enforces balance, executes mechanics, or defines an
authorized runtime schema. Unknown properties, multiple source values, and
unresolved effect semantics remain visible for review.
"""
import copy
import json

from character_origins_compound_traits import (
    _symbolic_references, project_compound_traits,
)
from character_origins_nested_heritage import _require_contributors
from character_origins_parameter_inheritance import (
    ParameterInheritanceError, _validate_blueprint, _value_key,
)
from character_origins_source_inventory import source_inventory


# Intrinsic characteristics that this design study can represent as choices.
# "sizeChoice" supplies candidate values to the same identity as fixed size.
INTRINSIC_DIMENSIONS = {
    "creatureType": "creature.type",
    "size": "creature.size",
    "sizeChoice": "creature.size",
    "walkingSpeedFeet": "movement.walk.speed",
    "climbingSpeedFeet": "movement.climb.speed",
    "swimmingSpeedFeet": "movement.swim.speed",
    "burrowingSpeedFeet": "movement.burrow.speed",
    "darkvisionFeet": "sense.darkvision.range",
    "needs": "creature.needs",
}

# These are property sources or authoring/selection controls, not independently
# inheritable effects. They remain visible in the audit, not silently dropped.
CONTEXT_ONLY_FIELDS = {
    "damageType": "trait-parameter-source",
    "breathWeaponArea": "trait-parameter-source",
    "spellcastingAbilityChoice": "dependent-choice",
    "ancestrySelection": "source-selection-control",
}


def _catalog_offer_records(selected_id, catalog, contracts, blueprints):
    """Project every native trait and all intrinsic fields without mutation."""
    inventory = source_inventory(selected_id, catalog)
    projected = project_compound_traits(selected_id, catalog, contracts)
    trait_projection = {
        item["sourceOccurrenceId"]: item
        for item in projected["compoundTraits"]
    }
    entries, deferred = [], []
    for source in inventory["lineage"]:
        sid = source["sourceId"]
        for field, value in source["intrinsicFields"].items():
            if field in INTRINSIC_DIMENSIONS:
                dimension = INTRINSIC_DIMENSIONS[field]
                if field == "sizeChoice":
                    if (not isinstance(value, dict)
                            or not isinstance(value.get("from"), list)
                            or not value["from"]
                            or any(not isinstance(v, str) or not v
                                   for v in value["from"])):
                        deferred.append({
                            "sourceId": sid, "field": field,
                            "reason": "unrecognized-size-choice",
                            "authoredValue": copy.deepcopy(value),
                        })
                        continue
                    for choice in value["from"]:
                        entries.append({
                            "kind": "intrinsic", "identity": dimension,
                            "value": copy.deepcopy(choice),
                            "sourceOccurrenceId": f"{sid}#intrinsic:{field}",
                            "sourceId": sid,
                            "authoredField": field,
                        })
                else:
                    entries.append({
                        "kind": "intrinsic", "identity": dimension,
                        "value": copy.deepcopy(value),
                        "sourceOccurrenceId": f"{sid}#intrinsic:{field}",
                        "sourceId": sid, "authoredField": field,
                    })
            else:
                deferred.append({
                    "sourceId": sid, "field": field,
                    "classification": CONTEXT_ONLY_FIELDS.get(
                        field, "unclassified-intrinsic"
                    ),
                    "authoredValue": copy.deepcopy(value),
                })

        for trait in source["traits"]:
            occurrence = trait["occurrenceId"]
            definition = trait_projection[occurrence]
            blueprint = blueprints.get(occurrence)
            if blueprint is not None:
                parameters, problems = {}, {}
                bindings = {
                    binding["path"]: binding
                    for binding in definition["parameterBindings"]
                }
                for name, spec in blueprint["parameters"].items():
                    result = bindings.get(spec["bindingPath"])
                    if result is None:
                        problems[name] = "binding-missing"
                    elif result["status"] != "bound":
                        problems[name] = result.get("reason", "unbound")
                    else:
                        parameters[name] = copy.deepcopy(
                            result["resolvedValue"]
                        )
                if problems:
                    deferred.append({
                        "sourceId": sid, "field": occurrence,
                        "classification": "trait-binding-incomplete",
                        "reasons": problems,
                        "authoredDefinition": copy.deepcopy(
                            trait["definition"]
                        ),
                    })
                    continue
            else:
                parameters = {}
                symbolic = list(
                    _symbolic_references(trait["definition"])
                )
                if symbolic:
                    deferred.append({
                        "sourceId": sid, "field": occurrence,
                        "classification": "trait-parameters-not-modeled",
                        "references": copy.deepcopy(symbolic),
                        "authoredDefinition": copy.deepcopy(
                            trait["definition"]
                        ),
                    })
                    continue
            entries.append({
                "kind": "trait",
                "identity": occurrence,
                "parameters": parameters,
                "sourceOccurrenceId": occurrence,
                "sourceId": sid,
                "authoredDefinition": copy.deepcopy(
                    trait["definition"]
                ),
                "reviewRequired": (
                    ["prose-only-effect"]
                    if not set(trait["definition"]) - {
                        "id", "name", "description"
                    } else []
                ),
            })
    return entries, deferred


def _check_blueprints(blueprints):
    if not isinstance(blueprints, dict):
        raise ParameterInheritanceError("Blueprint registry must be an object")
    for occurrence, blueprint in blueprints.items():
        _validate_blueprint(blueprint)
        if occurrence != blueprint["definitionOccurrenceId"]:
            raise ParameterInheritanceError("Blueprint identity mismatch")


def _canonical_profile(profile_id, contributors, selections, registry,
                       blueprints, catalog, contracts, ancestors=(),
                       origination=None):
    if not isinstance(profile_id, str) or not profile_id:
        raise ParameterInheritanceError("Profile ID must be nonempty")
    if not isinstance(selections, list):
        raise ParameterInheritanceError("Manifest selections must be a list")
    if origination is not None and not isinstance(origination, dict):
        raise ParameterInheritanceError("Origination must be an object or null")
    pool = characteristic_candidate_pool(
        contributors, registry, blueprints, catalog, contracts, ancestors,
    )
    chosen = []
    used = set()
    for selection in selections:
        if not isinstance(selection, dict) or (
            "kind" not in selection or "identity" not in selection
        ):
            raise ParameterInheritanceError("Malformed characteristic selection")
        kind, identity = selection["kind"], selection["identity"]
        key = (kind, identity)
        if key in used:
            raise ParameterInheritanceError("Duplicated manifestation identity")
        used.add(key)
        if kind == "intrinsic" and set(selection) == {
            "kind", "identity", "value"
        }:
            offers = [
                entry for entry in pool["offers"]
                if entry["kind"] == kind
                and entry["identity"] == identity
                and _value_key(entry["value"]) == _value_key(
                    selection["value"]
                )
            ]
            if not offers:
                raise ParameterInheritanceError(
                    f"Unoffered intrinsic value: {identity}"
                )
            chosen.append({
                "kind": kind, "identity": identity,
                "value": copy.deepcopy(selection["value"]),
                "contributedBy": [
                    copy.deepcopy(entry["provenance"]) for entry in offers
                ],
            })
        elif kind == "trait" and set(selection) == {
            "kind", "identity", "parameters"
        }:
            offers = [
                entry for entry in pool["offers"]
                if entry["kind"] == kind and entry["identity"] == identity
            ]
            if not offers:
                raise ParameterInheritanceError(
                    f"Unoffered trait: {identity}"
                )
            declared = blueprints.get(identity)
            proposed = selection["parameters"]
            expected = set(declared["parameters"]) if declared else set()
            if not isinstance(proposed, dict) or set(proposed) != expected:
                raise ParameterInheritanceError(
                    f"Trait parameters do not match declared slots: {identity}"
                )
            if declared and (
                declared["parameterInheritance"]["mode"] == "linked"
            ):
                matches = [
                    entry for entry in offers
                    if _value_key(entry["parameters"]) == _value_key(
                        proposed
                    )
                ]
                if not matches:
                    raise ParameterInheritanceError(
                        f"Unlinked configuration rejected: {identity}"
                    )
                evidence = {
                    parameter: [
                        copy.deepcopy(entry["provenance"]) for entry in matches
                    ] for parameter in proposed
                }
            else:
                evidence = {}
                for parameter, value in proposed.items():
                    matching = [
                        entry for entry in offers
                        if parameter in entry["parameters"]
                        and _value_key(entry["parameters"][parameter])
                        == _value_key(value)
                    ]
                    if not matching:
                        raise ParameterInheritanceError(
                            f"Unoffered trait parameter {parameter}: {identity}"
                        )
                    evidence[parameter] = [
                        copy.deepcopy(entry["provenance"])
                        for entry in matching
                    ]
            chosen.append({
                "kind": kind, "identity": identity,
                "parameters": copy.deepcopy(proposed),
                "contributedBy": (
                    [copy.deepcopy(entry["provenance"])
                     for entry in offers]
                    if not declared else []
                ),
                "parameterEvidence": evidence,
                "reviewRequired": copy.deepcopy(
                    offers[0].get("reviewRequired", [])
                ),
            })
        else:
            raise ParameterInheritanceError(
                "Unsupported characteristic selection"
            )

    return {
        "profileId": profile_id,
        "origination": copy.deepcopy(origination),
        "contributors": copy.deepcopy(contributors),
        "selections": copy.deepcopy(selections),
        "manifestedCharacteristics": chosen,
        "reviewFlags": copy.deepcopy(pool["reviewFlags"]),
        "status": "draft-origin-profile",
        "authority": "not-authorized",
    }


def characteristic_candidate_pool(contributors, registry, blueprints, catalog,
                                  contracts, ancestors=()):
    """Produce all available trait and intrinsic source offers.

    A nested profile exports only its explicitly manifested characteristics,
    and is recursively revalidated against its own recorded sources.
    """
    _check_blueprints(blueprints)
    _require_contributors(contributors)
    if not isinstance(registry, dict):
        raise ParameterInheritanceError("Profile registry must be an object")
    offers, deferred = [], []
    for contributor in contributors:
        cid = contributor["contributorId"]
        if "selectedSourceId" in contributor:
            selected = contributor["selectedSourceId"]
            records, issues = _catalog_offer_records(
                selected, catalog, contracts, blueprints
            )
            for record in records:
                offers.append({
                    **record,
                    "provenance": {
                        "contributorId": cid, "kind": "native-source",
                        "selectedSourceId": selected,
                        "sourceOccurrenceId": record["sourceOccurrenceId"],
                    },
                })
            deferred.extend([
                {"contributorId": cid, **issue} for issue in issues
            ])
        else:
            profile_id = contributor["profileId"]
            if profile_id in ancestors:
                raise ParameterInheritanceError(
                    "Cyclic profile references: " +
                    " -> ".join([*ancestors, profile_id])
                )
            profile = registry.get(profile_id)
            if (not isinstance(profile, dict)
                    or profile.get("profileId") != profile_id
                    or profile.get("status") != "draft-origin-profile"):
                raise ParameterInheritanceError(
                    f"Missing or malformed contributor profile: {profile_id}"
                )
            verified = _canonical_profile(
                profile_id, profile.get("contributors"),
                profile.get("selections"), registry, blueprints,
                catalog, contracts, (*ancestors, profile_id),
                profile.get("origination"),
            )
            if _value_key(verified) != _value_key(profile):
                raise ParameterInheritanceError(
                    f"Contributor profile has unverified manifestation: {profile_id}"
                )
            for item in verified["manifestedCharacteristics"]:
                # A previously manifested complete trait is one option.
                # Its ancestor options are not separately reintroduced.
                record = {
                    "kind": item["kind"],
                    "identity": item["identity"],
                    "sourceOccurrenceId": None,
                    "sourceId": None,
                    "provenance": {
                        "contributorId": cid,
                        "kind": "manifested-profile",
                        "profileId": profile_id,
                        "ancestorEvidence": copy.deepcopy(
                            item.get("parameterEvidence")
                            if item["kind"] == "trait"
                            else item["contributedBy"]
                        ),
                    },
                }
                if item["kind"] == "intrinsic":
                    record["value"] = copy.deepcopy(item["value"])
                else:
                    record["parameters"] = copy.deepcopy(
                        item["parameters"]
                    )
                    record["reviewRequired"] = copy.deepcopy(
                        item["reviewRequired"]
                    )
                offers.append(record)

    dimensions = {}
    for item in offers:
        if item["kind"] == "intrinsic":
            dimensions.setdefault(item["identity"], {}).setdefault(
                _value_key(item["value"]), []
            ).append(item["provenance"]["contributorId"])
    conflicts = [
        {
            "kind": "multiple-source-values-needs-origin-policy",
            "identity": dimension,
            "offeredValues": [
                next(
                    copy.deepcopy(item["value"])
                    for item in offers
                    if item["kind"] == "intrinsic"
                    and item["identity"] == dimension
                    and _value_key(item["value"]) == serialized
                ) for serialized in groups
            ],
        }
        for dimension, groups in dimensions.items()
        if len(groups) > 1
    ]
    # A low-light vision trait and Darkvision are different source facts;
    # whether they coexist, replace, or interact is not decided here.
    has_low_light = any(
        item["kind"] == "trait"
        and (item.get("authoredDefinition") or {}).get("rule")
        == "rule.low-light-vision"
        for item in offers
    )
    if has_low_light and "sense.darkvision.range" in dimensions:
        conflicts.append({
            "kind": "senses-interaction-needs-review",
            "identity": "sense.low-light-vs-darkvision",
        })

    return {
        "offers": offers,
        "deferredSourceFields": deferred,
        "reviewFlags": conflicts,
        "status": "origin-characteristic-candidates-only",
    }


def draft_manifestation_profile(profile_id, contributors, selections,
                                registry, blueprints, catalog, contracts,
                                origination=None):
    """Draft a profile with explicit choices, never automatic full packages."""
    if profile_id in registry:
        raise ParameterInheritanceError("Profile ID already exists")
    return _canonical_profile(
        profile_id, contributors, selections, registry, blueprints,
        catalog, contracts, origination=origination,
    )


def main():
    from character_origins_compound_traits import read_contracts
    from character_origins_parameter_inheritance import read_blueprint
    from character_origins_source_inventory import read_catalog

    blueprint = read_blueprint()
    catalog = read_catalog()
    pool = characteristic_candidate_pool(
        [{"contributorId": "elf", "selectedSourceId": "species.elf"},
         {"contributorId": "wood", "selectedSourceId": "ancestry.elf.wood"}],
        {}, {blueprint["definitionOccurrenceId"]: blueprint}, catalog,
        read_contracts()
    )
    print(json.dumps(pool, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
