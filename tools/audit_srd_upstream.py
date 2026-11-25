"""Compare a pinned upstream 5e.tools checkout with the existing SRD promotion inventory.

This is diagnostic only: flags, aliases and matching names do not establish
licensing, official fidelity, or authority to replace a reviewed native record.
Never serialize upstream record bodies into the report.
"""
import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path
import subprocess

from inventory_srd_editions import ROOT
from stage_srd_editions import PACKAGES, build
from report_srd_promotion import REPO, is_foundry_metadata, load_decisions, native_rows, report
from srd_upstream_crosswalk import Crosswalk

UPSTREAM_COMMIT = "2512db75eb48ef06f6190b086a82430887906dca"
FLAGS = {"srd": "srd-5.1", "srd52": "srd-5.2.1"}
SOURCES = {
    "srd-5.1": {"phb", "mm", "dmg", "srd51"},
    "srd-5.2.1": {"xphb", "xmm", "xdmg", "srd52"},
}
CONTEXT = ("raceName", "raceSource", "className", "classSource",
           "subclassShortName", "subclassSource", "level")


def norm(value):
    return str(value if value is not None else "").strip().casefold()


def name_of(record):
    return record.get("name") or record.get("abbreviation") or record.get("id")


def key(edition, kind, record, name):
    # Only normalize publication identities in the matching edition. Other
    # publication sources must remain visible rather than masquerading as SRD.
    context = tuple(
        edition if field.endswith("Source") and norm(record.get(field)) in SOURCES[edition]
        else norm(record.get(field)) for field in CONTEXT
    )
    return (kind, norm(name), *context)


def flag_value(value):
    return value is True or (isinstance(value, str) and bool(value.strip()))


def candidate_flags(record):
    inherited = record.get("inherits")
    if not isinstance(inherited, dict):
        inherited = {}
    for flag, edition in FLAGS.items():
        # A top-level explicit value takes precedence over an inherited one.
        origin = record if flag in record else inherited
        value = origin.get(flag)
        if flag_value(value):
            yield edition, value, flag if origin is record else "inherits/" + flag


def pointer_part(value):
    return str(value).replace("~", "~0").replace("/", "~1")


def flagged_nested(value):
    """Count flags outside promoted top-level occurrences, without emitting bodies."""
    if isinstance(value, dict):
        yield from (f for f in FLAGS if flag_value(value.get(f)))
        for child in value.values():
            yield from flagged_nested(child)
    elif isinstance(value, list):
        for child in value:
            yield from flagged_nested(child)


def upstream_occurrences(document):
    arrays = document if isinstance(document, dict) else {"$": document}
    for kind, rows in sorted(arrays.items()):
        if not isinstance(rows, list):
            continue
        for index, record in enumerate(rows):
            if not isinstance(record, dict):
                continue
            pointer = ("" if kind == "$" else "/" + pointer_part(kind)) + "/" + str(index)
            yield kind, pointer, record


def git(checkout, *args):
    return subprocess.check_output(["git", "-C", str(checkout), *args], text=True)


def checkout_files(checkout):
    if git(checkout, "rev-parse", "HEAD").strip() != UPSTREAM_COMMIT:
        raise ValueError("Unexpected upstream commit")
    if git(checkout, "status", "--porcelain", "--untracked-files=all", "--", "data").strip():
        raise ValueError("Upstream data checkout is modified")
    paths = git(checkout, "ls-tree", "-r", "--name-only", "HEAD", "--", "data").splitlines()
    if not paths:
        raise ValueError("Missing upstream data inventory")
    for path in sorted(paths):
        file = checkout / path
        if not file.is_file() or file.is_symlink():
            raise ValueError("Missing or unsupported upstream data file: " + path)
        yield path, file


def classify_rules_matches(matches, dispositions):
    rules = [row for row in matches if not is_foundry_metadata(row["source"].get("path", ""))]
    metadata = len(matches) - len(rules)
    # Preserve raw identity collisions while separating rules sources from
    # renderer sidecars. A reviewed exclusion is not a promoted definition.
    active = [row for row in rules if dispositions[row["candidateId"]]["status"] != "documented-not-promoted"]
    if len(rules) == 1:
        status = "unique-rules-source"
    elif len(rules) > 1 and len(active) == 1 and dispositions[active[0]["candidateId"]]["nativeIds"]:
        status = "one-promoted-with-reviewed-exclusions"
    elif len(rules) > 1:
        status = "ambiguous-rules-sources"
    elif metadata:
        status = "metadata-only"
    else:
        status = "no-rules-source"
    return {"status": status, "rulesSourceCount": len(rules), "metadataCount": metadata,
            "activeRulesSourceCount": len(active)}


