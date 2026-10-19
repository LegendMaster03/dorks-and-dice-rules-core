"""Safeguards for the final common monster ability and spell restoration."""
import json
from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[1]

def item(name):
    return json.loads((ROOT / ("data/native/bestiary/" + name + ".json")).read_text())

class FinalMonsterRemovedFeaturesTests(unittest.TestCase):
    def test_magic_attacks_and_vampire_regeneration_preserved(self):
        pit = item("pit-fiend")["definition"]
        self.assertIn("Magic Weapons", [x["name"] for x in pit["trait"]])
        for name in ("vampire", "vampire-spawn"):
            m = item(name)["definition"]
            self.assertIn("Regeneration", [x["name"] for x in m["trait"]])

    def test_older_sneak_attacks_are_distinct_from_poison(self):
        for name in ("assassin", "spy"):
            m = item(name)["definition"]
            self.assertIn("Sneak Attack (1/Turn)", [x["name"] for x in m["trait"]])

    def test_missing_nonstandard_spellcasting_restored(self):
        drider = item("drider")["definition"]
        self.assertIn("Dancing Lights", [s["name"] for s in drider["spellcasting"]])
        pit = item("pit-fiend")["definition"]
        self.assertIn("Detect Magic", [s["name"] for s in pit["spellcasting"]])
        self.assertTrue(any("Hellfire Spellcasting" in s["name"]
                            for s in pit["spellcasting"]))

    def test_remaining_decisions_are_explicit_not_extra_monsters(self):
        ledger = json.loads((ROOT / "data/reference/srd/monster-legacy-variant-review.json").read_text())
        remaining = {
            entry["nativeId"] for entry in ledger["entries"]
            if entry["remainingFeatureQuestions"]
        }
        self.assertEqual({
            "monster.assassin",
        }, remaining)

    def test_weretiger_pounce_preserves_old_trigger_with_new_attacks(self):
        weretiger = item("weretiger")["definition"]
        pounce = [x for x in weretiger["trait"]
                  if x["name"] == "Pounce (Tiger or Hybrid Form Only)"]
        self.assertEqual(1, len(pounce))
        rule = pounce[0]["entries"][0]
        self.assertIn("15 feet", rule)
        self.assertIn("Scratch attack", rule)
        self.assertIn("Bite (Tiger or Hybrid Form Only) attack", rule)
        self.assertIn("{@dc 14}", rule)
        self.assertIn("bonus action", rule)
        self.assertNotIn("claw attack", rule)
        self.assertEqual(2, len([x for x in weretiger["action"]
                                 if x["name"] in
                                 ("Scratch", "Bite (Tiger or Hybrid Form Only)")]))


    def test_all_three_beast_pounces_reuse_newer_rend_and_old_cost(self):
        for slug, feet, dc in (
            ("lion", 20, 13),
            ("panther", 20, 12),
            ("saber-toothed-tiger", 20, 14),
        ):
            with self.subTest(monster=slug):
                definition = item(slug)["definition"]
                attacks = [x["name"] for x in definition["action"]]
                self.assertIn("Rend", attacks)
                self.assertNotIn("Claw", attacks)
                self.assertNotIn("Bite", attacks)
                self.assertEqual(
                    1, len([x for x in definition["trait"]
                            if x["name"] == "Pounce"]))
                entry = next(x for x in definition["trait"]
                             if x["name"] == "Pounce")["entries"][0]
                self.assertIn(f"{feet} feet straight", entry)
                self.assertIn(f"{{@dc {dc}}} Strength saving throw", entry)
                self.assertIn("hits it with a Rend attack", entry)
                self.assertIn("knocked {@condition prone}", entry)
                self.assertIn("one Rend attack against it as a bonus action", entry)
                self.assertNotIn("claw attack", entry.lower())
                self.assertNotIn("bite attack", entry.lower())
                if slug in ("panther", "saber-toothed-tiger"):
                    self.assertIn("Nimble Escape",
                                  [x["name"] for x in definition["bonus"]])

    def test_assassin_first_turn_advantage_is_distinct_from_critical_ruling(self):
        assassin = item("assassin")["definition"]
        grants = [x for x in assassin["trait"]
                  if x["name"] == "Assassinate (First-Turn Advantage)"]
        self.assertEqual(1, len(grants))
        self.assertIn("first turn of combat", grants[0]["entries"][0])
        self.assertIn("hasn't taken a turn", grants[0]["entries"][0])
        self.assertNotIn("critical hit", grants[0]["entries"][0])
        self.assertIn("Sneak Attack (1/Turn)",
                      [x["name"] for x in assassin["trait"]])

    def test_priest_slots_and_divine_eminence_reconciled_without_spell_replacement(self):
        priest = item("priest")["definition"]
        legacy = json.loads(
            (ROOT / "data/reference/srd/structured/bestiary/bestiary-srd51.json")
            .read_text(encoding="utf-8")
        )
        current = json.loads(
            (ROOT / "data/reference/srd/structured/bestiary/bestiary-srd52.json")
            .read_text(encoding="utf-8")
        )
        older = next(x for x in legacy["monster"] if x["name"] == "Priest")
        newer = next(x for x in current["monster"] if x["name"] == "Priest")
        original_slots = {
            level: details["slots"]
            for level, details in older["spellcasting"][0]["spells"].items()
            if "slots" in details
        }
        self.assertEqual(
            {"mode": "spell-slots", "slotsByLevel": original_slots},
            priest["spellcastingResources"]
        )
        self.assertEqual(newer["spellcasting"], priest["spellcasting"])
        self.assertEqual(newer["action"], priest["action"])
        self.assertEqual("2", priest["cr"])
        self.assertEqual(1, sum(x["name"] == "Divine Eminence"
                                for x in priest["trait"]))
        rule = next(x for x in priest["trait"]
                    if x["name"] == "Divine Eminence")["entries"][0]
        self.assertIn("bonus action", rule)
        self.assertIn("one spell slot", rule)
        self.assertIn("{@damage 3d6}", rule)
        self.assertIn("{@damage 1d6}", rule)
        self.assertIn("each hit until the end of the current turn", rule)
        self.assertIn("Mace attack's existing Radiant damage", rule)
        self.assertIn("does not apply to Radiant Flame", rule)
        self.assertNotIn("prepared", json.dumps(priest["spellcasting"]).lower())
        ledger = json.loads(
            (ROOT / "data/reference/srd/monster-legacy-variant-review.json").read_text()
        )
        review = next(x for x in ledger["entries"]
                      if x["nativeId"] == "monster.priest")
        self.assertEqual(0, review["remainingFeatureQuestions"])
        self.assertEqual("restored",
                         review["featureDisposition"]["trait"][0]["status"])

    def test_restoration_ledger_has_no_duplicated_grants(self):
        ledger = json.loads(
            (ROOT / "data/reference/srd/monster-legacy-variant-review.json").read_text()
        )
        manifest = json.loads(
            (ROOT / "data/reference/srd/native-coverage-manifest.json").read_text()
        )
        locations = {r["nativeId"]: r["path"] for r in manifest["coverage"] if "path" in r}
        for row in ledger["entries"]:
            identity = row.get("correspondingDefault", row["nativeId"])
            definition = json.loads((ROOT / locations[identity]).read_text())["definition"]
            for kind, features in row["featureDisposition"].items():
                if kind == "spellcasting":
                    continue
                names = [x["name"] for x in definition.get(kind, [])]
                for feature in features:
                    if feature["status"] != "restored":
                        continue
                    with self.subTest(monster=identity, kind=kind, feature=feature["name"]):
                        self.assertEqual(1, names.count(feature["name"]))

    def test_reconciled_counts_are_consistent(self):
        ledger = json.loads((ROOT / "data/reference/srd/monster-legacy-variant-review.json").read_text())
        stats = json.loads((ROOT / "docs/rewrite/native-reconciliation-statistics.json").read_text())
        self.assertEqual(
            sum(x["remainingFeatureQuestions"] for x in ledger["entries"]),
            stats["monsterRemainingFeatureQuestions"])

if __name__ == "__main__":
    unittest.main()
