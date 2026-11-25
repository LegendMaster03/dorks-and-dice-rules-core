"""Project all unreviewed candidate occurrences into temporary loader fixtures.

This is deliberately a test data projection, NOT a promotion pipeline.
The fixture root is restricted to an ignored build directory. The production
Dockerfile stages data/rulesets only, and never consumes these fixtures.
"""
import json
from pathlib import Path
import re
import shutil

from stage_srd_editions import REPO, PACKAGES, build

OUTPUT = REPO / "build" / "srd-loader-fixtures"


def fixture_records(rows):
    result = []
    for row in rows:
        payload = row["payload"]
        category = re.sub(r"[^a-z0-9-]+", "-", row["sourceKind"].lower()).strip("-") or "source-record"
        if not category[0].isalpha():
            category = "source-" + category
        name = str(payload.get("name") or payload.get("abbreviation") or payload.get("id")
                   or row["proposedIdentity"]).strip() or row["candidateId"]
        result.append({"id": row["candidateId"], "kind": category, "name": name,
                       "fixtureOnly": True, "sourceOccurrence": row["source"],
                       "proposedIdentity": row["proposedIdentity"], "definition": payload})
    return result


def generate():
    # No arguments or alternative target: never write to a runtime source root.
    # Refuse links that could redirect this test-only output into real content.
    if (REPO / "build").is_symlink() or OUTPUT.is_symlink():
        raise ValueError("Fixture path must not be a symbolic link")
    if OUTPUT.exists():
        shutil.rmtree(OUTPUT)
    OUTPUT.mkdir(parents=True)
    counts = {}
    for edition, rows in build().items():
        package_id = PACKAGES[edition]
        package = OUTPUT / package_id
        (package / "native").mkdir(parents=True)
        manifest = {"id": package_id, "kind": "ruleset-package", "schemaVersion": 1,
                    "nativeRoot": "native/", "workingRoot": "working/",
                    "runtimeContentRoot": "native/", "testFixtureOnly": True}
        (package / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
        records = fixture_records(rows)
        (package / "native" / "fixture.json").write_text(
            json.dumps(records, ensure_ascii=False), encoding="utf-8")
        counts[package_id] = len(records)
    (OUTPUT / "expected-counts.json").write_text(json.dumps(counts), encoding="utf-8")
    print(json.dumps(counts))
    return counts


if __name__ == "__main__":
    generate()
