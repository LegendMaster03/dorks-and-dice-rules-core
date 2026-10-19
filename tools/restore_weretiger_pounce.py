#!/usr/bin/env python3
"""Restore the older Weretiger Pounce on the selected 5.5e stat block.

Only rename the replaced Claw attack reference to its newer Scratch attack;
retain the original movement, save DC, Prone effect and Bonus Action Bite.
Other old Pounce entries refer to an absent Bite attack and are not changed.
"""
from collections import Counter
import copy
import json
from pathlib import Path

from reconcile_native_baseline import ROOT, load, write

IDENTITY = "monster.weretiger"
TRAIT_NAME = "Pounce (Tiger or Hybrid Form Only)"
REFERENCE = ROOT / "data/reference/srd"
LEDGER = REFERENCE / "monster-legacy-variant-review.json"
STATS = ROOT / "docs/rewrite/native-reconciliation-statistics.json"
RESULTS = ROOT / "docs/rewrite/monster-legacy-feature-results.json"


def main():
    manifest = load(REFERENCE / "native-coverage-manifest.json")
    path = ROOT / next(x["path"] for x in manifest["coverage"]
                       if x["nativeId"] == IDENTITY)
    native = load(path)
    definition = native["definition"]
    actions = {x["name"] for x in definition.get("action", [])}
    assert "Scratch" in actions and "Bite (Tiger or Hybrid Form Only)" in actions
    assert "Claw" not in actions
    older = next(x for x in load(
        REFERENCE / "structured/bestiary/bestiary-srd51.json")["monster"]
        if x["name"] == "Weretiger")
    old_pounce = next(x for x in older["trait"] if x["name"] == TRAIT_NAME)
    restored = copy.deepcopy(old_pounce)
    assert len(restored["entries"]) == 1
    original = restored["entries"][0]
    assert "claw attack" in original and "one bite attack" in original
    restored["entries"] = [original.replace(
        "claw attack", "Scratch attack"
    ).replace(
        "one bite attack", "one Bite (Tiger or Hybrid Form Only) attack"
    )]
    found = [t for t in definition.get("trait", []) if t["name"] == TRAIT_NAME]
    if found:
        if found != [restored]:
            raise ValueError("Existing Pounce differs from approved faithful mapping")
    else:
        definition.setdefault("trait", []).append(restored)
        write(path, native)

    review = load(LEDGER)
    entry = next(x for x in review["entries"] if x["nativeId"] == IDENTITY)
    disposition = next(x for x in entry["featureDisposition"]["trait"]
                       if x["name"] == TRAIT_NAME)
    if disposition["status"] not in ("review-needed", "restored"):
        raise ValueError("Unexpected Pounce disposition: " + disposition["status"])
    disposition["status"] = "restored"
    disposition["note"] = (
        "Old Claw reference mapped to 5.5e Scratch; existing 5.5e Bite, "
        "15-foot approach, DC 14 Strength save, Prone and Bonus Action cost preserved"
    )
    counts = Counter()
    for row in review["entries"]:
        row["remainingFeatureQuestions"] = sum(
            x["status"] in ("review-needed", "possible-redesign")
            for features in row["featureDisposition"].values()
            for x in features
        )
        counts["monstersWithOpenFeatureQuestions"] += bool(row["remainingFeatureQuestions"])
        for features in row["featureDisposition"].values():
            for x in features:
                counts["feature:" + x["status"]] += 1
    review["summary"] = dict(sorted(counts.items()))
    write(LEDGER, review)

    stats = load(STATS)
    stats["monsterRemainingFeatureQuestions"] = sum(
        row["remainingFeatureQuestions"] for row in review["entries"]
    )
    stats["monsterReviewEntriesWithOpenQuestions"] = counts["monstersWithOpenFeatureQuestions"]
    stats["monsterFeatureDispositions"] = {
        k.removeprefix("feature:"): v
        for k, v in counts.items() if k.startswith("feature:")
    }
    # Set by reconciling current audit counts rather than incrementing on each run.
    stats["monsterAdditionalOlderFeaturesRestored"] = counts["feature:restored"]
    assert stats["monsterRemainingFeatureQuestions"] == 5
    assert stats["monsterReviewEntriesWithOpenQuestions"] == 5
    write(STATS, stats)

    results = load(RESULTS)
    results["summary"] = dict(sorted(counts.items()))
    results["totalAdditionalOlderFeaturesRestored"] = counts["feature:restored"]
    results["unresolvedFeatureQuestions"] = stats["monsterRemainingFeatureQuestions"]
    results["reviewEntriesWithOpenQuestions"] = stats["monsterReviewEntriesWithOpenQuestions"]
    results["weretigerPounceRestoration"] = {
        "nativeId": IDENTITY,
        "legacyAttackReference": "Claw",
        "nativeAttackReference": "Scratch",
        "nativeBonusAttack": "Bite (Tiger or Hybrid Form Only)",
        "sameMovementSaveAndCost": True,
    }
    write(RESULTS, results)
    print(json.dumps({
        "weretigerPounce": "restored",
        "totalRestoredFeatures": counts["feature:restored"],
        "remainingFeatureQuestions": stats["monsterRemainingFeatureQuestions"],
        "monstersWithOpenQuestions": counts["monstersWithOpenFeatureQuestions"],
    }, indent=2))


if __name__ == "__main__":
    main()
