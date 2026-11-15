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
                         "controlOpportunities"):
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
    unmodeled.extend(spell_issues + control_issues)
    modeled_movement = {opt["source"] for opt in _movement_options(profile, level, pb)}
    modeled_reactions = {d["traitId"] for d in _reaction_defenses(traits, level, pb)}
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
    for identifier in uncertain_defenses:
        unmodeled.append({"traitId": identifier,
                          "reason": "resistance-and-reduction-order-unresolved"})
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
            "savingThrowEventTrace": save_trace,
            "reactionEventTrace": reaction_trace,
            "innateSpellDemandTrace": spell_trace,
            "innateFixedSpellEntitlements": spell_entitlements,
            "controlEventTrace": control_trace,
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
    if type(suite.get("version")) is not int or suite["version"] not in (1, 2, 3, 4):
        raise ValueError("suite.version must be 1, 2, 3 or 4")
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
