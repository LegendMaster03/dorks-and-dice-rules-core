"""Read-only, source-independent numerical preview of configured abilities.

The evaluator receives the SAME configured definition for direct standard
selection, mixed-contributor parameter combinations, and homebrew abilities.
It deliberately interprets only the explicitly supported area-effect fields.
Unknown effects fail closed instead of being interpreted from prose.

Not a trusted rules evaluator, owned trait, damage application, or combat API.
Proficiency bonus and ability modifiers are supplied for the illustrative
character context, not inferred from or verified against a character build.
"""
import argparse
import copy
import json
import re

from character_origins_compound_traits import read_contracts
from character_origins_configured_trait import configure_trait
from character_origins_parameter_inheritance import read_blueprint
from character_origins_source_inventory import read_catalog


_DICE = re.compile(r"([1-9][0-9]*)d([1-9][0-9]*)")
_MODIFIER = re.compile(r"([a-z]+)-modifier")
_ALLOWED_DEFINITION = {
    "id", "name", "description", "activation", "uses", "savingThrow",
    "damage", "area",
}
_ALLOWED_ACTIVATION = {"action", "replaces", "timing"}
_ALLOWED_USES = {"value", "recharge", "resource", "independentOf"}
_ALLOWED_SAVE = {"ability", "dc", "onSuccess"}
_ALLOWED_DAMAGE = {"type", "progression", "dice"}


class MechanicsPreviewError(ValueError):
    """An unsupported or malformed mechanical expression was encountered."""


def _exact_int(value, name, minimum=None):
    if type(value) is not int or (minimum is not None and value < minimum):
        raise MechanicsPreviewError(f"Invalid integer for {name}")
    return value


def _object(value, name, allowed, required):
    if not isinstance(value, dict):
        raise MechanicsPreviewError(f"{name} must be an object")
    if not set(value).issubset(allowed):
        raise MechanicsPreviewError(
            f"Unsupported {name} fields: {sorted(set(value) - allowed)}"
        )
    missing = required - set(value)
    if missing:
        raise MechanicsPreviewError(
            f"Missing {name} fields: {sorted(missing)}"
        )
    return value


def _text(value, name):
    if not isinstance(value, str) or not value.strip():
        raise MechanicsPreviewError(f"{name} must be a nonempty string")
    return value


def evaluate_integer_expression(expression, context):
    """Evaluate additive numeric terms without eval or arbitrary expression code.

    Supported terms are nonnegative constants, proficiency-bonus and
    <ability>-modifier. An ability modifier may be negative. Unknown terms
    and any other syntax are intentionally rejected.
    """
    expression = _text(expression, "numeric expression")
    if not isinstance(context, dict):
        raise MechanicsPreviewError("Character context must be an object")
    modifiers = context.get("abilityModifiers")
    if not isinstance(modifiers, dict):
        raise MechanicsPreviewError("Missing abilityModifiers")
    total = 0
    for term in expression.split("+"):
        term = term.strip()
        if not term:
            raise MechanicsPreviewError("Empty numeric expression term")
        if term.isdecimal():
            total += int(term)
        elif term == "proficiency-bonus":
            total += _exact_int(
                context.get("proficiencyBonus"), "proficiencyBonus", 1
            )
        else:
            match = _MODIFIER.fullmatch(term)
            if match is None:
                raise MechanicsPreviewError(f"Unsupported expression: {term}")
            ability = match.group(1)
            total += _exact_int(
                modifiers.get(ability), f"{ability} modifier"
            )
    return total


def _damage_dice(damage, level):
    if "progression" in damage:
        if "dice" in damage:
            raise MechanicsPreviewError("Damage specifies both dice and progression")
        entries = damage["progression"]
        if not isinstance(entries, list) or not entries:
            raise MechanicsPreviewError("Missing damage progression steps")
        thresholds = []
        for step in entries:
            _object(step, "damage progression step",
                    {"characterLevel", "dice"},
                    {"characterLevel", "dice"})
            threshold = _exact_int(step["characterLevel"], "step level", 1)
            dice = _text(step["dice"], "damage dice")
            if _DICE.fullmatch(dice) is None:
                raise MechanicsPreviewError(f"Unsupported dice expression: {dice}")
            thresholds.append((threshold, dice))
        if thresholds != sorted(thresholds) or len({
            threshold for threshold, _ in thresholds
        }) != len(thresholds):
            raise MechanicsPreviewError("Progression levels must strictly ascend")
        available = [dice for threshold, dice in thresholds if level >= threshold]
        if not available:
            raise MechanicsPreviewError("Feature has no damage at this level")
        return available[-1]
    dice = _text(damage.get("dice"), "fixed damage dice")
    if _DICE.fullmatch(dice) is None:
        raise MechanicsPreviewError(f"Unsupported dice expression: {dice}")
    return dice


