"""End-to-end *preview* of generic configured area-effect mechanics.

These compare numeric outcomes for the same character context, not actual
combat rolls or trusted inheritance. No Species-specific evaluator is used.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_mechanics_preview import (  # noqa: E402
    MechanicsPreviewError,
    evaluate_configured_definition,
    evaluate_integer_expression,
    evaluate_origin_preview,
)
from character_origins_parameter_inheritance import read_blueprint  # noqa: E402
from character_origins_source_inventory import read_catalog  # noqa: E402


LINE = {"shape": "line", "lengthFeet": 30, "widthFeet": 5}
CONE = {"shape": "cone", "lengthFeet": 15}


def contributor(key, source):
    return {"contributorId": key, "selectedSourceId": source}


def context(level, pb, con=3):
    return {
        "characterLevel": level,
        "proficiencyBonus": pb,
        "abilityModifiers": {"constitution": con, "wisdom": 2},
    }


class Pillar2MechanicsPreviewTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.blueprint = read_blueprint()
        cls.contracts = read_contracts()

    def preview(self, parents, damage, area, character_context,
                catalog=None, blueprint=None, contracts=None):
        return evaluate_origin_preview(
            parents, {"damageType": damage, "area": area},
            self.blueprint if blueprint is None else blueprint,
            self.catalog if catalog is None else catalog,
            self.contracts if contracts is None else contracts,
            character_context,
        )

    def test_standard_vs_mixed_matching_instance_has_same_mechanics(self):
        standard = self.preview([
            contributor("standard", "ancestry.dragonborn.red")
        ], "fire", LINE, context(5, 3))
        mixed = self.preview([
            contributor("red", "ancestry.dragonborn.red"),
            contributor("bronze", "ancestry.dragonborn.bronze"),
        ], "fire", LINE, context(5, 3))
        self.assertEqual(
            standard["evaluatedMechanics"], mixed["evaluatedMechanics"]
        )
        self.assertEqual(
            standard["mechanicId"], mixed["mechanicId"]
        )
        self.assertNotEqual(
            standard["parameterProvenance"], mixed["parameterProvenance"]
        )
        self.assertEqual("evaluated-preview-not-earned", mixed["status"])

    def test_all_four_approved_pairings_share_operational_rules(self):
        contributors = [
            contributor("red", "ancestry.dragonborn.red"),
            contributor("bronze", "ancestry.dragonborn.bronze"),
        ]
        results = {
            (damage, area["shape"]): self.preview(
                contributors, damage, area, context(11, 4, 2)
            ) for damage in ("fire", "lightning")
            for area in (LINE, CONE)
        }
        self.assertEqual(4, len(results))
        for (damage, shape), value in results.items():
            with self.subTest(damage=damage, shape=shape):
                resolved = value["evaluatedMechanics"]
                self.assertEqual(damage, resolved["damage"]["type"])
                self.assertEqual(shape, resolved["area"]["shape"])
                self.assertEqual("3d10", resolved["damage"]["dice"])
                self.assertEqual(3, resolved["damage"]["minimumRoll"])
                self.assertEqual(30, resolved["damage"]["maximumRoll"])
                self.assertEqual(14, resolved["savingThrow"]["dc"])
                self.assertEqual("dexterity", resolved["savingThrow"]["ability"])
                self.assertEqual("half-damage",
                                 resolved["savingThrow"]["onSuccess"])
                self.assertEqual(4, resolved["uses"]["maximumUses"])
                self.assertEqual("long-rest", resolved["uses"]["recharge"])
                self.assertEqual(
                    {"action": "attack", "replaces": "one-attack",
                     "timing": "on-your-turn"},
                    resolved["activation"],
                )
                self.assertEqual(
                    "structured-mechanics-preview",
                    resolved["evaluationStatus"],
                )
                self.assertIn("roll-saving-throws",
                              resolved["unexecutedOperations"])

    def test_level_progression_boundaries_from_authored_rules(self):
        red = [contributor("red", "ancestry.dragonborn.red")]
        for level, pb, dice, high in (
            (1, 2, "1d10", 10),
            (4, 2, "1d10", 10),
            (5, 3, "2d10", 20),
            (10, 4, "2d10", 20),
            (11, 4, "3d10", 30),
            (16, 5, "3d10", 30),
            (17, 6, "4d10", 40),
            (20, 6, "4d10", 40),
        ):
            with self.subTest(level=level):
                result = self.preview(
                    red, "fire", LINE, context(level, pb)
                )
                facts = result["evaluatedMechanics"]
                self.assertEqual(dice, facts["damage"]["dice"])
                self.assertEqual(high, facts["damage"]["maximumRoll"])
                self.assertEqual(pb, facts["uses"]["maximumUses"])
                self.assertEqual(8 + pb + 3, facts["savingThrow"]["dc"])

    def test_negative_modifier_changes_dc_without_changing_damage(self):
        p = self.preview(
            [contributor("red", "ancestry.dragonborn.red")],
            "fire", LINE, context(5, 3, -2),
        )
        resolved = p["evaluatedMechanics"]
        self.assertEqual(9, resolved["savingThrow"]["dc"])
        self.assertEqual("2d10", resolved["damage"]["dice"])

    def test_generic_homebrew_fixed_dice_and_different_action(self):
        catalog = copy.deepcopy(self.catalog)
        catalog["species.sample"] = {
            "id": "species.sample",
            "name": "Sample",
            "kind": "species",
            "selectable": True,
            "damageType": "acid",
            "breathWeaponArea": {"shape": "cone", "lengthFeet": 20},
            "traits": [{
                "id": "wave",
                "description": "An author-provided wave",
                "activation": {"action": "bonus-action"},
                "uses": {"value": 1, "recharge": "short-rest"},
                "savingThrow": {
                    "ability": "wisdom",
                    "dc": "10 + wisdom-modifier",
                    "onSuccess": "no-damage",
                },
                "damage": {"typeFrom": "chosen-kind", "dice": "2d6"},
                "areaFrom": "chosen-area",
            }],
        }
        occurrence = "species.sample#trait:wave"
        blueprint = copy.deepcopy(self.blueprint)
        blueprint["mechanicId"] = "ability.custom-wave"
        blueprint["definitionOccurrenceId"] = occurrence
        contracts = copy.deepcopy(self.contracts) + [{
            "sourceOccurrenceId": occurrence,
            "bindings": [
                {
                    "path": "damage.typeFrom",
                    "authoredReference": "chosen-kind",
                    "lookup": {
                        "scope": "selected-lineage",
                        "field": "damageType",
                        "order": "nearest-to-selected",
                        "expectedType": "string",
                    },
                },
                {
                    "path": "areaFrom",
                    "authoredReference": "chosen-area",
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
            [contributor("custom", "species.sample")],
            "acid", {"shape": "cone", "lengthFeet": 20},
            context(3, 2), catalog=catalog,
            blueprint=blueprint, contracts=contracts,
        )
        self.assertEqual("ability.custom-wave", p["mechanicId"])
        effects = p["evaluatedMechanics"]
        self.assertEqual("bonus-action", effects["activation"]["action"])
        self.assertEqual("2d6", effects["damage"]["dice"])
        self.assertEqual(12, effects["damage"]["maximumRoll"])
        self.assertEqual(12, effects["savingThrow"]["dc"])
        self.assertEqual(1, effects["uses"]["maximumUses"])
        self.assertEqual("short-rest", effects["uses"]["recharge"])

    def test_unoffered_trait_or_parameter_stops_before_evaluation(self):
        p = self.preview([
            contributor("human", "species.human")
        ], "fire", LINE, context(5, 3))
        self.assertEqual("rejected-not-earned", p["status"])
        self.assertNotIn("evaluatedMechanics", p)
        p = self.preview([
            contributor("red", "ancestry.dragonborn.red")
        ], "cold", LINE, context(5, 3))
        self.assertEqual("rejected-not-earned", p["status"])
        self.assertNotIn("evaluatedMechanics", p)

    def test_unsafe_arithmetic_is_rejected(self):
        ctx = context(1, 2)
        for expression in (
            "8 + unknown-token",
            "8 + proficiency-bonus + evil()",
            "8 + proficiency-bonus - constitution-modifier",
            "__import__('os').system('false')",
            "8++proficiency-bonus",
        ):
            with self.subTest(expression=expression):
                with self.assertRaises(MechanicsPreviewError):
                    evaluate_integer_expression(expression, ctx)

    def test_malformed_progression_and_unknown_mechanics_fail_closed(self):
        baseline = self.preview([
            contributor("red", "ancestry.dragonborn.red")
        ], "fire", LINE, context(1, 2))
        from character_origins_configured_trait import configure_trait
        configured = configure_trait(
            [contributor("red", "ancestry.dragonborn.red")],
            {"damageType": "fire", "area": LINE},
            self.blueprint, self.catalog, self.contracts,
        )["configuredDefinition"]

        cases = [
            lambda d: d.update({"newMechanicalEffect": {"bonus": 2}}),
            lambda d: d["damage"].update({"progression": [
                {"characterLevel": 1, "dice": "1d10"},
                {"characterLevel": 1, "dice": "99d100"},
            ]}),
            lambda d: d["damage"].update({"progression": [
                {"characterLevel": 1, "dice": "1d10"},
                {"characterLevel": 5, "dice": "10 + 1d10"},
            ]}),
            lambda d: d["area"].update({"shape": "mysterious-area"}),
            lambda d: d["uses"].update({"value": True}),
            lambda d: d["savingThrow"].update({"dc": "8 + invalid-function()"}),
        ]
        for mutate in cases:
            example = copy.deepcopy(configured)
            mutate(example)
            with self.subTest(bad=example):
                with self.assertRaises(MechanicsPreviewError):
                    evaluate_configured_definition(example, context(5, 3))
        self.assertEqual("1d10",
                         baseline["evaluatedMechanics"]["damage"]["dice"])

    def test_invalid_character_context_fails_closed(self):
        from character_origins_configured_trait import configure_trait
        definition = configure_trait(
            [contributor("red", "ancestry.dragonborn.red")],
            {"damageType": "fire", "area": LINE},
            self.blueprint, self.catalog, self.contracts,
        )["configuredDefinition"]
        for bad_context in (
            {"characterLevel": True, "proficiencyBonus": 2,
             "abilityModifiers": {"constitution": 1}},
            {"characterLevel": 5, "proficiencyBonus": 0,
             "abilityModifiers": {"constitution": 1}},
            {"characterLevel": 5, "proficiencyBonus": 3,
             "abilityModifiers": {"constitution": True}},
            {"characterLevel": 5, "proficiencyBonus": 3,
             "abilityModifiers": {}},
        ):
            with self.subTest(context=bad_context):
                with self.assertRaises(MechanicsPreviewError):
                    evaluate_configured_definition(definition, bad_context)

    def test_pure_evaluation_does_not_modify_inputs(self):
        configured = self.preview([
            contributor("red", "ancestry.dragonborn.red")
        ], "fire", LINE, context(5, 3))
        from character_origins_configured_trait import configure_trait
        definition = configure_trait(
            [contributor("red", "ancestry.dragonborn.red")],
            {"damageType": "fire", "area": LINE},
            self.blueprint, self.catalog, self.contracts,
        )["configuredDefinition"]
        before = copy.deepcopy(definition)
        context_data = context(5, 3)
        before_context = copy.deepcopy(context_data)
        first = evaluate_configured_definition(definition, context_data)
        second = evaluate_configured_definition(definition, context_data)
        self.assertEqual(first, second)
        self.assertEqual(before, definition)
        self.assertEqual(before_context, context_data)
        self.assertEqual(first, configured["evaluatedMechanics"])


if __name__ == "__main__":
    unittest.main()
