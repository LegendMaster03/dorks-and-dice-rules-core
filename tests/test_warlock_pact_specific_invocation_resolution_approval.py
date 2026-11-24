"""Owner-approved native design must not be confused with executable Invocation implementation."""
from pathlib import Path
import json
import unittest

ROOT=Path(__file__).resolve().parents[1]
def read(path):
    return json.loads((ROOT/path).read_text(encoding="utf-8"))

class InvocationResolutionApprovalTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog=read("data/reference/warlock-pact-specific-invocations.json")
        cls.base=read("data/rulesets/first-party-001/working/classes/base/warlock.json")
    def test_critical_decisions_have_distinct_effect_ownership(self):
        x={x["name"]:x["ownerApprovedDisposition"]["key"] for x in self.catalog["entries"]}
        self.assertIn("retire-native-selectable",x["Thirsting Blade"])
        self.assertIn("three-attacks-total",x["Devouring Blade"])
        self.assertIn("alternate-bonus-action-command",x["Investment of the Chain Master"])
        self.assertIn("no-free-duplicate",x["Book of Ancient Secrets"])
        self.assertIn("separate-proficiency",x["Protection of the Talisman"])
        self.assertIn("one-shared-directional",x["Bond of the Talisman"])
        self.assertEqual("pending-native-reconciliation",self.base["baselineStatus"])
if __name__=="__main__":
    unittest.main()
