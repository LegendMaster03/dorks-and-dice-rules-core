"""Historical Warlock invocation provenance inventory; no native rules changed."""
import json
from pathlib import Path
import collections
import unittest

R=Path(__file__).resolve().parents[1]
def load(p): return json.loads((R/p).read_text(encoding="utf-8"))

class WarlockResearchInventoryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.r=load("data/reference/3x/warlock-invocations-research-index.json")

    def test_fixed_six_book_index(self):
        r=self.r
        self.assertEqual("research-only-not-approved-native-options",r["status"])
        self.assertEqual(90,r["totalIndexed"])
        self.assertEqual(90,len(r["entries"]))
        self.assertEqual({"CAr":50,"CM":22,"DrM":9,"City":3,"DotU":2,"MoI":4},
                         dict(collections.Counter(x["source"] for x in r["entries"])))
        self.assertEqual({"least":27,"lesser":30,"greater":21,"dark":12},
                         dict(collections.Counter(x["grade"] for x in r["entries"])))
        self.assertEqual(90,len({x["name"].lower() for x in r["entries"]}))
        self.assertTrue(all(x["approvalStatus"]=="research-only" for x in r["entries"]))
        self.assertEqual(4,sum(x["source"]=="MoI" for x in r["entries"]))
        self.assertTrue(self.r["original3xWarlock"]["eldritchBlastErrata"]["changes"])
        self.assertIn("INVOCATION",self.r["original3xWarlock"]["eldritchBlast"])

    def test_corrected_source_specific_categories(self):
        indexed={x["name"]:x for x in self.r["entries"]}
        for name,kind in {
            "Dragonward":"other","Eldritch Line":"blast-shape",
            "Penetrating Blast":"essence","Hindering Blast":"essence",
            "Eldritch Glaive":"blast-shape",
        }.items():
            self.assertEqual(kind,indexed[name]["category"])
        self.assertTrue(set(x["category"] for x in self.r["entries"]).issubset(
            {"blast-shape","essence","other"}))

    def test_historical_and_modern_are_separate(self):
        x=self.r
        self.assertEqual([1,2,2,3,3,4,4,5,5,6,7,7,8,8,9,10,10,11,11,12],
                         x["original3xWarlock"]["invocationsKnownPerLevel"])
        self.assertEqual({"least":1,"lesser":6,"greater":11,"dark":16},
                         x["original3xWarlock"]["gradeUnlockAtClassLevel"])
        self.assertEqual(8,x["modernSRDComparativeFacts"]["invocationKnownAt20"]["srd51"])
        self.assertEqual(10,x["modernSRDComparativeFacts"]["invocationKnownAt20"]["srd52"])
        self.assertEqual(28,x["modernSRDComparativeFacts"]["srd52ReferencedInvocationOptions"])
        native=load("data/rulesets/first-party-001/working/classes/base/warlock.json")
        self.assertEqual("pending-native-reconciliation",native["baselineStatus"])
        self.assertEqual(2,len(native["candidateDefinitions"]))

if __name__=="__main__":
    unittest.main()
