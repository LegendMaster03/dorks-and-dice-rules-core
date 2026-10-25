"""Verify approved native Pact pillar acquisition without mutating historical source projections."""
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[1]

def load(path):
    return json.loads((ROOT/path).read_text(encoding="utf-8"))

class WarlockPactPillarDecisionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.inventory=load("data/reference/srd/warlock-srd-slice-inventory.json")
        cls.base=load("data/native/classes/base/warlock.json")

    def test_first_pact_is_dedicated_level_one_class_entitlement(self):
        p=self.inventory["approvedNativePactDirection"]
        self.assertEqual("owner-approved-initial-pact-entitlement-and-level-one-acquisition",p["status"])
        self.assertEqual("Pacts",p["pillar"])
        self.assertTrue(p["separateFromInvocationPillar"])
        self.assertTrue(p["everyWarlockReceivesDedicatedInitialPactChoice"])
        self.assertEqual(1,p["initialPactClassLevel"])
        self.assertEqual(1,p["pactEntitlementCountAtLevel1"])
        self.assertEqual("separate-class-pact-selection-not-eldritch-invocation",p["initialPactEntitlement"])
        self.assertFalse(p["firstPactConsumesInvocationSelection"])
        self.assertFalse(p["pactChoicesAreOrdinaryInvocations"])
        self.assertTrue(p["existingPactSelectionIsNotDuplicatedByGuidedBuild"])
        grant=p["levelOneAcquisition"]
        self.assertEqual(1,grant["classLevel"])
        self.assertEqual(1,grant["pactSelections"])
        self.assertTrue(grant["usesDedicatedPactEntitlement"])
        self.assertTrue(grant["acquiredAlongsidePatronIdentity"])
        self.assertFalse(grant["consumesInvocationSelection"])
        self.assertTrue(grant["invocationChoicesRemainSeparate"])
        self.assertEqual("order-independent-with-final-prerequisite-validation",grant["patronPactSelectionOrder"])

    def test_patron_pact_invocation_are_distinct_without_forcing_literal_contract(self):
        p=self.inventory["approvedNativePactDirection"]
        self.assertEqual({"Patron","Pact","EldritchInvocations"},set(p["levelOnePillarRoles"]))
        self.assertTrue(p["clauseAnalogyNotMandatoryLiteralContract"])
        self.assertTrue(p["inheritedOrNoncontractualPowerRelationshipsRemainValid"])
        self.assertTrue(p["pactEnhancementsCanBeInvocations"])
        self.assertTrue(p["invocationMayRequirePact"])

    def test_three_pacts_keep_their_original_source_categories(self):
        p=self.inventory["approvedNativePactDirection"]
        names={"Pact of the Blade","Pact of the Chain","Pact of the Tome"}
        self.assertEqual(names,set(p["inScopeChoices"]))
        for name in names:
            found=[option for option in self.inventory["options"] if option["name"]==name]
            self.assertEqual(1,len(found))
            editions={edition["source"]:set(edition["sourceCategory"]) for edition in found[0]["editions"]}
            self.assertEqual({"SRD51":{"PB"},"SRD52":{"EI"}},editions)
        self.assertEqual(3,self.inventory["editionCounts"]["SRD51"]["separatePacts"])

    def test_unresolved_second_pact_rules_are_not_implicitly_approved(self):
        p=self.inventory["approvedNativePactDirection"]
        open_rules=p["decisionsPending"]
        self.assertNotIn("initialPactAcquisitionWarlockLevel",open_rules)
        self.assertIsNone(open_rules["additionalPactsAllowed"])
        self.assertIsNone(open_rules["additionalPactsAcquisitionCost"])
        self.assertIsNone(open_rules["furtherPactEntitlementProgression"])
        self.assertEqual(3,len(open_rules["individualPactMechanicalReconciliation"]))
        self.assertEqual("pending-native-reconciliation",self.base["baselineStatus"])
        self.assertIn("Additional Pact eligibility and cost",self.inventory["openDesignRulings"][0])

    def test_invocations_are_pact_bound_and_not_independent(self):
        p=self.inventory["approvedNativePactDirection"]
        lifecycle=p["pactInvocationLifecycle"]
        self.assertEqual("owner-approved-design-direction-implementation-semantics-deferred",lifecycle["status"])
        self.assertEqual("major-distinct-power-agreement",lifecycle["pactNature"])
        self.assertEqual("additional-clauses-bound-to-a-specific-pact",lifecycle["invocationNature"])
        self.assertTrue(lifecycle["invocationAlwaysAssociatedWithPact"])
        self.assertTrue(lifecycle["invocationNotIndependentOfPact"])
        self.assertTrue(lifecycle["pactTermination"]["dependentInvocationsLoseAccess"])
        self.assertFalse(lifecycle["pactTermination"]["defaultRetainInvocationUseAfterPactEnds"])
        self.assertFalse(lifecycle["pactTermination"]["removeHistoricalAcquisitionRecords"])
        self.assertTrue(lifecycle["newPactTerms"]["canExplicitlyRestorePriorInvocations"])
        self.assertTrue(lifecycle["newPactTerms"]["canExplicitlyTransferPriorInvocations"])
        self.assertFalse(lifecycle["newPactTerms"]["automaticCarryoverWithoutNewTerms"])
        self.assertEqual("deferred",lifecycle["newPactTerms"]["transferEligibilityAndCosts"])
        self.assertTrue(lifecycle["multiplePacts"]["conceptualSupportRequired"])
        self.assertTrue(lifecycle["multiplePacts"]["eachPactMustBeDistinct"])
        self.assertTrue(lifecycle["multiplePacts"]["invocationsMustTrackPactAffiliation"])
        self.assertFalse(lifecycle["multiplePacts"]["canAccessAdditionalPactThroughCurrentDefaultProgression"])
        self.assertEqual("deferred-to-future-advanced-rule",lifecycle["multiplePacts"]["acquisitionGates"])

    def test_optional_additional_pacts_not_prohibited_or_prematurely_enabled(self):
        p=self.inventory["approvedNativePactDirection"]
        extra=p["additionalPactDesignDirection"]
        self.assertEqual("owner-approved-defer-as-advanced-option",extra["status"])
        self.assertFalse(extra["additionalPactsCategoricallyForbidden"])
        self.assertFalse(extra["approvedAsDefaultNativeEntitlement"])
        self.assertFalse(extra["availableInCurrentWarlockVerticalSlice"])
        self.assertIsNone(extra["minimumWarlockLevel"])
        self.assertIsNone(extra["costOrEntitlement"])
        self.assertIsNone(extra["maximumPacts"])
        open_rules=p["decisionsPending"]
        self.assertIsNone(open_rules["additionalPactsAllowed"])
        self.assertIsNone(open_rules["additionalPactsAcquisitionCost"])
        self.assertIsNone(open_rules["furtherPactEntitlementProgression"])
        self.assertEqual("pending-native-reconciliation",self.base["baselineStatus"])

if __name__=="__main__":
    unittest.main()
