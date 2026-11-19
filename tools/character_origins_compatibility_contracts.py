"""Experimental, read-only, source-neutral Origin compatibility contracts.

Rules consume *explicit hypothetical characteristic choices*, not contributor
names or unevaluated native source properties. This prototype does not grant
traits, determine balance, infer a Species-specific grouping, or authorize an
exception. It demonstrates a declarative constraint language for future
same-Species ancestry coverage.

A missing prerequisite remains unresolved. A detected rule violation can be
matched to a narrowly authored exception for review, but the exception never
silently converts a proposal into an authorized character configuration.
"""
import argparse
import copy
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_FIXTURE = (
    ROOT / "docs/rewrite/character-origins/"
    "pillar2-synthetic-compatibility-contract-20261111.json"
)


class CompatibilityContractError(ValueError):
    """Invalid declarative contract or explicitly proposed characteristic set."""


def _value_key(value):
    """Compare JSON values without conflating booleans with numeric values."""
    try:
        return json.dumps(value, sort_keys=True, separators=(",", ":"),
                          ensure_ascii=False, allow_nan=False)
    except (ValueError, TypeError) as exc:
        raise CompatibilityContractError(
            "Characteristic values must be valid JSON"
        ) from exc


def _nonempty(value):
    return isinstance(value, str) and bool(value.strip())


def _guard_dimension(name, dimensions):
    if not _nonempty(name) or name not in dimensions:
        raise CompatibilityContractError(f"Unknown dimension: {name!r}")


def _guard_value(value):
    _value_key(value)


def _guard_condition(condition, dimensions):
    if not isinstance(condition, dict) or set(condition) != {
        "dimension", "equals"
    }:
        raise CompatibilityContractError("Conditions require dimension and equals")
    _guard_dimension(condition["dimension"], dimensions)
    _guard_value(condition["equals"])


def validate_compatibility_contract(contract):
    """Reject unsupported expressions and ambiguous or overbroad exceptions."""
    if not isinstance(contract, dict) or set(contract) != {
        "status", "speciesRootId", "dimensions", "rules", "exceptions"
    }:
        raise CompatibilityContractError("Invalid contract envelope")
    if contract["status"] != "synthetic-design-fixture-only" or not _nonempty(
        contract["speciesRootId"]
    ):
        raise CompatibilityContractError(
            "Prototype accepts only explicitly synthetic design fixtures"
        )
    dimensions = contract["dimensions"]
    if (not isinstance(dimensions, list) or not dimensions
            or any(not _nonempty(x) for x in dimensions)
            or len(set(dimensions)) != len(dimensions)):
        raise CompatibilityContractError("Dimensions must be unique identifiers")
    dimension_ids = set(dimensions)
    rules = contract["rules"]
    exceptions = contract["exceptions"]
    if not isinstance(rules, list) or not rules:
        raise CompatibilityContractError("At least one authored rule is required")
    if not isinstance(exceptions, list):
        raise CompatibilityContractError("Exceptions must be a list")
    rule_ids = set()
    for rule in rules:
        if not isinstance(rule, dict) or not set(rule).issuperset({
            "id", "when"
        }) or set(rule) not in (
            {"id", "when", "requires"},
            {"id", "when", "excludes"},
        ):
            raise CompatibilityContractError("Rule requires exactly one effect")
        identity = rule["id"]
        if not _nonempty(identity) or identity in rule_ids:
            raise CompatibilityContractError("Repeated or empty rule identity")
        rule_ids.add(identity)
        if rule["when"] is not None:
            _guard_condition(rule["when"], dimension_ids)
        if "requires" in rule:
            spec = rule["requires"]
            if not isinstance(spec, dict) or set(spec) not in (
                {"dimension", "oneOf"}, {"dimension", "sameAs"},
            ):
                raise CompatibilityContractError(
                    "Requires needs oneOf or sameAs"
                )
            _guard_dimension(spec["dimension"], dimension_ids)
            if "oneOf" in spec:
                if not isinstance(spec["oneOf"], list) or not spec["oneOf"]:
                    raise CompatibilityContractError("oneOf must be nonempty")
                for v in spec["oneOf"]:
                    _guard_value(v)
                if len({_value_key(v) for v in spec["oneOf"]}) != len(
                    spec["oneOf"]
                ):
                    raise CompatibilityContractError("Repeated oneOf value")
            else:
                _guard_dimension(spec["sameAs"], dimension_ids)
                if spec["sameAs"] == spec["dimension"]:
                    raise CompatibilityContractError(
                        "A dimension can not require equality to itself"
                    )
        else:
            spec = rule["excludes"]
            if not isinstance(spec, dict) or set(spec) != {
                "dimension", "oneOf"
            }:
                raise CompatibilityContractError("Excludes requires oneOf")
            _guard_dimension(spec["dimension"], dimension_ids)
            if not isinstance(spec["oneOf"], list) or not spec["oneOf"]:
                raise CompatibilityContractError("Excludes needs values")
            for value in spec["oneOf"]:
                _guard_value(value)
            if len({_value_key(v) for v in spec["oneOf"]}) != len(
                spec["oneOf"]
            ):
                raise CompatibilityContractError("Repeated excluded value")

    exception_ids = set()
    for exception in exceptions:
        if not isinstance(exception, dict) or set(exception) != {
            "id", "match", "waiveRuleIds", "reason"
        }:
            raise CompatibilityContractError("Invalid exception declaration")
        if (not _nonempty(exception["id"])
                or exception["id"] in exception_ids
                or not _nonempty(exception["reason"])):
            raise CompatibilityContractError(
                "Exception must have a distinct ID and a reason"
            )
        exception_ids.add(exception["id"])
        match = exception["match"]
        waivers = exception["waiveRuleIds"]
        if not isinstance(match, dict) or not match:
            raise CompatibilityContractError(
                "Exceptions must specify at least one exact match value"
            )
        for dimension, value in match.items():
            _guard_dimension(dimension, dimension_ids)
            _guard_value(value)
        if (not isinstance(waivers, list) or not waivers
                or any(not _nonempty(x) for x in waivers)
                or len(set(waivers)) != len(waivers)
                or any(x not in rule_ids for x in waivers)):
            raise CompatibilityContractError(
                "Exception may only waive known, distinct rule IDs"
            )


