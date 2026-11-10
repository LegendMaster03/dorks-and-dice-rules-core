"""Static contracts for scoped Character Upbringing bonuses.

These checks deliberately assert authored scope and provenance, not a universal
Proficiency Bonus stacking formula. General check mechanics remain separately
under review.
"""
import json
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native/upbringings"


def load(path):
    return json.loads((NATIVE / path).read_text(encoding="utf-8"))


def benefit(record, name):
    return next(b for b in record["benefits"] if b["id"] == name)


class OriginScopedBonusTests(unittest.TestCase):
    def test_history_multipliers_are_conditional_check_effects_not_training(self):
        cases = (
            ("dwarf/general.json", "stonecunning", "stonework-history"),
            ("gnome/rock.json", "artificers-lore",
             "magic-alchemy-technology-objects"),
        )
        for path, name, scope in cases:
            with self.subTest(record=path, name=name):
                b = benefit(load(path), name)
                self.assertEqual("skill.history", b["skill"])
                self.assertEqual("intelligence", b["ability"])
                self.assertEqual(2, b["proficiencyBonusMultiplier"])
                self.assertEqual(scope, b["scope"])
                self.assertNotIn("grantType", b)
                self.assertNotIn("competency", b)
                self.assertNotIn("ranks", b)
                self.assertNotIn("skillProficient", b)

    def test_elf_survival_modifiers_are_task_scoped_not_skill_training(self):
        cases = (
            ("elf/drow.json", "subterranean-survival",
             {"underground-navigation", "underground-tracking",
              "underground-foraging", "subterranean-hazards"}),
            ("elf/wood.json", "woodland-fieldcraft",
             {"forest-navigation", "forest-tracking",
              "forest-foraging", "natural-woodland-hazards"}),
        )
        for path, name, expected_scope in cases:
            with self.subTest(record=path, name=name):
                record = load(path)
                self.assertEqual("upbringing.elf.general", record["parent"])
                b = benefit(record, name)
                self.assertEqual("skill.survival", b["skill"])
                self.assertEqual(2, b["bonus"])
                self.assertTrue(b["stacksWithTraining"])
                self.assertEqual(expected_scope, set(b["scope"]))
                self.assertNotIn("proficiencyBonusMultiplier", b)
                self.assertNotIn("grantType", b)
                self.assertNotIn("competency", b)

    def test_general_elf_skill_choice_is_separate_from_child_bonus(self):
        parent = load("elf/general.json")
        skill = benefit(parent, "cultural-training")
        self.assertEqual("skill-training", skill["grantType"])
        self.assertEqual(1, skill["choiceCount"])
        self.assertEqual({"skill.insight", "skill.perception", "skill.survival"},
                         set(skill["chooseOne"]))
        for child_path in ("elf/drow.json", "elf/wood.json", "elf/high.json"):
            with self.subTest(child=child_path):
                child = load(child_path)
                self.assertEqual(parent["id"], child["parent"])
                self.assertNotIn("cultural-training",
                                 {b["id"] for b in child["benefits"]})

    def test_gnome_tinkering_does_not_make_history_a_competency_grant(self):
        gnome = load("gnome/rock.json")
        self.assertEqual("upbringing.gnome.general", gnome["parent"])
        tinkering = benefit(gnome, "tinkering")
        self.assertEqual("competency.tinkering", tinkering["competency"])
        self.assertEqual("competency-training", tinkering["grantType"])
        history = benefit(gnome, "artificers-lore")
        self.assertEqual("skill.history", history["skill"])
        self.assertNotIn("competency", history)


if __name__ == "__main__":
    unittest.main()
