"""Wizard native Band 1 regression suite."""
import json
from pathlib import Path
import unittest
R=Path(__file__).resolve().parents[1]
def read(p): return json.loads((R/p).read_text(encoding="utf-8"))
class WizardBand1Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.w=read("data/native/classes/base/wizard.json")["definition"]
        cls.f={p.stem:read(p.relative_to(R)) for p in (R/"data/native/features/classes/wizard").glob("*.json")}
    def test_full_progression(self):
        rows=self.w["progression"]
        self.assertEqual(list(range(1,21)),[x["level"] for x in rows])
        self.assertEqual([3,6,10,14],[x["level"] for x in rows if x["subclassMilestone"]])
        grants=[g for row in rows for g in row["featureGrants"]]
        self.assertEqual(16,len(grants));self.assertEqual(16,len(set(grants)))
        for g in grants:self.assertEqual("retained-native-definition",self.f[g.removeprefix("class-feature.wizard.")]["baselineStatus"])
        self.assertEqual(10,sum(x["baselineStatus"]=="pending-native-reconciliation" for x in self.f.values()))
        self.assertEqual({"class-feature.wizard.signature-spells-level-20","class-feature.wizard.wizard-specialization-level-20"},set(rows[19]["featureGrants"]))
        self.assertFalse(any(any(key in g for key in ("memorize-spell","epic-boon","subclass","arcane-tradition")) for g in grants))
    def test_caster_and_spellbook(self):
        self.assertEqual(6,self.w["hitDie"]["faces"])
        self.assertEqual("intelligence",self.w["spellcastingAbility"])
        self.assertEqual("none",self.w["spellPreparation"])
        self.assertEqual(["spell-slots","spell-points"],self.w["spellResourceModes"])
        self.assertEqual([3,4,5],[self.w["progression"][i-1]["cantripsKnown"] for i in (1,4,10)])
        self.assertEqual([6+2*i for i in range(20)],self.w["spellbook"]["learnedLeveledSpellsByLevelWithoutAddedCopies"])
        self.assertTrue(self.w["spellbook"]["eligibleAlreadyLearnedSpellsRemainCastableWithoutPhysicalBook"])
        self.assertTrue(self.f["ritual-adept-level-1"]["definition"]["mechanics"]["mustReadFromPhysicalSpellbook"])
    def test_specialization_scribe_and_mastery(self):
        s=self.w["wizardSpecialization"]
        self.assertEqual([5,10,15,20],s["classLevelGrants"])
        self.assertEqual(["metamagic","item-creation"],s["eligibleOptionFamilies"])
        self.assertTrue(s["ordinaryGeneralFeatMayBeReplacedByOneEligibleSpecializationOption"])
        self.assertTrue(s["usingFeatPathConsumesGeneralFeatChoice"])
        self.assertEqual("excluded",s["historicalSpellMasteryFeat"])
        self.assertFalse(self.f["scribe-scroll-level-1"]["definition"]["mechanics"]["consumesGeneralFeat"])
        sm=self.f["spell-mastery-level-18"]["definition"]["mechanics"]
        self.assertEqual("SRD51-2014",sm["sourceEdition"])
        self.assertEqual(8,sm["replacementStudyHours"])
        self.assertEqual([{"level":1,"count":1},{"level":2,"count":1}],sm["choices"])
        self.assertEqual(21,self.w["epicBoonMinimumTotalCharacterLevel"])
    def test_3x_and_companion(self):
        row=self.w["progression"][-1]
        self.assertEqual([10,5],row["iterativeBaseAttackBonuses"])
        self.assertEqual({"fortitude":6,"reflex":6,"will":12},row["baseSaves"])
        self.assertTrue(self.w["companionPathChoice"]["exclusive"])
        self.assertEqual("deferred-to-shared-companion-system",self.w["companionPathChoice"]["implementation"])
    def test_manifest(self):
        m=read("data/reference/srd/native-coverage-manifest.json")
        rows={x["path"]:x for x in m["coverage"]}
        self.assertEqual(2522,len(rows))
        for p in [R/"data/native/classes/base/wizard.json",*(R/"data/native/features/classes/wizard").glob("*.json")]:
            x=read(p.relative_to(R));entry=rows.get(p.relative_to(R).as_posix())
            if entry is None:self.assertIn(p.stem,{"scribe-scroll-level-1","wizard-specialization-level-5","wizard-specialization-level-10","wizard-specialization-level-15","wizard-specialization-level-20"})
            elif x["baselineStatus"]=="retained-native-definition":self.assertIn(entry["status"],{"native-single-source-retained","native-compatible-merged"})
            else:self.assertIn(entry["status"],{"single-candidate","multiple-candidates"})
        s=read("docs/rewrite/native-reconciliation-statistics.json")
        self.assertEqual(16,s["approvedWizardBand1"]["activeFeatureFiles"])
        self.assertEqual(1501,s["candidateRecordsStillUnresolved"])
        self.assertEqual(s["statuses"],m["reconciliationCounts"])
if __name__=="__main__":unittest.main()
