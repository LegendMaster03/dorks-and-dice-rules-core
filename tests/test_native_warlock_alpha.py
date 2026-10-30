"""Native Warlock Alpha 0.3.1 content and playtest-selection invariants."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"

def load(rel):
    return json.loads((NATIVE / rel).read_text(encoding="utf-8"))

def load_option_records(folder):
    directory = NATIVE / "classes/base/warlock" / folder
    paths = sorted(p for p in directory.glob("*.json") if p.name != "selection.json")
    return {record["id"]: record for record in (load(p.relative_to(NATIVE)) for p in paths)}

class WarlockNativeAlphaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.warlock = load("classes/base/warlock/warlock.json")["definition"]
        cls.patrons = load_option_records("patrons")
        cls.patron = cls.patrons["patron.warlock.fiend"]
        cls.pacts = load_option_records("pacts")
        cls.invocations = load_option_records("invocations")
        cls.option_rules = {section: load("classes/base/warlock/" + folder + "/selection.json")
                            for section, folder in (("patron", "patrons"), ("pact", "pacts"), ("invocations", "invocations"))}
        cls.features = {f["id"]: f for f in load("features/classes/warlock/features.json")["classFeatures"]}
        cls.reference = json.loads((ROOT / "data/reference/experiments/warlock-modular-alpha-0.3.1.json").read_text(encoding="utf-8"))

    def test_selected_alpha_progression_matches_playtest(self):
        rows = self.warlock["progression"]
        self.assertEqual(list(range(1, 21)), [r["level"] for r in rows])
        for actual, expected in zip(rows, self.reference["levels"]):
            for a,b in [("invocationsElectiveTotal","invocationsElectiveTotal"),
                        ("ordinaryCantripsKnown","cantripsKnownExcludingEldritchBlast"),
                        ("ordinarySpellsKnown","ordinarySpellsKnown"),
                        ("pactSlots","pactSlots"),("pactSlotLevel","pactSlotLevel")]:
                self.assertEqual(expected[b],actual[a],(actual["level"],a))
        self.assertEqual(1, rows[0]["invocationsElectiveTotal"])
        self.assertEqual(12, rows[-1]["invocationsElectiveTotal"])
        self.assertEqual([1,2,4,5,7,9,11,13,15,16,18,20],self.option_rules["invocations"]["definition"]["acquisitionLevels"])
        self.assertTrue(self.option_rules["pact"]["definition"]["firstChoiceFree"])
        self.assertEqual(1, self.option_rules["patron"]["definition"]["requiredAtWarlockLevel"])

    def test_all_grants_resolve_and_identity_is_unique(self):
        ids=set(self.features)
        for row in self.warlock["progression"]:
            for ref in row["featureGrants"]:
                self.assertIn(ref, ids)
        for id,feature in self.features.items():
            self.assertEqual("class.warlock",feature["parentClass"])
            self.assertEqual("classFeature",feature["kind"])
        self.assertEqual(set(self.warlock["baseFeatureIds"]),ids)
        self.assertEqual(set(self.option_rules["pact"]["definition"]["activePactIds"]),set(self.pacts))
        self.assertEqual(set(self.option_rules["invocations"]["definition"]["activeInvocationIds"]),set(self.invocations))

    def test_per_class_directories_and_individual_option_records(self):
        base = NATIVE / "classes/base"
        for name in ("barbarian", "bard", "cleric", "druid", "rogue", "sorcerer", "warlock", "wizard"):
            with self.subTest(name=name):
                document = load("classes/base/" + name + "/" + name + ".json")
                self.assertEqual("class." + name, document["id"])
                self.assertFalse((base / (name + ".json")).exists())
        self.assertNotIn("pillarSystem", self.warlock)
        self.assertEqual(
            {section: record["id"] for section, record in self.option_rules.items()},
            self.warlock["optionRuleIds"])
        for section, folder, records in (
            ("patron", "patrons", self.patrons),
            ("pact", "pacts", self.pacts),
            ("invocations", "invocations", self.invocations),
        ):
            with self.subTest(section=section):
                directory = base / "warlock" / folder
                self.assertEqual(len(records) + 1, len(list(directory.glob("*.json"))))
                rule = self.option_rules[section]
                self.assertEqual("rule", rule["kind"])
                self.assertEqual("class.warlock", rule["parentClass"])
                self.assertEqual(len(records), len(set(records)))
                self.assertTrue(all(record["parentClass"] == "class.warlock"
                                    for record in records.values()))
        self.assertEqual(set(self.patrons),
                         set(self.option_rules["patron"]["definition"]["activePatronIds"]))
        self.assertEqual("patron.warlock.fiend",
                         self.option_rules["patron"]["definition"]["defaultPatronId"])
        self.assertFalse((NATIVE / "classes/warlock-patrons.json").exists())
        self.assertFalse((NATIVE / "classes/warlock-pacts.json").exists())
        self.assertFalse((NATIVE / "classes/warlock-invocations.json").exists())

    def test_pact_and_patron_alpha(self):
        self.assertEqual("patron.warlock.fiend",self.patron["id"])
        roster=self.patron["spellAccess"]["grants"]
        self.assertEqual([3,5,7,9],[s["level"] for s in roster])
        self.assertEqual(13,sum(len(s["spells"]) for s in roster))
        self.assertEqual([1,6,8,13,14,18],[f["level"] for f in self.patron["features"]])
        self.assertTrue({"pact.warlock.blade", "pact.warlock.chain",
                         "pact.warlock.tome", "pact.warlock.talisman"}.issubset(self.pacts))
        for pact in self.pacts.values():
            self.assertEqual(0,pact["invocationCost"])
            self.assertEqual(1,pact["availableAtWarlockLevel"])
            self.assertIn("deepenedAtLevel5",pact)

    def test_invocation_catalog_is_selected_playtest_only(self):
        self.assertTrue(self.invocations)
        self.assertNotIn("invocation.thirsting-blade", self.invocations)
        for omitted in ("rebuke-of-the-talisman","protection-of-the-talisman","bond-of-the-talisman",
                        "improved-pact-weapon","gift-of-the-ever-living-ones","aspect-of-the-moon","far-scribe"):
            self.assertNotIn("invocation."+omitted,self.invocations)
        self.assertEqual(0,sum(x["cost"]!=1 for x in self.invocations.values()))
        for inv in self.invocations.values():
            self.assertEqual("class.warlock",inv["parentClass"])
            if inv["requiredPact"]:
                self.assertIn(inv["requiredPact"],self.pacts)
            self.assertLessEqual(inv["minimumWarlockLevel"],20)
            self.assertEqual("included-in-alpha-0.3.1",inv["selectionStatus"])
        self.assertEqual("pact.warlock.blade",self.invocations["invocation.devouring-blade"]["requiredPact"])
        self.assertEqual(12,self.invocations["invocation.devouring-blade"]["minimumWarlockLevel"])

    def test_reference_remains_comparison_not_authority(self):
        self.assertFalse(self.reference["runtimeAuthority"])
        self.assertEqual("class.warlock", self.reference["excludedAuthoritativeClassId"])
        self.assertEqual("0.3.1",self.warlock["alphaVersion"])

if __name__ == "__main__":
    unittest.main()
