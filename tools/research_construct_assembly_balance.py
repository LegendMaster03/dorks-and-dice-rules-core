#!/usr/bin/env python3
"""Independent, read-only Construct Assembly balance diagnostics.

Evaluates native recipe ingredients and *reference-only* hypothetical physical
bounds for the historical sensitivity scenarios. The reference file, not native
component or material rules, owns invented gross-support and DR comparison values. This is not a creature simulator, a crafting transaction,
a power-network evaluator or an authorization path. No output is a rule grant.

Use from repository root:
    python3 tools/research_construct_assembly_balance.py --format markdown
    python3 tools/research_construct_assembly_balance.py --format json
"""
from __future__ import annotations

import argparse
import copy
import json
from collections import Counter
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"
REFERENCE = ROOT / "data/reference/construct-assembly-balance-candidates-20261120.json"
PARTS = NATIVE / "origination/construct-assembly"


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def load_catalog(root: Path = ROOT) -> dict[str, Any]:
    native = root / "data/rulesets/first-party-001/native"
    parts = native / "origination/construct-assembly"
    reference = read_json(
        root / "data/reference/construct-assembly-balance-candidates-20261120.json"
    )
    method = next(
        m for m in read_json(native / "rules/creature-origination.json")[
            "methodDefinitions"
        ] if m["id"] == "construct-assembly"
    )
    if method.get("initialConstructionCostModel") != "recipe-inputs":
        raise ValueError("Native Construct method does not use recipe-inputs")
    if method["startingResourceUnitCosts"] != reference["candidateResourceUnitCosts"]:
        raise ValueError("Reference resource prices differ from the native method")

    def index(name: str) -> dict[str, Any]:
        entries = read_json(parts / name)
        result = {entry["id"]: entry for entry in entries}
        if len(result) != len(entries):
            raise ValueError(f"Duplicate native IDs in {name}")
        return result

    return {
        "reference": reference,
        "prices": method["startingResourceUnitCosts"],
        "components": index("components.json"),
        "recipes": index("crafting-recipes.json"),
        "sources": index("power-sources.json"),
        "materials": {**index("materials.json"), **index("crafting-ingredients.json")},
    }


def _ordered_stages(recipe: dict[str, Any]) -> list[dict[str, Any]]:
    if "stages" not in recipe:
        return [{"stepId": "manufacture", "dependencies": [],
                 "inputs": recipe["inputs"], "outputs": []}]
    stages = recipe["stages"]
    by_id = {stage["stepId"]: stage for stage in stages}
    if len(by_id) != len(stages):
        raise ValueError(f"Duplicate manufacturing stage in {recipe['id']}")
    finished: set[str] = set()
    ordered = []
    while len(ordered) < len(stages):
        available = [stage for stage in stages
                     if stage["stepId"] not in finished and
                     all(dep in finished for dep in stage.get("dependencies", []))]
        if not available:
            raise ValueError(f"Cyclic or missing stage dependency: {recipe['id']}")
        stage = sorted(available, key=lambda x: x["stepId"])[0]
        ordered.append(stage)
        finished.add(stage["stepId"])
    return ordered


def _manufacture(
    catalog: dict[str, Any], definition_id: str, recipe_id: str,
    material_slots: dict[str, str],
) -> dict[str, Any]:
    recipe = catalog["recipes"].get(recipe_id)
    if recipe is None or recipe["producesDefinitionId"] != definition_id:
        raise ValueError(f"Unverified recipe for {definition_id}: {recipe_id}")

    stages = _ordered_stages(recipe)
    external: Counter[str] = Counter()
    internal: Counter[str] = Counter()
    retained_mass = 0
    mass_measured = True
    all_inputs_marked = True
    has_outputs = False

    for stage in stages:
        for line in stage.get("inputs", []):
            fixed = line.get("materialId")
            slot = line.get("materialSlot")
            if (fixed is None) == (slot is None):
                raise ValueError(f"Ambiguous ingredient in {recipe_id}")
            if slot is not None and slot not in material_slots:
                raise ValueError(f"Unknown material slot in {recipe_id}: {slot}")
            material_id = fixed if fixed is not None else material_slots[slot]
            if material_id not in catalog["materials"]:
                raise ValueError(f"Unknown native resource: {material_id}")
            amount = line["amount"]
            if type(amount) is not int or amount <= 0:
                raise ValueError(f"Invalid material amount in {recipe_id}")

            used = min(internal[material_id], amount)
            internal[material_id] -= used
            if amount > used:
                external[material_id] += amount - used
            all_inputs_marked &= "retainedInProduct" in line
            if line.get("retainedInProduct") is True:
                unit_mass = catalog["materials"][material_id].get("massPoundsPerUnit")
                if unit_mass is None:
                    mass_measured = False
                else:
                    retained_mass += amount * unit_mass

        for output in stage.get("outputs", []):
            has_outputs = True
            material_id = output["materialId"]
            if material_id not in catalog["materials"] or output["amount"] <= 0:
                raise ValueError(f"Invalid stage output in {recipe_id}")
            internal[material_id] += output["amount"]

    certificate = recipe.get("physicalMassAccounting")
    certified = False
    if certificate is not None:
        additional = certificate.get("additionalProductMassPounds")
        if (certificate.get("fullyAccounted") is not True or
                type(additional) is not int or additional < 0 or
                not all_inputs_marked or not mass_measured or has_outputs):
            raise ValueError(f"Invalid complete-mass certificate: {recipe_id}")
        retained_mass += additional
        certified = True
    return {"inputs": external, "mass": retained_mass, "certified": certified}


