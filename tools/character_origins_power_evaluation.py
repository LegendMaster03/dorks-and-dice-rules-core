"""Read-only, scenario-based Pillar 2 power evaluation prototype.

Source: docs/rewrite/character-origins/pillar2-species-power-evaluation.md.
This deliberately reports separate endpoints and unknown mechanics, never an
unjustified universal power score or authorization to inherit a trait.
"""
from __future__ import annotations

import argparse
from collections import defaultdict
from functools import lru_cache
import copy
import json
import math
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_NATIVE = ROOT / "data/rulesets/first-party-001/native"
LEVELS = (1, 3, 4, 5, 11, 17)
PB_BY_LEVEL = {1: 2, 3: 2, 4: 2, 5: 3, 11: 4, 17: 6}
SOURCE_METADATA = frozenset({
    "id", "name", "kind", "parent", "selectable", "status", "traits",
    "reviewNotes", "defaultUpbringing", "description", "sourceReferences",
})
DEFAULT_SCENARIO = {
    "id": "diagnostic-reference-not-campaign-prevalence",
    "baseCheckSuccess": 0.5,
    "saveFailure": 0.55,
    "attackReplacementExpectedDamage": 5.2,
    "incomingDamage": [{"type": "fire", "amount": 20},
                       {"type": "lightning", "amount": 20},
                       {"type": "poison", "amount": 20}],
    "skillChecks": {"skill.listen": 2, "skill.spot": 2},
    "rolls": {"attack-roll": 8, "skill.search": 2, "skill.spot": 2},
    "conditionFrequency": {"condition.dazzled": 0.5},
    "damageTypeMultiplier": {},
    "abilityModifiers": {"constitution": 3},
    "saveEvents": [],
    "reactionDamageEvents": [],
    "spellDemands": [],
    "controlOpportunities": [],
    "strikeOpportunities": [],
    "tacticalOpportunities": [],
    "jointCombatPlans": [],
    "nativeClassActionPlans": [],
    "breathOpportunities": [
        {"areaTargets": {"line": 3, "cone": 1}},
        {"areaTargets": {"line": 1, "cone": 3}},
    ],
    "mobilityChallenges": [
        {"id": "walk-35", "mode": "walk", "distanceFeet": 35,
         "startMinute": 0},
        {"id": "climb-wall", "mode": "climb", "distanceFeet": 15,
         "startMinute": 1},
        {"id": "burrow-sand", "mode": "burrow", "distanceFeet": 15,
         "material": "loose-natural-sand", "startMinute": 2},
        {"id": "open-gap-1", "mode": "fly", "distanceFeet": 20,
         "startMinute": 3},
        {"id": "open-gap-2", "mode": "fly", "distanceFeet": 20,
         "startMinute": 18},
        {"id": "open-gap-3", "mode": "fly", "distanceFeet": 20,
         "startMinute": 33},
    ],
    "tremorsenseContacts": [
        {"distanceFeet": 10, "sensorTouches": "earth", "targetTouches": "earth",
         "connected": True, "anticipated": False},
        {"distanceFeet": 40, "sensorTouches": "stone", "targetTouches": "stone",
         "connected": True, "anticipated": True},
        {"distanceFeet": 10, "sensorTouches": "stone", "targetTouches": "stone",
         "connected": True, "anticipated": True},
        {"distanceFeet": 10, "sensorTouches": "air", "targetTouches": "stone",
         "connected": False, "anticipated": True},
    ],
}


# Only these fields have a defined interpretation. Reject others so a misspelled
# fixture cannot quietly produce apparently reproducible numerical output.
SCENARIO_FIELDS = frozenset(DEFAULT_SCENARIO)
MOVEMENT_MODES = frozenset(("walk", "climb", "swim", "burrow", "fly"))
# A scheduled, *potential* slot must occur before the effect expires. These
# are not assumed to be guaranteed actions or uniform encounter frequencies.
CONTROL_WINDOW_SLOTS = ("ActionSlots", "BonusActionSlots", "ReactionSlots",
                        "ConcentratingTargets")
CONTROL_WINDOW_PRONE = "NormalAttackRollsWhileProne"
CONTROL_WINDOW_FIELDS = frozenset(
    f"{side}{field}" for side in ("enemy", "ally")
    for field in (*CONTROL_WINDOW_SLOTS, CONTROL_WINDOW_PRONE))



def _number(value, path: str, minimum: float = 0.0) -> float:
    if (isinstance(value, bool) or not isinstance(value, (float, int))
            or not math.isfinite(value) or value < minimum):
        raise ValueError(f"{path} must be finite and >= {minimum}")
    return value


