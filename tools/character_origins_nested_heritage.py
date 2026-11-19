"""Read-only Origin model study: nested Heritage contributors and manifestation.

A mixed contributor offers ONLY the traits and parameter values explicitly
manifested in that contributor's Origin profile. Its parents' broader
candidate pool is never re-exported. Profiles are recalculated against
their declared contributors to reject stale or invented manifested values.

The profiles in this study are UNTRUSTED design artifacts: they are not
Rules Core entitlements, authorized character records, biological claims,
trait costs, or a final first-party content schema.
"""
import argparse
import copy
import json

from character_origins_compound_traits import read_contracts
from character_origins_parameter_inheritance import (
    ParameterInheritanceError,
    _validate_blueprint,
    _value_key,
    candidate_pool,
    read_blueprint,
    validate_parameter_proposal,
)
from character_origins_source_inventory import read_catalog


def _profile_ref(registry, identifier, ancestors):
    if identifier in ancestors:
        raise ParameterInheritanceError(
            "Cyclic Heritage profile references: " +
            " -> ".join([*ancestors, identifier])
        )
    if not isinstance(registry, dict) or identifier not in registry:
        raise ParameterInheritanceError(
            f"Unknown nested Heritage profile: {identifier}"
        )
    profile = registry[identifier]
    if not isinstance(profile, dict) or (
        profile.get("profileId") != identifier
        or profile.get("status") != "draft-origin-profile"
        or not isinstance(profile.get("contributors"), list)
        or not isinstance(profile.get("manifestedTraits"), list)
    ):
        raise ParameterInheritanceError(
            f"Malformed nested Heritage profile: {identifier}"
        )
    return profile


def _require_contributors(contributors):
    if not isinstance(contributors, list):
        raise ParameterInheritanceError("Contributors must be an array")
    seen = set()
    for item in contributors:
        if not isinstance(item, dict) or (
            "contributorId" not in item
            or len(item) != 2
            or (("selectedSourceId" in item) == ("profileId" in item))
        ):
            raise ParameterInheritanceError(
                "Contributor requires one selectedSourceId or profileId"
            )
        identity = item["contributorId"]
        target = item.get("selectedSourceId", item.get("profileId"))
        if not isinstance(identity, str) or not identity or (
            not isinstance(target, str) or not target
            or identity in seen
        ):
            raise ParameterInheritanceError(
                "Contributor occurrence IDs must be nonempty and distinct"
            )
        seen.add(identity)


def _validate_manifested_entry(entry, blueprint):
    if not isinstance(entry, dict) or set(entry) != {
        "mechanicId", "definitionOccurrenceId", "parameters",
        "parameterEvidence",
    }:
        raise ParameterInheritanceError("Malformed manifested trait entry")
    if entry["mechanicId"] != blueprint["mechanicId"] or (
        entry["definitionOccurrenceId"] != blueprint["definitionOccurrenceId"]
    ):
        raise ParameterInheritanceError(
            "Manifested trait definition does not match blueprint"
        )
    if not isinstance(entry["parameterEvidence"], dict) or (
        set(entry["parameterEvidence"]) != set(blueprint["parameters"])
    ):
        raise ParameterInheritanceError("Manifestation provenance is incomplete")


def _validated_manifest(profile, registry, blueprint, catalog, contracts,
                        ancestors):
    """Reconstruct the profile's choice rather than trusting its assertions."""
    recorded = profile["manifestedTraits"]
    matching = [
        item for item in recorded
        if isinstance(item, dict)
        and item.get("mechanicId") == blueprint["mechanicId"]
    ]
    if len(matching) > 1:
        raise ParameterInheritanceError(
            "A profile cannot manifest a trait twice in this study"
        )
    # Still walk the contributor graph if the requested trait is absent.
    # Cycles and dangling references must not escape validation.
    upstream = heritage_candidate_pool(
        profile["contributors"], registry, blueprint, catalog, contracts,
        ancestors,
    )
    if not matching:
        return None
    entry = matching[0]
    _validate_manifested_entry(entry, blueprint)
    resolution = validate_parameter_proposal(upstream, entry["parameters"])
    if not resolution["eligible"]:
        raise ParameterInheritanceError(
            "Nested profile includes a trait/parameter not eligible "
            f"from its contributors: {resolution['reason']}"
        )
    expected = _chosen_evidence(upstream, entry["parameters"], resolution)
    if _value_key(expected) != _value_key(entry["parameterEvidence"]):
        raise ParameterInheritanceError(
            "Nested profile provenance does not match its contributors"
        )
    return copy.deepcopy(entry)


def _chosen_evidence(pool, proposed, resolution):
    """Record all eligible sources of each chosen value, without assigning
    an unsupported unique parent when multiple sources offer the same value.
    """
    matching_configurations = set(
        resolution.get("configurationContributors", [])
    )
    origins = {}
    for parameter, selected in proposed.items():
        matching = [
            option for option in pool["parameterOptions"][parameter]
            if _value_key(option["value"]) == _value_key(selected)
        ]
        if len(matching) != 1:
            raise ParameterInheritanceError(
                "Selected value has missing or ambiguous source evidence"
            )
        rows = matching[0]["contributedBy"]
        if pool["parameterInheritance"]["mode"] == "linked":
            rows = [
                origin for origin in rows
                if origin["contributorId"] in matching_configurations
            ]
        if not rows:
            raise ParameterInheritanceError(
                "Chosen parameter has no qualifying source"
            )
        origins[parameter] = copy.deepcopy(rows)
    return origins


