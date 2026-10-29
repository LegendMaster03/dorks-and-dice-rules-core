#!/usr/bin/env python3
"""Finalize routine removed monster features without mixing attack editions.

Restores absent independent resistances, healing and class-like traits.
Reuses 5.5e action/spell mechanics for old renamed features; ambiguous effects
remain in the non-runtime review ledger.
"""
from collections import Counter
import copy
import json

from reconcile_native_baseline import ROOT, load, write
from reconcile_monster_families import normalized_name

REF = ROOT / "data/reference/srd"
LEDGER = REF / "monster-legacy-variant-review.json"
STATS = ROOT / "docs/rewrite/native-reconciliation-statistics.json"
RESULTS = ROOT / "docs/rewrite/monster-legacy-feature-results.json"
RESTORE = {"Magic Weapons", "Regeneration", "Sneak Attack (1/Turn)"}
SUCCESSOR = {
    "Angelic Weapons": ("Holy Mace", "Radiant Sword", "Slaying Bow"),
    "Hellish Weapons": ("Withering Sword", "Fiendish", "Infernal"),
    "Heated Body": ("Fire Aura", "Heat Aura"),
    "Heated Weapons": ("Burning Hammer", "Flame Spear"),
    "Elemental Demise": ("Elemental Restoration",),
    "Rejuvenation": ("Spirit Jar", "Celestial Restoration",
                     "Undead Restoration", "Naga Restoration"),
    "Hellish Rejuvenation": ("Hellish Restoration",),
    "Limited Magic Immunity": ("Greater Magic Resistance",),
    "Shapechanger": ("Disguise Self", "Succubus Form", "Shape-Shift"),
    "Charge": ("Charging Horn", "Tusk", "Gore", "Ram"),
    "Charge (Boar or Hybrid Form Only)": ("Tusk",),
}
REVISED_ATTACK_DAMAGE = {"Brute"}
CANTRIP_NAME = "Cantrip"

def has_successor(definition, targets):
    text = json.dumps({
        key: definition.get(key, [])
        for key in ("trait", "action", "bonus", "reaction",
                    "legendary", "spellcasting")
    }, ensure_ascii=False).lower()
    return next((word for word in targets if word.lower() in text), None)

