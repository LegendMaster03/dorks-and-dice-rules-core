"""Inventory pinned third-party SRD candidates without promoting any content.

Source tags are candidates, not evidence of official source fidelity.
All arrays, untagged records and non-object values are reported separately.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "data/reference/srd"
SOURCE = ROOT / "structured"
TAGS = {"SRD51": "srd-5.1", "SRD52": "srd-5.2.1"}
BOOKS = {"book/book-srd51.json": "srd-5.1", "book/book-srd52.json": "srd-5.2.1"}


def inventory(root=ROOT):
    manifest = json.loads((root / "structured-manifest.json").read_text(encoding="utf-8"))
    files = []
    total = Counter()
    for entry in sorted(manifest["files"], key=lambda item: item["path"]):
        path = entry["path"]
        raw = (root / "structured" / path).read_bytes()
        if len(raw) != entry["bytes"] or hashlib.sha256(raw).hexdigest() != entry["sha256"]:
            raise ValueError(f"Pinned source mismatch: {path}")
        value = json.loads(raw)
        arrays = value if isinstance(value, dict) else {"$": value}
        groups = []
        for kind, records in sorted(arrays.items()):
            if not isinstance(records, list):
                continue
            counts = Counter()
            for record in records:
                if not isinstance(record, dict):
                    label = "non-object"
                elif record.get("source") in TAGS:
                    label = TAGS[record["source"]]
                elif path in BOOKS and kind == "data" and "source" not in record:
                    label = BOOKS[path]
                elif "source" not in record:
                    label = "untagged"
                else:
                    label = "other-source"
                counts[label] += 1
                total[label] += 1
            groups.append({"array": kind, "count": len(records), "candidateCounts": dict(sorted(counts.items()))})
        files.append({"path": path, "arrays": groups})
    return {"status": "reference-only; not official completeness or rights verification",
            "provider": manifest["provider"], "commit": manifest["commit"],
            "fileCount": len(files), "candidateCounts": dict(sorted(total.items())), "files": files}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = json.dumps(inventory(), indent=2) + "\n"
    if args.output:
        args.output.write_text(result, encoding="utf-8")
    else:
        print(result, end="")
