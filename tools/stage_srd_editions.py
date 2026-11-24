"""Build separate, non-runtime review catalogs from the pinned 5e.tools-shaped corpus.

Do not promote source-tagged content automatically. The payload is preserved
unchanged, including known defects; source position and contextual identity are
separate from the eventual reviewed native entity ID.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import unicodedata

from inventory_srd_editions import ROOT, TAGS, BOOKS, inventory

REPO = ROOT.parents[2]
PACKAGES = {"srd-5.1": "dnd-5e", "srd-5.2.1": "dnd-5-5e"}


def slug(value):
    normalized = unicodedata.normalize("NFKD", str(value)).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9]+", "-", normalized.lower()).strip("-") or "unnamed"


def identity(kind, record):
    # Contextual keys are necessary for repeated feature names across classes,
    # subclasses, levels and subraces. Do not infer identity from display name alone.
    name = record.get("name") or record.get("abbreviation") or record.get("id")
    parts = [slug(kind)]
    for key in ("raceName", "raceSource", "className", "classSource",
                "subclassShortName", "subclassSource", "level"):
        if record.get(key) is not None:
            parts.append(slug(record[key]))
    parts.append(slug(name or "unnamed"))
    return ".".join(parts)


def canonical_digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False,
                                    separators=(",", ":")).encode("utf-8")).hexdigest()


def build(root=ROOT):
    audited = inventory(root)  # verifies every pinned byte before using source data
    packages = {edition: [] for edition in PACKAGES}
    for file in audited["files"]:
        path = file["path"]
        value = json.loads((root / "structured" / path).read_text(encoding="utf-8"))
        arrays = value if isinstance(value, dict) else {"$": value}
        for kind, records in sorted(arrays.items()):
            if not isinstance(records, list):
                continue
            for index, record in enumerate(records):
                if not isinstance(record, dict):
                    continue
                edition = TAGS.get(record.get("source"))
                if edition is None and "source" not in record and kind == "data":
                    edition = BOOKS.get(path)
                if edition is None:
                    continue
                # Every tagged object is represented, including metadata/fluff.
                # Review must classify those rather than silently treating them as rules.
                pointer = "/" + kind.replace("~", "~0").replace("/", "~1") + "/" + str(index)
                if kind == "$":
                    pointer = "/" + str(index)
                candidate_id = "candidate." + hashlib.sha256(
                    (path + "#" + pointer).encode("utf-8")).hexdigest()
                packages[edition].append({
                    "candidateId": candidate_id,
                    "proposedIdentity": identity(kind, record),
                    "source": {"provider": audited["provider"], "commit": audited["commit"],
                               "edition": edition, "path": path, "pointer": pointer,
                               "payloadSha256": canonical_digest(record)},
                    "sourceKind": kind,
                    "reviewStatus": "unreviewed",
                    "payload": record,
                })
    for edition, rows in packages.items():
        if len({row["candidateId"] for row in rows}) != len(rows):
            raise ValueError("Duplicate source occurrence in " + edition)
        if len(rows) != audited["candidateCounts"][edition]:
            raise ValueError("Candidate inventory does not reconcile for " + edition)
    return packages


def write_catalogs(output, packages):
    summary = {}
    for edition, rows in packages.items():
        package_id = PACKAGES[edition]
        directory = output / package_id / "working"
        directory.mkdir(parents=True, exist_ok=True)
        kinds = Counter(row["sourceKind"] for row in rows)
        identities = Counter(row["proposedIdentity"] for row in rows)
        collisions = sorted(key for key, count in identities.items() if count > 1)
        (directory / "srd-candidates.json").write_text(
            json.dumps({"status": "unreviewed-reference-only", "edition": edition,
                        "candidates": rows}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        summary[package_id] = {"edition": edition, "count": len(rows),
                               "sourceKinds": dict(sorted(kinds.items())),
                               "proposedIdentityCollisions": collisions}
    (output / "srd-review-summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return summary


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True,
                        help="Separate staging directory; never the native tree.")
    args = parser.parse_args()
    output = args.output.resolve()
    if output == REPO.resolve() or REPO.resolve() in output.parents:
        # A checkout's authoritative data must never be a generated output target.
        if output != (REPO / "build" / "srd-review").resolve():
            parser.error("Use an external directory or build/srd-review")
    output.mkdir(parents=True, exist_ok=True)
    print(json.dumps(write_catalogs(output, build()), indent=2))
