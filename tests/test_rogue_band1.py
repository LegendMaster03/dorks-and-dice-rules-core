"""Regression checks for approved Dorks & Dice Rogue Band 1 native rules."""
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/"data/native/classes/base/rogue.json"
FEATURES=ROOT/"data/native/features/classes/rogue"
MANIFEST=ROOT/"data/reference/srd/native-coverage-manifest.json"

def load(path):
    return json.loads(path.read_text(encoding="utf-8"))

class RogueBand1Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.native=load(BASE)
        cls.rule=cls.native["definition"]
        cls.rows=cls.rule["progression"]
        cls.inline={r["id"]:r for r in cls.rule["inlineFeatures"]}
        cls.features={r["id"]:r for r in (load(p) for p in FEATURES.glob("*.json"))}

    def m(self,name):
        return self.features[f"class-feature.rogue.{name}"]["definition"]["mechanics"]

    def test_one_approved_base_class_with_twenty_levels(self):
        self.assertEqual("class.rogue",self.native["id"])
        self.assertEqual("retained-native-definition",self.native["baselineStatus"])
        self.assertNotIn("candidateDefinitions",self.native)
        self.assertEqual(1,self.rule["band"])
        self.assertEqual([1,20],self.rule["levelRange"])
        self.assertEqual(list(range(1,21)),[row["level"] for row in self.rows])
        self.assertEqual(8,self.rule["hitDie"]["faces"])
        self.assertEqual([3,9,13,17],self.rule["subclassFeatureLevels"])
        self.assertEqual(21,self.rule["epicBoonMinimumTotalCharacterLevel"])
        self.assertIsNone(self.rule["permanentAbilityScoreMaximum"])

    def test_all_granted_features_are_native_and_only_granted_once(self):
        grants=[id_ for row in self.rows for id_ in row["featureGrants"]]
        self.assertEqual(len(grants),len(set(grants)))
        for grant in grants:
            if grant in self.inline:
                continue
            self.assertIn(grant,self.features)
            self.assertEqual("retained-native-definition",self.features[grant]["baselineStatus"])
            self.assertNotIn("candidateDefinitions",self.features[grant])
        self.assertFalse(any("subclass" in f or "archetype" in f or "epic-boon" in f
                             for f in grants))
        self.assertFalse(set(self.rule["choiceOnlyFeatureIds"]) & set(grants))
        self.assertEqual(6,len(self.rule["specialAbilityOptionIds"]))
        for id_ in self.rule["choiceOnlyFeatureIds"]:
            self.assertIn(id_,set(self.inline)|set(self.features))

    def test_per_attack_sneak_attack_and_cunning_strike(self):
        sneak=self.m("sneak-attack-level-1")
        self.assertEqual("each-independently-qualifying-successful-hit",sneak["frequency"])
        self.assertTrue(sneak["requiresWeaponProficiency"])
        self.assertFalse(sneak["finesseOrRangedNotRequired"] is False)
        self.assertTrue(sneak["advantageAloneInsufficient"])
        self.assertFalse(sneak["noExtraAttacksGranted"] is False)
        self.assertEqual("10d6",self.rows[19]["sneakAttackDice"])
        self.assertEqual([f"{i}d6" for i in range(1,11)],
                         [self.rows[i]["sneakAttackDice"] for i in range(0,20,2)])
        cunning=self.m("cunning-strike-level-5")
        self.assertTrue(cunning["appliesToEachQualifyingHit"])
        self.assertTrue(cunning["perHitSeparateCost"])
        self.assertEqual(2,self.m("improved-cunning-strike-level-11")["maximumEffectsPerHit"])
        self.assertEqual(6,len(self.rule["cunningStrikeOptionIds"]))
        self.assertEqual(6,self.m("knock-out-cost-6d6-level-14")["dieCost"])
        self.assertTrue(self.m("steady-aim-level-3")["doesNotCreateSneakAttackEligibility"])

    def test_historical_traps_defenses_and_options(self):
        self.assertEqual([10,13,16,19],self.rule["specialAbilityChoiceLevels"])
        self.assertEqual([1,2,3,4,5,6],
                         [self.rows[n-1]["trapSense"] for n in [3,6,9,12,15,18]])
        self.assertTrue(self.inline["rogue.trapfinding"]["mechanics"]
                        ["otherClassesStillCanSearchAndDisable"])
        evasion=self.inline["rogue.evasion"]["mechanics"]
        self.assertEqual(2,evasion["level"])
        self.assertTrue(evasion["absorbsHistoricalImprovedEvasion"])
        self.assertTrue(self.inline["rogue.uncanny-awareness"]["mechanics"]
                        ["improvementKeepsSameName"])
        self.assertEqual(2,self.inline["rogue.option.crippling-strike"]
                         ["mechanics"]["damagePerQualifyingSneakAttackHit"])
        self.assertEqual(2,self.inline["rogue.option.skill-mastery"]["mechanics"]["choices"])
        self.assertEqual("reaction",self.inline["rogue.option.opportunist"]
                         ["mechanics"]["cost"])
        self.assertEqual([15,10,5],self.rows[-1]["iterativeBaseAttackBonuses"])
        self.assertEqual({"fortitude":6,"reflex":12,"will":6},self.rows[-1]["baseSaves"])

    def test_defensive_roll_and_rest_based_recovery(self):
        defense=self.inline["rogue.option.defensive-roll"]["mechanics"]
        self.assertEqual("reaction",defense["cost"])
        self.assertEqual(1,defense["uses"])
        self.assertEqual("long-rest",defense["refresh"])
        self.assertEqual(1,defense["onSuccessHP"])
        self.assertEqual("dexterity",defense["save"])
        self.assertIsNone(defense["saveDC"])
        self.assertFalse(defense["executable"])
        self.assertTrue(self.m("uncanny-dodge-level-5")
                        ["competesWithDefensiveRollReaction"])
        self.assertTrue(self.m("stroke-of-luck-level-20")
                        ["noOptionalCriticalEscalation"])

    def test_approved_base_only_and_manifest(self):
        grants={f for row in self.rows for f in row["featureGrants"]}
        selected=grants | set(self.rule["choiceOnlyFeatureIds"])
        coverage={r["path"]:r for r in load(MANIFEST)["coverage"] if "path" in r}
        for path in [BASE,*FEATURES.glob("*.json")]:
            item=load(path)
            if item["id"] in selected or item["id"]=="class.rogue":
                self.assertEqual("retained-native-definition",item["baselineStatus"])
                self.assertNotIn("candidateDefinitions",item)
                self.assertIn(coverage[path.relative_to(ROOT).as_posix()]["status"],
                              {"native-compatible-merged","native-single-source-retained"})
            else:
                self.assertEqual("pending-native-reconciliation",item["baselineStatus"])
                self.assertIn("candidateDefinitions",item)
                self.assertIn(coverage[path.relative_to(ROOT).as_posix()]["status"],
                              {"multiple-candidates","single-candidate"})

if __name__=="__main__":
    unittest.main()
