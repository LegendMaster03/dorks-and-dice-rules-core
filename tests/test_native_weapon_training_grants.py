"""Native weapon proficiency and mastery source-contract regressions.

These tests validate authored JSON against the same canonical weapon taxonomy
used by the shared equipment resolver. They do not authorize a character to
receive a source or execute Weapon Mastery combat effects.
"""
import json
import unittest
from pathlib import Path


NATIVE = (Path(__file__).resolve().parents[1]
          / "data/rulesets/first-party-001/native")


def read(relative):
    return json.loads((NATIVE / relative).read_text(encoding="utf-8"))


def iter_nodes(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from iter_nodes(child)
    elif isinstance(value, list):
        for child in value:
            yield from iter_nodes(child)


class WeaponTrainingSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        catalog = read("equipment-qualifications.json")
        cls.types = {x["id"]: x for x in catalog["equipmentTypes"]}
        cls.groups = {x["id"]: x for x in catalog["equipmentGroups"]}
        cls.weapons = {
            k: v for k, v in cls.types.items()
            if v["equipmentCategory"] == "weapon"
        }
        # Scan every currently authored native Class, Class feature and
        # Origin; no fixed expected file count in a developing ruleset.
        folders = ("classes", "features/classes", "species", "backgrounds",
                   "upbringings")
        cls.sources = []
        for folder in folders:
            for path in sorted((NATIVE / folder).rglob("*.json")):
                cls.sources.append(json.loads(path.read_text(encoding="utf-8")))

    def test_each_native_weapon_has_one_family_and_classification(self):
        """Family, Simple/Martial and Melee/Ranged are independent axes."""
        for weapon_id, weapon in self.weapons.items():
            groups = weapon["groupIds"]
            with self.subTest(weapon=weapon_id):
                self.assertEqual(1, sum(
                    x.startswith("equipment.group.weapon.family.")
                    for x in groups))
                self.assertEqual(1, sum(
                    x in ("equipment.group.weapon.simple",
                          "equipment.group.weapon.martial") for x in groups))
                self.assertEqual(1, sum(
                    x in ("equipment.group.weapon.melee",
                          "equipment.group.weapon.ranged") for x in groups))
                self.assertEqual(len(groups), len(set(groups)))
                self.assertTrue(set(groups) <= self.groups.keys())

    def test_all_authored_equipment_and_mastery_grants_reference_catalog(self):
        """No display-name inference or unrecognized target/selector IDs."""
        known = self.types.keys() | self.groups.keys()
        for source in self.sources:
            for node in iter_nodes(source):
                profs = node.get("equipmentProficiencyGrants")
                if profs is not None:
                    for field in ("fixed", "multiclassFixed"):
                        for grant in profs.get(field, []):
                            with self.subTest(source=source["id"],
                                              grant=grant["id"]):
                                self.assertIn(grant["targetId"], known)
                    for field in ("choices", "multiclassChoices"):
                        for choice in profs.get(field, []):
                            with self.subTest(source=source["id"],
                                              choice=choice["id"]):
                                self.assertGreaterEqual(choice["count"], 1)
                                category = choice["fromCategory"]
                                kind = choice.get("selectionKind",
                                                  "equipment-type")
                                self.assertIn(kind,
                                              ("equipment-type",
                                               "equipment-group"))
                                allowed_groups = choice.get("fromGroupIds", [])
                                if kind == "equipment-group":
                                    self.assertTrue(allowed_groups)
                                    self.assertNotIn("fromTypeIds", choice)
                                for group_id in allowed_groups:
                                    self.assertIn(group_id, self.groups)
                                    self.assertEqual(
                                        category,
                                        self.groups[group_id]["equipmentCategory"])
                                for type_id in choice.get("fromTypeIds", []):
                                    self.assertIn(type_id, self.types)
                                    self.assertEqual(
                                        category,
                                        self.types[type_id]["equipmentCategory"])
                mastery = node.get("weaponMasteryGrants")
                if mastery is None:
                    continue
                for grant in mastery.get("fixed", []):
                    with self.subTest(source=source["id"], mastery=grant["id"]):
                        target = grant["targetId"]
                        self.assertIn(target, known)
                        row = self.types.get(target, self.groups.get(target))
                        self.assertEqual("weapon", row["equipmentCategory"])
                for choice in mastery.get("choices", []):
                    with self.subTest(source=source["id"],
                                      mastery=choice["id"]):
                        self.assertIs(choice.get("requireProficiency"), True)
                        self.assertNotEqual(
                            "count" in choice,
                            "countByClassLevel" in choice)
                        for group_id in choice.get("fromGroupIds", []):
                            self.assertIn(group_id, self.groups)
                            self.assertEqual(
                                "weapon",
                                self.groups[group_id]["equipmentCategory"])
                        for type_id in choice.get("fromTypeIds", []):
                            self.assertIn(type_id, self.weapons)

    def test_bard_martial_choice_is_one_base_type_not_whole_group(self):
        bard = read("classes/base/bard/bard.json")["definition"]
        grant = bard["equipmentProficiencyGrants"]
        choice, = grant["choices"]
        self.assertEqual("one-martial-weapon", choice["id"])
        self.assertEqual(1, choice["count"])
        self.assertEqual("weapon", choice["fromCategory"])
        self.assertEqual(["equipment.group.weapon.martial"],
                         choice["fromGroupIds"])
        self.assertEqual("equipment-type",
                         choice.get("selectionKind", "equipment-type"))
        self.assertEqual(
            "one-martial-weapon",
            bard["legacySingleMartialWeaponChoice"][
                "implementedByEquipmentGrant"])
        self.assertNotIn("weaponMasteryGrants", bard)

    def test_elf_and_gladiator_grants_do_not_award_mastery(self):
        paths = (
            "upbringings/elf/high.json",
            "upbringings/elf/wood.json",
            "backgrounds/gladiator.json",
        )
        for path in paths:
            record = read(path)
            with self.subTest(path=path):
                self.assertIn("equipmentProficiencyGrants", record)
                self.assertNotIn("weaponMasteryGrants", record)

    def test_mastery_class_features_require_prior_weapon_proficiency(self):
        barbarian = read(
            "features/classes/barbarian/weapon-mastery-level-1.json")
        rogue = read("features/classes/rogue/weapon-mastery-level-1.json")
        barbarian_choice, = barbarian["definition"][
            "weaponMasteryGrants"]["choices"]
        rogue_choice, = rogue["definition"]["weaponMasteryGrants"]["choices"]
        self.assertIs(barbarian_choice["requireProficiency"], True)
        self.assertEqual([[1, 2], [4, 3], [10, 4]],
                         barbarian_choice["countByClassLevel"])
        self.assertEqual(["equipment.group.weapon.melee"],
                         barbarian_choice["fromGroupIds"])
        self.assertIs(rogue_choice["requireProficiency"], True)
        self.assertEqual(2, rogue_choice["count"])
        self.assertNotIn("fromGroupIds", rogue_choice)

    def test_blade_pact_does_not_grant_permanent_weapon_training(self):
        """Bonded-item qualification is contextual, not all melee weapons."""
        pact = read("classes/base/warlock/pacts/blade.json")
        self.assertNotIn("equipmentProficiencyGrants", pact)
        self.assertNotIn("weaponMasteryGrants", pact)
        self.assertIn("While bonded, you are proficient",
                      pact["description"])
        self.assertIn("Weapon Mastery", pact["deepenedAtLevel5"])


if __name__ == "__main__":
    unittest.main()
