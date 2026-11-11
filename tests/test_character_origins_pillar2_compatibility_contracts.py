"""Explicit synthetic compatibility policies do not authorize Origins.

The live Dragonborn ancestry relationship and any cross-trait linkage remain
unapproved. These tests cover only a fabricated Species characteristic
contract and demonstrate source-neutral evidence and narrowly scoped review.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compatibility_contracts import (  # noqa: E402
    CompatibilityContractError, evaluate_compatibility_proposal,
    read_compatibility_fixture, validate_compatibility_contract,
)


class CompatibilityContractStudyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture = read_compatibility_fixture()

    def evaluate(self, values, fixture=None, root=None):
        policy = fixture if fixture is not None else self.fixture
        return evaluate_compatibility_proposal(
            policy, values,
            policy["speciesRootId"] if root is None else root
        )

    @staticmethod
    def sample(**overrides):
        values = {
            "category": "test.chromatic",
            "skinMaterial": "test.crimson-scale",
            "damageType": "fire",
            "resistanceDamageType": "fire",
        }
        values.update(overrides)
        return values

    def test_fully_matching_relationship_is_not_balance_certification(self):
        p = self.evaluate(self.sample())
        self.assertEqual("no-detected-conflicts-not-certified", p["status"])
        self.assertFalse(p["compatibilityCertified"])
        self.assertFalse(p["balanceEvaluated"])
        self.assertEqual("synthetic-design-evidence-only", p["authority"])
        self.assertEqual([], p["reviewedExceptions"])
        self.assertEqual(
            "satisfied",
            next(x["status"] for x in p["ruleOutcomes"]
                 if x["ruleId"] == "test.shared-affinity"),
        )

    def test_category_material_constraint_detects_explicit_wrong_material(self):
        p = self.evaluate(self.sample(skinMaterial="test.bronze-alloy"))
        self.assertEqual("default-rule-conflict", p["status"])
        self.assertEqual(
            "violated",
            next(x["status"] for x in p["ruleOutcomes"]
                 if x["ruleId"] == "test.category-material-a"),
        )

    def test_shared_affinity_requirement_is_just_synthetic_evidence(self):
        p = self.evaluate(
            self.sample(resistanceDamageType="lightning")
        )
        self.assertEqual("default-rule-conflict", p["status"])
        self.assertEqual(
            "required-relationship-not-met",
            next(x["reason"] for x in p["ruleOutcomes"]
                 if x["ruleId"] == "test.shared-affinity"),
        )

    def test_missing_trigger_is_unresolved_not_silently_non_applicable(self):
        values = self.sample()
        del values["category"]
        p = self.evaluate(values)
        self.assertEqual("incomplete-selection-needs-review", p["status"])
        self.assertEqual(
            "trigger-not-selected",
            next(x["reason"] for x in p["ruleOutcomes"]
                 if x["ruleId"] == "test.category-material-a"),
        )

    def test_missing_required_and_reference_dimensions_remain_unresolved(self):
        values = self.sample()
        del values["skinMaterial"]
        del values["damageType"]
        p = self.evaluate(values)
        self.assertEqual("incomplete-selection-needs-review", p["status"])
        self.assertIn(
            "required-dimension-not-selected",
            {x.get("reason") for x in p["ruleOutcomes"]},
        )
        self.assertIn(
            "reference-dimension-not-selected",
            {x.get("reason") for x in p["ruleOutcomes"]},
        )

    def test_exclusion_detected_without_any_exception(self):
        p = self.evaluate({
            "category": "test.glass",
            "skinMaterial": "test.glass-skin",
            "damageType": "fire",
            "resistanceDamageType": "fire",
        })
        self.assertEqual("no-detected-conflicts-not-certified", p["status"])
        self.assertEqual([], p["reviewedExceptions"])
        self.assertEqual(
            "satisfied",
            next(x["status"] for x in p["ruleOutcomes"]
                 if x["ruleId"] == "test.glass-cold-exclusion"),
        )

    def test_exact_exception_marks_review_not_automatic_permission(self):
        p = self.evaluate({
            "category": "test.glass",
            "skinMaterial": "test.glass-skin",
            "damageType": "cold",
            "resistanceDamageType": "cold",
        })
        self.assertEqual("authored-exception-needs-approval", p["status"])
        self.assertEqual(
            ["test.glass-cold-review"],
            [x["exceptionId"] for x in p["reviewedExceptions"]],
        )
        self.assertEqual(
            "requires-explicit-authority",
            p["reviewedExceptions"][0]["status"],
        )
        self.assertFalse(p["compatibilityCertified"])

    def test_exception_does_not_override_an_unrelated_violation(self):
        p = self.evaluate({
            "category": "test.glass",
            "skinMaterial": "test.glass-skin",
            "damageType": "cold",
            "resistanceDamageType": "fire",
        })
        self.assertEqual("default-rule-conflict", p["status"])
        self.assertEqual(1, len(p["reviewedExceptions"]))
        self.assertEqual(
            "violated",
            next(x["status"] for x in p["ruleOutcomes"]
                 if x["ruleId"] == "test.shared-affinity"),
        )

    def test_fixture_does_not_require_breath_area_or_infer_it(self):
        p = self.evaluate(self.sample(breathArea={
            "shape": "line", "lengthFeet": 30, "widthFeet": 5
        }))
        self.assertEqual(
            {"shape": "line", "lengthFeet": 30, "widthFeet": 5},
            p["selectedValues"]["breathArea"],
        )
        self.assertEqual("no-detected-conflicts-not-certified", p["status"])
        self.assertNotIn("manifestedCharacteristics", p)

    def test_unknown_dimensions_and_wrong_species_scope_are_rejected(self):
        with self.assertRaisesRegex(
            CompatibilityContractError, "Unknown or malformed"
        ):
            self.evaluate(self.sample(hiddenSpeciesTrait=True))
        with self.assertRaisesRegex(
            CompatibilityContractError, "does not cover"
        ):
            self.evaluate(self.sample(), root="species.human")

    def test_untrusted_schema_extensions_and_duplicate_ids_rejected(self):
        altered = copy.deepcopy(self.fixture)
        altered["rules"][0]["id"] = altered["rules"][1]["id"]
        with self.assertRaises(CompatibilityContractError):
            validate_compatibility_contract(altered)
        altered = copy.deepcopy(self.fixture)
        altered["rules"][0]["when"]["expression"] = "arbitrary-code"
        with self.assertRaises(CompatibilityContractError):
            validate_compatibility_contract(altered)
        altered = copy.deepcopy(self.fixture)
        altered["rules"][0]["requires"]["anyTrait"] = "x"
        with self.assertRaises(CompatibilityContractError):
            validate_compatibility_contract(altered)

    def test_invalid_exception_coverage_does_not_override_any_rule(self):
        for mutation in ("unknown-rule", "empty-match", "empty-reason"):
            bad = copy.deepcopy(self.fixture)
            if mutation == "unknown-rule":
                bad["exceptions"][0]["waiveRuleIds"] = ["not-a-rule"]
            elif mutation == "empty-match":
                bad["exceptions"][0]["match"] = {}
            else:
                bad["exceptions"][0]["reason"] = ""
            with self.subTest(mutation=mutation):
                with self.assertRaises(CompatibilityContractError):
                    validate_compatibility_contract(bad)

    def test_overlapping_exceptions_fail_closed(self):
        bad = copy.deepcopy(self.fixture)
        second = copy.deepcopy(bad["exceptions"][0])
        second["id"] = "test.duplicate-exception"
        second["match"] = {"damageType": "cold"}
        bad["exceptions"].append(second)
        with self.assertRaisesRegex(
            CompatibilityContractError, "Ambiguous overlapping exceptions"
        ):
            self.evaluate({
                "category": "test.glass",
                "skinMaterial": "test.glass-skin",
                "damageType": "cold",
                "resistanceDamageType": "cold",
            }, fixture=bad)

    def test_same_engine_can_read_another_synthetic_species_contract(self):
        other = copy.deepcopy(self.fixture)
        other["speciesRootId"] = "species.synthetic-new"
        p = self.evaluate(self.sample(), fixture=other)
        self.assertEqual("species.synthetic-new", p["speciesRootId"])
        self.assertEqual("no-detected-conflicts-not-certified", p["status"])

    def test_input_and_contract_remain_unchanged(self):
        values = self.sample()
        before = (copy.deepcopy(values), copy.deepcopy(self.fixture))
        one = self.evaluate(values)
        two = self.evaluate(values)
        self.assertEqual(one, two)
        one["selectedValues"]["skinMaterial"] = "changed"
        self.assertEqual(before, (values, self.fixture))


if __name__ == "__main__":
    unittest.main()
