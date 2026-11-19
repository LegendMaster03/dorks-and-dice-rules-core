"""Curated native trait identity evidence must fail closed on source drift.

Literal authored equality is not a grant, inheritance permission, duplicate
policy, or authoritative mechanic equivalence mapping.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_curated_identity_review import (  # noqa: E402
    CuratedIdentityReviewError, audit_curated_identity_evidence,
    read_curated_evidence,
)
from character_origins_source_inventory import read_catalog  # noqa: E402


class CuratedIdentityReviewTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.evidence = read_curated_evidence()

    def audit(self, catalog=None, evidence=None):
        return audit_curated_identity_evidence(
            self.catalog if catalog is None else catalog,
            self.evidence if evidence is None else evidence,
        )

    def test_current_literal_groups_are_identical_in_complete_authored_json(self):
        report = self.audit()
        groups = {
            x["candidateDefinitionId"]: x
            for x in report["identicalAuthoredGroups"]
        }
        amphibious = groups["study.definition.amphibious-air-water"]
        lowlight = groups["study.definition.low-light-vision"]
        self.assertEqual(
            {"ancestry.dragonborn.black#trait:amphibious",
             "ancestry.dragonborn.green#trait:amphibious",
             "ancestry.dragonborn.bronze#trait:amphibious",
             "ancestry.dragonborn.gold#trait:amphibious"},
            set(amphibious["sourceOccurrenceIds"]),
        )
        self.assertEqual(
            {"species.dragonborn#trait:low-light-vision",
             "species.elf#trait:low-light-vision",
             "species.gnome#trait:low-light-vision",
             "species.orc#trait:low-light-vision"},
            set(lowlight["sourceOccurrenceIds"]),
        )
        self.assertEqual(
            8, report["literalOccurrenceCoverageCount"]
        )
        self.assertEqual(
            {"identical-complete-authored-content"},
            {x["verifiedEquality"] for x in groups.values()},
        )
        self.assertTrue(all(
            x["semanticEquivalence"] == "not-promoted"
            and x["grantOrDuplicatePolicy"] == "not-evaluated"
            for x in groups.values()
        ))

    def test_tiefling_low_light_same_rule_but_prose_change_stays_review(self):
        report = self.audit()
        finding = next(
            x for x in report["semanticReviewQueue"]
            if x["reviewId"] == "study.review.low-light-tiefling"
        )
        self.assertEqual(
            "same-structure-different-prose",
            finding["observedEvidence"],
        )
        self.assertEqual("unresolved", finding["semanticEquivalence"])

    def test_red_copper_natural_climber_stays_review_not_automatic_identity(self):
        report = self.audit()
        finding = next(
            x for x in report["semanticReviewQueue"]
            if x["reviewId"] == "study.review.natural-climber"
        )
        self.assertEqual(
            "same-structure-different-prose", finding["observedEvidence"]
        )
        self.assertEqual("unresolved", finding["semanticEquivalence"])

    def test_orc_goliath_powerful_build_remains_a_different_mechanic(self):
        report = self.audit()
        finding = next(
            x for x in report["semanticReviewQueue"]
            if x["reviewId"] == "study.review.powerful-build"
        )
        self.assertEqual(
            "same-local-id-different-structure", finding["observedEvidence"]
        )
        self.assertEqual("unresolved", finding["semanticEquivalence"])

    def test_a_literal_group_fails_on_prose_drift_even_if_mechanics_equal(self):
        mutated = copy.deepcopy(self.catalog)
        mutated["ancestry.dragonborn.gold"]["traits"][0]["description"] += (
            " An extra new rule sentence."
        )
        with self.assertRaisesRegex(
            CuratedIdentityReviewError, "Complete authored definition drift"
        ):
            self.audit(catalog=mutated)

    def test_a_literal_group_fails_on_structural_mechanical_drift(self):
        mutated = copy.deepcopy(self.catalog)
        amended = next(
            x for x in mutated["species.gnome"]["traits"]
            if x["id"] == "low-light-vision"
        )
        amended["rule"] = "rule.different"
        with self.assertRaisesRegex(
            CuratedIdentityReviewError, "Complete authored definition drift"
        ):
            self.audit(catalog=mutated)

    def test_a_missing_or_retired_literal_source_fails(self):
        mutated = copy.deepcopy(self.catalog)
        mutated["ancestry.dragonborn.black"]["traits"] = []
        with self.assertRaisesRegex(
            CuratedIdentityReviewError, "Unknown or retired"
        ):
            self.audit(catalog=mutated)

    def test_near_match_fails_when_structured_mechanics_change(self):
        mutated = copy.deepcopy(self.catalog)
        mutated["ancestry.dragonborn.copper"]["traits"][0]["speedFeet"] = 20
        with self.assertRaisesRegex(
            CuratedIdentityReviewError, "Near-match evidence changed"
        ):
            self.audit(catalog=mutated)

    def test_near_match_fails_if_prose_converges(self):
        mutated = copy.deepcopy(self.catalog)
        red_desc = mutated["ancestry.dragonborn.red"]["traits"][0][
            "description"
        ]
        mutated["ancestry.dragonborn.copper"]["traits"][0][
            "description"
        ] = red_desc
        with self.assertRaisesRegex(
            CuratedIdentityReviewError, "Near-match evidence changed"
        ):
            self.audit(catalog=mutated)

    def test_unapproved_claim_or_invalid_schema_fails(self):
        for invalid in ("status", "extra-field", "unknown-evidence"):
            mutated = copy.deepcopy(self.evidence)
            if invalid == "status":
                mutated["status"] = "approved"
            elif invalid == "extra-field":
                mutated["identicalAuthoredGroups"][0]["stacking"] = "stack"
            else:
                mutated["semanticReviewGroups"][0]["expectedEvidence"] = (
                    "equivalent"
                )
            with self.subTest(invalid=invalid):
                with self.assertRaises(CuratedIdentityReviewError):
                    self.audit(evidence=mutated)

    def test_duplicate_literal_coverage_is_not_a_second_identity(self):
        mutated = copy.deepcopy(self.evidence)
        duplicated = copy.deepcopy(mutated["identicalAuthoredGroups"][0])
        duplicated["candidateDefinitionId"] = "study.definition.redundant"
        mutated["identicalAuthoredGroups"].append(duplicated)
        with self.assertRaisesRegex(
            CuratedIdentityReviewError, "conflicting literal-identity"
        ):
            self.audit(evidence=mutated)

    def test_duplicate_review_ids_and_unknown_references_fail(self):
        mutated = copy.deepcopy(self.evidence)
        mutated["semanticReviewGroups"].append(
            copy.deepcopy(mutated["semanticReviewGroups"][0])
        )
        with self.assertRaisesRegex(
            CuratedIdentityReviewError, "Malformed or duplicate"
        ):
            self.audit(evidence=mutated)
        mutated = copy.deepcopy(self.evidence)
        mutated["semanticReviewGroups"][0]["sourceOccurrenceIds"][1] = (
            "species.unknown#trait:vision"
        )
        with self.assertRaisesRegex(
            CuratedIdentityReviewError, "Unknown or retired"
        ):
            self.audit(evidence=mutated)

    def test_new_native_species_does_not_require_a_frozen_corpus_count(self):
        mutated = copy.deepcopy(self.catalog)
        mutated["species.homebrew-study"] = {
            "id": "species.homebrew-study",
            "kind": "species", "selectable": True,
            "traits": [{"id": "new-trait", "name": "New Trait",
                        "description": "A new custom trait.", "atWill": True}]
        }
        baseline = self.audit()
        expanded = self.audit(catalog=mutated)
        self.assertEqual(
            baseline["sourceTraitDefinitionCount"] + 1,
            expanded["sourceTraitDefinitionCount"],
        )
        self.assertEqual(
            baseline["literalOccurrenceCoverageCount"],
            expanded["literalOccurrenceCoverageCount"],
        )
        self.assertEqual(
            baseline["remainingSourceTraitDefinitions"] + 1,
            expanded["remainingSourceTraitDefinitions"],
        )

    def test_report_does_not_claim_compatibility_balance_or_grant(self):
        report = self.audit()
        self.assertEqual("not-authorized", report["authority"])
        self.assertEqual("not-evaluated", report["balance"])
        self.assertNotIn("manifestedCharacteristics", report)
        self.assertNotIn("approvedDefinitions", report)
        self.assertEqual(
            {"not-evaluated"},
            {x["grantOrDuplicatePolicy"]
             for x in report["semanticReviewQueue"]}
        )

    def test_deterministic_and_does_not_mutate_corpus_or_review_inputs(self):
        catalog, evidence = (
            copy.deepcopy(self.catalog), copy.deepcopy(self.evidence)
        )
        before = (copy.deepcopy(catalog), copy.deepcopy(evidence))
        first = self.audit(catalog=catalog, evidence=evidence)
        second = self.audit(catalog=catalog, evidence=evidence)
        self.assertEqual(first, second)
        first["identicalAuthoredGroups"][0]["sourceOccurrenceIds"].clear()
        self.assertEqual(before, (catalog, evidence))


if __name__ == "__main__":
    unittest.main()
