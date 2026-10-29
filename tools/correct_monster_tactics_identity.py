#!/usr/bin/env python3
"""Correct monster identities: tactics do not make separate creature variants.

Consolidates old Hobgoblin and Bugbear names as aliases of their newer
Warrior identities while retaining the old mechanics in the non-runtime
review ledger. No numerical blending or automatic ability grant occurs.
"""
from collections import Counter
import json

from reconcile_native_baseline import ROOT, MANIFEST, REPORT, load, write
from reconcile_monster_families import legacy_differences

REF = ROOT / "data/reference/srd"
LEDGER = REF / "monster-legacy-variant-review.json"
FAMILIES = ROOT / "data/rulesets/first-party-001/working/monster-families.json"
MAPPINGS = {
    "Hobgoblin": "Hobgoblin Warrior",
    "Bugbear": "Bugbear Warrior",
}


def main():
    manifest = load(MANIFEST)
    rows = {row["nativeId"]: row for row in manifest["coverage"]}
    reviews = load(LEDGER)
    families = load(FAMILIES)
    old_sources = {item["name"]: item for item in load(
        REF / "structured/bestiary/bestiary-srd51.json")["monster"]}
    newer_sources = {item["name"]: item for item in load(
        REF / "structured/bestiary/bestiary-srd52.json")["monster"]}
    documents = {}
    edits = 0
    for source, target in MAPPINGS.items():
        identity = "monster." + source.lower()
        target_id = "monster." + target.lower().replace(" ", "-")
        row = rows[identity]
        doc_path = ROOT / row["path"]
        doc = load(doc_path)
        assert doc["id"] == identity, identity
        assert rows[target_id]["status"] == "native-single-source-retained"
        if row["status"] == "native-monster-identity-alias":
            if doc.get("aliasOf") != target_id or "definition" in doc:
                raise ValueError(f"Inconsistent alias: {identity}")
        else:
            if row["status"] != "native-single-source-retained" or "definition" not in doc:
                raise ValueError(f"Unexpected old monster record: {identity}")
            del doc["definition"]
            doc["baselineStatus"] = "native-identity-alias"
            doc["aliasOf"] = target_id
            row["status"] = "native-monster-identity-alias"
            row["aliasOf"] = target_id
            row["nativeShapingRequired"] = False
            edits += 1
            write(doc_path, doc)
        row["legacyVariantReviewRequired"] = True
        differences = legacy_differences(old_sources[source], newer_sources[target])
        record = next((x for x in reviews["entries"] if x["nativeId"] == identity), None)
        if record is None:
            raise ValueError(f"Missing retained-abilities review: {identity}")
        record.pop("familyId", None)
        record["correspondingDefault"] = target_id
        record["olderAbilitiesNotIncludedAutomatically"] = differences
        record["reason"] = (
            "Reconcile old mechanical options into one coherent creature; "
            "different tactics do not constitute a separate monster variant"
        )
        if not any(x["old"] == source for x in reviews["roleMappings"]):
            reviews["roleMappings"].append({"old": source, "current": target})

    reviews["roleMappings"].sort(key=lambda x: x["old"])
    reviews.pop("distinctLegacyRoleCandidates", None)
    reviews["mechanicalOptionReview"] = {
        source: {
            "default": "monster." + target.lower().replace(" ", "-"),
            "policy": "Review older capabilities for inclusion in the single existing creature, not a tactical clone",
        }
        for source, target in MAPPINGS.items()
    }
    for family in families["monsterFamilies"]:
        if family["id"] in ("monster-family.hobgoblin", "monster-family.bugbear"):
            legacy_id = family["id"].replace("monster-family.", "monster.")
            family.pop("legacyTacticalReview", None)
            family["legacyMechanicsReview"] = [legacy_id]
        else:
            family.pop("legacyTacticalReview", None)

    write(LEDGER, reviews)
    write(FAMILIES, families)
    statuses = Counter(row["status"] for row in manifest["coverage"])
    manifest["reconciliationCounts"] = dict(sorted(statuses.items()))
    write(MANIFEST, manifest)
    report = load(REPORT)
    report["statuses"] = dict(sorted(statuses.items()))
    report["nativeMonsterIdentityAliases"] = statuses["native-monster-identity-alias"]
    report["monsterOlderOnlySourceEntriesStillRetained"] = sum(
        row["kind"] == "monster" and row["status"] == "native-single-source-retained"
        for row in manifest["coverage"]
    )
    report["reconciledByApprovedGeneralRules"]["native-single-source-retained"] = statuses[
        "native-single-source-retained"
    ]
    report["retainedDefinitionsNeedingNativeShaping"] = sum(
        statuses[s] for s in (
            "native-compatible-merged", "native-equivalent-reconciled",
            "native-single-source-retained", "native-monster-role-default",
        )
    )
    report["monsterLegacyVariantReviewEntries"] = len(reviews["entries"])
    report["monsterReviewCaveat"] = (
        "Tactics alone do not create independent monsters. The older Hobgoblin "
        "and Bugbear are aliases of newer Warrior identities; their mechanical "
        "differences remain in the review ledger for a coherent single-stat-block "
        "decision. Remaining native schema/markup conversion is pending."
    )
    assert statuses["native-monster-identity-alias"] == 23
    assert statuses["multiple-candidates"] + statuses["single-candidate"] == 1663
    write(REPORT, report)
    print(json.dumps({
        "aliasesAdded": edits,
        "aliasTotal": statuses["native-monster-identity-alias"],
        "remainingSourceOnlyMonsters": report["monsterOlderOnlySourceEntriesStillRetained"],
        "legacyReviewEntries": len(reviews["entries"]),
        "unresolvedCandidateEntries": report["candidateRecordsStillUnresolved"]
    }, indent=2))


if __name__ == "__main__":
    main()