def _area(area):
    _object(area, "area", {"shape", "lengthFeet", "widthFeet"},
            {"shape", "lengthFeet"})
    shape = _text(area["shape"], "area shape")
    if shape == "line":
        if "widthFeet" not in area:
            raise MechanicsPreviewError("Line area needs widthFeet")
        _exact_int(area["widthFeet"], "line width", 1)
    elif shape == "cone":
        if "widthFeet" in area:
            raise MechanicsPreviewError("Cone has unexpected line width")
    else:
        raise MechanicsPreviewError(f"Unsupported area shape: {shape}")
    _exact_int(area["lengthFeet"], "area length", 1)
    return copy.deepcopy(area)


def evaluate_configured_definition(definition, context):
    """Derive numeric facts from structured fields, never from a Species ID.

    This is intentionally a narrow area-effect slice. Any unsupported field
    halts interpretation; no prose interpretation or damage rolls occur.
    """
    _object(definition, "configured definition",
            _ALLOWED_DEFINITION, {
                "id", "activation", "uses", "savingThrow", "damage", "area"
            })
    if not isinstance(context, dict):
        raise MechanicsPreviewError("Invalid character context")
    level = _exact_int(context.get("characterLevel"), "characterLevel", 1)
    _exact_int(context.get("proficiencyBonus"), "proficiencyBonus", 1)

    activation = _object(definition["activation"], "activation",
                         _ALLOWED_ACTIVATION, {"action"})
    _text(activation["action"], "activation action")
    for field in ("replaces", "timing"):
        if field in activation:
            _text(activation[field], f"activation {field}")

    uses = _object(definition["uses"], "uses", _ALLOWED_USES, {
        "value", "recharge",
    })
    value = uses["value"]
    count = (_exact_int(context["proficiencyBonus"], "proficiencyBonus", 1)
             if value == "proficiency-bonus"
             else _exact_int(value, "uses", 0))
    _text(uses["recharge"], "recharge")
    for field in ("resource", "independentOf"):
        if field in uses:
            _text(uses[field], field)

    saving = _object(definition["savingThrow"], "saving throw",
                     _ALLOWED_SAVE, {"ability", "dc", "onSuccess"})
    _text(saving["ability"], "saving throw ability")
    _text(saving["onSuccess"], "save success behavior")
    dc = evaluate_integer_expression(saving["dc"], context)

    damage = _object(definition["damage"], "damage", _ALLOWED_DAMAGE,
                     {"type"})
    damage_type = _text(damage["type"], "damage type")
    dice = _damage_dice(damage, level)
    matched = _DICE.fullmatch(dice)
    quantity, sides = int(matched.group(1)), int(matched.group(2))

    return {
        "evaluationStatus": "structured-mechanics-preview",
        "characterContext": {
            "characterLevel": level,
            "proficiencyBonus": context["proficiencyBonus"],
        },
        "activation": copy.deepcopy(activation),
        "area": _area(definition["area"]),
        "savingThrow": {
            "ability": saving["ability"],
            "dc": dc,
            "onSuccess": saving["onSuccess"],
        },
        "damage": {
            "type": damage_type,
            "dice": dice,
            "minimumRoll": quantity,
            "maximumRoll": quantity * sides,
        },
        "uses": {
            **copy.deepcopy(uses),
            "maximumUses": count,
        },
        "unexecutedOperations": [
            "validate-action-availability",
            "choose-and-check-targets",
            "roll-saving-throws",
            "roll-and-apply-damage",
            "consume-and-recharge-resources",
        ],
    }


def evaluate_origin_preview(contributors, proposed_parameters, blueprint,
                            catalog, contracts, context):
    """Rebuild configured evidence, then evaluate it via the common function."""
    instance = configure_trait(
        contributors, proposed_parameters, blueprint, catalog, contracts
    )
    if not instance["configured"]:
        return instance
    return {
        "mechanicId": instance["mechanicId"],
        "definitionOccurrenceId": instance["definitionOccurrenceId"],
        "selectedParameters": copy.deepcopy(instance["selectedParameters"]),
        "parameterProvenance": copy.deepcopy(instance["parameterProvenance"]),
        "configurationContributors": copy.deepcopy(
            instance["configurationContributors"]
        ),
        "evaluatedMechanics": evaluate_configured_definition(
            instance["configuredDefinition"], context
        ),
        "status": "evaluated-preview-not-earned",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Inspect a configured trait's known mechanics without combat"
    )
    parser.add_argument("--contributors", nargs="+", required=True)
    parser.add_argument("--damage-type", required=True)
    parser.add_argument("--area", required=True,
                        help="Complete JSON area object")
    parser.add_argument("--level", type=int, required=True)
    parser.add_argument("--pb", type=int, required=True)
    parser.add_argument("--constitution-modifier", type=int, required=True)
    args = parser.parse_args()
    contributors = [
        {"contributorId": f"contributor-{i+1}",
         "selectedSourceId": source}
        for i, source in enumerate(args.contributors)
    ]
    result = evaluate_origin_preview(
        contributors, {
            "damageType": args.damage_type, "area": json.loads(args.area)
        }, read_blueprint(), read_catalog(), read_contracts(), {
            "characterLevel": args.level,
            "proficiencyBonus": args.pb,
            "abilityModifiers": {"constitution": args.constitution_modifier},
        }
    )
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
