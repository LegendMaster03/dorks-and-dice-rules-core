"""Approved specialization rules stay distinct from Sorcerer Metamagic."""
import json
from pathlib import Path
import unittest
R=Path(__file__).resolve().parents[1]
class WizardApprovalTests(unittest.TestCase):
    def test_selection_policy(self):
        w=json.loads((R/"data/rulesets/first-party-001/working/classes/base/wizard.json").read_text(encoding="utf-8"))
        self.assertEqual("retained-native-definition",w["baselineStatus"])
        s=w["definition"]["wizardSpecialization"]
        self.assertEqual([5,10,15,20],s["classLevelGrants"])
        self.assertEqual(["metamagic","item-creation"],s["eligibleOptionFamilies"])
        self.assertTrue(s["usingFeatPathConsumesGeneralFeatChoice"])
        self.assertTrue(s["ordinaryAsiAllowsFeatSubstitutionOnlyIfItAlreadyPermitsFeats"])
        self.assertTrue(s["optionPrerequisitesApply"])
        self.assertTrue(s["repeatOptionOnlyIfItsOwnRulesAllow"])
        self.assertTrue(s["doesNotGrantSorceryPoints"])
        self.assertTrue(s["doesNotGrantSorcererMetamagicClassFeature"])
        self.assertEqual("excluded",s["historicalSpellMasteryFeat"])
        self.assertEqual("automatic-class-feature-no-feat-or-specialization-choice",w["definition"]["scribeScrollLevel1"])
if __name__=="__main__":unittest.main()
