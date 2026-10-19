#!/usr/bin/env python3
"""Conservative, deterministic reconciliation of the SRD native seed.

Applies approved policies for uniquely occurring compatible catalog entries,
equivalent definitions and mechanically compatible weapon mastery additions.
Never selects a conflicting quantity or fundamentally changed procedure.
The retained native definition still needs runtime-schema and markup shaping.
"""
import copy
from collections import Counter
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/native"
MANIFEST = ROOT / "data/reference/srd/native-coverage-manifest.json"
REPORT = ROOT / "docs/rewrite/native-reconciliation-statistics.json"

# Single-source categories where merely retaining the independently
# identified content does not change common action, condition, progression
# or entitlement mechanics. Features and general rules require deeper review.
CATALOG_ONLY_KINDS = {
    "monster", "spell", "item", "baseitem", "magicvariant",
    "itemEntry", "itemGroup", "itemType", "itemProperty",
    "deity", "language", "sense", "hazard", "table", "trap",
    "vehicle", "object", "background", "feat", "itemMastery",
}
CANDIDATE_STATUSES = {"single-candidate", "multiple-candidates"}
RECONCILED_STATUSES = {
    "native-single-source-retained",
    "native-equivalent-reconciled",
    "native-compatible-merged",
}
REFERENCE_KEYS = {
    "source", "page", "srd", "srd52", "classSource", "subclassSource",
    "otherSources", "reprintedAs",
}
REF_SUFFIX = re.compile(r"\|srd(?:51|52)(?=\||\}|$)", re.IGNORECASE)


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def write(path, data):
    serialized = json.dumps(data, ensure_ascii=False, indent=2) + "\n"
    if not path.exists() or path.read_text(encoding="utf-8") != serialized:
        path.write_text(serialized, encoding="utf-8")


def comparable(value):
    """Strip only formatting/provenance differences we can safely identify."""
    if isinstance(value, str):
        return REF_SUFFIX.sub("", value)
    if isinstance(value, list):
        return [comparable(child) for child in value]
    if isinstance(value, dict):
        return {
            key: comparable(child)
            for key, child in value.items()
            if key not in REFERENCE_KEYS
        }
    return value


def is_compatible_mastery_addition(kind, a, b):
    """Weapon mastery is a separately eligible property, not a free attack."""
    if kind != "baseitem":
        return None
    for common, extended in ((a, b), (b, a)):
        if "mastery" in common or "mastery" not in extended:
            continue
        stripped = {k: v for k, v in extended.items() if k != "mastery"}
        if comparable(common) == comparable(stripped):
            merged = copy.deepcopy(extended)
            return merged
    return None


def select_definition(row, native):
    variants = native.get("candidateDefinitions")
    if not isinstance(variants, list) or not variants:
        return None
    payloads = [v.get("content") for v in variants]
    if not all(isinstance(x, dict) for x in payloads):
        return None
    kind = row["kind"]
    if len(payloads) == 1:
        if kind in CATALOG_ONLY_KINDS:
            return "native-single-source-retained", payloads[0]
        return None
    if len(payloads) == 2:
        a, b = payloads
        if comparable(a) == comparable(b):
            return "native-equivalent-reconciled", a
        merged = is_compatible_mastery_addition(kind, a, b)
        if merged is not None:
            return "native-compatible-merged", merged
    return None


def native_entity(doc, identity):
    if isinstance(doc, dict) and doc.get("id") == identity:
        return doc
    if isinstance(doc, dict):
        found = [
            record for value in doc.values() if isinstance(value, list)
            for record in value if isinstance(record, dict)
            and record.get("id") == identity
        ]
        if len(found) == 1:
            return found[0]
    raise ValueError(f"Expected exactly one record for {identity}")


def retained_definition(content, name):
    # Keep all rule text and properties rather than accidentally dropping a
    # mechanical distinction. The later native-shaping pass will convert any
    # remaining 5etools markup. Source attribution is tracked outside native.
    data = {
        key: copy.deepcopy(value)
        for key, value in content.items()
        if key not in REFERENCE_KEYS and not (key == "name" and value == name)
    }
    return data


def apply():
    manifest = load(MANIFEST)
    documents = {}
    changed = Counter()
    changed_ids = []
    for row in manifest["coverage"]:
        if row["status"] not in CANDIDATE_STATUSES:
            continue
        location = ROOT / row["path"]
        if location not in documents:
            documents[location] = load(location)
        doc = documents[location]
        native = native_entity(doc, row["nativeId"])
        resolution = select_definition(row, native)
        if resolution is None:
            continue
        status, content = resolution
        if "definition" in native:
            raise ValueError(f"Would overwrite native definition: {row['nativeId']}")
        del native["candidateDefinitions"]
        native["baselineStatus"] = "retained-native-definition"
        native["definition"] = retained_definition(content, native["name"])
        row["status"] = status
        row["nativeShapingRequired"] = True
        changed[status] += 1
        changed_ids.append(row["nativeId"])

    # Persist the data only after all cross-file checks succeed.
    for location, document in documents.items():
        write(location, document)
    counts = Counter(row["status"] for row in manifest["coverage"])
    manifest["reconciliationStatus"] = (
        "approved-general-rules-applied; unresolved-mechanics-retained; native-shaping-pending"
    )
    manifest["reconciliationCounts"] = dict(sorted(counts.items()))
    write(MANIFEST, manifest)
    report = {
        "description": "Rule status counts for the pinned staged source pack; not a runtime readiness certificate.",
        "baselineEntries": len(manifest["coverage"]),
        "statuses": dict(sorted(counts.items())),
        "nativeCatalogRecords": sum(counts.values()),
        "resolvedThisRun": dict(sorted(changed.items())),
        "remainingWithMultipleCandidates": counts["multiple-candidates"],
        "remainingWithOneCandidate": counts["single-candidate"],
        "retainedDefinitionsNeedingNativeShaping": sum(
            counts[status] for status in RECONCILED_STATUSES
        ),
        "previouslyMatchingItemTypes": counts["matching-candidates"],
        "preexistingApprovedNativeCoverage": counts["existing-native-covered"],
        "candidateRecordsStillUnresolved": (
            counts["single-candidate"] + counts["multiple-candidates"]
        ),
        "remainingQuestions": [
            "Conflicting numeric values require field- and family-level decisions.",
            "Substantively different procedures require explicit decisions.",
            "Class features, progression and core actions require entitlement-aware review.",
            "Retained records may include imported inline markup; conversion to final typed native execution schemas remains necessary.",
        ],
    }
    write(REPORT, report)
    print(json.dumps(report, indent=2))
    return report


if __name__ == "__main__":
    apply()
