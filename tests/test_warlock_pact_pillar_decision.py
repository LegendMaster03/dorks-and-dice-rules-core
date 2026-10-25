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
        self.assertIn("defer eligibility, level, cost",self.inventory["openDesignRulings"][0])

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

    def test_pact_patron_and_invocation_requirements_use_same_bound_pact(self):
        p=self.inventory["approvedNativePactDirection"]
        requirements=p["compatibilityRules"]
        self.assertEqual("owner-approved-pact-invocation-patron-eligibility-and-patron-acceptance",requirements["status"])
        self.assertTrue(requirements["pactChoice"]["mayRequireCompatiblePatron"])
        self.assertTrue(requirements["pactChoice"]["validatesPatronAssociatedWithPact"])
        self.assertTrue(requirements["pactChoice"]["noMandatoryPatronRestrictionsOnEveryPact"])
        invocation=requirements["invocationChoice"]
        self.assertTrue(invocation["alwaysHasBoundPact"])
        self.assertTrue(invocation["mayRestrictPactType"])
        self.assertTrue(invocation["mayRestrictPatronOfBoundPact"])
        self.assertTrue(invocation["mayRequireBothPactAndItsPatron"])
        self.assertTrue(invocation["validatesAgainstBoundPactNotAnyUnrelatedPact"])
        self.assertTrue(invocation["validatesAgainstPatronOfBoundPactNotAnyOtherPatron"])
        self.assertTrue(invocation["noMandatorySpecificPactTypeRestrictionOnEveryInvocation"])
        self.assertTrue(invocation["noMandatoryPatronRestrictionOnEveryInvocation"])
        patron_links=requirements["pactAssociations"]
        self.assertEqual("per-distinct-pact-not-solely-global-character-patron",patron_links["pactToPatronRecordScope"])
        self.assertTrue(patron_links["patronMayBeUnrevealedOrInherited"])
        self.assertTrue(patron_links["literalPersonalContractNotRequired"])
        self.assertEqual(4,len(requirements["exampleEligibilityMatrixNonPlayable"]))
        self.assertEqual([True,False,False,True],
                         [fixture["eligible"] for fixture in requirements["exampleEligibilityMatrixNonPlayable"]])

    def test_patrons_control_terms_without_waiving_explicit_prerequisites(self):
        p=self.inventory["approvedNativePactDirection"]
        acceptance=p["compatibilityRules"]["patronTermsAndWillingness"]
        for field in ("patronCanSpecifyAcceptablePacts",
                      "patronCanSpecifyAcceptableInvocationsOrClauses",
                      "patronTermsMayDependOnSelectedPact",
                      "patronAcceptanceDistinctFromMechanicalPrerequisites",
                      "bothMustBeSatisfiedWhenAcceptanceIsSpecified",
                      "individualPatronMayDeclineOtherwiseEligibleOption",
                      "acceptanceMayBeEstablishedInInitialOrInheritedPactTerms",
                      "patronCannotSilentlyOverrideExplicitMechanicalPrerequisites"):
            self.assertTrue(acceptance[field],field)
        self.assertTrue(acceptance["noBlanketDefaultRejectionOfStandardPactsOrInvocations"])
        self.assertFalse(acceptance["requiresActivePatronCommunicationForEveryChoice"])
        self.assertFalse(acceptance["requiresPersonallySignedAgreement"])
        self.assertEqual("setting-or-gm-defined-patron-terms-and-willingness",acceptance["adjudication"])
        checks=p["compatibilityRules"]["validation"]
        self.assertTrue(checks["checkPatronAcceptanceWhenSpecified"])
        self.assertTrue(checks["acceptanceScopedToPatronOfBoundPact"])
        self.assertTrue(checks["revalidatePatronAcceptanceOnInvocationTransfer"])
        self.assertTrue(checks["revalidateOnInvocationRestorationOrTransfer"])
        self.assertTrue(checks["inheritedTermsCanSatisfyWithoutNewNegotiation"])
        self.assertTrue(checks["newPactTermsMustExplicitlyAuthorizeRestorationOrTransfer"])
        self.assertTrue(checks["newPactTermsNotAutomaticWaiverOfRequirements"])
        self.assertTrue(checks["doNotTreatGeneralMechanicalEligibilityAsProofOfPatronAgreement"])
        self.assertTrue(checks["noUnapprovedExceptionOrSourceSpecificRestriction"])
        self.assertTrue(checks["sameInGuidedAndCustomBuilds"])
        self.assertIn("receiving Patron",p["pactInvocationLifecycle"]["compatibilityRevalidation"])

    def test_blade_2024_baseline_and_intrinsic_level_five_pact_advancement(self):
        pact=self.inventory["approvedNativePactDirection"]
        blade=pact["bladePactNativeBaseline"]
        self.assertEqual("owner-approved-2024-base-and-intrinsic-level5-extra-attack",blade["status"])
        self.assertEqual("Pact of the Blade",blade["pact"])
        self.assertEqual("SRD52",blade["sourceEdition"])
        self.assertEqual(1,blade["initialAcquisition"]["classLevel"])
        self.assertFalse(blade["initialAcquisition"]["consumesInvocationSelection"])
        features=blade["approvedBaseFeatures"]
        self.assertEqual("bonus-action",features["createConjuredWeapon"]["activation"])
        self.assertEqual(["simple-melee","martial-melee"],features["createConjuredWeapon"]["weaponTypes"])
        self.assertEqual("bonus-action",features["bondExistingMagicWeapon"]["activation"])
        self.assertTrue(features["bondExistingMagicWeapon"]["forbidsAttunedToAnotherCreature"])
        self.assertTrue(features["bondExistingMagicWeapon"]["forbidsAlreadyBoundToAnotherWarlock"])
        self.assertTrue(features["proficientWithPactWeapon"])
        self.assertTrue(features["weaponCanBeSpellcastingFocus"])
        self.assertTrue(features["mayUseCharismaForAttackAndDamageInsteadOfStrengthOrDexterity"])
        self.assertEqual({"normal-weapon-type","necrotic","psychic","radiant"},
                         set(features["selectableDamageTypes"]))
        self.assertEqual("review-separately",
                         blade["deferredSourceDifferences"]["original2014HourRitualAndExtradimensionalMagicWeaponStorage"])
        progression=blade["pactProgressionConcern"]
        self.assertEqual("owner-approved-intrinsic-level5-attack-other-progression-open",progression["status"])
        self.assertEqual("partially-owner-approved-level5-extra-attack",progression["nativePactIntrinsicProgression"])
        self.assertEqual("owner-approved-intrinsic-pact-feature",progression["specificPotentialLevel5SecondAttack"])
        self.assertEqual("base-two-attack-benefit-superseded-by-approved-level5-pact-progression-replacement-option-unresolved",progression["thirstingBladeDisposition"])
        self.assertTrue(progression["avoidsAutomaticStacking"])
        extra=progression["approvedLevel5ExtraAttack"]
        self.assertEqual("owner-approved",extra["status"])
        self.assertEqual("Blade Pact Extra Attack (Pact pillar advancement)",extra["feature"])
        self.assertEqual(5,extra["warlockClassLevel"])
        self.assertEqual("attack-action",extra["action"])
        self.assertEqual(2,extra["totalAttacksWithPactWeapon"])
        self.assertEqual("weapon-currently-bound-or-conjured-by-pact-of-the-blade",extra["qualifyingWeapon"])
        self.assertTrue(extra["grantsOneAdditionalAttackRatherThanTwoIndependentFeatures"])
        self.assertFalse(extra["consumesInvocationSelection"])
        self.assertFalse(extra["requiresThirstingBladeInvocation"])
        self.assertEqual("pact-of-the-blade",extra["grantedBy"])
        self.assertEqual("unresolved",extra["additionalHigherLevelBladeProgression"])
        self.assertIn("non-Thirsting-Blade-prerequisite",progression["devouringBladeDisposition"])
        bg3=blade["externalBG3Research"]
        self.assertEqual("comparison-only-not-SRD-not-blanket-native-grant",bg3["status"])
        self.assertTrue(bg3["bladeExtraAttackViaDeepenedPact"])
        self.assertTrue(bg3["appliesWithoutThirstingBladeInvocation"])
        self.assertTrue(bg3["bindsNonmagicalOrMagicalExistingWeapon"])
        self.assertTrue(bg3["pactWeaponImmuneToDisarmingAndDropping"])
        self.assertTrue(bg3["preventsThrowingBoundWeapon"])
        self.assertTrue(bg3["stackingExploitNotAdopted"])
        self.assertEqual(4,len(bg3["candidatesRequiringOwnerApproval"]))

        self.assertFalse(blade["nativeExecutableActivation"])
        self.assertEqual("pending-native-reconciliation",self.base["baselineStatus"])

if __name__=="__main__":
    unittest.main()
