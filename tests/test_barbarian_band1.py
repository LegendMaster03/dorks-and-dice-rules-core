"""Regression tests for the approved, source-neutral Band 1 Barbarian native rules."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "data/native/classes/base/barbarian.json"
FEATURES = ROOT / "data/native/features/classes/barbarian"
MANIFEST = ROOT / "data/reference/srd/native-coverage-manifest.json"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class BarbarianBand1Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.native = read(BASE)
        cls.rule = cls.native["definition"]
        cls.levels = cls.rule["progression"]
        cls.features = {}
        for path in FEATURES.glob("*.json"):
            item = read(path)
            cls.features[item["id"]] = item

    def test_one_native_base_class_and_exactly_twenty_levels(self):
        self.assertEqual("class.barbarian", self.native["id"])
        self.assertEqual("retained-native-definition", self.native["baselineStatus"])
        self.assertNotIn("candidateDefinitions", self.native)
        self.assertEqual(1, self.rule["band"])
        self.assertEqual([1, 20], self.rule["levelRange"])
        self.assertEqual(list(range(1, 21)),
                         [row["level"] for row in self.levels])

    def test_grants_reference_only_active_base_class_or_inline_features(self):
        inline = {f["id"] for f in self.rule["inlineFeatures"]}
        grants = [id_ for level in self.levels for id_ in level["featureGrants"]]
        self.assertEqual(len(grants), len(set(grants)), "Feature must not be granted twice")
        for feature_id in grants:
            if feature_id in inline:
                continue
            self.assertIn(feature_id, self.features)
            self.assertEqual("retained-native-definition",
                             self.features[feature_id]["baselineStatus"])
            self.assertNotIn("candidateDefinitions", self.features[feature_id])
        self.assertFalse(any("subclass" in x or "epic-boon" in x or
                             "primal-path" in x or "path-feature" in x
                             for x in grants))

    def test_separate_rage_modes_share_one_resource(self):
        rage = self.features["class-feature.barbarian.rage-level-1"]
        detail = rage["definition"]["mechanics"]
        self.assertEqual("rage-resource", detail["kind"])
        self.assertIn("battle", detail)
        self.assertIn("primal", detail)
        self.assertEqual("unlimited", self.levels[19]["rageUses"])
        self.assertEqual(6, self.levels[18]["rageUses"])
        self.assertTrue(detail["primal"]["oncePerEncounter"])
        self.assertEqual(15, detail["persistentUpgradeAt"])
        self.assertEqual(17, detail["primal"]["fatigueRemovedAtLevel"])
        self.assertEqual(["Battle Rage", "Greater Battle Rage", "Mighty Battle Rage"],
                         [x[1] for x in detail["battle"]["names"]])
        self.assertEqual(["Primal Rage", "Greater Primal Rage", "Mighty Primal Rage"],
                         [x[1] for x in detail["primal"]["names"]])

    def test_reconciled_advancement_and_prevent_zero_hp(self):
        hp = self.features["class-feature.barbarian.relentless-rage-level-11"]
        self.assertEqual("2 * barbarian-class-level",
                         hp["definition"]["mechanics"]["successHP"])
        self.assertEqual(10, hp["definition"]["mechanics"]["initialDC"])
        for level in (4, 8, 12, 16, 19):
            feat = self.features[
                f"class-feature.barbarian.ability-score-improvement-level-{level}"]
            self.assertIsNone(feat["definition"]["mechanics"]["abilityScoreMaximum"])
        self.assertEqual(
            ["class-feature.barbarian.ability-score-improvement-level-19"],
            self.levels[18]["featureGrants"])
        self.assertIsNone(self.rule["permanentAbilityScoreMaximum"])

    def test_historical_class_depth_and_exclusions(self):
        self.assertEqual(20, self.levels[19]["baseAttackBonus"])
        self.assertEqual([20, 15, 10, 5], self.levels[19]["iterativeBaseAttackBonuses"])
        self.assertEqual({"fortitude": 12, "reflex": 6, "will": 6},
                         self.levels[19]["baseSaves"])
        self.assertEqual(5, self.levels[18]["damageReduction"])
        self.assertEqual(6, self.levels[17]["trapSense"])
        self.assertFalse(self.rule["illiteracyRestriction"])
        self.assertIsNone(self.rule["alignmentRestriction"])
        self.assertEqual(21, self.rule["epicBoonTotalCharacterLevelMinimum"])

    def test_source_only_and_subclass_records_remain_inert(self):
        granted = {id_ for row in self.levels for id_ in row["featureGrants"]}
        for record in self.features.values():
            if record["id"] not in granted:
                self.assertEqual("pending-native-reconciliation",
                                 record["baselineStatus"])
                self.assertIn("candidateDefinitions", record)
        manifest = read(MANIFEST)
        by_path = {row["path"]: row for row in manifest["coverage"]
                   if "path" in row}
        for path in [BASE, *FEATURES.glob("*.json")]:
            rel = path.relative_to(ROOT).as_posix()
            row = by_path[rel]
            native = read(path)
            if native["baselineStatus"] == "retained-native-definition":
                self.assertIn(row["status"],
                              {"native-compatible-merged", "native-single-source-retained"})
            else:
                self.assertIn(row["status"], {"single-candidate", "multiple-candidates"})


if __name__ == "__main__":
    unittest.main()
