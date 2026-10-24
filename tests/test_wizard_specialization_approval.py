"""Approved Wizard specialization selections are recorded without prematurely converting the class."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class WizardSpecializationApprovalTests(unittest.TestCase):
    def setUp(self):
        self.wizard = load("data/native/classes/base/wizard.json")
        self.specialization = self.wizard["approvedNativeSelections"]["wizardSpecialization"]

    def test_class_source_candidates_remain_unconverted(self):
        self.assertEqual("pending-native-reconciliation", self.wizard["baselineStatus"])
        self.assertEqual({"candidate-a", "candidate-b"}, {
            entry["key"] for entry in self.wizard["candidateDefinitions"]
        })
        self.assertEqual("approved-declarative-unwired", self.specialization["status"])

    def test_approved_bonus_choice_levels(self):
        s = self.specialization
        self.assertEqual([5, 10, 15, 20], s["classLevelGrants"])
        self.assertEqual(1, s["optionsPerClassLevelGrant"])
        self.assertEqual(
            ["metamagic", "item-creation", "spell-mastery"],
            s["eligibleOptionFamilies"],
        )
        self.assertFalse(s["classGrantReplacesOtherClassFeatures"])

    def test_feat_substitution_and_prerequisites(self):
        s = self.specialization
        self.assertTrue(s["acquisitionPathways"]["featSubstitutionConsumesFeatChoice"])
        self.assertTrue(s["acquisitionPathways"]["abilityScoreImprovementRequiresExistingFeatAlternative"])
        self.assertEqual("apply-underlying-option-prerequisites", s["prerequisites"])
        self.assertEqual("only-when-underlying-option-permits", s["duplicateSelections"])

    def test_no_unapproved_metamagic_or_auto_scribe_scroll(self):
        s = self.specialization
        self.assertFalse(s["grantsSorceryPoints"])
        self.assertFalse(s["grantsSorcererMetamagicFeature"])
        self.assertEqual("separate-reconciliation", s["historicalScribeScrollLevel1Grant"])
        self.assertTrue(s["sharedOptionEffectsAndCosts"].startswith("deferred-"))

    def test_owner_decision_ledger(self):
        decisions = load("data/reference/srd/class-reconciliation-decisions.json")["decisions"]
        hits = [d for d in decisions if d["nativeRuleId"] == "class.wizard.specialization-selections"]
        self.assertEqual(1, len(hits))
        self.assertEqual("approved-native-rule", hits[0]["state"])


if __name__ == "__main__":
    unittest.main()
