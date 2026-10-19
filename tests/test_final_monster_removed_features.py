"""Safeguards for the final common monster ability and spell restoration."""
import json
from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[1]

def item(name):
    return json.loads((ROOT / ("data/native/bestiary/" + name + ".json")).read_text())

class FinalMonsterRemovedFeaturesTests(unittest.TestCase):
    def test_magic_attacks_and_vampire_regeneration_preserved(self):
        pit = item("pit-fiend")["definition"]
        self.assertIn("Magic Weapons", [x["name"] for x in pit["trait"]])
        for name in ("vampire", "vampire-spawn"):
            m = item(name)["definition"]
            self.assertIn("Regeneration", [x["name"] for x in m["trait"]])

    def test_older_sneak_attacks_are_distinct_from_poison(self):
        for name in ("assassin", "spy"):
            m = item(name)["definition"]
            self.assertIn("Sneak Attack (1/Turn)", [x["name"] for x in m["trait"]])

    def test_missing_nonstandard_spellcasting_restored(self):
        drider = item("drider")["definition"]
        self.assertIn("Dancing Lights", [s["name"] for s in drider["spellcasting"]])
        pit = item("pit-fiend")["definition"]
        self.assertIn("Detect Magic", [s["name"] for s in pit["spellcasting"]])
        self.assertTrue(any("Hellfire Spellcasting" in s["name"]
                            for s in pit["spellcasting"]))

    def test_remaining_decisions_are_explicit_not_extra_monsters(self):
        ledger = json.loads((ROOT / "data/reference/srd/monster-legacy-variant-review.json").read_text())
        remaining = {
            entry["nativeId"] for entry in ledger["entries"]
            if entry["remainingFeatureQuestions"]
        }
        self.assertEqual({
            "monster.assassin", "monster.lion", "monster.panther",
            "monster.priest", "monster.saber-toothed-tiger",
        }, remaining)

    def test_weretiger_pounce_preserves_old_trigger_with_new_attacks(self):
        weretiger = item("weretiger")["definition"]
        pounce = [x for x in weretiger["trait"]
                  if x["name"] == "Pounce (Tiger or Hybrid Form Only)"]
        self.assertEqual(1, len(pounce))
        rule = pounce[0]["entries"][0]
        self.assertIn("15 feet", rule)
        self.assertIn("Scratch attack", rule)
        self.assertIn("Bite (Tiger or Hybrid Form Only) attack", rule)
        self.assertIn("{@dc 14}", rule)
        self.assertIn("bonus action", rule)
        self.assertNotIn("claw attack", rule)
        self.assertEqual(2, len([x for x in weretiger["action"]
                                 if x["name"] in
                                 ("Scratch", "Bite (Tiger or Hybrid Form Only)")]))

    def test_reconciled_counts_are_consistent(self):
        ledger = json.loads((ROOT / "data/reference/srd/monster-legacy-variant-review.json").read_text())
        stats = json.loads((ROOT / "docs/rewrite/native-reconciliation-statistics.json").read_text())
        self.assertEqual(
            sum(x["remainingFeatureQuestions"] for x in ledger["entries"]),
            stats["monsterRemainingFeatureQuestions"])

if __name__ == "__main__":
    unittest.main()
