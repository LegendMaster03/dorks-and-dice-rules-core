"""Regression tests for source-specific spells and restored monster utility."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]

def get(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))

def monster(name):
    return get("data/native/bestiary/" + name + ".json")["definition"]

class MonsterSecondPassTests(unittest.TestCase):
    def test_dragon_unique_actions_restored_without_old_multiattack(self):
        dragon = monster("adult-bronze-dragon")
        names = [a["name"] for a in dragon["action"]]
        self.assertIn("Change Shape", names)
        self.assertIn("Frightful Presence", names)
        self.assertIn("Rend", names)
        self.assertNotIn("Claw", names)
        self.assertNotIn("Tail", names)

    def test_spell_access_added_only_when_missing(self):
        giant = monster("cloud-giant")
        primary = next(s for s in giant["spellcasting"]
                       if s["name"] == "Spellcasting")
        spells = json.dumps(primary).lower()
        self.assertIn("feather fall", spells)
        self.assertIn("fly", spells)
        # Existing spells are never duplicated.
        drider = monster("drider")
        joined = json.dumps(drider["spellcasting"]).lower()
        self.assertEqual(1, joined.count('spell darkness|srd52'))
        self.assertEqual(1, joined.count('spell faerie fire|srd52'))

    def test_non_dragon_fear_does_not_duplicate_revised_bellow(self):
        tarrasque = monster("tarrasque")
        self.assertFalse(any(a["name"] == "Frightful Presence"
                             for a in tarrasque["action"]))

    def test_retained_5_5_only_creatures_still_unchanged(self):
        for name in ("pirate", "pirate-captain"):
            item = get("data/native/bestiary/" + name + ".json")
            self.assertEqual("retained-native-definition", item["baselineStatus"])
            self.assertNotIn("candidateDefinitions", item)

    def test_review_counts_reconcile(self):
        ledger = get("data/reference/srd/monster-legacy-variant-review.json")
        results = get("docs/rewrite/monster-legacy-feature-results.json")
        statistics = get("docs/rewrite/native-reconciliation-statistics.json")
        count = sum(
            item["remainingFeatureQuestions"] for item in ledger["entries"])
        self.assertEqual(count, results["unresolvedFeatureQuestions"])
        self.assertEqual(count, statistics["monsterRemainingFeatureQuestions"])
        self.assertEqual(
            statistics["monsterAdditionalSpellsRestored"],
            results["additionalSpellsRestored"])
        self.assertEqual(249, len(ledger["entries"]))

if __name__ == "__main__":
    unittest.main()
