"""Experimental explicit reconciliation decisions do not authorize grants.

The decision validator must bind to a specific candidate evidence snapshot,
require declared proposed authority, reject duplicate/stale decisions, and
review whole-proposal consistency across relationships rather than applying
pairwise choices independently.
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
from character_origins_curated_identity_review import read_curated_evidence  # noqa: E402
from character_origins_manifestation_inventory import (  # noqa: E402
    characteristic_candidate_pool, draft_manifestation_profile,
)
from character_origins_parameter_inheritance import read_blueprints  # noqa: E402
from character_origins_reconciliation_decisions import (  # noqa: E402
    ReconciliationDecisionError, examine_reconciliation_decisions,
    proposal_fingerprint,
)
from character_origins_reconciliation_review import (  # noqa: E402
    review_proposed_manifestation,
)
from character_origins_source_inventory import read_catalog  # noqa: E402


def direct(key, selected):
    return {"contributorId": key, "selectedSourceId": selected}


class ReconciliationDecisionsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        cls.blueprints = read_blueprints()
        cls.mapping = read_identity_mapping()
        cls.curated = read_curated_evidence()

    def preview(self, contributors, predicate, registry=None):
        pool = characteristic_candidate_pool(
            contributors, {} if registry is None else registry,
            self.blueprints, self.catalog, self.contracts
        )
        identity = project_characteristic_identity_evidence(
            pool, self.mapping, curated=self.curated, catalog=self.catalog
        )
        ids = [row["candidateId"] for row in identity["candidates"]
               if predicate(row)]
        return review_proposed_manifestation(identity, ids)

    @staticmethod
    def relationship(report, kind=None):
        matches = [x for x in report["pairwiseRelationships"]
                   if kind is None or x["relation"] == kind]
        if len(matches) != 1:
            raise AssertionError(f"Expected one relationship, got {matches}")
        return matches[0]

    @staticmethod
    def decision(relation, disposition, kind="design-hypothesis",
                 authority="study.provisional", reason="Review this choice"):
        return {
            "firstCandidateId": relation["firstCandidateId"],
            "secondCandidateId": relation["secondCandidateId"],
            "relation": relation["relation"],
            "disposition": disposition,
            "authority": {"kind": kind, "reference": authority},
            "rationale": reason,
        }

    @staticmethod
    def examine(report, *decisions, fingerprint=None):
        return examine_reconciliation_decisions(report, {
            "status": "experimental-decision-proposal",
            "proposalFingerprint": (
                proposal_fingerprint(report) if fingerprint is None
                else fingerprint
            ),
            "decisions": list(decisions),
        })

    def test_unknown_relationships_require_no_contrived_policy(self):
        report = self.preview([
            direct("orc", "species.orc"),
            direct("human", "species.human"),
        ], lambda x: x["sourceTraitOrFieldId"] in (
            "species.orc#trait:adrenaline-rush",
            "species.human#trait:skillful",
        ))
        self.assertEqual([], report["pairwiseRelationships"])
        result = self.examine(report)
        self.assertEqual([], result["unreviewedRelationships"])
        self.assertEqual("complete-proposal-not-authorized",
                         result["reviewStatus"])
        self.assertFalse(result["grantsIssued"])
        self.assertEqual("not-evaluated", result["fullDependencyClosure"])

    def test_incomplete_review_keeps_exact_relationship_visible(self):
        report = self.preview([
            direct("human", "species.human"),
            direct("elf", "ancestry.elf.high"),
        ], lambda x: x.get("intrinsicSlotId") == "creature.type")
        result = self.examine(report)
        self.assertEqual("incomplete-relation-decisions",
                         result["reviewStatus"])
        self.assertEqual(
            [report["pairwiseRelationships"][0]["relation"]],
            [x["relation"] for x in result["unreviewedRelationships"]],
        )
        self.assertFalse(result["grantsIssued"])

    def test_competing_intrinsic_choice_remains_unapproved_when_one_chosen(self):
        report = self.preview([
            direct("human", "species.human"),
            direct("elf", "ancestry.elf.high"),
        ], lambda x: x.get("intrinsicSlotId") == "creature.type")
        rel = self.relationship(report, "competing-intrinsic-slot-values")
        chosen = self.decision(rel, "choose-first")
        result = self.examine(report, chosen)
        self.assertEqual("complete-proposal-not-authorized",
                         result["reviewStatus"])
        self.assertEqual("pending-actual-policy-authorization",
                         result["reviewedDecisions"][0]["status"])
        self.assertEqual("not-authorized", result["authorization"])
        self.assertEqual("not-evaluated", result["selectionEligibility"])

    def test_preserve_both_competing_intrinsics_requires_special_slot_rule(self):
        report = self.preview([
            direct("human", "species.human"),
            direct("elf", "ancestry.elf.high"),
        ], lambda x: x.get("intrinsicSlotId") == "creature.type")
        proposed = self.decision(
            self.relationship(report), "preserve-both",
            kind="table-proposal", authority="study.dm-proposal"
        )
        result = self.examine(report, proposed)
        self.assertEqual("requires-additional-intrinsic-policy",
                         result["reviewStatus"])
        self.assertEqual(1, len(result["intrinsicSlotPolicyGaps"]))
        self.assertFalse(result["grantsIssued"])

    def test_human_size_options_cannot_both_be_preserved_as_default(self):
        report = self.preview(
            [direct("human", "species.human")],
            lambda x: x.get("intrinsicSlotId") == "creature.size"
        )
        rel = self.relationship(
            report, "alternatives-in-one-authored-intrinsic-choice"
        )
        result = self.examine(report, self.decision(rel, "preserve-both"))
        self.assertEqual("requires-additional-intrinsic-policy",
                         result["reviewStatus"])
        self.assertEqual(
            "not-authorized", result["authorization"]
        )

    def test_repeated_breath_with_equal_parameters_may_propose_consolidation(self):
        report = self.preview([
            direct("one", "ancestry.dragonborn.red"),
            direct("two", "ancestry.dragonborn.red"),
        ], lambda x: x["sourceTraitOrFieldId"] ==
            "species.dragonborn#trait:breath-weapon")
        rel = self.relationship(
            report, "repeated-source-trait-same-configuration"
        )
        result = self.examine(
            report, self.decision(rel, "consolidate-evidence")
        )
        self.assertEqual("complete-proposal-not-authorized",
                         result["reviewStatus"])
        self.assertEqual(2, len(report["selectedCandidateEvidence"]))
        self.assertEqual("not-authorized", result["authorization"])
        self.assertFalse(result["grantsIssued"])

    def test_different_breath_parameters_are_not_automatically_consolidated(self):
        report = self.preview([
            direct("red", "ancestry.dragonborn.red"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        ], lambda x: x["sourceTraitOrFieldId"] ==
            "species.dragonborn#trait:breath-weapon")
        rel = self.relationship(
            report, "repeated-source-trait-different-configurations"
        )
        with self.assertRaisesRegex(
            ReconciliationDecisionError, "Insufficient identity evidence"
        ):
            self.examine(
                report, self.decision(rel, "consolidate-evidence")
            )

    def test_literal_identical_amphibious_grants_are_evidence_only(self):
        report = self.preview([
            direct("black", "ancestry.dragonborn.black"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        ], lambda x: x["sourceTraitOrFieldId"].endswith(
            "#trait:amphibious"
        ))
        rel = self.relationship(
            report, "identical-authored-content-same-configuration"
        )
        decision = self.decision(rel, "consolidate-evidence")
        result = self.examine(report, decision)
        self.assertEqual("not-authorized", result["authorization"])
        self.assertEqual(0, len(result["globalContradictions"]))
        self.assertFalse(result["grantsIssued"])

    def test_partial_mechanical_overlap_cannot_be_auto_consolidated(self):
        report = self.preview([
            direct("orc", "species.orc"),
            direct("goliath", "ancestry.goliath.cloud"),
        ], lambda x: x["sourceTraitOrFieldId"].endswith(
            "#trait:powerful-build"
        ))
        rel = self.relationship(
            report, "related-trait-concept-distinct-definitions"
        )
        with self.assertRaisesRegex(
            ReconciliationDecisionError, "Insufficient identity evidence"
        ):
            self.examine(
                report, self.decision(rel, "consolidate-evidence")
            )
        result = self.examine(
            report, self.decision(rel, "preserve-both")
        )
        self.assertEqual("complete-proposal-not-authorized",
                         result["reviewStatus"])
        self.assertEqual("not-evaluated", result["fullDependencyClosure"])

    def test_deferred_relation_stays_incomplete(self):
        report = self.preview([
            direct("human", "species.human"),
            direct("elf", "ancestry.elf.high"),
        ], lambda x: x.get("intrinsicSlotId") == "creature.type")
        rel = self.relationship(report)
        result = self.examine(
            report, self.decision(rel, "defer")
        )
        self.assertEqual("incomplete-relation-decisions",
                         result["reviewStatus"])
        self.assertEqual(1, len(result["explicitlyDeferredRelationships"]))

    def test_stale_snapshot_does_not_apply_to_a_changed_parameter_value(self):
        report = self.preview([
            direct("red", "ancestry.dragonborn.red"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        ], lambda x: x["sourceTraitOrFieldId"] ==
            "species.dragonborn#trait:breath-weapon")
        stale = proposal_fingerprint(report)
        updated = copy.deepcopy(report)
        updated["selectedCandidateEvidence"][0][
            "parameters"
        ]["damageType"] = "invented"
        with self.assertRaisesRegex(
            ReconciliationDecisionError, "stale or different"
        ):
            self.examine(updated, fingerprint=stale)

    def test_authority_proposal_is_not_claim_of_approved_dm_permission(self):
        report = self.preview([
            direct("one", "ancestry.dragonborn.red"),
            direct("two", "ancestry.dragonborn.red"),
        ], lambda x: x["sourceTraitOrFieldId"] ==
            "species.dragonborn#trait:breath-weapon")
        rel = self.relationship(report)
        invalid = self.decision(rel, "choose-first", kind="approved-dm")
        with self.assertRaisesRegex(
            ReconciliationDecisionError, "proposed authority"
        ):
            self.examine(report, invalid)
        invalid = self.decision(rel, "choose-first", authority="")
        with self.assertRaises(ReconciliationDecisionError):
            self.examine(report, invalid)

    def test_unknown_or_duplicate_relation_decision_rejected(self):
        report = self.preview([
            direct("human", "species.human"),
            direct("elf", "ancestry.elf.high"),
        ], lambda x: x.get("intrinsicSlotId") == "creature.type")
        rel = self.relationship(report)
        entry = self.decision(rel, "choose-first")
        with self.assertRaisesRegex(
            ReconciliationDecisionError, "multiple competing"
        ):
            self.examine(report, entry, entry)
        bad = copy.deepcopy(entry)
        bad["secondCandidateId"] = "not-selected"
        with self.assertRaisesRegex(
            ReconciliationDecisionError, "observed relationship"
        ):
            self.examine(report, bad)
        bad = copy.deepcopy(entry)
        bad["relation"] = "same-intrinsic-slot-and-value"
        with self.assertRaisesRegex(
            ReconciliationDecisionError, "does not match"
        ):
            self.examine(report, bad)

    def test_duplicate_reversed_pair_is_not_accepted(self):
        report = self.preview([
            direct("human", "species.human"),
            direct("elf", "ancestry.elf.high"),
        ], lambda x: x.get("intrinsicSlotId") == "creature.type")
        rel = self.relationship(report)
        reversed_order = self.decision(rel, "choose-first")
        reversed_order["firstCandidateId"], reversed_order[
            "secondCandidateId"
        ] = reversed_order["secondCandidateId"], reversed_order[
            "firstCandidateId"
        ]
        with self.assertRaisesRegex(
            ReconciliationDecisionError, "observed relationship"
        ):
            self.examine(report, reversed_order)

    def test_three_candidate_transitive_conflict_is_detected(self):
        report = self.preview([
            direct("human", "species.human"),
            direct("orc", "species.orc"),
            direct("halfling", "species.halfling"),
        ], lambda x: x.get("intrinsicSlotId") == "creature.type")
        self.assertEqual(3, len(report["pairwiseRelationships"]))
        a, b, c = report["proposedCandidateIds"]
        relations = {
            (x["firstCandidateId"], x["secondCandidateId"]): x
            for x in report["pairwiseRelationships"]
        }
        first = self.decision(relations[(a, b)], "preserve-both")
        second = self.decision(relations[(a, c)], "choose-second")
        third = self.decision(relations[(b, c)], "choose-first")
        result = self.examine(report, first, second, third)
        self.assertEqual("contradictory-proposal",
                         result["reviewStatus"])
        self.assertEqual({a, c}, {
            x["candidateId"] for x in result["globalContradictions"]
        })
        self.assertEqual([], result["unreviewedRelationships"])
        self.assertFalse(result["grantsIssued"])

    def test_nested_contributor_provenance_not_lost_in_decisions(self):
        parent = draft_manifestation_profile(
            "parent", [direct("orc", "species.orc")],
            [{"kind": "trait",
              "identity": "species.orc#trait:low-light-vision",
              "parameters": {}}],
            {}, self.blueprints, self.catalog, self.contracts
        )
        report = self.preview([
            {"contributorId": "mixed", "profileId": "parent"},
            direct("elf", "ancestry.elf.high"),
        ], lambda x: x["sourceTraitOrFieldId"].endswith(
            "#trait:low-light-vision"
        ), registry={"parent": parent})
        rel = self.relationship(report)
        result = self.examine(
            report, self.decision(rel, "preserve-both")
        )
        self.assertEqual(
            {"native-source", "manifested-profile"},
            {r["contributorProvenance"]["kind"]
             for r in report["selectedCandidateEvidence"]}
        )
        self.assertEqual("complete-proposal-not-authorized",
                         result["reviewStatus"])

    def test_source_diagnostics_preserved_not_conflated_with_selection(self):
        report = self.preview([
            direct("wood", "ancestry.elf.wood")
        ], lambda x: x["sourceTraitOrFieldId"] ==
            "ancestry.elf.wood#trait:druidcraft")
        self.assertEqual([], report["pairwiseRelationships"])
        result = self.examine(report)
        self.assertEqual(report["sourceReviewFlags"],
                         result["sourceReviewFlags"])
        self.assertEqual(
            "contains-unresolved-source-evidence",
            result["candidatePoolEvidenceStatus"],
        )
        self.assertEqual("complete-proposal-not-authorized",
                         result["reviewStatus"])

    def test_immutable_inputs_and_deterministic_snapshot(self):
        report = self.preview([
            direct("human", "species.human"),
            direct("elf", "ancestry.elf.high"),
        ], lambda x: x.get("intrinsicSlotId") == "creature.type")
        rel = self.relationship(report)
        decision = self.decision(rel, "choose-first")
        contract = {
            "status": "experimental-decision-proposal",
            "proposalFingerprint": proposal_fingerprint(report),
            "decisions": [decision]
        }
        original = (copy.deepcopy(report), copy.deepcopy(contract))
        first = examine_reconciliation_decisions(report, contract)
        second = examine_reconciliation_decisions(report, contract)
        self.assertEqual(first, second)
        first["reviewedDecisions"][0]["proposedAuthority"].clear()
        self.assertEqual((report, contract), original)


if __name__ == "__main__":
    unittest.main()
