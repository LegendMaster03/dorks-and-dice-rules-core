"""Experimental, read-only universal characteristic facet inventory.

This produces evidence for the Pillar 2 design review. It does not authorize
character grants, select inherited traits, resolve source parameters, stack
modifiers, or implement an executable character. Facet names are provisional
shared mechanical *dimensions*, not finalized trait/effect identities.

Every authored field remains available in the source-preserving inventory.
Unknown fields are made explicit rather than guessed or dropped.
"""
import argparse
import copy
import json
from collections import defaultdict

from character_origins_source_inventory import read_catalog, source_inventory


# Candidate identities correspond to mechanical dimensions, not grants.
# No dispatch on Species, Ancestry, or trait name is permitted.
INTRINSIC_FACETS = {
    "creatureType": "creature.type",
    "size": "creature.size",
    "sizeChoice": "creature.size-choice",
    "walkingSpeedFeet": "movement.walk.speed",
    "climbingSpeedFeet": "movement.climb.speed",
    "swimmingSpeedFeet": "movement.swim.speed",
    "burrowingSpeedFeet": "movement.burrow.speed",
    "darkvisionFeet": "sense.darkvision.range",
    "breathWeaponArea": "parameter.breath-area",
    "damageType": "parameter.damage-type",
    "spellcastingAbilityChoice": "spellcasting.ability-choice",
    "needs": "creature.biological-needs",
    "ancestrySelection": "origin.ancestry-selection",
}

TRAIT_FACETS = {
    "rule": "mechanic.rule-reference",
    "activation": "ability.activation",
    "uses": "ability.uses",
    "requiredCharacterLevel": "ability.minimum-character-level",
    "level": "ability.level-as-authored",
    "spell": "spellcasting.spell-reference",
    "spellList": "spellcasting.spell-list-reference",
    "spellcastingAbilityFrom": "spellcasting.ability-parameter",
    "savingThrows": "saving-throw.modifier",
    "savingThrow": "saving-throw.requirement",
    "skillBonuses": "skill.bonus",
    "grantType": "entitlement.grant-type",
    "chooseFrom": "entitlement.choice-pool",
    "choiceCount": "entitlement.choice-count",
    "featCategory": "entitlement.feat-category",
    "additionalRanksAtCharacterLevel1": "skill-rank.additional-at-level-one",
    "additionalRanksPerSubsequentCharacterLevel": "skill-rank.additional-per-level",
    "obeysNormalSkillRankLimits": "skill-rank.enforces-normal-limits",
    "damageResistance": "defense.damage-resistance",
    "damageResistanceOverride": "defense.damage-resistance-override",
    "hitPointsPerCharacterLevel": "health.hit-points-per-character-level",
    "longRestHours": "rest.long-rest-hours",
    "sleepRequired": "creature.sleep-required",
    "effectiveSizeCategoriesHigherForCarryingCapacity": "carrying.effective-size-categories",
    "effectiveSizeCategoriesHigherForPushingDraggingLifting": "lifting.effective-size-categories",
    "advantageOnAbilityChecksToEndCondition": "condition.escape-advantage",
    "movementMode": "movement.mode",
    "speedFeet": "movement.mode-speed",
    "sense": "sense.kind",
    "rangeFeet": "ability.range-feet",
    "canBreathe": "creature.breathing-media",
    "area": "ability.area",
    "areaFrom": "ability.area-parameter",
    "damage": "ability.damage",
    "damageType": "ability.damage-type",
    "trigger": "ability.trigger",
    "targetMaximumSize": "ability.target-maximum-size",
    "duration": "ability.duration",
    "durationMinutes": "ability.duration-minutes",
    "atWill": "ability.at-will",
    "alwaysPrepared": "spellcasting.always-prepared",
    "freeUses": "spellcasting.free-uses",
    "recharge": "ability.recharge",
    "slotCasting": "spellcasting.slot-casting",
    "spellcastingAbilityChoice": "spellcasting.ability-choice",
    "abilityChoice": "ability.score-choice",
}

TRAIT_METADATA = frozenset(("id", "name", "description"))
MIRRORED_MOVEMENT = {
    "climbingSpeedFeet": "climb",
    "swimmingSpeedFeet": "swim",
    "burrowingSpeedFeet": "burrow",
}


def _facet(origin, native_field, value, candidate_map):
    """Generate an inspectable facet without changing or interpreting values."""
    candidate = candidate_map.get(native_field)
    return {
        "origin": origin,
        "nativeField": native_field,
        "candidateSemanticId": candidate,
        "classification": ("mapped-candidate" if candidate else "unmapped"),
        "authoredValue": copy.deepcopy(value),
    }


