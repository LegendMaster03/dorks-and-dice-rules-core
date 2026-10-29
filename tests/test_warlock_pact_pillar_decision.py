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
        cls.base=load("data/rulesets/first-party-001/working/classes/base/warlock.json")

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

    def test_original_srd_pacts_preserve_source_categories_when_scope_extends(self):
        p=self.inventory["approvedNativePactDirection"]
        names={"Pact of the Blade","Pact of the Chain","Pact of the Tome"}
        self.assertTrue(names.issubset(set(p["inScopeChoices"])))
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
        self.assertEqual(4,len(open_rules["individualPactMechanicalReconciliation"]))
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
        self.assertEqual(3,len(bg3["candidatesRequiringOwnerApproval"]))

        self.assertFalse(blade["nativeExecutableActivation"])
        self.assertEqual("pending-native-reconciliation",self.base["baselineStatus"])

    def test_all_four_active_pacts_gain_intrinsic_level_five_upgrades_without_invocation_cost(self):
        p=self.inventory["approvedNativePactDirection"]
        deep=p["approvedLevelFivePactImprovements"]
        self.assertEqual("owner-approved-design-not-executable-native-content",deep["status"])
        self.assertEqual(5,deep["warlockClassLevel"])
        self.assertFalse(deep["consumesEldritchInvocationSelections"])
        self.assertEqual("Pacts",deep["progressionPillar"])
        self.assertTrue(deep["appliesToEachPactSeparately"])
        self.assertEqual({"blade","chain","tome","talisman"},set(deep["options"]))
        blade=deep["options"]["blade"]
        self.assertTrue(blade["approved"])
        self.assertEqual(2,blade["totalAttacksOnAttackAction"])
        self.assertTrue(blade["noStackingDuplicateThirstingBlade"])
        chain=deep["options"]["chain"]
        self.assertTrue(chain["approved"])
        self.assertEqual(5,chain["level"])
        self.assertTrue(chain["extraAttackOnFamiliarAttackAction"])
        self.assertEqual(2,chain["totalAttacksIfOrdinarilyOne"])
        self.assertFalse(chain["usesInvocationChoice"])
        self.assertTrue(chain["doesNotGrantWarlockExtraAttack"])
        self.assertIn("reconcile-with-native",chain["attackCommandAndTurnEconomy"])
        tome=deep["options"]["tome"]
        self.assertTrue(tome["approved"])
        self.assertEqual(5,tome["level"])
        self.assertEqual({"Animate Dead","Haste","Call Lightning"},{z["name"] for z in tome["spells"]})
        self.assertTrue(all(z["freeCastsPerLongRest"]==1 and z["spellLevel"]==3 for z in tome["spells"]))
        self.assertEqual(0,tome["slotsConsumedForGrantedUses"])
        self.assertFalse(tome["consumeRegularWarlockSpellsKnownEntitlement"])
        self.assertFalse(tome["usesInvocationChoice"])
        self.assertIn("no-daily",tome["noDailyPreparation"])
        self.assertNotIn("Hexblade",deep["options"])
        self.assertFalse(p["bladePactNativeBaseline"]["nativeExecutableActivation"])
        self.assertEqual("pending-native-reconciliation",self.base["baselineStatus"])

    def test_nonmagical_weapon_bond_applies_to_blade_only(self):
        p=self.inventory["approvedNativePactDirection"]
        b=p["bladePactNativeBaseline"]["approvedBaseFeatures"]["bondOrdinaryNonmagicalWeapons"]
        self.assertEqual("owner-approved-for-blade-specific-weapon-bond",b["status"])
        self.assertTrue(b["mayBindExistingNonmagicalMeleeWeapon"])
        self.assertTrue(b["turnsBoundWeaponMagicalForDamageAndResistanceInteraction"])
        self.assertEqual("bonus-action-from-approved-2024-Pact-bond",b["action"])
        self.assertTrue(b["requiresPactOfTheBlade"])
        self.assertTrue(b["doesNotGrantWeaponBondingToChainOrTome"])
        self.assertEqual("not-approved-yet",b["droppingThrowingDisarm"])
        self.assertNotIn("bond-mundane-existing-weapons-as-magical",
                         p["bladePactNativeBaseline"]["externalBG3Research"]["candidatesRequiringOwnerApproval"])

    def test_hexblade_pillar_classification_remains_research_only(self):
        p=self.inventory["approvedNativePactDirection"]
        h=p["hexbladeClassificationReview"]
        self.assertEqual("research-only-no-owner-reclassification",h["status"])
        self.assertFalse(h["inCurrentPlayableSRDVerticalSlice"])
        self.assertTrue(h["doesNotChangePlayablePatronMenu"])
        self.assertIn("Shadowfell",h["xgte2014Lore"])
        self.assertIn("Curse", " ".join(h["openIssues"]))
        self.assertIn("Charisma weapon attack/damage",h["redundantHexWarriorElements"])

    def test_four_active_pacts_preserve_fifth_archival_research_and_srd_counts(self):
        pact=self.inventory["approvedNativePactDirection"]
        scope=pact["expandedFivePactReview"]
        choices={"Pact of the Blade","Pact of the Chain","Pact of the Tome",
                 "Pact of the Talisman"}
        self.assertEqual(choices,set(pact["inScopeChoices"]))
        self.assertNotIn("Pact of the Star Chain",pact["inScopeChoices"])
        self.assertEqual(["Pact of the Star Chain"],scope["deferredPactChoices"])
        self.assertEqual(4,scope["activePactCount"])
        self.assertEqual(choices,set(scope["activePactChoices"]))
        self.assertEqual(choices,set(pact["decisionsPending"]["individualPactMechanicalReconciliation"]))
        self.assertEqual(5,scope["totalDistinctPactCandidates"])
        self.assertEqual(1,scope["initialPactClassLevel"])
        self.assertEqual(1,scope["initialPactEntitlementCount"])
        self.assertFalse(scope["eachFirstPactConsumesInvocationChoice"])
        self.assertEqual("pending-individual-native-reconciliation",scope["nativeRuntimeImplementationStatus"])
        self.assertTrue(scope["originalSrdOptionCountsMustRemainUnchanged"])
        self.assertEqual(42,len(self.inventory["options"]))
        self.assertEqual(32,self.inventory["editionCounts"]["SRD51"]["invocations"])
        self.assertEqual(3,self.inventory["editionCounts"]["SRD51"]["separatePacts"])
        self.assertEqual(28,self.inventory["editionCounts"]["SRD52"]["invocationsIncludingPacts"])
        self.assertFalse(any(x["name"] in choices-{"Pact of the Blade","Pact of the Chain","Pact of the Tome"} 
                             for x in self.inventory["options"]))
        self.assertTrue(scope["noPatronCatalogExpansion"])
        self.assertTrue(scope["noAdditionalPactAcquisitionRuleApproved"])
        self.assertFalse(any(x.startswith("Pact of the Talisman") for x in self.inventory["outOfScope"]))

    def test_talisman_published_source_and_invocation_family_are_reference_only(self):
        scope=self.inventory["approvedNativePactDirection"]["expandedFivePactReview"]
        t=scope["newSources"]["talisman"]
        self.assertEqual("Pact of the Talisman",t["name"])
        self.assertEqual(2020,t["year"])
        self.assertEqual(3,t["sourcePactLevel"])
        self.assertEqual("published-official-non-SRD-2014-era-pact-boon",t["sourceType"])
        self.assertEqual("1d4 on a failed ability check by the wearer",t["sourceCore"]["failureRollBonus"])
        self.assertEqual("proficiency-bonus",t["sourceCore"]["usesPerLongRest"])
        self.assertEqual(1,t["sourceCore"]["replacementCeremonyHours"])
        self.assertEqual({"Rebuke of the Talisman","Protection of the Talisman","Bond of the Talisman"},
                         set(t["invocationNames"]))
        self.assertEqual({"Rebuke of the Talisman":None,"Protection of the Talisman":7,
                          "Bond of the Talisman":12},t["publishedInvocationLevelPrereqs"])
        self.assertTrue(t["mechanicsApprovedForNative"])
        self.assertEqual("owner-approved-deepened-pact-shared-ward",t["level5IntrinsicAdvance"])
        self.assertFalse(t["nativeExecutableActivation"])
        self.assertTrue(t["sourceRulesNotCoveredBySrdLicense"])

    def test_star_chain_enforces_seeker_patron_constraint_not_generic_fiend(self):
        p=self.inventory["approvedNativePactDirection"]
        scope=p["expandedFivePactReview"]
        c=scope["newSources"]["starChain"]
        self.assertEqual("Pact of the Star Chain",c["name"])
        self.assertEqual("unpublished-wizards-of-the-coast-playtest-non-SRD",c["sourceType"])
        self.assertEqual(2016,c["year"])
        self.assertEqual(3,c["sourcePactLevel"])
        self.assertEqual("dedicated-to-the-Seeker",c["patronRestrictionInSource"])
        self.assertEqual("Augury",c["sourceCore"]["ritualSpell"])
        self.assertFalse(c["sourceCore"]["ritualSpellConsumesKnownSpellSelection"])
        self.assertEqual("advantage-on-one-intelligence-check",c["sourceCore"]["checkBenefit"])
        self.assertEqual("short-or-long-rest",c["sourceCore"]["checkBenefitRecharge"])
        self.assertEqual(1,c["sourceCore"]["replacementCeremonyHours"])
        elig=c["eligibility"]
        self.assertTrue(elig["requiresSeekerLinkedPatron"])
        self.assertFalse(elig["matchesOrdinaryFiendPatronByDefault"])
        self.assertEqual("disabled-no-exceptions-under-current-owner-ruling",elig["otherPatronSourceTraitOrSpecialException"])
        self.assertEqual("disabled-not-offered",elig["playabilityWithFiendOnlyPatronSlice"])
        self.assertTrue(elig["nonPlayableSyntheticSeekerFixtureAllowed"])
        self.assertTrue(c["noUnapprovedSeekerPatronImport"])
        self.assertFalse(c["mechanicsApprovedForNative"])
        self.assertEqual("deferred-alongside-Seeker",c["level5IntrinsicAdvance"])
        self.assertTrue(scope["noPatronCatalogExpansion"])
        self.assertFalse(c["enabled"])
        self.assertFalse(c["inActiveReview"])
        self.assertFalse(c["pairedSeekerEnabled"])
        self.assertTrue(c["seekerMayUseOtherPactsInOriginalUA"])
        self.assertTrue(c["starChainRequiresSeekerInOriginalUA"])
        self.assertFalse(c["mustEnableTogetherWithSeeker"])
        self.assertIn("NOT an original source rule",c["ownerNativeMutualDependency"])
        pair=self.inventory["scope"]["disabledHistoricalPatronPactPackage"]
        self.assertEqual("disabled-together",pair["status"])
        self.assertEqual("The Seeker",pair["patron"])
        self.assertEqual("Pact of the Star Chain",pair["pact"])
        self.assertTrue(pair["archivedNotDeleted"])
        self.assertTrue(pair["noRuntimeAvailability"])
        self.assertIn("Pact of the Star Chain and Seeker Patron",self.inventory["outOfScope"][-1])

    def test_all_four_active_pacts_gain_level_five_advancements_but_star_chain_is_deferred(self):
        p=self.inventory["approvedNativePactDirection"]
        levels=p["expandedFivePactReview"]["nativeFirstPactUpgradeApproval"]
        self.assertEqual({"blade":5,"chain":5,"tome":5,"talisman":5,"starChain":None},levels)
        self.assertEqual({"blade","chain","tome","talisman"},set(p["approvedLevelFivePactImprovements"]["options"]))
        self.assertFalse(p["bladePactNativeBaseline"]["nativeExecutableActivation"])
        self.assertEqual("pending-native-reconciliation",self.base["baselineStatus"])

    def test_talisman_approved_level_one_base_fuses_both_sources_without_stacking(self):
        p=self.inventory["approvedNativePactDirection"]
        t=p["expandedFivePactReview"]["newSources"]["talisman"]
        native=t["ownerApprovedNative"]
        self.assertEqual("owner-approved-native-design-pending-runtime-integration",native["status"])
        self.assertTrue(t["mechanicsApprovedForNative"])
        self.assertFalse(t["nativeExecutableActivation"])
        self.assertEqual("Pact of the Talisman",t["name"])
        self.assertEqual(1,native["level1"]["level"])
        self.assertTrue(native["usesDedicatedFirstPactEntitlement"])
        item=native["level1"]["artifact"]
        self.assertEqual(1,item["activePerPact"])
        self.assertTrue(item["mayBeWornByWarlock"])
        self.assertTrue(item["mayBeWornByAlly"])
        guidance=native["level1"]["untrainedGuidance"]
        self.assertEqual("1d4",guidance["dice"])
        self.assertEqual(0,guidance["chargesCost"])
        self.assertEqual("none",guidance["usageLimit"])
        self.assertIn("skill-in-which-wearer-is-not-proficient",guidance["condition"])
        self.assertIn("saving-throws",guidance["excludes"])
        correction=native["level1"]["fortunateCorrection"]
        self.assertEqual("wearer-fails-any-ability-check",correction["condition"])
        self.assertEqual("1d4",correction["dice"])
        self.assertEqual(1,correction["chargesCost"])
        self.assertTrue(correction["mayAffectChecksWithSkillProficiency"])
        self.assertTrue(correction["mayAffectOtherAbilityChecks"])
        nostack=native["level1"]["nonstacking"]
        self.assertEqual(1,nostack["maxNativeTalismanDicePerCheck"])
        self.assertIn("untrained-guidance-and-fortunate-correction",nostack["forbidden"])
        charges=native["level1"]["charges"]
        self.assertEqual("warlock-proficiency-bonus",charges["maximum"])
        self.assertEqual({"fortunate-correction","shared-ward"},set(charges["currentPoolSharedBy"]))
        self.assertEqual("long-rest",charges["recharge"])
        self.assertTrue(charges["replacementDoesNotRecharge"])
        replacement=native["level1"]["replacement"]
        self.assertEqual(1,replacement["ceremonyHours"])
        self.assertTrue(replacement["previousTalismanDeactivates"])
        self.assertFalse(replacement["rechargeChargesOnReplacement"])

    def test_talisman_approved_shared_ward_spends_same_pool_and_reaction(self):
        p=self.inventory["approvedNativePactDirection"]
        tal=p["expandedFivePactReview"]["newSources"]["talisman"]["ownerApprovedNative"]
        ward=tal["level5"]
        self.assertEqual("Deepened Pact: Shared Ward",ward["name"])
        self.assertEqual(5,ward["level"])
        self.assertEqual("active-talisman-wearer-takes-damage",ward["trigger"])
        self.assertEqual(60,ward["rangeFeet"])
        self.assertTrue(ward["selfWearerAllowed"])
        self.assertEqual("reaction-of-warlock",ward["actionCost"])
        self.assertEqual(1,ward["chargesCost"])
        self.assertEqual("level1-talisman-shared-charges",ward["chargesResource"])
        self.assertEqual("2d6",ward["damageReductionDice"])
        self.assertEqual("warlock-charisma-modifier",ward["damageReductionAdd"])
        self.assertEqual(0,ward["minimumDamageReduction"])
        self.assertTrue(ward["reductionCannotExceedIncomingDamage"])
        self.assertTrue(ward["reducesSingleDamageInstance"])
        self.assertFalse(ward["consumesInvocationSelection"])
        self.assertFalse(ward["consumesSpellSlot"])
        self.assertIn("rebuke-of-the-talisman",ward["reactionCompetition"])
        self.assertEqual("not-approved",ward["furtherIntrinsicScaling"])
        deep=p["approvedLevelFivePactImprovements"]["options"]["talisman"]
        self.assertTrue(deep["approved"])
        self.assertTrue(deep["competesWithRebukeReaction"])
        self.assertTrue(deep["noAdditionalChargePool"])
        self.assertFalse(deep["nativeExecutableActivation"])
        self.assertEqual(5,p["expandedFivePactReview"]["nativeFirstPactUpgradeApproval"]["talisman"])
        self.assertIsNone(p["expandedFivePactReview"]["nativeFirstPactUpgradeApproval"]["starChain"])

    def test_talisman_three_invocations_remain_optional_pact_bound_clauses(self):
        p=self.inventory["approvedNativePactDirection"]
        native=p["expandedFivePactReview"]["newSources"]["talisman"]["ownerApprovedNative"]
        invocation=native["invocations"]
        self.assertTrue(invocation["allArePactAttached"])
        self.assertTrue(invocation["noAutomaticRebukeOrProtectionOrBond"])
        self.assertEqual([],invocation["automaticInvocationGrants"])
        entries={x["name"]:x for x in invocation["eligibleSpecializations"]}
        self.assertEqual({"Rebuke of the Talisman","Protection of the Talisman","Bond of the Talisman"},set(entries))
        self.assertIsNone(entries["Rebuke of the Talisman"]["classLevelPrerequisite"])
        self.assertEqual(7,entries["Protection of the Talisman"]["classLevelPrerequisite"])
        self.assertEqual(12,entries["Bond of the Talisman"]["classLevelPrerequisite"])
        self.assertEqual("own-published-proficiency-bonus-per-long-rest-pool",
                         entries["Protection of the Talisman"]["resource"])
        self.assertEqual("own-published-proficiency-bonus-per-long-rest-pool",
                         entries["Bond of the Talisman"]["resource"])
        self.assertEqual("invocation-selection",entries["Bond of the Talisman"]["cost"])
        self.assertTrue(invocation["subjectToPatronAndPactRestrictions"])
        self.assertFalse(native["implementationNotes"]["runtimeImplemented"])
        self.assertTrue(native["implementationNotes"]["starChainAndSeekerStillDisabled"])
        self.assertEqual("pending-native-reconciliation",self.base["baselineStatus"])

if __name__=="__main__":
    unittest.main()
