"""Validate only explicitly reviewed earlier-edition Origin Competency conversions.

There is NO universal Origin Competency grant. An earlier-edition benefit that
actually needs conversion may receive a source-type starting allocation.
Newly authored choices retain their approved existence but require separate
approval for the independent rank/proficiency effects.
"""
import json
from pathlib import Path
import unittest


NATIVE = (Path(__file__).resolve().parents[1]
          / "data/rulesets/first-party-001/native")

# Source-backed legacy tool/Competency training mapped into one native capability.
# These current allocations are neither a fixed corpus total nor general Origin rules.
CONVERTED_BACKGROUNDS = {
    "charlatan": {"charlatan-disguise-kit": None, "charlatan-forgery": None},
    "entertainer": {"entertainer-performance-method": "disguise-kit"},
    "guild-artisan": {"guild-artisan-trade": None},
    "guild-merchant": {"guild-merchant-professional-choice": "navigation"},
    "sailor": {"sailor-navigation": None},
}
CONVERTED_UPBRINGINGS = {
    "dwarf/general": {"crafting-tradition": None},
    "gnome/rock": {"tinkering": None},
}

# These are independently authored or materially reallocated training outcomes.
# The earlier source type alone does not determine their numeric/binary amounts.
AWAITING_SOURCE_SPECIFIC_ALLOCATION = (
    ("backgrounds", "explorer", "explorer-field-competency", None),
    ("backgrounds", "spy", "spy-tradecraft-competency",
     ("disguise", "forgery", "calligraphy")),
    ("upbringings", "aristocratic", "aristocratic-formative-competency",
     ("calligraphy", "painting", "cooking")),
    ("upbringings", "goliath/general", "clan-craft", None),
    ("upbringings", "street-raised", "street-raised-community-competency",
     ("disguise", "cooking", "forgery")),
)


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


def grants(record, id_, options):
    outer = next(x for x in record["benefits"] if x["id"] == id_)
    if options is None:
        return [outer]
    selected = (options,) if isinstance(options, str) else options
    return [entry for option in outer["options"] if option["id"] in selected
            for entry in option["benefits"]]