def _parameter_references(value, location=""):
    """Expose symbolic bindings as unresolved; never infer their target."""
    if isinstance(value, dict):
        for key, child in value.items():
            subpath = f"{location}.{key}" if location else key
            if isinstance(child, str) and (
                key.endswith("From")
                or key in ("typeFrom", "areaFrom")
                or child.startswith(("selected-", "chosen-"))
            ):
                yield {
                    "sourcePath": subpath,
                    "authoredReference": child,
                    "resolution": "unbound",
                }
            yield from _parameter_references(child, subpath)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from _parameter_references(child, f"{location}[{index}]")


def project_facets(selected_id, catalog):
    """Produce an audit projection for a directly selectable standard option.

    The returned observations are *not* a character sheet, resolved gameplay
    effects, an eligibility decision, or a post-inheritance trait allocation.
    """
    inventory = source_inventory(selected_id, catalog)
    sources = []
    unresolved_bindings = []
    diagnostics = []

    for entry in inventory["lineage"]:
        source_id = entry["sourceId"]
        intrinsic = entry["intrinsicFields"]
        fields = [
            _facet(source_id, name, value, INTRINSIC_FACETS)
            for name, value in intrinsic.items()
        ]
        unresolved_bindings.extend(
            {"sourceId": source_id, **ref}
            for ref in _parameter_references(intrinsic)
        )

        trait_items = []
        for occurrence in entry["traits"]:
            trait = occurrence["definition"]
            mechanical_fields = {
                key: value for key, value in trait.items()
                if key not in TRAIT_METADATA
            }
            trait_items.append({
                "sourceOccurrenceId": occurrence["occurrenceId"],
                "localTraitId": occurrence["localTraitId"],
                "candidateFacets": [
                    _facet(occurrence["occurrenceId"], key, value, TRAIT_FACETS)
                    for key, value in mechanical_fields.items()
                ],
                "reviewRequired": (
                    ["prose-only-effect"] if not mechanical_fields else []
                ),
            })
            unresolved_bindings.extend({
                "sourceId": source_id,
                "sourceOccurrenceId": occurrence["occurrenceId"],
                **ref,
            } for ref in _parameter_references(mechanical_fields))

        for top_field, movement_mode in MIRRORED_MOVEMENT.items():
            if top_field not in intrinsic:
                continue
            for occurrence in entry["traits"]:
                trait = occurrence["definition"]
                if (trait.get("movementMode") == movement_mode
                        and "speedFeet" in trait):
                    if intrinsic[top_field] == trait["speedFeet"]:
                        issue = "mirrored-movement-same-value"
                    else:
                        issue = "mirrored-movement-conflict"
                    diagnostics.append({
                        "kind": issue,
                        "sourceId": source_id,
                        "sourceOccurrenceId": occurrence["occurrenceId"],
                        "field": top_field,
                        "topLevelValue": intrinsic[top_field],
                        "traitValue": trait["speedFeet"],
                        "action": "review-before-effective-grant-projection",
                    })

        sources.append({
            "sourceId": source_id,
            "intrinsicFacets": fields,
            "traits": trait_items,
        })

    return {
        "selectedSourceId": selected_id,
        "sources": sources,
        "symbolicBindings": unresolved_bindings,
        "diagnostics": diagnostics,
        "evaluationStatus": "not-evaluated",
    }


def analyze_shared_trait_ids(catalog):
    """Detect names reused across sources, without conflating their effects."""
    occurrences = defaultdict(list)
    for source_id in sorted(catalog):
        source = catalog[source_id]
        for trait in source.get("traits", []):
            definition = {
                key: value for key, value in trait.items()
                if key not in TRAIT_METADATA
            }
            occurrences[trait["id"]].append({
                "sourceId": source_id,
                "sourceOccurrenceId": f"{source_id}#trait:{trait['id']}",
                "mechanicalFields": definition,
            })
    findings = []
    for local_id, rows in sorted(occurrences.items()):
        if len(rows) < 2:
            continue
        original = rows[0]["mechanicalFields"]
        same = all(row["mechanicalFields"] == original for row in rows[1:])
        findings.append({
            "localTraitId": local_id,
            "occurrenceIds": [row["sourceOccurrenceId"] for row in rows],
            "mechanicalFieldsIdentical": same,
            "classification": (
                "same-structured-fields" if same
                else "different-structured-fields"
            ),
        })
    return findings


def main():
    parser = argparse.ArgumentParser(
        description="Inspect provisional semantic facets without creating a character."
    )
    options = parser.add_mutually_exclusive_group(required=True)
    options.add_argument("--selected", metavar="SOURCE_ID")
    options.add_argument("--shared-ids", action="store_true")
    args = parser.parse_args()
    catalog = read_catalog()
    result = (
        analyze_shared_trait_ids(catalog) if args.shared_ids
        else project_facets(args.selected, catalog)
    )
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
