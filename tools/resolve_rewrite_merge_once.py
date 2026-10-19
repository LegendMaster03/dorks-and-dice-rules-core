#!/usr/bin/env python3
"""One-time three-way integration of separately verified monster/source corrections.

Executed only inside a GitHub Actions merge-conflict workspace. Does not merge
to rewrite or main. Removed from the feature branch after successful validation.
"""
from collections import Counter
import copy
import json
from pathlib import Path
import subprocess


def show(stage, path):
    return json.loads(subprocess.check_output(
        ["git", "show", f":{stage}:{path}"], text=True))


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write(path, obj):
    Path(path).write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n",
                          encoding="utf-8")
    subprocess.run(["git", "add", path], check=True)


def require(condition, message):
    if not condition:
        raise ValueError(message)


DRAGONS = [
    "data/native/bestiary/adult-black-dragon.json",
    "data/native/bestiary/ancient-black-dragon.json",
]
for path in DRAGONS:
    ours, theirs = show(2, path), show(3, path)
    newer = theirs["candidateDefinitions"][1]["content"]
    require(ours["baselineStatus"] == "retained-native-definition", path)
    for key in ("action", "spellcasting"):
        require(key in newer and key in ours["definition"], path + ":" + key)
    original = ours["definition"]
    require("Melf's Acid Arrow" in original["action"][0]["entries"][0], path)
    require("Acid Arrow" in newer["action"][0]["entries"][0], path)
    original["action"][0]["entries"][0] = newer["action"][0]["entries"][0]
    original["spellcasting"][0]["will"][2] = newer["spellcasting"][0]["will"][2]
    write(path, ours)

ITEMS = [
    "data/native/items/magic/dragon-orb.json",
    "data/native/items/magic/mysterious-deck.json",
    "data/native/items/magic/spider-s-sting.json",
]
for path in ITEMS:
    ours, theirs = show(2, path), show(3, path)
    newer = theirs["candidateDefinitions"][0]["content"]
    require(ours["baselineStatus"] == "retained-native-definition", path)
    require(len(ours["definition"]["entries"]) >= len(newer["entries"]), path)
    ours["definition"]["entries"] = copy.deepcopy(newer["entries"])
    if "seeAlsoDeck" not in newer:
        ours["definition"].pop("seeAlsoDeck", None)
    write(path, ours)

path = "data/native/bestiary/avatar-of-death.json"
ours, theirs = show(2, path), show(3, path)
require(len(theirs["candidateDefinitions"]) == 2, "Expected official Avatar addition")
newer = copy.deepcopy(theirs["candidateDefinitions"][1]["content"])
require("Multiattack" in [f["name"] for f in newer["action"]], "Newer Avatar action missing")
turning = next(x for x in ours["definition"]["trait"] if x["name"] == "Turning Immunity")
require(not any(x["name"] == "Turning Immunity" for x in newer["trait"]),
        "Newer Avatar already has old Turning Immunity")
newer.setdefault("trait", []).append(copy.deepcopy(turning))
newer["lore"] = copy.deepcopy(ours["definition"].get("lore", []))
for tag in ("hasToken", "languageTags", "damageTags"):
    if tag in ours["definition"] and tag not in newer:
        newer[tag] = copy.deepcopy(ours["definition"][tag])
newer["traitTags"] = ["Incorporeal Movement", "Turning Immunity"]
ours["definition"] = newer
write(path, ours)

# New official 5.5e magic-item counterpart has a different 10-minute duration
# and valid-armor scope. That is an unresolved item rule, not a duplicate item.
path = "data/native/items/magic/plate-armor-of-etherealness.json"
theirs = json.loads(subprocess.check_output(
    ["git", "show", "origin/feature/rules-core-rewrite:" + path], text=True))
require(len(theirs["candidateDefinitions"]) == 2, "Plate armor candidate correction missing")
write(path, theirs)

