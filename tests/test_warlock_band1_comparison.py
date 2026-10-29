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
        cls.warlock=load("data/rulesets/first-party-001/working/classes/base/warlock.json")

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
                "owner-decision-needed","approved-native-rule","source-equivalence-check",
                "shared-class-rule-already-established",
                "shared-casting-constraints-already-established",
                "shared-epic-boon-rule-already-established",
                "follow-on-to-invocation-progression"})

    def test_historical_case_1_approved_without_premature_implementation(self):
        decision=next(x for x in self.crosswalk["decisions"]
                      if x["id"]=="historical-defense-utility")
        self.assertEqual("approved-native-rule",decision["status"])
        disposition=decision["approvedDisposition"]
        self.assertEqual(4,len(disposition["retainedClassFeatureFamilies"]))
        self.assertIn("Eldritch Sight",disposition["detectMagic"])
        self.assertEqual([3,7,11,15,19],
                         [x["classLevel"] for x in disposition["damageReduction"]])
        self.assertNotIn("fiendish-resilience",disposition["retainedClassFeatureFamilies"])
        self.assertEqual([1,2,5],decision["priorApprovedBaseHealing"]["historicalCandidateValues"]["healingPerRound"])
        self.assertEqual("long-rest",decision["priorApprovedBaseHealing"]["historicalCandidateValues"]["recharge"])
        retaliation=disposition["fiendPatronInfernalRetaliation"]
        self.assertTrue(retaliation["singleFeatureEntitlement"])
        self.assertEqual("Fiend Patron only",retaliation["scope"])
        self.assertEqual(1,retaliation["sharedUses"])
        self.assertEqual(2,len(retaliation["effects"]))
        self.assertEqual(["fiendish-resilience-fast-healing","fiendish-flamewreath"],retaliation["effects"])
        self.assertEqual([13,18],retaliation["upgradeClassLevels"])
        self.assertEqual("stabilize-once-remain-unconscious-no-healing-or-repeat-stabilization",retaliation["fastHealingAtZeroHp"])
        self.assertIn("optional Hellfire",disposition["hellfireShieldNativeDisposition"])
        self.assertEqual("pending-native-reconciliation",self.warlock["baselineStatus"])
        ledger=load("data/reference/srd/class-reconciliation-decisions.json")["decisions"]
        self.assertEqual(1,sum(x["nativeRuleId"]=="class.warlock.historical-class-features-band1"
                               for x in ledger))


if __name__=="__main__": unittest.main()