def evaluate(config: dict[str, Any], catalog: dict[str, Any]) -> dict[str, Any]:
    """Quote authored ingredients; report only nominal upper bounds for operation."""
    resources: Counter[str] = Counter()
    known_mass = 0
    mass_complete = True
    power_demand = 0
    nominal_output = 0
    drive_count = 0
    max_support = 0
    candidate_same_region_dr = 0
    sized: set[str] = set()
    instances: set[str] = set()
    for instance_id, short_id, material, source_id in config["components"]:
        if instance_id in instances:
            raise ValueError(f"Duplicate installed instance: {instance_id}")
        instances.add(instance_id)
        definition_id = "construct-component." + short_id
        definition = catalog["components"][definition_id]
        slots = definition.get("materialSlots", [])
        if len(slots) != 1:
            raise ValueError(f"Unsupported multi-slot reference fixture: {definition_id}")
        material_id = "construct-material." + material
        if material_id not in catalog["materials"]:
            raise ValueError(f"Unknown selected material: {material_id}")
        chosen_slots = {slots[0]: material_id}
        variants = config.get("bodyConstructionVariants", {})
        selected = variants.get(instance_id)
        recipe_id = definition["craftingRecipeId"]
        if selected is not None:
            matching = [v for v in definition.get("constructionVariants", [])
                        if v["id"] == selected]
            if len(matching) != 1:
                raise ValueError(f"Invalid construction variant: {instance_id}")
            variant = matching[0]
            if not definition.get("definesBodySize"):
                raise ValueError(f"Size not authorized by {definition_id}")
            sized.add(variant["bodySize"])
            recipe_id = variant["craftingRecipeId"]

        jobs = [(definition_id, recipe_id, chosen_slots)]
        if source_id is not None:
            if not definition.get("powerSourceSlot"):
                raise ValueError(f"No native power housing slot on {definition_id}")
            source = catalog["sources"]["construct-power-source." + source_id]
            jobs.append((source["id"], source["craftingRecipeId"], {}))
            nominal_output += source["powerOutput"]

        for product_id, job_id, job_slots in jobs:
            product = _manufacture(catalog, product_id, job_id, job_slots)
            resources.update(product["inputs"])
            known_mass += product["mass"]
            mass_complete &= product["certified"]

        power_demand += definition.get("powerDemand", 0)
        if "locomotion-interface" in definition.get("provides", []):
            drive_count += 1
            max_support += catalog["reference"]["candidateResearchOnlyPhysicalAssumptions"][
                "hypotheticalGrossLoadSupportPerDrivePounds"
            ]
        if "protective-surface" in definition.get("provides", []):
            # Upper bound only: sample rows do not author installed regions,
            # valid stacking, attacks, coverage, or armor class.
            candidate_same_region_dr += catalog["reference"][
                "candidateResearchOnlyPhysicalAssumptions"
            ]["hypotheticalFlatDamageReductionPerProtectiveLayer"][material_id]
    if len(sized) > 1:
        raise ValueError("Conflicting declared body sizes")
    unknown_prices = sorted(r for r, n in resources.items()
                            if n and r not in catalog["prices"])
    if unknown_prices:
        raise ValueError(f"Missing native ingredient prices: {unknown_prices}")
    cost = sum(n * catalog["prices"][r] for r, n in resources.items())
    allowance = catalog["reference"]["candidateStartingAllowance"]
    return {
        "id": config["id"],
        "resourceCost": cost,
        "candidateAllowanceHeadroom": allowance - cost,
        "knownMinimumRetainedMassLb": known_mass,
        "massComplete": mass_complete,
        "derivedBodySize": next(iter(sized)) if sized else None,
        "requestedPowerDemand": power_demand,
        "nominalPowerOutput": nominal_output,
        "minimumSimultaneousPowerShortfall": max(0, power_demand - nominal_output),
        "installedDriveCount": drive_count,
        "nominalGrossLoadSupportCeilingLb": max_support,
        "minimumMassExceedsSupportCeiling": bool(
            drive_count and known_mass > max_support
        ),
        "minimumMassToSupportCeilingRatio": (
            round(known_mass / max_support, 3) if max_support else None
        ),
        "hypotheticalSameRegionFlatDrSum": candidate_same_region_dr,
        "externalIngredients": dict(sorted(resources.items())),
        "operatingState": "not verified: missing power links, fuel and dependencies",
        "movement": "not determined: payload and complete operating state absent",
    }