# Preserve the feature branch's reconciled manifest, then apply every
# independent source fidelity correction from rewrite (including newly
# discovered candidate provenance) by stable nativeId.
path = "data/reference/srd/native-coverage-manifest.json"
ours, theirs = show(2, path), show(3, path)
byid = {entry["nativeId"]: entry for entry in theirs["coverage"]}
for changed_id in (
    "action.use-an-object", "baseitem.musket", "baseitem.pistol",
    "feat.magic-initiate", "feat.savage-attacker", "feat.skilled",
    "item.plate-armor-of-etherealness", "monster.avatar-of-death",
):
    require(changed_id in byid, f"Missing revised manifest row: {changed_id}")
for idx, row in enumerate(ours["coverage"]):
    id_ = row["nativeId"]
    target_id = "action.use-an-object" if id_ == "action.use-an-item" else id_
    if target_id not in (
        "action.use-an-object", "baseitem.musket", "baseitem.pistol",
        "feat.magic-initiate", "feat.savage-attacker", "feat.skilled",
        "item.plate-armor-of-etherealness", "monster.avatar-of-death",
    ):
        continue
    revised = copy.deepcopy(byid[target_id])
    if target_id == "monster.avatar-of-death":
        revised["status"] = "native-compatible-merged"
        revised["nativeShapingRequired"] = True
        revised["legacyVariantReviewRequired"] = False
    elif target_id == "item.plate-armor-of-etherealness":
        revised["status"] = "multiple-candidates"
        revised["nativeShapingRequired"] = True
    ours["coverage"][idx] = revised
ours["unresolvedMultipleSourceRecordKeys"] = copy.deepcopy(
    theirs["unresolvedMultipleSourceRecordKeys"])
counts = Counter(r["status"] for r in ours["coverage"])
require(len(ours["coverage"]) == 2522, "Manifest changed identity count")
require(len({r["nativeId"] for r in ours["coverage"]}) == 2522,
        "Manifest has duplicate identity")
ours["reconciliationCounts"] = dict(sorted(counts.items()))
write(path, ours)

path = "docs/rewrite/native-reconciliation-statistics.json"
stats = read(path)
stats["statuses"] = dict(sorted(counts.items()))
stats["reconciledByApprovedGeneralRules"] = {
    s: counts[s] for s in (
        "native-compatible-merged", "native-equivalent-reconciled",
        "native-single-source-retained",
    )}
stats["remainingWithMultipleCandidates"] = counts["multiple-candidates"]
stats["remainingWithOneCandidate"] = counts["single-candidate"]
stats["candidateRecordsStillUnresolved"] = (
    counts["multiple-candidates"] + counts["single-candidate"])
stats["reducedUnresolvedCandidateCount"] = (
    stats["originalUnresolvedCandidateCount"]
    - stats["candidateRecordsStillUnresolved"])
stats["retainedDefinitionsNeedingNativeShaping"] = sum(
    counts[s] for s in (
        "native-compatible-merged", "native-equivalent-reconciled",
        "native-single-source-retained", "native-monster-role-default"))
stats["monsterOlderOnlySourceEntriesStillRetained"] = sum(
    r["kind"] == "monster" and r["status"] == "native-single-source-retained"
    for r in ours["coverage"])
stats["officialSourceReconciliationCorrections"] = {
    "avatarOfDeath": "5.5e creature selected with unique older Turning Immunity retained",
    "plateArmorOfEtherealness": "two nonequivalent sources preserved pending item decision",
}
require(stats["candidateRecordsStillUnresolved"] == 1664,
        "Unexpected candidate backlog after fidelity corrections")
write(path, stats)

# Keep the coverage test's updated monster selections AND the rewrite
# branch's expanded 30-condition inventory/conditions-test behavior.
path = "tests/test_native_baseline_coverage.py"
content = subprocess.check_output(["git", "show", ":2:" + path], text=True)
needle = '            if row["status"] == "matching-candidates":'
block = '''            if cid.startswith("condition.") or cid == "rule.conditions":
                self.assertNotIn("baselineStatus", native, cid)
                self.assertNotIn("candidateDefinitions", native, cid)
                if cid.startswith("condition."):
                    approved = {
                        decision["nativeRuleId"] for decision in
                        load(REF / "reconciliation-decisions.json")["decisions"]
                    }
                    self.assertIn(cid, approved, cid)
                else:
                    self.assertEqual(30, len(native["conditionIds"]))
                continue
'''
require(needle in content and "if cid.startswith(\"condition.\")" not in content,
        "Coverage test changed unexpectedly")
