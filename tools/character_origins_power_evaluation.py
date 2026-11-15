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
    "breathOpportunities": [
        {"areaTargets": {"line": 3, "cone": 1}},
        {"areaTargets": {"line": 1, "cone": 3}},
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
    entries = conditions.get("entries", []) if isinstance(conditions, dict) else conditions
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
                   scenario: dict, remaining: int) -> tuple[float, int]:
    """Evaluate authored Attack-replacement area attacks, not every attack trait."""
    progression = trait["damage"]["progression"]
    eligible = [step for step in progression if level >= step["characterLevel"]]
    if not eligible:
        return 0.0, 0
    dice = dice_mean(eligible[-1]["dice"])
    area = intrinsics.get("breathWeaponArea", {})
    shape = area.get("shape") if isinstance(area, dict) else None
    if not shape:
        return 0.0, 0
    multiplier = scenario.get("damageTypeMultiplier", {}).get(intrinsics.get("damageType"), 1)
    values = []
    for event in scenario.get("breathOpportunities", []):
        count = event.get("areaTargets", {}).get(shape, 0)
        fail = event.get("saveFailure", scenario.get("saveFailure", 0.55))
        replacement = event.get("attackReplacementExpectedDamage",
                                scenario.get("attackReplacementExpectedDamage", 5.2))
        values.append(max(0.0, count * dice * (0.5 + 0.5 * fail) * multiplier - replacement))
    useful = sorted((v for v in values if v > 0), reverse=True)[:remaining]
    return sum(useful), len(useful)


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


def evaluate(profile: dict, catalog: dict, level: int,
             scenario: dict | None = None) -> dict:
    """A conservative subset of verified mechanics, never a total power price."""
    scenario = copy.deepcopy(DEFAULT_SCENARIO if scenario is None else scenario)
    intrinsics, traits = profile["intrinsics"], profile["traits"]
    pb = proficiency_bonus(level)
    metrics: dict[str, float] = defaultdict(float)
    modeled, unmodeled, future = [], [], []
    resistances: dict[str, float] = {}
    skill_bonuses: dict[str, int] = defaultdict(int)
    conditions = []
    breath = []
    for identifier, trait in traits.items():
        if not unlocked(trait, level):
            future.append(identifier)
            continue
        recognized = False
        if "hitPointsPerCharacterLevel" in trait:
            metrics["maximum_hit_points_added"] += trait["hitPointsPerCharacterLevel"] * level
            recognized = True
        if "additionalRanksAtCharacterLevel1" in trait:
            metrics["additional_skill_ranks_entitled"] += (
                trait["additionalRanksAtCharacterLevel1"] +
                (level - 1) * trait.get("additionalRanksPerSubsequentCharacterLevel", 0))
            recognized = True
        if "skillBonuses" in trait:
            for skill, bonus in trait["skillBonuses"].items():
                skill_bonuses[skill] += bonus
            recognized = True
        if "damageResistance" in trait:
            values = _resistance(trait, intrinsics)
            for type_, factor in values.items():
                resistances[type_] = max(resistances.get(type_, 0), factor)
            recognized = bool(values)
        if "condition" in trait and trait["condition"] in catalog["conditions"]:
            conditions.append(trait["condition"])
            recognized = True
        if (isinstance(trait.get("damage"), dict) and
                "progression" in trait["damage"] and "areaFrom" in trait and
                trait.get("activation", {}).get("replaces") == "one-attack"):
            breath.append(trait)
            recognized = True
        if trait.get("sense") == "tremorsense":
            recognized = True
        if recognized:
            modeled.append(identifier)
            # Certain complex traits have additional unmodeled effects.
            if any(k in trait for k in ("damageResistanceOverride", "savingThrows",
                                         "freeUses", "spell", "sharedUsesFromTrait")):
                unmodeled.append({"traitId": identifier, "reason": "partially-modeled"})
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
        gain, charges = _breath_damage(trait, intrinsics, level, scenario, budget)
        metrics["breath_expected_net_damage_delta"] += gain
        metrics["breath_uses_selected"] += charges
    metrics["tremorsense_contacts_detected"] = _tremorsense_contacts(
        traits, level, pb, scenario)
    # Carry the intrinsic facts into the output instead of inventing a price for
    # movement, creature type, size, generic vision or other access gates.
    return {"sourceId": profile.get("selectedSourceId"), "level": level,
            "scenarioId": scenario.get("id"),
            "metrics": dict(sorted(metrics.items())),
            "intrinsicContext": intrinsics,
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


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--native", type=Path, default=DEFAULT_NATIVE)
    parser.add_argument("--scenario", type=Path, help="Explicit JSON scenario overrides default")
    parser.add_argument("--include-deferred", action="store_true")
    parser.add_argument("--source", help="Evaluate one direct preset ID instead of all terminal paths")
    parser.add_argument("--compare-source", help="Paired comparison against another complete preset; requires --source")
    parser.add_argument("--output", type=Path, help="Save JSON rather than write to stdout")
    args = parser.parse_args(argv)
    if args.compare_source and not args.source:
        parser.error("--compare-source requires --source")
    catalog = load_native(args.native)
    scenario = (json.loads(args.scenario.read_text(encoding="utf-8"))
                if args.scenario else DEFAULT_SCENARIO)
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
