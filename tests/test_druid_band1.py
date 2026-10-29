"""Band 1 Druid reconciliations, Wild Shape HP house rule, and companion persistence."""
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/"data/rulesets/first-party-001/working/classes/base/druid.json"
FEATURES=ROOT/"data/rulesets/first-party-001/working/features/classes/druid"
MANIFEST=ROOT/"data/reference/srd/native-coverage-manifest.json"

def load(path):
    return json.loads(path.read_text(encoding="utf-8"))

class DruidBand1Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.obj=load(BASE)
        cls.rule=cls.obj["definition"]
        cls.rows=cls.rule["progression"]
        cls.features={v["id"]:v for v in (load(p) for p in FEATURES.glob("*.json"))}
        cls.inline={v["id"]:v for v in cls.rule["inlineFeatures"]}

    def mech(self,key):
        return self.features["class-feature.druid."+key]["definition"]["mechanics"]

    def test_base_class_grants_are_real_and_exclude_circles(self):
        self.assertEqual("class.druid",self.obj["id"])
        self.assertEqual("retained-native-definition",self.obj["baselineStatus"])
        self.assertNotIn("candidateDefinitions",self.obj)
        self.assertEqual([1,20],self.rule["levelRange"])
        self.assertEqual(list(range(1,21)),[r["level"] for r in self.rows])
        ids=[i for r in self.rows for i in r["featureGrants"]]
        self.assertEqual(len(ids),len(set(ids)))
        self.assertFalse(any("circle" in i or "subclass" in i or "epic-boon" in i for i in ids))
        for id_ in ids:
            self.assertIn(id_,set(self.features)|set(self.inline))
            if id_ in self.features:
                self.assertEqual("retained-native-definition",self.features[id_]["baselineStatus"])
                self.assertNotIn("candidateDefinitions",self.features[id_])
        self.assertEqual(["class-feature.druid.ability-score-improvement-level-19"],
                         self.rows[18]["featureGrants"])

    def test_wild_shape_uses_separate_hp_with_no_overflow(self):
        hp=self.mech("wild-shape-level-2")["hp"]
        self.assertEqual("separate-beast-hit-point-pool",hp["mode"])
        self.assertEqual(0,hp["overflowDamageToNormalHp"])
        self.assertEqual(0,hp["tempHpFromWildShape"])
        self.assertEqual("revert",hp["atFormZeroHp"])
        self.assertEqual("unchanged-while-in-form",hp["normalHitPoints"])
        self.assertIn("house-rule",hp["source"])
        self.assertEqual("independent-form-hp-pool-with-no-spillover",self.rule["wildShapeHp"])

    def test_normal_creature_size_and_form_eligibility(self):
        forms=self.mech("wild-shape-level-2")["forms"]
        self.assertTrue(forms["sizeAlwaysChecked"])
        self.assertTrue(forms["sourcePermissionsNotStackedAsIncompatibleGates"])
        self.assertIn("OR",forms["eligibility"])
        self.assertEqual([[2,.25],[4,.5],[8,1]],forms["modern"]["maximumCRByLevel"])
        self.assertEqual([[2,4],[4,6],[8,8]],forms["modern"]["knownFormsByLevel"])
        self.assertEqual(8,forms["modern"]["flyingFormsFromLevel"])
        self.assertEqual(2,forms["modern"]["swimmingFormsFromLevel"])
        legacy=forms["historical"]["progression"]
        self.assertEqual([5,8,11,12,15,16,18,20],[x["level"] for x in legacy])
        self.assertIn("large",legacy[1]["addSizes"])
        self.assertIn("tiny",legacy[2]["addSizes"])
        self.assertIn("plant-creature",legacy[3]["types"])
        self.assertIn("huge",legacy[4]["addSizes"])
        self.assertIn("elemental",legacy[5]["types"])
        self.assertFalse(forms["historical"]["noModernKnownFormLimit"] is False)

    def test_persistent_animal_companion_levels_and_replacement(self):
        comp=self.inline["druid.animal-companion"]["mechanics"]
        self.assertTrue(comp["lastsAcrossRestsAndEncounters"])
        self.assertEqual(1,comp["maximumPersistentCompanions"])
        self.assertEqual(24,comp["replacementCeremonyHours"])
        self.assertEqual([0,2,4,6,8,10,12],[x["bonusHitDice"] for x in comp["progression"]])
        self.assertEqual([0,2,4,6,8,10,12],[x["naturalArmorBonus"] for x in comp["progression"]])
        self.assertEqual([0,1,2,3,4,5,6],[x["strDexBonus"] for x in comp["progression"]])
        self.assertEqual([1,2,3,4,5,6,7],[x["bonusTricks"] for x in comp["progression"]])
        self.assertEqual([4,7,10,13,16],
                         [x["availableAtDruidLevel"] for x in comp["alternativeCompanions"]])
        self.assertEqual([3,6,9,12,15],
                         [x["effectiveCompanionLevelPenalty"] for x in comp["alternativeCompanions"]])
        self.assertIn("link",comp["abilities"])
        self.assertIn("improvedEvasion" if "improvedEvasion" in comp["abilities"] else "improvedEvasion",comp["abilities"])

    def test_temporary_wild_companion_coexists_without_extra_actions(self):
        temporary=self.mech("wild-companion-level-2")
        self.assertTrue(temporary["bothMayCoexist"])
        self.assertEqual("finish-long-rest",temporary["expires"])
        self.assertEqual("native-controlled-creature-initiative-and-actions",temporary["control"])
        self.assertTrue(temporary["unlimitedArchdruidTransformationsCannotPayCost"])
        self.assertEqual("druid.animal-companion",temporary["distinctFrom"])

    def test_resource_exchange_and_archdruid_bound(self):
        shape=self.mech("wild-shape-level-2")
        self.assertEqual([[2,2],[6,3],[18,4]],shape["finiteUsesByLevel"])
        self.assertEqual(1,shape["shortRestRestore"])
        self.assertEqual("all",shape["longRestRestore"])
        self.assertEqual(3,self.rows[19]["elementalFormsPerLongRest"])
        self.assertEqual(4,self.rows[19]["finiteWildShapeCharges"])
        self.assertTrue(self.rows[19]["unlimitedTransformationPermission"])
        historical=self.inline["druid.elemental-wild-shape"]["mechanics"]
        self.assertEqual([[16,1],[18,2],[20,3]],historical["separateElementalQuotaByLevel"])
        self.assertTrue(historical["notUnlimitedElementalTransformationsAtLevel20"])
        arch=self.mech("archdruid-level-20")
        self.assertTrue(arch["unlimitedWildShapeTransformations"])
        self.assertTrue(arch["unlimitedTransformationsCannotPayCosts"])
        self.assertEqual(4,arch["finiteTrackedChargePoolStillMaximum"])
        self.assertTrue(arch["natureMagician"]["consumeFiniteCharges"])
        self.assertEqual(8,arch["natureMagician"]["maximumSlotLevel"])
        resurgence=self.mech("wild-resurgence-level-5")
        self.assertTrue(resurgence["unlimitedTransformationsNeverCountAsConvertibleFiniteCharges"])

    def test_primal_order_fury_and_nonmetal_rule(self):
        grants={f for r in self.rows for f in r["featureGrants"]}
        choices=set(self.rule["choiceOnlyFeatureIds"])
        self.assertEqual(4,len(choices))
        self.assertFalse(grants&choices)
        self.assertEqual(1,self.mech("primal-order-level-1")["choose"])
        self.assertEqual(1,self.mech("elemental-fury-level-7")["choose"])
        self.assertTrue(self.mech("improved-elemental-fury-level-15")["onlySelectedOptionImproves"])
        self.assertFalse(self.rule["metalArmorRestrictions"]["mechanicalLossOfMagic"])
        self.assertFalse(self.rule["metalArmorRestrictions"]["mandatoryNonmetalOnly"])
        self.assertIsNone(self.rule["alignmentRestriction"])
        self.assertIsNone(self.rule["permanentAbilityScoreMaximum"])
        self.assertEqual(21,self.rule["epicBoonMinimumTotalCharacterLevel"])

    def test_source_only_subclasses_and_manifest(self):
        allGranted={f for row in self.rows for f in row["featureGrants"]}
        allGranted.update(self.rule["choiceOnlyFeatureIds"])
        sourceOnly=[v for k,v in self.features.items() if k not in allGranted]
        self.assertEqual(9,len(sourceOnly))
        for record in sourceOnly:
            self.assertEqual("pending-native-reconciliation",record["baselineStatus"])
            self.assertIn("candidateDefinitions",record)
        coverage={r["path"]:r for r in load(MANIFEST)["coverage"] if "path" in r}
        for path in [BASE,*FEATURES.glob("*.json")]:
            item=load(path)
            row=coverage[path.relative_to(ROOT).as_posix()]
            if item["baselineStatus"]=="retained-native-definition":
                self.assertIn(row["status"],{"native-compatible-merged","native-single-source-retained"})
            else:
                self.assertIn(row["status"],{"single-candidate","multiple-candidates"})

if __name__=="__main__":
    unittest.main()
