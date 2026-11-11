"""Experimental parameter-eligibility study for mixed Character Heritage.

Composes contributor *evidence*, not abilities or earned character grants.
A trait-level policy chooses linked configurations or independently selectable
parameter values. No gameplay, cost, trait entitlement, or balance decision
is made here.
"""
import argparse
import copy
import json
from pathlib import Path

from character_origins_compound_traits import (
    project_compound_traits,
    read_contracts,
)
from character_origins_source_inventory import read_catalog


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BLUEPRINT = (
    ROOT / "docs/rewrite/character-origins/"
    "pillar2-parameter-inheritance-evidence-20261111.json"
)


class ParameterInheritanceError(ValueError):
    """Invalid input, blueprint, or proposed parameter configuration."""


def read_blueprint(path=DEFAULT_BLUEPRINT):
    """Read a provisional mechanic/parameter association, not active rules."""
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    if len(data.get("traits", [])) != 1:
        raise ParameterInheritanceError(
            "This focused prototype accepts exactly one trait definition"
        )
    return data["traits"][0]


def _validate_blueprint(blueprint):
    if not isinstance(blueprint, dict):
        raise ParameterInheritanceError("Trait definition must be an object")
    mechanic_id = blueprint.get("mechanicId")
    occurrence_id = blueprint.get("definitionOccurrenceId")
    if not all(isinstance(value, str) and value for value in
               (mechanic_id, occurrence_id)):
        raise ParameterInheritanceError("Missing mechanic/source identity")
    parameters = blueprint.get("parameters")
    if not isinstance(parameters, dict) or not parameters:
        raise ParameterInheritanceError("Trait requires parameter slots")
    paths = []
    for name, spec in parameters.items():
        if not isinstance(name, str) or not name or not isinstance(spec, dict):
            raise ParameterInheritanceError("Invalid parameter declaration")
        if set(spec) != {"bindingPath"} or not isinstance(
            spec["bindingPath"], str
        ) or not spec["bindingPath"]:
            raise ParameterInheritanceError(
                f"Parameter {name} requires exactly one bindingPath"
            )
        paths.append(spec["bindingPath"])
    if len(paths) != len(set(paths)):
        raise ParameterInheritanceError("Two parameter slots share one path")
    policy = blueprint.get("parameterInheritance")
    if not isinstance(policy, dict) or set(policy) != {"mode"} or (
        policy["mode"] not in ("linked", "independent")
    ):
        raise ParameterInheritanceError(
            "Each trait must explicitly select linked or independent mode"
        )


def _value_key(value):
    """Compare structured JSON options exactly without losing distinctions."""
    return json.dumps(value, sort_keys=True, separators=(",", ":"),
                      ensure_ascii=False)


