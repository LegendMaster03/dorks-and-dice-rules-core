"""Regression tests for Fatigued distinct from native six-tier Exhaustion."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class FatiguedReconciliationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = load("data/native/conditions.json")["conditions"]
        cls.conditions = {x["id"]: x for x in cls.catalog}
        cls.chapter = load("data/native/rules/conditions.json")
        cls.history = load("data/reference/3x/fatigue-exhaustion-reconciliation.json")

    def test_new_fatigued_condition_while_exhaustion_remains_six_levels(self):
        self.assertEqual(28, len(self.catalog))
        self.assertEqual(28, len(self.conditions))
        self.assertEqual(set(self.conditions), set(self.chapter["conditionIds"]))
        fatigued = self.conditions["condition.fatigued"]
        exhaustion = self.conditions["condition.exhaustion"]
        self.assertEqual("condition", fatigued["kind"])
        self.assertEqual("leveled-condition", exhaustion["kind"])
        self.assertNotIn("appliesConditions", fatigued)
        self.assertNotIn("levels", fatigued)
        self.assertEqual(list(range(1, 7)), [x["level"] for x in exhaustion["levels"]])
        self.assertEqual(0, exhaustion["levelRules"]["endsAtLevel"])

    def test_fatigued_effective_strength_and_dexterity_penalties(self):
        effects = self.conditions["condition.fatigued"]["effects"]
        self.assertEqual(3, len(effects))
        abilities = {x["ability"]: x for x in effects
                     if x["type"] == "effective-ability-score"}
        self.assertEqual({"strength", "dexterity"}, set(abilities))
        for name in ("strength", "dexterity"):
            score = abilities[name]
            self.assertEqual(-2, score["value"])
            self.assertEqual("add", score["operation"])
            self.assertEqual(0, score["minimum"])
            self.assertTrue(score["affectsDerivedStatistics"])
        self.assertEqual(["run", "charge"], effects[0]["prohibited"])

    def test_eight_hour_recovery_does_not_inherit_exhaustion_rest_rule(self):
        fatigued = self.conditions["condition.fatigued"]
        recovery = fatigued["endsWhen"]
        self.assertEqual([{
            "event": "complete-rest", "durationHours": 8,
            "unless": {"imposingEffectOverridesRecovery": True}
        }], recovery)
        exhaustion = self.conditions["condition.exhaustion"]
        self.assertEqual(1, exhaustion["recovery"]["longRest"]["levelsRemoved"])
        self.assertEqual({"food-consumed", "drink-consumed"},
                         set(exhaustion["recovery"]["longRest"]["requirements"]))

    def test_3x_exhausted_is_not_5x_exhaustion_alias(self):
        relationships = self.history["sourceEquivalence"]
        self.assertEqual("condition.fatigued", relationships["fatigued"]["native"])
        old_exhausted = relationships["exhausted"]
        self.assertEqual(False, old_exhausted["equivalent"])
        self.assertEqual("condition.exhaustion", old_exhausted["nativeCandidate"])
        self.assertIn("-6 effective Strength", old_exhausted["edition3x"])
        self.assertIn("half Speed", old_exhausted["edition3x"])
        decisions = {d["id"]: d for d in self.history["decisions"]}
        self.assertEqual("deferred", decisions["3x.exhausted-conversion"]["state"])
        self.assertEqual("deferred", decisions["3x.fatigue-repeat"]["state"])

    def test_cross_system_evidence_kept_separate_from_native(self):
        native = self.conditions["condition.fatigued"]
        for k in ("edition", "source", "candidateDefinitions", "sourceAliases"):
            self.assertNotIn(k, native)
        self.assertEqual("fatigued-native-definition-adopted; repeated-fatigue-and-3x-exhausted-mapping-deferred",
                         self.history["status"])
        sources = self.history["sourceMaterials"]
        self.assertEqual({"D&D 3.0", "D&D 3.5", "D&D 5e"},
                         {x["edition"] for x in sources})
        self.assertEqual(16, len(load("data/reference/srd/reconciliation-decisions.json")["decisions"]))


if __name__ == "__main__":
    unittest.main()
