"""Tests for the selected native Fear rule, independent of draft Skill catalogs."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data" / "rulesets" / "first-party-001" / "native"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class NativeFearTests(unittest.TestCase):
    def test_fear_definition_has_valid_condition_references(self):
        fear = read(NATIVE / "fear.json")
        self.assertEqual("rule.fear-family", fear["id"])
        self.assertEqual("rule", fear["kind"])
        self.assertNotIn("status", fear)
        conditions = {entry["id"] for entry in read(NATIVE / "conditions.json")["conditions"]}
        self.assertTrue(set(fear["conditions"]).issubset(conditions))

    def test_unresolved_escalation_is_inert(self):
        fear = read(NATIVE / "fear.json")
        self.assertEqual("deferred", fear["escalation"]["status"])
        self.assertTrue(fear["escalation"]["notAdoptedAutomatically"])


if __name__ == "__main__":
    unittest.main()
