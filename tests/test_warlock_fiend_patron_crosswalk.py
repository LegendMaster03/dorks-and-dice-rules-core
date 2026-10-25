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
        self.assertEqual({"owner-ruling-needed":4,"deferred-outside-srd":1,"owner-approved-design-direction":1,"partially-owner-approved":1,"reopened-owner-review":1},statuses)
        initial=next(d for d in decision if d["id"]=="patron-initial-mechanical-grant")
        self.assertIn("level 1",initial["resolution"])
        self.assertEqual(1,self.crosswalk["approvedNativeDirections"]["patronInitialFiendMilestone"]["classLevel"])
        self.assertEqual(["warlock.patron.fiend.spell-access","warlock.patron.fiend.dark-ones-blessing"],self.crosswalk["approvedNativeDirections"]["patronInitialFiendMilestone"]["partIds"])
        luck=self.crosswalk["approvedNativeDirections"]["luckUses"]
        self.assertEqual("max(1, charisma_modifier)",luck["usesExpression"])
        self.assertEqual("unresolved",luck["reset"])
        ledger=load("data/reference/srd/class-reconciliation-decisions.json")["decisions"]
        review=next(v["reviewClarification"] for v in ledger if v["nativeRuleId"]=="class.warlock.historical-class-features-band1")
        self.assertEqual("reopened-for-owner-review-not-a-new-mechanical-ruling",review["status"])
        self.assertEqual(4,len(review["unaffectedApprovedFeatureFamilies"]))
        self.assertNotIn("definition",self.native)

if __name__=="__main__":
    unittest.main()
