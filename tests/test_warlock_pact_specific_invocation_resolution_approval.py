"""Owner-approved native design must not be confused with executable Invocation implementation."""
from pathlib import Path
from collections import Counter
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
    def test_all_sixteen_dispositions_authorized_without_activation(self):
        approval=self.catalog["ownerApprovedConflictResolution"]
        self.assertEqual("owner-approved-native-design-not-implemented",approval["status"])
        self.assertEqual(16,approval["approvedOptionsCount"])
        self.assertEqual(15,approval["selectableCandidateCountAfterRetiringThirsting"])
        self.assertTrue(approval["historicalThirstingOnly"])
        self.assertTrue(approval["chainBaselineCommandRuleApproved"])
        self.assertTrue(approval["preserve2014And2024Variants"])
        self.assertTrue(approval["nonSrdRightsReviewAndNativeConversionStillRequired"])
        self.assertFalse(approval["invocationSelectionProgressionApproved"])
        self.assertFalse(approval["threeFiveInvocationCatalogImportApproved"])
        self.assertTrue(approval["noPlayableRuntimeImplementationYet"])
        self.assertTrue(approval["noMainMergeAuthorized"])
        self.assertEqual(16,len(self.catalog["entries"]))
        self.assertEqual(16,len({x["name"] for x in self.catalog["entries"]}))
        self.assertEqual({"Blade":5,"Chain":4,"Tome":4,"Talisman":3},
            dict(Counter(x["requiredPactType"] for x in self.catalog["entries"])))
        for x in self.catalog["entries"]:
            with self.subTest(name=x["name"]):
                d=x["ownerApprovedDisposition"]
                self.assertEqual("approved-mechanics-pending-native-conversion",d["status"])
                self.assertFalse(d["runtimeEnabled"])
                self.assertTrue(d["doesNotChangeSourceVariants"])
                self.assertTrue(d["key"])
                self.assertFalse(x["nativeExecutable"])
    def test_critical_decisions_have_distinct_effect_ownership(self):
        x={x["name"]:x["ownerApprovedDisposition"]["key"] for x in self.catalog["entries"]}
        self.assertIn("retire-native-selectable",x["Thirsting Blade"])
        self.assertIn("three-attacks-total",x["Devouring Blade"])
        self.assertIn("alternate-bonus-action-command",x["Investment of the Chain Master"])
        self.assertIn("no-free-duplicate",x["Book of Ancient Secrets"])
        self.assertIn("separate-proficiency",x["Protection of the Talisman"])
        self.assertIn("one-shared-directional",x["Bond of the Talisman"])
        self.assertEqual("pending-native-reconciliation",self.base["baselineStatus"])
    def test_original_srd_native_variants_still_unmodified_by_approval(self):
        for x in self.catalog["entries"]:
            if not x["existingNativeFile"]:
                continue
            f=read(x["existingNativeFile"])
            self.assertEqual("pending-native-reconciliation",f["baselineStatus"])
            self.assertIn("candidateDefinitions",f)
            self.assertNotIn("definition",f)

if __name__=="__main__":
    unittest.main()