def compare_candidate(edition, kind, record, value, indexes, native_names, dispositions):
    original = name_of(record)
    name = value if isinstance(value, str) else original
    matches = indexes[edition].get(key(edition, kind, record, name), []) if name else []
    # Exact contextual identities can still collide; never choose a winner
    # based on file order. Name-only native matches are separate review hints.
    status = ("no-identity" if not name else "not-in-pinned-corpus" if not matches
              else "unique-pinned-identity" if len(matches) == 1 else "ambiguous-pinned-identity")
    result = {
        "edition": edition, "kind": kind, "name": name, "status": status,
        "publication": record.get("source") or (
            record.get("inherits", {}).get("source") if isinstance(record.get("inherits"), dict) else None),
        "nativeNameHints": sorted(native_names[edition].get(norm(name), [])) if name else [],
        "rulesSourceDisposition": classify_rules_matches(matches, dispositions[edition]),
        "pinnedMatches": [
            {"source": row["source"],
             "promotionStatus": dispositions[edition][row["candidateId"]]["status"],
             "nativeIds": dispositions[edition][row["candidateId"]]["nativeIds"]}
            for row in matches
        ],
    }
    if len(matches) == 1:
        pinned = matches[0]["payload"]
        # Field-level triage only. Navigation/source renaming and deliberate
        # publisher corrections will also produce differences.
        result["differentSharedFields"] = sorted(
            field for field in record.keys() & pinned.keys() if record[field] != pinned[field])
        result["upstreamOnlyFields"] = sorted(record.keys() - pinned.keys())
        result["pinnedOnlyFields"] = sorted(pinned.keys() - record.keys())
    return result


# These statuses establish a bounded representation or an explicit publisher
# scope decision, not full text equivalence or redistribution approval.
CROSSWALK_DISPOSITIONS = {
    "expanded-source-pointer", "merged-unnamed-subrace",
    "canonical-deck-parent", "native-table-title-match",
    "nested-card-effect-heading", "nested-card-table-row",
    "nested-table-title-match", "normalized-collection",
    "not-published-as-table", "publisher-different-table-context",
    "publisher-narrower-table", "publisher-verified-normalized-collection",
    "publisher-different-table-structure", "reviewed-related-native-identity",
}


def coverage_issues(occurrences, native_ids):
    """Detect unclassified source occurrences and stale edition-local targets."""
    issues = []
    for row in occurrences:
        reasons = []
        if row["rulesSourceDisposition"]["status"] in {"ambiguous-rules-sources", "metadata-only"}:
            reasons.append("unresolved-rules-source")
        if row["status"] in {"not-in-pinned-corpus", "no-identity"}:
            crosswalk = row.get("nativeCrosswalk", {})
            status = crosswalk.get("status")
            targets = crosswalk.get("nativeIds", [])
            if status not in CROSSWALK_DISPOSITIONS:
                reasons.append("unclassified-crosswalk")
            elif status != "not-published-as-table" and not targets:
                reasons.append("missing-crosswalk-target")
            elif status == "not-published-as-table" and targets:
                reasons.append("excluded-table-has-target")
            if set(targets) - native_ids[row["edition"]]:
                reasons.append("unknown-edition-local-crosswalk-target")
        if reasons:
            issues.append({k: row[k] for k in ("edition", "path", "pointer", "kind", "name")}
                          | {"reasons": reasons})
    return issues


