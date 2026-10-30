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
    paths = sorted(directory.glob("*.json"))
    records = [load(path.relative_to(NATIVE)) for path in paths]
    assert len(records) == len({entry["id"] for entry in records}), f"Duplicate option ID in {folder}"
    return {record["id"]: record for record in records}

class WarlockNativeAlphaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.warlock = load("classes/base/warlock/warlock.json")["definition"]
        cls.patrons = load_option_records("patrons")
        cls.patron = cls.patrons["patron.warlock.fiend"]
        cls.pacts = load_option_records("pacts")
        cls.invocations = load_option_records("invocations")
        cls.selection = cls.warlock["selectionRules"]
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
        granted_at = [
            row["level"] for i, row in enumerate(rows)
            if row["invocationsElectiveTotal"] > (rows[i-1]["invocationsElectiveTotal"] if i else 0)
        ]
        self.assertEqual([1,2,4,5,7,9,11,13,15,16,18,20], granted_at)
        self.assertEqual(1, self.selection["invocations"]["firstChoiceAtWarlockLevel"])
        self.assertTrue(self.selection["pact"]["firstChoiceFree"])
        self.assertEqual(1, self.selection["patron"]["requiredAtWarlockLevel"])
        self.assertEqual(1, self.selection["pact"]["requiredAtWarlockLevel"])
        self.assertIn("class-feature.warlock.patron-choice", rows[0]["featureGrants"])
        self.assertIn("class-feature.warlock.pact-choice", rows[0]["featureGrants"])
        self.assertIn("class-feature.warlock.eldritch-invocations", rows[0]["featureGrants"])
        self.assertIn("class-feature.warlock.deepened-pact", rows[4]["featureGrants"])
        self.assertTrue(all("fiendMilestones" not in row for row in rows))

    def test_all_grants_resolve_and_identity_is_unique(self):
        ids=set(self.features)
        for row in self.warlock["progression"]:
            for ref in row["featureGrants"]:
                self.assertIn(ref, ids)
        for id,feature in self.features.items():
            self.assertEqual("class.warlock",feature["parentClass"])
            self.assertEqual("classFeature",feature["kind"])
        self.assertEqual(set(self.warlock["baseFeatureIds"]),ids)
        option_ids = list(self.patrons) + list(self.pacts) + list(self.invocations)
        self.assertEqual(len(option_ids), len(set(option_ids)))
        self.assertNotIn("activePatronIds", self.selection["patron"])
        self.assertNotIn("activePactIds", self.selection["pact"])
        self.assertNotIn("activeInvocationIds", self.selection["invocations"])

    def test_per_class_directories_and_individual_option_records(self):
        base = NATIVE / "classes/base"
        for name in ("barbarian", "bard", "cleric", "druid", "rogue", "sorcerer", "warlock", "wizard"):
            with self.subTest(name=name):
                document = load("classes/base/" + name + "/" + name + ".json")
                self.assertEqual("class." + name, document["id"])
                self.assertFalse((base / (name + ".json")).exists())
        self.assertNotIn("pillarSystem", self.warlock)
        self.assertNotIn("optionRuleIds", self.warlock)
        self.assertEqual({"patron", "pact", "invocations"}, set(self.selection))
        for folder, records in (
            ("patrons", self.patrons),
            ("pacts", self.pacts),
            ("invocations", self.invocations),
        ):
            with self.subTest(folder=folder):
                directory = base / "warlock" / folder
                self.assertFalse((directory / "selection.json").exists())
                paths = list(directory.glob("*.json"))
                self.assertEqual(len(records), len(paths))
                self.assertTrue(records)
                for path in paths:
                    record = load(path.relative_to(NATIVE))
                    self.assertEqual("class.warlock", record["parentClass"])
                    self.assertEqual(record["id"].split(".")[-1] + ".json", path.name)
        self.assertEqual("patron.warlock.fiend", self.selection["patron"]["playtestPatronId"])
        self.assertIn(self.selection["patron"]["playtestPatronId"], self.patrons)
        self.assertTrue(self.selection["patron"]["identityMayBeInheritedOrUnknown"])
        self.assertTrue(self.selection["pact"]["firstChoiceFree"])
        self.assertEqual("out-of-scope", self.selection["pact"]["extraPacts"])
        self.assertTrue(self.selection["invocations"]["everyChoiceAttachedToOneActualPact"])
        self.assertTrue(self.selection["invocations"]["patronAcceptanceRequired"])
        self.assertEqual("suspend-without-refund", self.selection["invocations"]["severance"])
        self.assertEqual("one-on-warlock-level-up", self.selection["invocations"]["replacement"])
        self.assertNotIn("acquisitionLevels", self.selection["invocations"])
        self.assertNotIn("level20Choices", self.selection["invocations"])
        self.assertNotIn("selectionCost", self.selection["invocations"])
        self.assertNotIn("fiendSpellRosterAutomatic", self.warlock["separateGrants"])
        self.assertFalse((NATIVE / "classes/warlock-patrons.json").exists())
        self.assertFalse((NATIVE / "classes/warlock-pacts.json").exists())
        self.assertFalse((NATIVE / "classes/warlock-invocations.json").exists())

    def test_pact_and_patron_alpha(self):
        self.assertEqual("patron.warlock.fiend",self.patron["id"])
        roster=self.patron["spellAccess"]["grants"]
        self.assertEqual([3,5,7,9],[s["level"] for s in roster])
        self.assertEqual(13,sum(len(s["spells"]) for s in roster))
        self.assertEqual("automatic-known-no-ordinary-selection-cost",
                         self.patron["spellAccess"]["defaultMode"])
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
        for omitted in ("improved-pact-weapon","gift-of-the-ever-living-ones","aspect-of-the-moon","far-scribe"):
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

    def test_three_talisman_invocations_preserve_approved_resources(self):
        entries = {entry['id']:entry for entry in self.invocations.values()
                   if entry['requiredPact']=='pact.warlock.talisman'}
        self.assertEqual({'invocation.rebuke-of-the-talisman', 'invocation.protection-of-the-talisman',
                          'invocation.bond-of-the-talisman'}, set(entries))
        self.assertEqual([1,7,12], [entries['invocation.'+slug]['minimumWarlockLevel'] for slug in
                         ['rebuke-of-the-talisman','protection-of-the-talisman','bond-of-the-talisman']])
        self.assertTrue(all(entry['cost']==1 and not entry['repeatable'] for entry in entries.values()))
        rebuke = entries['invocation.rebuke-of-the-talisman']['description']
        self.assertIn('within 30 feet of you',rebuke)
        self.assertIn('10 feet away from the wearer',rebuke)
        self.assertIn('no core Talisman charge',rebuke)
        self.assertIn('same Reaction',rebuke)
        protection = entries['invocation.protection-of-the-talisman']['description']
        self.assertIn('fails a saving throw',protection)
        self.assertIn('add 1d4',protection)
        self.assertIn('separate from the core Talisman charges',protection)
        bond = entries['invocation.bond-of-the-talisman']['description']
        self.assertIn('same plane',bond)
        self.assertIn('use an Action',bond)
        self.assertIn('nearest unoccupied space',bond)
        self.assertIn('single shared Bond pool',bond)
        self.assertIn('regardless of direction',bond)
        self.assertIn('separate from core Talisman charges',bond)

    def test_reference_remains_comparison_not_authority(self):
        self.assertFalse(self.reference["runtimeAuthority"])
        self.assertEqual("class.warlock", self.reference["excludedAuthoritativeClassId"])
        self.assertEqual("0.3.1",self.warlock["alphaVersion"])

    def test_restored_play_procedures_and_approved_feedback(self):
        blade = self.pacts['pact.warlock.blade']
        self.assertIn('5 feet away for one minute', blade['description'])
        self.assertIn('physical weapon remains present', blade['description'])
        self.assertIn('Extra Attack features do not automatically add', blade['deepenedAtLevel5'])
        chain = self.pacts['pact.warlock.chain']
        self.assertIn('give the command before', chain['description'])
        self.assertIn('not gain a second Attack action', chain['deepenedAtLevel5'])
        tome = self.pacts['pact.warlock.tome']
        self.assertIn('Only you access its Pact magic', tome['description'])
        self.assertIn('does not add them to normal Pact-slot casting unless separately learned', tome['deepenedAtLevel5'])
        self.assertIn('three independent uses, not a shared pool', tome['deepenedAtLevel5'])
        talisman = self.pacts['pact.warlock.talisman']
        self.assertIn('\n\nUntrained Guidance:', talisman['description'])
        self.assertIn('\n\nFortunate Correction:', talisman['description'])
        self.assertIn('old one becomes inactive', talisman['description'])
        self.assertIn('sight of the attacker is not required', talisman['deepenedAtLevel5'])
        investment = self.invocations['invocation.investment-of-the-chain-master']['description']
        self.assertIn('preserving superior base speed', investment)
        self.assertIn('Bludgeoning, Piercing, or Slashing', investment)
        self.assertIn('Necrotic, Psychic, or Radiant', investment)
        self.assertIn('refill limited attacks', investment)
        self.assertIn('Paid ritual records survive book replacement', self.invocations['invocation.book-of-ancient-secrets']['description'])
        self.assertIn('erase a name with a Magic action', self.invocations['invocation.gift-of-the-protectors']['description'])
        retaliation = next(f for f in self.patron['features'] if f['level']==8)['description']
        self.assertIn('including those spent at 0 without healing', retaliation)
        self.assertIn('twentieth subsequent turn start', retaliation)
        self.assertIn('restore that choice for another eligible spell', self.patron['spellAccess']['description'])
        self.assertNotIn('Summon Undead', tome['deepenedAtLevel5'])
        self.assertNotIn('invocation.grasp-of-hadar', self.invocations)

    def test_advancement_shows_improvements_and_related_patron_grants(self):
        rows = {r['level']:r for r in self.warlock['progression']}
        asi = 'class-feature.warlock.ability-score-improvement'
        self.assertEqual([4,8,12,16,19], [level for level,row in rows.items() if asi in row['featureGrants']])
        for level in [7,11,15,19,20]: self.assertTrue(rows[level]['improvements'])
        for level in [1,3,5,6,7,8,9,13,14,18]:
            self.assertTrue(rows[level]['relatedFeatures'])
            self.assertTrue(all(f['ruleId']=='patron.warlock.fiend' for f in rows[level]['relatedFeatures']))
            self.assertNotIn('patron.warlock.fiend',rows[level]['featureGrants'])

if __name__ == "__main__":
    unittest.main()

