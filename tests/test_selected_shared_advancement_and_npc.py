"""Previously established shared policies remain visible in native data."""
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[1]/"data/rulesets/first-party-001/native"

class SelectedAdvancementAndCreaturePolicies(unittest.TestCase):
    def load(self, name):
        return json.loads((ROOT/name).read_text(encoding="utf-8"))

    def test_level_bands_and_eligibility(self):
        d=self.load("level-bands.json")
        self.assertEqual(20,d["bands"]["size"])
        self.assertEqual([21,40],d["bands"]["examples"][1]["levels"])
        self.assertIsNone(d["abilityScores"]["universalPermanentMaximum"])
        self.assertEqual("total-character-level >= 21",d["epicBoons"]["eligibility"])
        self.assertTrue(d["epicBoons"]["noAutomaticAward"])

    def test_npc_casting_pool_never_inferred(self):
        d=self.load("npc-monster-casting-resources.json")
        self.assertTrue(d["resourceProfiles"]["noCRDerivedSlots"])
        self.assertEqual({"1":4,"2":3,"3":2},d["example"]["sourceSupportedSlotsByLevel"])

    def test_companion_exclusive_path_and_explicit_exception(self):
        d=self.load("exclusive-companion-paths.json")
        self.assertFalse(d["druid"]["bothAutomatically"])
        self.assertIn("Find Familiar",d["independentGrants"])

if __name__=="__main__":
    unittest.main()
