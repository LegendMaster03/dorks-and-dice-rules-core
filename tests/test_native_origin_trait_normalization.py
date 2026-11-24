"""Integrity checks for independently stored inherent trait definitions."""
import json
from pathlib import Path
import tempfile
import unittest

from tools.native_origin_trait_projection import expand_species_traits

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
            for path in (NATIVE / "origination" / "biological" / "species").rglob("*.json")
            for row in records(path)
        }
        cls.definitions = {}
        for path in (NATIVE / "origination" / "biological" / "genes").rglob("*.json"):
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
        self.assertEqual(5, len(owners["trait.low-light-vision"]))
        climbers = owners["trait.natural-climber"]
        self.assertEqual(3, len(climbers))
        self.assertEqual(3, len(set(climbers)))
        self.assertIn("ancestry.gnome.rock", {source for source, _ in climbers})
        self.assertEqual(4, len(owners["trait.amphibious"]))
        self.assertEqual(2, len(owners["trait.powerful-build"]))
        self.assertEqual(2, len(owners["trait.darkness"]))

    def test_unified_powerful_build_has_both_mechanical_benefits(self):
        trait = self.definitions["trait.powerful-build"]["trait"]
        self.assertEqual("condition.grappled",
                         trait["advantageOnAbilityChecksToEndCondition"])
        self.assertEqual(1, trait["effectiveSizeCategoriesHigherForCarryingCapacity"])
        self.assertEqual(1, trait["effectiveSizeCategoriesHigherForPushingDraggingLifting"])
        for species in ("goliath", "orc"):
            self.assertIn({"traitId": "trait.powerful-build"},
                          self.sources[f"species.{species}"]["traits"])

    def test_darkness_retains_its_casting_resource_and_source_bindings(self):
        trait = self.definitions["trait.darkness"]["trait"]
        self.assertEqual("darkness", trait["spell"])
        self.assertEqual(5, trait["level"])
        self.assertEqual(1, trait["freeUses"])
        self.assertEqual("long-rest", trait["recharge"])
        self.assertTrue(trait["slotCasting"])
        self.assertEqual("manifested-intrinsic",
                         trait["originBindings"]["spellcastingAbility"]["lookup"])
        for ancestry in ("ancestry.elf.drow", "ancestry.tiefling.infernal"):
            self.assertIn({"traitId": "trait.darkness"},
                          self.sources[ancestry]["traits"])

    def test_research_projection_reads_changed_trait_content_without_stale_cache(self):
        with tempfile.TemporaryDirectory() as temporary:
            native = Path(temporary) / "native"
            source_path = native / "origination" / "biological" / "species" / "base" / "example.json"
            source_path.parent.mkdir(parents=True)
            trait_path = native / "origination" / "biological" / "genes" / "example.json"
            trait_path.parent.mkdir(parents=True)
            source = {
                "id": "species.example", "kind": "species", "name": "Example",
                "traits": [{"traitId": "trait.example"}],
            }
            source_path.write_text(json.dumps(source), encoding="utf-8")

            def write_trait(local_id):
                trait_path.write_text(json.dumps({
                    "id": "trait.example", "name": "Example",
                    "kind": "origin-trait", "trait": {"id": local_id},
                }), encoding="utf-8")

            write_trait("before")
            self.assertEqual("before",
                             expand_species_traits(source, source_path)["traits"][0]["id"])
            write_trait("after")
            self.assertEqual("after",
                             expand_species_traits(source, source_path)["traits"][0]["id"])
            # Projection must not replace references in the caller's source.
            self.assertEqual([{"traitId": "trait.example"}], source["traits"])

    def test_approved_choice_and_parameter_shapes_remain_in_definitions(self):
        def trait(identity):
            return self.definitions[identity]["trait"]

        human = trait("trait.skillful")
        self.assertEqual("any-native-skill", human["chooseFrom"])
        self.assertEqual(1, human["choiceCount"])
        spell_traits = {
            "high": {"trait.arcane-cantrip", "trait.detect-magic", "trait.misty-step"},
            "wood": {"trait.druidcraft", "trait.longstrider", "trait.pass-without-trace"},
        }
        for ancestry, identities in spell_traits.items():
            source = self.sources[f"ancestry.elf.{ancestry}"]
            self.assertEqual(["intelligence", "wisdom", "charisma"],
                             source["spellcastingAbilityChoice"])
            self.assertTrue(identities <= {occurrence["traitId"]
                                          for occurrence in source["traits"]})
            for identity in identities:
                value = trait(identity)
                self.assertEqual("manifested-intrinsic",
                                 value["originBindings"]["spellcastingAbility"]["lookup"])
                self.assertEqual("source",
                                 value["originBindings"]["spellcastingAbility"]["scope"])

        breath = trait("trait.breath-weapon")
        self.assertEqual("leaf", breath["originBindings"]["damageType"]["lookup"])
        self.assertEqual("nearest", breath["originBindings"]["area"]["lookup"])
        self.assertEqual(["damageType", "area"],
                         list(breath["originBindings"]))
        self.assertEqual(4, len(breath["damage"]["progression"]))
        self.assertEqual("nearest",
                         trait("trait.damage-type-resistance")[
                             "originBindings"]["damageType"]["lookup"])


if __name__ == "__main__":
    unittest.main()
