#!/usr/bin/env python3
"""Inventory native rule representations without choosing source variants."""
import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path


def top_level_records(document):
    """Enumerate native entities, not nested source or mechanic details."""
    if isinstance(document, dict):
        if "id" in document:
            return [document]
        return [
            item
            for values in document.values()
            if isinstance(values, list)
            for item in values
            if isinstance(item, dict) and "id" in item
        ]
    if isinstance(document, list):
        return [item for item in document if isinstance(item, dict) and "id" in item]
    return []


def audit(root):
    root = Path(root)
    counts = Counter()
    paths_by_id = defaultdict(list)
    invalid = []
    outstanding = []
    for path in sorted(root.rglob("*.json")):
        relative = path.relative_to(root).as_posix()
        try:
            records = top_level_records(json.loads(path.read_text(encoding="utf-8")))
        except (ValueError, UnicodeError) as exc:
            invalid.append(f"{relative}: {exc}")
            continue
        if not records:
            invalid.append(f"{relative}: no top-level native records")
        for record in records:
            identity = record.get("id")
            if not isinstance(identity, str) or not identity.strip():
                invalid.append(f"{relative}: missing native ID")
                continue
            counts["records"] += 1
            paths_by_id[identity].append(relative)
            candidates = record.get("candidateDefinitions", [])
            if not isinstance(candidates, list):
                invalid.append(f"{relative}: malformed candidateDefinitions")
                continue
            if candidates:
                counts["staged_candidates"] += 1
                counts["multiple_candidates" if len(candidates) > 1 else "single_candidate"] += 1
                outstanding.append(identity)
            if str(record.get("baselineStatus", "")).startswith("pending"):
                counts["pending"] += 1
                if "activeDefinition" in record:
                    invalid.append(f"{relative}: pending rule has activeDefinition")
            elif not candidates:
                counts["without_candidates"] += 1
    duplicates = {identity: paths for identity, paths in paths_by_id.items() if len(paths) > 1}
    return {
        "counts": dict(counts),
        "unresolvedNativeIds": sorted(outstanding),
        "duplicateIds": duplicates,
        "invalid": invalid,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--native", type=Path, default=Path("data/rulesets/first-party-001/native"))
    parser.add_argument("--strict", action="store_true",
                        help="Fail if native records retain alternative candidates or validation errors")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args(argv)
    report = audit(args.native)
    if args.json:
        print(json.dumps(report, indent=2))
    else:
        print(json.dumps({
            **report["counts"],
            "duplicateIds": len(report["duplicateIds"]),
            "invalid": len(report["invalid"]),
        }, indent=2))
        for message in report["invalid"]:
            print(message)
    return int(args.strict and bool(report["unresolvedNativeIds"] or
                                    report["duplicateIds"] or report["invalid"]))


if __name__ == "__main__":
    raise SystemExit(main())
