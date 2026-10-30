"""Check the selected native condition catalog is usable for read-only Wiki display.

These tests validate content structure and recorded reconciliation decisions.
They do not imply that the Rules Core executes condition mechanics.
"""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class ConditionsWikiReadinessTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.document = read(NATIVE / "conditions.json")
        cls.items = cls.document["conditions"]
        cls.conditions = {entry["id"]: entry for entry in cls.items}
        cls.chapter = read(NATIVE / "rules/conditions.json")

    def test_complete_identity_and_readable_summary(self):
        self.assertGreater(len(self.items), 0)
        self.assertEqual(len(self.items), len(self.conditions))
        for entry in self.items:
            with self.subTest(condition=entry["id"]):
                self.assertTrue(entry["id"].startswith("condition."))
                self.assertIn(entry["kind"], {"condition", "leveled-condition"})
                self.assertTrue(entry["name"].strip())
                self.assertTrue(entry.get("description", "").strip())
                self.assertNotIn("candidateDefinitions", entry)
                self.assertNotIn("baselineStatus", entry)
                self.assertNotIn("sourceAliases", entry)
                self.assertTrue(entry.get("effects") or entry.get("levels"))

    def test_chapter_references_all_real_conditions_once(self):
        self.assertEqual("rule.conditions", self.chapter["id"])
        self.assertEqual("ruleChapter", self.chapter["kind"])
        self.assertEqual(set(self.conditions), set(self.chapter["conditionIds"]))
        self.assertEqual(len(self.conditions), len(self.chapter["conditionIds"]))
        self.assertNotIn("data/native/", json.dumps(self.chapter))
        self.assertIn("rule.defenses", " ".join(self.chapter["entries"]))
        self.assertEqual("rule.defenses", read(NATIVE / "defenses.json")["id"])

    def test_inherited_and_required_condition_links(self):
        for condition in self.items:
            for target in condition.get("appliesConditions", []):
                with self.subTest(source=condition["id"], target=target):
                    self.assertIn(target, self.conditions)
            for requirement in condition.get("requiresConditions", []):
                with self.subTest(source=condition["id"], target=requirement["condition"]):
                    self.assertIn(requirement["condition"], self.conditions)
        self.assertEqual(
            {"condition.incapacitated", "condition.prone", "condition.helpless"},
            set(self.conditions["condition.unconscious"]["appliesConditions"]),
        )
        self.assertEqual(
            {"condition.grappled"},
            set(self.conditions["condition.pinned"]["appliesConditions"]),
        )
        self.assertEqual(
            {"condition.helpless"},
            set(self.conditions["condition.held"]["appliesConditions"]),
        )

    def test_previous_approved_distinctions_survive(self):
        petrified = self.conditions["condition.petrified"]
        self.assertNotIn("condition.unconscious", petrified.get("appliesConditions", []))
        self.assertFalse(any(e.get("type") == "awareness" and
                             e.get("availability") == "none"
                             for e in petrified.get("effects", [])))

        stunned = self.conditions["condition.stunned"]
        self.assertIn(
            {"type": "speed", "operation": "multiply", "factor": 0.5},
            stunned["effects"],
        )
        self.assertNotIn("condition.helpless", stunned.get("appliesConditions", []))

        wet = self.conditions["condition.wet"]
        self.assertIn(
            {"event": "takes-damage", "damageType": "fire",
             "minimumDamageTaken": 5, "sameDamageInstance": True},
            wet["endsWhen"],
        )
        self.assertNotIn("condition.exhaustion",
                         self.conditions["condition.fatigued"].get("appliesConditions", []))
        self.assertEqual(
            list(range(1, 7)),
            [tier["level"] for tier in self.conditions["condition.exhaustion"]["levels"]],
        )


if __name__ == "__main__":
    unittest.main()