def heritage_candidate_pool(contributors, registry, blueprint, catalog,
                            contracts, ancestors=()):
    """Project one trait's candidate values from immediate contributors only.

    Direct native contributors read their authored source lineage.
    Profile contributors export only previously *manifested* trait instances.
    Their provenance is retained as nested evidence, never treated as extra
    independent traits or an inherited copy of the grandparents' options.
    """
    _validate_blueprint(blueprint)
    _require_contributors(contributors)
    candidates = []
    for contributor in contributors:
        identity = contributor["contributorId"]
        if "selectedSourceId" in contributor:
            selected = contributor["selectedSourceId"]
            native = candidate_pool(
                [{"contributorId": identity, "selectedSourceId": selected}],
                blueprint, catalog, contracts,
            )
            if not native["candidateConfigurations"]:
                continue
            record = native["candidateConfigurations"][0]
            evidence = {
                parameter: {
                    "contributorId": identity,
                    "kind": "direct-source",
                    "selectedSourceId": selected,
                    "resolvedFromSourceId": record["resolvedFromSources"][parameter],
                }
                for parameter in blueprint["parameters"]
            }
            candidates.append({
                "contributorId": identity,
                "sourceKind": "direct-source",
                "sourceRef": selected,
                "parameters": copy.deepcopy(record["parameters"]),
                "parameterRoots": evidence,
            })
        else:
            profile_id = contributor["profileId"]
            profile = _profile_ref(registry, profile_id, ancestors)
            manifest = _validated_manifest(
                profile, registry, blueprint, catalog, contracts,
                (*ancestors, profile_id),
            )
            if manifest is None:
                continue
            evidence = {
                parameter: {
                    "contributorId": identity,
                    "kind": "manifested-profile",
                    "profileId": profile_id,
                    "ancestorEvidence": copy.deepcopy(
                        manifest["parameterEvidence"][parameter]
                    ),
                }
                for parameter in blueprint["parameters"]
            }
            candidates.append({
                "contributorId": identity,
                "sourceKind": "manifested-profile",
                "sourceRef": profile_id,
                "parameters": copy.deepcopy(manifest["parameters"]),
                "parameterRoots": evidence,
            })

    options = {}
    for name in blueprint["parameters"]:
        by_value = {}
        for record in candidates:
            value = record["parameters"][name]
            key = _value_key(value)
            if key not in by_value:
                by_value[key] = {
                    "value": copy.deepcopy(value),
                    "contributedBy": [],
                }
            by_value[key]["contributedBy"].append(
                copy.deepcopy(record["parameterRoots"][name])
            )
        options[name] = list(by_value.values())

    return {
        "mechanicId": blueprint["mechanicId"],
        "definitionOccurrenceId": blueprint["definitionOccurrenceId"],
        "parameterInheritance": copy.deepcopy(
            blueprint["parameterInheritance"]
        ),
        "traitCandidateAvailable": bool(candidates),
        "candidateConfigurations": candidates,
        "parameterOptions": options,
        "status": "heritage-candidate-evidence-only",
    }


def draft_origin_profile(profile_id, contributors, proposed_parameters,
                         registry, blueprint, catalog, contracts,
                         origination=None):
    """Draft a hypothetical Origin profile: explicit manifested or no trait.

    proposed_parameters=None means the candidate trait does not manifest,
    even when it is eligible. Other traits are deliberately out of scope for
    this focused, single-mechanic proof.
    """
    if not isinstance(profile_id, str) or not profile_id or (
        profile_id in registry
    ):
        raise ParameterInheritanceError(
            "Profile ID must be unique and nonempty"
        )
    if origination is not None and not isinstance(origination, dict):
        raise ParameterInheritanceError("Origination must be an object or null")
    pool = heritage_candidate_pool(
        contributors, registry, blueprint, catalog, contracts
    )
    manifested = []
    if proposed_parameters is not None:
        selection = validate_parameter_proposal(pool, proposed_parameters)
        if not selection["eligible"]:
            return {
                "created": False,
                "status": "rejected-origin-profile",
                "reason": selection["reason"],
            }
        manifested.append({
            "mechanicId": blueprint["mechanicId"],
            "definitionOccurrenceId": blueprint["definitionOccurrenceId"],
            "parameters": copy.deepcopy(selection["parameters"]),
            "parameterEvidence": _chosen_evidence(
                pool, selection["parameters"], selection
            ),
        })

    return {
        "profileId": profile_id,
        "origination": copy.deepcopy(origination),
        "contributors": copy.deepcopy(contributors),
        "manifestedTraits": manifested,
        "status": "draft-origin-profile",
        "created": True,
        "authority": "not-authorized",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Inspect explicit manifestations across two generations"
    )
    parser.add_argument("--red-only", action="store_true",
                        help="First generation manifests Fire + Line instead")
    args = parser.parse_args()
    catalog, blueprint, contracts = (
        read_catalog(), read_blueprint(), read_contracts()
    )
    red, bronze = (
        {"contributorId": "red", "selectedSourceId": "ancestry.dragonborn.red"},
        {"contributorId": "bronze",
         "selectedSourceId": "ancestry.dragonborn.bronze"},
    )
    area = ({"shape": "line", "lengthFeet": 30, "widthFeet": 5}
            if args.red_only
            else {"shape": "cone", "lengthFeet": 15})
    first = draft_origin_profile(
        "generation-1", [red, bronze],
        {"damageType": "fire", "area": area},
        {}, blueprint, catalog, contracts,
    )
    registry = {"generation-1": first}
    second_pool = heritage_candidate_pool([
        {"contributorId": "mixed-parent", "profileId": "generation-1"},
        {"contributorId": "human-parent", "selectedSourceId": "species.human"},
    ], registry, blueprint, catalog, contracts)
    print(json.dumps({
        "firstGenerationManifestation": first,
        "secondGenerationEligibility": second_pool,
    }, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
