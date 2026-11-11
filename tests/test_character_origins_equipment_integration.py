"""Character Origins: native equipment grants without reviving tool Competencies.

These tests validate authored Origin data. The equipment evaluator does not
authorize Origin selections or award Competency ranks; those boundaries remain
separate from the structural contract checked here.
"""
import json
from pathlib import Path
import sys
import unittest


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from validate_ruleset_split import matches_preserved_working_evidence

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

    def test_approved_backgrounds_match_preserved_source_divergence_ledger(self):
        """Selected 2014-first Origins must not overwrite historical source evidence."""
        for name in ("acolyte", "criminal", "sage", "soldier"):
            with self.subTest(background=name):
                native = self.backgrounds["background." + name]
                source_path = (ROOT / "data/rulesets/first-party-001/working"
                               / "backgrounds" / (name + ".json"))
                working = json.loads(source_path.read_text(encoding="utf-8"))
                self.assertTrue(
                    matches_preserved_working_evidence(native, working, source_path),
                    "Record the exact approved native/source difference in the "
                    "promotion ledger; never rewrite historical working evidence")

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

    def test_entertainer_item_obeys_selected_performance_method(self):
        """One physical item follows the outer benefit option and any inner type.

        These checks validate source-authored relationships, not the future
        source-ownership authorization or real inventory materialization.
        """
        entertainer = self.backgrounds["background.entertainer"]
        outer = benefit(entertainer, "entertainer-performance-method")
        self.assertEqual("benefit-choice", outer["grantType"])
        self.assertEqual(1, outer["choiceCount"])
        options = {option["id"]: option for option in outer["options"]}
        self.assertEqual({"disguise-kit", "musical-instrument"}, set(options))

        item = self.assert_single(entertainer["startingEquipment"]["items"][:1])
        self.assertEqual("Performance item", item["name"])
        self.assertEqual(1, item["quantity"])
        self.assertEqual(1, item["choiceCount"])
        self.assertEqual(outer["id"], item["sourceBenefitChoiceId"])
        branches = item["conditionalItemChoices"]
        self.assertEqual(len(options), len(branches))
        self.assertEqual(set(options), {branch["whenOptionId"]
                                        for branch in branches})
        self.assertEqual(len(branches),
                         len({branch["whenOptionId"] for branch in branches}))

        disguise = next(x for x in branches if x["whenOptionId"] == "disguise-kit")
        self.assertEqual(["Disguise Kit", "Mundane performance prop"],
                         disguise["ordinaryItemOptions"])
        self.assertEqual({"mundane": True}, disguise["itemRequirements"])
        self.assertNotIn("equipmentTypeChoiceId", disguise)
        self.assertNotIn("equipmentProficiencyGrants", options["disguise-kit"])
        disguise_training = self.assert_single(options["disguise-kit"]["benefits"])
        self.assertEqual("competency.disguise", disguise_training["competency"])
        self.assertEqual({"ranks": 1, "proficient": False},
                         disguise_training["competencyTraining"])

        instrument = next(x for x in branches
                          if x["whenOptionId"] == "musical-instrument")
        grant = self.assert_single(options["musical-instrument"][
            "equipmentProficiencyGrants"]["choices"])
        self.assertEqual("entertainer-musical-instrument", grant["id"])
        self.assertEqual(grant["id"], instrument["equipmentTypeChoiceId"])
        training = self.assert_single(options["musical-instrument"]["benefits"])
        self.assertEqual(grant["id"], training["equipmentTypeChoiceId"])
        self.assertEqual("equipment-type",
                         grant.get("selectionKind", "equipment-type"))
        self.assertEqual(["equipment.group.tool.musical-instrument"],
                         grant["fromGroupIds"])
        self.assertEqual(1, grant["count"])
        self.assertEqual({"sameBaseEquipmentTypeAsChoice": True,
                          "mundane": True}, instrument["itemRequirements"])
        self.assertNotIn("ordinaryItemOptions", instrument)
        self.assertNotIn("equipmentProficiencyGrants", entertainer)
        self.assertNotIn("weaponMasteryGrants", entertainer)
        self.assertNotIn("itemId", item)

    def test_vehicle_operations_remain_distinct_source_qualifications(self):
        """Vehicle training is neither a tool nor a ranked Competency.

        Do not route an Origin's land/water operator training through the
        existing binary equipment training or weapon Mastery contracts.
        """
        expected = {
            "background.soldier": ("soldier-land-vehicles", "land"),
            "background.sailor": ("sailor-water-vehicles", "water"),
        }
        for source_id, (benefit_id, scope) in expected.items():
            with self.subTest(origin=source_id):
                origin = self.backgrounds[source_id]
                vehicle_grants = [x for x in origin["benefits"]
                                  if x.get("qualificationCategory") == "vehicle"]
                self.assertEqual([benefit_id], [x["id"] for x in vehicle_grants])
                training = vehicle_grants[0]
                self.assertEqual("qualification-training", training["grantType"])
                self.assertEqual(scope, training["vehicleType"])
                self.assertNotIn("equipmentTypeId", training)
                self.assertNotIn("competencyTraining", training)
                self.assertNotIn("weaponMasteryGrants", origin)
                equipment = origin.get("equipmentProficiencyGrants", {})
                for group in equipment.get("fixed", []):
                    self.assertNotIn("vehicle", group["targetId"])
                for choice in equipment.get("choices", []):
                    self.assertNotEqual("vehicle", choice["fromCategory"])
                self.assertNotIn("vehicle", self.equipment_groups)
                self.assertFalse(any(x["equipmentCategory"] == "vehicle"
                                     for x in self.equipment_types.values()))

    def test_guild_merchant_trade_language_is_exclusive_of_navigation(self):
        """The fifth mixed-domain benefit is language OR Competency, not both.

        This tests the approved authored shape, not the future Character
        authorization runtime. A selected branch must suppress the other.
        """
        merchant = self.backgrounds["background.guild-merchant"]
        outer = benefit(merchant, "guild-merchant-professional-choice")
        self.assertEqual("benefit-choice", outer["grantType"])
        self.assertEqual(1, outer["choiceCount"])
        options = {x["id"]: x for x in outer["options"]}
        self.assertEqual({"navigation", "additional-trade-language"}, set(options))

        nav = self.assert_single(options["navigation"]["benefits"])
        self.assertEqual("competency-training", nav["grantType"])
        self.assertEqual("competency.navigation", nav["competency"])
        self.assertEqual({"ranks": 1, "proficient": False},
                         nav["competencyTraining"])
        self.assertNotIn("extraLanguages", nav)

        language = self.assert_single(
            options["additional-trade-language"]["benefits"])
        self.assertEqual(1, language["extraLanguages"])
        self.assertEqual("learned-through-commercial-dealings",
                         language["choiceConstraint"])
        self.assertNotIn("competency", language)
        self.assertNotIn("competencyTraining", language)

        for option in options.values():
            self.assertNotIn("equipmentProficiencyGrants", option)
        self.assertNotIn("equipmentProficiencyGrants", merchant)
        self.assertNotIn("weaponMasteryGrants", merchant)

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

    def test_elf_cultural_weapon_training_uses_canonical_base_types(self):
        """Inherited Elf upbringing grants apply by weapon type, not family.

        Existing cultural training does not imply Weapon Mastery and must not
        depend on legacy display-name weaponProficiencies arrays.
        """
        cases = (
            ("elf/high", "traditional-weapons",
             ("longsword", "shortsword", "shortbow", "longbow")),
            ("elf/wood", "woodland-archery",
             ("shortbow", "longbow")),
        )
        for name, benefit_id, weapon_names in cases:
            with self.subTest(upbringing=name):
                origin = load("upbringings/" + name + ".json")
                grant = origin["equipmentProficiencyGrants"]
                self.assertNotIn("choices", grant)
                self.assertEqual(
                    {"equipment.weapon." + x for x in weapon_names},
                    {x["targetId"] for x in grant["fixed"]},
                )
                self.assertEqual(len(weapon_names), len(grant["fixed"]))
                self.assertEqual("qualification-training",
                                 benefit(origin, benefit_id)["grantType"])
                self.assertEqual("weapon",
                                 benefit(origin, benefit_id)["qualificationCategory"])
                self.assertNotIn("weaponProficiencies",
                                 benefit(origin, benefit_id))
                self.assertNotIn("weaponMasteryGrants", origin)
                for item in grant["fixed"]:
                    self.assertIn(item["targetId"], self.equipment_types)
                    self.assertEqual(
                        "weapon",
                        self.equipment_types[item["targetId"]]["equipmentCategory"],
                    )
                    self.assertFalse(
                        item["targetId"].startswith("equipment.group."),
                        "Specific weapon proficiency must not cover a family",
                    )

    def test_gladiator_selects_one_base_weapon_without_weapon_mastery(self):
        """A selected weapon type is not a family, category, or mastery."""
        gladiator = self.backgrounds["background.gladiator"]
        arena = benefit(gladiator, "gladiator-arena-weapon")
        self.assertEqual("qualification-training", arena["grantType"])
        self.assertEqual("weapon", arena["qualificationCategory"])
        self.assertEqual(1, arena["choiceCount"])
        grant = gladiator["equipmentProficiencyGrants"]
        self.assertNotIn("fixed", grant)
        choice = self.assert_single(grant["choices"])
        self.assertEqual("arena-weapon", choice["id"])
        self.assertEqual("equipment-type",
                         choice.get("selectionKind", "equipment-type"))
        self.assertEqual("weapon", choice["fromCategory"])
        self.assertEqual(1, choice["count"])
        self.assertEqual(["equipment.group.weapon.all"],
                         choice["fromGroupIds"])
        # The arena restriction is a contextual authorization gate. A static
        # type allowlist would silently exclude otherwise suitable weapons.
        self.assertNotIn("fromTypeIds", choice)
        self.assertNotIn("weaponMasteryGrants", gladiator)
        # The single authorized selection must feed both the narrow
        # proficiency grant and one ordinary inventory entry. Neither the
        # inventory item nor the All Weapons candidate pool confers mastery
        # or proficiency with any other type.
        inventory_item = gladiator["startingEquipment"]["items"][0]
        self.assertEqual(choice["id"], arena["equipmentTypeChoiceId"])
        self.assertEqual(choice["id"], inventory_item["equipmentTypeChoiceId"])
        self.assertEqual(1, inventory_item["quantity"])
        self.assertEqual({
            "sameBaseWeaponTypeAsChoice": True,
            "mundane": True,
            "inexpensive": True,
            "arenaAppropriate": True,
        }, inventory_item["itemRequirements"])
        self.assertIn("same-specific-mundane-arena-appropriate-weapon-type",
                      inventory_item["choiceConstraint"])
        # The native taxonomy only enumerates candidate base types. The
        # future Origin choice executor must establish arena suitability,
        # actual item identity, price, and mundane status before selection.
        self.assertNotIn("itemId", inventory_item)
        self.assertNotIn("equipmentTypeId", inventory_item)
        self.assertFalse(any(x.startswith("equipment.group.weapon.")
                             for x in (choice.get("fromTypeIds") or [])))

    def assert_single(self, values):
        self.assertEqual(1, len(values))
        return values[0]

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
