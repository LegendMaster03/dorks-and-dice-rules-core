"""Read-only, scenario-based Pillar 2 power evaluation prototype.

Source: docs/rewrite/character-origins/pillar2-species-power-evaluation.md.
This deliberately reports separate endpoints and unknown mechanics, never an
unjustified universal power score or authorization to inherit a trait.
"""
from __future__ import annotations

import argparse
from collections import defaultdict
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
    for field in ("incomingDamage", "breathOpportunities",
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
    return {"sources": sources, "traits": traits,
            "conditions": {v["id"]: v for v in entries}}


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
    modeled_movement = {opt["source"] for opt in _movement_options(profile, level, pb)}
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
            recognized = bool(values)
            if values:
                interpreted.add("damageResistance")
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
    metrics["tremorsense_contacts_detected"] = _tremorsense_contacts(
        traits, level, pb, scenario)
    mobility = mobility_access(profile, level, pb, scenario)
    metrics["origin_mobility_gates_satisfied"] = sum(
        e["status"] == "origin-mode-satisfies-gate" for e in mobility)
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
    if type(suite.get("version")) is not int or suite["version"] != 1:
        raise ValueError("suite.version must be 1")
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
                vals = [(context_id, result["metrics"].get(endpoint, 0))
                        for context_id, result in observations]
                low = min(value for _, value in vals)
                high = max(value for _, value in vals)
                ranges[endpoint] = {
                    "minimum": low, "maximum": high,
                    "minimumContexts": [name for name, val in vals if val == low],
                    "maximumContexts": [name for name, val in vals if val == high]}
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
