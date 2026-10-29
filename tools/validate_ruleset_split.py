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


def draft_key_paths(value, prefix=""):
    """Locate imported candidate structures at any depth of a native record."""
    forbidden = {"candidateDefinitions", "candidateSpecializations"}
    if isinstance(value, dict):
        for key, item in value.items():
            path = f"{prefix}.{key}" if prefix else key
            if key in forbidden:
                yield path
            yield from draft_key_paths(item, path)
    elif isinstance(value, list):
        for index, item in enumerate(value):
            yield from draft_key_paths(item, f"{prefix}[{index}]")


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
            for draft_path in draft_key_paths(record):
                errors.append(f"{path}: {ident} includes draft candidate structure at {draft_path}")
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


def check_migration_ledger():
    """Permanently validate the original migration inventory, not mutable bytes.

    This check remains valid when rules are edited, moved to native after
    approval, or have their coverage paths updated in later phases.
    """
    errors = []
    ledger = read_json(REPO / "docs/rewrite/phase-1a-native-working-ledger.json")
    counts = ledger["counts"]
    files = ledger["files"]
    source_rows = ledger["records"]
    manifest = read_json(REPO / "data/reference/srd/native-coverage-manifest.json")
    current_rows = manifest["coverage"]

    if len(files) != counts["beforeFiles"]:
        errors.append("migration ledger original-file count mismatch")
    if counts["afterNativeFiles"] + counts["afterWorkingFiles"] + counts["archivedReadmes"] != len(files):
        errors.append("migration ledger destination-file counts do not reconcile")
    if counts["afterNativeJson"] + counts["afterWorkingJson"] != counts["beforeJson"]:
        errors.append("migration ledger JSON-file counts do not reconcile")
    if len(source_rows) != counts["SRDCoverageBefore"] or len(source_rows) != counts["SRDCoverageAfter"]:
        errors.append("migration ledger original SRD record counts do not reconcile")

    old_paths, new_paths = set(), set()
    for entry in files:
        old_path, new_path = entry["oldPath"], entry["newPath"]
        if not old_path.startswith("data/native/"):
            errors.append(f"invalid original path in migration ledger: {old_path}")
        if not (new_path.startswith("data/rulesets/first-party-001/native/") or
                new_path.startswith("data/rulesets/first-party-001/working/") or
                new_path == "docs/rewrite/phase-1a-original-native-readme.md"):
            errors.append(f"unexpected original migration destination: {new_path}")
        if old_path in old_paths or new_path in new_paths:
            errors.append(f"repeated original or destination path: {old_path} -> {new_path}")
        old_paths.add(old_path)
        new_paths.add(new_path)
        if not isinstance(entry["bytes"], int) or entry["bytes"] < 0:
            errors.append(f"invalid original blob length: {old_path}")
        if not re.fullmatch(r"[0-9a-f]{40}", entry["gitBlobSha"]):
            errors.append(f"invalid original Git blob SHA: {old_path}")

    initial_ids = [entry["id"] for entry in source_rows]
    current_ids = [entry["nativeId"] for entry in current_rows]
    if len(initial_ids) != len(set(initial_ids)):
        errors.append("duplicate original SRD entity in migration ledger")
    if len(current_ids) != len(set(current_ids)):
        errors.append("duplicate current SRD entity in coverage manifest")
    if set(initial_ids) != set(current_ids):
        errors.append("current SRD logical identities differ from original migration ledger")
    initial_kinds = {entry["id"]: entry["kind"] for entry in source_rows}
    for row in current_rows:
        if initial_kinds.get(row["nativeId"]) != row["kind"]:
            errors.append(f"source kind changed for original SRD identity: {row['nativeId']}")
    return errors, len(files)


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
    ledger_errors, _ = check_migration_ledger()
    errors.extend(ledger_errors)
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
    for ident, locations in working_ids.items():
        if len(locations) != 1:
            errors.append(f"duplicate working ID {ident}: {locations}")
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