def _count(value, path: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise ValueError(f"{path} must be a nonnegative integer")
    return value


def _probability(value, path: str) -> float:
    result = _number(value, path)
    if result > 1:
        raise ValueError(f"{path} must be <= 1")
    return result


def _nonempty(value, path: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{path} must be a nonempty string")
    return value


def validate_scenario(scenario: dict) -> None:
    """Validate declared fixture assumptions rather than accepting silent zeros."""
    if not isinstance(scenario, dict):
        raise ValueError("Scenario must be a JSON object")
    unexpected = sorted(set(scenario) - SCENARIO_FIELDS)
    if unexpected:
        raise ValueError(f"Unknown scenario fields: {unexpected}")
    if "id" in scenario:
        _nonempty(scenario["id"], "id")
    for field in ("baseCheckSuccess", "saveFailure"):
        if field in scenario:
            _probability(scenario[field], field)
    if "attackReplacementExpectedDamage" in scenario:
        _number(scenario["attackReplacementExpectedDamage"],
                "attackReplacementExpectedDamage")
    ability_modifiers = scenario.get("abilityModifiers", {})
    if not isinstance(ability_modifiers, dict):
        raise ValueError("abilityModifiers must be an object")
    for ability, modifier in ability_modifiers.items():
        _nonempty(ability, "abilityModifiers key")
        if isinstance(modifier, bool) or not isinstance(modifier, int) or abs(modifier) > 30:
            raise ValueError(f"abilityModifiers.{ability} must be an integer from -30 to 30")
    for field in ("skillChecks", "rolls"):
        obj = scenario.get(field, {})
        if not isinstance(obj, dict):
            raise ValueError(f"{field} must be an object")
        for name, count in obj.items():
            _nonempty(name, f"{field} key")
            _count(count, f"{field}.{name}")
    for field in ("conditionFrequency", "damageTypeMultiplier"):
        obj = scenario.get(field, {})
        if not isinstance(obj, dict):
            raise ValueError(f"{field} must be an object")
        for name, value in obj.items():
            _nonempty(name, f"{field} key")
            if field == "conditionFrequency":
                _probability(value, f"{field}.{name}")
            else:
                _number(value, f"{field}.{name}")
    for field in ("incomingDamage", "breathOpportunities", "saveEvents",
                  "reactionDamageEvents", "spellDemands", "controlOpportunities",
                  "strikeOpportunities", "tacticalOpportunities",
                  "jointCombatPlans", "nativeClassActionPlans",
                  "tremorsenseContacts", "mobilityChallenges"):
        events = scenario.get(field, [])
        if not isinstance(events, list):
            raise ValueError(f"{field} must be an array")
        seen_ids = set()
        for index, event in enumerate(events):
            location = f"{field}[{index}]"
            if not isinstance(event, dict):
                raise ValueError(f"{location} must be an object")
            if field == "incomingDamage":
                _nonempty(event.get("type"), f"{location}.type")
                _number(event.get("amount"), f"{location}.amount")
            elif field == "breathOpportunities":
                targets = event.get("areaTargets")
                if not isinstance(targets, dict):
                    raise ValueError(f"{location}.areaTargets must be an object")
                for shape, count in targets.items():
                    _nonempty(shape, f"{location}.areaTargets key")
                    _count(count, f"{location}.areaTargets.{shape}")
                if "saveFailure" in event:
                    _probability(event["saveFailure"], f"{location}.saveFailure")
                if "attackReplacementExpectedDamage" in event:
                    _number(event["attackReplacementExpectedDamage"],
                            f"{location}.attackReplacementExpectedDamage")
            elif field == "saveEvents":
                extra = set(event) - {
                    "id", "ability", "tags", "magical", "baseSuccess",
                    "conditionOnFailure", "damageOnFailure", "damageOnSuccess", "damageType"}
                if extra:
                    raise ValueError(f"Unknown {location} fields: {sorted(extra)}")
                _nonempty(event.get("id"), f"{location}.id")
                _nonempty(event.get("ability"), f"{location}.ability")
                _probability(event.get("baseSuccess"), f"{location}.baseSuccess")
                tags = event.get("tags", [])
                if not isinstance(tags, list) or any(
                        not isinstance(t, str) or not t.strip() for t in tags):
                    raise ValueError(f"{location}.tags must be an array of nonempty strings")
                if "magical" in event and not isinstance(event["magical"], bool):
                    raise ValueError(f"{location}.magical must be boolean")
                if "conditionOnFailure" in event:
                    _nonempty(event["conditionOnFailure"], f"{location}.conditionOnFailure")
                damaged = "damageOnFailure" in event or "damageOnSuccess" in event
                if damaged:
                    _number(event.get("damageOnFailure"), f"{location}.damageOnFailure")
                    _number(event.get("damageOnSuccess"), f"{location}.damageOnSuccess")
                    _nonempty(event.get("damageType"), f"{location}.damageType")
                if "damageType" in event and not damaged:
                    raise ValueError(f"{location}.damageType requires damage amounts")
                if not damaged and "conditionOnFailure" not in event:
                    raise ValueError(f"{location} requires a damage or condition outcome")
            elif field == "reactionDamageEvents":
                extra = set(event) - {
                    "id", "round", "amount", "damageType", "isAttackHit",
                    "attackerVisible", "reactionAvailable"}
                if extra:
                    raise ValueError(f"Unknown {location} fields: {sorted(extra)}")
                _nonempty(event.get("id"), f"{location}.id")
                _count(event.get("round"), f"{location}.round")
                if event["round"] < 1:
                    raise ValueError(f"{location}.round must be >= 1")
                _number(event.get("amount"), f"{location}.amount")
                _nonempty(event.get("damageType"), f"{location}.damageType")
                for flag in ("isAttackHit", "attackerVisible", "reactionAvailable"):
                    if flag not in event or not isinstance(event[flag], bool):
                        raise ValueError(f"{location}.{flag} must be boolean")
            elif field == "spellDemands":
                extra = set(event) - {"id", "spell", "eligible"}
                if extra:
                    raise ValueError(f"Unknown {location} fields: {sorted(extra)}")
                _nonempty(event.get("id"), f"{location}.id")
                _nonempty(event.get("spell"), f"{location}.spell")
                if "eligible" in event and not isinstance(event["eligible"], bool):
                    raise ValueError(f"{location}.eligible must be boolean")
            elif field == "controlOpportunities":
                extra = set(event) - ({"id", "option", "areaShape", "enemyTargets",
                                       "allyTargets", "failureProbability",
                                       "attackReplacementExpectedDamage", "baseHitChance"}
                                      | CONTROL_WINDOW_FIELDS)
                if extra:
                    raise ValueError(f"Unknown {location} fields: {sorted(extra)}")
                _nonempty(event.get("id"), f"{location}.id")
                _nonempty(event.get("option"), f"{location}.option")
                _nonempty(event.get("areaShape"), f"{location}.areaShape")
                _count(event.get("enemyTargets"), f"{location}.enemyTargets")
                _count(event.get("allyTargets"), f"{location}.allyTargets")
                _probability(event.get("failureProbability"),
                             f"{location}.failureProbability")
                if "attackReplacementExpectedDamage" in event:
                    _number(event["attackReplacementExpectedDamage"],
                            f"{location}.attackReplacementExpectedDamage")
                if "baseHitChance" in event:
                    _probability(event["baseHitChance"], f"{location}.baseHitChance")
                for side in ("enemy", "ally"):
                    targets = event[f"{side}Targets"]
                    for field in CONTROL_WINDOW_SLOTS:
                        name = f"{side}{field}"
                        if name in event:
                            _count(event[name], f"{location}.{name}")
                            if event[name] > targets:
                                raise ValueError(f"{location}.{name} exceeds declared targets")
                    name = f"{side}{CONTROL_WINDOW_PRONE}"
                    if name in event:
                        _count(event[name], f"{location}.{name}")
                        if event[name] and not targets:
                            raise ValueError(f"{location}.{name} has no targets")
                if (any(f"{side}{CONTROL_WINDOW_PRONE}" in event
                        for side in ("enemy", "ally")) and
                        "baseHitChance" not in event):
                    raise ValueError(f"{location}.baseHitChance required for Prone attacks")
            elif field == "strikeOpportunities":
                extra = set(event) - {
                    "id", "traitId", "turn", "delivery", "mode", "targetSize",
                    "hit", "dealtDamage", "targetIsCreature", "failureProbability",
                    "normalAttackRollsBeforeRecovery", "baseHitChance",
                    "targetSpeedFeet"}
                if extra:
                    raise ValueError(f"Unknown {location} fields: {sorted(extra)}")
                _nonempty(event.get("id"), f"{location}.id")
                _nonempty(event.get("traitId"), f"{location}.traitId")
                _count(event.get("turn"), f"{location}.turn")
                if event["turn"] < 1:
                    raise ValueError(f"{location}.turn must be >= 1")
                if event.get("delivery") not in (
                        "melee-weapon", "thrown-weapon", "other-attack-roll"):
                    raise ValueError(f"{location}.delivery is invalid")
                if event.get("mode", "base") not in ("base", "alternate"):
                    raise ValueError(f"{location}.mode is invalid")
                if event.get("targetSize") not in (
                        "tiny", "small", "medium", "large", "huge", "gargantuan"):
                    raise ValueError(f"{location}.targetSize is invalid")
                for flag in ("hit", "dealtDamage", "targetIsCreature"):
                    if not isinstance(event.get(flag), bool):
                        raise ValueError(f"{location}.{flag} must be boolean")
                if event["dealtDamage"] and not event["hit"]:
                    raise ValueError(f"{location}.dealtDamage requires a hit")
                _probability(event.get("failureProbability"),
                             f"{location}.failureProbability")
                if "normalAttackRollsBeforeRecovery" in event:
                    _count(event["normalAttackRollsBeforeRecovery"],
                           f"{location}.normalAttackRollsBeforeRecovery")
                    if "baseHitChance" not in event:
                        raise ValueError(f"{location}.baseHitChance required for attack rolls")
                if "baseHitChance" in event:
                    _probability(event["baseHitChance"], f"{location}.baseHitChance")
                if "targetSpeedFeet" in event:
                    _number(event["targetSpeedFeet"], f"{location}.targetSpeedFeet")
            elif field == "nativeClassActionPlans":
                # These are declared *pure-class* diagnostic builds. A fixture
                # evaluated at each breakpoint uses that same class level, not
                # a guessed multiclass distribution or feat/spell selection.
                allowed = {"id", "classId", "attackProcedure", "baseHitChance",
                           "weaponDamageOnHit", "events"}
                if set(event) - allowed:
                    raise ValueError(f"Unknown {location} fields: {sorted(set(event) - allowed)}")
                for name in ("id", "classId"):
                    _nonempty(event.get(name), f"{location}.{name}")
                if event.get("attackProcedure") not in ("standard", "iterative"):
                    raise ValueError(f"{location}.attackProcedure must be standard or iterative")
                _probability(event.get("baseHitChance"), f"{location}.baseHitChance")
                _number(event.get("weaponDamageOnHit"), f"{location}.weaponDamageOnHit")
                if not isinstance(event.get("events"), list) or not event["events"]:
                    raise ValueError(f"{location}.events must be nonempty array")
                local_ids = set()
                previous_round = 0
                for ei, action in enumerate(event["events"]):
                    epath = f"{location}.events[{ei}]"
                    if not isinstance(action, dict):
                        raise ValueError(f"{epath} must be an object")
                    common = {"id", "round", "kind"}
                    fields = {
                        "attack": {"precisionHits", "strengthBasedAttacks"},
                        "class-bonus-action": {"featureId", "option", "movedThisTurn",
                                               "wearingHeavyArmor", "rageEndedBeforeActivation"},
                        "origin-bonus-action": {"traitId", "rockAvailable", "distanceFeet",
                                                "targetIsCreature", "hitChance", "saveFailure"},
                        "breath": {"traitId", "targets", "saveFailure", "replacedAttackIndex",
                                   "spellAlternative", "strengthBasedAttacks"},
                        "searing-ignition": {"traitId", "targets", "saveFailure",
                                             "replacedAttackIndex", "spellAlternative",
                                             "strengthBasedAttacks"},
                        "spell": {"slotLevel", "expectedDamage", "spellId"},
                        "class-reaction": {"featureId", "damageAmount", "damageType",
                                           "attackerVisible", "isAttackHit"},
                        "origin-reaction": {"traitId", "damageAmount", "damageType",
                                            "attackerVisible", "isAttackHit"},
                    }
                    kind = action.get("kind")
                    if kind not in fields or set(action) - common - fields[kind]:
                        raise ValueError(f"{epath}.kind or fields unsupported")
                    name = _nonempty(action.get("id"), f"{epath}.id")
                    if name in local_ids:
                        raise ValueError(f"Duplicate {epath}.id {name}")
                    local_ids.add(name)
                    rnd = _count(action.get("round"), f"{epath}.round")
                    if rnd < 1 or rnd < previous_round:
                        raise ValueError(f"{epath}.round must be chronological and >= 1")
                    previous_round = rnd
                    if kind in ("attack", "breath", "searing-ignition"):
                        if "strengthBasedAttacks" in action and type(action["strengthBasedAttacks"]) is not bool:
                            raise ValueError(f"{epath}.strengthBasedAttacks must be boolean")
                    if kind == "attack":
                        hits = action.get("precisionHits", [])
                        if not isinstance(hits, list):
                            raise ValueError(f"{epath}.precisionHits must be an array")
                        seen_precision = set()
                        for hi, hit in enumerate(hits):
                            hpath = f"{epath}.precisionHits[{hi}]"
                            if not isinstance(hit, dict) or set(hit) != {
                                    "attackIndex", "basis", "weaponProficient",
                                    "withinOrdinaryRange", "targetImmune"}:
                                raise ValueError(f"{hpath} must declare all precision requirements")
                            idx = _count(hit["attackIndex"], f"{hpath}.attackIndex")
                            if idx in seen_precision:
                                raise ValueError(f"{hpath}.attackIndex duplicated")
                            seen_precision.add(idx)
                            if hit["basis"] not in ("undetected", "unanticipated", "precision-vulnerability"):
                                raise ValueError(f"{hpath}.basis is not an authored qualification")
                            for flag in ("weaponProficient", "withinOrdinaryRange", "targetImmune"):
                                if type(hit[flag]) is not bool:
                                    raise ValueError(f"{hpath}.{flag} must be boolean")
                    if kind == "class-bonus-action":
                        _nonempty(action.get("featureId"), f"{epath}.featureId")
                        if action.get("option") not in ("battle-rage", "dash", "disengage", "hide", "steady-aim"):
                            raise ValueError(f"{epath}.option must be a supported class Bonus Action")
                        required_flag = {"battle-rage": "wearingHeavyArmor",
                                         "steady-aim": "movedThisTurn"}.get(action["option"])
                        if required_flag:
                            if type(action.get(required_flag)) is not bool:
                                raise ValueError(f"{epath}.{required_flag} must be boolean")
                            other = "movedThisTurn" if required_flag == "wearingHeavyArmor" else "wearingHeavyArmor"
                            if other in action:
                                raise ValueError(f"{epath}.{other} is irrelevant to option")
                        elif "movedThisTurn" in action or "wearingHeavyArmor" in action:
                            raise ValueError(f"{epath} has irrelevant class Bonus Action flag")
                        if option := action.get("option"):
                            if option == "battle-rage":
                                if type(action.get("rageEndedBeforeActivation")) is not bool:
                                    raise ValueError(f"{epath}.rageEndedBeforeActivation must be boolean")
                            elif "rageEndedBeforeActivation" in action:
                                raise ValueError(f"{epath}.rageEndedBeforeActivation is irrelevant")
                    if kind == "origin-bonus-action":
                        _nonempty(action.get("traitId"), f"{epath}.traitId")
                        for flag in ("rockAvailable", "targetIsCreature"):
                            if type(action.get(flag)) is not bool:
                                raise ValueError(f"{epath}.{flag} must be boolean")
                        _number(action.get("distanceFeet"), f"{epath}.distanceFeet")
                        _probability(action.get("hitChance"), f"{epath}.hitChance")
                        _probability(action.get("saveFailure"), f"{epath}.saveFailure")
                    if kind in ("breath", "searing-ignition"):
                        _nonempty(action.get("traitId"), f"{epath}.traitId")
                        _count(action.get("targets"), f"{epath}.targets")
                        _probability(action.get("saveFailure"), f"{epath}.saveFailure")
                        _count(action.get("replacedAttackIndex"),
                               f"{epath}.replacedAttackIndex")
                        if "spellAlternative" in action:
                            alt = action["spellAlternative"]
                            if not isinstance(alt, dict) or set(alt) != {"slotLevel", "expectedDamage"}:
                                raise ValueError(f"{epath}.spellAlternative must specify slot level and damage")
                            _count(alt.get("slotLevel"), f"{epath}.spellAlternative.slotLevel")
                            if alt["slotLevel"] < 1:
                                raise ValueError(f"{epath}.spellAlternative.slotLevel must be >= 1")
                            _number(alt.get("expectedDamage"),
                                    f"{epath}.spellAlternative.expectedDamage")
                    elif kind == "spell":
                        _count(action.get("slotLevel"), f"{epath}.slotLevel")
                        if action["slotLevel"] < 1:
                            raise ValueError(f"{epath}.slotLevel must be >= 1")
                        _nonempty(action.get("spellId"), f"{epath}.spellId")
                        _number(action.get("expectedDamage"), f"{epath}.expectedDamage")
                    elif kind in ("class-reaction", "origin-reaction"):
                        key = "featureId" if kind == "class-reaction" else "traitId"
                        _nonempty(action.get(key), f"{epath}.{key}")
                        _number(action.get("damageAmount"), f"{epath}.damageAmount")
                        _nonempty(action.get("damageType"), f"{epath}.damageType")
                        for flag in ("attackerVisible", "isAttackHit"):
                            if type(action.get(flag)) is not bool:
                                raise ValueError(f"{epath}.{flag} must be boolean")
                if event["id"] in seen_ids:
                    raise ValueError(f"Duplicate nativeClassActionPlans ID: {event['id']}")
                seen_ids.add(event["id"])
            elif field == "jointCombatPlans":
                allowed = {"id", "archetype", "events"}
                if set(event) - allowed:
                    raise ValueError(f"Unknown {location} fields: {sorted(set(event) - allowed)}")
                _nonempty(event.get("id"), f"{location}.id")
                if event.get("archetype") not in (
                        "weapon-martial", "bonus-action-skirmisher", "prepared-caster"):
                    raise ValueError(f"{location}.archetype is unsupported")
                if not isinstance(event.get("events"), list):
                    raise ValueError(f"{location}.events must be an array")
                seen_plan_events = set()
                last_round = 0
                for position, planned in enumerate(event["events"]):
                    epath = f"{location}.events[{position}]"
                    if not isinstance(planned, dict) or set(planned) - {
                            "type", "opportunity", "traitId",
                            "reactionAlternativeExpectedDamage"}:
                        raise ValueError(f"{epath} has unknown fields or is not an object")
                    kind = planned.get("type")
                    if kind not in ("strike", "tactical", "reaction"):
                        raise ValueError(f"{epath}.type is unsupported")
                    opportunity = planned.get("opportunity")
                    if not isinstance(opportunity, dict):
                        raise ValueError(f"{epath}.opportunity must be an object")
                    if kind != "reaction" and ("traitId" in planned or
                                               "reactionAlternativeExpectedDamage" in planned):
                        raise ValueError(f"{epath} includes reaction-only fields")
                    if kind == "reaction":
                        _nonempty(planned.get("traitId"), f"{epath}.traitId")
                        _number(planned.get("reactionAlternativeExpectedDamage"),
                                f"{epath}.reactionAlternativeExpectedDamage")
                    destination = {"strike": "strikeOpportunities",
                                   "tactical": "tacticalOpportunities",
                                   "reaction": "reactionDamageEvents"}[kind]
                    # Reuse the authoritative fixture validator for each
                    # opportunity. No special permissive joint-only path.
                    validate_scenario({destination: [opportunity]})
                    unique_id = opportunity["id"]
                    if unique_id in seen_plan_events:
                        raise ValueError(f"Duplicate joint plan event ID: {unique_id}")
                    seen_plan_events.add(unique_id)
                    when = opportunity["turn" if kind == "strike" else "round"]
                    if when < last_round:
                        raise ValueError(f"{epath} is not in chronological round order")
                    last_round = when
                    if kind == "tactical":
                        if opportunity["kind"] == "searing-ignition":
                            if "attackReplacementExpectedDamage" not in opportunity:
                                raise ValueError(f"{epath} needs attack action opportunity cost")
                        elif "bonusActionAlternativeExpectedDamage" not in opportunity:
                            raise ValueError(f"{epath} needs bonus action opportunity cost")
                if event["id"] in seen_ids:
                    raise ValueError(f"Duplicate jointCombatPlans ID: {event['id']}")
                seen_ids.add(event["id"])
            elif field == "tacticalOpportunities":
                allowed = {"id", "traitId", "round", "kind", "actionAvailable",
                           "rockAvailable", "distanceFeet", "hitChance",
                           "targetIsCreature", "failureProbability",
                           "normalAttackRollsBeforeRecovery", "baseHitChance",
                           "bonusActionAlternativeExpectedDamage", "enemyTargets",
                           "enemyNormalAttackRollsBeforeRecovery",
                           "attackReplacementExpectedDamage", "enemyStartsWithinAura",
                           "attacksTargetingYouBeforeExpiry", "targetSpeedFeet",
                           "incomingNonattackPackets", "auraUninterrupted"}
                extra = set(event) - allowed
                if extra:
                    raise ValueError(f"Unknown {location} fields: {sorted(extra)}")
                for name in ("id", "traitId", "kind"):
                    _nonempty(event.get(name), f"{location}.{name}")
                kind = event["kind"]
                if kind not in ("stone-throw", "searing-ignition", "maelstrom-aura"):
                    raise ValueError(f"{location}.kind is unsupported")
                _count(event.get("round"), f"{location}.round")
                if event["round"] < 1:
                    raise ValueError(f"{location}.round must be >= 1")
                if type(event.get("actionAvailable")) is not bool:
                    raise ValueError(f"{location}.actionAvailable must be boolean")
                common = {"id", "traitId", "round", "kind", "actionAvailable"}
                kind_fields = {
                    "stone-throw": {"rockAvailable", "distanceFeet", "hitChance",
                                    "targetIsCreature", "failureProbability",
                                    "normalAttackRollsBeforeRecovery", "baseHitChance",
                                    "bonusActionAlternativeExpectedDamage"},
                    "searing-ignition": {"enemyTargets", "failureProbability",
                                         "enemyNormalAttackRollsBeforeRecovery", "baseHitChance",
                                         "attackReplacementExpectedDamage"},
                    "maelstrom-aura": {"enemyStartsWithinAura", "failureProbability",
                                       "attacksTargetingYouBeforeExpiry", "baseHitChance",
                                       "targetSpeedFeet", "incomingNonattackPackets",
                                       "auraUninterrupted", "bonusActionAlternativeExpectedDamage"},
                }[kind]
                if set(event) - common - kind_fields:
                    raise ValueError(f"{location}: fields do not match {kind}")
                if kind == "stone-throw":
                    for flag in ("rockAvailable", "targetIsCreature"):
                        if type(event.get(flag)) is not bool:
                            raise ValueError(f"{location}.{flag} must be boolean")
                    _number(event.get("distanceFeet"), f"{location}.distanceFeet")
                    _probability(event.get("hitChance"), f"{location}.hitChance")
                    _probability(event.get("failureProbability"), f"{location}.failureProbability")
                    if "normalAttackRollsBeforeRecovery" in event:
                        _count(event["normalAttackRollsBeforeRecovery"],
                               f"{location}.normalAttackRollsBeforeRecovery")
                        if "baseHitChance" not in event:
                            raise ValueError(f"{location}.baseHitChance required for Prone attacks")
                        if not event["targetIsCreature"] and event["normalAttackRollsBeforeRecovery"]:
                            raise ValueError(f"{location}: noncreature target can not attack while Prone")
                elif kind == "searing-ignition":
                    _count(event.get("enemyTargets"), f"{location}.enemyTargets")
                    _probability(event.get("failureProbability"), f"{location}.failureProbability")
                    if "enemyNormalAttackRollsBeforeRecovery" in event:
                        _count(event["enemyNormalAttackRollsBeforeRecovery"],
                               f"{location}.enemyNormalAttackRollsBeforeRecovery")
                        if "baseHitChance" not in event:
                            raise ValueError(f"{location}.baseHitChance required for Blinded attacks")
                        if event["enemyTargets"] == 0 and event["enemyNormalAttackRollsBeforeRecovery"]:
                            raise ValueError(f"{location}: no enemy targets for Blinded attacks")
                else:
                    _count(event.get("enemyStartsWithinAura"),
                           f"{location}.enemyStartsWithinAura")
                    _count(event.get("attacksTargetingYouBeforeExpiry"),
                           f"{location}.attacksTargetingYouBeforeExpiry")
                    _probability(event.get("failureProbability"), f"{location}.failureProbability")
                    _number(event.get("targetSpeedFeet"), f"{location}.targetSpeedFeet")
                    if event["attacksTargetingYouBeforeExpiry"] and "baseHitChance" not in event:
                        raise ValueError(f"{location}.baseHitChance required for attack rolls")
                    if type(event.get("auraUninterrupted")) is not bool:
                        raise ValueError(f"{location}.auraUninterrupted must be boolean")
                    packets = event.get("incomingNonattackPackets")
                    if not isinstance(packets, list):
                        raise ValueError(f"{location}.incomingNonattackPackets must be an array")
                    for n, packet in enumerate(packets):
                        if not isinstance(packet, dict) or set(packet) != {"type", "amount"}:
                            raise ValueError(f"{location}.incomingNonattackPackets[{n}] malformed")
                        _nonempty(packet["type"], f"{location}.packet[{n}].type")
                        _number(packet["amount"], f"{location}.packet[{n}].amount")
                for numeric in ("baseHitChance",):
                    if numeric in event:
                        _probability(event[numeric], f"{location}.{numeric}")
                for numeric in ("attackReplacementExpectedDamage",
                                "bonusActionAlternativeExpectedDamage"):
                    if numeric in event:
                        _number(event[numeric], f"{location}.{numeric}")
            elif field == "tremorsenseContacts":
                _number(event.get("distanceFeet"), f"{location}.distanceFeet")
                for key in ("sensorTouches", "targetTouches"):
                    if key in event:
                        _nonempty(event[key], f"{location}.{key}")
                for key in ("anticipated", "connected"):
                    if key in event and not isinstance(event[key], bool):
                        raise ValueError(f"{location}.{key} must be boolean")
            elif field == "mobilityChallenges":
                identifier = _nonempty(event.get("id"), f"{location}.id")
                if identifier in seen_ids:
                    raise ValueError(f"Duplicate mobility challenge ID: {identifier}")
                seen_ids.add(identifier)
                if event.get("mode") not in MOVEMENT_MODES:
                    raise ValueError(f"{location}.mode is not supported")
                _number(event.get("distanceFeet"), f"{location}.distanceFeet", 0.000001)
                _number(event.get("startMinute", 0), f"{location}.startMinute")
                _number(event.get("durationMinutes", 0.1),
                        f"{location}.durationMinutes", 0.000001)
                _count(event.get("maxMoves", 1), f"{location}.maxMoves")
                if event.get("maxMoves", 1) == 0:
                    raise ValueError(f"{location}.maxMoves must be >= 1")
                if "material" in event:
                    _nonempty(event["material"], f"{location}.material")
                for key in ("requiresHover", "requiresWholeParty"):
                    if key in event and not isinstance(event[key], bool):
                        raise ValueError(f"{location}.{key} must be boolean")
                if event["mode"] == "burrow" and "material" not in event:
                    raise ValueError(f"{location}.material required for burrow")
            if field in ("saveEvents", "reactionDamageEvents", "spellDemands",
                         "controlOpportunities", "strikeOpportunities", "tacticalOpportunities"):
                identifier = event["id"]
                if identifier in seen_ids:
                    raise ValueError(f"Duplicate {field} event ID: {identifier}")
                seen_ids.add(identifier)


def load_native(native: Path) -> dict:
    """Resolve canonical definitions without relying on source-local traits."""
    sources, traits = {}, {}
    for path in sorted((native / "species").rglob("*.json")):
        record = json.loads(path.read_text(encoding="utf-8"))
        if record.get("kind") not in ("species", "ancestry"):
            continue
        identifier = record["id"]
        if identifier in sources:
            raise ValueError(f"Duplicate source ID: {identifier}")
        sources[identifier] = record
    for path in sorted((native / "traits").rglob("*.json")):
        entries = json.loads(path.read_text(encoding="utf-8"))
        for definition in entries if isinstance(entries, list) else [entries]:
            identifier = definition["id"]
            if identifier in traits:
                raise ValueError(f"Duplicate trait ID: {identifier}")
            if definition.get("kind") != "origin-trait":
                raise ValueError(f"Unexpected trait category: {identifier}")
            traits[identifier] = definition["trait"]
    conditions = json.loads((native / "conditions.json").read_text(encoding="utf-8"))
    if isinstance(conditions, dict):
        entries = conditions.get("conditions", conditions.get("entries", []))
    else:
        entries = conditions
    if not isinstance(entries, list) or not entries:
        raise ValueError("Native conditions catalog is empty or has an unsupported shape")
    classes, class_features = {}, {}
    for path in sorted((native / "classes" / "base").rglob("*.json")):
        record = json.loads(path.read_text(encoding="utf-8"))
        if record.get("kind") != "class":
            # Class variants or non-class files are outside this pure-class pass.
            continue
        identifier = record["id"]
        if identifier in classes:
            raise ValueError(f"Duplicate native class: {identifier}")
        classes[identifier] = record
    for path in sorted((native / "features" / "classes").rglob("*.json")):
        record = json.loads(path.read_text(encoding="utf-8"))
        if record.get("kind") != "classFeature":
            continue
        identifier = record["id"]
        if identifier in class_features:
            raise ValueError(f"Duplicate native class feature: {identifier}")
        class_features[identifier] = record
    return {"sources": sources, "traits": traits,
            "conditions": {v["id"]: v for v in entries},
            "classes": classes, "classFeatures": class_features}


def terminal_sources(catalog: dict, include_deferred: bool = False) -> list[str]:
    parents = {r.get("parent") for r in catalog["sources"].values()}
    return sorted(identifier for identifier, record in catalog["sources"].items()
                  if identifier not in parents and
                  (include_deferred or record.get("selectable", True)))


def reconstruct_preset(catalog: dict, selected_id: str) -> dict:
    """Reconstruct a *source* preset, not a fully authorized individual Origin."""
    sources = catalog["sources"]
    seen, lineage = set(), []
    cursor = selected_id
    while cursor:
        if cursor in seen:
            raise ValueError(f"Cyclic source ancestry: {cursor}")
        seen.add(cursor)
        if cursor not in sources:
            raise ValueError(f"Missing parent source: {cursor}")
        record = sources[cursor]
        lineage.append(record)
        cursor = record.get("parent")
    lineage.reverse()
    intrinsics, occurrences, effective_traits = {}, [], {}
    for record in lineage:
        for key, value in record.items():
            if key not in SOURCE_METADATA:
                # Direct descendant replacement is valid within *this* authored
                # source lineage. Cross-contributor replacement is a different rule.
                intrinsics[key] = copy.deepcopy(value)
        for reference in record.get("traits", []):
            if "traitId" not in reference:
                raise ValueError(f"Unnormalized trait in {record['id']}")
            identifier = reference["traitId"]
            if identifier not in catalog["traits"]:
                raise ValueError(f"Missing canonical definition: {identifier}")
            occurrences.append({"traitId": identifier, "sourceId": record["id"]})
            effective_traits.setdefault(identifier, copy.deepcopy(catalog["traits"][identifier]))
    return {"selectedSourceId": selected_id,
            "lineage": [r["id"] for r in lineage],
            "intrinsics": intrinsics, "occurrences": occurrences,
            "traits": effective_traits}


def proficiency_bonus(level: int) -> int:
    if level in PB_BY_LEVEL:
        return PB_BY_LEVEL[level]
    if 1 <= level <= 20:
        return min(6, 2 + (level - 1) // 4)
    raise ValueError("Expected character level 1..20")


def dice_mean(expression: str) -> float:
    match = re.fullmatch(r"(\d+)d(\d+)", expression)
    if not match:
        raise ValueError(f"Unsupported dice expression: {expression}")
    return int(match[1]) * (int(match[2]) + 1) / 2


def unlocked(trait: dict, level: int) -> bool:
    return level >= trait.get("requiredCharacterLevel", trait.get("level", 1))


def roll_change(base: float, modifier: int) -> float:
    """Interior d20 check with clipping at possible success bounds."""
    return min(1.0, max(0.0, base + modifier / 20)) - base


def _resistance(trait: dict, intrinsics: dict) -> dict[str, float]:
    resistance = trait.get("damageResistance", {})
    if not isinstance(resistance, dict):
        return {}
    if "percent" in resistance:
        damage_type = resistance.get("damageType")
        if not damage_type and resistance.get("damageTypeFrom") == "selected-dragonborn-leaf":
            damage_type = intrinsics.get("damageType")
        return {damage_type: resistance["percent"] / 100} if damage_type else {}
    return {type_: percent / 100 for type_, percent in resistance.items()
            if isinstance(percent, (float, int))}


def _save_advantage_sources(traits: dict, event: dict, level: int) -> tuple[list[str], list[str]]:
    """Match only authored saving-throw advantage categories and their scope.

    Unknown savingThrows shapes are explicitly reported rather than considered
    inapplicable. Advantage from multiple sources is not stacked.
    """
    matching, unsupported = [], []
    for identifier, trait in traits.items():
        if not unlocked(trait, level) or "savingThrows" not in trait:
            continue
        spec = trait["savingThrows"]
        if not isinstance(spec, dict) or set(spec) - {
                "advantageAgainst", "advantageAbilities", "scope"}:
            unsupported.append(identifier)
            continue
        if not isinstance(spec.get("advantageAgainst", []), list) or not isinstance(
                spec.get("advantageAbilities", []), list):
            unsupported.append(identifier)
            continue
        if spec.get("scope") not in (None, "magic-only"):
            unsupported.append(identifier)
            continue
        tags = set(event.get("tags", []))
        against = bool(tags.intersection(spec.get("advantageAgainst", [])))
        ability = (event["ability"] in spec.get("advantageAbilities", [])
                   and (spec.get("scope") is None or event.get("magical", False)))
        if against or ability:
            matching.append(identifier)
    return matching, unsupported


def saving_throw_outcomes(traits: dict, level: int, scenario: dict,
                          resistances: dict[str, float]) -> tuple[dict[str, float], list[dict], list[dict]]:
    """Compare exactly the same event under baseline versus expressed grants.

    damageOnSuccess and damageOnFailure are already the conditional amounts.
    The damage-resistance calculation occurs once, after save expectations;
    the independent incomingDamage packets are not reused here.
    """
    values = {"saving_throw_success_delta": 0.0,
              "saving_throw_damage_prevented": 0.0,
              "saving_throw_conditions_avoided": 0.0}
    trace, issues = [], []
    for event in scenario.get("saveEvents", []):
        matches, unsupported = _save_advantage_sources(traits, event, level)
        for identifier in unsupported:
            issue = {"traitId": identifier, "reason": "unsupported-saving-throw-shape"}
            if issue not in issues:
                issues.append(issue)
        p = event["baseSuccess"]
        effective = 1 - (1 - p) ** 2 if matches else p
        values["saving_throw_success_delta"] += effective - p
        condition = event.get("conditionOnFailure")
        if condition:
            values["saving_throw_conditions_avoided"] += effective - p
        base_damage = actual_damage = 0.0
        if "damageOnFailure" in event:
            base_damage = (p * event["damageOnSuccess"] +
                           (1 - p) * event["damageOnFailure"])
            retained = 1 - resistances.get(event["damageType"], 0)
            actual_damage = (effective * event["damageOnSuccess"] +
                             (1 - effective) * event["damageOnFailure"]) * retained
            values["saving_throw_damage_prevented"] += base_damage - actual_damage
        trace.append({"id": event["id"], "advantageSources": matches,
                      "baselineSuccessChance": p, "effectiveSuccessChance": effective,
                      "expectedBaselineDamage": base_damage,
                      "expectedDamageAfterOrigin": actual_damage,
                      "conditionOnFailure": condition})
    return values, trace, issues



def spell_demand_outcomes(traits: dict, level: int, pb: int,
                          scenario: dict) -> tuple[dict, list[dict], list[dict], set[str]]:
    """Match authored fixed-spell entitlements to explicitly eligible demands.

    This does not execute a spell, infer its action, select a spell-list choice,
    add class slots, or assign effectiveness to a successful cast. Distinct
    sources of the *same* limited spell require a separate stacking decision.
    """
    values = {"innate_fixed_at_will_spell_entitlements": 0,
              "innate_fixed_free_cast_capacity": 0,
              "spell_free_cast_demands_matched": 0,
              "spell_at_will_demands_matched": 0}
    by_spell = defaultdict(list)
    unresolved_lists = []
    modeled = set()
    issues = []
    entitlements = []
    for identifier, trait in traits.items():
        if not unlocked(trait, level):
            continue
        if "spellList" in trait:
            unresolved_lists.append(identifier)
        spell = trait.get("spell")
        if not isinstance(spell, str) or not spell.strip():
            continue
        at_will = trait.get("atWill") is True
        uses = trait.get("freeUses")
        charges = pb if uses == "proficiency-bonus" else uses
        if at_will and uses is not None:
            issues.append({"traitId": identifier, "reason": "both-at-will-and-limited-casts"})
            continue
        if not at_will and (isinstance(charges, bool) or
                            not isinstance(charges, int) or charges < 0 or
                            trait.get("recharge") != "long-rest"):
            continue
        entitlements.append({"traitId": identifier, "spell": spell,
                             "atWill": at_will, "freeUsesPerLongRest": None if at_will else charges,
                             "canUseSpellSlots": trait.get("slotCasting", False) is True,
                             "freeCastSpellLevel": trait.get("freeCastSpellLevel")})
        by_spell[spell].append({"traitId": identifier, "atWill": at_will,
                                "remaining": None if at_will else charges})
        modeled.add(identifier)
        if at_will:
            values["innate_fixed_at_will_spell_entitlements"] += 1
        else:
            values["innate_fixed_free_cast_capacity"] += charges
    ambiguous = {name for name, entries in by_spell.items()
                 if sum(not item["atWill"] for item in entries) > 1}
    for spell in sorted(ambiguous):
        issues.append({"spell": spell, "reason": "duplicate-fixed-spell-free-pools-unresolved",
                       "traitIds": [item["traitId"] for item in by_spell[spell]]})
    trace = []
    for event in scenario.get("spellDemands", []):
        row = {"id": event["id"], "spell": event["spell"], "sourceTraitId": None,
               "status": "unresolved-no-authored-fixed-spell"}
        candidates = by_spell.get(event["spell"], [])
        if event.get("eligible", True) is False:
            row["status"] = "fixture-ineligible"
        elif event["spell"] in ambiguous:
            row["status"] = "unresolved-duplicate-free-use-pools"
        elif candidates:
            at_will = next((v for v in candidates if v["atWill"]), None)
            available = at_will or next((v for v in candidates if v["remaining"] > 0), None)
            if available:
                row["sourceTraitId"] = available["traitId"]
                row["status"] = "at-will-access" if available["atWill"] else "free-cast-access"
                if available["atWill"]:
                    values["spell_at_will_demands_matched"] += 1
                else:
                    values["spell_free_cast_demands_matched"] += 1
                    available["remaining"] -= 1
            else:
                row["status"] = "free-use-exhausted"
        elif unresolved_lists:
            row["status"] = "unresolved-spell-list-choice"
        trace.append(row)
    return values, trace, issues, modeled, entitlements


def _control_window_effects(failure: dict, catalog: dict, event: dict,
                            metrics: dict, row: dict) -> None:
    """Conditional action opportunities, never unconditional turns lost.

    Exposures are counts of *different targets* with relevant opportunities
    before the condition's end, except for declared normal attack rolls. One
    target may separately lose an Action, Bonus Action and Reaction, but those
    slots are not interchangeable. No spell effects or DM behavior are inferred.
    """
    condition = failure.get("applyCondition")
    if not condition:
        return
    definition = catalog["conditions"].get(condition, {})
    effects = definition.get("effects", [])
    # A condition without a recognized authored effect cannot deny actions.
    restrictions = set()
    ends_concentration = False
    for effect in effects:
        if effect.get("type") == "action-restriction":
            restrictions.update(effect.get("cannotTake", []))
        if effect.get("type") == "concentration" and effect.get("operation") == "end":
            ends_concentration = True
    duration_verified = failure.get("endsAt") == "start-of-your-next-turn"
    row["conditionWindowKnown"] = duration_verified
    for side in ("enemy", "ally"):
        for suffix, restriction, metric_suffix in (
                ("ActionSlots", "action", "action_slots_denied"),
                ("BonusActionSlots", "bonus-action", "bonus_action_slots_denied"),
                ("ReactionSlots", "reaction", "reaction_slots_denied"),
                ("ConcentratingTargets", "concentration", "concentrations_ended")):
            field = f"{side}{suffix}"
            if field not in event:
                continue
            authorized = (ends_concentration if restriction == "concentration"
                          else restriction in restrictions)
            if not duration_verified or not authorized:
                row.setdefault("unresolvedWindowExposures", []).append(field)
                continue
            expected = event[field] * event["failureProbability"]
            metric = f"control_expected_{side}_{metric_suffix}"
            metrics[metric] = metrics.get(metric, 0) + expected
            row.setdefault("expectedWindowEffects", {})[field] = expected

    # Prone does not erase a turn. Only rolls explicitly assumed to occur
    # before the target stands, and to have no other roll modes, are modeled.
    has_attack_disadvantage = any(
        effect.get("type") == "roll-mode" and
        effect.get("appliesTo") == "attack-roll" and
        effect.get("mode") == "disadvantage" and
        effect.get("subject") == "attacks-by-affected-creature"
        for effect in effects)
    if condition != "condition.prone":
        for side in ("enemy", "ally"):
            field = f"{side}{CONTROL_WINDOW_PRONE}"
            if field in event:
                row.setdefault("unresolvedWindowExposures", []).append(field)
        return
    for side in ("enemy", "ally"):
        field = f"{side}{CONTROL_WINDOW_PRONE}"
        if field not in event:
            continue
        if not has_attack_disadvantage:
            row.setdefault("unresolvedWindowExposures", []).append(field)
            continue
        p = event["baseHitChance"]
        expected = event[field] * event["failureProbability"] * p * (1 - p)
        metric = f"control_expected_{side}_normal_attack_hits_prevented_by_prone"
        metrics[metric] = metrics.get(metric, 0) + expected
        row.setdefault("expectedWindowEffects", {})[field] = expected


def metallic_control_outcomes(traits: dict, catalog: dict, level: int,
                              scenario: dict) -> tuple[dict, list[dict], list[dict], set[str]]:
    """Measure expected *applications* for a narrowly authored control area.

    Event geometry, enemy/ally presence, and save failure are fixture inputs.
    Processing events in order is a declared first-eligible-use policy; the
    scheduled windows only count potential action slots *if* a fixture
    explicitly supplies their availability and the native condition supports
    the claimed consequence. No actual player decisions are inferred.
    """
    metrics = {"control_attack_replacements": 0}
    trace = []
    issues = []
    modeled = set()
    abilities = []
    for identifier, trait in traits.items():
        if not unlocked(trait, level):
            continue
        act, pool, area = trait.get("activation"), trait.get("uses"), trait.get("area")
        options = trait.get("options")
        if not (isinstance(act, dict) and act.get("action") == "attack" and
                act.get("replaces") == "one-attack" and isinstance(options, list) and
                isinstance(pool, dict) and pool.get("recharge") == "long-rest" and
                isinstance(area, dict) and area.get("shape") == "cone" and
                area.get("affectsAllies") is True):
            continue
        if type(pool.get("value")) is not int or pool["value"] < 0:
            issues.append({"traitId": identifier, "reason": "unresolved-control-use-pool"})
            continue
        recognized_options = {}
        for opt in options:
            if not isinstance(opt, dict) or not isinstance(opt.get("id"), str):
                continue
            save = opt.get("savingThrow")
            failure = opt.get("onFailure")
            if (not isinstance(save, dict) or
                    save.get("onSuccess") != "no-effect" or
                    not isinstance(failure, dict) or
                    set(opt) - {"id", "name", "savingThrow", "onFailure"} or
                    set(save) - {"ability", "dc", "onSuccess"} or
                    set(failure) - {"applyCondition", "endsAt", "pushFeet",
                                    "direction", "forcedMovement"} or
                    not isinstance(save.get("ability"), str) or
                    not isinstance(save.get("dc"), str)):
                continue
            condition = failure.get("applyCondition")
            if condition is not None and condition not in catalog["conditions"]:
                continue
            if condition is None and "pushFeet" not in failure:
                continue
            if "pushFeet" in failure and (type(failure["pushFeet"]) is not int or
                                          failure["pushFeet"] < 0):
                continue
            recognized_options[opt["id"]] = failure
        if len(recognized_options) != len(options):
            issues.append({"traitId": identifier, "reason": "partially-unresolved-control-options"})
            continue
        abilities.append({"traitId": identifier, "remaining": pool["value"],
                          "shape": area["shape"], "options": recognized_options})
        modeled.add(identifier)
    for event in scenario.get("controlOpportunities", []):
        row = {"id": event["id"], "option": event["option"],
               "selectedTraitId": None, "status": "no-matching-authored-control"}
        for ability in abilities:
            if (ability["shape"] != event["areaShape"] or
                    event["option"] not in ability["options"]):
                continue
            if not event["enemyTargets"] and not event["allyTargets"]:
                row["status"] = "no-targets"
                break
            if ability["remaining"] <= 0:
                row["status"] = "control-use-exhausted"
                break
            ability["remaining"] -= 1
            row["status"] = "applied-to-fixture-targets"
            row["selectedTraitId"] = ability["traitId"]
            row["expectedEnemyFailedSaves"] = event["enemyTargets"] * event["failureProbability"]
            row["expectedAllyFailedSaves"] = event["allyTargets"] * event["failureProbability"]
            failure = ability["options"][event["option"]]
            metrics["control_attack_replacements"] += 1
            attack_alternative = event.get(
                "attackReplacementExpectedDamage",
                scenario.get("attackReplacementExpectedDamage",
                             DEFAULT_SCENARIO["attackReplacementExpectedDamage"]))
            row["replacementAttackExpectedDamageForgone"] = attack_alternative
            metrics["control_replaced_attack_expected_damage_forgone"] = (
                metrics.get("control_replaced_attack_expected_damage_forgone", 0)
                + attack_alternative)
            _control_window_effects(failure, catalog, event, metrics, row)
            if "applyCondition" in failure:
                condition = failure["applyCondition"].split(".")[-1]
                for name, count in (("enemy", row["expectedEnemyFailedSaves"]),
                                    ("ally", row["expectedAllyFailedSaves"])):
                    metric = f"control_expected_{name}_{condition}_applications"
                    metrics[metric] = metrics.get(metric, 0) + count
            if "pushFeet" in failure:
                for name, count in (("enemy", row["expectedEnemyFailedSaves"]),
                                    ("ally", row["expectedAllyFailedSaves"])):
                    metric = f"control_expected_{name}_forced_movement_feet"
                    metrics[metric] = metrics.get(metric, 0) + count * failure["pushFeet"]
            break
        trace.append(row)
    return metrics, trace, issues, modeled


def on_hit_control_outcomes(traits: dict, catalog: dict, level: int,
                            scenario: dict) -> tuple[dict, list[dict], list[dict], set[str], dict]:
    """Replay deliberate confirmed-hit activations without inventing hit rates.

    An event names exactly one trait to activate; it is not an optimized attack
    rotation or permission to inherit that trait. Resources are shared by the
    authored ``sharedUsesFromTrait`` and canonical parent ``id``. Movement
    reductions are nominal speed changes, never guaranteed lost movement.
    """
    metrics = {"strike_uses_selected": 0}
    trace, issues, modeled = [], [], set()
    pool_remaining = {}
    spent = {}
    used_turns = set()
    size_rank = {size: n for n, size in enumerate(
        ("tiny", "small", "medium", "large", "huge", "gargantuan"))}
    # Short native IDs locate the resource owner, but duplicated IDs in a
    # mixed construction are ambiguous; silently choosing the last one would
    # hide a potential shared-pool or manifestation conflict.
    owners_by_short_id = defaultdict(list)
    for identifier, trait in traits.items():
        if isinstance(trait.get("id"), str):
            owners_by_short_id[trait["id"]].append(identifier)

    for event in scenario.get("strikeOpportunities", []):
        trait_id = event["traitId"]
        row = {"id": event["id"], "traitId": trait_id,
               "mode": event.get("mode", "base"), "status": "trait-not-expressed"}
        trait = traits.get(trait_id)
        if trait is None:
            trace.append(row)
            continue
        if not unlocked(trait, level):
            row["status"] = "trait-not-yet-unlocked"
            trace.append(row)
            continue
        base = trait
        if row["mode"] == "alternate":
            base = trait.get("alternateStrikeMode")
            if not isinstance(base, dict):
                row["status"] = "no-authored-alternate-mode"
                trace.append(row)
                continue
        trigger = base.get("trigger", trait.get("trigger"))
        if trigger not in (
                "when-you-hit-with-an-attack-roll-and-deal-damage",
                "melee-weapon-or-thrown-weapon-attack-hit",
                "melee-weapon-or-thrown-weapon-attack-hit-and-deal-damage"):
            row["status"] = "unsupported-hit-trigger"
            issues.append({"traitId": trait_id, "reason": "unsupported-hit-trigger"})
            trace.append(row)
            continue
        row["status"] = "no-qualifying-hit"
        if not event["hit"]:
            trace.append(row)
            continue
        if trigger.endswith("and-deal-damage") and not event["dealtDamage"]:
            row["status"] = "hit-without-required-damage"
            trace.append(row)
            continue
        if trigger.startswith("melee-weapon-or-thrown-weapon") and event["delivery"] not in (
                "melee-weapon", "thrown-weapon"):
            row["status"] = "wrong-attack-delivery"
            trace.append(row)
            continue
        limit = base.get("targetMaximumSize", trait.get("targetMaximumSize"))
        if limit is not None and (
                limit not in size_rank or size_rank[event["targetSize"]] > size_rank[limit]):
            row["status"] = "target-too-large"
            trace.append(row)
            continue
        if (base.get("applyCondition") or base.get("applyConditionOnFailedSave")) and (
                not event["targetIsCreature"]):
            row["status"] = "requires-creature-target"
            trace.append(row)
            continue
        once = bool(base.get("oncePerTurn", trait.get("oncePerTurn", False)))
        turn_key = (trait_id, event["turn"])
        if once and turn_key in used_turns:
            row["status"] = "once-per-turn-exhausted"
            trace.append(row)
            continue
        pool_short_id = base.get("sharedUsesFromTrait", trait.get("sharedUsesFromTrait"))
        owner_candidates = (owners_by_short_id.get(pool_short_id, []) if pool_short_id
                            else [trait_id])
        if len(owner_candidates) != 1:
            row["status"] = "unresolved-shared-resource-owner"
            issues.append({"traitId": trait_id,
                           "reason": ("missing-shared-resource-owner" if not owner_candidates
                                      else "ambiguous-shared-resource-owner")})
            trace.append(row)
            continue
        owner = owner_candidates[0]
        if owner not in pool_remaining:
            uses = traits[owner].get("uses", {})
            if not isinstance(uses, dict) or uses.get("recharge") != "long-rest":
                row["status"] = "unsupported-resource"
                issues.append({"traitId": trait_id, "reason": "unsupported-strike-resource"})
                trace.append(row)
                continue
            count = uses.get("value")
            count = proficiency_bonus(level) if count == "proficiency-bonus" else count
            if type(count) is not int or count < 0:
                row["status"] = "unsupported-resource"
                issues.append({"traitId": trait_id, "reason": "unsupported-strike-resource"})
                trace.append(row)
                continue
            pool_remaining[owner] = count
        if pool_remaining[owner] <= 0:
            row["status"] = "shared-resource-exhausted"
            trace.append(row)
            continue
        # Recognize only the exact native field shapes being quantified.
        extra_damage = base.get("extraDamage", trait.get("extraDamage"))
        # Hill's Tumble's base mode applies Prone without a damage rider.
        # Other supported modes may include damage, control, or both.
        if extra_damage is not None and (
                not isinstance(extra_damage, dict) or not re.fullmatch(
                    r"\d+d\d+", str(extra_damage.get("dice", "")))):
            row["status"] = "unresolved-strike-effect"
            issues.append({"traitId": trait_id, "reason": "unresolved-strike-effect"})
            trace.append(row)
            continue
        if extra_damage is None and not (
                base.get("applyCondition") or base.get("applyConditionOnFailedSave") or
                base.get("speedReduction") or base.get("onFailedSave")):
            row["status"] = "unresolved-strike-effect"
            issues.append({"traitId": trait_id, "reason": "unresolved-strike-effect"})
            trace.append(row)
            continue
        failure_spec = base.get("onFailedSave", {})
        if not isinstance(failure_spec, dict) or set(failure_spec) - {
                "forcedMovementFeet", "direction", "speedSetFeet", "endsAt", "attackRollMode"}:
            row["status"] = "unresolved-strike-effect"
            issues.append({"traitId": trait_id, "reason": "unmodeled-on-failed-save-fields"})
            trace.append(row)
            continue
        pool_remaining[owner] -= 1
        spent[owner] = spent.get(owner, 0) + 1
        if once:
            used_turns.add(turn_key)
        modeled.add(trait_id)
        row.update(status="activated", poolOwnerTraitId=owner,
                   poolUsesRemaining=pool_remaining[owner])
        metrics["strike_uses_selected"] += 1
        if extra_damage is not None:
            mean_damage = dice_mean(extra_damage["dice"])
            metrics["strike_gross_extra_damage_on_confirmed_hits"] = (
                metrics.get("strike_gross_extra_damage_on_confirmed_hits", 0) + mean_damage)
            row["grossExtraDamage"] = mean_damage
            row["damageType"] = extra_damage.get("damageType", extra_damage.get("damageTypeFrom"))
        # A hit on an object can activate an extra-damage strike, but its
        # creature-only control rider must not apply to an inanimate object.
        if not event["targetIsCreature"]:
            trace.append(row)
            continue
        # An automatic Prone grant differs from the save-dependent alternative.
        condition = base.get("applyCondition")
        failure = base.get("onFailedSave", {})
        if not isinstance(failure, dict):
            failure = {}
        if base.get("applyConditionOnFailedSave"):
            condition = base["applyConditionOnFailedSave"]
            p_effect = event["failureProbability"]
        elif condition and base.get("additionalSavingThrow") is False:
            p_effect = 1.0
        elif isinstance(failure.get("savingThrow"), dict):
            p_effect = event["failureProbability"]
        else:
            p_effect = event["failureProbability"]
        if condition:
            effects = catalog.get("conditions", {}).get(condition, {}).get("effects", [])
            if condition not in catalog.get("conditions", {}):
                issues.append({"traitId": trait_id, "reason": "condition-definition-not-available"})
            else:
                name = condition.removeprefix("condition.")
                key = f"strike_expected_enemy_{name}_applications"
                metrics[key] = metrics.get(key, 0) + p_effect
                row["conditionApplicationProbability"] = p_effect
                if "normalAttackRollsBeforeRecovery" in event:
                    if any(e.get("type") == "roll-mode" and
                           e.get("appliesTo") == "attack-roll" and
                           e.get("mode") == "disadvantage" and
                           e.get("subject") == "attacks-by-affected-creature"
                           for e in effects):
                        p = event["baseHitChance"]
                        value = (p_effect * event["normalAttackRollsBeforeRecovery"] *
                                 p * (1 - p))
                        key = "strike_expected_enemy_attack_hits_prevented_by_prone"
                        metrics[key] = metrics.get(key, 0) + value
                        row["expectedEnemyAttackHitsPrevented"] = value
                    else:
                        issues.append({"traitId": trait_id,
                                       "reason": "condition-attack-effect-not-modeled"})
        forced = failure.get("forcedMovementFeet")
        if forced is not None:
            if type(forced) is int and forced >= 0:
                value = forced * event["failureProbability"]
                metrics["strike_expected_enemy_forced_movement_feet"] = (
                    metrics.get("strike_expected_enemy_forced_movement_feet", 0) + value)
                row["expectedForcedMovementFeet"] = value
            else:
                issues.append({"traitId": trait_id, "reason": "unknown-forced-movement"})
        reduction = base.get("speedReduction")
        if isinstance(reduction, dict) and "targetSpeedFeet" in event:
            if reduction.get("endsAt") != "start-of-your-next-turn":
                issues.append({"traitId": trait_id, "reason": "unknown-speed-reduction-duration"})
                trace.append(row)
                continue
            feet = reduction.get("feet")
            if type(feet) is int and feet >= 0:
                value = min(feet, event["targetSpeedFeet"])
                metrics["strike_expected_enemy_nominal_speed_reduction_feet"] = (
                    metrics.get("strike_expected_enemy_nominal_speed_reduction_feet", 0) + value)
                row["nominalSpeedReductionFeet"] = value
        if "speedSetFeet" in failure and "targetSpeedFeet" in event:
            if failure.get("endsAt") != "start-of-your-next-turn":
                issues.append({"traitId": trait_id, "reason": "unknown-speed-zero-duration"})
                trace.append(row)
                continue
            speed = failure["speedSetFeet"]
            if type(speed) is int and speed >= 0:
                value = max(0, event["targetSpeedFeet"] - speed) * event["failureProbability"]
                metrics["strike_expected_enemy_nominal_speed_reduction_feet"] = (
                    metrics.get("strike_expected_enemy_nominal_speed_reduction_feet", 0) + value)
                row["nominalSpeedReductionFeet"] = value
        if failure.get("attackRollMode") == "disadvantage" and (
                "normalAttackRollsBeforeRecovery" in event):
            if failure.get("endsAt") != "start-of-your-next-turn":
                issues.append({"traitId": trait_id, "reason": "unknown-attack-disadvantage-duration"})
                trace.append(row)
                continue
            p = event["baseHitChance"]
            value = (event["normalAttackRollsBeforeRecovery"] *
                     event["failureProbability"] * p * (1 - p))
            metrics["strike_expected_enemy_attack_hits_prevented_by_disadvantage"] = (
                metrics.get("strike_expected_enemy_attack_hits_prevented_by_disadvantage", 0) + value)
            row["expectedEnemyAttackHitsPrevented"] = value
        trace.append(row)
    return dict(metrics), trace, issues, modeled, spent


def _tactical_kind(trait: dict) -> str | None:
    """Recognize exact authored forms, not a generic rule for similarly named abilities."""
    ident = trait.get("id")
    act = trait.get("activation", {})
    uses = trait.get("uses", {})
    if not isinstance(act, dict) or not isinstance(uses, dict):
        return None
    if uses.get("value") != "proficiency-bonus" or uses.get("recharge") != "long-rest":
        return None
    if ident == "stone-throw":
        attack = trait.get("attack", {})
        return (ident if act.get("action") == "bonus-action" and
                trait.get("requiresItem") == "rock" and
                attack.get("type") == "ranged-spell-attack" and
                attack.get("rangeFeet") == 60 and
                trait.get("damage", {}).get("dice") == "1d10" and
                trait.get("savingThrow", {}).get("ability") == "strength" and
                trait.get("savingThrow", {}).get("onSuccess") == "no-prone" and
                trait.get("applyConditionOnFailedSave") == "condition.prone"
                else None)
    if ident == "searing-ignition":
        area = trait.get("area", {})
        status = trait.get("applyConditionOnFailedSave", {})
        return (ident if act.get("action") == "attack" and
                act.get("replaces") == "one-attack" and
                trait.get("oncePerTurn") is True and
                area.get("shape") == "sphere" and area.get("radiusFeet") == 15 and
                area.get("targets") == "creatures-of-your-choice" and
                trait.get("damage", {}).get("dice") == "1d8" and
                trait.get("damage", {}).get("add") == "proficiency-bonus" and
                trait.get("damage", {}).get("damageType") == "fire" and
                trait.get("savingThrow", {}).get("ability") == "dexterity" and
                trait.get("savingThrow", {}).get("onSuccess") == "half-damage-only" and
                trait.get("savingThrow", {}).get("onFailure") == "full-damage-and-condition" and
                status.get("condition") == "condition.blinded" and
                status.get("endsAt") == "start-of-your-next-turn" else None)
    if ident == "maelstrom-aura":
        area, duration = trait.get("area", {}), trait.get("duration", {})
        active = trait.get("whileActive", {})
        beginning = trait.get("startsTurnInside", {})
        resist = active.get("damageResistance", [])
        return (ident if act.get("action") == "bonus-action" and
                area.get("shape") == "aura" and area.get("radiusFeet") == 10 and
                area.get("blockedBy") == "total-cover" and
                duration.get("endsAt") == "start-of-your-next-turn" and
                duration.get("alsoEndsWhen") == "condition.incapacitated" and
                active.get("attackRollsTargetingYou") == "disadvantage" and
                isinstance(resist, list) and
                {(x.get("damageType"), x.get("percent")) for x in resist} ==
                {("lightning", 50), ("thunder", 50)} and
                beginning.get("target") == "other-creature" and
                beginning.get("onFailedSave", {}).get("speedMultiplier") == 0.5 and
                beginning.get("onFailedSave", {}).get("endsAt") == "start-of-targets-next-turn" and
                beginning.get("savingThrow", {}).get("ability") == "strength" and
                beginning.get("savingThrow", {}).get("onSuccess") == "no-speed-change" else None)
    return None


def _native_attack_disadvantage(catalog: dict, condition: str) -> bool:
    """Only claim attack consequences when the authored condition defines them."""
    return any(effect.get("type") == "roll-mode" and
               effect.get("appliesTo") == "attack-roll" and
               effect.get("mode") == "disadvantage" and
               effect.get("subject") == "attacks-by-affected-creature"
               for effect in catalog.get("conditions", {}).get(condition, {}).get("effects", []))


def tactical_action_outcomes(traits: dict, catalog: dict, level: int, pb: int,
                             scenario: dict, resistances: dict[str, float]):
    """Sequential fixture replay. One bonus action and attack replacement/round.

    Deliberately not an optimal rotation or a total utility measure. Control,
    movement, damage and opportunity costs are separate endpoints. Each event
    specifies *eligible* targets and windows; it does not infer positioning.
    """
    metrics = defaultdict(float)
    trace, issues, modeled = [], [], set()
    uses = defaultdict(int)
    occupied = set()
    for event in scenario.get("tacticalOpportunities", []):
        row = {"id": event["id"], "traitId": event["traitId"],
               "round": event["round"], "status": None}
        identifier = event["traitId"]
        trait = traits.get(identifier)
        if trait is None:
            row["status"] = "not-offered"
            trace.append(row)
            continue
        if not unlocked(trait, level):
            row["status"] = "level-locked"
            trace.append(row)
            continue
        kind = _tactical_kind(trait)
        if kind != event["kind"]:
            row["status"] = "unrecognized-authored-mechanics"
            issues.append({"traitId": identifier,
                           "reason": "tactical-definition-does-not-match-fixture"})
            trace.append(row)
            continue
        modeled.add(identifier)
        act = "bonus-action" if kind != "searing-ignition" else "attack-replacement"
        if not event["actionAvailable"]:
            row["status"] = "action-unavailable"
        elif (event["round"], act) in occupied:
            row["status"] = "action-already-spent-this-round"
        elif uses[identifier] >= pb:
            row["status"] = "resource-exhausted"
        elif kind == "stone-throw" and not event["rockAvailable"]:
            row["status"] = "missing-rock"
        elif kind == "stone-throw" and event["distanceFeet"] > trait["attack"]["rangeFeet"]:
            row["status"] = "out-of-range"
        elif kind == "maelstrom-aura" and not event["auraUninterrupted"]:
            row["status"] = "aura-interruption-unresolved"
            issues.append({"traitId": identifier, "reason": "aura-interruption-timing-unresolved"})
        else:
            row["status"] = "activated"
            uses[identifier] += 1
            occupied.add((event["round"], act))
            metrics["tactical_activated_uses"] += 1
            metrics["tactical_bonus_actions_used" if act == "bonus-action"
                    else "tactical_attack_replacements"] += 1
            if kind == "stone-throw":
                hit = event["hitChance"]
                gross = (hit * dice_mean(trait["damage"]["dice"]) *
                         scenario.get("damageTypeMultiplier", {}).get("force", 1))
                metrics["tactical_expected_gross_damage"] += gross
                row["expectedGrossDamage"] = gross
                if event["targetIsCreature"]:
                    if "condition.prone" in catalog["conditions"]:
                        application = hit * event["failureProbability"]
                        metrics["tactical_expected_enemy_prone_applications"] += application
                        row["expectedProneApplications"] = application
                        if ("normalAttackRollsBeforeRecovery" in event and
                                _native_attack_disadvantage(catalog, "condition.prone")):
                            p = event["baseHitChance"]
                            delta = application * event["normalAttackRollsBeforeRecovery"] * p*(1-p)
                            metrics["tactical_expected_enemy_attack_hits_prevented"] += delta
                    else:
                        issues.append({"traitId": identifier, "reason": "prone-condition-not-in-catalog"})
            elif kind == "searing-ignition":
                f, n = event["failureProbability"], event["enemyTargets"]
                gross = (n * (dice_mean(trait["damage"]["dice"]) + pb) *
                         (0.5 + 0.5*f) * scenario.get("damageTypeMultiplier", {}).get("fire", 1))
                net = gross - event.get("attackReplacementExpectedDamage",
                                         scenario.get("attackReplacementExpectedDamage", 5.2))
                metrics["tactical_expected_gross_damage"] += gross
                metrics["tactical_expected_net_attack_replacement_damage"] += net
                row.update(expectedGrossDamage=gross, expectedNetDamage=net)
                if "condition.blinded" in catalog["conditions"]:
                    metrics["tactical_expected_enemy_blinded_applications"] += n * f
                    if ("enemyNormalAttackRollsBeforeRecovery" in event and
                            _native_attack_disadvantage(catalog, "condition.blinded")):
                        p = event["baseHitChance"]
                        delta = f * event["enemyNormalAttackRollsBeforeRecovery"] * p*(1-p)
                        metrics["tactical_expected_enemy_attack_hits_prevented"] += delta
                else:
                    issues.append({"traitId": identifier, "reason": "blinded-condition-not-in-catalog"})
                issues.append({"traitId": identifier, "reason": "blinded-other-effects-unmodeled"})
            else:
                f = event["failureProbability"]
                p = event.get("baseHitChance")
                if p is not None:
                    hits = event["attacksTargetingYouBeforeExpiry"] * p*(1-p)
                    metrics["tactical_expected_self_attack_hits_prevented"] += hits
                else:
                    issues.append({"traitId": identifier, "reason": "aura-base-hit-chance-not-specified"})
                speed = event["targetSpeedFeet"]
                metrics["tactical_expected_enemy_nominal_speed_reduction_feet"] += (
                    event["enemyStartsWithinAura"] * f * speed * .5)
                saved = 0.0
                for packet in event["incomingNonattackPackets"]:
                    if packet["type"] in ("lightning", "thunder"):
                        saved += packet["amount"] * max(0, .5 - resistances.get(packet["type"], 0))
                metrics["tactical_expected_nonattack_damage_prevented"] += saved
                row["expectedNonattackDamagePrevented"] = saved
                issues.append({"traitId": identifier, "reason": "aura-movement-and-positioning-unmodeled"})
            if act == "bonus-action":
                if "bonusActionAlternativeExpectedDamage" in event:
                    # For Auras this is merely a competing action cost, not a
                    # logically subtractable damage from prevented hits.
                    row["alternativeBonusActionExpectedDamage"] = event["bonusActionAlternativeExpectedDamage"]
                    if kind == "stone-throw":
                        metrics["tactical_expected_net_bonus_action_damage"] += (
                            row["expectedGrossDamage"] - event["bonusActionAlternativeExpectedDamage"])
                else:
                    issues.append({"traitId": identifier,
                                   "reason": "bonus-action-opportunity-cost-unresolved"})
        trace.append(row)
    return dict(metrics), trace, issues, modeled


def _damage_reduction_expectation(dice: str, modifier: int, amount: float) -> float:
    """Exact expectation of capped single-die reduction, not min(mean, hit)."""
    match = re.fullmatch(r"1d(\d+)", dice)
    if not match or int(match[1]) <= 0:
        raise ValueError("Unsupported reduction dice")
    faces = int(match[1])
    return sum(min(amount, max(0, face + modifier))
               for face in range(1, faces + 1)) / faces


def _reaction_defenses(traits: dict, level: int, pb: int) -> list[dict]:
    """Recognize narrowly specified native defensive reactions only."""
    results = []
    for identifier, trait in sorted(traits.items()):
        if not unlocked(trait, level):
            continue
        activation = trait.get("activation")
        uses = trait.get("uses")
        if not isinstance(activation, dict) or activation.get("action") != "reaction":
            continue
        if not isinstance(uses, dict) or uses.get("recharge") != "long-rest":
            continue
        value = uses.get("value")
        charges = pb if value == "proficiency-bonus" else value
        if isinstance(charges, bool) or not isinstance(charges, int) or charges < 0:
            continue
        reduction = trait.get("damageReduction")
        resist = trait.get("damageResistance")
        if (activation.get("trigger") == "when-you-take-damage" and
                isinstance(reduction, dict) and reduction.get("appliesTo") == "triggering-damage" and
                re.fullmatch(r"1d\d+", str(reduction.get("dice"))) and
                reduction.get("addAbilityModifier") in (None, "constitution")):
            results.append({"traitId": identifier, "kind": "damage-reduction",
                            "charges": charges, "dice": reduction["dice"],
                            "ability": reduction.get("addAbilityModifier")})
        elif (activation.get("trigger") == "visible-creature-hits-you-with-an-attack-roll" and
              isinstance(resist, dict) and resist.get("percent") == 50 and
              resist.get("scope") == "triggering-attack" and
              resist.get("stacking") == "existing-resistance-rules"):
            results.append({"traitId": identifier, "kind": "attack-resistance",
                            "charges": charges, "fraction": 0.5})
    return results


def reaction_damage_outcomes(traits: dict, level: int, pb: int,
                             scenario: dict, resistances: dict[str, float]) -> tuple[float, list[dict], list[str]]:
    """Optimistic best-informed allocation of reactions to actual packet sizes.

    A character has at most one reaction per round. Multiple distinct pools
    compete for that reaction; no number of source occurrences grants extra
    charges. This is a best-informed frontier, not a player-decision forecast.
    """
    events = scenario.get("reactionDamageEvents", [])
    defenses = _reaction_defenses(traits, level, pb)
    # Retain the trace even when no reaction ability exists.
    trace = [{"id": e["id"], "round": e["round"], "selectedDefense": None,
              "expectedReactionPrevention": 0.0} for e in events]
    if not events or not defenses:
        return 0.0, trace, []
    rounds = sorted({e["round"] for e in events})
    by_round = [[i for i, e in enumerate(events) if e["round"] == r]
                for r in rounds]
    choices = []
    issues = set()
    for indices in by_round:
        choices_here = [(None, None, 0.0)]
        for index in indices:
            event = events[index]
            if not event["reactionAvailable"] or event["amount"] <= 0:
                continue
            for number, defense in enumerate(defenses):
                if defense["kind"] == "damage-reduction":
                    # Combined resistance plus a damage-reduction reaction
                    # needs an explicitly defined ordering rule.
                    if resistances.get(event["damageType"], 0):
                        issues.add(defense["traitId"])
                        continue
                    mod = scenario.get("abilityModifiers", {}).get(defense["ability"], 0)
                    value = _damage_reduction_expectation(defense["dice"], mod,
                                                          event["amount"])
                else:
                    if not event["isAttackHit"] or not event["attackerVisible"]:
                        continue
                    base = resistances.get(event["damageType"], 0)
                    value = event["amount"] * max(0, defense["fraction"] - base)
                if value > 0:
                    choices_here.append((index, number, value))
        choices.append(choices_here)

    @lru_cache(maxsize=None)
    def solve(position: int, remaining: tuple[int, ...]) -> tuple[float, tuple[tuple[int, int, float], ...]]:
        if position == len(choices):
            return 0.0, ()
        best_value, best_plan = solve(position + 1, remaining)
        for index, number, value in choices[position][1:]:
            if remaining[number] <= 0:
                continue
            reduced = list(remaining)
            reduced[number] -= 1
            downstream, plan = solve(position + 1, tuple(reduced))
            if value + downstream > best_value + 1e-10:
                best_value = value + downstream
                best_plan = ((index, number, value),) + plan
        return best_value, best_plan

    total, plan = solve(0, tuple(d["charges"] for d in defenses))
    for event_index, defense_index, prevented in plan:
        trace[event_index]["selectedDefense"] = defenses[defense_index]["traitId"]
        trace[event_index]["expectedReactionPrevention"] = prevented
    return total, trace, sorted(issues)



def joint_combat_plan_outcomes(profile: dict, catalog: dict, level: int,
                               scenario: dict, resistances: dict[str, float]) -> list[dict]:
    """Replay nominated class-build action choices with a single resource ledger.

    Only *one declared attack opportunity per round* is considered. Strike
    riders occupy that opportunity, but do not consume a second Action. This
    conservatively excludes a later separate attack in the same round until a
    complete class attack schedule is supplied. Bonus Actions and Reactions
    have distinct per-round slots. No unstated class feature is inferred from
    the label, and no resource refresh occurs during this single Long Rest day.
    """
    traits = profile["traits"]
    pb = proficiency_bonus(level)
    output = []
    owners_by_short_name = defaultdict(list)
    for identifier, trait in traits.items():
        if isinstance(trait.get("id"), str):
            owners_by_short_name[trait["id"]].append(identifier)

    for plan in scenario.get("jointCombatPlans", []):
        spent: dict[str, int] = defaultdict(int)
        capacities: dict[str, int] = {}
        occupied: set[tuple[int, str]] = set()
        trace: list[dict] = []
        metrics: dict[str, float] = defaultdict(float)
        unresolved = []
        for planned in plan["events"]:
            kind = planned["type"]
            opportunity = planned["opportunity"]
            identifier = (planned["traitId"] if kind == "reaction"
                          else opportunity["traitId"])
            round_number = opportunity.get("round", opportunity.get("turn"))
            trace_row = {"id": opportunity["id"], "type": kind,
                         "traitId": identifier, "round": round_number,
                         "status": None}
            trait = traits.get(identifier)
            if trait is None:
                trace_row["status"] = "not-offered"
                trace.append(trace_row)
                continue
            if not unlocked(trait, level):
                trace_row["status"] = "level-locked"
                trace.append(trace_row)
                continue

            if kind == "strike":
                pool_short = (trait.get("alternateStrikeMode", {}).get("sharedUsesFromTrait")
                              if opportunity.get("mode") == "alternate" else None)
                pool_short = pool_short or trait.get("sharedUsesFromTrait")
                slot = "attack-opportunity"
            elif kind == "tactical":
                pool_short = None
                slot = ("attack-opportunity" if opportunity["kind"] == "searing-ignition"
                        else "bonus-action")
            else:
                pool_short = None
                slot = "reaction"

            if (round_number, slot) in occupied:
                trace_row["status"] = "action-already-spent-this-round"
                trace.append(trace_row)
                continue
            # Resolve source-bound short resource IDs conservatively. Mixed
            # source collisions are not assigned an arbitrary pool owner.
            if pool_short:
                candidates = owners_by_short_name.get(pool_short, [])
                if len(candidates) != 1:
                    trace_row["status"] = "unresolved-shared-resource-owner"
                    unresolved.append({"traitId": identifier,
                                       "reason": "ambiguous-or-missing-shared-resource-owner",
                                       "planId": plan["id"]})
                    trace.append(trace_row)
                    continue
                owner = candidates[0]
            else:
                owner = identifier
            uses = traits[owner].get("uses", {})
            value = uses.get("value") if isinstance(uses, dict) else None
            capacity = pb if value == "proficiency-bonus" else value
            if (not isinstance(uses, dict) or uses.get("recharge") != "long-rest"
                    or type(capacity) is not int or capacity < 0):
                trace_row["status"] = "unresolved-resource-definition"
                unresolved.append({"traitId": identifier, "reason": "unsupported-joint-pool",
                                   "planId": plan["id"]})
                trace.append(trace_row)
                continue
            capacities[owner] = capacity
            if spent[owner] >= capacity:
                trace_row["status"] = "shared-resource-exhausted"
                trace_row["poolOwnerTraitId"] = owner
                trace.append(trace_row)
                continue

            extra_metrics, helper_row, helper_issues = {}, {}, []
            if kind == "strike":
                v, rows, helper_issues, _, _ = on_hit_control_outcomes(
                    traits, catalog, level, {"strikeOpportunities": [opportunity]})
                helper_row = rows[0]
                extra_metrics = {"joint_" + key: number for key, number in v.items()
                                 if key != "strike_uses_selected"}
            elif kind == "reaction":
                v, rows, affected = reaction_damage_outcomes(
                    {identifier: trait}, level, pb,
                    {"reactionDamageEvents": [opportunity],
                     "abilityModifiers": scenario.get("abilityModifiers", {})},
                    resistances)
                helper_row = rows[0]
                helper_issues = [{"traitId": entry,
                                  "reason": "reaction-resistance-reduction-order-unresolved"}
                                 for entry in affected]
                extra_metrics = {"joint_reaction_damage_prevented": v}
            else:
                v, rows, helper_issues, _ = tactical_action_outcomes(
                    {identifier: trait}, catalog, level, pb,
                    {"tacticalOpportunities": [opportunity],
                     "damageTypeMultiplier": scenario.get("damageTypeMultiplier", {}),
                     "attackReplacementExpectedDamage": scenario.get(
                         "attackReplacementExpectedDamage", 5.2)}, resistances)
                helper_row = rows[0]
                extra_metrics = {"joint_" + key: number for key, number in v.items()}

            # Only an actual legal activation occupies a slot or spends a pool.
            if kind == "reaction":
                valid = helper_row.get("selectedDefense") == identifier
                status = ("activated" if valid else "unresolved-reaction-trigger"
                          if helper_issues else "no-qualifying-reaction")
            else:
                status = helper_row["status"]
                valid = status == "activated"
            trace_row["status"] = status
            trace_row["detail"] = helper_row
            if not valid:
                trace.extend((trace_row,))
                if helper_issues:
                    unresolved.extend({**issue, "planId": plan["id"]}
                                      for issue in helper_issues)
                continue
            spent[owner] += 1
            occupied.add((round_number, slot))
            trace_row["poolOwnerTraitId"] = owner
            trace_row["poolUsesRemaining"] = capacity - spent[owner]
            for name, value in extra_metrics.items():
                metrics[name] += value
            if kind == "strike":
                # A confirmed hit already occurred; this rider adds no
                # second attack or extra Action, only a shared-use cost.
                metrics["joint_expected_net_damage_delta"] += (
                    extra_metrics.get("joint_strike_gross_extra_damage_on_confirmed_hits", 0))
            elif kind == "tactical":
                if opportunity["kind"] == "stone-throw":
                    metrics["joint_expected_net_damage_delta"] += (
                        helper_row["expectedGrossDamage"] -
                        opportunity["bonusActionAlternativeExpectedDamage"])
                elif opportunity["kind"] == "searing-ignition":
                    metrics["joint_expected_net_damage_delta"] += helper_row["expectedNetDamage"]
                else:
                    metrics["joint_expected_net_damage_delta"] -= (
                        opportunity["bonusActionAlternativeExpectedDamage"])
            else:
                metrics["joint_expected_net_damage_delta"] -= (
                    planned["reactionAlternativeExpectedDamage"])
            unresolved.extend({**issue, "planId": plan["id"]}
                              for issue in helper_issues)
            trace.append(trace_row)
        output.append({"id": plan["id"], "archetype": plan["archetype"],
                       "metrics": dict(sorted(metrics.items())),
                       "events": trace, "resourcesSpent": dict(sorted(spent.items())),
                       "resourcePoolCapacities": dict(sorted(capacities.items())),
                       "unresolved": unresolved,
                       "interpretation": "declared-chronological-plan-not-optimal-or-complete-build"})
    return output


def _native_class_actions(catalog: dict, class_id: str, level: int,
                          procedure: str, first_hit: float,
                          damage_on_hit: float) -> tuple[dict | None, str | None]:
    """Read the current pure-class progression and a single attack procedure.

    Standard Extra Attack is granted by a concrete class-feature mechanic.
    BAB iteratives are a separate procedure with lower later attack bonuses;
    they must never be added to a standard Extra Attack grant. Every hit
    probability depends on an explicitly supplied first-attack baseline.
    """
    record = catalog.get("classes", {}).get(class_id)
    if not record or not isinstance(record.get("definition"), dict):
        return None, "missing-native-class-definition"
    definition = record["definition"]
    entries = definition.get("progression")
    if not isinstance(entries, list):
        return None, "missing-native-class-progression"
    current = next((entry for entry in entries if entry.get("level") == level), None)
    if current is None:
        return None, "missing-native-class-level"
    if procedure == "iterative":
        bonuses = current.get("iterativeBaseAttackBonuses")
        if not isinstance(bonuses, list) or any(type(v) is not int for v in bonuses):
            return None, "unsupported-native-iterative-attack-schedule"
        # A BAB +0 character still has an ordinary weapon attack; [] means
        # no positive-BAB attacks, not that the character cannot attack.
        if not bonuses:
            if current.get("baseAttackBonus") != 0:
                return None, "ambiguous-empty-iterative-schedule"
            bonuses = [0]
        elif current.get("baseAttackBonus") != bonuses[0]:
            return None, "inconsistent-native-bab-progression"
        chances = [max(0.0, min(1.0, first_hit + (bonus - bonuses[0]) / 20))
                   for bonus in bonuses]
        evidence = {"procedure": "iterative-BAB", "baseAttackBonuses": bonuses,
                    "progressionSource": class_id}
    else:
        if (definition.get("extraAttackAndIteratives") not in
                (None, "alternative-procedures-not-cumulative") or
                definition.get("iterativeAttackPolicy") not in
                (None, "shared-native-alternative-attack-procedures",
                 "shared-native-attack-procedures")):
            return None, "unsupported-attack-procedure-policy"
        # Explicit standard Attack action feature mechanics, not an inference
        # from a class's name or any BAB count.
        count = 1
        grant_ids = set()
        for entry in entries:
            if entry.get("level", 0) <= level:
                grant_ids.update(entry.get("featureGrants", []))
        for grant in grant_ids:
            if "extra-attack" not in grant:
                continue
            feature = catalog.get("classFeatures", {}).get(grant)
            mechanics = feature.get("definition", {}).get("mechanics", {}) if feature else {}
            if (not feature or feature.get("parentClass") != class_id or
                    type(feature.get("classLevel")) is not int or
                    feature["classLevel"] > level or
                    mechanics.get("kind") != "attack-action" or
                    mechanics.get("babInteraction") != "alternative-procedure-not-additive" or
                    type(mechanics.get("standardAttackCount")) is not int or
                    mechanics["standardAttackCount"] < 1):
                return None, "unverified-native-extra-attack-feature"
            count = max(count, mechanics["standardAttackCount"])
        chances = [first_hit] * count
        evidence = {"procedure": "standard-Attack-action", "grants": sorted(grant_ids),
                    "extraAttackRule": "native-feature-only-not-added-to-BAB"}
    unmodeled_progression_fields = sorted(set(current) - {
        "level", "baseAttackBonus", "iterativeBaseAttackBonuses", "featureGrants",
        "spellSlotsBySpellLevel"})
    spell_slots = current.get("spellSlotsBySpellLevel", [])
    if not isinstance(spell_slots, list) or any(type(n) is not int or n < 0 for n in spell_slots):
        return None, "invalid-native-spell-slot-progression"
    return {"classId": class_id, "classLevel": level,
            "attackProcedure": procedure, "attackCount": len(chances),
            "perAttackHitChances": chances,
            "perAttackExpectedDamages": [p * damage_on_hit for p in chances],
            "attackActionExpectedDamage": sum(p * damage_on_hit for p in chances),
            "nativeSpellSlotsByLevel": {i+1: n for i, n in enumerate(spell_slots)},
            "otherNativeProgressionFieldsNotEvaluated": unmodeled_progression_fields,
            "evidence": evidence}, None


def _class_feature_mechanics(catalog: dict, class_id: str,
                             granted: set[str], feature_id: str,
                             level: int, expected_kind: str) -> dict | None:
    """Use an authored, currently granted feature; never guess from its name."""
    if feature_id not in granted:
        return None
    feature = catalog.get("classFeatures", {}).get(feature_id)
    if (not feature or feature.get("parentClass") != class_id or
            type(feature.get("classLevel")) is not int or
            feature["classLevel"] > level):
        return None
    mechanic = feature.get("definition", {}).get("mechanics", {})
    return mechanic if mechanic.get("kind") == expected_kind else None


def _rage_battle_bonus(mechanic: dict, level: int,
                       progression: dict) -> tuple[float, int] | None:
    """Cross-check native Rage tables against class progression values."""
    battle = mechanic.get("battle", {})
    if (mechanic.get("activation") != "bonus-action" or
            mechanic.get("kind") != "rage-resource" or
            mechanic.get("shared", {}).get("noHeavyArmor") is not True or
            mechanic.get("shared", {}).get("forbidSpellsAndConcentration") is not True or
            battle.get("durationUntil") != "end-of-next-turn" or
            battle.get("extendBy") != ["attack-enemy", "force-enemy-saving-throw", "bonus-action"]):
        return None
    try:
        bonus = next(val for lvl, val in reversed(battle["strengthAttackDamageBonus"])
                     if lvl <= level)
        uses = next(val for lvl, val in reversed(mechanic["usesByClassLevel"])
                    if lvl <= level)
    except (KeyError, ValueError, TypeError):
        return None
    if (type(bonus) is not int or type(uses) is not int or
            progression.get("battleRageDamage") != bonus or
            progression.get("rageUses") != uses):
        return None
    return bonus, uses


def _class_sneak_dice(catalog: dict, class_id: str, granted: set[str],
                       level: int, progression: dict) -> float | None:
    mechanic = _class_feature_mechanics(
        catalog, class_id, granted,
        "class-feature.rogue.sneak-attack-level-1", level, "sneak-attack")
    if not mechanic or any((
            mechanic.get("dice") != "1d6 plus 1d6 each odd Rogue level, through 10d6",
            mechanic.get("frequency") != "each-independently-qualifying-successful-hit",
            mechanic.get("eligibility") != "undetected-or-unanticipated-attack-or-explicit-precision-vulnerability",
            mechanic.get("advantageAloneInsufficient") is not True,
            mechanic.get("requiresWeaponProficiency") is not True,
            mechanic.get("ordinaryRangeOnly") is not True,
            mechanic.get("creatureTypeNotBlanketImmunity") is not True,
            mechanic.get("respectsActualImmunities") is not True,
            mechanic.get("noExtraAttacksGranted") is not True)):
        return None
    expected = f"{(level + 1) // 2}d6"
    if progression.get("sneakAttackDice") != expected:
        return None
    return dice_mean(expected)


def native_class_action_outcomes(profile: dict, catalog: dict, level: int,
                                 scenario: dict, resistances: dict) -> list[dict]:
    """Replay one native pure-class action schedule with explicitly valued options.

    Attack counts and spell slot charges come from native progression; damage
    assumptions and candidate spell identities do NOT. A class-based Reaction
    is legal only when its feature is actually granted at this class level.
    Per-round Action and Reaction capacities remain independent.
    """
    output = []
    traits = profile["traits"]
    for plan in scenario.get("nativeClassActionPlans", []):
        model, problem = _native_class_actions(
            catalog, plan["classId"], level, plan["attackProcedure"],
            plan["baseHitChance"], plan["weaponDamageOnHit"])
        result = {"id": plan["id"], "classId": plan["classId"], "level": level,
                  "events": [], "resourceExpenditure": {}, "unresolved": [],
                  "interpretation": "native-single-class-progression-with-fixture-valued-actions"}
        if model is None:
            result["status"] = "unresolved-native-progression"
            result["unresolved"].append({"reason": problem, "classId": plan["classId"]})
            output.append(result)
            continue
        result["classProgression"] = model
        result["status"] = "evaluated-conditional-options"
        result["limitations"] = [
            "Only verified Battle Rage, Sneak Attack, Cunning Action, Steady Aim and Uncanny Dodge are partly modeled; other class features remain unvalued.",
            "Hit probabilities, weapon damage, spell choice and spell effects are fixture assumptions.",
            "No multiclass, class subclass, rest recharge, equipment, concentration, critical or feat optimization is inferred.",
        ]
        granted = set()
        native_record = catalog["classes"][plan["classId"]]["definition"]
        for row in native_record["progression"]:
            if row.get("level", 0) <= level:
                granted.update(row.get("featureGrants", []))
        partially_modeled_grants = {
            "class-feature.barbarian.rage-level-1",
            "class-feature.barbarian.extra-attack-level-5",
            "class-feature.rogue.sneak-attack-level-1",
            "class-feature.rogue.cunning-action-level-2",
            "class-feature.rogue.steady-aim-level-3",
            "class-feature.rogue.uncanny-dodge-level-5",
        }
        result["nativeClassFeatureGrantsPartiallyModeled"] = sorted(granted & partially_modeled_grants)
        result["nativeClassFeatureGrantsNotPriced"] = sorted(granted - partially_modeled_grants)
        slots = model["nativeSpellSlotsByLevel"].copy()
        spent: dict[str, int] = defaultdict(int)
        action_slots: set[int] = set()
        reaction_slots: set[int] = set()
        bonus_slots: set[int] = set()
        rage_activation_round: int | None = None
        rage_damage_bonus: int | None = None
        rage_uses_spent = 0
        steady_aim_round: int | None = None
        progression = next(row for row in native_record["progression"]
                           if row.get("level") == level)
        for event in plan["events"]:
            rnd, choice = event["round"], event["kind"]
            row = {"id": event["id"], "round": rnd, "kind": choice,
                   "status": "unresolved"}
            if choice in ("class-reaction", "origin-reaction"):
                occupied = reaction_slots
            elif choice in ("class-bonus-action", "origin-bonus-action"):
                occupied = bonus_slots
            else:
                occupied = action_slots
            action_kind = occupied is action_slots
            if rnd in occupied:
                row["status"] = "same-round-slot-already-used"
                result["events"].append(row)
                continue
            # Battle Rage can persist across turns, but its exact duration and
            # extension triggers require a turn-level model. Do not quietly
            # score later Attack actions as though Rage had ended.
            if (rage_activation_round is not None and rnd > rage_activation_round
                    and choice in ("attack", "breath", "searing-ignition")):
                row["status"] = "rage-upkeep-before-action-unresolved"
                result["unresolved"].append({"eventId": event["id"], "reason": row["status"]})
                result["events"].append(row)
                continue
            baseline = model["attackActionExpectedDamage"]
            if action_kind:
                row["attackActionExpectedDamage"] = baseline
            if choice == "attack":
                chances = model["perAttackHitChances"].copy()
                if steady_aim_round == rnd:
                    chances[0] = chances[0] + chances[0] * (1 - chances[0])
                    steady_aim_round = None
                    row["steadyAimAdvantageAppliedToAttackIndex"] = 0
                baseline = sum(p * plan["weaponDamageOnHit"] for p in chances)
                row["attackActionExpectedDamage"] = baseline
                if rage_activation_round == rnd and event.get("strengthBasedAttacks") is True:
                    added = sum(chances) * rage_damage_bonus
                    row["battleRageExpectedBonusDamage"] = added
                    baseline += added
                elif rage_activation_round == rnd and "strengthBasedAttacks" not in event:
                    result["unresolved"].append({"eventId": event["id"],
                                                 "reason": "rage-strength-attack-basis-unspecified"})
                precision = event.get("precisionHits", [])
                if precision:
                    mean = _class_sneak_dice(catalog, plan["classId"], granted,
                                             level, progression)
                    if mean is None:
                        result["unresolved"].append({"eventId": event["id"],
                                                     "reason": "native-sneak-attack-unverified-or-not-granted"})
                    else:
                        bonus = 0.0
                        for hit in precision:
                            idx = hit["attackIndex"]
                            if idx >= len(chances):
                                result["unresolved"].append({"eventId": event["id"],
                                                             "reason": "precision-attack-index-unavailable"})
                                continue
                            eligible = (hit["weaponProficient"] and
                                        hit["withinOrdinaryRange"] and not hit["targetImmune"])
                            if eligible:
                                bonus += chances[idx] * mean
                        row["sneakAttackExpectedExtraDamage"] = bonus
                        baseline += bonus
                row["status"] = "used"
                row["expectedDamage"] = baseline
            elif choice == "class-bonus-action":
                feature_id = event["featureId"]
                option = event["option"]
                expected = {"battle-rage": ("class-feature.barbarian.rage-level-1", "rage-resource"),
                            "steady-aim": ("class-feature.rogue.steady-aim-level-3", "aim-for-accuracy"),
                            "dash": ("class-feature.rogue.cunning-action-level-2", "bonus-action-choices"),
                            "disengage": ("class-feature.rogue.cunning-action-level-2", "bonus-action-choices"),
                            "hide": ("class-feature.rogue.cunning-action-level-2", "bonus-action-choices")}[option]
                mechanic = (_class_feature_mechanics(catalog, plan["classId"],
                             granted, feature_id, level, expected[1])
                            if feature_id == expected[0] else None)
                if mechanic is None:
                    row["status"] = "class-bonus-feature-not-granted-or-unverified"
                elif option == "battle-rage":
                    spec = _rage_battle_bonus(mechanic, level, progression)
                    if spec is None:
                        row["status"] = "battle-rage-native-progression-unresolved"
                    elif event["wearingHeavyArmor"]:
                        row["status"] = "heavy-armor-prohibits-rage"
                    elif (rage_activation_round is not None and
                          (not event["rageEndedBeforeActivation"] or
                           rage_activation_round >= rnd)):
                        row["status"] = "rage-ongoing-duration-unresolved"
                    elif rage_uses_spent >= spec[1]:
                        row["status"] = "rage-resource-exhausted"
                    else:
                        rage_damage_bonus = spec[0]
                        rage_activation_round = rnd
                        rage_uses_spent += 1
                        row.update(status="used", rageUsesSpent=rage_uses_spent,
                                   battleRageBonusPerQualifyingHit=rage_damage_bonus)
                        result["unresolved"].append({"eventId": event["id"],
                            "reason": "rage-duration-after-activation-round-and-resistance-not-modeled"})
                elif option == "steady-aim":
                    if (mechanic.get("cost") != "bonus-action" or
                            mechanic.get("advantageNextAttackCurrentTurn") is not True or
                            mechanic.get("doesNotCreateSneakAttackEligibility") is not True or
                            mechanic.get("requiresNotMovedThisTurn") is not True or
                            mechanic.get("speedZeroUntilTurnEnd") is not True):
                        row["status"] = "unverified-steady-aim-rules"
                    elif event["movedThisTurn"]:
                        row["status"] = "steady-aim-movement-restriction"
                    else:
                        steady_aim_round = rnd
                        row.update(status="used", movementSpeedUntilTurnEnd=0,
                                   nextAttackRollAdvantage=True)
                elif (mechanic.get("cost") == "bonus-action" and
                        option in mechanic.get("options", []) and
                        set(mechanic.get("options", [])) == {"dash", "disengage", "hide"}):
                    row.update(status="used", selectedOption=option)
                    result["unresolved"].append({"eventId": event["id"],
                        "reason": "cunning-action-positioning-effect-unvalued"})
                else:
                    row["status"] = "unverified-cunning-action-rules"
            elif choice == "origin-bonus-action":
                identifier = event["traitId"]
                trait = traits.get(identifier)
                if not trait or not unlocked(trait, level):
                    row["status"] = "origin-bonus-not-expressed-or-level-locked"
                elif _tactical_kind(trait) != "stone-throw":
                    row["status"] = "unverified-origin-bonus-mechanics"
                elif not event["rockAvailable"]:
                    row["status"] = "missing-rock"
                elif event["distanceFeet"] > trait["attack"]["rangeFeet"]:
                    row["status"] = "out-of-range"
                elif spent[identifier] >= proficiency_bonus(level):
                    row["status"] = "origin-bonus-resource-exhausted"
                else:
                    spent[identifier] += 1
                    gross = event["hitChance"] * dice_mean("1d10")
                    row.update(status="used", expectedGrossDamage=gross,
                               resourceOwnerTraitId=identifier)
                    if event["targetIsCreature"]:
                        if "condition.prone" in catalog["conditions"]:
                            row["expectedEnemyProneApplications"] = event["hitChance"] * event["saveFailure"]
                        else:
                            result["unresolved"].append({"eventId": event["id"],
                                "reason": "stone-throw-prone-condition-not-cataloged"})
            elif choice in ("breath", "searing-ignition"):
                if steady_aim_round == rnd:
                    row["status"] = "steady-aim-and-attack-replacement-unresolved"
                    result["unresolved"].append({"eventId": event["id"],
                                                 "reason": row["status"]})
                    result["events"].append(row)
                    continue
                if rage_activation_round == rnd and "strengthBasedAttacks" not in event:
                    row["status"] = "rage-attack-basis-unspecified"
                    result["unresolved"].append({"eventId": event["id"],
                                                 "reason": row["status"]})
                    result["events"].append(row)
                    continue
                identifier = event["traitId"]
                trait = traits.get(identifier)
                if trait is None:
                    row["status"] = "not-expressed"
                    result["events"].append(row)
                    continue
                if not unlocked(trait, level):
                    row["status"] = "level-locked"
                    result["events"].append(row)
                    continue
                idx = event["replacedAttackIndex"]
                if idx >= model["attackCount"]:
                    row["status"] = "attack-index-not-available"
                    result["events"].append(row)
                    continue
                if choice == "breath":
                    activation = trait.get("activation", {})
                    damage = trait.get("damage", {})
                    progression = damage.get("progression", [])
                    active = [step for step in progression if step["characterLevel"] <= level]
                    intrinsics = profile["intrinsics"]
                    if (activation.get("replaces") != "one-attack" or
                            activation.get("action") != "attack" or not active or
                            not isinstance(intrinsics.get("breathWeaponArea"), dict) or
                            not intrinsics.get("damageType")):
                        row["status"] = "unverified-breath-source-bindings"
                        result["unresolved"].append({"reason": row["status"], "traitId": identifier})
                        result["events"].append(row)
                        continue
                    damage_type = intrinsics["damageType"]
                    gross = (event["targets"] * dice_mean(active[-1]["dice"]) *
                             (0.5 + 0.5 * event["saveFailure"]) *
                             scenario.get("damageTypeMultiplier", {}).get(damage_type, 1))
                else:
                    activation = trait.get("activation", {})
                    dmg = trait.get("damage", {})
                    if (trait.get("requiredCharacterLevel") != 4 or
                            activation.get("action") != "attack" or
                            activation.get("replaces") != "one-attack" or
                            trait.get("applyConditionOnFailedSave", {}).get("condition") !=
                            "condition.blinded" or dmg.get("dice") != "1d8" or
                            dmg.get("add") != "proficiency-bonus"):
                        row["status"] = "unverified-searing-ignition"
                        result["unresolved"].append({"reason": row["status"], "traitId": identifier})
                        result["events"].append(row)
                        continue
                    gross = (event["targets"] * (dice_mean("1d8")+proficiency_bonus(level)) *
                             (0.5+0.5*event["saveFailure"]) *
                             scenario.get("damageTypeMultiplier", {}).get("fire", 1))
                uses = trait.get("uses", {})
                if not isinstance(uses, dict) or uses.get("value") != "proficiency-bonus" or                         uses.get("recharge") not in ("long-rest", None):
                    row["status"] = "unresolved-origin-resource"
                    result["unresolved"].append({"reason": row["status"], "traitId": identifier})
                    result["events"].append(row)
                    continue
                if spent[identifier] >= proficiency_bonus(level):
                    row["status"] = "origin-resource-exhausted"
                    result["events"].append(row)
                    continue
                spent[identifier] += 1
                replaced = model["perAttackExpectedDamages"][idx]
                if rage_activation_round == rnd and event["strengthBasedAttacks"]:
                    rage_marginal = rage_damage_bonus * sum(model["perAttackHitChances"])
                    baseline += rage_marginal
                    replaced += model["perAttackHitChances"][idx] * rage_damage_bonus
                    row["battleRageExpectedBonusOnRetainedAttacks"] = (
                        rage_marginal - model["perAttackHitChances"][idx] * rage_damage_bonus)
                row.update({"status": "used", "grossExpectedDamage": gross,
                            "expectedDamage": baseline - replaced + gross,
                            "marginalVersusWeaponAttack": gross - replaced,
                            "attackReplacedIndex": idx})
                if "spellAlternative" in event:
                    spec = event["spellAlternative"]
                    lvl = spec["slotLevel"]
                    if slots.get(lvl, 0) > 0:
                        # An alternative is *not* consumed in this trace.
                        row["spellAlternativeExpectedDamage"] = spec["expectedDamage"]
                        row["marginalVersusSpellAction"] = row["expectedDamage"] - spec["expectedDamage"]
                        result["unresolved"].append({"reason": "spell-identity-and-effect-fixture-assumed",
                                                     "eventId": event["id"]})
                    else:
                        row["spellAlternativeStatus"] = "slot-not-available"
            elif choice == "spell":
                if rage_activation_round is not None:
                    row["status"] = ("rage-forbids-spellcasting" if rage_activation_round == rnd
                                     else "rage-upkeep-and-spellcasting-unresolved")
                    result["events"].append(row)
                    continue
                lvl = event["slotLevel"]
                if slots.get(lvl, 0) <= 0:
                    row["status"] = "spell-slot-exhausted-or-locked"
                    result["events"].append(row)
                    continue
                # Slot expenditure is grounded; permission to know, access and
                # apply this spell is not proved by the class slot table.
                slots[lvl] -= 1
                row.update({"status": "used-fixture-assumption",
                            "spellId": event["spellId"], "spellSlotLevel": lvl,
                            "expectedDamage": event["expectedDamage"],
                            "marginalVersusWeaponAttack": event["expectedDamage"] - baseline})
                result["unresolved"].append({"eventId": event["id"],
                                             "reason": "spell-identity-and-effect-fixture-assumed"})
            elif choice == "origin-reaction":
                identifier = event["traitId"]
                trait = traits.get(identifier)
                if not trait or not unlocked(trait, level):
                    row["status"] = "origin-reaction-not-expressed-or-locked"
                    result["events"].append(row)
                    continue
                defense = next((d for d in _reaction_defenses(traits, level,
                                       proficiency_bonus(level))
                                if d["traitId"] == identifier), None)
                if not defense:
                    row["status"] = "unsupported-origin-reaction"
                    result["events"].append(row)
                    continue
                if (defense["kind"] == "attack-resistance" and
                        (not event["attackerVisible"] or not event["isAttackHit"])):
                    row["status"] = "reaction-trigger-not-met"
                    result["events"].append(row)
                    continue
                if resistances.get(event["damageType"], 0):
                    row["status"] = "resistance-order-unresolved"
                    result["unresolved"].append({"eventId": event["id"],
                                                 "reason": "origin-reaction-resistance-order-unresolved"})
                    result["events"].append(row)
                    continue
                if spent[identifier] >= defense["charges"]:
                    row["status"] = "origin-reaction-resource-exhausted"
                    result["events"].append(row)
                    continue
                if defense["kind"] == "damage-reduction":
                    ability = defense["ability"]
                    if ability and ability not in scenario.get("abilityModifiers", {}):
                        row["status"] = "missing-ability-modifier"
                        result["unresolved"].append({"eventId": event["id"],
                                                     "reason": "origin-reaction-modifier-unspecified"})
                        result["events"].append(row)
                        continue
                    prevention = _damage_reduction_expectation(
                        defense["dice"],
                        scenario.get("abilityModifiers", {}).get(ability, 0),
                        event["damageAmount"])
                else:
                    prevention = event["damageAmount"] * defense["fraction"]
                spent[identifier] += 1
                row.update({"status": "used", "expectedDamagePrevented": prevention,
                            "resourceOwnerTraitId": identifier,
                            "resourceUsesRemaining": defense["charges"]-spent[identifier]})
            else:  # class-reaction, currently only verified Rogue Uncanny Dodge
                feature_id = event["featureId"]
                feature = catalog.get("classFeatures", {}).get(feature_id)
                mechanics = feature.get("definition", {}).get("mechanics", {}) if feature else {}
                if (feature_id not in granted or not feature or
                        feature.get("parentClass") != plan["classId"] or
                        mechanics.get("kind") != "reaction-damage-halving" or
                        mechanics.get("trigger") != "visible-attacker-hits-you" or
                        mechanics.get("cost") != "reaction" or
                        mechanics.get("divideDamageBy") != 2 or
                        mechanics.get("roundDown") is not True):
                    row["status"] = "class-reaction-not-granted-or-unverified"
                    result["events"].append(row)
                    continue
                if not event["attackerVisible"] or not event["isAttackHit"]:
                    row["status"] = "reaction-trigger-not-met"
                    result["events"].append(row)
                    continue
                if resistances.get(event["damageType"], 0):
                    row["status"] = "resistance-order-unresolved"
                    result["unresolved"].append({"eventId": event["id"],
                                                 "reason": "class-reaction-resistance-order-unresolved"})
                    result["events"].append(row)
                    continue
                # Exact after-damage floor for positive integer HP packets;
                # fractional expected packets need a distribution first.
                amount = event["damageAmount"]
                if type(amount) is not int:
                    row["status"] = "fractional-damage-rounding-unresolved"
                    result["events"].append(row)
                    continue
                row.update({"status": "used", "expectedDamagePrevented":
                            amount - amount // 2})
            if row["status"] in ("used", "used-fixture-assumption"):
                occupied.add(rnd)
            result["events"].append(row)
        result["resourceExpenditure"] = {
            "originTraitUses": dict(sorted(spent.items())),
            "remainingClassSpellSlotsByLevel": slots,
            "nativeRageUsesSpent": rage_uses_spent,
        }
        output.append(result)
    return output


def native_shared_pool_frontiers(traits: dict, level: int) -> list[dict]:
    """Expose only defense/strike links that match the conservative frontier."""
    frontiers = []
    for alternative_id, alternative in traits.items():
        if not unlocked(alternative, level) or "sharedUsesFromTrait" not in alternative:
            continue
        for base_id, base in traits.items():
            if (not unlocked(base, level) or
                    alternative["sharedUsesFromTrait"] != base.get("id") or
                    "damageReduction" not in base or "extraDamage" not in alternative):
                continue
            frontiers.append({"baseTraitId": base_id,
                              "alternativeTraitId": alternative_id,
                              "description": "optimistic fully eligible triggers; on-hit damage; one shared pool",
                              "frontier": shared_pool_frontier(base, alternative, level)})
    return frontiers


def _breath_damage(trait: dict, intrinsics: dict, level: int,
                   scenario: dict, remaining: int) -> tuple[float, int, list[dict]]:
    """Evaluate attack replacement across declared, distinct area opportunities.

    This is an optimistic selection of the best net-positive opportunities.
    It does not assert that every opportunity is reachable in actual combat.
    """
    progression = trait["damage"]["progression"]
    eligible = [step for step in progression if level >= step["characterLevel"]]
    if not eligible:
        return 0.0, 0, []
    dice = dice_mean(eligible[-1]["dice"])
    area = intrinsics.get("breathWeaponArea", {})
    shape = area.get("shape") if isinstance(area, dict) else None
    if not shape:
        return 0.0, 0, []
    multiplier = scenario.get("damageTypeMultiplier", {}).get(intrinsics.get("damageType"), 1)
    events = []
    for index, event in enumerate(scenario.get("breathOpportunities", [])):
        count = event.get("areaTargets", {}).get(shape, 0)
        fail = event.get("saveFailure", scenario.get("saveFailure", 0.55))
        replacement = event.get("attackReplacementExpectedDamage",
                                scenario.get("attackReplacementExpectedDamage", 5.2))
        gross = count * dice * (0.5 + 0.5 * fail) * multiplier
        events.append({"eventIndex": index, "areaShape": shape,
                       "targets": count, "grossExpectedDamage": gross,
                       "replacementExpectedDamage": replacement,
                       "netExpectedDamage": gross - replacement,
                       "selected": False})
    picks = sorted((e for e in events if e["netExpectedDamage"] > 0),
                   key=lambda e: (-e["netExpectedDamage"], e["eventIndex"]))[:remaining]
    for event in picks:
        event["selected"] = True
    return sum(e["netExpectedDamage"] for e in picks), len(picks), events


def _sense_contact_detected(trait: dict, contact: dict, active: bool) -> bool:
    if contact["distanceFeet"] > trait.get("rangeFeet", -1):
        return False
    if not contact.get("connected", False):
        return False
    if "requiresContact" in trait and contact.get("sensorTouches") != trait["requiresContact"]:
        return False
    if "requiresSharedContact" in trait:
        media = trait["requiresSharedContact"]
        if (contact.get("sensorTouches") not in media or
                contact.get("targetTouches") not in media or
                contact.get("sensorTouches") != contact.get("targetTouches")):
            return False
    if active and not contact.get("anticipated", False):
        return False
    return True


def _tremorsense_contacts(traits: dict, level: int, pb: int, scenario: dict) -> int:
    """Passive opportunities first; activated contacts cost actual uses."""
    senses = [t for t in traits.values() if t.get("sense") == "tremorsense"
              and unlocked(t, level)]
    available = {i: (pb if t.get("uses", {}).get("value") == "proficiency-bonus"
                    else t.get("uses", {}).get("value", 10**9))
                 for i, t in enumerate(senses)}
    count = 0
    for contact in scenario.get("tremorsenseContacts", []):
        passive = any(_sense_contact_detected(t, contact, False)
                      for t in senses if t.get("activation") == "passive")
        if passive:
            count += 1
            continue
        for i, t in enumerate(senses):
            if t.get("activation") != "passive" and available[i] > 0 and \
                    _sense_contact_detected(t, contact, True):
                count += 1
                available[i] -= 1
                break
    return count


def _movement_options(profile: dict, level: int, pb: int) -> list[dict]:
    """List only explicit movement modes; never infer a fallback route or spell."""
    intrinsic = profile["intrinsics"]
    options = []
    for mode, field in (("walk", "walkingSpeedFeet"),
                        ("climb", "climbingSpeedFeet"),
                        ("swim", "swimmingSpeedFeet"),
                        ("burrow", "burrowingSpeedFeet")):
        speed = intrinsic.get(field)
        if isinstance(speed, (int, float)) and speed > 0:
            # Intrinsic burrowing needs an explicit substrate eligibility rule.
            options.append({"mode": mode, "speed": speed, "source": field,
                            "maxUses": None, "duration": None,
                            "materials": None, "hover": False})
    for identifier, trait in profile["traits"].items():
        if not unlocked(trait, level):
            continue
        mode = trait.get("movementMode")
        if mode in MOVEMENT_MODES and isinstance(trait.get("speedFeet"), (int, float)):
            speed = trait["speedFeet"]
        elif "flyingSpeed" in trait:
            mode = "fly"
            spec = trait["flyingSpeed"]
            speed = (intrinsic.get("walkingSpeedFeet")
                     if isinstance(spec, dict) and spec.get("equalTo") == "current-walking-speed"
                     else spec if isinstance(spec, (int, float)) else None)
        else:
            continue
        if not isinstance(speed, (int, float)) or speed <= 0:
            continue
        use_spec = trait.get("uses")
        if use_spec is None:
            max_uses = None if trait.get("atWill", True) else 0
        elif use_spec.get("recharge") == "long-rest":
            units = use_spec.get("value")
            max_uses = pb if units == "proficiency-bonus" else units
            if not isinstance(max_uses, int) or max_uses < 0:
                continue
        else:
            # The fixture has no short-rest timings for movement resources.
            continue
        duration_spec = trait.get("duration")
        if isinstance(duration_spec, dict) and "minutes" in duration_spec:
            duration = duration_spec["minutes"]
        else:
            duration = trait.get("durationMinutes")
        if max_uses is not None and not isinstance(duration, (float, int)):
            # Do not silently turn a limited capability into unlimited access.
            continue
        options.append({"mode": mode, "speed": speed, "source": identifier,
                        "maxUses": max_uses, "duration": duration,
                        "materials": trait.get("allowedMaterials"),
                        "hover": trait.get("canHover", False)})
    return options


def mobility_access(profile: dict, level: int, pb: int,
                    scenario: dict) -> list[dict]:
    """Replay solo intrinsic movement gates with day-limited activation pools.

    A satisfied gate means this Origin explicitly supplies the required *mode*;
    it is not a claim that the full party completes the adventure objective.
    """
    options = _movement_options(profile, level, pb)
    state = [{"remaining": option["maxUses"], "activeUntil": -1}
             for option in options]
    challenges = list(enumerate(scenario.get("mobilityChallenges", [])))
    results = [None] * len(challenges)
    for index, event in sorted(challenges,
                               key=lambda pair: (pair[1].get("startMinute", 0), pair[0])):
        mode = event["mode"]
        started = event.get("startMinute", 0)
        ended = started + event.get("durationMinutes", 0.1)
        needed = event["distanceFeet"]
        eligible = []
        for n, option in enumerate(options):
            if option["mode"] != mode or option["speed"] * event.get("maxMoves", 1) < needed:
                continue
            if event.get("requiresHover", False) and not option["hover"]:
                continue
            if mode == "burrow":
                # A generic burrowing speed does not imply passage through stone.
                if (not option["materials"] or
                        event["material"] not in option["materials"]):
                    continue
            if option["duration"] is not None and option["duration"] < event.get("durationMinutes", 0.1):
                continue
            current = state[n]
            active = (option["maxUses"] is not None and current["activeUntil"] >= ended)
            available = (option["maxUses"] is None or active or current["remaining"] > 0)
            if available:
                eligible.append((0 if option["maxUses"] is None or active else 1, n))
        if event.get("requiresWholeParty", False):
            results[index] = {"id": event["id"], "mode": mode,
                              "status": "unresolved-party-access",
                              "reason": "one character's mode does not move the full party"}
        elif not eligible:
            uncertain_substrate = (mode == "burrow" and any(
                option["mode"] == mode and option["materials"] is None
                and option["speed"] * event.get("maxMoves", 1) >= needed
                for option in options))
            results[index] = {"id": event["id"], "mode": mode,
                              "status": ("unresolved-substrate"
                                         if uncertain_substrate else "not-supplied-by-origin"),
                              "reason": ("an intrinsic burrowing speed has no explicit substrate rules"
                                         if uncertain_substrate else
                                         "no eligible native mode, range, substrate, duration, or available use")}
        else:
            _, n = min(eligible)
            option, current = options[n], state[n]
            if option["maxUses"] is not None and current["activeUntil"] < ended:
                current["remaining"] -= 1
                current["activeUntil"] = started + option["duration"]
            results[index] = {"id": event["id"], "mode": mode,
                              "status": "origin-mode-satisfies-gate",
                              "source": option["source"], "speedFeet": option["speed"]}
    return results


def evaluate(profile: dict, catalog: dict, level: int,
             scenario: dict | None = None) -> dict:
    """A conservative subset of verified mechanics, never a total power price."""
    scenario = copy.deepcopy(DEFAULT_SCENARIO if scenario is None else scenario)
    validate_scenario(scenario)
    intrinsics, traits = profile["intrinsics"], profile["traits"]
    pb = proficiency_bonus(level)
    metrics: dict[str, float] = defaultdict(float)
    modeled, unmodeled, future = [], [], []
    resistances: dict[str, float] = {}
    skill_bonuses: dict[str, int] = defaultdict(int)
    conditions = []
    breath = []
    breath_events = []
    spell_values, spell_trace, spell_issues, modeled_spells, spell_entitlements = spell_demand_outcomes(
        traits, level, pb, scenario)
    control_values, control_trace, control_issues, modeled_control = metallic_control_outcomes(
        traits, catalog, level, scenario)
    metrics.update(spell_values)
    metrics.update(control_values)
    strike_values, strike_trace, strike_issues, modeled_strikes, strike_spent = (
        on_hit_control_outcomes(traits, catalog, level, scenario))
    if scenario.get("strikeOpportunities"):
        metrics.update(strike_values)
    unmodeled.extend(spell_issues + control_issues + strike_issues)
    modeled_movement = {opt["source"] for opt in _movement_options(profile, level, pb)}
    modeled_reactions = {d["traitId"] for d in _reaction_defenses(traits, level, pb)}
    modeled_tactical = {identifier for identifier, trait in traits.items()
                        if unlocked(trait, level) and _tactical_kind(trait) is not None
                        and any(e["traitId"] == identifier
                                for e in scenario.get("tacticalOpportunities", []))}
    shared_frontiers = native_shared_pool_frontiers(traits, level)
    modeled_shared_links = {f["alternativeTraitId"] for f in shared_frontiers}
    for identifier, trait in traits.items():
        if not unlocked(trait, level):
            future.append(identifier)
            continue
        recognized = False
        interpreted = {"requiredCharacterLevel", "level"}
        if "hitPointsPerCharacterLevel" in trait:
            metrics["maximum_hit_points_added"] += trait["hitPointsPerCharacterLevel"] * level
            recognized = True
            interpreted.add("hitPointsPerCharacterLevel")
        if "additionalRanksAtCharacterLevel1" in trait:
            metrics["additional_skill_ranks_entitled"] += (
                trait["additionalRanksAtCharacterLevel1"] +
                (level - 1) * trait.get("additionalRanksPerSubsequentCharacterLevel", 0))
            recognized = True
            interpreted.update(("additionalRanksAtCharacterLevel1", "additionalRanksPerSubsequentCharacterLevel"))
        if "skillBonuses" in trait:
            for skill, bonus in trait["skillBonuses"].items():
                skill_bonuses[skill] += bonus
            recognized = True
            interpreted.add("skillBonuses")
        if "damageResistance" in trait:
            values = _resistance(trait, intrinsics)
            for type_, factor in values.items():
                resistances[type_] = max(resistances.get(type_, 0), factor)
            if values:
                recognized = True
                interpreted.add("damageResistance")
        if "savingThrows" in trait:
            spec = trait["savingThrows"]
            if (isinstance(spec, dict) and
                    not set(spec) - {"advantageAgainst", "advantageAbilities", "scope"} and
                    isinstance(spec.get("advantageAgainst", []), list) and
                    isinstance(spec.get("advantageAbilities", []), list) and
                    spec.get("scope") in (None, "magic-only")):
                recognized = True
                interpreted.add("savingThrows")
        if identifier in modeled_reactions:
            recognized = True
            interpreted.update(("activation", "uses"))
            if "damageReduction" in trait:
                interpreted.add("damageReduction")
            elif "damageResistance" in trait:
                interpreted.add("damageResistance")
        if identifier in modeled_shared_links:
            recognized = True
            interpreted.update(("sharedUsesFromTrait", "extraDamage"))
        if identifier in modeled_spells:
            recognized = True
            interpreted.update(("spell", "atWill", "freeUses", "recharge",
                                "slotCasting", "alwaysPrepared", "freeCastSpellLevel"))
        if "spellList" in trait:
            unmodeled.append({"traitId": identifier,
                              "reason": "unresolved-spell-list-selection"})
        if trait.get("grantType") == "feat-choice" and trait.get("featCategory") == "origin":
            count = trait.get("choiceCount")
            if type(count) is int and count >= 0:
                metrics["origin_feat_choice_entitlements"] += count
                recognized = True
                interpreted.update(("grantType", "featCategory", "choiceCount", "when"))
                unmodeled.append({"traitId": identifier,
                                  "reason": "unresolved-origin-feat-effects"})
        if trait.get("grantType") == "skill-proficiency" and trait.get("chooseFrom") == "any-native-skill":
            count = trait.get("choiceCount")
            if type(count) is int and count >= 0:
                metrics["skill_proficiency_choice_entitlements"] += count
                recognized = True
                interpreted.update(("grantType", "chooseFrom", "choiceCount", "when"))
                unmodeled.append({"traitId": identifier,
                                  "reason": "unresolved-selected-skill"})
        if identifier in modeled_tactical:
            recognized = True
            interpreted.update(("activation", "uses", "oncePerTurn", "requiresItem",
                                "attack", "damage", "savingThrow", "applyConditionOnFailedSave",
                                "area", "whileActive", "startsTurnInside", "duration"))
        if identifier in modeled_strikes:
            recognized = True
            interpreted.update(("trigger", "oncePerTurn", "uses", "sharedUsesFromTrait",
                                "extraUsesGranted", "extraDamage", "onFailedSave",
                                "applyCondition", "applyConditionOnFailedSave",
                                "additionalSavingThrow", "targetMaximumSize",
                                "alternateStrikeMode", "speedReduction", "savingThrow"))
        if identifier in modeled_control:
            recognized = True
            interpreted.update(("activation", "uses", "area", "choiceTiming", "options"))
            unmodeled.append({"traitId": identifier,
                              "reason": "control-action-cost-and-duration-unvalued"})
        if "condition" in trait and trait["condition"] in catalog["conditions"]:
            conditions.append(trait["condition"])
            recognized = True
            interpreted.add("condition")
        if (isinstance(trait.get("damage"), dict) and
                "progression" in trait["damage"] and "areaFrom" in trait and
                trait.get("activation", {}).get("replaces") == "one-attack"):
            breath.append(trait)
            recognized = True
            interpreted.update(("damage", "areaFrom", "uses"))
        if trait.get("sense") == "tremorsense":
            recognized = True
            interpreted.update(("sense", "rangeFeet", "requiresContact",
                                "requiresSharedContact", "activation", "uses"))
        if "movementMode" in trait or "flyingSpeed" in trait:
            if identifier in modeled_movement:
                recognized = True
                interpreted.update(("movementMode", "flyingSpeed", "speedFeet",
                                    "duration", "durationMinutes", "allowedMaterials",
                                    "canHover", "uses", "atWill"))
            else:
                unmodeled.append({"traitId": identifier,
                                  "reason": "unresolved-movement-definition"})
        unknown_fields = sorted(set(trait) - interpreted -
                                {"id", "name", "description"})
        if recognized:
            modeled.append(identifier)
            if unknown_fields:
                unmodeled.append({"traitId": identifier,
                                  "reason": "unmodeled-effect-fields",
                                  "fields": unknown_fields})
        else:
            unmodeled.append({"traitId": identifier, "reason": "not-modeled"})
    tactical_values, tactical_trace, tactical_issues, _ = (
        tactical_action_outcomes(traits, catalog, level, pb, scenario, resistances))
    if scenario.get("tacticalOpportunities"):
        metrics.update(tactical_values)
    unmodeled.extend(tactical_issues)
    for identifier, trait in traits.items():
        if not unlocked(trait, level) or trait.get("id") not in (
                "stone-throw", "searing-ignition", "maelstrom-aura"):
            continue
        if identifier not in modeled_tactical and not any(
                issue.get("traitId") == identifier for issue in tactical_issues):
            unmodeled.append({"traitId": identifier,
                              "reason": "tactical-ability-not-evaluated-in-this-scenario"})
    for event in scenario.get("incomingDamage", []):
        fraction = resistances.get(event["type"], 0)
        metrics["incoming_damage_prevented"] += event["amount"] * fraction
    for skill, count in scenario.get("skillChecks", {}).items():
        if skill in skill_bonuses:
            metrics["additional_expected_skill_successes"] += count * roll_change(
                scenario.get("baseCheckSuccess", 0.5), skill_bonuses[skill])
    for condition in conditions:
        definition = catalog["conditions"][condition]
        frequency = scenario.get("conditionFrequency", {}).get(condition)
        if frequency is None:
            unmodeled.append({"conditionId": condition, "reason": "unknown-exposure-frequency"})
            continue
        for effect in definition.get("effects", []):
            if effect.get("type") != "roll-modifier" or effect.get("operation") != "add":
                unmodeled.append({"conditionId": condition, "reason": "unmodeled-condition-effect"})
                continue
            rolls = scenario.get("rolls", {})
            if effect.get("appliesTo") == "attack-roll":
                count = rolls.get("attack-roll", 0)
            else:
                count = sum(rolls.get(s, 0) for s in effect.get("skills", []))
            metrics["condition_expected_success_delta"] += frequency * count * roll_change(
                scenario.get("baseCheckSuccess", 0.5), effect["value"])
    for trait in breath:
        uses = trait.get("uses", {}).get("value")
        if uses not in ("proficiency-bonus", 1, 2, 3, 4, 5, 6):
            unmodeled.append({"reason": "unresolved-breath-pool", "value": uses})
            continue
        budget = pb if uses == "proficiency-bonus" else uses
        gain, charges, trace = _breath_damage(trait, intrinsics, level, scenario, budget)
        breath_events.append({"traitId": next((k for k, v in traits.items() if v is trait), None),
                              "events": trace})
        metrics["breath_expected_net_damage_delta"] += gain
        metrics["breath_uses_selected"] += charges
    save_values, save_trace, save_issues = saving_throw_outcomes(
        traits, level, scenario, resistances)
    if scenario.get("saveEvents"):
        metrics.update(save_values)
    unmodeled.extend(issue for issue in save_issues if issue not in unmodeled)
    reaction_prevented, reaction_trace, uncertain_defenses = reaction_damage_outcomes(
        traits, level, pb, scenario, resistances)
    if scenario.get("reactionDamageEvents"):
        metrics["reaction_damage_prevented"] = reaction_prevented
        metrics["reaction_uses_selected"] = sum(
            entry["selectedDefense"] is not None for entry in reaction_trace)
    # A shared offensive/defensive pool is not two independent budgets.
    # The reaction model optimizes defensively; the confirmed-hit model
    # replays attack choices. Do not present both as one realized plan.
    if scenario.get("reactionDamageEvents") and strike_spent:
        defenses_spent = {x["selectedDefense"] for x in reaction_trace
                          if x["selectedDefense"] is not None}
        for owner in sorted(set(strike_spent) & defenses_spent):
            unmodeled.append({"traitId": owner,
                              "reason": "joint-offense-defense-resource-plan-unresolved"})
    for identifier in uncertain_defenses:
        unmodeled.append({"traitId": identifier,
                          "reason": "resistance-and-reduction-order-unresolved"})
    metrics["tremorsense_contacts_detected"] = _tremorsense_contacts(
        traits, level, pb, scenario)
    mobility = mobility_access(profile, level, pb, scenario)
    metrics["origin_mobility_gates_satisfied"] = sum(
        e["status"] == "origin-mode-satisfies-gate" for e in mobility)
    joint_plans = joint_combat_plan_outcomes(profile, catalog, level, scenario, resistances)
    for plan in joint_plans:
        unmodeled.extend(plan["unresolved"])
    class_plans = native_class_action_outcomes(profile, catalog, level, scenario, resistances)
    for plan in class_plans:
        unmodeled.extend(plan["unresolved"])
    # Carry the intrinsic facts into the output instead of inventing a price for
    # movement, creature type, size, generic vision or other access gates.
    return {"sourceId": profile.get("selectedSourceId"), "level": level,
            "scenarioId": scenario.get("id"),
            "metrics": dict(sorted(metrics.items())),
            "intrinsicContext": intrinsics,
            "unmodeledIntrinsicFields": sorted(set(intrinsics) - {
                "walkingSpeedFeet", "climbingSpeedFeet", "swimmingSpeedFeet",
                "burrowingSpeedFeet", "breathWeaponArea", "damageType"}),
            "mobilityGateResults": mobility, "breathEventTrace": breath_events,
            "savingThrowEventTrace": save_trace,
            "reactionEventTrace": reaction_trace,
            "innateSpellDemandTrace": spell_trace,
            "innateFixedSpellEntitlements": spell_entitlements,
            "controlEventTrace": control_trace,
            "strikeEventTrace": strike_trace,
            "tacticalEventTrace": tactical_trace,
            "jointCombatPlanResults": joint_plans,
            "nativeClassActionPlanResults": class_plans,
            "sharedResourceFrontiers": shared_frontiers,
            "modeledTraits": sorted(modeled), "unmodeled": unmodeled,
            "committedFutureTraits": sorted(future)}


def endpoint_difference(a: dict, b: dict) -> dict[str, float]:
    """Subtract comparable endpoints; never sum unlike units."""
    am, bm = a["metrics"], b["metrics"]
    return {key: am.get(key, 0) - bm.get(key, 0)
            for key in sorted(set(am) | set(bm))}


def interaction(both: dict, a: dict, b: dict, neither: dict) -> dict[str, float]:
    """Outcome(AB) - outcome(A) - outcome(B) + outcome(neither)."""
    keys = set().union(*(r["metrics"].keys() for r in (both, a, b, neither)))
    return {key: (both["metrics"].get(key, 0) - a["metrics"].get(key, 0)
                  - b["metrics"].get(key, 0) + neither["metrics"].get(key, 0))
            for key in sorted(keys)}


def pareto_frontier(points: list[dict], endpoints: tuple[str, ...]) -> list[dict]:
    """Return nondominated points with independent, maximized endpoints."""
    return [p for p in points if not any(
        all(q[k] >= p[k] for k in endpoints) and any(q[k] > p[k] for k in endpoints)
        for q in points)]


def shared_pool_frontier(base: dict, alternative: dict, level: int,
                         ability_modifier: int = 3, save_failure: float = 0.55) -> list[dict]:
    """Model one explicitly shared pool using damage/mitigation opportunity units.

    This is an optimistic two-option frontier with sufficient eligible triggers,
    not an encounter simulation or a conversion of mitigation into damage.
    """
    if alternative.get("sharedUsesFromTrait") != base.get("id"):
        raise ValueError("Alternative does not consume this base trait's pool")
    uses = base.get("uses", {}).get("value")
    count = proficiency_bonus(level) if uses == "proficiency-bonus" else uses
    if not isinstance(count, int) or count < 0:
        raise ValueError("Cannot evaluate unknown shared pool size")
    prevention = dice_mean(base["damageReduction"]["dice"]) + ability_modifier
    strike = dice_mean(alternative["extraDamage"]["dice"])
    points = [{"defensiveUses": n, "offensiveUses": count - n,
               "expectedDamagePrevented": n * prevention,
               "expectedExtraDamageOnHits": (count - n) * strike,
               "expectedSuccessfulPushes": (count - n) * save_failure
               if "onFailedSave" in alternative else 0.0}
              for n in range(count + 1)]
    return pareto_frontier(points, ("expectedDamagePrevented", "expectedExtraDamageOnHits",
                                    "expectedSuccessfulPushes"))


def validate_scenario_suite(suite: dict) -> None:
    """Require explicitly identified, distinct diagnostic fixtures.

    The suite has no sampling weights. No observed campaign prevalence or
    cross-domain exchange rate is implied by running all its scenarios.
    """
    if not isinstance(suite, dict):
        raise ValueError("Scenario suite must be an object")
    unexpected = set(suite) - {"id", "version", "contexts"}
    if unexpected:
        raise ValueError(f"Unknown scenario-suite fields: {sorted(unexpected)}")
    _nonempty(suite.get("id"), "suite.id")
    if type(suite.get("version")) is not int or suite["version"] not in (1, 2, 3, 4, 5, 6, 7, 8, 9):
        raise ValueError("suite.version must be 1 through 9")
    contexts = suite.get("contexts")
    if not isinstance(contexts, list) or len(contexts) < 2:
        raise ValueError("suite.contexts must include at least two scenarios")
    seen = set()
    for index, context in enumerate(contexts):
        location = f"contexts[{index}]"
        if not isinstance(context, dict):
            raise ValueError(f"{location} must be an object")
        extra = set(context) - {"id", "purpose", "scenario"}
        if extra:
            raise ValueError(f"Unexpected {location} fields: {sorted(extra)}")
        identifier = _nonempty(context.get("id"), f"{location}.id")
        if identifier in seen:
            raise ValueError(f"Duplicate context ID: {identifier}")
        seen.add(identifier)
        _nonempty(context.get("purpose"), f"{location}.purpose")
        scenario = context.get("scenario")
        validate_scenario(scenario)
        if scenario.get("id") != identifier:
            raise ValueError(f"{location}.scenario.id must match context ID")


def _suite_coverage(results: list[dict]) -> dict:
    """Aggregate unresolved mechanics without treating them as zero-valued."""
    incomplete = set()
    fully_modeled = set()
    intrinsic_unknown = set()
    future = set()
    for result in results:
        fully_modeled.update(result["modeledTraits"])
        future.update(result["committedFutureTraits"])
        intrinsic_unknown.update(result["unmodeledIntrinsicFields"])
        for issue in result["unmodeled"]:
            if "traitId" in issue:
                incomplete.add(issue["traitId"])
            elif "conditionId" in issue:
                incomplete.add(issue["conditionId"])
            else:
                incomplete.add("unattributed: " + str(issue.get("reason")))
    return {"fullyModeledTraits": sorted(fully_modeled - incomplete),
            "incompleteOrUnsupportedMechanics": sorted(incomplete),
            "unmodeledIntrinsicFields": sorted(intrinsic_unknown),
            "futureTraitIds": sorted(future),
            "isCompleteForCurrentLevel": not incomplete and not intrinsic_unknown}


def evaluate_scenario_suite(catalog: dict, suite: dict, identifiers: list[str],
                            compare_source: str | None = None) -> dict:
    """Replay all profiles and breakpoints against the same named contexts.

    Each scenario's results and each within-endpoint sensitivity range retain
    native units. Do not assign frequencies, sum endpoints, or rank Species.
    """
    validate_scenario_suite(suite)
    profiles = {identifier: reconstruct_preset(catalog, identifier)
                for identifier in identifiers}
    counterfactual = (reconstruct_preset(catalog, compare_source)
                      if compare_source else None)
    outputs = []
    all_outcomes: dict[str, dict[int, list[tuple[str, dict]]]] = {
        identifier: {level: [] for level in LEVELS} for identifier in identifiers}
    for context in suite["contexts"]:
        scenario = context["scenario"]
        comparison = []
        profile_rows = []
        for identifier, profile in profiles.items():
            levels = [evaluate(profile, catalog, level, scenario) for level in LEVELS]
            for result in levels:
                all_outcomes[identifier][result["level"]].append((context["id"], result))
            profile_rows.append({"id": identifier, "lineage": profile["lineage"],
                                 "levels": levels})
            if counterfactual:
                baseline = [evaluate(counterfactual, catalog, level, scenario)
                            for level in LEVELS]
                comparison.append({
                    "source": identifier, "counterfactual": compare_source,
                    "levels": [
                        {"level": level, "pairedEndpointDifferences": endpoint_difference(a, b),
                         "sourceUnmodeled": a["unmodeled"],
                         "counterfactualUnmodeled": b["unmodeled"]}
                        for level, a, b in zip(LEVELS, levels, baseline)]})
        row = {"id": context["id"], "purpose": context["purpose"],
               "scenario": scenario, "profiles": profile_rows}
        if comparison:
            row["comparisons"] = comparison
        outputs.append(row)

    sensitivity = []
    for identifier, levels in all_outcomes.items():
        summaries = []
        for level, observations in levels.items():
            names = sorted(set().union(*(r["metrics"].keys() for _, r in observations)))
            ranges = {}
            for endpoint in names:
                # An omitted endpoint means *not evaluated*, not observed zero.
                # A measured zero remains meaningful when an explicit fixture
                # provides a qualifying opportunity but the ability has no effect.
                vals = [(context_id, result["metrics"][endpoint])
                        for context_id, result in observations
                        if endpoint in result["metrics"]]
                omitted = [context_id for context_id, result in observations
                           if endpoint not in result["metrics"]]
                low = min(value for _, value in vals)
                high = max(value for _, value in vals)
                ranges[endpoint] = {
                    "minimum": low, "maximum": high,
                    "minimumContexts": [name for name, val in vals if val == low],
                    "maximumContexts": [name for name, val in vals if val == high],
                    "evaluatedContextCount": len(vals),
                    "notEvaluatedContexts": omitted}
            summaries.append({"level": level, "rangesInNativeUnits": ranges,
                              "coverage": _suite_coverage([r for _, r in observations])})
        sensitivity.append({"sourceId": identifier, "levels": summaries})
    return {
        "status": "research-prototype-not-gameplay-rule",
        "suiteId": suite["id"],
        "catalogTraitCount": len(catalog["traits"]),
        "terminalProfileCount": len(identifiers),
        "levelBreakpoints": LEVELS,
        "scenarioCount": len(outputs),
        "warnings": [
            "Diagnostic scenes are not a sampled campaign distribution; no frequencies or weights are inferred.",
            "Endpoint ranges are within-domain sensitivity across fixtures, not whole-Species rankings.",
            "Unsupported mechanics and unresolved choices prevent complete comparative power judgments.",
        ],
        "contexts": outputs,
        "sourceSensitivity": sensitivity,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--native", type=Path, default=DEFAULT_NATIVE)
    scenarios = parser.add_mutually_exclusive_group()
    scenarios.add_argument("--scenario", type=Path, help="Explicit JSON scenario overrides default")
    scenarios.add_argument("--scenario-suite", type=Path, help="Replay a versioned suite of diagnostic scenarios")
    parser.add_argument("--include-deferred", action="store_true")
    parser.add_argument("--source", help="Evaluate one direct preset ID instead of all terminal paths")
    parser.add_argument("--compare-source", help="Paired comparison against another complete preset; requires --source")
    parser.add_argument("--output", type=Path, help="Save JSON rather than write to stdout")
    args = parser.parse_args(argv)
    if args.compare_source and not args.source:
        parser.error("--compare-source requires --source")
    catalog = load_native(args.native)
    if args.scenario_suite:
        suite = json.loads(args.scenario_suite.read_text(encoding="utf-8"))
        identifiers = [args.source] if args.source else terminal_sources(catalog, args.include_deferred)
        output = evaluate_scenario_suite(catalog, suite, identifiers, args.compare_source)
        formatted = json.dumps(output, indent=2, sort_keys=True) + "\n"
        if args.output:
            args.output.write_text(formatted, encoding="utf-8")
        else:
            print(formatted, end="")
        return 0
    scenario = (json.loads(args.scenario.read_text(encoding="utf-8"))
                if args.scenario else DEFAULT_SCENARIO)
    validate_scenario(scenario)
    identifiers = [args.source] if args.source else terminal_sources(catalog, args.include_deferred)
    results = []
    for identifier in identifiers:
        profile = reconstruct_preset(catalog, identifier)
        results.append({"id": identifier, "lineage": profile["lineage"],
                        "levels": [evaluate(profile, catalog, level, scenario) for level in LEVELS]})
    output = {"status": "research-prototype-not-gameplay-rule",
              "catalogTraitCount": len(catalog["traits"]),
              "terminalProfileCount": len(results),
              "levelBreakpoints": LEVELS, "scenario": scenario, "profiles": results}
    if args.compare_source:
        other = reconstruct_preset(catalog, args.compare_source)
        left = results[0]["levels"]
        right = [evaluate(other, catalog, level, scenario) for level in LEVELS]
        output["comparison"] = {
            "source": args.source, "counterfactual": args.compare_source,
            "pairedEndpointDifferences": [
                {"level": level, "difference": endpoint_difference(a, b),
                 "sourceUnmodeled": a["unmodeled"],
                 "counterfactualUnmodeled": b["unmodeled"]}
                for level, a, b in zip(LEVELS, left, right)],
            "warning": "Context-specific partial endpoints only; not total Species power.",
        }
    formatted = json.dumps(output, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.write_text(formatted, encoding="utf-8")
    else:
        print(formatted, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
