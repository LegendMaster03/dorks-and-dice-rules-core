"""Preview one complete parameterized trait, without authorizing inheritance.

The preview re-derives the eligible options from the native/catalog evidence
and an explicitly supplied experimental blueprint. No client-supplied
candidate pool is trusted. A configured definition retains the original
mechanic and swaps only declared symbolic parameters with eligible values.

Not an executable character feature, trusted entitlement, balance rule,
or runtime character schema.
"""
import argparse
import copy
import json

from character_origins_compound_traits import (
    _get_path,
    _symbolic_references,
    read_contracts,
)
from character_origins_parameter_inheritance import (
    ParameterInheritanceError,
    candidate_pool,
    read_blueprint,
    validate_parameter_proposal,
)
from character_origins_source_inventory import read_catalog


def _extract_authored_trait(blueprint, catalog):
    occurrence = blueprint["definitionOccurrenceId"]
    if "#trait:" not in occurrence:
        raise ParameterInheritanceError("Invalid source-qualified trait ID")
    source_id, trait_id = occurrence.split("#trait:", 1)
    if not source_id or not trait_id or source_id not in catalog:
        raise ParameterInheritanceError("Unknown trait definition source")
    matching = [
        trait for trait in catalog[source_id].get("traits", [])
        if trait.get("id") == trait_id
    ]
    if len(matching) != 1:
        raise ParameterInheritanceError("Missing or ambiguous trait definition")
    return copy.deepcopy(matching[0])


def _configured_path(binding_path):
    """Map an authored '<name>From' field to its concrete '<name>' field.

    This generic convention is suitable for the currently reviewed fixture.
    A future native schema should author an explicit, typed target path.
    """
    name = binding_path.split(".")[-1]
    if not name.endswith("From") or len(name) <= 4:
        raise ParameterInheritanceError(
            f"Cannot materialize symbolic binding path: {binding_path}"
        )
    return ".".join(
        [*binding_path.split(".")[:-1], name[:-4]]
    )


def _substitute(definition, binding_path, concrete_value):
    """Replace precisely one existing symbolic field; never alter siblings."""
    # Existing compound binding paths use dotted keys. Reject array indexes
    # until an explicit typed path model is reviewed, rather than guessing.
    if not isinstance(binding_path, str) or not binding_path or (
        "[" in binding_path or "]" in binding_path
    ):
        raise ParameterInheritanceError("Unsupported materialization path")
    keys = binding_path.split(".")
    parent = definition
    for key in keys[:-1]:
        if not isinstance(parent, dict) or key not in parent:
            raise ParameterInheritanceError("Missing symbolic parent path")
        parent = parent[key]
    source_field = keys[-1]
    target_field = _configured_path(binding_path).split(".")[-1]
    if not isinstance(parent, dict) or source_field not in parent:
        raise ParameterInheritanceError("Missing symbolic source field")
    if target_field in parent:
        raise ParameterInheritanceError(
            "Concrete field already exists; refusing overwrite"
        )
    if not isinstance(parent[source_field], str):
        raise ParameterInheritanceError("Symbolic field is not a reference")
    del parent[source_field]
    parent[target_field] = copy.deepcopy(concrete_value)


def configure_trait(contributors, proposed_parameters, blueprint, catalog,
                    binding_contracts):
    """Return a non-authoritative, configured mechanical definition preview.

    Eligibility, source provenance, and the complete original trait are
    reconstructed from supplied design evidence each time. The proposal can
    only set declared inheritable parameters; fixed activation, charges,
    saving throws, progression, etc. are copied unchanged.
    """
    pool = candidate_pool(contributors, blueprint, catalog, binding_contracts)
    choice = validate_parameter_proposal(pool, proposed_parameters)
    if not choice["eligible"]:
        return {
            "configured": False,
            "reason": choice["reason"],
            "parameter": choice.get("parameter"),
            "status": "rejected-not-earned",
        }

    authored = _extract_authored_trait(blueprint, catalog)
    required_paths = {
        name: spec["bindingPath"] for name, spec in blueprint["parameters"].items()
    }
    source_symbolic = dict(_symbolic_references(authored))
    if set(source_symbolic) != set(required_paths.values()):
        raise ParameterInheritanceError(
            "Unresolved symbolic fields outside declared parameter bindings"
        )

    # Keep a complete, original source definition for audit and future
    # equivalence checks. The configured copy changes no fixed mechanics.
    configured = copy.deepcopy(authored)
    for parameter, path in required_paths.items():
        _substitute(configured, path, choice["parameters"][parameter])

    if dict(_symbolic_references(configured)):
        raise ParameterInheritanceError("Symbolic references remain unresolved")

    return {
        "mechanicId": pool["mechanicId"],
        "definitionOccurrenceId": pool["definitionOccurrenceId"],
        "parameterInheritance": copy.deepcopy(pool["parameterInheritance"]),
        "selectedParameters": copy.deepcopy(choice["parameters"]),
        "parameterProvenance": copy.deepcopy(
            choice.get("parameterProvenance", {})
        ),
        "configurationContributors": copy.deepcopy(
            choice.get("configurationContributors", [])
        ),
        "authoredDefinition": authored,
        "configuredDefinition": configured,
        "configured": True,
        "status": "configured-preview-not-earned",
        "evaluationStatus": "not-evaluated",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Preview a fully parameter-bound trait without granting it"
    )
    parser.add_argument("--contributors", nargs="+", required=True,
                        metavar="SELECTABLE_SOURCE_ID")
    parser.add_argument("--damage-type", required=True)
    parser.add_argument("--area", required=True,
                        help="JSON area object, e.g. '{\"shape\":\"cone\",\"lengthFeet\":15}'")
    args = parser.parse_args()
    contributors = [
        {"contributorId": f"contributor-{index + 1}",
         "selectedSourceId": source}
        for index, source in enumerate(args.contributors)
    ]
    proposal = {"damageType": args.damage_type,
                "area": json.loads(args.area)}
    print(json.dumps(configure_trait(
        contributors, proposal, read_blueprint(), read_catalog(),
        read_contracts()
    ), indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
