"""Lossless source inventory for the Character Origins Pillar 2 design study.

This is a read-only design aid, not an Origin resolver, character builder,
inheritance algorithm, or approved runtime rule. It deliberately retains
unresolved native attributes and source-authored trait definitions rather
than interpreting them into game effects.
"""

import argparse
import copy
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"
SOURCE_FOLDERS = ("species/base", "species/ancestry")

# These fields describe source records, selection, or cultural suggestions.
# They are not intrinsic characteristic grants. Unknown native fields remain
# in intrinsicFields, preserving information for later schema review.
SOURCE_METADATA = frozenset({
    "id",
    "name",
    "kind",
    "status",
    "selectable",
    "parent",
    "reviewNotes",
    "defaultUpbringing",
    "ancestryTheme",
    "traits",
})


class OriginInventoryError(ValueError):
    """An invalid source record or direct-selection relationship."""


def read_catalog(native_root=NATIVE):
    """Read all source records without modifying their native definitions."""
    catalog = {}
    for folder in SOURCE_FOLDERS:
        for path in sorted((Path(native_root) / folder).rglob("*.json")):
            source = json.loads(path.read_text(encoding="utf-8"))
            identifier = source.get("id")
            if not isinstance(identifier, str) or not identifier:
                raise OriginInventoryError(f"Missing source ID in {path}")
            if identifier in catalog:
                raise OriginInventoryError(f"Duplicate source ID {identifier}")
            if source.get("kind") not in ("species", "ancestry"):
                raise OriginInventoryError(f"Unsupported source kind in {path}")
            if not isinstance(source.get("traits", []), list):
                raise OriginInventoryError(f"Invalid traits array in {path}")

            ids = [trait.get("id") for trait in source.get("traits", [])]
            if any(not isinstance(value, str) or not value for value in ids):
                raise OriginInventoryError(f"Missing trait ID in {path}")
            if len(ids) != len(set(ids)):
                raise OriginInventoryError(f"Repeated local trait ID in {path}")
            catalog[identifier] = source
    return catalog


def source_lineage(selected_id, catalog):
    """Return a valid root-to-leaf lineage, including unselectable ancestors.

    Only the selected source must be selectable. This is a direct-selection
    inventory, not a mixed-heritage contributor eligibility policy.
    """
    if selected_id not in catalog:
        raise OriginInventoryError(f"Unknown source {selected_id}")
    if catalog[selected_id].get("selectable") is not True:
        raise OriginInventoryError(f"Source is not directly selectable: {selected_id}")

    lineage = []
    visited = set()
    cursor = selected_id
    while cursor is not None:
        if cursor in visited:
            raise OriginInventoryError(f"Cycle in source lineage at {cursor}")
        if cursor not in catalog:
            raise OriginInventoryError(
                f"Missing parent {cursor} referenced by {selected_id}"
            )
        visited.add(cursor)
        source = catalog[cursor]
        lineage.append(source)
        cursor = source.get("parent")
    lineage.reverse()

    if lineage[0]["kind"] != "species":
        raise OriginInventoryError(
            f"Lineage for {selected_id} has no root Species"
        )
    if any(source["kind"] != "ancestry" for source in lineage[1:]):
        raise OriginInventoryError(
            f"Lineage for {selected_id} contains a non-Ancestry descendant"
        )
    if selected_id.startswith("species.") and len(lineage) != 1:
        raise OriginInventoryError(
            f"Direct base Species selection has a parent: {selected_id}"
        )
    return lineage


def source_inventory(selected_id, catalog):
    """Preserve all intrinsic source fields and trait occurrences by origin.

    Fields and individual traits are preserved verbatim, with a stable
    source-qualified occurrence ID. No trait stacking, overrides, parameter
    binding, acquisition, or mixed-heritage selection is performed.
    """
    lineage = []
    for source in source_lineage(selected_id, catalog):
        source_id = source["id"]
        traits = []
        for trait in source.get("traits", []):
            traits.append({
                "occurrenceId": f"{source_id}#trait:{trait['id']}",
                "sourceId": source_id,
                "localTraitId": trait["id"],
                "definition": copy.deepcopy(trait),
            })

        lineage.append({
            "sourceId": source_id,
            "sourceKind": source["kind"],
            "intrinsicFields": {
                key: copy.deepcopy(value)
                for key, value in source.items()
                if key not in SOURCE_METADATA
            },
            "traits": traits,
        })

    return {
        "selectedSourceId": selected_id,
        "lineage": lineage,
        "evaluationStatus": "not-evaluated",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Inspect source-preserving Pillar 2 characteristic inputs."
    )
    options = parser.add_mutually_exclusive_group(required=True)
    options.add_argument(
        "--selected",
        metavar="SOURCE_ID",
        help="Show full source inventory for one directly selectable Species/Ancestry.",
    )
    options.add_argument(
        "--summary",
        action="store_true",
        help="List the available standard configurations and lineage/trait counts.",
    )
    args = parser.parse_args()
    catalog = read_catalog()

    if args.selected:
        result = source_inventory(args.selected, catalog)
    else:
        result = []
        for identifier in sorted(catalog):
            if catalog[identifier].get("selectable") is not True:
                continue
            candidate = source_inventory(identifier, catalog)
            result.append({
                "selectedSourceId": identifier,
                "lineage": [
                    source["sourceId"] for source in candidate["lineage"]
                ],
                "traitOccurrences": sum(
                    len(source["traits"]) for source in candidate["lineage"]
                ),
            })
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
