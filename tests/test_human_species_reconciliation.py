"""Regression checks for approved first-pass Human Species content."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native/species/base/human.json"
WORKING = ROOT / "data/rulesets/first-party-001/working/species/base/human.json"
MANIFEST = ROOT / "data/reference/srd/native-coverage-manifest.json"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class HumanSpeciesReconciliationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.human = read(NATIVE)
        cls.traits = {trait["id"]: trait for trait in cls.human["traits"]}

    def test_authoritative_identity_and_characteristics(self):
        human = self.human
        self.assertEqual("species.human", human["id"])
        self.assertEqual("species", human["kind"])
        self.assertEqual("selected-native", human["status"])
        self.assertTrue(human["selectable"])
        self.assertEqual("humanoid", human["creatureType"])
        self.assertEqual(30, human["walkingSpeedFeet"])
        self.assertEqual(["small", "medium"], human["sizeChoice"]["from"])
        self.assertNotIn("defaultUpbringing", human)
        self.assertNotIn("parent", human)
        self.assertNotIn("candidateDefinitions", human)
        self.assertFalse(WORKING.exists())

    def test_all_three_customization_benefits_are_cumulative(self):
        self.assertEqual({"skillful", "versatile", "adaptable-learning"},
                         set(self.traits))
        skill = self.traits["skillful"]
        feat = self.traits["versatile"]
        self.assertEqual("skill-proficiency", skill["grantType"])
        self.assertEqual("any-native-skill", skill["chooseFrom"])
        self.assertEqual(1, skill["choiceCount"])
        self.assertEqual("feat-choice", feat["grantType"])
        self.assertEqual("origin", feat["featCategory"])
        self.assertEqual(1, feat["choiceCount"])

    def test_bonus_ranks_preserve_normal_rank_limits(self):
        ranks = self.traits["adaptable-learning"]
        self.assertEqual(4, ranks["additionalRanksAtCharacterLevel1"])
        self.assertEqual(1, ranks["additionalRanksPerSubsequentCharacterLevel"])
        self.assertEqual("player-choice", ranks["allocation"])
        self.assertTrue(ranks["obeysNormalSkillRankLimits"])

    def test_inspiration_is_not_prematurely_granted(self):
        self.assertNotIn("resourceful", self.traits)
        self.assertNotIn("heroic-inspiration", self.traits)
        self.assertTrue(any("Heroic Inspiration" in note
                            for note in self.human["reviewNotes"]))

    def test_source_coverage_is_promoted_without_dropping_evidence(self):
        manifest = read(MANIFEST)
        rows = [r for r in manifest["coverage"]
                if r["nativeId"] == "species.human"]
        self.assertEqual(1, len(rows))
        self.assertEqual("approved-native-reconciled", rows[0]["status"])
        self.assertEqual(
            "data/rulesets/first-party-001/native/species/base/human.json",
            rows[0]["path"])
        self.assertEqual(2, rows[0]["sourceRecords"])
        self.assertEqual(2, len(rows[0]["candidates"]))


if __name__ == "__main__":
    unittest.main()
