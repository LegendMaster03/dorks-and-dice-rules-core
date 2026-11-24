"""Explicit migration of persisted Origin mechanical definition references.

Canonical Species/Ancestry source occurrences are unchanged. This one-time
migration only replaces verified historical gene definition identifiers in
recognizable ID fields; it does not synthesize new grants or alter provenance.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Mapping

MANIFEST = (Path(__file__).resolve().parents[1] / "data" / "migrations"
            / "biological-gene-ids-20261121.json")
IDENTITY_FIELDS = frozenset({
    "mechanicalDefinitionId", "MechanicalDefinitionId",
    "traitId", "TraitId",
    "requiresTraitDefinitionId", "RequiresTraitDefinitionId",
})


def load_map(path: Path = MANIFEST) -> dict[str, str]:
    document = json.loads(path.read_text(encoding="utf-8"))
    if (document.get("kind") != "biological-gene-id-migration" or
            document.get("schemaVersion") != 1):
        raise ValueError("Unsupported biological gene ID migration manifest")
    result = document.get("identityMap")
    if not isinstance(result, dict) or not result or any(
            not isinstance(k, str) or not isinstance(v, str)
            for k, v in result.items()):
        raise ValueError("Invalid biological gene identity mapping")
    if (len(result) != document.get("legacyGeneCount") or
            len(set(result.values())) != document.get("canonicalGeneCount")):
        raise ValueError("Gene identity migration cardinality mismatch")
    return result


def migrate_profile(document: Any, mapping: Mapping[str, str]) -> tuple[Any, int]:
    """Return a copy with only recognized gene-identity fields converted.

    Do not rewrite source IDs, source-occurrence paths, contributor IDs, history
    identities, parameter source evidence, or arbitrary descriptive strings.
    """
    count = 0

    def walk(value: Any) -> Any:
        nonlocal count
        if isinstance(value, list):
            return [walk(item) for item in value]
        if not isinstance(value, dict):
            return value
        result: dict[str, Any] = {}
        for key, item in value.items():
            if key in IDENTITY_FIELDS and isinstance(item, str) and item in mapping:
                result[key] = mapping[item]
                count += 1
            else:
                result[key] = walk(item)
        return result

    return walk(document), count


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, type=Path,
                        help="Saved Origin JSON file to migrate")
    parser.add_argument("--output", type=Path,
                        help="New JSON file; existing files are never overwritten")
    parser.add_argument("--dry-run", action="store_true",
                        help="Report the number of reference changes without writing")
    args = parser.parse_args(argv)
    if not args.dry_run and args.output is None:
        parser.error("--output is required unless --dry-run is used")
    if args.output is not None and args.input.resolve() == args.output.resolve():
        parser.error("Input and output paths must differ; in-place rewrite is not supported")
    original = json.loads(args.input.read_text(encoding="utf-8"))
    if not isinstance(original, (dict, list)):
        parser.error("Saved Origin JSON must be an object or array")
    migrated, count = migrate_profile(original, load_map())
    if not args.dry_run:
        # x mode refuses to overwrite an existing profile or migration output.
        with args.output.open("x", encoding="utf-8") as stream:
            json.dump(migrated, stream, indent=2, ensure_ascii=False)
            stream.write("\n")
    print(f"Updated {count} known biological gene ID references."
          + (" No files written." if args.dry_run else f" Output: {args.output}"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
