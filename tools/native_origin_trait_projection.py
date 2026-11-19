"""Read-only projection of native Species/Ancestry trait references.

Authoritative native source records contain only trait IDs. This helper is for
legacy Python reconciliation checks which inspect the former embedded view.
It never changes rules, provenance, authorization, or native files.
"""
from pathlib import Path
import copy
import json


def _definitions(native_root):
    # This is a design-time reader, and callers can revise native files in the
    # same process. Caching by directory path would project stale trait payloads.
    entries = {}
    for path in sorted((native_root / "traits").rglob("*.json")):
        records = json.loads(path.read_text(encoding="utf-8"))
        for record in records if isinstance(records, list) else [records]:
            identity = record.get("id")
            if record.get("kind") != "origin-trait" or not isinstance(identity, str):
                raise ValueError(f"Invalid inherent trait definition: {path}")
            if identity in entries:
                raise ValueError(f"Duplicate inherent trait definition: {identity}")
            entries[identity] = record["trait"]
    return entries


def expand_species_traits(value, path):
    """Return original embedded-style trait content for a native source record.

    Non-Species documents, including rules and manifest data, pass unchanged.
    An unresolved or malformed reference fails rather than hiding missing data.
    """
    if not isinstance(value, dict) or value.get("kind") not in ("species", "ancestry"):
        return value
    traits = value.get("traits", [])
    if not any(isinstance(x, dict) and "traitId" in x for x in traits):
        return value

    native_root = next((parent for parent in Path(path).resolve().parents
                        if (parent / "species").is_dir() and
                        (parent / "traits").is_dir()), None)
    if native_root is None:
        raise ValueError(f"Missing native root for {path}")
    definitions = _definitions(native_root)
    resolved = copy.deepcopy(value)
    expanded = []
    for occurrence in traits:
        if "traitId" not in occurrence:
            expanded.append(occurrence)
            continue
        if set(occurrence) != {"traitId"} or occurrence["traitId"] not in definitions:
            raise ValueError(f"Unresolved inherent trait reference in {path}")
        expanded.append(copy.deepcopy(definitions[occurrence["traitId"]]))
    resolved["traits"] = expanded
    return resolved
