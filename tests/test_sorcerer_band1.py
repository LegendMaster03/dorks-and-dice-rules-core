"""Sorcerer Band 1 native reconciliation and shared familiar deferral."""
import json
from pathlib import Path
import unittest
R=Path(__file__).resolve().parents[1]
def load(x): return json.loads((R/x).read_text(encoding="utf-8"))
class SorcererBand1Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.obj=load("data/native/classes/base/sorcerer.json")
        cls.rule=cls.obj["definition"]
        cls.rows=cls.rule["progression"]
        cls.feats={p.stem:load(p.relative_to(R)) for p in (R/"data/native/features/classes/sorcerer").glob("*.json")}
    def mech(self,k): return self.feats[k]["definition"]["mechanics"]
    def test_grants_and_band(self):
        self.assertEqual("retained-native-definition",self.obj["baselineStatus"])
        self.assertEqual(list(range(1,21)),[r["level"] for r in self.rows])
        grants=[g for r in self.rows for g in r["featureGrants"]]
        self.assertEqual(18,len(grants))
        self.assertEqual(len(grants),len(set(grants)))
        self.assertTrue(all(g.removeprefix("class-feature.sorcerer.") in self.feats for g in grants))
        self.assertTrue(all(self.feats[g.removeprefix("class-feature.sorcerer.")]["baselineStatus"]=="retained-native-definition" for g in grants))
        self.assertFalse(any("subclass" in g or "epic-boon" in g or "origin" in g for g in grants))
        self.assertEqual([3,6,14,18],[r["level"] for r in self.rows if r["subclassMilestone"]])
    def test_spells_and_resources(self):
        self.assertEqual("none",self.rule["spellPreparation"])
        self.assertEqual("finite-known-by-default",self.rule["spellAccess"])
        self.assertEqual([2,4,6,7,9,10,11,12,14,15,16,16,17,17,18,18,19,20,21,22],self.rule["spellKnownProgression"])
        self.assertEqual(["spell-slots","spell-points"],self.rule["spellResourceModes"])
        self.assertEqual([0]+list(range(2,21)),self.rule["sorceryPointMaximumByLevel"])
        self.assertTrue(self.mech("sorcery-points-level-2")["independentOfCastingSpellPoints"])
        self.assertEqual([2,3,5,6,7],[x["cost"] for x in self.mech("flexible-casting-level-2")["createSlot"]["costs"]])
    def test_metamagic_and_additive_restoration(self):
        self.assertEqual([[2,2],[10,4],[17,6]],self.mech("metamagic-level-2")["optionsKnownByClassLevel"])
        catalog={v["name"]:v for v in self.mech("metamagic-options-level-2")["options"]}
        self.assertEqual(14,len(catalog))
        self.assertEqual(3,catalog["Amplified Spell"]["costSorceryPoints"])
        self.assertEqual(2,catalog["Ascendant Spell"]["costPerEffectiveSpellLevel"])
        self.assertEqual(4,catalog["Widened Spell"]["costSorceryPoints"])
        self.assertEqual(5,catalog["Maximized Spell"]["costSorceryPoints"])
        self.assertEqual(2,len(catalog["Twinned Spell"]["modes"]))
        self.assertEqual(8,self.mech("metamagic-options-level-2")["amplifiedPlusMaximized"]["totalCost"])
        self.assertTrue(self.mech("sorcerous-restoration-level-5")["stacksWithLevel20Benefit"])
        self.assertEqual(14,self.mech("sorcerous-restoration-level-20")["maximumRecoveredWithLevel5RestorationAtLevel20"])
        self.assertEqual(2,self.mech("arcane-apotheosis-level-20")["discountUpToSorceryPoints"])
    def test_companions_subclasses_and_3x(self):
        self.assertTrue(self.rule["companionPathChoice"]["exclusive"])
        self.assertEqual("deferred-to-shared-companion-system",self.rule["companionPathChoice"]["implementation"])
        self.assertTrue(self.rows[0]["pendingCompanionPathChoice"])
        self.assertEqual(["class-feature.sorcerer.ability-score-improvement-level-19"],self.rows[18]["featureGrants"])
        self.assertEqual(21,self.rule["epicBoonMinimumTotalCharacterLevel"])
        self.assertIsNone(self.rule["permanentAbilityScoreMaximum"])
        self.assertEqual(10,self.rows[19]["baseAttackBonus"])
        self.assertEqual([10,5],self.rows[19]["iterativeBaseAttackBonuses"])
        self.assertEqual({"fortitude":6,"reflex":6,"will":12},self.rows[19]["baseSaves"])
    def test_manifest(self):
        rows={x.get("path"):x for x in load("data/reference/srd/native-coverage-manifest.json")["coverage"]}
        for p in [R/"data/native/classes/base/sorcerer.json",*(R/"data/native/features/classes/sorcerer").glob("*.json")]:
            obj=load(p.relative_to(R)); status=rows[p.relative_to(R).as_posix()]["status"]
            if obj["baselineStatus"]=="retained-native-definition":
                self.assertIn(status,{"native-single-source-retained","native-compatible-merged"})
            else:
                self.assertIn(status,{"single-candidate","multiple-candidates"})
if __name__=="__main__": unittest.main()