class CharacterOriginsCompetencyConversionTests(unittest.TestCase):
    def test_explicit_effects_are_valid_when_authored(self):
        """Unallocated grants are allowed; an authored zero-effect grant is not."""
        for folder in ("backgrounds", "upbringings"):
            for path in (NATIVE / folder).rglob("*.json"):
                record = json.loads(path.read_text(encoding="utf-8"))
                for node in nodes(record):
                    if node.get("grantType") != "competency-training":
                        continue
                    with self.subTest(origin=record["id"],
                                      target=node.get("competency")):
                        if "competencyTraining" not in node:
                            continue
                        effect = node["competencyTraining"]
                        self.assertEqual({"ranks", "proficient"}, set(effect))
                        self.assertIs(type(effect["ranks"]), int)
                        self.assertGreaterEqual(effect["ranks"], 0)
                        self.assertIs(type(effect["proficient"]), bool)
                        self.assertTrue(effect["ranks"] or effect["proficient"])

    def test_reviewed_earlier_background_benefits_contribute_one_rank(self):
        for name, benefits in CONVERTED_BACKGROUNDS.items():
            record = read("backgrounds", name)
            with self.subTest(background=name):
                self.assertEqual("background." + name, record["id"])
                for id_, options in benefits.items():
                    entries = grants(record, id_, options)
                    self.assertTrue(entries)
                    for entry in entries:
                        self.assertEqual("competency-training", entry["grantType"])
                        self.assertEqual({"ranks": 1, "proficient": False},
                                         entry["competencyTraining"])

    def test_reviewed_earlier_upbringing_benefits_grant_proficiency(self):
        for name, benefits in CONVERTED_UPBRINGINGS.items():
            record = read("upbringings", name)
            with self.subTest(upbringing=name):
                self.assertTrue(record["id"].startswith("upbringing."))
                for id_, options in benefits.items():
                    entries = grants(record, id_, options)
                    self.assertTrue(entries)
                    for entry in entries:
                        self.assertEqual("competency-training", entry["grantType"])
                        self.assertEqual({"ranks": 0, "proficient": True},
                                         entry["competencyTraining"])

    def test_newly_authored_training_choices_are_not_automatically_converted(self):
        """Preserve approved choices but do not invent an independent grant amount."""
        for kind, name, id_, options in AWAITING_SOURCE_SPECIFIC_ALLOCATION:
            record = read(kind, name)
            with self.subTest(origin=record["id"], benefit=id_):
                entries = grants(record, id_, options)
                self.assertTrue(entries)
                for entry in entries:
                    self.assertEqual("competency-training", entry["grantType"])
                    self.assertNotIn("competencyTraining", entry)

    def test_direct_thieves_tools_conversions_are_equipment_only(self):
        """Historical Thieves' Tools proficiency stays binary across Origin choices."""
        criminal = read("backgrounds", "criminal")
        fixed = next(x for x in criminal["benefits"]
                     if x["id"] == "criminal-thieves-tools")
        self.assertEqual("qualification-training", fixed["grantType"])
        self.assertEqual("equipment.tool.thieves-tools", fixed["equipmentTypeId"])
        self.assertNotIn("competencyTraining", fixed)
        self.assertEqual(
            "equipment.tool.thieves-tools",
            criminal["equipmentProficiencyGrants"]["fixed"][0]["targetId"])

        for kind, name, outer_id in (
            ("backgrounds", "spy", "spy-tradecraft-competency"),
            ("upbringings", "street-raised", "street-raised-community-competency"),
        ):
            record = read(kind, name)
            outer = next(x for x in record["benefits"] if x["id"] == outer_id)
            with self.subTest(origin=record["id"]):
                self.assertEqual("benefit-choice", outer["grantType"])
                self.assertEqual(1, outer["choiceCount"])
                option = next(x for x in outer["options"]
                              if x["id"] == "thieves-tools")
                self.assertEqual(1, len(option["benefits"]))
                training = option["benefits"][0]
                self.assertEqual("qualification-training", training["grantType"])
                self.assertEqual("tool", training["qualificationCategory"])
                self.assertEqual("equipment.tool.thieves-tools",
                                 training["equipmentTypeId"])
                self.assertNotIn("competencyTraining", training)
                self.assertEqual(
                    "equipment.tool.thieves-tools",
                    option["equipmentProficiencyGrants"]["fixed"][0]["targetId"])
                self.assertNotIn("equipmentProficiencyGrants", record)
                for other in outer["options"]:
                    if other["id"] != "thieves-tools":
                        self.assertNotIn("equipmentProficiencyGrants", other)


    def test_origins_with_no_competency_benefit_stay_that_way(self):
        """Neither is normal, not an exception that needs an explicit empty grant."""
        for kind, name in (
            ("backgrounds", "acolyte"), ("backgrounds", "criminal"),
            ("backgrounds", "noble"), ("backgrounds", "sage"),
            ("backgrounds", "folk-hero"), ("backgrounds", "hermit"),
            ("backgrounds", "gladiator"), ("backgrounds", "soldier"),
            ("upbringings", "wilderness-raised"),
            ("upbringings", "giant-foundling"),
        ):
            record = read(kind, name)
            with self.subTest(origin=record["id"]):
                self.assertFalse(any(
                    node.get("grantType") == "competency-training"
                    for node in nodes(record)))

    def test_intentionally_discarded_source_tools_are_not_restored(self):
        """2014 source grants may be omitted through reviewed Origin redesign."""
        for name in ("folk-hero", "hermit"):
            record = read("backgrounds", name)
            with self.subTest(background=name):
                self.assertNotIn("equipmentProficiencyGrants", record)
                self.assertFalse(any(
                    node.get("grantType") in
                    ("competency-training", "qualification-training")
                    for node in nodes(record)))
        gladiator = read("backgrounds", "gladiator")
        qualifications = [b for b in gladiator["benefits"]
                          if b.get("grantType") == "qualification-training"]
        self.assertEqual(1, len(qualifications))
        self.assertEqual("weapon", qualifications[0]["qualificationCategory"])
        self.assertFalse(any(
            b.get("grantType") == "competency-training"
            for b in gladiator["benefits"]))

    def test_mixed_choices_keep_equipment_and_competency_effects_separate(self):
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
                        for b in option["benefits"]))


if __name__ == "__main__":
    unittest.main()
