"""Generic characteristic reconciliation evidence, no grants or policy.

All cases exercise source-neutral relationships between *proposed* choices,
not a playable Origin or approved duplication, overlap or upgrade rule.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_characteristic_identity import (  # noqa: E402
    project_characteristic_identity_evidence, read_identity_mapping,
)
from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_curated_identity_review import (  # noqa: E402
    read_curated_evidence,
)
from character_origins_manifestation_inventory import (  # noqa: E402
    characteristic_candidate_pool, draft_manifestation_profile,
)
from character_origins_parameter_inheritance import read_blueprints  # noqa: E402
from character_origins_reconciliation_review import (  # noqa: E402
    ReconciliationReviewError, review_proposed_manifestation,
)
from character_origins_source_inventory import read_catalog  # noqa: E402


def direct(cid, source):
    return {"contributorId": cid, "selectedSourceId": source}


class ReconciliationReviewTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        cls.blueprints = read_blueprints()
        cls.identity_mapping = read_identity_mapping()
        cls.curated = read_curated_evidence()

    def identity(self, contributors, registry=None, curated=True):
        pool = characteristic_candidate_pool(
            contributors, {} if registry is None else registry,
            self.blueprints, self.catalog, self.contracts
        )
        return project_characteristic_identity_evidence(
            pool, self.identity_mapping,
            curated=self.curated if curated else None,
            catalog=self.catalog if curated else None,
        )

    @staticmethod
    def locate(report, source_id=None, dimension=None, contributor=None,
               value=None):
        found = []
        for row in report["candidates"]:
            if source_id is not None and (
                row["sourceTraitOrFieldId"] != source_id
            ):
                continue
            if dimension is not None and row.get("intrinsicSlotId") != dimension:
                continue
            if contributor is not None and (
                row["contributorProvenance"]["contributorId"] != contributor
            ):
                continue
            if value is not None and row.get("value") != value:
                continue
            found.append(row["candidateId"])
        return found

    def review(self, report, *candidate_ids):
        return review_proposed_manifestation(report, list(candidate_ids))

    @staticmethod
    def one_relation(result, kind):
        rows = [x for x in result["pairwiseRelationships"]
                if x["relation"] == kind]
        if len(rows) != 1:
            raise AssertionError(f"Expected one {kind}, got {rows}")
        return rows[0]

    def test_identical_intrinsic_values_are_not_additive_grants(self):
        r = self.identity([
            direct("human", "species.human"),
            direct("orc", "species.orc"),
        ])
        ids = self.locate(r, dimension="creature.type")
        self.assertEqual(2, len(ids))
        out = self.review(r, *ids)
        self.one_relation(out, "same-intrinsic-slot-and-value")
        self.assertFalse(out["traitsGranted"])
        self.assertEqual("not-authorized", out["authorization"])

    def test_competing_different_species_creature_types_require_review(self):
        r = self.identity([
            direct("human", "species.human"),
            direct("elf", "ancestry.elf.high"),
        ])
        ids = self.locate(r, dimension="creature.type")
        self.assertEqual(2, len(ids))
        out = self.review(r, *ids)
        self.one_relation(out, "competing-intrinsic-slot-values")
        self.assertEqual(
            "relationships-require-general-policy",
            out["reconciliationStatus"]
        )
        self.assertFalse(out["traitsGranted"])

    def test_two_human_size_options_are_alternatives_not_species_conflict(self):
        r = self.identity([direct("human", "species.human")])
        ids = self.locate(r, dimension="creature.size")
        self.assertEqual(2, len(ids))
        out = self.review(r, *ids)
        self.one_relation(
            out, "alternatives-in-one-authored-intrinsic-choice"
        )
        self.assertEqual("unresolved-no-grants",
                         out["pairwiseRelationships"][0]["resolution"])

    def test_repeated_contributors_do_not_double_grant_same_breath_weapon(self):
        r = self.identity([
            direct("red-one", "ancestry.dragonborn.red"),
            direct("red-two", "ancestry.dragonborn.red"),
        ])
        ids = self.locate(r, source_id="species.dragonborn#trait:breath-weapon")
        self.assertEqual(2, len(ids))
        out = self.review(r, *ids)
        self.one_relation(
            out, "repeated-source-trait-same-configuration"
        )
        self.assertIsNone(
            out["selectedCandidateEvidence"][0]["grantInstanceId"]
        )
        self.assertEqual(
            ["red-one", "red-two"],
            [i["contributorProvenance"]["contributorId"]
             for i in out["selectedCandidateEvidence"]]
        )

    def test_same_breath_weapon_with_different_contributor_parameters(self):
        r = self.identity([
            direct("red", "ancestry.dragonborn.red"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        ])
        ids = self.locate(r, source_id="species.dragonborn#trait:breath-weapon")
        self.assertEqual(2, len(ids))
        out = self.review(r, *ids)
        self.one_relation(
            out, "repeated-source-trait-different-configurations"
        )
        self.assertEqual(
            {"fire", "lightning"},
            {x["parameters"]["damageType"]
             for x in out["selectedCandidateEvidence"]}
        )
        # The approved ability-level recombination of type and COMPLETE area
        # belongs to the separate binding/manifestation mechanism, NOT this
        # pairwise interaction review.

    def test_explicit_same_definition_mapping_does_not_merge_low_light(self):
        r = self.identity([
            direct("orc", "species.orc"),
            direct("elf", "ancestry.elf.high"),
        ])
        ids = [
            x["candidateId"] for x in r["candidates"]
            if x["sourceTraitOrFieldId"].endswith(
                "#trait:low-light-vision"
            )
        ]
        self.assertEqual(2, len(ids))
        out = self.review(r, *ids)
        self.one_relation(
            out, "explicit-identity-mapping-same-configuration"
        )
        self.assertFalse(out["traitsGranted"])
        self.assertEqual(0, sum(
            bool(x["grantInstanceId"])
            for x in out["selectedCandidateEvidence"]
        ))

    def test_different_native_amphibious_sources_use_literal_identity_evidence(self):
        r = self.identity([
            direct("black", "ancestry.dragonborn.black"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        ])
        ids = [
            x["candidateId"] for x in r["candidates"]
            if x["sourceTraitOrFieldId"].endswith("#trait:amphibious")
        ]
        self.assertEqual(2, len(ids))
        out = self.review(r, *ids)
        self.one_relation(
            out, "identical-authored-content-same-configuration"
        )
        self.assertEqual(
            "literal-evidence-not-semantic-or-grant-policy",
            out["pairwiseRelationships"][0]["reasonForReview"]
        )

    def test_shared_concept_different_definitions_does_not_imply_upgrade(self):
        r = self.identity([
            direct("orc", "species.orc"),
            direct("goliath", "ancestry.goliath.cloud"),
        ])
        ids = [
            x["candidateId"] for x in r["candidates"]
            if x["sourceTraitOrFieldId"].endswith("#trait:powerful-build")
        ]
        self.assertEqual(2, len(ids))
        out = self.review(r, *ids)
        self.one_relation(
            out, "related-trait-concept-distinct-definitions"
        )
        self.assertEqual(
            "conceptual-family-does-not-establish-upgrade",
            out["pairwiseRelationships"][0]["reasonForReview"]
        )

    def test_generic_mechanical_component_overlap_without_identity_mapping(self):
        synthetic = {
            "status": "origin-characteristic-candidates-only",
            "offers": [
                {
                    "kind": "trait", "identity": "study.a#trait:carry",
                    "sourceOccurrenceId": None,
                    "parameters": {},
                    "authoredDefinition": {
                        "effectiveSizeCategoriesHigherForCarryingCapacity": 1,
                        "unrelatedExtra": "alpha",
                    },
                    "provenance": {
                        "kind": "custom-proposal", "contributorId": "one",
                        "proposedAuthorityRef": "study.authority",
                    },
                },
                {
                    "kind": "trait", "identity": "study.b#trait:lift",
                    "sourceOccurrenceId": None,
                    "parameters": {},
                    "authoredDefinition": {
                        "effectiveSizeCategoriesHigherForCarryingCapacity": 1,
                        "unrelatedExtra": "beta",
                    },
                    "provenance": {
                        "kind": "custom-proposal", "contributorId": "two",
                        "proposedAuthorityRef": "study.authority",
                    },
                },
            ], "reviewFlags": [], "deferredSourceFields": []
        }
        r = project_characteristic_identity_evidence(
            synthetic, self.identity_mapping
        )
        out = self.review(r, "evidence-1", "evidence-2")
        self.one_relation(out, "shared-component-possible-overlap")
        self.assertFalse(out["traitsGranted"])

    def test_matching_trait_intrinsic_movement_is_not_a_second_speed(self):
        r = self.identity([direct("red", "ancestry.dragonborn.red")])
        trait = self.locate(
            r, source_id="ancestry.dragonborn.red#trait:natural-climber"
        )
        intrinsic = self.locate(r, dimension="movement.climb.speed")
        self.assertEqual((1, 1), (len(trait), len(intrinsic)))
        out = self.review(r, *trait, *intrinsic)
        self.one_relation(
            out, "trait-and-intrinsic-movement-same-value"
        )
        self.assertEqual(
            "possible-mirrored-property-not-a-second-grant",
            out["pairwiseRelationships"][0]["reasonForReview"]
        )

    def test_unequal_trait_intrinsic_movement_remains_unresolved(self):
        r = self.identity([direct("red", "ancestry.dragonborn.red")])
        trait = self.locate(
            r, source_id="ancestry.dragonborn.red#trait:natural-climber"
        )[0]
        intrinsic = self.locate(r, dimension="movement.climb.speed")[0]
        changed = copy.deepcopy(r)
        item = next(x for x in changed["candidates"]
                    if x["candidateId"] == intrinsic)
        item["value"] = 20
        out = self.review(changed, trait, intrinsic)
        self.one_relation(
            out, "trait-and-intrinsic-movement-different-values"
        )
        self.assertFalse(out["traitsGranted"])

    def test_unrelated_traits_do_not_trigger_unsupported_compatibility_claim(self):
        r = self.identity([
            direct("orc", "species.orc"),
            direct("human", "species.human"),
        ])
        a = self.locate(r, source_id="species.orc#trait:adrenaline-rush")
        b = self.locate(r, source_id="species.human#trait:skillful")
        out = self.review(r, *a, *b)
        self.assertEqual([], out["pairwiseRelationships"])
        self.assertNotEqual("authorized", out["reconciliationStatus"])
        self.assertEqual("not-authorized", out["authorization"])

    def test_nested_contributor_evidence_survives_selection(self):
        parent = draft_manifestation_profile(
            "parent", [direct("orc", "species.orc")],
            [{"kind": "trait",
              "identity": "species.orc#trait:low-light-vision",
              "parameters": {}}],
            {}, self.blueprints, self.catalog, self.contracts
        )
        r = self.identity([
            {"contributorId": "parent", "profileId": "parent"},
            direct("elf", "ancestry.elf.high"),
        ], {"parent": parent})
        ids = [
            x["candidateId"] for x in r["candidates"]
            if x["sourceTraitOrFieldId"].endswith(
                "#trait:low-light-vision"
            )
        ]
        out = self.review(r, *ids)
        self.assertEqual(2, len(out["selectedCandidateEvidence"]))
        self.assertIn(
            "manifested-profile",
            {x["contributorProvenance"]["kind"]
             for x in out["selectedCandidateEvidence"]}
        )
        self.assertEqual("not-authorized", out["authorization"])

    def test_reject_unknown_duplicate_or_malformed_candidate_ids(self):
        r = self.identity([direct("orc", "species.orc")])
        with self.assertRaises(ReconciliationReviewError):
            self.review(r, "unknown")
        with self.assertRaises(ReconciliationReviewError):
            self.review(r, "evidence-1", "evidence-1")
        with self.assertRaises(ReconciliationReviewError):
            review_proposed_manifestation(r, None)

    def test_reject_projection_already_claiming_a_grant_or_permission(self):
        r = self.identity([direct("orc", "species.orc")])
        bad = copy.deepcopy(r)
        bad["candidates"][0]["grantInstanceId"] = "invented-grant"
        with self.assertRaisesRegex(
            ReconciliationReviewError, "already granted"
        ):
            self.review(bad, "evidence-1")
        bad = copy.deepcopy(r)
        bad["authority"] = "approved"
        with self.assertRaisesRegex(
            ReconciliationReviewError, "Expected a complete"
        ):
            self.review(bad, "evidence-1")

    def test_order_and_input_evidence_are_preserved_without_mutation(self):
        r = self.identity([
            direct("red", "ancestry.dragonborn.red"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        ])
        ids = self.locate(
            r, source_id="species.dragonborn#trait:breath-weapon"
        )
        before = copy.deepcopy(r)
        out = self.review(r, *ids)
        again = self.review(r, *ids)
        self.assertEqual(out, again)
        self.assertEqual(ids, out["proposedCandidateIds"])
        self.assertEqual(
            [next(x for x in r["candidates"] if x["candidateId"] == i)
             for i in ids],
            out["selectedCandidateEvidence"],
        )
        out["selectedCandidateEvidence"][0]["originalOffer"].clear()
        self.assertEqual(before, r)


if __name__ == "__main__":
    unittest.main()
