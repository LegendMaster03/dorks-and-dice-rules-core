"""Regression checks for source-backed Construct balance diagnostics.

These tests do not authorize starting allowances or manufacturing statistics.
They guard calculations against silent changes to native candidate inputs.
"""
import copy
import unittest

from tools.research_construct_assembly_balance import (
    _manufacture, evaluate, load_catalog, report, scenarios,
)


class ConstructAssemblyBalanceResearchTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = load_catalog()
        cls.rows = {row["id"]: row for row in report(cls.catalog)}

    def test_three_reference_totals_match_actual_native_recipes(self):
        for case_id, expected in {
            "mechanical-medium-like": (173, 223, 4, 7, 300),
            "all-steel-overbuilt": (297, 474, 4, 4, 900),
            "minimal-nonhumanoid": (95, 58, 5, 3, 300),
        }.items():
            row = self.rows[case_id]
            self.assertEqual(expected, (
                row["resourceCost"], row["knownMinimumRetainedMassLb"],
                row["nominalPowerOutput"], row["requestedPowerDemand"],
                row["nominalGrossLoadSupportCeilingLb"]))
            self.assertFalse(row["massComplete"])
            self.assertEqual("not determined: payload and complete operating state absent",
                             row["movement"])

    def test_medium_nominal_power_bottleneck_is_not_a_complete_build(self):
        standard = self.rows["mechanical-medium-like"]
        infernal = self.rows["mechanical-medium-infernal"]
        self.assertEqual(3, standard["minimumSimultaneousPowerShortfall"])
        self.assertEqual((173, 212), (
            standard["resourceCost"], infernal["resourceCost"]))
        self.assertEqual((4, 8), (
            standard["nominalPowerOutput"], infernal["nominalPowerOutput"]))
        self.assertEqual(0, infernal["minimumSimultaneousPowerShortfall"])
        self.assertEqual(8, infernal["candidateAllowanceHeadroom"])
        self.assertIn("not verified", infernal["operatingState"])

    def test_more_drives_trade_material_cost_and_power_for_load_capacity(self):
        cases = [self.rows[f"overbuilt-{n}-drives"] for n in (1, 2)]
        cases.append(self.rows["all-steel-overbuilt"])
        self.assertEqual([253, 275, 297],
                         [x["resourceCost"] for x in cases])
        self.assertEqual([414, 444, 474],
                         [x["knownMinimumRetainedMassLb"] for x in cases])
        self.assertEqual([300, 600, 900],
                         [x["nominalGrossLoadSupportCeilingLb"] for x in cases])
        self.assertTrue(cases[0]["minimumMassExceedsSupportCeiling"])
        self.assertFalse(cases[-1]["minimumMassExceedsSupportCeiling"])
        self.assertEqual([2, 3, 4],
                         [x["requestedPowerDemand"] for x in cases])
        self.assertTrue(all(x["hypotheticalSameRegionFlatDrSum"] == 12
                            for x in cases))

    def test_plate_material_changes_mass_cost_and_candidate_protection(self):
        expected = {
            "all-steel-overbuilt": (297, 474, 12),
            "overbuilt-wood-plating": (225, 294, 0),
            "overbuilt-stone-plating": (225, 546, 12),
            "overbuilt-cloth-plating": (189, 258, 0),
        }
        for case_id, numbers in expected.items():
            row = self.rows[case_id]
            self.assertEqual(numbers, (row["resourceCost"],
                                      row["knownMinimumRetainedMassLb"],
                                      row["hypotheticalSameRegionFlatDrSum"]))
        # The stone/steel comparison is conditional: with unused capacity,
        # current candidate material prices can favor heavier stone.
        self.assertGreater(
            self.rows["overbuilt-stone-plating"]["knownMinimumRetainedMassLb"],
            self.rows["all-steel-overbuilt"]["knownMinimumRetainedMassLb"])

    def test_intermediate_ingredient_not_purchased_twice(self):
        actuator = _manufacture(
            self.catalog, "construct-component.alchemical-actuator",
            "crafting-recipe.construct-component.alchemical-actuator",
            {"actuator": "construct-material.steel"})
        self.assertEqual(2, actuator["inputs"]["crafting-material.alchemical-reagent"])
        self.assertNotIn("crafting-material.activated-medium", actuator["inputs"])
        self.assertFalse(actuator["certified"])

    def test_invalid_recipe_and_material_do_not_become_free(self):
        changed = copy.deepcopy(self.catalog)
        changed["recipes"]["crafting-recipe.construct-component.structural-frame"][
            "producesDefinitionId"] = "construct-component.crystal-eye"
        with self.assertRaises(ValueError):
            _manufacture(changed, "construct-component.structural-frame",
                         "crafting-recipe.construct-component.structural-frame",
                         {"frame": "construct-material.steel"})
        changed2 = copy.deepcopy(self.catalog)
        del changed2["prices"]["construct-material.steel"]
        with self.assertRaises(ValueError):
            evaluate(scenarios(changed2["reference"])[0], changed2)

    def test_provisional_allowance_is_not_native_authorization(self):
        self.assertEqual(220, self.catalog["reference"]["candidateStartingAllowance"])
        self.assertEqual(-77, self.rows["all-steel-overbuilt"][
            "candidateAllowanceHeadroom"])
        self.assertEqual(125, self.rows["minimal-nonhumanoid"][
            "candidateAllowanceHeadroom"])


if __name__ == "__main__":
    unittest.main()