content = content.replace(needle, block + needle)
content = content.replace("self.assertEqual(15, len(conditions))",
                          "self.assertEqual(30, len(conditions))")
content = content.replace(
    'self.assertEqual(9, sum(x.get("baselineStatus") ==',
    'self.assertEqual(0, sum(x.get("baselineStatus") ==')
Path(path).write_text(content, encoding="utf-8")
subprocess.run(["git", "add", path], check=True)

path = "tests/test_monster_family_reconciliation.py"
content = Path(path).read_text(encoding="utf-8")
require("self.assertEqual(1663, report" in content, "Unexpected family tests")
content = content.replace("self.assertEqual(1663, report",
                          "self.assertEqual(1664, report")
Path(path).write_text(content, encoding="utf-8")
subprocess.run(["git", "add", path], check=True)

path = "tests/test_monster_catalog_invariants.py"
content = Path(path).read_text(encoding="utf-8")
needle = '                current = native["definition"]\n                source = self.old[name]'
repl = '''                current = native["definition"]
                if name == "Avatar of Death":
                    # Official 5.5e source correction recovered an omitted
                    # same-identity creature. One native 5.5e definition
                    # retains the older unique Turning Immunity trait.
                    self.assertEqual("native-compatible-merged",
                                     self.rows[native["id"]]["status"])
                    self.assertIn("Multiattack",
                                  [x["name"] for x in current["action"]])
                    self.assertIn("Turning Immunity",
                                  [x["name"] for x in current["trait"]])
                    continue
                source = self.old[name]'''
require(needle in content, "Monster test insertion missing")
content=content.replace(needle,repl)
Path(path).write_text(content,encoding="utf-8")
subprocess.run(["git","add",path],check=True)

# Update report prose without changing the older pinned 285-pair count.
for path in ("docs/rewrite/native-baseline-consistency-gate.md",
             "docs/rewrite/monster-variant-family-model.md"):
    content = Path(path).read_text(encoding="utf-8")
    content = content.replace("1,073 still hold multiple candidate definitions and 590 hold single candidates: **1,663 candidate records**",
                              "1,069 still hold multiple candidate definitions and 595 hold single candidates: **1,664 candidate records**")
    content = content.replace("**1,663**", "**1,664**")
    content = content.replace("The original 2,479 pending candidate records decreased by **816 (32.9%)**",
                              "The original 2,479 pending candidate records decreased by **815 (32.9%)**")
    content = content.replace("the count of 1,663", "the count of 1,664")
    content = content.replace("The candidate backlog remains **1,664**",
                              "The candidate backlog is **1,664** after retaining the newly identified conflicting Plate Armor of Etherealness item as pending")
    if "monster-variant-family" in path:
        content = content.replace("**64** single-source monster records remain retained",
                                  "**63** single-source monster records remain retained")
        content = content.replace("**64 source-unique monster entries**",
                                  "**63 source-unique monster entries**")
        content += "\n**Official source correction during branch integration:** Avatar of Death now uses the corrected 5.5e default plus unique legacy Turning Immunity. Plate Armor of Etherealness has a substantive older/newer duration and eligible-armor difference and is pending an item-specific decision; it does not represent an additional monster decision.\n"
    else:
        content += "\n**Official source correction during branch integration:** The selected native Avatar of Death incorporates the newly identified 5.5e candidate plus unique older Turning Immunity. Plate Armor of Etherealness now has two distinct source candidates and remains pending item adjudication; no magic-item default was invented.\n"
    Path(path).write_text(content, encoding="utf-8")
    subprocess.run(["git", "add", path], check=True)

require(not subprocess.check_output(
    ["git", "diff", "--name-only", "--diff-filter=U"], text=True).strip(),
    "Conflict resolution left unresolved files")

print(json.dumps({
    "status": "resolved conflicting source and native records",
    "statuses": dict(counts),
    "pending": stats["candidateRecordsStillUnresolved"],
}, indent=2))
