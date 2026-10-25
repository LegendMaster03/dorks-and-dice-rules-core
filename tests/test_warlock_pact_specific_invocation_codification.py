"""Guard the source-backed Pact-specific Invocation codification before conflict rulings."""
import json
import unittest
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "data/reference/warlock-pact-specific-invocations.json"

def load(path):
    return json.loads(path.read_text(encoding="utf-8"))

class PactSpecificInvocationCodificationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = load(MANIFEST)
        cls.records = cls.data["entries"]
        cls.by_name = {entry["name"]: entry for entry in cls.records}
        cls.srd = load(ROOT / "data/reference/srd/structured/optionalfeatures.json")["optionalfeature"]
        cls.warlock = load(ROOT / "data/native/classes/base/warlock.json")

    def test_pass_order_preserves_unresolved_budget_and_three_five_work(self):
        self.assertEqual("source-staged-conflicts-not-resolved", self.data["status"])
        self.assertEqual([
            "codify-known-pact-specific-invocations",
            "resolve-mechanical-overlap-and-source-version-conflicts",
            "choose-3.5-invocation-research-or-invocation-progression",
        ], self.data["decisionOrder"])
        self.assertTrue(self.data["authority"]["selectionProgressionUndecided"])
        self.assertTrue(self.data["authority"]["threeFiveImportNotStarted"])
        self.assertTrue(self.data["authority"]["starChainSeekerDisabled"])
        self.assertTrue(self.data["authority"]["firstPactFreeAtWarlockLevel1"])
        self.assertTrue(self.data["authority"]["noExistingIntrinsicPactAbilitiesModified"])
        self.assertTrue(self.data["authority"]["nativeSelectableOptionsNotEnabledByThisCodification"])
        self.assertEqual("pending-native-reconciliation", self.warlock["baselineStatus"])

    def test_all_sixteen_identified_options_and_distribution(self):
        self.assertEqual(16, len(self.records))
        self.assertEqual(16, len(self.by_name))
        self.assertEqual(16, len({entry["id"] for entry in self.records}))
        self.assertEqual({"Blade": 5, "Chain": 4, "Tome": 4, "Talisman": 3},
                         dict(Counter(x["requiredPactType"] for x in self.records)))
        self.assertEqual(9, self.data["counts"]["srdUnique"])
        self.assertEqual(3, self.data["counts"]["talismanPublished"])
        self.assertEqual(4, self.data["counts"]["otherNonSrdPublished"])
        self.assertEqual(16, self.data["counts"]["total"])
        self.assertEqual({
            "Thirsting Blade", "Devouring Blade", "Lifedrinker", "Eldritch Smite",
            "Voice of the Chain Master", "Chains of Carceri", "Investment of the Chain Master",
            "Book of Ancient Secrets", "Gift of the Protectors",
            "Rebuke of the Talisman", "Protection of the Talisman", "Bond of the Talisman",
            "Improved Pact Weapon", "Gift of the Ever-Living Ones",
            "Aspect of the Moon", "Far Scribe",
        }, set(self.by_name))

    def test_every_invocation_attaches_to_one_pact_and_consumes_separate_selection(self):
        for entry in self.records:
            with self.subTest(name=entry["name"]):
                binding = entry["attachment"]
                self.assertTrue(binding["belongsToSpecificPact"])
                self.assertTrue(binding["sourcePatronIsPatronOfThatPact"])
                self.assertTrue(binding["requiresOwnerApprovedInvocationChoice"])
                self.assertFalse(binding["automaticallyGrantedByPact"])
                self.assertEqual("deferred-until-after-codification",
                                 entry["mechanicsReconciliation"])
                self.assertFalse(entry["nativeExecutable"])
                self.assertIn(entry["requiredPactType"], ("Blade", "Chain", "Tome", "Talisman"))

    def test_nine_existing_native_stubs_keep_original_sources_and_stay_unresolved(self):
        actual = 0
        for entry in self.records:
            path = entry["existingNativeFile"]
            if path is None:
                continue
            actual += 1
            native = load(ROOT / path)
            with self.subTest(name=entry["name"]):
                self.assertEqual("pending-native-reconciliation", native["baselineStatus"])
                self.assertNotIn("definition", native)
                self.assertEqual("optionalfeature." + entry["id"].split(".", 1)[1], native["id"])
                codified = native["pactInvocationCodification"]
                self.assertEqual("source-staged-unresolved-not-runtime", codified["status"])
                self.assertEqual(entry["id"], codified["invocationRef"])
                self.assertEqual(entry["requiredPactType"], codified["requiredPactType"])
                self.assertTrue(codified["bindsToOneConcretePact"])
                self.assertTrue(codified["usesSeparateInvocationEntitlement"])
                self.assertTrue(codified["neverGrantedAutomaticallyByPact"])
                self.assertTrue(codified["associatedPatronMustAccept"])
                self.assertEqual(entry["openConflict"], codified["conflictGate"])
                self.assertEqual(len(entry["sourceVariants"]), len(native["candidateDefinitions"]))
                for version in entry["sourceVariants"]:
                    self.assertIn(version["source"], ("SRD51", "SRD52"))
                    original = next(x for x in self.srd if
                                    x["source"] == version["source"] and
                                    x["name"] == entry["name"] and
                                    "EI" in x.get("featureType", []))
                    staged = next(x for x in native["candidateDefinitions"] if
                                  x["key"] == version["sourceCandidateKey"])["content"]
                    self.assertEqual(original["entries"], staged["entries"])
                    self.assertEqual(original.get("prerequisite", []),
                                     version["originalPrerequisites"])
        self.assertEqual(9, actual)

    def test_seven_published_non_srd_leads_are_reference_only(self):
        external = [x for x in self.records if x["existingNativeFile"] is None]
        self.assertEqual(7, len(external))
        for item in external:
            with self.subTest(name=item["name"]):
                self.assertEqual(1, len(item["sourceVariants"]))
                source = item["sourceVariants"][0]
                self.assertIn(source["source"], ("TCOE", "XGTE"))
                self.assertEqual("non-srd-copyrighted-published-reference",
                                 source["licenseCategory"])
                self.assertFalse(source["fullSourceTextImported"])
                self.assertFalse(item["nativeExecutable"])
        talisman = {x["name"] for x in external if x["requiredPactType"] == "Talisman"}
        self.assertEqual({"Rebuke of the Talisman",
                          "Protection of the Talisman",
                          "Bond of the Talisman"}, talisman)
        self.assertEqual(7, self.by_name["Protection of the Talisman"]["sourceVariants"][0]["minimumWarlockLevel"])
        self.assertEqual(12, self.by_name["Bond of the Talisman"]["sourceVariants"][0]["minimumWarlockLevel"])

    def test_conflicts_stay_visible_without_mutating_approved_base(self):
        expected = {
            "Thirsting Blade": "native-level5-second-attack-duplicate",
            "Devouring Blade": "thirsting-blade-prerequisite-becomes-stale",
            "Lifedrinker": "2014-vs-2024-damage-frequency-and-healing",
            "Investment of the Chain Master": "native-familiar-command-economy-vs-intrinsic-second-attack",
            "Book of Ancient Secrets": "2024-tome-two-starter-ritual-overlap",
            "Rebuke of the Talisman": "shared-ward-reaction-competition",
        }
        for name, issue in expected.items():
            self.assertEqual(issue, self.by_name[name]["openConflict"])
        inventory = load(ROOT / "data/reference/srd/warlock-srd-slice-inventory.json")
        p = inventory["approvedNativePactDirection"]
        self.assertEqual({"blade", "chain", "tome", "talisman"},
                         set(p["approvedLevelFivePactImprovements"]["options"]))
        self.assertFalse(p["expandedFivePactReview"]["newSources"]["starChain"]["enabled"])

if __name__ == "__main__":
    unittest.main()
