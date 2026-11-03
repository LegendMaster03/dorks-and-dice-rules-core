"""Approved nine-species vision review: shared base vision versus specialized ancestry."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def base(slug):
    return read(NATIVE / "species/base" / (slug + ".json"))


def ancestry(path):
    return read(NATIVE / "species/ancestry" / (path + ".json"))


class ApprovedVisionReviewTests(unittest.TestCase):
    def test_dwarf_interim_darkvision_60_feet(self):
        dwarf = base("dwarf")
        self.assertEqual(60, dwarf["darkvisionFeet"])
        self.assertNotIn("low-light-vision", {t["id"] for t in dwarf["traits"]})

    def test_elf_and_gnome_share_native_low_light(self):
        rule = read(NATIVE / "rules/low-light-vision.json")
        self.assertEqual("rule.low-light-vision", rule["id"])
        for slug in ("elf", "gnome"):
            with self.subTest(slug=slug):
                species = base(slug)
                self.assertNotIn("darkvisionFeet", species)
                trait = next(t for t in species["traits"] if t["id"] == "low-light-vision")
                self.assertEqual(rule["id"], trait["rule"])

    def test_previous_approved_vision_changes_are_preserved(self):
        for slug in ("dragonborn", "orc", "tiefling"):
            with self.subTest(slug=slug):
                species = base(slug)
                self.assertNotIn("darkvisionFeet", species)
                self.assertEqual("rule.low-light-vision",
                                 next(t for t in species["traits"]
                                      if t["id"] == "low-light-vision")["rule"])

    def test_dark_adapted_ancestry_exceptions(self):
        drow = ancestry("elf/drow")
        self.assertEqual("species.elf", drow["parent"])
        self.assertEqual(120, drow["darkvisionFeet"])
        self.assertTrue(any(t["id"] == "sunlight-sensitivity" and
                            t["condition"] == "condition.dazzled" for t in drow["traits"]))
        stone = ancestry("goliath/stone")
        cavern = next(t for t in stone["traits"] if t["id"] == "cavernous-sight")
        self.assertIn("Darkvision", cavern["description"])
        self.assertIn("60 feet", cavern["description"])

    def test_unmodified_species_do_not_gain_vision(self):
        for slug in ("human", "halfling", "goliath"):
            with self.subTest(slug=slug):
                species = base(slug)
                self.assertNotIn("darkvisionFeet", species)
                self.assertFalse(any(t.get("rule") == "rule.low-light-vision"
                                     for t in species["traits"]))

    def test_approved_species_movement_and_creature_types(self):
        expected = {
            "dwarf": (25, "humanoid"),
            "elf": (30, "humanoid"),
            "gnome": (25, "humanoid"),
            "halfling": (25, "humanoid"),
            "dragonborn": (30, "dragon"),
            "goliath": (35, "giant"),
            "orc": (30, "humanoid"),
        }
        for slug, (speed, creature_type) in expected.items():
            with self.subTest(slug=slug):
                species = base(slug)
                self.assertEqual(speed, species["walkingSpeedFeet"])
                self.assertEqual(creature_type, species["creatureType"])


if __name__ == "__main__":
    unittest.main()
