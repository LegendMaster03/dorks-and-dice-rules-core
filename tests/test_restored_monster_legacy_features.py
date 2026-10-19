"""Verify selective older-monster ability retention under the 5.5e roster."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]

def read(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))

class LegacyMonsterAbilityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.ledger = read("data/reference/srd/monster-legacy-variant-review.json")
        cls.report = read("docs/rewrite/native-reconciliation-statistics.json")
        cls.legacy_summary = read("docs/rewrite/monster-legacy-feature-results.json")

    def test_every_old_feature_has_one_review_disposition(self):
        self.assertEqual(249, len(self.ledger["entries"]))
        counted = 0
        for entry in self.ledger["entries"]:
            statuses = entry["featureDisposition"]
            for kind, names in entry["olderAbilitiesNotIncludedAutomatically"].items():
                annotated = statuses[kind]
                self.assertEqual(names, [row["name"] for row in annotated])
                self.assertTrue(all(row["status"] in {
                    "restored", "already-present", "newer-successor",
                    "revised-attack-suite", "review-needed", "possible-redesign",
                    "spell-access-extended"
                } for row in annotated))
                counted += len(annotated)
            self.assertEqual(
                sum(row["status"] in ("review-needed", "possible-redesign")
                    for options in statuses.values() for row in options),
                entry["remainingFeatureQuestions"])
        self.assertEqual(
            counted, sum(self.report["monsterFeatureDispositions"].values()))
        self.assertEqual(
            self.legacy_summary["unresolvedFeatureQuestions"],
            self.report["monsterRemainingFeatureQuestions"])

    def test_old_hobgoblin_and_bugbear_not_separate_creatures(self):
        hobgoblin = read("data/native/bestiary/hobgoblin-warrior.json")
        bugbear = read("data/native/bestiary/bugbear-warrior.json")
        self.assertIn("Martial Advantage",
                      [x["name"] for x in hobgoblin["definition"]["trait"]])
        self.assertIn("Pack Tactics",
                      [x["name"] for x in hobgoblin["definition"]["trait"]])
        self.assertIn("Surprise Attack",
                      [x["name"] for x in bugbear["definition"]["trait"]])
        self.assertNotIn("Brute",
                         [x["name"] for x in bugbear["definition"]["trait"]])
        self.assertEqual(
            "monster.hobgoblin-warrior",
            read("data/native/bestiary/hobgoblin.json")["aliasOf"])
        self.assertEqual(
            "monster.bugbear-warrior",
            read("data/native/bestiary/bugbear.json")["aliasOf"])

    def test_transferred_abilities_do_not_duplicate_newer_mechanics(self):
        aboleth = read("data/native/bestiary/aboleth.json")["definition"]
        self.assertEqual(1, sum(x["name"] == "Mucus Cloud"
                                for x in aboleth["trait"]))
        self.assertEqual(0, sum(x["name"] == "Mucous Cloud"
                                for x in aboleth["trait"]))
        basilisk = read("data/native/bestiary/basilisk.json")["definition"]
        self.assertEqual([], basilisk.get("trait", []))
        self.assertTrue(any(x["name"].startswith("Petrifying Gaze")
                            for x in basilisk["bonus"]))
        cloaker = read("data/native/bestiary/cloaker.json")["definition"]
        self.assertFalse(any(x["name"] == "Damage Transfer"
                             for x in cloaker.get("trait", [])))
        self.assertIn("halves the damage",
                      json.dumps(cloaker["action"]))

    def test_mephit_spell_access_reconciled_without_duplicates(self):
        magma = read("data/native/bestiary/magma-mephit.json")["definition"]
        self.assertEqual(
            ["Innate Spellcasting (1/Day)"],
            [s["name"] for s in magma.get("spellcasting", [])])
        steam = read("data/native/bestiary/steam-mephit.json")["definition"]
        self.assertFalse(steam.get("spellcasting"))
        self.assertIn(
            "Blurred Form", [t["name"] for t in steam.get("trait", [])])

    def test_5_5_only_monsters_are_not_reconciled_against_missing_sources(self):
        pirate = read("data/native/bestiary/pirate.json")
        captain = read("data/native/bestiary/pirate-captain.json")
        for record in (pirate, captain):
            self.assertEqual("retained-native-definition",
                             record["baselineStatus"])
            self.assertNotIn("candidateDefinitions", record)
        self.assertEqual("1", pirate["definition"]["cr"])
        self.assertEqual("6", captain["definition"]["cr"])

    def test_no_legacy_attack_suite_duplicate(self):
        dragon = read("data/native/bestiary/adult-black-dragon.json")["definition"]
        action_names = [x["name"] for x in dragon["action"]]
        self.assertIn("Rend", action_names)
        self.assertNotIn("Claw", action_names)
        self.assertNotIn("Tail", action_names)
        self.assertIn("Wing Attack (Costs 2 Actions)",
                      [x["name"] for x in dragon["legendary"]])

if __name__ == "__main__":
    unittest.main()
