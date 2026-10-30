"""Approved shared rules are available as native player-facing references."""
import json
from pathlib import Path
import unittest

NATIVE = Path(__file__).resolve().parents[1] / "data/rulesets/first-party-001/native"

class SelectedSharedRulesTests(unittest.TestCase):
    def read(self, name):
        return json.loads((NATIVE/name).read_text(encoding="utf-8"))

    def test_emphasis_cancellation_and_extreme_outcome(self):
        rule=self.read("emphasis.json")
        self.assertEqual("rule.emphasis",rule["id"])
        self.assertIn("21",rule["playerFacingRule"])
        self.assertTrue(rule["criticalEscalationExempt"])
        self.assertEqual(["cancel-reroll",7,4,7],[e["outcome"] for e in rule["examples"]])

    def test_critical_stacking_is_explicitly_optional(self):
        rule=self.read("critical-stacking.json")
        self.assertIsNone(rule["defaultEnabled"])
        self.assertTrue(rule["noAutomaticUniversalActivation"])
        self.assertTrue(rule["escalationRolls"]["unmodified"])
        self.assertFalse(rule["escalationRolls"]["emphasis"])
        self.assertIn("0 HP",rule["criticalSuccess"]["triple"])

    def test_access_resources_and_recovery_do_not_expand_entitlements(self):
        spell=self.read("spell-access-policy.json")
        self.assertFalse(spell["dailySpellPreparation"]["required"])
        self.assertTrue(spell["openSpellAccess"]["optional"])
        self.assertTrue(spell["openSpellAccess"]["noAdditionalSlotsOrPoints"])
        rest=self.read("rest-recovery-policy.json")
        self.assertIn("Long Rest",rest["default"])

    def test_grapple_requires_touch_then_opposed_check(self):
        grapple=self.read("grapple-procedure.json")
        self.assertEqual([1,2,3,4],[s["order"] for s in grapple["steps"]])
        self.assertIn("Touch AC",grapple["steps"][0]["rule"])
        self.assertIn("opposed grapple check",grapple["steps"][1]["rule"])
        self.assertIn("Escape Artist",grapple["steps"][3]["rule"])

if __name__ == "__main__":
    unittest.main()
