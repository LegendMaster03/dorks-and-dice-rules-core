"""Regression checks for approved Cleric Band 1 native reconciliation."""
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/"data/rulesets/first-party-001/working/classes/base/cleric.json"
FEATURE_DIR=ROOT/"data/rulesets/first-party-001/working/features/classes/cleric"
MANIFEST=ROOT/"data/reference/srd/native-coverage-manifest.json"

def read(path):
    return json.loads(path.read_text(encoding="utf-8"))

class ClericBand1Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.native=read(BASE)
        cls.rule=cls.native["definition"]
        cls.levels=cls.rule["progression"]
        cls.features={v["id"]:v for v in (read(p) for p in FEATURE_DIR.glob("*.json"))}

    def test_class_levels_and_reconciled_base_features(self):
        self.assertEqual("class.cleric",self.native["id"])
        self.assertEqual("retained-native-definition",self.native["baselineStatus"])
        self.assertNotIn("candidateDefinitions",self.native)
        self.assertEqual([1,20],self.rule["levelRange"])
        self.assertEqual(list(range(1,21)),[r["level"] for r in self.levels])
        inline={v["id"] for v in self.rule["inlineFeatures"]}
        eligible=set(self.features)|inline
        grants=[feature for level in self.levels for feature in level["featureGrants"]]
        self.assertEqual(len(grants),len(set(grants)))
        for feature in grants:
            self.assertIn(feature,eligible)
            if feature in self.features:
                self.assertEqual("retained-native-definition",self.features[feature]["baselineStatus"])
                self.assertNotIn("candidateDefinitions",self.features[feature])
        self.assertFalse(any(("domain" in id_ or "subclass" in id_ or "epic-boon" in id_)
                             for id_ in grants))
        self.assertEqual(["class-feature.cleric.ability-score-improvement-level-19"],
                         self.levels[18]["featureGrants"])

    def test_divine_order_and_blessed_strikes_are_choices_not_both_granted(self):
        selections=set(self.rule["selectableOptionFeatures"])
        self.assertEqual({
            "class-feature.cleric.protector-level-1",
            "class-feature.cleric.thaumaturge-level-1",
            "class-feature.cleric.divine-strike-level-7",
            "class-feature.cleric.potent-spellcasting-level-7",
        },selections)
        unconditional={f for l in self.levels for f in l["featureGrants"]}
        self.assertFalse(unconditional.intersection(selections))
        order=self.features["class-feature.cleric.divine-order-level-1"]["definition"]["mechanics"]
        strikes=self.features["class-feature.cleric.blessed-strikes-level-7"]["definition"]["mechanics"]
        self.assertEqual(1,order["choose"])
        self.assertEqual(1,strikes["choose"])

    def test_full_list_spells_without_preparation(self):
        spell=self.features["class-feature.cleric.spellcasting-level-1"]["definition"]["mechanics"]
        self.assertEqual("all-eligible-cleric-spells-of-castable-level",spell["spellAccess"])
        self.assertIsNone(spell["knownSpellsCountLimit"])
        self.assertFalse(spell["requiresDailyPreparation"])
        self.assertEqual(["spell-slots","spell-points"],spell["resourceChoices"])
        self.assertEqual(9,len(self.levels[19]["spellSlotsBySpellLevel"]))
        self.assertEqual([3,3,3,4,4,4,4,4,4,5],spell["cantripsKnownByLevel"][:10])
        self.assertEqual("not-granted-until-later-domain-pass",self.rule["domainSpellSlots"])

    def test_one_channel_divinity_pool_and_alternate_procedures(self):
        self.assertEqual([2,2,2,2,2,3], [l["channelDivinityUses"] for l in self.levels[:6]])
        self.assertEqual(4,self.levels[17]["channelDivinityUses"])
        self.assertEqual("cleric.channel-divinity",self.rule["channelDivinityResource"])
        self.assertEqual(1,self.rule["channelDivinityShortRestReturn"])
        self.assertEqual("all",self.rule["channelDivinityLongRestReturn"])
        self.assertEqual(1,self.rule["channelDivinityAvailableAtClassLevel"])
        self.assertNotIn("modern-turn-undead",self.levels[0]["availableChannelDivinityEffects"])
        self.assertIn("modern-turn-undead",self.levels[1]["availableChannelDivinityEffects"])
        legacy=next(v["mechanics"] for v in self.rule["inlineFeatures"]
                    if v["id"]=="cleric.historical-turn-undead")
        self.assertEqual("d20+charisma-modifier+religion-rank-synergy",legacy["turnCheck"])
        self.assertEqual("2d6+cleric-class-level+charisma-modifier",legacy["totalAffectedHitDice"])
        modern=self.features["class-feature.cleric.turn-undead-level-2"]["definition"]["mechanics"]
        self.assertEqual("wisdom",modern["savingThrow"])
        self.assertTrue(modern["searUndeadDamageDoesNotEndEffect"])

    def test_sear_destroy_and_strike_progression(self):
        self.assertEqual(0.5,self.levels[4]["modernDestroyUndeadMaxCR"])
        self.assertEqual(4,self.levels[16]["modernDestroyUndeadMaxCR"])
        self.assertEqual(4,self.levels[17]["divineSparkDice"])
        self.assertEqual(["cleric.channel-divinity"],[self.rule["channelDivinityResource"]])
        sear=self.features["class-feature.cleric.sear-undead-level-5"]["definition"]["mechanics"]
        self.assertEqual("max(1,wisdom-modifier)d8",sear["damageDice"])
        improved=self.features["class-feature.cleric.improved-blessed-strikes-level-14"]["definition"]["mechanics"]
        self.assertTrue(improved["doesNotGrantUnselectedOption"])

    def test_intervention_both_procedures_and_caps(self):
        feature=self.features["class-feature.cleric.divine-intervention-level-10"]["definition"]["mechanics"]
        self.assertEqual("dual-divine-intervention",feature["kind"])
        self.assertEqual(5,feature["direct"]["spellLevelMaximum"])
        self.assertEqual("long-rest",feature["direct"]["regain"])
        self.assertEqual("d100<=cleric-class-level",feature["supplication"]["successCheck"])
        self.assertEqual(7,feature["supplication"]["successCooldownDays"])
        self.assertEqual("automatic-success-no-percentile-roll",feature["supplication"]["atClericLevel20"])
        cap=self.features["class-feature.cleric.greater-divine-intervention-level-20"]["definition"]["mechanics"]
        self.assertEqual("wish",cap["addsSpell"])
        self.assertEqual("2d4-long-rests",cap["whenWishChosenLockout"])
        old=self.features["class-feature.cleric.divine-intervention-improvement-level-20"]
        self.assertTrue(old["definition"]["mechanics"]["supplicationAutomaticSuccess"])

    def test_historical_progression_and_no_alignment_or_domains(self):
        self.assertEqual(15,self.levels[19]["baseAttackBonus"])
        self.assertEqual([15,10,5],self.levels[19]["iterativeBaseAttackBonuses"])
        self.assertEqual({"fortitude":12,"reflex":6,"will":12},self.levels[19]["baseSaves"])
        self.assertFalse(self.rule["deityRequired"])
        self.assertIsNone(self.rule["alignmentRestriction"])
        self.assertIsNone(self.rule["permanentAbilityScoreMaximum"])
        self.assertEqual(21,self.rule["epicBoonTotalCharacterLevelMinimum"])

    def test_staged_domain_subclass_and_superseded_candidates_remain_inert(self):
        selected={f for level in self.levels for f in level["featureGrants"]}
        selected|=set(self.rule["selectableOptionFeatures"])
        inactive=[v for k,v in self.features.items() if k not in selected]
        self.assertEqual(10,len(inactive))
        for obj in inactive:
            self.assertEqual("pending-native-reconciliation",obj["baselineStatus"])
            self.assertIn("candidateDefinitions",obj)
        cover={v["path"]:v for v in read(MANIFEST)["coverage"] if "path" in v}
        for path in [BASE,*FEATURE_DIR.glob("*.json")]:
            obj=read(path)
            row=cover[path.relative_to(ROOT).as_posix()]
            if obj["baselineStatus"]=="retained-native-definition":
                self.assertIn(row["status"],{"native-single-source-retained","native-compatible-merged"})
            else:
                self.assertIn(row["status"],{"single-candidate","multiple-candidates"})

if __name__=="__main__":
    unittest.main()
