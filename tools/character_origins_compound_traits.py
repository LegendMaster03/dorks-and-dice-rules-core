"""Read-only compound-trait preservation and typed parameter-binding study.

Each authored trait remains a single indivisible evidence unit. Declarative
bindings resolve only explicitly declared source properties; unrecognized,
invalid, or missing bindings remain unresolved. This is not an authorized
Origin grant, a general evaluator, or a mixed-Heritage composition engine.
"""
import argparse
import copy
import json
import re
from pathlib import Path

from character_origins_characteristic_facets import TRAIT_FACETS, TRAIT_METADATA
from character_origins_source_inventory import read_catalog, source_inventory


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CONTRACTS = (
    ROOT / "docs/rewrite/character-origins/"
    "pillar2-legacy-binding-evidence-20261111.json"
)
_FIELD_PATH = re.compile(r"[A-Za-z][A-Za-z0-9]*(?:\[\d+\])?"
                         r"(?:\.[A-Za-z][A-Za-z0-9]*(?:\[\d+\])?)*")
_SEGMENT = re.compile(r"([A-Za-z][A-Za-z0-9]*)(?:\[(\d+)\])?")


class CompoundBindingError(ValueError):
    """A binding contract or source path is malformed or ambiguous."""


def read_contracts(path=DEFAULT_CONTRACTS):
    """Load explicit evidence contracts, independently of native gameplay."""
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    return data["contracts"]


def _get_path(value, path):
    """Safely read a constrained dotted/indexed property path."""
    if not isinstance(path, str) or not _FIELD_PATH.fullmatch(path):
        raise CompoundBindingError(f"Invalid field path: {path!r}")
    cursor = value
    for part in path.split("."):
        match = _SEGMENT.fullmatch(part)
        if match is None:
            raise CompoundBindingError(f"Invalid field segment: {part!r}")
        key, index = match.groups()
        if not isinstance(cursor, dict) or key not in cursor:
            raise KeyError(path)
        cursor = cursor[key]
        if index is not None:
            if not isinstance(cursor, list) or int(index) >= len(cursor):
                raise KeyError(path)
            cursor = cursor[int(index)]
    return cursor


def _symbolic_references(value, prefix=""):
    """Find binding-like source values without interpreting their semantics.

    A field ending in 'From' signals a source binding, as does an explicitly
    selected-/chosen- symbolic reference. This is intentionally conservative.
    """
    if isinstance(value, dict):
        for field, child in value.items():
            path = f"{prefix}.{field}" if prefix else field
            if isinstance(child, str) and (
                field.endswith("From")
                or child.startswith(("selected-", "chosen-"))
            ):
                yield path, child
            yield from _symbolic_references(child, path)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from _symbolic_references(child, f"{prefix}[{index}]")


def _contracts_by_occurrence(contracts):
    """Validate and index declarative contracts by qualified occurrence."""
    by_source = {}
    for contract in contracts:
        occurrence = contract.get("sourceOccurrenceId")
        if not isinstance(occurrence, str) or not occurrence:
            raise CompoundBindingError("Missing sourceOccurrenceId")
        if occurrence in by_source:
            raise CompoundBindingError(f"Repeated source contract: {occurrence}")
        bindings = contract.get("bindings")
        if not isinstance(bindings, list):
            raise CompoundBindingError(f"Invalid bindings for {occurrence}")
        paths = set()
        for item in bindings:
            path = item.get("path")
            if not isinstance(path, str) or not _FIELD_PATH.fullmatch(path):
                raise CompoundBindingError(f"Invalid contract path: {path!r}")
            if path in paths:
                raise CompoundBindingError(
                    f"Duplicate binding path in {occurrence}: {path}"
                )
            paths.add(path)
            if not isinstance(item.get("authoredReference"), str):
                raise CompoundBindingError(f"Missing authored reference: {path}")
            lookup = item.get("lookup")
            if not isinstance(lookup, dict) or (
                lookup.get("scope") != "selected-lineage"
                or lookup.get("order") != "nearest-to-selected"
                or lookup.get("expectedType") not in ("string", "object")
                or not isinstance(lookup.get("field"), str)
                or not _FIELD_PATH.fullmatch(lookup["field"])
            ):
                raise CompoundBindingError(
                    f"Unsupported or incomplete lookup at {occurrence}:{path}"
                )
        by_source[occurrence] = bindings
    return by_source