def scenarios(reference: dict[str, Any]) -> list[dict[str, Any]]:
    base = [copy.deepcopy(x) for x in reference["illustrativeConfigurations"]]
    by_id = {x["id"]: x for x in base}
    variants = []
    mechanical = copy.deepcopy(by_id["mechanical-medium-like"])
    mechanical["id"] = "mechanical-medium-infernal"
    for part in mechanical["components"]:
        if part[0] == "housing":
            part[3] = "infernal-engine"
    variants.append(mechanical)
    for count in (1, 2):
        sample = copy.deepcopy(by_id["all-steel-overbuilt"])
        sample["id"] = f"overbuilt-{count}-drives"
        sample["components"] = [
            part for part in sample["components"]
            if part[1] != "locomotion-unit" or
            int(part[0].rsplit("-", 1)[1]) <= count
        ]
        variants.append(sample)
    for material in ("wood", "stone", "cloth"):
        sample = copy.deepcopy(by_id["all-steel-overbuilt"])
        sample["id"] = f"overbuilt-{material}-plating"
        for part in sample["components"]:
            if part[1] == "protective-plating":
                part[2] = material
        variants.append(sample)
    mixed = copy.deepcopy(by_id["all-steel-overbuilt"])
    mixed["id"] = "overbuilt-five-stone-one-cloth-plating"
    for part in mixed["components"]:
        if part[1] == "protective-plating":
            part[2] = "cloth" if part[0] == "plate-6" else "stone"
    variants.append(mixed)
    return base + variants


def report(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    results = [evaluate(config, catalog)
               for config in scenarios(catalog["reference"])]
    # An independent arithmetic cross-check must not silently accept stale
    # manually-entered reference numbers.
    for row, source in zip(results, catalog["reference"]["illustrativeConfigurations"]):
        if (row["resourceCost"] != source["provisionalIngredientCost"] or
                row["knownMinimumRetainedMassLb"] !=
                source["provisionalKnownRetainedMassPounds"]):
            raise ValueError(f"Stale candidate reference totals: {row['id']}")
    return results


def markdown(rows: list[dict[str, Any]]) -> str:
    lines = [
        "# Construct Assembly — native candidate sensitivity",
        "",
        "These are arithmetic diagnostics, not approved Origin grants.",
        "Power output and load-support capacities are potential maxima, not verified",
        "operating networks. Mass is a minimum unless explicitly certified. DR",
        "assumes every listed protective layer hypothetically covers one region;",
        "neither coverage nor stacking is authorized.",
        "",
        "| Scenario | Cost | Min. mass (lb) | Power demand/output | Max. load support (lb) | Hypothetical regional DR |",
        "| --- | ---: | ---: | --- | ---: | ---: |",
    ]
    for row in rows:
        lines.append(
            f"| {row['id']} | {row['resourceCost']} | "
            f"{row['knownMinimumRetainedMassLb']} | "
            f"{row['requestedPowerDemand']}/{row['nominalPowerOutput']} | "
            f"{row['nominalGrossLoadSupportCeilingLb']} | "
            f"{row['hypotheticalSameRegionFlatDrSum']} |"
        )
    lines.extend(["", "No speed or class/species-wide power equivalence is asserted.",
                  "The proposed starting allowance is not a first-party entitlement."])
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--format", choices=["json", "markdown"],
                        default="markdown")
    args = parser.parse_args()
    rows = report(load_catalog())
    if args.format == "json":
        print(json.dumps(rows, indent=2))
    else:
        print(markdown(rows))


if __name__ == "__main__":
    main()