def read_compatibility_fixture(path=DEFAULT_FIXTURE):
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    validate_compatibility_contract(data)
    return data


def _matched(condition, proposal):
    dimension = condition["dimension"]
    if dimension not in proposal:
        return None
    return _value_key(proposal[dimension]) == _value_key(condition["equals"])


def _review_rule(rule, proposal):
    if rule["when"] is not None:
        trigger = _matched(rule["when"], proposal)
        if trigger is None:
            return "unresolved", "trigger-not-selected"
        if trigger is False:
            return "not-applicable", None
    if "requires" in rule:
        required = rule["requires"]
        dim = required["dimension"]
        if dim not in proposal:
            return "unresolved", "required-dimension-not-selected"
        if "oneOf" in required:
            good = any(
                _value_key(proposal[dim]) == _value_key(value)
                for value in required["oneOf"]
            )
        else:
            other = required["sameAs"]
            if other not in proposal:
                return "unresolved", "reference-dimension-not-selected"
            good = _value_key(proposal[dim]) == _value_key(proposal[other])
        return ("satisfied", None) if good else (
            "violated", "required-relationship-not-met"
        )
    exclusion = rule["excludes"]
    dim = exclusion["dimension"]
    if dim not in proposal:
        return "unresolved", "excluded-dimension-not-selected"
    bad = any(
        _value_key(proposal[dim]) == _value_key(value)
        for value in exclusion["oneOf"]
    )
    return ("violated", "excluded-combination-present") if bad else (
        "satisfied", None
    )


def evaluate_compatibility_proposal(contract, selected_values,
                                    species_root_id):
    """Report generic relationships only, with no authorized compatibility.

    An exception annotates a violated rule for DM/designer review; it is not
    silently interpreted as a default first-party authorization.
    """
    validate_compatibility_contract(contract)
    if species_root_id != contract["speciesRootId"]:
        raise CompatibilityContractError(
            "Contract does not cover the proposed Species root"
        )
    if not isinstance(selected_values, dict) or any(
        not isinstance(d, str) or d not in contract["dimensions"]
        for d in selected_values
    ):
        raise CompatibilityContractError("Unknown or malformed proposal dimension")
    for value in selected_values.values():
        _guard_value(value)

    outcomes = []
    for rule in contract["rules"]:
        status, reason = _review_rule(rule, selected_values)
        outcomes.append({
            "ruleId": rule["id"],
            "status": status,
            **({"reason": reason} if reason else {}),
        })

    exception_matches = []
    for exception in contract["exceptions"]:
        if all(
            dim in selected_values and
            _value_key(selected_values[dim]) == _value_key(value)
            for dim, value in exception["match"].items()
        ):
            exception_matches.append(exception)

    reviewed_exceptions = []
    for item in outcomes:
        if item["status"] != "violated":
            continue
        matches = [
            e for e in exception_matches
            if item["ruleId"] in e["waiveRuleIds"]
        ]
        if len(matches) > 1:
            raise CompatibilityContractError(
                f"Ambiguous overlapping exceptions at {item['ruleId']}"
            )
        if matches:
            match = matches[0]
            item["status"] = "violation-with-authored-exception"
            item["exceptionId"] = match["id"]
            reviewed_exceptions.append({
                "exceptionId": match["id"],
                "ruleId": item["ruleId"],
                "reason": match["reason"],
                "status": "requires-explicit-authority",
            })

    statuses = {outcome["status"] for outcome in outcomes}
    if "violated" in statuses:
        overall = "default-rule-conflict"
    elif "unresolved" in statuses:
        overall = "incomplete-selection-needs-review"
    elif "violation-with-authored-exception" in statuses:
        overall = "authored-exception-needs-approval"
    else:
        overall = "no-detected-conflicts-not-certified"

    return {
        "speciesRootId": species_root_id,
        "selectedValues": copy.deepcopy(selected_values),
        "ruleOutcomes": outcomes,
        "reviewedExceptions": reviewed_exceptions,
        "status": overall,
        "compatibilityCertified": False,
        "balanceEvaluated": False,
        "authority": "synthetic-design-evidence-only",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Evaluate synthetic explicit Origin constraint proposals"
    )
    parser.add_argument(
        "--selections-json", required=True,
        help="Explicit JSON object of characteristic decisions"
    )
    parser.add_argument("--fixture", type=Path, default=DEFAULT_FIXTURE)
    args = parser.parse_args()
    contract = read_compatibility_fixture(args.fixture)
    try:
        selected = json.loads(args.selections_json)
    except ValueError as exc:
        parser.error(f"Invalid selection JSON: {exc}")
    result = evaluate_compatibility_proposal(
        contract, selected, contract["speciesRootId"]
    )
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
