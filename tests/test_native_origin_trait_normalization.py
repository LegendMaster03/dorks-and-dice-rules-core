"""Integrity checks for independently stored inherent trait definitions."""
import json
from pathlib import Path
import unittest

NATIVE = (Path(__file__).resolve().parents[1]
          / "data/rulesets/first-party-001/native")


def records(path):
    value = json.loads(path.read_text(encoding="utf-8"))
    return value if isinstance(value, list) else [value]


class NativeOriginTraitNormalizationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = {
            row["id"]: row
            for path in (NATIVE / "species").rglob("*.json")
            for row in records(path)
        }
        cls.definitions = {}
        for path in (NATIVE / "traits").rglob("*.json"):
            for record in records(path):
                identity = record["id"]
                if identity in cls.definitions:
                    raise AssertionError(f"Duplicate trait definition {identity}")
                cls.definitions[identity] = record

    def test_every_source_occurrence_resolves_exactly_once(self):
        self.assertTrue(self.definitions)
        referenced = set()
        for source_id, source in self.sources.items():
            with self.subTest(source=source_id):
                self.assertIn(source["kind"], ("species", "ancestry"))
                occurrences = source.get("traits", [])
                self.assertIsInstance(occurrences, list)
                local_ids = set()
                for occurrence in occurrences:
                    self.assertEqual({"traitId"}, set(occurrence))
                    definition_id = occurrence["traitId"]
                    self.assertIn(definition_id, self.definitions)
                    definition = self.definitions[definition_id]
                    self.assertEqual("origin-trait", definition["kind"])
                    self.assertTrue(definition["name"])
                    self.assertEqual(definition_id, definition["id"])
                    self.assertIsInstance(definition["trait"], dict)
                    local_id = definition["trait"]["id"]
                    self.assertNotIn(local_id, local_ids)
                    local_ids.add(local_id)
                    referenced.add(definition_id)
        self.assertEqual(set(self.definitions), referenced,
                         "Orphaned trait definitions must not enter the active catalog")

    def test_existing_species_and_ancestry_parent_chains(self):
        for source_id, source in self.sources.items():
            if source["kind"] == "species":
                self.assertNotIn("parent", source)
                continue
            ancestry_chain = {source_id}
            current = source
            while current["kind"] != "species":
                parent_id = current.get("parent")
                self.assertIn(parent_id, self.sources)
                self.assertNotIn(parent_id, ancestry_chain)
                ancestry_chain.add(parent_id)
                current = self.sources[parent_id]

    def test_shared_definitions_have_distinct_occurrences(self):
        owners = {}
        for source in self.sources.values():
            for index, item in enumerate(source.get("traits", [])):
                owners.setdefault(item["traitId"], []).append((source["id"], index))
        self.assertEqual(4, len(owners["trait.shared.low-light-vision"]))
        self.assertEqual(4, len(owners["trait.shared.amphibious"]))
        self.assertEqual(1, len(owners["trait.goliath.powerful-build"]))
        self.assertEqual(1, len(owners["trait.orc.powerful-build"]))
        self.assertNotIn("trait.shared.powerful-build", owners)

    def test_approved_choice_and_parameter_shapes_remain_in_definitions(self):
        def trait(identity):
            return self.definitions[identity]["trait"]

        human = trait("trait.human.skillful")
        self.assertEqual("any-native-skill", human["chooseFrom"])
        self.assertEqual(1, human["choiceCount"])
        for ancestry in ("high", "wood"):
            source = self.sources[f"ancestry.elf.{ancestry}"]
            self.assertEqual(["intelligence", "wisdom", "charisma"],
                             source["spellcastingAbilityChoice"])
            for occurrence in source["traits"]:
                value = trait(occurrence["traitId"])
                self.assertEqual("manifested-intrinsic",
                                 value["originBindings"]["spellcastingAbility"]["lookup"])
                self.assertEqual("source",
                                 value["originBindings"]["spellcastingAbility"]["scope"])

        breath = trait("trait.dragonborn.breath-weapon")
        self.assertEqual("leaf", breath["originBindings"]["damageType"]["lookup"])
        self.assertEqual("nearest", breath["originBindings"]["area"]["lookup"])
        self.assertEqual(["damageType", "area"],
                         list(breath["originBindings"]))
        self.assertEqual(4, len(breath["damage"]["progression"]))
        self.assertEqual("leaf",
                         trait("trait.dragonborn.draconic-resistance")[
                             "originBindings"]["damageType"]["lookup"])


if __name__ == "__main__":
    unittest.main()
