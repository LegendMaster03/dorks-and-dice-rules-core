"""Native source choice requirements are evidence, not authorized grants."""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_choice_schema import (  # noqa: E402
    ChoiceSchemaError, audit_native_choice_schema,
)
from character_origins_source_inventory import (  # noqa: E402
    read_catalog, source_lineage,
)


class ChoiceSchemaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.report = audit_native_choice_schema(cls.catalog)

    @staticmethod
    def find(report, source, field, trait=None):
        occurrence = (
            f"{source}#trait:{trait}" if trait is not None
            else None
        )
        records = [
            x for x in report["choiceObservations"]
            if x["sourceId"] == source
            and x["authoredField"] == field
            and (occurrence is None or
                 x["authoredOccurrenceId"] == occurrence)
        ]
        if len(records) != 1:
            raise AssertionError(
                f"Expected one choice {source}/{field}, found {records}"
            )
        return records[0]

    def test_current_corpus_choice_evidence_is_never_authorized(self):
        report = self.report
        self.assertGreater(report["observationCount"], 0)
        self.assertEqual(
            len(report["choiceObservations"]), report["observationCount"]
        )
        self.assertEqual(
            set(self.catalog), set(report["sourceIds"])
        )
        self.assertTrue(all(
            x["selection"] == "not-selected"
            and x["authority"] == "not-authorized"
            for x in report["choiceObservations"]
        ))
        self.assertEqual("not-certified", report["completeness"])
        self.assertEqual(
            "not-authorized", report["authorization"]
        )

    def test_human_skillful_is_an_open_catalog_entitlement(self):
        record = self.find(
            self.report, "species.human", "chooseFrom", "skillful"
        )
        self.assertEqual(
            "catalog-entitlement", record["kind"]
        )
        self.assertEqual(
            "any-native-skill", record["authoredValue"]
        )
        self.assertEqual("character-creation", record["timing"])
        self.assertEqual(1, record["constraints"]["count"])
        self.assertEqual(
            "requires-catalog-resolution", record["schemaStatus"]
        )

    def test_human_versatile_and_allocation_remain_distinct(self):
        feat = self.find(
            self.report, "species.human", "featCategory", "versatile"
        )
        ranks = self.find(
            self.report, "species.human", "allocation", "adaptable-learning"
        )
        self.assertEqual("catalog-entitlement", feat["kind"])
        self.assertEqual("feat", feat["constraints"]["catalogType"])
        self.assertEqual("origin", feat["authoredValue"])
        self.assertEqual("player-allocation", ranks["kind"])
        self.assertEqual(4, ranks["constraints"][
            "additionalRanksAtCharacterLevel1"
        ])
        self.assertEqual(
            1, ranks["constraints"]["additionalRanksPerSubsequentCharacterLevel"]
        )
        self.assertEqual(
            "requires-allocation-policy", ranks["schemaStatus"]
        )

    def test_goliath_ability_choice_is_a_persistent_selection_candidate(self):
        record = self.find(
            self.report, "species.goliath", "abilityChoice",
            "giant-ancestry-aptitude"
        )
        self.assertEqual("finite-ability-choice", record["kind"])
        self.assertEqual(
            ["strength", "constitution"], record["constraints"]["options"]
        )
        self.assertEqual(
            "goliath-ancestry-selection", record["timing"]
        )
        self.assertTrue(record["constraints"]["perCharacter"])
        self.assertNotEqual("on-use", record["timing"])

    def test_high_elf_spell_list_and_ability_choice_are_separate(self):
        ability = self.find(
            self.report, "ancestry.elf.high", "spellcastingAbilityChoice"
        )
        spell = self.find(
            self.report, "ancestry.elf.high", "spellList",
            "arcane-cantrip",
        )
        self.assertEqual("finite-ability-choice", ability["kind"])
        self.assertEqual(
            ["intelligence", "wisdom", "charisma"],
            ability["constraints"]["options"],
        )
        self.assertEqual(
            "timing-unspecified", ability["timing"]
        )
        self.assertEqual("spell-from-list", spell["kind"])
        self.assertEqual("wizard", spell["authoredValue"])
        self.assertEqual(
            "after-long-rest", spell["constraints"]["replacement"]
        )
        self.assertEqual(
            "requires-catalog-resolution", spell["schemaStatus"]
        )

    def test_human_size_choice_is_one_intrinsic_slot_not_two_grants(self):
        record = self.find(
            self.report, "species.human", "sizeChoice"
        )
        self.assertEqual("intrinsic-choice", record["kind"])
        self.assertEqual("creature.size", record["constraints"]["slotId"])
        self.assertEqual(
            ["small", "medium"], record["constraints"]["options"]
        )
        self.assertEqual("character-creation", record["timing"])
        self.assertEqual("not-selected", record["selection"])

    def test_tiefling_required_ancestry_selection_is_a_source_control(self):
        record = self.find(
            self.report, "species.tiefling", "ancestrySelection"
        )
        self.assertEqual("source-selection-control", record["kind"])
        self.assertEqual("source-selection", record["timing"])
        self.assertEqual(
            "separate-from-inherent-characteristics",
            record["schemaStatus"],
        )
        self.assertEqual(1, record["constraints"]["count"])
        self.assertEqual(3, len(record["constraints"]["options"]))

    def test_metallic_breath_options_are_use_time_not_birth_traits(self):
        record = self.find(
            self.report, "ancestry.dragonborn.metallic",
            "options", "metallic-breath-weapon",
        )
        self.assertEqual("effect-option", record["kind"])
        self.assertEqual("on-use", record["timing"])
        self.assertEqual("use-time-option", record["schemaStatus"])
        self.assertEqual(
            {"enervating-breath", "repulsion-breath"},
            set(record["constraints"]["optionIds"]),
        )
        self.assertEqual("not-selected", record["selection"])

    def test_derived_spellcasting_ability_is_not_new_independent_choice(self):
        record = self.find(
            self.report, "species.tiefling", "spellcastingAbilityFrom",
            "otherworldly-presence",
        )
        self.assertEqual("derived-parameter-binding", record["kind"])
        self.assertEqual(
            "source-binding-unresolved", record["schemaStatus"]
        )
        self.assertIn(
            record["choiceEvidenceId"],
            self.report["unmodeledEvidenceIds"]
        )

    def test_selected_lineage_includes_parent_trait_choices_only_once(self):
        report = audit_native_choice_schema(
            self.catalog, ["ancestry.elf.high", "ancestry.elf.wood"]
        )
        expected = []
        for sid in ("ancestry.elf.high", "ancestry.elf.wood"):
            for node in source_lineage(sid, self.catalog):
                if node["id"] not in expected:
                    expected.append(node["id"])
        self.assertEqual(expected, report["sourceIds"])
        self.assertEqual(
            len({x["choiceEvidenceId"] for x in report["choiceObservations"]}),
            report["observationCount"],
        )
        self.assertNotIn("species.human", report["sourceIds"])

    def test_new_homebrew_traits_join_same_schema_without_species_branches(self):
        catalog = copy.deepcopy(self.catalog)
        catalog["species.synthetic"] = {
            "id": "species.synthetic", "kind": "species",
            "selectable": True, "traits": [{
                "id": "skill", "chooseFrom": [
                    "craft", "perform", "profession"
                ], "choiceCount": 2, "when": "character-creation",
            }]
        }
        report = audit_native_choice_schema(catalog)
        x = self.find(
            report, "species.synthetic", "chooseFrom", "skill"
        )
        self.assertEqual("finite-choice", x["kind"])
        self.assertEqual("finite-options-representable", x["schemaStatus"])
        self.assertEqual(2, x["constraints"]["count"])

    def test_unmodeled_future_choice_field_is_preserved_for_review(self):
        catalog = copy.deepcopy(self.catalog)
        catalog["species.synthetic"] = {
            "id": "species.synthetic", "kind": "species",
            "selectable": True, "traits": [{
                "id": "custom", "customChoiceEngine": {
                    "selectionMode": "new"
                },
            }]
        }
        report = audit_native_choice_schema(catalog)
        x = self.find(
            report, "species.synthetic", "customChoiceEngine", "custom"
        )
        self.assertEqual(
            "unmodeled-choice-field", x["kind"]
        )
        self.assertEqual(
            {"selectionMode": "new"}, x["authoredValue"]
        )
        self.assertEqual(
            "requires-schema-review", x["schemaStatus"]
        )

    def test_malformed_authoring_fails_without_inventing_a_choice(self):
        variants = [
            {"chooseFrom": [], "choiceCount": 1},
            {"chooseFrom": ["a", "a"], "choiceCount": 1},
            {"chooseFrom": "any", "choiceCount": True},
            {"featCategory": "origin", "choiceCount": 0},
            {"abilityChoice": {"from": []}},
            {"options": [{"id": "a"}, {"id": "a"}]},
            {"spellList": ""},
        ]
        for fields in variants:
            catalog = {"species.synthetic": {
                "id": "species.synthetic", "kind": "species",
                "selectable": True, "traits": [{"id": "sample", **fields}],
            }}
            with self.subTest(fields=fields):
                with self.assertRaises(ChoiceSchemaError):
                    audit_native_choice_schema(catalog)

    def test_unknown_source_and_invalid_lineage_rejected(self):
        with self.assertRaises(ValueError):
            audit_native_choice_schema(
                self.catalog, ["species.missing"]
            )
        with self.assertRaises(ChoiceSchemaError):
            audit_native_choice_schema(self.catalog, [])

    def test_source_evidence_is_pure_and_deterministic(self):
        catalog = copy.deepcopy(self.catalog)
        before = copy.deepcopy(catalog)
        first = audit_native_choice_schema(catalog)
        second = audit_native_choice_schema(catalog)
        self.assertEqual(first, second)
        first["choiceObservations"][0]["authoredValue"] = "overwritten"
        self.assertEqual(before, catalog)


if __name__ == "__main__":
    unittest.main()
