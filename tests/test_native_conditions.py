"""Selected native Exhaustion contract (SRD 5.1 six-level tiered rule)."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class ExhaustionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.native_data = load("data/native/conditions.json")
        cls.native = next(
            x for x in cls.native_data["conditions"] if x["id"] == "condition.exhaustion"
        )
        cls.reference_data = load("data/reference/srd/structured/conditionsdiseases.json")

    def test_one_native_exhaustion_condition_no_parallel_edition_variants(self):
        all_ids = [x["id"] for x in self.native_data["conditions"]]
        self.assertEqual(len(all_ids), len(set(all_ids)), "Native condition IDs must be unique")
        self.assertEqual(1, all_ids.count("condition.exhaustion"))
        self.assertFalse(any("exhaustion-" in x or ".exhaustion." in x for x in all_ids))
        self.assertEqual("leveled-condition", self.native["kind"])
        self.assertEqual("Exhaustion", self.native["name"])
        for key in ("source", "edition", "srd", "srd52", "historical", "sourceAliases"):
            self.assertNotIn(key, self.native)

    def test_six_cumulative_levels(self):
        spec = self.native["levelRules"]
        self.assertEqual(0, spec["minimum"])
        self.assertEqual(6, spec["maximum"])
        self.assertEqual(0, spec["endsAtLevel"])
        self.assertIn("every lower level", spec["stacking"])
        self.assertEqual(list(range(1, 7)), [x["level"] for x in self.native["levels"]])

    def test_approved_tier_effects(self):
        effects = {v["level"]: v["effects"] for v in self.native["levels"]}
        self.assertEqual([{"type": "roll-mode", "appliesTo": "ability-check", "mode": "disadvantage"}], effects[1])
        self.assertEqual([{"type": "speed", "operation": "multiply", "factor": 0.5}], effects[2])
        self.assertEqual([
            {"type": "roll-mode", "appliesTo": "attack-roll", "mode": "disadvantage"},
            {"type": "roll-mode", "appliesTo": "saving-throw", "mode": "disadvantage"},
        ], effects[3])
        self.assertEqual([{"type": "maximum-hit-points", "operation": "multiply", "factor": 0.5}], effects[4])
        self.assertEqual([{"type": "speed", "operation": "set", "value": 0}], effects[5])
        self.assertEqual([{"type": "death"}], effects[6])

    def test_recovery_and_ending(self):
        recovery = self.native["recovery"]
        self.assertEqual(1, recovery["longRest"]["levelsRemoved"])
        self.assertEqual({"food-consumed", "drink-consumed"}, set(recovery["longRest"]["requirements"]))
        self.assertEqual(1, recovery["raisedFromDead"]["levelsRemoved"])
        self.assertIn("amount specified", recovery["general"])

    def test_source_selection_recorded_outside_native(self):
        ref = [x for x in self.reference_data["condition"] if x.get("name") == "Exhaustion"]
        self.assertEqual({"SRD51", "SRD52"}, {x["source"] for x in ref})
        chosen = load("data/reference/srd/reconciliation-decisions.json")["decisions"]
        match = [x for x in chosen if x["nativeRuleId"] == "condition.exhaustion"]
        self.assertEqual(1, len(match))
        self.assertEqual("SRD 5.1 exhaustion", match[0]["adoptedVariant"])


if __name__ == "__main__":
    unittest.main()
