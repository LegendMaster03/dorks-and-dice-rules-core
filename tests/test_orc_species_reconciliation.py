"""Approved Orc native Species and shared vision regression checks."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"
ORC = NATIVE / "species/base/orc.json"
WORKING_ORC = ROOT / "data/rulesets/first-party-001/working/species/base/orc.json"
MANIFEST = ROOT / "data/reference/srd/native-coverage-manifest.json"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class OrcSpeciesReconciliationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.orc = read(ORC)
        cls.traits = {t["id"]: t for t in cls.orc["traits"]}

    def test_orc_is_native_directly_selectable_and_no_forced_culture(self):
        o = self.orc
        self.assertEqual("species.orc", o["id"])
        self.assertEqual("species", o["kind"])
        self.assertEqual("selected-native", o["status"])
        self.assertTrue(o["selectable"])
        self.assertEqual("humanoid", o["creatureType"])
        self.assertEqual("medium", o["size"])
        self.assertEqual(30, o["walkingSpeedFeet"])
        self.assertNotIn("defaultUpbringing", o)
        self.assertNotIn("parent", o)
        self.assertFalse(WORKING_ORC.exists())

    def test_low_light_vision_without_darkvision_or_sunlight_penalty(self):
        rule = read(NATIVE / "rules/low-light-vision.json")
        self.assertEqual("rule.low-light-vision", self.traits["low-light-vision"]["rule"])
        self.assertEqual("dim", rule["effects"][0]["actualLightLevel"])
        self.assertEqual("bright", rule["effects"][0]["perceivedLightLevel"])
        self.assertNotIn("darkvisionFeet", self.orc)
        self.assertNotIn("sunlight-sensitivity", self.traits)
        self.assertNotIn("dazzled", self.traits)

    def test_adrenaline_rush_scales_through_proficiency_bonus(self):
        t = self.traits["adrenaline-rush"]
        self.assertEqual({"action": "bonus-action", "grantsAction": "dash"}, t["activation"])
        self.assertEqual("proficiency-bonus", t["temporaryHitPoints"]["value"])
        self.assertEqual({"value": "proficiency-bonus",
                          "recharge": "short-or-long-rest"}, t["uses"])
        self.assertNotIn("aggressive", self.traits)

    def test_survival_and_physical_traits(self):
        endure = self.traits["relentless-endurance"]
        self.assertEqual(1, endure["hitPointsInstead"])
        self.assertEqual({"value": 1, "recharge": "long-rest"}, endure["uses"])
        powerful = self.traits["powerful-build"]
        self.assertEqual(1, powerful["effectiveSizeCategoriesHigherForCarryingCapacity"])
        self.assertEqual(1, powerful["effectiveSizeCategoriesHigherForPushingDraggingLifting"])
        self.assertIn("advantage-on-checks-to-end-grappled", powerful["doesNotGrant"])
        savage = self.traits["savage-attacks"]
        self.assertEqual("critical-hit-with-melee-weapon-attack", savage["trigger"])
        self.assertEqual(1, savage["bonusDamageDice"]["count"])
        menacing = self.traits["menacing-presence"]
        self.assertEqual("skill.intimidation", menacing["skillCheckAdvantage"]["skill"])
        self.assertTrue(menacing["skillCheckAdvantage"]["requiresTargetCanPerceivePresence"])
        self.assertFalse(menacing["grantsSkillProficiency"])

    def test_source_coverage_promoted(self):
        matches = [e for e in read(MANIFEST)["coverage"]
                   if e["nativeId"] == "species.orc"]
        self.assertEqual(1, len(matches))
        self.assertEqual("approved-native-reconciled", matches[0]["status"])
        self.assertEqual("data/rulesets/first-party-001/native/species/base/orc.json",
                         matches[0]["path"])
        self.assertEqual(1, matches[0]["sourceRecords"])


if __name__ == "__main__":
    unittest.main()
