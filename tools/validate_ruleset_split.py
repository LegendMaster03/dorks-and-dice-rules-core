#!/usr/bin/env python3
"""Validate the physical ruleset boundary without inferring rule approval.

Use --check-migration-sha only for the Phase 1A snapshot: future approved
promotions will legitimately change content while preserving stable IDs.
"""
import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path
import re

from audit_native_baseline import top_level_records

REPO = Path(__file__).resolve().parents[1]
PACKAGE = REPO / "data/rulesets/first-party-001"
NATIVE = PACKAGE / "native"
WORKING = PACKAGE / "working"
ID_PATTERN = re.compile(r"^[a-z0-9-]+(?:\.[a-z0-9-]+)+$")


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def validate_native_tree(root):
    """Return identity map and errors; inspect every top-level operative record."""
    ids = {}
    errors = []
    for path in sorted(root.rglob("*.json")):
        try:
            document = read_json(path)
            records = top_level_records(document)
        except (ValueError, UnicodeError) as exc:
            errors.append(f"{path}: invalid JSON: {exc}")
            continue
        if not records:
            errors.append(f"{path}: no identified records")
        for record in records:
            ident = record.get("id")
            if not isinstance(ident, str) or not ID_PATTERN.fullmatch(ident):
                errors.append(f"{path}: invalid stable ID {ident!r}")
                continue
            if ident in ids:
                errors.append(f"{path}: duplicate authoritative ID {ident} (also {ids[ident]})")
            ids[ident] = path
            if not record.get("kind") or not record.get("name"):
                errors.append(f"{path}: {ident} needs name and kind")
            if "candidateDefinitions" in record or "candidateSpecializations" in record:
                errors.append(f"{path}: {ident} includes draft candidate structures")
            if "aliasOf" in record:
                errors.append(f"{path}: {ident} is only an identity alias")
            if "baselineStatus" in record:
                errors.append(f"{path}: {ident} still uses a staging baselineStatus")
            if str(record.get("status", "")).startswith(("pending", "partial", "provisional", "draft")):
                errors.append(f"{path}: {ident} is not marked as final")
    return ids, errors


def check_relationships():
    errors = []
    catalog = read_json(NATIVE / "conditions.json")
    chapter = read_json(NATIVE / "rules/conditions.json")
    conditions = {x["id"]: x for x in catalog["conditions"]}
    if len(conditions) != len(catalog["conditions"]):
        errors.append("duplicate condition ID within authoritative condition catalog")
    if set(chapter["conditionIds"]) != set(conditions) or len(chapter["conditionIds"]) != len(conditions):
        errors.append("condition chapter and canonical condition catalog differ")
    for ident, record in conditions.items():
        for linked in record.get("appliesConditions", []):
            if linked not in conditions:
                errors.append(f"{ident}: missing applied condition {linked}")
        for requirement in record.get("requiresConditions", []):
            linked = requirement.get("condition")
            if linked not in conditions:
                errors.append(f"{ident}: missing required condition {linked}")
    return errors


def check_srd_coverage():
    errors = []
    m = read_json(REPO / "data/reference/srd/native-coverage-manifest.json")
    rows = m["coverage"]
    if len(rows) != 2522 or m["nativeDefinitionCount"] != 2522:
        errors.append("pinned 2,522-entry SRD coverage count changed")
    ids = set()
    cache = {}
    for row in rows:
        ident = row["nativeId"]
        if ident in ids:
            errors.append(f"duplicate SRD coverage ID: {ident}")
        ids.add(ident)
        path_text = row.get("path")
        if not path_text:
            if row["status"] != "existing-native-covered":
                errors.append(f"coverage ID without destination: {ident}")
            continue
        if not path_text.startswith("data/rulesets/first-party-001/"):
            errors.append(f"coverage path outside ruleset: {ident}: {path_text}")
            continue
        path = REPO / path_text
        if path not in cache:
            try:
                cache[path] = top_level_records(read_json(path))
            except (OSError, ValueError, UnicodeError) as exc:
                errors.append(f"missing/invalid coverage file {path_text}: {exc}")
                cache[path] = []
        if sum(rec.get("id") == ident for rec in cache[path]) != 1:
            errors.append(f"coverage record {ident} is missing or duplicated in {path_text}")
    return errors, len(rows)


def check_migration_shas():
    """Snapshot-only proof: not a permanent publication or future-edit gate."""
    errors = []
    ledger = read_json(REPO / "docs/rewrite/phase-1a-native-working-ledger.json")
    files = ledger["files"]
    if len(files) != ledger["counts"]["beforeFiles"]:
        errors.append("migration ledger file count mismatch")
    old = set()
    new = set()
    for entry in files:
        before, after = entry["oldPath"], entry["newPath"]
        if before in old or after in new:
            errors.append(f"duplicate migration path: {before} -> {after}")
        old.add(before)
        new.add(after)
        path = REPO / after
        try:
            data = path.read_bytes()
        except OSError as exc:
            errors.append(f"lost original blob: {after}: {exc}")
            continue
        sha = hashlib.sha1(b"blob " + str(len(data)).encode() + bytes([0]) + data).hexdigest()
        if sha != entry["gitBlobSha"]:
            errors.append(f"modified original blob: {after}")
        if len(data) != entry["bytes"]:
            errors.append(f"original byte length changed: {after}")
    source = read_json(REPO / "data/reference/srd/native-coverage-manifest.json")
    if len(ledger["records"]) != len(source["coverage"]):
        errors.append("migration record ledger does not match source manifest")
    return errors, len(files)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check-migration-sha", action="store_true",
                        help="Verify the immutable Phase 1A snapshot, not appropriate after later promotions")
    args = parser.parse_args(argv)
    errors = []
    package = read_json(PACKAGE / "manifest.json")
    if (package.get("id") != "first-party-001" or
            package.get("runtimeContentRoot") != "native/"):
        errors.append("ruleset manifest must select native/ only")
    if (REPO / "data/native").exists():
        errors.append("obsolete data/native/ location still exists")
    ids, native_errors = validate_native_tree(NATIVE)
    errors.extend(native_errors)
    errors.extend(check_relationships())
    errors.extend(check_srd_coverage()[0])
    working_ids = defaultdict(list)
    for path in sorted(WORKING.rglob("*.json")):
        try:
            records = top_level_records(read_json(path))
        except (ValueError, UnicodeError) as exc:
            errors.append(f"working JSON invalid: {path}: {exc}")
            continue
        for rec in records:
            if "id" in rec:
                working_ids[rec["id"]].append(str(path))
    for ident in ids:
        if ident in working_ids:
            errors.append(f"authoritative {ident} also appears in working")
    checked = None
    if args.check_migration_sha:
        sha_errors, checked = check_migration_shas()
        errors.extend(sha_errors)
    print(json.dumps({"nativeRecords": len(ids),
                      "workingIdentities": len(working_ids),
                      "migrationBlobsChecked": checked,
                      "errors": errors}, indent=2))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
