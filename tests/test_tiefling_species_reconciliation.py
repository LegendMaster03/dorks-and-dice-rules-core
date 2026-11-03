"""Tiefling first-pass Species, ancestry, and source-coverage checks."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"
SPECIES = NATIVE / "species/base/tiefling.json"
ANCESTRY = NATIVE / "species/ancestry/tiefling"
SOURCE = ROOT / "data/rulesets/first-party-001/working/species/base/tiefling.json"
HISTORICAL_SECTION = ROOT / "data/rulesets/first-party-001/working/species/subspecies/tiefling/unnamed-77c4d3f18398af4b.json"
COVERAGE = ROOT / "data/reference/srd/native-coverage-manifest.json"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class TieflingSpeciesReconciliationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.base = read(SPECIES)
        cls.leaves = {name: read(ANCESTRY / (name + ".json"))
                      for name in ("abyssal", "chthonic", "infernal")}

    def test_base_is_fiend_native_but_playable_by_ancestry(self):
        base = self.base
        self.assertEqual("species.tiefling", base["id"])
        self.assertEqual("species", base["kind"])
        self.assertEqual("selected-native", base["status"])
        self.assertFalse(base["selectable"])
        self.assertEqual("fiend", base["creatureType"])
        self.assertEqual("native", base["creatureSubtype"])
        self.assertEqual({"breathing": True, "food": True, "sleep": True},
                         base["needs"])
        self.assertEqual(["small", "medium"], base["sizeChoice"]["from"])
        self.assertEqual(30, base["walkingSpeedFeet"])
        self.assertTrue(base["ancestrySelection"]["required"])
        self.assertEqual({l["id"] for l in self.leaves.values()},
                         set(base["ancestrySelection"]["selectOneFrom"]))
        self.assertNotIn("defaultUpbringing", base)

    def test_low_light_vision_and_shared_thaumaturgy(self):
        base = self.base
        traits = {trait["id"]: trait for trait in base["traits"]}
        self.assertEqual({"low-light-vision", "otherworldly-presence"},
                         set(traits))
        self.assertEqual("rule.low-light-vision", traits["low-light-vision"]["rule"])
        self.assertNotIn("darkvisionFeet", base)
        self.assertTrue(traits["otherworldly-presence"]["atWill"])
        self.assertEqual("thaumaturgy", traits["otherworldly-presence"]["spell"])
        self.assertEqual("selected-tiefling-ancestry",
                         traits["otherworldly-presence"]["spellcastingAbilityFrom"])

    def test_three_distinct_fiendish_ancestries(self):
        expected = {
            "abyssal": ("poison", "poison-spray", "ray-of-sickness", "hold-person"),
            "chthonic": ("necrotic", "chill-touch", "false-life", "ray-of-enfeeblement"),
            "infernal": ("fire", "fire-bolt", "hellish-rebuke", "darkness"),
        }
        self.assertEqual(set(expected),
                         {path.stem for path in ANCESTRY.glob("*.json")})
        for name, (damage, cantrip, level3, level5) in expected.items():
            with self.subTest(name=name):
                leaf = self.leaves[name]
                self.assertEqual("ancestry.tiefling." + name, leaf["id"])
                self.assertEqual("ancestry", leaf["kind"])
                self.assertEqual("selected-native", leaf["status"])
                self.assertTrue(leaf["selectable"])
                self.assertEqual("species.tiefling", leaf["parent"])
                self.assertEqual(["intelligence", "wisdom", "charisma"],
                                 leaf["spellcastingAbilityChoice"])
                resistance, first, third, fifth = leaf["traits"]
                self.assertEqual({"percent": 50, "damageType": damage},
                                 resistance["damageResistance"])
                self.assertEqual(cantrip, first["spell"])
                self.assertEqual(1, first["level"])
                self.assertTrue(first["atWill"])
                for entry, spell, level in ((third, level3, 3),
                                            (fifth, level5, 5)):
                    self.assertEqual(spell, entry["spell"])
                    self.assertEqual(level, entry["level"])
                    self.assertTrue(entry["alwaysPrepared"])
                    self.assertEqual(1, entry["freeUses"])
                    self.assertEqual("long-rest", entry["recharge"])
                    self.assertTrue(entry["slotCasting"])
                self.assertNotIn("defaultUpbringing", leaf)
                self.assertNotIn("darkvisionFeet", leaf)

    def test_infernal_2014_hellish_rebuke_exception(self):
        for slug, leaf in self.leaves.items():
            third = leaf["traits"][2]
            if slug == "infernal":
                self.assertEqual(2, third["freeCastSpellLevel"])
            else:
                self.assertNotIn("freeCastSpellLevel", third)

    def test_working_source_promoted_and_historical_artifact_untouched(self):
        self.assertFalse(SOURCE.exists())
        self.assertTrue(HISTORICAL_SECTION.exists())
        rows = [r for r in read(COVERAGE)["coverage"]
                if r["nativeId"] == "species.tiefling"]
        self.assertEqual(1, len(rows))
        self.assertEqual("approved-native-reconciled", rows[0]["status"])
        self.assertEqual("data/rulesets/first-party-001/native/species/base/tiefling.json",
                         rows[0]["path"])
        self.assertEqual(2, rows[0]["sourceRecords"])
        self.assertEqual(2, len(rows[0]["candidates"]))


if __name__ == "__main__":
    unittest.main()
