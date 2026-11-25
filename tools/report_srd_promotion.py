"""Report promotion coverage without interpreting SRD tags as rights approval.

Explicit source pointers establish occurrence linkage, not semantic fidelity.
Publisher-extracted spells are matched on the previously reviewed named inventory
only. Everything else stays unreviewed unless separately adjudicated.
"""
import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path

from stage_srd_editions import PACKAGES, build, slug
from inventory_srd_editions import ROOT

REPO = ROOT.parents[2]


def native_rows(root):
    for path in sorted(root.rglob("*.json")):
        value = json.loads(path.read_text(encoding="utf-8"))
        if isinstance(value, list):
            rows = value
        elif "id" in value:
            rows = [value]
        else:
            rows = [item for key, collection in value.items()
                    if not key.startswith("_") for item in collection]
        for row in rows:
            if row.get("kind") != "metadata":
                yield row


def report(edition, candidates, native, decisions=()):
    by_id = {row["id"]: row for row in native}
    if len(by_id) != len(native):
        raise ValueError("Duplicate native identity: " + edition)
    linked = defaultdict(list)
    for row in native:
        source = row.get("source", {})
        if source.get("release") != edition:
            continue
        pointers = [source.get("structuredSource"), *source.get("additionalStructuredSources", [])]
        for pointer in pointers:
            if pointer:
                key = pointer["path"], pointer["pointer"]
                if row["id"] not in linked[key]:
                    linked[key].append(row["id"])
    candidate_keys = {(row["source"]["path"], row["source"]["pointer"])
                      for row in candidates}
    missing_pointers = sorted(set(linked) - candidate_keys)
    if missing_pointers:
        raise ValueError("Native source pointers missing from edition: " + str(missing_pointers))

    # Only exact, whole-occurrence decisions can change an unlinked candidate's
    # disposition. A field exclusion is not an exclusion of the record.
    reviewed = defaultdict(list)
    for decision in decisions:
        key = decision.get("path"), decision.get("pointer")
        if key in candidate_keys and not decision.get("field"):
            reviewed[key].append(decision)
    counts = defaultdict(Counter)
    occurrences = []
    for candidate in candidates:
        source = candidate["source"]
        kind = candidate["sourceKind"]
        key = source["path"], source["pointer"]
        targets = linked.get(key, [])
        status = "unreviewed"
        if targets:
            status = "explicit-source-pointer"
        elif kind == "spell":
            identity = "spell." + slug(candidate["payload"]["name"])
            row = by_id.get(identity)
            if row and row.get("source", {}).get("release") == edition:
                targets = [identity]
                status = "publisher-name-match-only"
        if status == "unreviewed" and reviewed[key]:
            status = ("documented-deferred" if any(d["disposition"] == "deferred"
                                                  for d in reviewed[key])
                      else "documented-not-promoted")
        counts[kind][status] += 1
        occurrences.append({"candidateId": candidate["candidateId"],
                            "proposedIdentity": candidate["proposedIdentity"],
                            "source": source, "kind": kind, "status": status,
                            "nativeIds": targets, "decisions": reviewed[key]})
    matched = {identity for entry in occurrences for identity in entry["nativeIds"]}
    unresolved = [{"entity": row["id"], "target": target}
                  for row in native for target in row.get("catalogReferences", [])
                  if target not in by_id]
    return {
        "edition": edition,
        "candidateOccurrences": len(candidates),
        "nativeEntities": len(native),
        "nativeKinds": dict(sorted(Counter(row["kind"] for row in native).items())),
        "occurrenceStatusByKind": {kind: dict(sorted(values.items()))
                                   for kind, values in sorted(counts.items())},
        "nativeWithoutCandidateMatch": sorted(set(by_id) - matched),
        "unresolvedExplicitReferences": unresolved,
        "occurrences": occurrences,
    }


def load_decisions(root, edition):
    """Read documented dispositions, not inferred license approvals."""
    tag = "51" if edition == "srd-5.1" else "52"
    for group in ("item", "origins", "class", "feature", "general", "supplementary", "deity"):
        filename = f"{group}-promotion-{tag}.json"
        path = root / filename
        if not path.exists():
            continue
        report_data = json.loads(path.read_text(encoding="utf-8"))
        if report_data["edition"] != edition:
            raise ValueError("Wrong edition promotion report: " + filename)
        for section in ("excluded", "deferred"):
            for row in report_data.get(section, []):
                # Deity and vehicle records are explicitly awaiting further review.
                if group == "supplementary" and row.get("path") == "deities.json" and (root / "deity-promotion-51.json").exists():
                    continue  # Superseded by complete publisher table reconciliation.
                deferred = section == "deferred" or row.get("path") == "vehicles.json"
                yield dict(row, disposition="deferred" if deferred else "not-promoted",
                           evidence=filename)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    candidates = build()
    results = {edition: report(edition, candidates[edition], list(native_rows(
        REPO / "data" / "rulesets" / package / "native")), list(load_decisions(ROOT, edition)))
        for edition, package in PACKAGES.items()}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(results, ensure_ascii=False, indent=2) + "\n",
                           encoding="utf-8")
    if any(row["unresolvedExplicitReferences"] for row in results.values()):
        raise SystemExit("Unresolved explicit native catalog references")


if __name__ == "__main__":
    main()
