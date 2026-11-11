"""Configured-trait previews preserve the whole mechanic and provenance.

This test suite establishes parameterized *shape*, not earned character
ability, reliable gameplay execution, or balance equivalence.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_configured_trait import configure_trait  # noqa: E402
from character_origins_parameter_inheritance import (  # noqa: E402
    ParameterInheritanceError, read_blueprint,
)
from character_origins_source_inventory import read_catalog  # noqa: E402


LINE = {"shape": "line", "lengthFeet": 30, "widthFeet": 5}
CONE = {"shape": "cone", "lengthFeet": 15}


def contributor(identity, selected):
    return {"contributorId": identity, "selectedSourceId": selected}


class Pillar2ConfiguredTraitTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.blueprint = read_blueprint()
        cls.contracts = read_contracts()

    def preview(self, contributors, parameters, blueprint=None, catalog=None,
                contracts=None):
        return configure_trait(
            contributors,
            parameters,
            self.blueprint if blueprint is None else blueprint,
            self.catalog if catalog is None else catalog,
            self.contracts if contracts is None else contracts,
        )

    def test_red_standard_configuration_retains_full_breath_mechanic(self):
        p = self.preview(
            [contributor("red", "ancestry.dragonborn.red")],
            {"damageType": "fire", "area": LINE},
        )
        self.assertTrue(p["configured"])
        self.assertEqual("configured-preview-not-earned", p["status"])
        self.assertEqual("not-evaluated", p["evaluationStatus"])
        self.assertEqual("ability.breath-weapon", p["mechanicId"])
        self.assertEqual(
            "species.dragonborn#trait:breath-weapon",
            p["definitionOccurrenceId"],
        )
        original = p["authoredDefinition"]
        configured = p["configuredDefinition"]
        self.assertEqual(original["id"], configured["id"])
        self.assertEqual(original["activation"], configured["activation"])
        self.assertEqual(original["uses"], configured["uses"])
        self.assertEqual(original["savingThrow"], configured["savingThrow"])
        self.assertEqual(
            original["damage"]["progression"],
            configured["damage"]["progression"],
        )
        self.assertEqual("fire", configured["damage"]["type"])
        self.assertEqual(LINE, configured["area"])
        self.assertNotIn("typeFrom", configured["damage"])
        self.assertNotIn("areaFrom", configured)
        self.assertEqual(
            "selected-dragonborn-leaf",
            original["damage"]["typeFrom"],
        )
        self.assertEqual(
            ["red"],
            [source["contributorId"] for source in
             p["parameterProvenance"]["damageType"]],
        )
        self.assertEqual(
            "ancestry.dragonborn.chromatic",
            p["parameterProvenance"]["area"][0]["resolvedFromSourceId"],
        )

    def test_crossed_fire_cone_and_lightning_line_both_create_instances(self):
        parents = [
            contributor("red", "ancestry.dragonborn.red"),
            contributor("bronze", "ancestry.dragonborn.bronze"),
        ]
        for damage, area, damage_source, area_source in (
            ("fire", CONE, "red", "bronze"),
            ("lightning", LINE, "bronze", "red"),
        ):
            with self.subTest(damage=damage, area=area):
                p = self.preview(
                    parents, {"damageType": damage, "area": area}
                )
                self.assertTrue(p["configured"])
                self.assertEqual(damage, p["configuredDefinition"]["damage"]["type"])
                self.assertEqual(area, p["configuredDefinition"]["area"])
                self.assertEqual(
                    [damage_source],
                    [x["contributorId"] for x in
                     p["parameterProvenance"]["damageType"]],
                )
                self.assertEqual(
                    [area_source],
                    [x["contributorId"] for x in
                     p["parameterProvenance"]["area"]],
                )
                self.assertEqual(
                    {"value": "proficiency-bonus", "recharge": "long-rest"},
                    p["configuredDefinition"]["uses"],
                )
                self.assertEqual(
                    "dexterity",
                    p["configuredDefinition"]["savingThrow"]["ability"],
                )
                self.assertEqual(
                    [1, 5, 11, 17],
                    [entry["characterLevel"] for entry in
                     p["configuredDefinition"]["damage"]["progression"]],
                )

    def test_all_four_independent_pairs_produce_same_mechanic_shape(self):
        parents = [
            contributor("red", "ancestry.dragonborn.red"),
            contributor("bronze", "ancestry.dragonborn.bronze"),
        ]
        instances = [
            self.preview(parents, {"damageType": dtype, "area": area})
            for dtype in ("fire", "lightning")
            for area in (LINE, CONE)
        ]
        self.assertTrue(all(p["configured"] for p in instances))
        self.assertEqual(
            1, len({p["definitionOccurrenceId"] for p in instances})
        )
        for p in instances[1:]:
            self.assertEqual(
                instances[0]["configuredDefinition"]["activation"],
                p["configuredDefinition"]["activation"],
            )
            self.assertEqual(
                instances[0]["configuredDefinition"]["savingThrow"],
                p["configuredDefinition"]["savingThrow"],
            )

    def test_two_matching_contributors_preserve_both_sources(self):
        p = self.preview(
            [contributor("a", "ancestry.dragonborn.red"),
             contributor("b", "ancestry.dragonborn.red")],
            {"damageType": "fire", "area": LINE},
        )
        self.assertEqual(
            ["a", "b"],
            [x["contributorId"] for x in
             p["parameterProvenance"]["area"]],
        )
        self.assertEqual("fire", p["configuredDefinition"]["damage"]["type"])

    def test_unoffered_values_are_rejected_without_partial_instance(self):
        p = self.preview(
            [contributor("red", "ancestry.dragonborn.red")],
            {"damageType": "cold", "area": LINE},
        )
        self.assertFalse(p["configured"])
        self.assertEqual("parameter-value-not-offered", p["reason"])
        self.assertEqual("rejected-not-earned", p["status"])
        self.assertNotIn("configuredDefinition", p)
        self.assertNotIn("authoredDefinition", p)

    def test_linked_ability_rejects_crossed_pair(self):
        bp = copy.deepcopy(self.blueprint)
        bp["parameterInheritance"]["mode"] = "linked"
        p = self.preview(
            [contributor("red", "ancestry.dragonborn.red"),
             contributor("bronze", "ancestry.dragonborn.bronze")],
            {"damageType": "fire", "area": CONE},
            blueprint=bp,
        )
        self.assertFalse(p["configured"])
        self.assertEqual("linked-configuration-not-offered", p["reason"])

    def test_incomplete_parameter_binding_blocks_instance(self):
        broken = copy.deepcopy(self.catalog)
        del broken["ancestry.dragonborn.red"]["damageType"]
        p = self.preview(
            [contributor("red", "ancestry.dragonborn.red")],
            {"damageType": "fire", "area": LINE}, catalog=broken,
        )
        self.assertFalse(p["configured"])
        self.assertNotIn("configuredDefinition", p)

    def test_no_contributor_trait_blocks_instance(self):
        p = self.preview(
            [contributor("human", "species.human")],
            {"damageType": "fire", "area": LINE},
        )
        self.assertFalse(p["configured"])
        self.assertEqual("no-complete-contributor-configuration", p["reason"])

    def test_fixed_operational_mechanics_are_not_parameter_choices(self):
        with self.assertRaises(ParameterInheritanceError):
            self.preview(
                [contributor("red", "ancestry.dragonborn.red")],
                {"damageType": "fire", "area": LINE,
                 "uses": {"value": 999}},
            )
        with self.assertRaises(ParameterInheritanceError):
            self.preview(
                [contributor("red", "ancestry.dragonborn.red")],
                {"damageType": "fire", "area": LINE,
                 "activation": {"action": "free"}},
            )

    def test_undeclared_symbolic_value_is_not_silently_left_unresolved(self):
        bad = copy.deepcopy(self.catalog)
        breath = next(t for t in bad["species.dragonborn"]["traits"]
                      if t["id"] == "breath-weapon")
        breath["extraEffectFrom"] = "unknown-custom-parameter"
        with self.assertRaisesRegex(
            ParameterInheritanceError, "Unresolved symbolic fields"
        ):
            self.preview(
                [contributor("red", "ancestry.dragonborn.red")],
                {"damageType": "fire", "area": LINE},
                catalog=bad,
            )

    def test_conflicting_concrete_target_is_rejected(self):
        bad = copy.deepcopy(self.catalog)
        breath = next(t for t in bad["species.dragonborn"]["traits"]
                      if t["id"] == "breath-weapon")
        breath["damage"]["type"] = "arbitrary-fixed"
        with self.assertRaisesRegex(
            ParameterInheritanceError, "Concrete field already exists"
        ):
            self.preview(
                [contributor("red", "ancestry.dragonborn.red")],
                {"damageType": "fire", "area": LINE}, catalog=bad,
            )

    def test_unmaterializable_path_is_rejected(self):
        # A missing binding can not be treated as a resolved parameter.
        bp = copy.deepcopy(self.blueprint)
        bp["parameters"]["damageType"]["bindingPath"] = "damage.progression"
        p = self.preview(
            [contributor("red", "ancestry.dragonborn.red")],
            {"damageType": "fire", "area": LINE}, blueprint=bp,
        )
        self.assertFalse(p["configured"])
        self.assertNotIn("configuredDefinition", p)

    def test_homebrew_uses_identical_generic_materialization(self):
        catalog = copy.deepcopy(self.catalog)
        catalog["species.homebrew"] = {
            "id": "species.homebrew", "name": "Homebrew",
            "kind": "species", "selectable": True,
            "damageType": "acid",
            "breathWeaponArea": {"shape": "cone", "lengthFeet": 20},
            "traits": [{
                "id": "wave",
                "activation": {"action": "bonus-action"},
                "uses": {"value": 1, "recharge": "short-rest"},
                "damage": {"typeFrom": "custom-leaf"},
                "areaFrom": "custom-shape",
            }],
        }
        occurrence = "species.homebrew#trait:wave"
        bp = copy.deepcopy(self.blueprint)
        bp["definitionOccurrenceId"] = occurrence
        contracts = copy.deepcopy(self.contracts) + [{
            "sourceOccurrenceId": occurrence,
            "bindings": [
                {
                    "path": "damage.typeFrom",
                    "authoredReference": "custom-leaf",
                    "lookup": {
                        "scope": "selected-lineage",
                        "field": "damageType",
                        "order": "nearest-to-selected",
                        "expectedType": "string",
                    },
                },
                {
                    "path": "areaFrom",
                    "authoredReference": "custom-shape",
                    "lookup": {
                        "scope": "selected-lineage",
                        "field": "breathWeaponArea",
                        "order": "nearest-to-selected",
                        "expectedType": "object",
                    },
                },
            ],
        }]
        p = self.preview(
            [contributor("homebrew", "species.homebrew")],
            {"damageType": "acid",
             "area": {"shape": "cone", "lengthFeet": 20}},
            blueprint=bp, catalog=catalog, contracts=contracts,
        )
        self.assertTrue(p["configured"])
        self.assertEqual("acid", p["configuredDefinition"]["damage"]["type"])
        self.assertEqual(
            {"action": "bonus-action"},
            p["configuredDefinition"]["activation"],
        )
        self.assertEqual(
            {"value": 1, "recharge": "short-rest"},
            p["configuredDefinition"]["uses"],
        )

    def test_preview_is_deterministic_without_catalog_mutations(self):
        catalog = copy.deepcopy(self.catalog)
        bp = copy.deepcopy(self.blueprint)
        contracts = copy.deepcopy(self.contracts)
        args = (
            [contributor("red", "ancestry.dragonborn.red"),
             contributor("bronze", "ancestry.dragonborn.bronze")],
            {"damageType": "fire", "area": CONE},
        )
        a = self.preview(*args, blueprint=bp, catalog=catalog,
                         contracts=contracts)
        b = self.preview(*args, blueprint=bp, catalog=catalog,
                         contracts=contracts)
        self.assertEqual(a, b)
        a["configuredDefinition"]["damage"]["type"] = "changed"
        self.assertEqual(self.catalog, catalog)
        self.assertEqual(self.blueprint, bp)
        self.assertEqual(self.contracts, contracts)


if __name__ == "__main__":
    unittest.main()