def _lookup_lineage_field(lineage, lookup):
    """Bind a typed field from the nearest applicable selected source node.

    There is no Species-, family-, or trait-name dispatch. If the closest
    source with that property has a wrong type, fail rather than skip it.
    """
    expected = lookup["expectedType"]
    for source in reversed(lineage):
        try:
            value = _get_path(source["intrinsicFields"], lookup["field"])
        except KeyError:
            continue
        valid = (
            isinstance(value, str) and bool(value) if expected == "string"
            else isinstance(value, dict) if expected == "object"
            else False
        )
        if not valid:
            return {
                "status": "unresolved",
                "reason": "nearest-field-type-mismatch",
                "resolvedFromSourceId": source["sourceId"],
            }
        return {
            "status": "bound",
            "resolvedFromSourceId": source["sourceId"],
            "resolvedValue": copy.deepcopy(value),
        }
    return {"status": "unresolved", "reason": "lineage-field-not-found"}


def project_compound_traits(selected_id, catalog, contracts):
    """Keep every source trait whole and bind explicit, typed references.

    Binding values are shown separately from the unchanged native definition.
    The output is evidence, not an executable or earned character ability.
    """
    inventory = source_inventory(selected_id, catalog)
    indexed = _contracts_by_occurrence(contracts)
    units = []
    for source in inventory["lineage"]:
        for occurrence in source["traits"]:
            authored = occurrence["definition"]
            source_occurrence_id = occurrence["occurrenceId"]
            recognized = {
                item["path"]: item
                for item in indexed.get(source_occurrence_id, [])
            }
            references = list(_symbolic_references(authored))
            found_paths = {path for path, _ in references}
            results = []

            for path, authored_reference in references:
                contract = recognized.get(path)
                if contract is None:
                    results.append({
                        "path": path,
                        "authoredReference": authored_reference,
                        "status": "unresolved",
                        "reason": "binding-contract-required",
                    })
                elif authored_reference != contract["authoredReference"]:
                    results.append({
                        "path": path,
                        "authoredReference": authored_reference,
                        "status": "unresolved",
                        "reason": "authored-reference-mismatch",
                    })
                else:
                    result = _lookup_lineage_field(
                        inventory["lineage"], contract["lookup"]
                    )
                    results.append({
                        "path": path,
                        "authoredReference": authored_reference,
                        **result,
                    })

            for path in recognized.keys() - found_paths:
                results.append({
                    "path": path,
                    "status": "unresolved",
                    "reason": "contract-path-not-found",
                })

            mechanical_fields = {
                key: value for key, value in authored.items()
                if key not in TRAIT_METADATA
            }
            units.append({
                "sourceId": source["sourceId"],
                "sourceOccurrenceId": source_occurrence_id,
                "localTraitId": occurrence["localTraitId"],
                "authoredDefinition": copy.deepcopy(authored),
                "components": [
                    {
                        "nativeField": key,
                        "candidateSemanticId": TRAIT_FACETS.get(key),
                        "authoredValue": copy.deepcopy(value),
                    }
                    for key, value in mechanical_fields.items()
                ],
                "parameterBindings": results,
                "bindingStatus": (
                    "requires-review"
                    if any(result["status"] != "bound" for result in results)
                    else "parameters-bound"
                    if results else "no-symbolic-parameters"
                ),
            })

    return {
        "selectedSourceId": selected_id,
        "sourceLineage": [
            source["sourceId"] for source in inventory["lineage"]
        ],
        "intrinsicSources": [
            {
                "sourceId": source["sourceId"],
                "intrinsicFields": copy.deepcopy(source["intrinsicFields"]),
            }
            for source in inventory["lineage"]
        ],
        "compoundTraits": units,
        "evaluationStatus": "not-evaluated",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Inspect coherent compound traits and typed bindings."
    )
    parser.add_argument("--selected", required=True, metavar="SOURCE_ID")
    parser.add_argument("--contracts", type=Path, default=DEFAULT_CONTRACTS)
    args = parser.parse_args()
    result = project_compound_traits(
        args.selected, read_catalog(), read_contracts(args.contracts)
    )
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