def main():
    ledger = load(LEDGER)
    stats = load(STATS)
    rows = {r["nativeId"]: r for r in load(
        REF / "native-coverage-manifest.json")["coverage"]}
    old_sources = {r["name"]: r for r in load(
        REF / "structured/bestiary/bestiary-srd51.json")["monster"]}
    source_by_slug = {
        __import__("re").sub(r"[^a-z0-9]+", "-", n.lower()).strip("-"): n
        for n in old_sources
    }
    documents = {}
    delta = Counter()
    for row in ledger["entries"]:
        identity = row["nativeId"]
        source_name = source_by_slug[identity.removeprefix("monster.")]
        old = old_sources[source_name]
        target = row.get("correspondingDefault", identity)
        p = ROOT / rows[target]["path"]
        if p not in documents:
            documents[p] = load(p)
        definition = documents[p]["definition"]
        for kind, features in row["featureDisposition"].items():
            for f in features:
                if f["status"] not in ("review-needed", "possible-redesign"):
                    continue
                name = f["name"]
                current_names = {
                    normalized_name(x["name"])
                    for field in ("trait", "action", "bonus", "reaction",
                                  "legendary", "spellcasting")
                    for x in definition.get(field, [])
                    if x.get("name")
                }
                if normalized_name(name) in current_names:
                    f.update(status="already-present", note="No duplicate trait added")
                    continue
                if kind == "trait" and name in RESTORE:
                    old_feature = next(
                        x for x in old.get("trait", []) if x["name"] == name
                    )
                    definition.setdefault("trait", []).append(copy.deepcopy(old_feature))
                    f.update(status="restored",
                             note="Independent older ability restored; preserve its original restrictions")
                    delta["traitsRestored"] += 1
                    continue
                if kind == "trait" and name in REVISED_ATTACK_DAMAGE:
                    # Both older Brute stat blocks already included the extra
                    # weapon die in their listed attack damage. Do not stack
                    # another die on the fully selected 5.5e attack statistics.
                    f.update(status="revised-attack-suite",
                             note="Old extra damage die was included in old attack damage; 5.5e attack damage is authoritative")
                    continue
                if kind == "trait" and name in SUCCESSOR:
                    found = has_successor(definition, SUCCESSOR[name])
                    if found:
                        f.update(status="newer-successor",
                                 note="Selected 5.5e equivalent/reworked capability: " + found)
                        continue
                if kind == "legendary" and name == CANTRIP_NAME:
                    # A cantrip legendary action uses existing newer access
                    # and the unchanged total legendary action budget.
                    available = json.dumps(definition.get("spellcasting", [])).lower()
                    if "mage hand" in available or "prestidigitation" in available:
                        old_feature = next(
                            x for x in old.get("legendary", []) if x["name"] == name)
                        definition.setdefault("legendary", []).append(copy.deepcopy(old_feature))
                        f.update(status="restored",
                                 note="Additional choice within current legendary action budget")
                        delta["legendaryRestored"] += 1
                        continue
                if kind == "spellcasting" and name.startswith("Innate Spellcasting"):
                    if source_name == "Drider":
                        definition.setdefault("spellcasting", []).append({
                            "name": "Dancing Lights",
                            "type": "spellcasting",
                            "headerEntries": [
                                "The drider casts {@spell Dancing Lights|SRD52} at will, requiring no Material components, using Wisdom as its spellcasting ability."
                            ],
                            "will": ["{@spell Dancing Lights|SRD52}"],
                            "ability": "wis", "displayAs": "action",
                        })
                        f.update(status="spell-access-extended",
                                 note="Restored missing at-will Dancing Lights without changing rechargeable 5.5e spellcasting")
                        delta["spellsRestored"] += 1
                    elif source_name == "Pit Fiend":
                        definition.setdefault("spellcasting", []).append({
                            "name": "Detect Magic",
                            "type": "spellcasting",
                            "headerEntries": [
                                "The pit fiend casts {@spell Detect Magic|SRD52} at will, requiring no Material components, using Charisma as its spellcasting ability."
                            ],
                            "will": ["{@spell Detect Magic|SRD52}"],
                            "ability": "cha", "displayAs": "action",
                        })
                        f.update(status="spell-access-extended",
                                 note="Restored missing at-will Detect Magic without modifying Hellfire Spellcasting")
                        delta["spellsRestored"] += 1
        row["remainingFeatureQuestions"] = sum(
            x["status"] in ("review-needed", "possible-redesign")
            for group in row["featureDisposition"].values() for x in group
        )
    for path, data in documents.items():
        write(path, data)
    counts = Counter()
    for row in ledger["entries"]:
        if row["remainingFeatureQuestions"]:
            counts["monstersWithOpenFeatureQuestions"] += 1
        for group in row["featureDisposition"].values():
            for f in group:
                counts["feature:" + f["status"]] += 1
    ledger["summary"] = dict(sorted(counts.items()))
    write(LEDGER, ledger)
    stats["monsterAdditionalOlderFeaturesRestored"] += (
        delta["traitsRestored"] + delta["legendaryRestored"])
    stats["monsterAdditionalSpellsRestored"] += delta["spellsRestored"]
    stats["monsterRemainingFeatureQuestions"] = sum(
        row["remainingFeatureQuestions"] for row in ledger["entries"]
    )
    stats["monsterReviewEntriesWithOpenQuestions"] = counts["monstersWithOpenFeatureQuestions"]
    stats["monsterFeatureDispositions"] = {
        k.removeprefix("feature:"): v for k, v in counts.items()
        if k.startswith("feature:")
    }
    write(STATS, stats)
    results = load(RESULTS)
    results["summary"] = dict(sorted(counts.items()))
    results["featuresRestoredThisPass"] = (
        delta["traitsRestored"] + delta["legendaryRestored"])
    results["totalAdditionalOlderFeaturesRestored"] = stats["monsterAdditionalOlderFeaturesRestored"]
    results["additionalSpellsRestored"] = stats["monsterAdditionalSpellsRestored"]
    results["unresolvedFeatureQuestions"] = stats["monsterRemainingFeatureQuestions"]
    results["reviewEntriesWithOpenQuestions"] = counts["monstersWithOpenFeatureQuestions"]
    results["finalPassChanges"] = dict(delta)
    write(RESULTS, results)
    print(json.dumps({
        "changes": dict(delta),
        "openFeatureQuestions": stats["monsterRemainingFeatureQuestions"],
        "creaturesWithOpenQuestions": counts["monstersWithOpenFeatureQuestions"]
    }, indent=2))

if __name__ == "__main__":
    main()
