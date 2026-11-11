"""Shared native crafting-method category used by Guild Artisan.

This checks data eligibility only. A character must still select a guild trade;
a category membership is neither proficiency nor rank advancement by itself.
"""
import json
import unittest
from pathlib import Path


NATIVE = (Path(__file__).resolve().parents[1]
          / "data/rulesets/first-party-001/native")


def read(relative):
    return json.loads((NATIVE / relative).read_text(encoding="utf-8"))


class CharacterOriginsCraftingCategoryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        catalog = read("competencies.json")
        cls.competencies = {c["id"]: c for c in catalog["competencies"]}
        cls.categories = {c["id"]: c for c in catalog["competencyCategories"]}
        cls.artisan = read("backgrounds/guild-artisan.json")
        cls.trade = next(x for x in cls.artisan["benefits"]
                         if x["id"] == "guild-artisan-trade")

    def test_guild_trade_uses_shared_authoritative_category(self):
        self.assertEqual("competency-category.crafting-method",
                         self.trade["choiceCategoryId"])
        category = self.categories[self.trade["choiceCategoryId"]]
        self.assertEqual("competency-category", category["kind"])
        self.assertEqual("crafting-competency", category["choiceCategory"])
        self.assertEqual(category["choiceCategory"],
                         self.trade["choiceCategory"])
        self.assertEqual(1, self.trade["choiceCount"])
        self.assertEqual({"ranks": 1, "proficient": False},
                         self.trade["competencyTraining"])
        self.assertNotIn("equipmentProficiencyGrants", self.artisan)

    def test_category_references_existing_methods_and_not_product_types(self):
        category = self.categories["competency-category.crafting-method"]
        members = category["competencyIds"]
        self.assertEqual(len(members), len(set(members)))
        self.assertTrue(set(members) <= self.competencies.keys())
        self.assertTrue({
            "competency.smithing", "competency.carpentry",
            "competency.woodcarving", "competency.leatherworking",
            "competency.weaving", "competency.calligraphy",
            "competency.cartography", "competency.cooking",
            "competency.tinkering", "competency.stonemasonry",
        } <= set(members))
        self.assertFalse({
            "competency.navigation", "competency.disguise",
            "competency.forgery", "competency.poisoning",
            "competency.bowmaking", "competency.weaponsmithing",
            "competency.armorsmithing", "competency.shipmaking",
            "competency.trapmaking",
        } & set(members))

    def test_examples_are_members_but_not_a_second_eligibility_source(self):
        category = self.categories[self.trade["choiceCategoryId"]]
        self.assertTrue(set(self.trade["exampleCompetencies"])
                        <= set(category["competencyIds"]))
        self.assertGreater(len(category["competencyIds"]),
                           len(self.trade["exampleCompetencies"]))


if __name__ == "__main__":
    unittest.main()