def audit(checkout):
    candidates = build()
    indexes = {edition: defaultdict(list) for edition in PACKAGES}
    native_names = {edition: defaultdict(list) for edition in PACKAGES}
    dispositions = {}
    expanded_links = {edition: defaultdict(list) for edition in PACKAGES}
    crosswalks = {}
    for edition, package in PACKAGES.items():
        native = list(native_rows(REPO / "data" / "rulesets" / package / "native"))
        crosswalks[edition] = Crosswalk(edition, native)
        for row in native:
            if row.get("name"):
                native_names[edition][norm(row["name"])].append(row["id"])
            pointer = row.get("source", {}).get("expandedStructuredSource")
            if pointer:
                if pointer.get("commit") != UPSTREAM_COMMIT or pointer.get("provider") != "5etools-mirror-3/5etools-src":
                    raise ValueError("Unexpected expanded source pin: " + row["id"])
                if row["source"].get("release") != edition:
                    raise ValueError("Wrong expanded source edition: " + row["id"])
                expanded_links[edition][(pointer["path"], pointer["pointer"])].append(row["id"])
        for row in candidates[edition]:
            payload = row["payload"]
            if name_of(payload):
                indexes[edition][key(edition, row["sourceKind"], payload, name_of(payload))].append(row)
        promotion = report(edition, candidates[edition], native, list(load_decisions(ROOT, edition)))
        dispositions[edition] = {row["candidateId"]: row for row in promotion["occurrences"]}

    files, occurrences = [], []
    totals = {edition: Counter() for edition in PACKAGES}
    rules_totals = {edition: Counter() for edition in PACKAGES}
    gap_kinds = {edition: Counter() for edition in PACKAGES}
    crosswalk_totals = {edition: Counter() for edition in PACKAGES}
    for path, file in checkout_files(checkout):
        entry = {"path": path, "bytes": file.stat().st_size}
        files.append(entry)
        if file.suffix != ".json":
            entry["status"] = "non-json"
            continue
        document = json.loads(file.read_text(encoding="utf-8"))
        groups = Counter()
        represented_flags = Counter()
        for kind, pointer, record in upstream_occurrences(document):
            groups[kind] += 1
            for edition, value, flag in candidate_flags(record):
                row = compare_candidate(edition, kind, record, value, indexes, native_names, dispositions)
                row.update({"path": path, "pointer": pointer, "flag": flag,
                            "expandedSourceNativeIds": sorted(expanded_links[edition].pop((path, pointer), []))})
                if row["status"] in {"not-in-pinned-corpus", "no-identity"}:
                    row["nativeCrosswalk"] = crosswalks[edition].classify(row, record)
                    crosswalk_totals[edition][row["nativeCrosswalk"]["status"]] += 1
                occurrences.append(row)
                totals[edition][row["status"]] += 1
                rules_totals[edition][row["rulesSourceDisposition"]["status"]] += 1
                if row["status"] == "not-in-pinned-corpus" and not row["expandedSourceNativeIds"]:
                    gap_kinds[edition][kind] += 1
                represented_flags[flag.rsplit("/", 1)[-1]] += 1
        all_flags = Counter(flagged_nested(document))
        entry["topLevelObjectCounts"] = dict(sorted(groups.items()))
        entry["flagsOutsideCandidateSelection"] = dict(sorted((all_flags - represented_flags).items()))
    if any(expanded_links[edition] for edition in PACKAGES):
        raise ValueError("Unmatched expanded upstream native source pointer")
    return {"status": "diagnostic-only; not licensing or official fidelity verification",
            "upstreamRepository": "5etools-mirror-3/5etools-src", "upstreamCommit": UPSTREAM_COMMIT,
            "fileCount": len(files), "files": files,
            "identityCounts": {edition: dict(sorted(counts.items())) for edition, counts in totals.items()},
            "rulesSourceCounts": {edition: dict(sorted(counts.items())) for edition, counts in rules_totals.items()},
            "unmatchedUnlinkedByKind": {edition: dict(sorted(counts.items())) for edition, counts in gap_kinds.items()},
            "nativeCrosswalkCounts": {edition: dict(sorted(counts.items())) for edition, counts in crosswalk_totals.items()},
            "coverageIssues": coverage_issues(occurrences, {
                edition: set(crosswalk.rows) for edition, crosswalk in crosswalks.items()}),
            "occurrences": occurrences}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--checkout", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--require-classified-coverage", action="store_true",
                        help="Fail on unresolved rules sources or crosswalk dispositions/targets")
    args = parser.parse_args()
    output = args.output.resolve()
    if REPO.resolve() in output.parents and (REPO / "build" / "srd-review").resolve() not in output.parents:
        parser.error("Use an external report path or build/srd-review")
    result = audit(args.checkout.resolve())
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"fileCount": result["fileCount"], "identityCounts": result["identityCounts"],
                      "rulesSourceCounts": result["rulesSourceCounts"],
                      "unmatchedUnlinkedByKind": result["unmatchedUnlinkedByKind"],
                      "nativeCrosswalkCounts": result["nativeCrosswalkCounts"],
                      "coverageIssues": result["coverageIssues"]}, indent=2))
    if args.require_classified_coverage and result["coverageIssues"]:
        raise SystemExit("Unresolved upstream source coverage; see report")


if __name__ == "__main__":
    main()
