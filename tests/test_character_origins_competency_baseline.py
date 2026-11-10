"""Origin-specific Competency grant defaults and explicit override boundaries.

This validates authored sources and choices, not crafting progression or
character entitlement. The native grant reducer only accepts already-earned
source occurrences; a selected option must first be authorized.
"""
import json
from pathlib import Path
import unittest


NATIVE = (Path(__file__).resolve().parents[1]
          / "data/rulesets/first-party-001/native")

# Explicit currently reviewed Origin benefits. These are not an exhaustive
# inventory or a fixed total for an actively developed catalog.
BACKGROUND_SITES = {
    "charlatan": {"charlatan-disguise-kit": None, "charlatan-forgery": None},
    "entertainer": {"entertainer-performance-method": "disguise-kit"},
    "explorer": {"explorer-field-competency": None},
    "guild-artisan": {"guild-artisan-trade": None},
    "guild-merchant": {"guild-merchant-professional-choice": "navigation"},
    "sailor": {"sailor-navigation": None},
    "spy": {"spy-tradecraft-competency": ("disguise", "forgery", "calligraphy")},
}
UPBRINGING_SITES = {
    "aristocratic": {"aristocratic-formative-competency":
                    ("calligraphy", "painting", "cooking")},
    "dwarf/general": {"crafting-tradition": None},
    "goliath/general": {"clan-craft": None},
    "gnome/rock": {"tinkering": None},
    "street-raised": {"street-raised-community-competency":
                     ("disguise", "cooking", "forgery")},
}


def read(kind, name):
    path = NATIVE / kind / (name + ".json")
    return json.loads(path.read_text(encoding="utf-8"))


def nodes(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from nodes(child)
    elif isinstance(value, list):
        for child in value:
            yield from nodes(child)


def selected_grants(record, id_, options):
    outer = next(x for x in record["benefits"] if x["id"] == id_)
    if options is None:
        return [outer]
    selected = (options,) if isinstance(options, str) else options
    return [entry for option in outer["options"] if option["id"] in selected
            for entry in option["benefits"]]


class CharacterOriginsCompetencyBaselineTests(unittest.TestCase):
    def test_all_native_origin_competency_grants_have_explicit_effects(self):
        """Future Origin grants must state their own values, not assume global rules."""
        for folder in ("backgrounds", "upbringings"):
            for path in (NATIVE / folder).rglob("*.json"):
                record = json.loads(path.read_text(encoding="utf-8"))
                for node in nodes(record):
                    if node.get("grantType") != "competency-training":
                        continue
                    with self.subTest(origin=record["id"],
                                      target=node.get("competency")):
                        effect = node["competencyTraining"]
                        self.assertEqual({"ranks", "proficient"}, set(effect))
                        self.assertIs(type(effect["ranks"]), int)
                        self.assertGreaterEqual(effect["ranks"], 0)
                        self.assertIs(type(effect["proficient"]), bool)
                        self.assertTrue(effect["ranks"] or effect["proficient"])

    def test_reviewed_background_grants_add_rank_without_implied_proficiency(self):
        for name, benefits in BACKGROUND_SITES.items():
            record = read("backgrounds", name)
            with self.subTest(background=name):
                self.assertEqual("background." + name, record["id"])
                for id_, options in benefits.items():
                    grants = selected_grants(record, id_, options)
                    self.assertTrue(grants)
                    for grant in grants:
                        self.assertEqual("competency-training", grant["grantType"])
                        self.assertEqual({"ranks": 1, "proficient": False},
                                         grant["competencyTraining"])

    def test_reviewed_upbringing_grants_proficiency_without_implied_rank(self):
        for name, benefits in UPBRINGING_SITES.items():
            record = read("upbringings", name)
            with self.subTest(upbringing=name):
                self.assertTrue(record["id"].startswith("upbringing."))
                for id_, options in benefits.items():
                    grants = selected_grants(record, id_, options)
                    self.assertTrue(grants)
                    for grant in grants:
                        self.assertEqual("competency-training", grant["grantType"])
                        self.assertEqual({"ranks": 0, "proficient": True},
                                         grant["competencyTraining"])

    def test_mixed_choices_keep_equipment_and_competency_effects_disjoint(self):
        cases = (
            ("backgrounds", "entertainer", "entertainer-performance-method",
             "musical-instrument"),
            ("backgrounds", "spy", "spy-tradecraft-competency", "thieves-tools"),
            ("upbringings", "aristocratic", "aristocratic-formative-competency",
             "gaming-set"),
            ("upbringings", "street-raised", "street-raised-community-competency",
             "thieves-tools"),
        )
        for kind, name, benefit_id, equipment_id in cases:
            outer = next(b for b in read(kind, name)["benefits"]
                         if b["id"] == benefit_id)
            with self.subTest(origin=name):
                self.assertEqual("benefit-choice", outer["grantType"])
                self.assertEqual(1, outer["choiceCount"])
                equipment_options = [o for o in outer["options"]
                                     if o["id"] == equipment_id]
                self.assertEqual(1, len(equipment_options))
                equip = equipment_options[0]
                self.assertIn("equipmentProficiencyGrants", equip)
                self.assertTrue(all(
                    b["grantType"] == "qualification-training"
                    and "competencyTraining" not in b
                    for b in equip["benefits"]))
                for option in outer["options"]:
                    if option is equip:
                        continue
                    self.assertNotIn("equipmentProficiencyGrants", option)
                    self.assertTrue(all(
                        b["grantType"] == "competency-training"
                        and "competencyTraining" in b
                        for b in option["benefits"]))


if __name__ == "__main__":
    unittest.main()
