"""Character Origins: native equipment grants without reviving tool Competencies.

These tests validate authored Origin data. The equipment evaluator does not
authorize Origin selections or award Competency ranks; those boundaries remain
separate from the structural contract checked here.
"""
import json
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"


def load(relative):
    return json.loads((NATIVE / relative).read_text(encoding="utf-8"))


def benefit(record, benefit_id):
    return next(x for x in record["benefits"] if x["id"] == benefit_id)


def choice_option(record, benefit_id, option_id):
    return next(x for x in benefit(record, benefit_id)["options"]
                if x["id"] == option_id)


def visit(value):
    """Walk nested grants and chosen options without treating them as earned."""
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from visit(child)
    elif isinstance(value, list):
        for child in value:
            yield from visit(child)


def strings(value):
    """Yield canonical references even from lists of selectable alternatives."""
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from strings(child)


class CharacterOriginsEquipmentContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        catalog = load("equipment-qualifications.json")
        cls.equipment_types = {x["id"]: x for x in catalog["equipmentTypes"]}
        cls.equipment_groups = {x["id"]: x for x in catalog["equipmentGroups"]}
        cls.skills = {x["id"] for x in load("skills.json")["skills"]}
        cls.competencies = {x["id"] for x in load("competencies.json")["competencies"]}
        cls.backgrounds = {
            x["id"]: x
            for p in sorted((NATIVE / "backgrounds").glob("*.json"))
            for x in [json.loads(p.read_text(encoding="utf-8"))]
        }
        cls.upbringings = {
            x["id"]: x
            for p in sorted((NATIVE / "upbringings").rglob("*.json"))
            for x in [json.loads(p.read_text(encoding="utf-8"))]
        }

    def test_all_native_origin_training_ids_are_canonical(self):
        """Do not permit an obsolete Skill or tool Competency back into the corpus."""
        for record in (*self.backgrounds.values(), *self.upbringings.values()):
            with self.subTest(origin=record["id"]):
                for value in strings(record):
                    if value.startswith("competency."):
                        self.assertIn(value, self.competencies)
                    if value.startswith("skill."):
                        self.assertIn(value, self.skills)
                    self.assertNotEqual("competency.thieves-tools", value)

    def test_authored_equipment_targets_are_known_and_category_consistent(self):
        """Validate root and nested-option grants; do not infer missing catalog types."""
        known = {**self.equipment_types, **self.equipment_groups}
        for record in (*self.backgrounds.values(), *self.upbringings.values()):
            for node in visit(record):
                grant = node.get("equipmentProficiencyGrants")
                if not grant:
                    continue
                with self.subTest(origin=record["id"], node=node.get("id")):
                    self.assertTrue(grant.get("fixed") or grant.get("choices"))
                    for fixed in grant.get("fixed", []):
                        self.assertIn(fixed["targetId"], known)
                        self.assertNotIn("/", fixed["id"])
                    for choice in grant.get("choices", []):
                        self.assertGreater(choice["count"], 0)
                        self.assertNotIn("/", choice["id"])
                        category = choice["fromCategory"]
                        for group_id in choice.get("fromGroupIds", []):
                            self.assertIn(group_id, self.equipment_groups)
                            self.assertEqual(category,
                                             self.equipment_groups[group_id]["equipmentCategory"])
                        for type_id in choice.get("fromTypeIds", []):
                            self.assertIn(type_id, self.equipment_types)
                            self.assertEqual(category,
                                             self.equipment_types[type_id]["equipmentCategory"])

    def test_fixed_criminal_thieves_tools_and_type_specific_gaming_choice(self):
        criminal = self.backgrounds["background.criminal"]
        grant = criminal["equipmentProficiencyGrants"]
        self.assertEqual(
            [{"id": "criminal-thieves-tools", "targetId": "equipment.tool.thieves-tools"}],
            grant["fixed"])
        self.assertEqual(["equipment.group.tool.gaming-set"],
                         grant["choices"][0]["fromGroupIds"])
        self.assertEqual(1, grant["choices"][0]["count"])
        training = benefit(criminal, "criminal-thieves-tools")
        self.assertEqual("qualification-training", training["grantType"])
        self.assertEqual("equipment.tool.thieves-tools", training["equipmentTypeId"])
        self.assertNotIn("competency", training)
        # The qualification is narrow and does not grant the Open Lock Skill.
        self.assertNotIn("skill", training)

    def test_direct_background_gaming_and_leisure_choices_keep_one_type(self):
        cases = (
            ("background.criminal", "criminal-gaming-set",
             {"equipment.group.tool.gaming-set"}),
            ("background.noble", "noble-gaming-set",
             {"equipment.group.tool.gaming-set"}),
            ("background.soldier", "soldier-gaming-set",
             {"equipment.group.tool.gaming-set"}),
            ("background.knight", "knight-leisure-training",
             {"equipment.group.tool.gaming-set",
              "equipment.group.tool.musical-instrument"}),
        )
        for source_id, grant_id, groups in cases:
            with self.subTest(origin=source_id):
                record = self.backgrounds[source_id]
                self.assertEqual("qualification-training",
                                 benefit(record, grant_id)["grantType"])
                choices = [x for x in record["equipmentProficiencyGrants"]["choices"]
                           if x["id"] == grant_id]
                self.assertEqual(1, len(choices))
                choice = choices[0]
                self.assertEqual("tool", choice["fromCategory"])
                self.assertEqual(1, choice["count"])
                self.assertEqual(groups, set(choice["fromGroupIds"]))
                self.assertEqual("equipment-type",
                                 choice.get("selectionKind", "equipment-type"))

    def test_mixed_choices_are_option_scoped_and_exclusive(self):
        cases = (
            ("background.spy", "spy-tradecraft-competency", "thieves-tools",
             "equipment.tool.thieves-tools", 4),
            ("upbringing.street-raised", "street-raised-community-competency",
             "thieves-tools", "equipment.tool.thieves-tools", 4),
            ("background.entertainer", "entertainer-performance-method",
             "musical-instrument", "equipment.group.tool.musical-instrument", 2),
            ("upbringing.aristocratic", "aristocratic-formative-competency",
             "gaming-set", "equipment.group.tool.gaming-set", 4),
        )
        for origin_id, benefit_id, equipment_option, target_id, number in cases:
            with self.subTest(origin=origin_id):
                record = (self.backgrounds | self.upbringings)[origin_id]
                outer = benefit(record, benefit_id)
                self.assertEqual("benefit-choice", outer["grantType"])
                self.assertEqual(1, outer["choiceCount"])
                self.assertEqual(number, len(outer["options"]))
                self.assertNotIn("equipmentProficiencyGrants", record,
                                 "Mixed options can not grant equipment unconditionally")
                matching = [x for x in outer["options"]
                            if "equipmentProficiencyGrants" in x]
                self.assertEqual([equipment_option], [x["id"] for x in matching])
                nested = matching[0]["equipmentProficiencyGrants"]
                if target_id.startswith("equipment.group."):
                    self.assertEqual(1, len(nested["choices"]))
                    self.assertEqual([target_id], nested["choices"][0]["fromGroupIds"])
                    self.assertEqual(1, nested["choices"][0]["count"])
                else:
                    self.assertEqual(1, len(nested["fixed"]))
                    self.assertEqual(target_id, nested["fixed"][0]["targetId"])
                for option in outer["options"]:
                    if option["id"] != equipment_option:
                        self.assertEqual(1, len(option["benefits"]))
                        self.assertEqual("competency-training",
                                         option["benefits"][0]["grantType"])
                        self.assertIn(option["benefits"][0]["competency"],
                                      self.competencies)

    def test_ordinary_competencies_do_not_imply_separate_tool_grants(self):
        for origin_id, trained_id in (
            ("background.charlatan", "charlatan-forgery"),
            ("background.sailor", "sailor-navigation"),
            ("upbringing.gnome.rock", "tinkering"),
        ):
            with self.subTest(origin=origin_id):
                record = (self.backgrounds | self.upbringings)[origin_id]
                trained = benefit(record, trained_id)
                self.assertEqual("competency-training", trained["grantType"])
                self.assertIn(trained["competency"], self.competencies)
                self.assertNotIn("equipmentProficiencyGrants", record)

    def test_vehicle_grants_are_not_mislabeled_as_tools(self):
        for origin_id, benefit_id, vehicle_type in (
            ("background.soldier", "soldier-land-vehicles", "land"),
            ("background.sailor", "sailor-water-vehicles", "water"),
        ):
            with self.subTest(origin=origin_id):
                vehicle = benefit(self.backgrounds[origin_id], benefit_id)
                self.assertEqual("vehicle", vehicle["qualificationCategory"])
                self.assertEqual(vehicle_type, vehicle["vehicleType"])
                self.assertNotIn("equipmentTypeId", vehicle)


if __name__ == "__main__":
    unittest.main()
