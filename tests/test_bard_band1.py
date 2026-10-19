"""Band 1 Bard: unified sources, nonmusical expressions and inactive subclasses."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "data/native/classes/base/bard.json"
FEATURE_DIR = ROOT / "data/native/features/classes/bard"
MANIFEST = ROOT / "data/reference/srd/native-coverage-manifest.json"


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


class BardBand1Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.native = load(BASE)
        cls.definition = cls.native["definition"]
        cls.progression = cls.definition["progression"]
        cls.features = {x["id"]: x for x in (load(p) for p in FEATURE_DIR.glob("*.json"))}

    def test_one_class_twenty_levels_and_no_subclass_grants(self):
        self.assertEqual("class.bard", self.native["id"])
        self.assertEqual("retained-native-definition", self.native["baselineStatus"])
        self.assertNotIn("candidateDefinitions", self.native)
        self.assertEqual(1, self.definition["band"])
        self.assertEqual([1, 20], self.definition["levelRange"])
        self.assertEqual(list(range(1, 21)), [r["level"] for r in self.progression])
        inline = {f["id"] for f in self.definition["inlineFeatures"]}
        grants = [feature for r in self.progression for feature in r["featureGrants"]]
        self.assertEqual(len(grants), len(set(grants)), "No duplicated base grants")
        for feature in grants:
            if feature in inline:
                continue
            self.assertIn(feature, self.features)
            self.assertEqual("retained-native-definition", self.features[feature]["baselineStatus"])
            self.assertNotIn("candidateDefinitions", self.features[feature])
        self.assertFalse(any("bard-college" in x or "subclass" in x or "epic-boon" in x for x in grants))

    def test_nonmusical_bards_have_base_class_access(self):
        self.assertTrue(self.definition["expression"]["musicNotRequired"])
        self.assertTrue(self.definition["expression"]["audienceNotAutomaticallyRequired"])
        self.assertFalse(self.definition["artisticTrainingChoices"]["musicalInstrumentRequired"])
        self.assertEqual(3, self.definition["artisticTrainingChoices"]["count"])
        self.assertIn("cooking", next(x for x in self.definition["inlineFeatures"]
                                     if x["id"] == "bard.bardic-expression")["mechanics"]["mediaExamples"])
        self.assertIsNone(self.definition["alignmentRestriction"])
        self.assertIsNone(self.definition["permanentAbilityScoreMaximum"])

    def test_approved_inspiration_resource_and_scaling(self):
        inspiration = self.features["class-feature.bard.bardic-inspiration-level-1"]["definition"]["mechanics"]
        self.assertEqual(60, inspiration["durationMinutes"])
        self.assertEqual("recipient-fails-a-d20-test", inspiration["triggerToRoll"])
        self.assertEqual([[1, "d6"], [5, "d8"], [10, "d10"], [15, "d12"]], inspiration["dice"])
        self.assertTrue(inspiration["notPerformanceResource"])
        performance = next(x for x in self.definition["inlineFeatures"]
                           if x["id"] == "bard.bardic-performances")["mechanics"]
        self.assertEqual("bard-class-level-per-day", performance["uses"])
        self.assertTrue(performance["independentFromBardicInspiration"])

    def test_approved_spell_progression(self):
        known = [r["leveledSpellsKnown"] for r in self.progression]
        cantrips = [r["cantripsKnown"] for r in self.progression]
        self.assertEqual([4,5,6,7,9,10,11,12,14,15,16,16,17,18,19,19,20,22,22,22], known)
        self.assertEqual([4,5]+[6]*18, cantrips)
        casting = self.features["class-feature.bard.spellcasting-level-1"]["definition"]["mechanics"]
        self.assertEqual("known-not-prepared", casting["spellAccess"])
        self.assertTrue(casting["cantripsAtWill"])
        self.assertEqual(["spell-slots","spell-points"], casting["resourceChoices"])
        self.assertEqual(9, casting["maxSpellLevel"])
        self.assertEqual(9, len(self.progression[19]["spellSlotsBySpellLevel"]))
        secrets = self.features["class-feature.bard.magical-secrets-level-10"]["definition"]["mechanics"]
        self.assertEqual([10,14,18], secrets["anyClassSpellsAtBardLevels"])
        self.assertTrue(secrets["countsWithinKnownSpellsCapacity"])
        words = self.features["class-feature.bard.words-of-creation-level-20"]["definition"]["mechanics"]
        self.assertFalse(words["countAgainstKnownSpells"])
        self.assertTrue(words["noPreparedSpells"])

    def test_historical_courage_and_saving_throws(self):
        self.assertEqual(1, self.progression[0]["inspireCourageAttackDamageBonus"])
        self.assertEqual(2, self.progression[0]["inspireCourageCharmFearSaveBonus"])
        self.assertEqual(2, self.progression[7]["inspireCourageAttackDamageBonus"])
        self.assertEqual(2, self.progression[7]["inspireCourageCharmFearSaveBonus"])
        self.assertEqual(3, self.progression[13]["inspireCourageCharmFearSaveBonus"])
        self.assertEqual(4, self.progression[19]["inspireCourageCharmFearSaveBonus"])
        self.assertEqual(15, self.progression[19]["baseAttackBonus"])
        self.assertEqual([15, 10, 5], self.progression[19]["iterativeBaseAttackBonuses"])
        self.assertEqual({"fortitude":6,"reflex":12,"will":12}, self.progression[19]["baseSaves"])

    def test_expertise_countercharm_advance_and_epic_not_granted(self):
        self.assertIn("class-feature.bard.expertise-level-2", self.progression[1]["featureGrants"])
        self.assertIn("class-feature.bard.expertise-level-9", self.progression[8]["featureGrants"])
        self.assertIn("class-feature.bard.countercharm-level-6", self.progression[5]["featureGrants"])
        self.assertIn("class-feature.bard.countercharm-level-7", self.progression[6]["featureGrants"])
        self.assertIn("class-feature.bard.superior-inspiration-level-18", self.progression[17]["featureGrants"])
        self.assertEqual(["class-feature.bard.ability-score-improvement-level-19"],
                         self.progression[18]["featureGrants"])
        self.assertEqual(["class-feature.bard.words-of-creation-level-20"],
                         self.progression[19]["featureGrants"])
        self.assertEqual(21, self.definition["epicBoonMinimumTotalCharacterLevel"])

    def test_inactive_source_records_and_manifest_integrity(self):
        grant = {id_ for row in self.progression for id_ in row["featureGrants"]}
        inert = [v for k, v in self.features.items() if k not in grant]
        self.assertEqual(10, len(inert))
        for item in inert:
            self.assertEqual("pending-native-reconciliation", item["baselineStatus"])
            self.assertIn("candidateDefinitions", item)
        by_path = {x["path"]: x for x in load(MANIFEST)["coverage"] if "path" in x}
        for path in [BASE, *FEATURE_DIR.glob("*.json")]:
            obj = load(path)
            coverage = by_path[path.relative_to(ROOT).as_posix()]
            if obj["baselineStatus"] == "retained-native-definition":
                self.assertIn(coverage["status"], {"native-compatible-merged",
                                                  "native-single-source-retained"})
            else:
                self.assertIn(coverage["status"], {"single-candidate","multiple-candidates"})


if __name__ == "__main__":
    unittest.main()
