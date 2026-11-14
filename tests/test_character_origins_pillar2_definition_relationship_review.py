"""Source-neutral trait definition relationship review, not canonical grants.

Same local names, declared rule references, and equal structured fields are
candidate evidence only. Neither semantics nor inheritance is inferred here.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_definition_relationship_review import (  # noqa: E402
    DefinitionReviewError, review_definition_relationships,
)
from character_origins_source_inventory import read_catalog  # noqa: E402


def source(identifier, *traits):
    return {
        "id": identifier,
        "kind": "species",
        "selectable": True,
        "traits": list(traits),
    }


def trait(identifier, **kwargs):
    return {"id": identifier, **kwargs}


class DefinitionRelationshipReviewTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()

    def review(self, catalog=None):
        return review_definition_relationships(
            self.catalog if catalog is None else catalog
        )

    @staticmethod
    def pair(report, first, second):
        expected = {first, second}
        return next(
            x for x in report["relationshipCandidates"]
            if {x["firstOccurrenceId"], x["secondOccurrenceId"]} == expected
        )

    def test_full_native_corpus_preserved_without_treating_ids_as_grants(self):
        report = self.review()
        expected = {
            f"{source_id}#trait:{trait['id']}": trait
            for source_id, record in self.catalog.items()
            for trait in record.get("traits", [])
        }
        actual = {
            x["sourceOccurrenceId"]: x["authoredTrait"]
            for x in report["sourceTraitDefinitions"]
        }
        self.assertEqual(expected, actual)
        self.assertEqual(len(expected), report["traitDefinitionCount"])
        self.assertTrue(all(
            x["mechanicalEquivalence"] == "not-established"
            and x["relationshipAuthority"] == "requires-manual-review"
            and x["grantsOrInheritance"] == "not-evaluated"
            for x in report["relationshipCandidates"]
        ))
        self.assertEqual("not-authorized", report["authority"])
        self.assertNotIn("manifestedCharacteristics", report)

    def test_orc_and_goliath_powerful_build_are_not_equated_by_local_name(self):
        report = self.review()
        pair = self.pair(
            report,
            "species.orc#trait:powerful-build",
            "species.goliath#trait:powerful-build",
        )
        self.assertIn("same-local-trait-id", pair["candidateReasons"])
        self.assertNotEqual(
            "identical-structured-fields-review",
            pair["classification"]
        )
        self.assertEqual("not-established", pair["mechanicalEquivalence"])

    def test_identical_structured_definitions_have_distinct_source_occurrences(self):
        catalog = {
            "species.first": source(
                "species.first",
                trait("alpha", description="First phrasing", rule="rule.example",
                      uses={"value": 1, "recharge": "long-rest"})
            ),
            "species.second": source(
                "species.second",
                trait("beta", description="Second phrasing", rule="rule.example",
                      uses={"recharge": "long-rest", "value": 1})
            ),
        }
        report = self.review(catalog)
        pair = self.pair(
            report, "species.first#trait:alpha", "species.second#trait:beta"
        )
        self.assertEqual(
            "identical-structured-fields-review",
            pair["classification"]
        )
        self.assertEqual(
            ["same-rule-reference", "same-complete-structured-fields"],
            pair["candidateReasons"]
        )
        self.assertTrue(pair["proseDescriptionsDiffer"])
        self.assertEqual("not-established", pair["mechanicalEquivalence"])
        self.assertEqual(2, report["traitDefinitionCount"])

    def test_matching_names_with_distinct_structures_require_review(self):
        catalog = {
            "species.one": source(
                "species.one", trait(
                    "armor", naturalArmor=12, description="One"
                )
            ),
            "species.two": source(
                "species.two", trait(
                    "armor", naturalArmor=13, description="Two"
                )
            ),
        }
        pair = self.pair(
            self.review(catalog),
            "species.one#trait:armor", "species.two#trait:armor",
        )
        self.assertEqual(
            "different-structured-fields-review",
            pair["classification"]
        )
        self.assertEqual(["naturalArmor"], pair["differingMechanicalFieldNames"])
        self.assertEqual(["same-local-trait-id"], pair["candidateReasons"])

    def test_potential_extension_is_not_approved_as_an_upgrade(self):
        catalog = {
            "species.first": source("species.first", trait("build", carry=1)),
            "species.second": source(
                "species.second", trait("build", carry=1, escapeAdvantage=True)
            ),
        }
        pair = self.pair(
            self.review(catalog),
            "species.first#trait:build", "species.second#trait:build"
        )
        self.assertEqual(
            "first-structure-subset-of-second-review",
            pair["classification"]
        )
        self.assertEqual(["carry"], pair["matchingMechanicalFieldNames"])
        self.assertEqual(
            ["escapeAdvantage"], pair["differingMechanicalFieldNames"]
        )
        self.assertEqual("not-evaluated", pair["grantsOrInheritance"])

    def test_opposite_subset_direction_preserves_authored_order(self):
        catalog = {
            "species.first": source("species.first", trait(
                "build", carry=1, escapeAdvantage=True
            )),
            "species.second": source("species.second", trait("build", carry=1)),
        }
        pair = self.pair(
            self.review(catalog),
            "species.first#trait:build", "species.second#trait:build"
        )
        self.assertEqual(
            "second-structure-subset-of-first-review",
            pair["classification"]
        )

    def test_prose_only_traits_do_not_claim_identical_mechanics(self):
        catalog = {
            "species.first": source("species.first", trait(
                "lore", name="Lore", description="Knows many things"
            )),
            "species.second": source("species.second", trait(
                "lore", name="Lore", description="Trained in some lore"
            )),
        }
        pair = self.pair(
            self.review(catalog),
            "species.first#trait:lore", "species.second#trait:lore"
        )
        self.assertEqual(
            "insufficient-structured-evidence", pair["classification"]
        )
        self.assertNotIn(
            "same-complete-structured-fields", pair["candidateReasons"]
        )

    def test_same_rule_reference_can_require_review_for_different_fields(self):
        catalog = {
            "species.first": source("species.first", trait(
                "vision-a", rule="rule.shared", rangeFeet=60
            )),
            "species.second": source("species.second", trait(
                "vision-b", rule="rule.shared", rangeFeet=120
            )),
        }
        pair = self.pair(
            self.review(catalog),
            "species.first#trait:vision-a", "species.second#trait:vision-b"
        )
        self.assertIn("same-rule-reference", pair["candidateReasons"])
        self.assertIn("rangeFeet", pair["differingMechanicalFieldNames"])
        self.assertEqual("not-established", pair["mechanicalEquivalence"])

    def test_same_symbolic_template_requires_source_binding_context(self):
        catalog = {
            "species.first": source("species.first", trait(
                "breath", damage={"typeFrom": "selected-element"},
                activation={"action": "action"}
            )),
            "species.second": source("species.second", trait(
                "breath", damage={"typeFrom": "selected-element"},
                activation={"action": "action"}
            )),
        }
        report = self.review(catalog)
        pair = self.pair(
            report,
            "species.first#trait:breath", "species.second#trait:breath"
        )
        self.assertEqual(
            "identical-structured-fields-review", pair["classification"]
        )
        self.assertTrue(pair["symbolicBindingContextRequired"])
        self.assertEqual(
            ["damage.typeFrom"],
            report["sourceTraitDefinitions"][0]["symbolicReferencePaths"]
        )
        self.assertEqual("not-established", pair["mechanicalEquivalence"])

    def test_shared_common_field_alone_is_not_a_mechanical_identity(self):
        catalog = {
            "species.first": source("species.first", trait(
                "flight", speedFeet=30, movementMode="fly"
            )),
            "species.second": source("species.second", trait(
                "climb", speedFeet=30, movementMode="climb"
            )),
        }
        report = self.review(catalog)
        self.assertEqual(0, report["relationshipCandidateCount"])

    def test_new_homebrew_definition_is_included_without_dispatch(self):
        expanded = copy.deepcopy(self.catalog)
        expanded["species.homebrew"] = source(
            "species.homebrew",
            trait("powerful-build", carry=3, description="Homebrew effect")
        )
        result = self.review(expanded)
        source_id = "species.homebrew#trait:powerful-build"
        self.assertIn(
            source_id,
            {x["sourceOccurrenceId"] for x in result["sourceTraitDefinitions"]}
        )
        self.assertTrue(any(
            source_id in (x["firstOccurrenceId"], x["secondOccurrenceId"])
            for x in result["relationshipCandidates"]
        ))
        self.assertEqual("not-authorized", result["authority"])

    def test_repeated_local_trait_ids_are_rejected_within_one_source(self):
        catalog = {
            "species.first": source(
                "species.first", trait("same", value=1),
                trait("same", value=2)
            )
        }
        with self.assertRaisesRegex(
            DefinitionReviewError, "Duplicate local trait ID"
        ):
            self.review(catalog)

    def test_invalid_and_non_json_data_fail_closed(self):
        for catalog in (
            {"species.first": {"id": "wrong", "traits": []}},
            {"species.first": source("species.first", {"name": "No ID"})},
            {"species.first": source("species.first", trait("a", value=float("nan")))},
            {"species.first": source("species.first", trait("a", value=set()))},
        ):
            with self.subTest(catalog=str(catalog)):
                with self.assertRaises(DefinitionReviewError):
                    self.review(catalog)

    def test_determinism_and_inputs_unchanged(self):
        catalog = copy.deepcopy(self.catalog)
        before = copy.deepcopy(catalog)
        first = self.review(catalog)
        self.assertEqual(first, self.review(catalog))
        first["sourceTraitDefinitions"][0]["authoredTrait"].clear()
        first["relationshipCandidates"].clear()
        self.assertEqual(before, catalog)


if __name__ == "__main__":
    unittest.main()