def candidate_pool(contributors, blueprint, catalog, contracts):
    """Collect per-contributor configurations and each parameter's provenance.

    Contributors are identified by distinct occurrence IDs even if they have
    identical Species. No candidate is selected or automatically inherited.
    """
    _validate_blueprint(blueprint)
    if not isinstance(contributors, list) or not contributors:
        raise ParameterInheritanceError("At least one contributor is required")

    candidates = []
    unresolved = []
    seen = set()
    for contributor in contributors:
        if not isinstance(contributor, dict) or (
            set(contributor) != {"contributorId", "selectedSourceId"}
        ):
            raise ParameterInheritanceError(
                "Contributor requires contributorId and selectedSourceId"
            )
        contributor_id = contributor["contributorId"]
        selected_id = contributor["selectedSourceId"]
        if not isinstance(contributor_id, str) or not contributor_id or (
            not isinstance(selected_id, str) or not selected_id
        ) or contributor_id in seen:
            raise ParameterInheritanceError(
                "Contributor IDs must be distinct and nonempty"
            )
        seen.add(contributor_id)
        projection = project_compound_traits(
            selected_id, catalog, contracts
        )
        matches = [
            trait for trait in projection["compoundTraits"]
            if trait["sourceOccurrenceId"] ==
            blueprint["definitionOccurrenceId"]
        ]
        if not matches:
            continue
        if len(matches) != 1:
            raise ParameterInheritanceError(
                "Ambiguous trait occurrence in a contributor"
            )
        trait = matches[0]
        bindings = {
            binding["path"]: binding
            for binding in trait["parameterBindings"]
        }
        parameter_values = {}
        problems = {}
        for name, spec in blueprint["parameters"].items():
            path = spec["bindingPath"]
            binding = bindings.get(path)
            if binding is None:
                problems[name] = "binding-path-not-in-trait"
            elif binding["status"] != "bound":
                problems[name] = binding.get("reason", "binding-unresolved")
            else:
                parameter_values[name] = copy.deepcopy(binding["resolvedValue"])
        # A partial configuration is not an inheritable option.
        if problems:
            unresolved.append({
                "contributorId": contributor_id,
                "selectedSourceId": selected_id,
                "problems": problems,
            })
            continue
        candidates.append({
            "contributorId": contributor_id,
            "selectedSourceId": selected_id,
            "sourceOccurrenceId": trait["sourceOccurrenceId"],
            "parameters": parameter_values,
            "resolvedFromSources": {
                name: bindings[spec["bindingPath"]]["resolvedFromSourceId"]
                for name, spec in blueprint["parameters"].items()
            },
        })

    options = {}
    for name in blueprint["parameters"]:
        values = {}
        for candidate in candidates:
            value = candidate["parameters"][name]
            key = _value_key(value)
            if key not in values:
                values[key] = {
                    "value": copy.deepcopy(value),
                    "contributedBy": [],
                }
            values[key]["contributedBy"].append({
                "contributorId": candidate["contributorId"],
                "selectedSourceId": candidate["selectedSourceId"],
                "resolvedFromSourceId": candidate["resolvedFromSources"][name],
            })
        options[name] = list(values.values())

    return {
        "mechanicId": blueprint["mechanicId"],
        "definitionOccurrenceId": blueprint["definitionOccurrenceId"],
        "parameterInheritance": copy.deepcopy(
            blueprint["parameterInheritance"]
        ),
        "traitCandidateAvailable": bool(candidates),
        "candidateConfigurations": candidates,
        "parameterOptions": options,
        "unresolvedContributors": unresolved,
        "status": "eligibility-evidence-only",
    }


def validate_parameter_proposal(pool, proposed):
    """Check candidate value eligibility; never grant an ability.

    Linked mode requires one contributed *whole configuration*. Independent
    mode permits one contributed value for each declared parameter. It does
    not override any future dependencies or permission to acquire the trait.
    """
    expected = set(pool["parameterOptions"])
    if not isinstance(proposed, dict) or set(proposed) != expected:
        raise ParameterInheritanceError(
            f"Proposal must choose exactly these parameters: {sorted(expected)}"
        )
    if not pool["traitCandidateAvailable"]:
        return {
            "eligible": False,
            "reason": "no-complete-contributor-configuration",
        }
    if pool["parameterInheritance"]["mode"] == "linked":
        matching = [
            candidate for candidate in pool["candidateConfigurations"]
            if _value_key(candidate["parameters"]) == _value_key(proposed)
        ]
        if not matching:
            return {"eligible": False, "reason": "linked-configuration-not-offered"}
        return {
            "eligible": True,
            "configurationContributors": [
                candidate["contributorId"] for candidate in matching
            ],
            "parameters": copy.deepcopy(proposed),
            "status": "eligible-proposal-not-earned",
        }
    if pool["parameterInheritance"]["mode"] == "independent":
        provenance = {}
        for name, selected in proposed.items():
            matching = next((
                option for option in pool["parameterOptions"][name]
                if _value_key(option["value"]) == _value_key(selected)
            ), None)
            if matching is None:
                return {
                    "eligible": False,
                    "reason": "parameter-value-not-offered",
                    "parameter": name,
                }
            provenance[name] = copy.deepcopy(matching["contributedBy"])
        return {
            "eligible": True,
            "parameterProvenance": provenance,
            "parameters": copy.deepcopy(proposed),
            "status": "eligible-proposal-not-earned",
        }
    raise ParameterInheritanceError("Unrecognized trait selection policy")


def main():
    parser = argparse.ArgumentParser(
        description="Inspect optional Heritage parameter selections only."
    )
    parser.add_argument("--contributors", nargs="+", required=True,
                        metavar="SELECTABLE_SOURCE_ID")
    parser.add_argument("--blueprint", type=Path, default=DEFAULT_BLUEPRINT)
    args = parser.parse_args()
    contributors = [
        {"contributorId": f"contributor-{index + 1}",
         "selectedSourceId": selected}
        for index, selected in enumerate(args.contributors)
    ]
    print(json.dumps(candidate_pool(
        contributors, read_blueprint(args.blueprint), read_catalog(),
        read_contracts()
    ), indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
