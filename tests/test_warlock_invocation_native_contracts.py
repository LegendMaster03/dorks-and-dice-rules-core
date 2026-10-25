"""Data-contract regression tests for owner-approved Pact-specific Invocation decisions.

The rewrite has no gameplay runtime. These tests validate inert native contracts,
source preservation and intended behavior; they do not certify a playable engine.
"""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
OPT = ROOT / "data/native/features/optional"

def read(path):
    return json.loads(path.read_text(encoding="utf-8"))

def invocation(slug):
    return read(OPT / (slug + ".json"))["nativeInvocationDefinition"]

class NativeWarlockInvocationContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.inventory = read(ROOT / "data/reference/warlock-pact-specific-invocations.json")
        cls.progress = read(ROOT / "data/reference/warlock-invocation-native-conversion-status.json")
        cls.chain = read(OPT / "pact-of-the-chain.json")["nativeFamiliarCommandDefinition"]

    def test_all_sixteen_are_single_pact_contracts_not_runtime(self):
        self.assertEqual(16, len(self.progress["files"]))
        self.assertEqual(15, self.progress["count"]["selectable"])
        self.assertEqual(7, self.progress["count"]["nonSrdRightsReviewPending"])
        self.assertFalse(self.progress["runtimeImplemented"])
        self.assertFalse(self.progress["invocationProgressionApproved"])
        entries = {r["invocationId"]: r
                   for r in self.progress["files"]}
        self.assertEqual(16, len(entries))
        for item in self.inventory["entries"]:
            slug = item["id"].split(".", 1)[1]
            contract = invocation(slug)
            with self.subTest(slug=slug):
                self.assertEqual(item["requiredPactType"], contract["requiredPactType"])
                self.assertFalse(contract["nativeExecutable"])
                self.assertEqual("undecided", contract["invocationProgressionSchedule"])
                self.assertTrue(contract["originalSourceVersionsUnaffected"])
                if slug == "thirsting-blade":
                    self.assertFalse(contract["selectable"])
                    self.assertEqual(0, contract["invocationSelectionCost"])
                else:
                    self.assertTrue(contract["selectable"])
                    self.assertEqual(1, contract["invocationSelectionCost"])
                    owner = contract["ownership"]
                    self.assertTrue(owner["bindsToExactlyOneConcretePact"])
                    self.assertTrue(owner["patronMustMatchAttachedPact"])
                    self.assertTrue(owner["patronAcceptanceRequired"])
                    self.assertTrue(owner["pactMustBeActiveForEffects"])
                    self.assertTrue(owner["retainPurchaseHistoryOnPactSeverance"])
                    self.assertTrue(owner["revalidateBeforeRestoringOrTransferring"])
                    self.assertTrue(owner["noAutomaticRefundOrFreeGrant"])
                if item["existingNativeFile"]:
                    native = read(ROOT / item["existingNativeFile"])
                    self.assertTrue(native["candidateDefinitions"])
                    self.assertEqual(item["id"], native["pactInvocationCodification"]["invocationRef"])
                else:
                    self.assertEqual("non-srd-review-pending", contract["sourceRightsReview"])
                    self.assertFalse(read(OPT / (slug + ".json"))["publishedSourceBoundary"]["fullProtectedTextImported"])

    def test_blade_attack_counts_and_requirements(self):
        two = invocation("thirsting-blade")
        three = invocation("devouring-blade")
        self.assertEqual("historical-alias", two["effect"]["kind"])
        self.assertFalse(two["effect"]["selectableInvocationEffect"])
        self.assertEqual(12, three["minimumWarlockLevel"])
        self.assertEqual(3, three["effect"]["totalAttacks"])
        self.assertIn("level-5", three["effect"]["requiresPactMilestone"])
        self.assertEqual("highest-compatible-total-not-additive", three["effect"]["stacking"])
        self.assertEqual(9, invocation("lifedrinker")["minimumWarlockLevel"])
        life = invocation("lifedrinker")["effect"]
        self.assertEqual(1, life["damage"]["usesPerTurn"])
        self.assertEqual("1d6", life["damage"]["dice"])
        self.assertFalse(life["retains2014PerHitCharismaDamage"])
        self.assertEqual("hit-die", life["optionalHealing"]["cost"]["resource"])
        smite = invocation("eldritch-smite")["effect"]
        self.assertEqual("pact-magic-spell-slot", smite["cost"]["resource"])
        self.assertEqual("1+expended-slot-level", smite["extraDamage"]["diceCount"])
        self.assertEqual(1, smite["usesPerTurn"])
        improved = invocation("improved-pact-weapon")["effect"]
        self.assertTrue(improved["weaponEnhancement"]["neverStacksWithExistingEnhancement"])
        self.assertEqual(4, len(improved["additionalConjurableForms"]))
        self.assertFalse(improved["allowsOrdinaryRangedWeaponBonding"])
        self.assertFalse(improved["duplicatesBladeSpellcastingFocus"])

    def test_chain_command_paths_share_one_familiar_action(self):
        c = self.chain
        self.assertFalse(c["nativeExecutable"])
        self.assertEqual(1, c["baseCommand"]["warlockAttackSacrifice"])
        self.assertEqual("Attack", c["baseCommand"]["warlockAction"])
        self.assertEqual("Bonus Action", c["investmentAlternative"]["warlockAction"])
        self.assertEqual(1, c["commandedFamiliarAttackActionsPerSharedInitiativeRound"])
        self.assertTrue(c["cannotUseBothCommandPathsForExtraAction"])
        self.assertTrue(c["investmentAlternative"]["preservesWarlockRegularAction"])
        self.assertTrue(c["otherControlledSummonsUnaffected"])
        a = c["attackRollsPerFamiliarAttackAction"]
        for level in (1, 4, 5, 12):
            count = a["fromWarlockLevel5"] if level >= 5 else a["beforeWarlockLevel5"]
            self.assertEqual(2 if level >= 5 else 1, count)
        invest = invocation("investment-of-the-chain-master")["effect"]
        self.assertTrue(invest["bonusActionCommand"]["usesSameCommandedActionAllowance"])
        self.assertFalse(invest["grantsExtraFamiliarAttackAction"])
        self.assertEqual(40, invest["flyOrSwimSpeedFeet"])
        voice = invocation("voice-of-the-chain-master")["effect"]
        self.assertEqual("same-plane", voice["planeRestriction"])
        carceri = invocation("chains-of-carceri")["effect"]
        self.assertEqual("target-identity", carceri["lockout"]["key"])
        self.assertIn("including-failure", carceri["lockout"]["begins"])
        self.assertFalse(carceri["globalOncePerLongRestLimit"])
        everliving = invocation("gift-of-the-ever-living-ones")["effect"]
        self.assertIn("flat-healing", everliving["excludes"])
        self.assertEqual(100, everliving["requiresAttachedPactFamiliarWithinFeet"])

    def test_tome_rituals_and_two_independent_name_lists(self):
        book = invocation("book-of-ancient-secrets")["effect"]
        self.assertEqual(0, book["additionalFreeStartingRituals"])
        self.assertEqual("ceil(warlock-level/2)", book["maxRitualSpellLevel"])
        self.assertEqual(2, book["hoursPerSpellLevel"])
        self.assertEqual(50, book["goldPiecesPerSpellLevel"])
        self.assertTrue(book["paidTranscriptionsPersistAcrossBookReplacement"])
        rescue = invocation("gift-of-the-protectors")["effect"]
        self.assertEqual("max(1,charisma-modifier)", rescue["nameCapacity"])
        self.assertEqual(1, rescue["sharedUsesPerLongRest"])
        self.assertEqual(1, rescue["resultHitPoints"])
        scribe = invocation("far-scribe")["effect"]
        self.assertEqual("warlock-proficiency-bonus", scribe["nameCapacity"])
        self.assertEqual("gift-of-the-protectors", scribe["independentNameListFrom"])
        self.assertEqual(0, scribe["pactMagicSlotCost"])
        moon = invocation("aspect-of-the-moon")["effect"]
        self.assertTrue(moon["longRestStillRequired"])
        self.assertFalse(moon["grantsAdditionalLongRests"])

    def test_talisman_reaction_and_resources_are_separate(self):
        rebuke = invocation("rebuke-of-the-talisman")["effect"]
        self.assertEqual("warlock-reaction", rebuke["cost"]["resource"])
        self.assertEqual(0, rebuke["intrinsicTalismanChargeCost"])
        self.assertIn("shared-ward", rebuke["mutuallyExclusiveWith"])
        protect = invocation("protection-of-the-talisman")["effect"]
        bond = invocation("bond-of-the-talisman")["effect"]
        self.assertEqual("1d4", protect["bonusDice"])
        self.assertEqual("warlock-proficiency-bonus", protect["resource"]["maximum"])
        self.assertEqual("warlock-proficiency-bonus", bond["resource"]["maximum"])
        self.assertNotEqual(protect["resource"]["id"], bond["resource"]["id"])
        self.assertTrue(bond["resource"]["oneSharedPoolBothDirections"])
        self.assertEqual(2, len(bond["directionChoices"]))
        self.assertEqual("same-plane", bond["planeRestriction"])
        self.assertIn("talisman.base-charges", protect["resource"]["separateFrom"])
        self.assertIn("talisman.base-charges", bond["resource"]["separateFrom"])

if __name__ == "__main__":
    unittest.main()
