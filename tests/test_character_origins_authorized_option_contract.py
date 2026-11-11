"""Static conformance tests for the proposed trusted Origin grant projection.

The examples under docs/rewrite describe the *approved native grants* that
a future authoritative selector may emit. This test never authenticates an
owned character, grants an equipment source, or materializes an item.
"""
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"
EXAMPLES = (ROOT / "docs/rewrite/character-origins"
            / "pillar3-authorized-option-cases-20261110.json")


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def unique_records(folder):
    """Index native source identities, rejecting duplicate IDs in the tree."""
    indexed = {}
    for path in sorted((NATIVE / folder).rglob("*.json")):
        record = read(path)
        identifier = record["id"]
        if identifier in indexed:
            raise AssertionError(f"Duplicate source {identifier}: {path}")
        indexed[identifier] = record
    return indexed


class AuthorizedOriginOptionContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = read(EXAMPLES)
        cls.sources = (unique_records("backgrounds")
                       | unique_records("upbringings"))
        catalog = read(NATIVE / "equipment-qualifications.json")
        cls.types = {entry["id"]: entry for entry in catalog["equipmentTypes"]}
        cls.groups = {entry["id"]: entry for entry in catalog["equipmentGroups"]}
        cls.competencies = {
            entry["id"] for entry in read(NATIVE / "competencies.json")["competencies"]
        }

    def test_matrix_anchors_all_five_exclusive_source_choices(self):
        """An example is a reference to one native choice, not a new rule."""
        self.assertEqual(1, self.cases["contractVersion"])
        case_keys = {(case["sourceId"], case["benefitId"])
                     for case in self.cases["choices"]}
        self.assertEqual(5, len(case_keys))
        self.assertEqual({
            ("background.entertainer", "entertainer-performance-method"),
            ("background.guild-merchant", "guild-merchant-professional-choice"),
            ("background.spy", "spy-tradecraft-competency"),
            ("upbringing.aristocratic", "aristocratic-formative-competency"),
            ("upbringing.street-raised", "street-raised-community-competency"),
        }, case_keys)
        for case in self.cases["choices"]:
            with self.subTest(source=case["sourceId"]):
                source = self.sources[case["sourceId"]]
                matching = [benefit for benefit in source["benefits"]
                            if benefit.get("id") == case["benefitId"]]
                self.assertEqual(1, len(matching))
                benefit = matching[0]
                self.assertEqual("benefit-choice", benefit["grantType"])
                self.assertEqual(1, case["count"])
                self.assertEqual(case["count"], benefit["choiceCount"])
                self.assertNotIn("equipmentProficiencyGrants", source,
                                 "A nested branch must not grant equipment at its parent root")
                authored = benefit["options"]
                self.assertEqual(len(authored),
                                 len({option["id"] for option in authored}))
                self.assertEqual(
                    {option["id"] for option in authored},
                    {option["optionId"] for option in case["options"]})
                self.assertEqual(len(case["options"]),
                                 len({option["optionId"] for option in case["options"]}))

    def test_each_exclusive_branch_has_only_its_approved_grant(self):
        """The option chosen controls the grant; sibling options do not."""
        for case in self.cases["choices"]:
            source = self.sources[case["sourceId"]]
            benefit = next(x for x in source["benefits"]
                           if x["id"] == case["benefitId"])
            by_id = {x["id"]: x for x in benefit["options"]}
            grant_ids = set()
            for expected in case["options"]:
                with self.subTest(source=case["sourceId"],
                                  option=expected["optionId"]):
                    option = by_id[expected["optionId"]]
                    effects = option["benefits"]
                    self.assertEqual(1, len(effects))
                    effect = effects[0]
                    rule = expected["effect"]
                    domain = rule["domain"]
                    if domain == "competency":
                        self.assertEqual("competency-training", effect["grantType"])
                        self.assertEqual(rule["targetId"], effect["competency"])
                        self.assertIn(rule["targetId"], self.competencies)
                        self.assertEqual({
                            "ranks": rule["ranks"],
                            "proficient": rule["proficient"],
                        }, effect["competencyTraining"])
                        self.assertNotIn("equipmentProficiencyGrants", option)
                        self.assertNotIn("extraLanguages", effect)
                    elif domain == "equipment":
                        self.assertEqual("qualification-training",
                                         effect["grantType"])
                        grants = option["equipmentProficiencyGrants"]
                        self.assertEqual({"fixed" if rule["grantKind"] == "fixed"
                                          else "choices"}, set(grants))
                        if rule["grantKind"] == "fixed":
                            grant, = grants["fixed"]
                            self.assertEqual(rule["grantId"], grant["id"])
                            self.assertEqual(rule["targetId"], grant["targetId"])
                            self.assertIn(rule["targetId"], self.types)
                            self.assertEqual("tool", self.types[
                                rule["targetId"]]["equipmentCategory"])
                        else:
                            grant, = grants["choices"]
                            self.assertEqual(rule["grantId"], grant["id"])
                            self.assertEqual(rule["count"], grant["count"])
                            self.assertEqual(rule["category"], grant["fromCategory"])
                            self.assertEqual(rule["fromGroupIds"],
                                             grant["fromGroupIds"])
                            self.assertEqual("equipment-type",
                                             grant.get("selectionKind", "equipment-type"))
                            self.assertNotIn("fromTypeIds", grant)
                            for group in grant["fromGroupIds"]:
                                self.assertIn(group, self.groups)
                                self.assertEqual("tool", self.groups[
                                    group]["equipmentCategory"])
                        self.assertNotIn("competencyTraining", effect)
                        self.assertNotIn("extraLanguages", effect)
                        self.assertNotIn(rule["grantId"], grant_ids)
                        grant_ids.add(rule["grantId"])
                    elif domain == "language":
                        self.assertEqual(rule["count"], effect["extraLanguages"])
                        self.assertEqual(rule["choiceConstraint"],
                                         effect["choiceConstraint"])
                        self.assertNotIn("competency", effect)
                        self.assertNotIn("competencyTraining", effect)
                        self.assertNotIn("equipmentProficiencyGrants", option)
                    else:
                        self.fail(f"Unknown effect domain in examples: {domain}")

    def test_parent_upbringing_is_one_inherited_source_not_an_extra_choice(self):
        """Inheritance provenance must not be represented as a second pick."""
        upbringings = unique_records("upbringings")
        rule = read(NATIVE / "rules/species-subspecies-upbringing.json")
        self.assertEqual(1, rule["upbringings"]["selectedCount"])
        self.assertFalse(rule["originInheritance"]["defaultIsAdditionalSelection"])
        for child_id, child in upbringings.items():
            if "parent" not in child:
                continue
            with self.subTest(child=child_id):
                self.assertTrue(child["selectable"])
                seen = set()
                current = child_id
                while current:
                    self.assertNotIn(current, seen, "Cycle or duplicate parent source")
                    seen.add(current)
                    self.assertIn(current, upbringings)
                    if current != child_id:
                        self.assertFalse(upbringings[current]["selectable"],
                                         "Current inherited parent must not require another selection")
                    current = upbringings[current].get("parent")

    def test_starting_items_do_not_act_as_training_source(self):
        """Matching equipment is a future authorization gate, not a grant."""
        gladiator = self.sources["background.gladiator"]
        entertainer = self.sources["background.entertainer"]
        arena = gladiator["startingEquipment"]["items"][0]
        proficiency = gladiator["equipmentProficiencyGrants"]["choices"][0]
        self.assertEqual(proficiency["id"], arena["equipmentTypeChoiceId"])
        self.assertTrue(arena["itemRequirements"]["sameBaseWeaponTypeAsChoice"])
        self.assertNotIn("equipmentProficiencyGrants", arena)

        performance = entertainer["startingEquipment"]["items"][0]
        options = next(x["options"] for x in entertainer["benefits"]
                       if x["id"] == performance["sourceBenefitChoiceId"])
        self.assertEqual({x["id"] for x in options},
                         {x["whenOptionId"] for x in performance["conditionalItemChoices"]})
        for entry in performance["conditionalItemChoices"]:
            self.assertNotIn("equipmentProficiencyGrants", entry)
        self.assertNotIn("equipmentProficiencyGrants", performance)


if __name__ == "__main__":
    unittest.main()
