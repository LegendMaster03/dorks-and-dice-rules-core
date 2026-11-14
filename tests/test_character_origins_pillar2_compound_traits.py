"""Content-preserving compound-ability and generic binding design tests.

Tests verify resolution of *authored parameters*, not permission to inherit
or cast an ability, gameplay resolution, balance, or complete character builds.
"""
import copy
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_source_inventory import read_catalog  # noqa: E402
from character_origins_compound_traits import (  # noqa: E402
    CompoundBindingError,
    project_compound_traits,
    read_contracts,
)


def trait(projection, local_id, source_id=None):
    return next(
        item for item in projection["compoundTraits"]
        if item["localTraitId"] == local_id
        and (source_id is None or item["sourceId"] == source_id)
    )


def bound(item, path):
    return next(value for value in item["parameterBindings"]
                if value["path"] == path)


class Pillar2CompoundTraitTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()

    def test_all_selectable_sources_preserve_one_unit_per_source_trait(self):
        for identifier, source in self.catalog.items():
            if source.get("selectable") is not True:
                continue
            with self.subTest(selected=identifier):
                p = project_compound_traits(
                    identifier, self.catalog, self.contracts
                )
                self.assertEqual("not-evaluated", p["evaluationStatus"])
                expected = [
                    (ancestor, t["id"], t)
                    for ancestor in p["sourceLineage"]
                    for t in self.catalog[ancestor].get("traits", [])
                ]
                self.assertEqual(
                    len(expected), len(p["compoundTraits"])
                )
                for (source_id, local_id, authored), unit in zip(
                    expected, p["compoundTraits"]
                ):
                    self.assertEqual(source_id, unit["sourceId"])
                    self.assertEqual(local_id, unit["localTraitId"])
                    self.assertEqual(authored, unit["authoredDefinition"])
                    self.assertEqual(
                        f"{source_id}#trait:{local_id}",
                        unit["sourceOccurrenceId"],
                    )
                    expected_fields = (
                        set(authored) - {"id", "name", "description"}
                    )
                    self.assertEqual(
                        expected_fields,
                        {part["nativeField"] for part in unit["components"]},
                    )
                    for part in unit["components"]:
                        self.assertEqual(
                            authored[part["nativeField"]],
                            part["authoredValue"],
                        )

    def test_red_dragonborn_breath_is_one_unit_with_bound_parameters(self):
        red = project_compound_traits(
            "ancestry.dragonborn.red", self.catalog, self.contracts
        )
        self.assertEqual(
            ["species.dragonborn", "ancestry.dragonborn.chromatic",
             "ancestry.dragonborn.red"],
            red["sourceLineage"],
        )
        breath = trait(red, "breath-weapon")
        self.assertEqual("parameters-bound", breath["bindingStatus"])
        self.assertEqual(2, len(breath["parameterBindings"]))
        damage = bound(breath, "damage.typeFrom")
        self.assertEqual("bound", damage["status"])
        self.assertEqual("fire", damage["resolvedValue"])
        self.assertEqual(
            "ancestry.dragonborn.red", damage["resolvedFromSourceId"]
        )
        area = bound(breath, "areaFrom")
        self.assertEqual(
            {"shape": "line", "lengthFeet": 30, "widthFeet": 5},
            area["resolvedValue"],
        )
        self.assertEqual(
            "ancestry.dragonborn.chromatic", area["resolvedFromSourceId"]
        )
        # Do not flatten activation, one use pool, damage scaling or save DC.
        authored = breath["authoredDefinition"]
        self.assertEqual(
            {"action": "attack", "replaces": "one-attack",
             "timing": "on-your-turn"},
            authored["activation"],
        )
        self.assertEqual(
            [1, 5, 11, 17],
            [x["characterLevel"] for x in
             authored["damage"]["progression"]],
        )
        self.assertEqual(
            "8 + proficiency-bonus + constitution-modifier",
            authored["savingThrow"]["dc"],
        )
        self.assertEqual(
            "selected-dragonborn-leaf",
            authored["damage"]["typeFrom"],
            "Source-authored symbolic evidence remains intact",
        )

    def test_chromatic_warding_and_resistance_bind_same_leaf_type(self):
        red = project_compound_traits(
            "ancestry.dragonborn.red", self.catalog, self.contracts
        )
        resistance = trait(red, "draconic-resistance")
        ward = trait(red, "chromatic-warding")
        self.assertEqual(
            "fire",
            bound(resistance, "damageResistance.damageTypeFrom")["resolvedValue"],
        )
        self.assertEqual(
            "fire",
            bound(ward, "damageResistanceOverride.damageTypeFrom")["resolvedValue"],
        )
        self.assertEqual(
            100, ward["authoredDefinition"]["damageResistanceOverride"]["percent"]
        )
        self.assertEqual(
            "replace-lower-same-type",
            ward["authoredDefinition"]["damageResistanceOverride"]["stacking"],
        )
        self.assertEqual(
            5, ward["authoredDefinition"]["requiredCharacterLevel"]
        )
        self.assertTrue(ward["authoredDefinition"]["doesNotGrantConditionImmunity"])

    def test_metallic_breath_retains_separate_options_and_use_pool(self):
        gold = project_compound_traits(
            "ancestry.dragonborn.gold", self.catalog, self.contracts
        )
        base = trait(gold, "breath-weapon")
        metallic = trait(gold, "metallic-breath-weapon")
        self.assertEqual(
            {"shape": "cone", "lengthFeet": 15},
            bound(base, "areaFrom")["resolvedValue"],
        )
        self.assertEqual(
            "fire", bound(base, "damage.typeFrom")["resolvedValue"]
        )
        self.assertEqual(
            "no-symbolic-parameters", metallic["bindingStatus"]
        )
        self.assertEqual(
            "breath-weapon",
            metallic["authoredDefinition"]["uses"]["independentOf"]
        )
        self.assertEqual(
            1, metallic["authoredDefinition"]["uses"]["value"]
        )
        self.assertEqual(
            ["enervating-breath", "repulsion-breath"],
            [o["id"] for o in metallic["authoredDefinition"]["options"]],
        )
        self.assertEqual(
            "on-use", metallic["authoredDefinition"]["choiceTiming"]
        )

    def test_unknown_source_reference_requires_explicit_contract(self):
        infernal = project_compound_traits(
            "ancestry.tiefling.infernal", self.catalog, self.contracts
        )
        presence = trait(infernal, "otherworldly-presence")
        self.assertEqual("requires-review", presence["bindingStatus"])
        ref = bound(presence, "spellcastingAbilityFrom")
        self.assertEqual("selected-tiefling-ancestry", ref["authoredReference"])
        self.assertEqual("binding-contract-required", ref["reason"])
        self.assertNotIn("resolvedValue", ref)

    def test_generic_homebrew_binding_uses_same_resolver_without_new_code(self):
        catalog = copy.deepcopy(self.catalog)
        catalog["species.test-creature"] = {
            "id": "species.test-creature",
            "name": "Test Creature",
            "kind": "species",
            "selectable": False,
            "damageType": "acid",
            "traits": [
                {"id": "custom-wave",
                 "activation": {"action": "bonus-action"},
                 "damage": {"typeFrom": "selected-homebrew-leaf"},
                 "areaFrom": "selected-homebrew-area"},
            ],
        }
        catalog["ancestry.test-creature.variant"] = {
            "id": "ancestry.test-creature.variant",
            "kind": "ancestry",
            "name": "Test Variant",
            "selectable": True,
            "parent": "species.test-creature",
            "damageType": "cold",
            "breathWeaponArea": {"shape": "cone", "lengthFeet": 10},
            "traits": [],
        }
        contracts = [{
            "sourceOccurrenceId": "species.test-creature#trait:custom-wave",
            "bindings": [
                {
                    "path": "damage.typeFrom",
                    "authoredReference": "selected-homebrew-leaf",
                    "lookup": {
                        "scope": "selected-lineage",
                        "field": "damageType",
                        "order": "nearest-to-selected",
                        "expectedType": "string",
                    },
                },
                {
                    "path": "areaFrom",
                    "authoredReference": "selected-homebrew-area",
                    "lookup": {
                        "scope": "selected-lineage",
                        "field": "breathWeaponArea",
                        "order": "nearest-to-selected",
                        "expectedType": "object",
                    },
                },
            ],
        }]
        p = project_compound_traits(
            "ancestry.test-creature.variant", catalog, contracts
        )
        wave = trait(p, "custom-wave")
        self.assertEqual("parameters-bound", wave["bindingStatus"])
        self.assertEqual(
            "cold", bound(wave, "damage.typeFrom")["resolvedValue"]
        )
        self.assertEqual(
            "ancestry.test-creature.variant",
            bound(wave, "damage.typeFrom")["resolvedFromSourceId"],
        )
        self.assertEqual(
            {"shape": "cone", "lengthFeet": 10},
            bound(wave, "areaFrom")["resolvedValue"],
        )
        self.assertEqual(
            {"action": "bonus-action"},
            wave["authoredDefinition"]["activation"],
        )

    def test_missing_target_does_not_fall_back_or_invent_mechanics(self):
        catalog = copy.deepcopy(self.catalog)
        del catalog["ancestry.dragonborn.red"]["damageType"]
        p = project_compound_traits(
            "ancestry.dragonborn.red", catalog, self.contracts
        )
        breath = trait(p, "breath-weapon")
        missing = bound(breath, "damage.typeFrom")
        self.assertEqual("requires-review", breath["bindingStatus"])
        self.assertEqual("lineage-field-not-found", missing["reason"])
        self.assertNotIn("resolvedValue", missing)
        self.assertEqual(
            "bound", bound(breath, "areaFrom")["status"]
        )

    def test_nearest_wrong_type_fails_closed(self):
        catalog = copy.deepcopy(self.catalog)
        catalog["ancestry.dragonborn.red"]["damageType"] = 42
        p = project_compound_traits(
            "ancestry.dragonborn.red", catalog, self.contracts
        )
        ref = bound(trait(p, "breath-weapon"), "damage.typeFrom")
        self.assertEqual("unresolved", ref["status"])
        self.assertEqual("nearest-field-type-mismatch", ref["reason"])

    def test_stale_or_changed_reference_cannot_be_silently_bound(self):
        catalog = copy.deepcopy(self.catalog)
        catalog["species.dragonborn"]["traits"][2]["areaFrom"] = (
            "new-unsupported-alias"
        )
        p = project_compound_traits(
            "ancestry.dragonborn.red", catalog, self.contracts
        )
        ref = bound(trait(p, "breath-weapon"), "areaFrom")
        self.assertEqual("authored-reference-mismatch", ref["reason"])
        self.assertNotIn("resolvedValue", ref)

    def test_stale_contract_path_detected(self):
        altered = copy.deepcopy(self.contracts)
        altered[0]["bindings"][0]["path"] = "nonexistent"
        p = project_compound_traits(
            "ancestry.dragonborn.red", self.catalog, altered
        )
        resistance = trait(p, "draconic-resistance")
        reasons = {ref["reason"] for ref in resistance["parameterBindings"]}
        self.assertEqual(
            {"binding-contract-required", "contract-path-not-found"}, reasons
        )

    def test_malformed_and_duplicate_contracts_rejected(self):
        for bad in (
            [{"sourceOccurrenceId": "x", "bindings": [
                {"path": "damage.typeFrom", "authoredReference": "a",
                 "lookup": {"scope": "all", "field": "damageType",
                            "order": "nearest-to-selected",
                            "expectedType": "string"}}
            ]}],
            copy.deepcopy(self.contracts) + [copy.deepcopy(self.contracts[0])],
        ):
            with self.subTest(contracts=bad):
                with self.assertRaises(CompoundBindingError):
                    project_compound_traits(
                        "ancestry.dragonborn.red", self.catalog, bad
                    )

    def test_binding_does_not_mutate_catalog_or_contracts(self):
        original_catalog = copy.deepcopy(self.catalog)
        original_contracts = copy.deepcopy(self.contracts)
        first = project_compound_traits(
            "ancestry.dragonborn.red", self.catalog, self.contracts
        )
        second = project_compound_traits(
            "ancestry.dragonborn.red", self.catalog, self.contracts
        )
        self.assertEqual(first, second)
        first["compoundTraits"][0]["authoredDefinition"]["id"] = "changed"
        self.assertEqual(original_catalog, self.catalog)
        self.assertEqual(original_contracts, self.contracts)


if __name__ == "__main__":
    unittest.main()
