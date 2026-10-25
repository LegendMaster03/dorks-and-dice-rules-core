"""Verify Patron pillar's Fiend source conversions without declaring native gameplay reconciled."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]

def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))

class WarlockFiendPatronCrosswalkTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.crosswalk = load("data/reference/srd/warlock-fiend-patron-crosswalk.json")
        cls.source = load("data/reference/srd/structured/class/class-warlock.json")
        cls.native = load("data/native/classes/subclasses/warlock/fiend.json")

    def test_pillar_identity_and_no_premature_approval(self):
        x = self.crosswalk
        self.assertEqual("Patron", x["pillar"])
        self.assertEqual("source-mapped-native-mechanics-not-yet-reconciled", x["status"])
        self.assertEqual(1, x["authoritativePatronIdentity"]["classLevel"])
        self.assertEqual("approved-native-design-direction", x["authoritativePatronIdentity"]["status"])
        self.assertEqual("pending-native-reconciliation", self.native["baselineStatus"])
        self.assertEqual(2, len(self.native["candidateDefinitions"]))
        self.assertEqual(5, len(x["parts"]))
        self.assertEqual(5, len({p["id"] for p in x["parts"]}))
        self.assertTrue(all(x["authoritativePatronIdentity"]["id"] in p["dependencies"] for p in x["parts"]))
        self.assertFalse(any(p["id"] == x["authoritativePatronIdentity"]["id"] for p in x["parts"]))

    def test_complete_source_feature_coverage(self):
        for edition in ("SRD51", "SRD52"):
            declared = set(self.crosswalk["sourceFeatureCoverage"][edition])
            actual = {f["name"] for f in self.source["subclassFeature"] if f["source"] == edition}
            self.assertEqual(actual, declared)
            self.assertEqual(len(actual), len(self.crosswalk["sourceFeatureCoverage"][edition]))

    def test_historical_presets_share_parts_and_dont_double_grant(self):
        x = self.crosswalk
        ids = {p["id"] for p in x["parts"]}
        expected = {"SRD51":[1,6,10,14],"SRD52":[3,6,10,14]}
        for edition in ("SRD51","SRD52"):
            preset = x["sourcePresets"][edition]
            source = next(s for s in self.source["subclass"] if s["source"] == edition)
            self.assertEqual(source["name"], preset["sourceSubclassName"])
            self.assertEqual(source["subclassFeatures"][0].rsplit("|", 1)[-1], str(preset["sourceSubclassChoiceLevel"]))
            self.assertEqual(1, preset["nativeIdentityChoiceLevel"])
            self.assertEqual(expected[edition], [m["level"] for m in preset["milestones"]])
            granted = [p for row in preset["milestones"] for p in row["parts"]]
            self.assertEqual(ids, set(granted))
            self.assertEqual(len(granted), len(set(granted)))
            self.assertEqual([x["parts"][0]["id"],x["parts"][1]["id"]], preset["milestones"][0]["parts"])

    def test_spell_list_and_source_semantics(self):
        m = self.crosswalk["sourceSpellAccess"]
        for edition in ("SRD51","SRD52"):
            s = next(s for s in self.source["subclass"] if s["source"] == edition)
            spell_info = s["additionalSpells"][0]
            if edition=="SRD51":
                actual = {key[1:]: [x.lower() for x in val] for key,val in spell_info["expanded"].items()}
                self.assertEqual(actual,m[edition]["spellLevelGroups"])
                self.assertEqual("expanded-choices-not-automatic",m[edition]["grantMode"])
            else:
                actual={str(key):[v.split("|")[0].lower() for v in values] for key,values in spell_info["prepared"].items()}
                self.assertEqual(actual,m[edition]["classLevelGroups"])
                self.assertTrue("no-preparation-reconciliation" in m[edition]["grantMode"])
        names51={v for vals in m["SRD51"]["spellLevelGroups"].values() for v in vals}
        names52={v for vals in m["SRD52"]["classLevelGroups"].values() for v in vals}
        self.assertEqual(10,len(names51))
        self.assertEqual(10,len(names52))
        self.assertEqual(13,len(names51 | names52))
        self.assertEqual(7,len(names51 & names52))

    def test_each_part_is_source_backed_without_spoofing_class_resilience(self):
        source_names={s:{x["name"] for x in self.source["subclassFeature"] if x["source"] == s} for s in ("SRD51","SRD52")}
        parts=self.crosswalk["parts"]
        self.assertEqual("warlock.patron.fiend.damage-resilience",parts[3]["id"])
        self.assertNotEqual(parts[3]["id"],"class-feature.warlock.fiendish-resilience")
        for part in parts:
            for s in ("SRD51","SRD52"):
                assert_names=[n for n in part["sourceNames"][s] if n not in {"Expanded Spell List"}]
                self.assertTrue(set(assert_names) <= source_names[s],part["id"])

    def test_decisions_left_open_not_hidden_as_native(self):
        decision=self.crosswalk["openNativeDecisions"]
        self.assertEqual(8,len(decision))
        self.assertEqual(8,len({d["id"] for d in decision}))
        statuses={k:sum(d["status"]==k for d in decision) for k in {"owner-ruling-needed","deferred-outside-srd","owner-approved-design-direction","partially-owner-approved","reopened-owner-review"}}
        self.assertEqual({"owner-ruling-needed":2,"deferred-outside-srd":1,"owner-approved-design-direction":5,"partially-owner-approved":0,"reopened-owner-review":0},statuses)
        initial=next(d for d in decision if d["id"]=="patron-initial-mechanical-grant")
        self.assertIn("level 1",initial["resolution"])
        self.assertEqual(1,self.crosswalk["approvedNativeDirections"]["patronInitialFiendMilestone"]["classLevel"])
        self.assertEqual(["warlock.patron.fiend.spell-access","warlock.patron.fiend.dark-ones-blessing"],self.crosswalk["approvedNativeDirections"]["patronInitialFiendMilestone"]["partIds"])
        luck=self.crosswalk["approvedNativeDirections"]["luckUses"]
        self.assertEqual("max(1, charisma_modifier)",luck["usesExpression"])
        self.assertEqual("long-rest",luck["reset"])
        approved=self.crosswalk["approvedNativeDirections"]
        self.assertEqual("automatic-warlock-base-class-feature",approved["baseEldritchBlast"]["grantMode"])
        self.assertEqual(1,approved["baseEldritchBlast"]["classLevel"])
        self.assertFalse(approved["baseEldritchBlast"]["requiresCantripSelection"])
        self.assertEqual(1,approved["darkOnesBlessing"]["classLevel"])
        self.assertEqual(2,len(approved["darkOnesBlessing"]["triggers"]))
        self.assertEqual("max(1, charisma_modifier + warlock_level)",approved["darkOnesBlessing"]["temporaryHitPoints"])
        self.assertEqual(14,approved["hurlThroughHell"]["classLevel"])
        self.assertTrue(approved["hurlThroughHell"]["spentOnAttempt"])
        self.assertEqual("8d10 psychic immediately",approved["hurlThroughHell"]["onFailedSave"]["nonFiendDamage"])
        resolutions={d["id"]:d for d in decision}
        for key in ("patron-fiend-blessing-trigger","patron-fiend-luck-recovery","patron-fiend-hurl-through-hell"):
            self.assertEqual("owner-approved-design-direction",resolutions[key]["status"])
        for key in ("patron-fiend-spell-entitlement","patron-fiend-damage-resilience"):
            self.assertEqual("owner-ruling-needed",resolutions[key]["status"])
        ledger=load("data/reference/srd/class-reconciliation-decisions.json")["decisions"]
        review=next(v["reviewClarification"] for v in ledger if v["nativeRuleId"]=="class.warlock.historical-class-features-band1")
        self.assertEqual("resolved-owner-approved-fiend-patron-infernal-retaliation",review["status"])
        self.assertEqual(4,len(review["unaffectedApprovedFeatureFamilies"]))
        retaliation=approved["infernalRetaliation"]
        self.assertEqual("warlock.patron.fiend.identity",retaliation["patronId"])
        self.assertEqual("one-fiend-patron-feature",retaliation["grantedAs"])
        self.assertFalse(retaliation["effectsAreSeparateEntitlements"])
        self.assertFalse(retaliation["automaticForAllWarlocks"])
        self.assertEqual("on-activation",retaliation["choiceTiming"])
        self.assertEqual({"fiendish-resilience-fast-healing","fiendish-flamewreath"},set(retaliation["availableEffects"]))
        self.assertEqual({"uses":1,"recovery":"long-rest","consumesOnAnyEffect":True},retaliation["sharedResource"])
        self.assertEqual("reaction",retaliation["activation"]["actionType"])
        self.assertEqual("one-reaction",retaliation["activation"]["actionCost"])
        self.assertEqual("player-chosen-when-reaction-available",retaliation["activation"]["actionTiming"])
        self.assertFalse(retaliation["activation"]["requiresExternalTrigger"])
        self.assertEqual(set(retaliation["availableEffects"]),set(retaliation["activation"]["modes"]))
        self.assertTrue(all(mode["actionType"]=="reaction" and mode["requiresExternalTrigger"] is False
                            and mode["activation"]=="player-choice" for mode in retaliation["activation"]["modes"].values()))
        provenance=retaliation["historicalEffectScaling"]
        self.assertEqual([8,13,18],provenance["fiendishResilience"]["sourceWarlockLevels"])
        self.assertEqual([1,2,5],provenance["fiendishResilience"]["fastHealingPerRound"])
        self.assertEqual(["1d6","2d6","5d6"],provenance["fiendishFlamewreath"]["retaliationFireDamage"])
        self.assertEqual("Player's Handbook II",provenance["fiendishFlamewreath"]["source"])
        self.assertNotIn("hellfireShield",provenance)
        self.assertEqual(20,retaliation["effectRules"]["fiendishResilience"]["durationRounds"])
        self.assertEqual(20,retaliation["effectRules"]["fiendishFlamewreath"]["durationRounds"])
        at_zero=retaliation["effectRules"]["fiendishResilience"]["atZeroHitPoints"]
        self.assertEqual("stabilize-once",at_zero["whenReducedToZeroWhileActive"])
        self.assertTrue(at_zero["remainsUnconscious"])
        self.assertFalse(at_zero["healingWhileAtZero"])
        self.assertFalse(at_zero["restoresConsciousnessByOngoingHealing"])
        self.assertFalse(at_zero["repeatedAutomaticStabilization"])
        self.assertEqual("normal-damage-and-death-saving-throw-rules",at_zero["subsequentDamageAtZero"])
        native=retaliation["nativeProgression"]
        self.assertEqual("owner-approved-design-direction",native["status"])
        self.assertEqual(8,native["grantClassLevel"])
        self.assertEqual([13,18],native["upgradeClassLevels"])
        self.assertTrue(native["doesNotGrantAdditionalResourceUsesOnUpgrade"])
        self.assertEqual([8,13,18],[m["warlockClassLevel"] for m in native["milestones"]])
        self.assertEqual([1,2,5],[m["fiendishResilienceFastHealingPerRound"] for m in native["milestones"]])
        self.assertEqual(["1d6 fire","2d6 fire","5d6 fire"],[m["fiendishFlamewreathRetaliationDamage"] for m in native["milestones"]])
        self.assertTrue(all("hellfireShieldDamage" not in m for m in native["milestones"]))
        self.assertEqual("one-fiend-patron-feature-with-two-mode-options",native["sourceGrant"])
        specialization=approved["hellfireSpecialization"]
        self.assertEqual("optional-hellfire-specialization-not-infernal-retaliation",specialization["disposition"])
        shield=specialization["shield"]
        self.assertFalse(shield["automaticallyGrantedAtFiendLevel8"])
        self.assertIsNone(shield["requiresSeparateAcquisition"])
        self.assertEqual("three-permitted-candidate-routes-no-final-route-selected",shield["acquisitionStatus"])
        self.assertEqual({"automatic-patron-feature","selectable-patron-feature","eldritch-invocation"},
                         {r["route"] for r in shield["acquisitionCandidates"]})
        self.assertTrue(all(r["requiresPatron"]=="fiend" for r in shield["acquisitionCandidates"]))
        self.assertTrue(all(r["status"]=="owner-proposed-option" for r in shield["acquisitionCandidates"]))
        self.assertTrue(next(r for r in shield["acquisitionCandidates"] if r["route"]=="eldritch-invocation")["usesNormalInvocationSelection"])
        self.assertFalse(shield["consumesInfernalRetaliationResource"])
        self.assertEqual("reaction",shield["actionType"])
        self.assertEqual(3,shield["historicalSource"]["sourceHellfireWarlockLevel"])
        derived=shield["nativeDamageDesign"]
        self.assertEqual("derived-not-fixed-tiers",derived["progressionType"])
        self.assertEqual("warlock-current-eldritch-blast-damage",derived["derivesFrom"])
        self.assertFalse(derived["automaticHellfireBlastEntitlement"])
        self.assertFalse(derived["fixedBonusesAtLevels8_13_18"])
        self.assertTrue(derived["noStackedAllBeamAssumption"])
        self.assertEqual("warlock.patron.fiend.infernal-retaliation",review["nativeFeatureId"])
        base=next(v for v in ledger if v["nativeRuleId"]=="class.warlock.historical-class-features-band1")
        self.assertIn("retains 4 historical",base["decision"])
        self.assertNotIn("Fiendish Resilience (levels",base["decision"])
        self.assertIn("Fiendish Resilience (levels",base["priorApproval"]["decision"])
        self.assertNotIn("definition",self.native)

if __name__=="__main__":
    unittest.main()
