"""Warlock base-only reconciliation source crosswalk guardrails."""
import json
from pathlib import Path
import unittest

R=Path(__file__).resolve().parents[1]
def load(path): return json.loads((R/path).read_text(encoding="utf-8"))

class WarlockBand1ComparisonTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.crosswalk=load("data/reference/3x/warlock-band1-base-comparison.json")
        cls.warlock=load("data/native/classes/base/warlock.json")

    def test_scope_and_base_source_consistency(self):
        obj=self.crosswalk
        self.assertEqual("source-comparison-complete-owner-decisions-open",obj["status"])
        self.assertEqual(20,len(obj["levels"]))
        self.assertEqual(list(range(1,21)),[r["level"] for r in obj["levels"]])
        self.assertTrue(obj["nativeWarlockDefinitionUnmodified"])
        self.assertEqual("pending-native-reconciliation",self.warlock["baselineStatus"])
        self.assertEqual(2,len(self.warlock["candidateDefinitions"]))

    def test_invocation_progressions_are_not_merged(self):
        rows=self.crosswalk["levels"]
        self.assertEqual((12,8,10),(rows[-1]["historical3x"]["invocationsKnown"],
                                   rows[-1]["srd51"]["invocationsKnown"],
                                   rows[-1]["srd52"]["invocationsKnown"]))
        self.assertEqual((1,0,1),(rows[0]["historical3x"]["invocationsKnown"],
                                 rows[0]["srd51"]["invocationsKnown"],
                                 rows[0]["srd52"]["invocationsKnown"]))
        self.assertEqual((4,4),(rows[-1]["srd51"]["pactSlots"],
                                rows[-1]["srd52"]["pactSlots"]))
        self.assertTrue(all(a["srd51"]["spellsKnown"]==a["srd52"]["finiteSpellChoices"] for a in rows))

    def test_subclasses_are_separate(self):
        rows=self.crosswalk["levels"]
        self.assertTrue(rows[0]["srd51"]["subclassFeaturesOrMilestones"])
        self.assertEqual([] ,rows[0]["srd52"]["subclassFeaturesOrMilestones"])
        self.assertTrue(rows[2]["srd52"]["subclassFeaturesOrMilestones"])
        self.assertEqual([6,10,14],[x["level"] for x in rows
            if x["level"]>3 and x["srd52"]["subclassFeaturesOrMilestones"]])

    def test_no_option_catalog_import_or_automatic_approval(self):
        self.assertIn("ninety-supplement-invocation-crosswalk",self.crosswalk["expresslyOutOfScope"])
        for decision in self.crosswalk["decisions"]:
            self.assertIn(decision["status"],{
                "owner-decision-needed","source-equivalence-check",
                "shared-class-rule-already-established",
                "shared-casting-constraints-already-established",
                "shared-epic-boon-rule-already-established",
                "follow-on-to-invocation-progression"})

if __name__=="__main__": unittest.main()
