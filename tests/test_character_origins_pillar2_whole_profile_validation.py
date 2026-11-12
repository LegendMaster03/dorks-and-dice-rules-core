"""Source-neutral whole Origin closure remains an evidence-only experiment."""
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
    proposal_fingerprint,
)
from character_origins_reconciliation_review import (  # noqa: E402
    review_proposed_manifestation,
)
from character_origins_source_inventory import read_catalog  # noqa: E402
from character_origins_whole_profile_validation import (  # noqa: E402
    WholeProfileReviewError, review_whole_origin_profile,
    validate_dependency_study,
)


def source(key, selected):
    return {"contributorId": key, "selectedSourceId": selected}


def sel(kind, identifier, **params):
    return {"kind": kind, "id": identifier, **params}


def entry(id_, kind, **kwargs):
    return {"id": id_, "kind": kind, **kwargs}


class WholeProfileDependencyTests(unittest.TestCase):
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
            self.blueprints, self.catalog, self.contracts,
        )
        return self.from_pool(pool, predicate)

    def from_pool(self, pool, predicate):
        identity = project_characteristic_identity_evidence(
            pool, self.mapping, curated=(
                self.curated if all(
                    x.get("provenance", {}).get("kind") == "native-source"
                    for x in pool["offers"]
                ) else None
            ),
            catalog=self.catalog,
        )
        ids = [row["candidateId"] for row in identity["candidates"]
               if predicate(row)]
        return review_proposed_manifestation(identity, ids)

    @staticmethod
    def decision_contract(preview, dispositions=()):
        relationships = preview["pairwiseRelationships"]
        if len(dispositions) != len(relationships):
            raise AssertionError("Provide one action for each observed pair")
        return {
            "status": "experimental-decision-proposal",
            "proposalFingerprint": proposal_fingerprint(preview),
            "decisions": [{
                "firstCandidateId": relationship["firstCandidateId"],
                "secondCandidateId": relationship["secondCandidateId"],
                "relation": relationship["relation"],
                "disposition": disposition,
                "authority": {
                    "kind": "design-hypothesis",
                    "reference": "study.whole-profile",
                },
                "rationale": "Proposed study only",
            } for relationship, disposition in zip(
                relationships, dispositions
            )],
        }

    def evaluate(self, preview, rules, dispositions=(), choices=None,
                 contract=None):
        return review_whole_origin_profile(
            preview,
            contract if contract is not None else self.decision_contract(
                preview, dispositions,
            ),
            {"status": "synthetic-dependency-study", "rules": rules},
            choices,
        )

    @staticmethod
    def check_status(out, expected, rule=None):
        candidates = out["dependencyRuleOutcomes"]
        if rule is not None:
            candidates = [x for x in candidates if x["ruleId"] == rule]
        if len(candidates) != 1:
            raise AssertionError(f"Expected one rule outcome: {candidates}")
        self_status = candidates[0]["status"]
        if self_status != expected:
            raise AssertionError(
                f"Expected {expected}, got {self_status}"
            )

    def test_no_declarations_does_not_certify_standard_character(self):
        preview = self.preview(
            [source("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        out = self.evaluate(preview, [])
        self.assertEqual(
            "declared-dependencies-require-review",
            out["wholeProfileReviewStatus"],
        )
        self.assertTrue(out["authoredChoiceObservations"])
        self.assertEqual("not-certified", out["fullDependencyClosure"])
        self.assertFalse(out["grantsIssued"])

    def test_missing_prerequisite_for_a_selected_trait(self):
        preview = self.preview(
            [source("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        rules = [entry(
            "study.skillful-requires-rush", "requires-any",
            subject=sel("trait-source", "species.human#trait:skillful"),
            oneOf=[sel("trait-source", "species.orc#trait:adrenaline-rush")],
        )]
        out = self.evaluate(preview, rules)
        self.check_status(out, "missing-prerequisite")
        self.assertEqual(
            "declared-dependency-conflicts",
            out["wholeProfileReviewStatus"],
        )

    def test_prerequisite_satisfied_by_a_second_contributor(self):
        preview = self.preview(
            [source("human", "species.human"),
             source("orc", "species.orc")],
            lambda x: x["sourceTraitOrFieldId"] in (
                "species.human#trait:skillful",
                "species.orc#trait:adrenaline-rush",
            ),
        )
        rules = [entry(
            "study.required-trait", "requires-any",
            subject=sel("trait-source", "species.human#trait:skillful"),
            oneOf=[sel("trait-source", "species.orc#trait:adrenaline-rush")],
        )]
        out = self.evaluate(preview, rules)
        self.check_status(out, "satisfied-evidence")
        self.assertEqual("not-authorized", out["authorization"])

    def test_trait_exclusion_is_detected_across_whole_selection(self):
        preview = self.preview(
            [source("human", "species.human"),
             source("orc", "species.orc")],
            lambda x: x["sourceTraitOrFieldId"] in (
                "species.human#trait:skillful",
                "species.orc#trait:adrenaline-rush",
            ),
        )
        rules = [entry(
            "study.exclusion", "excludes",
            first=sel("trait-source", "species.human#trait:skillful"),
            second=sel("trait-source", "species.orc#trait:adrenaline-rush"),
        )]
        out = self.evaluate(preview, rules)
        self.check_status(out, "excluded-combination")
        self.assertEqual("declared-dependency-conflicts",
                         out["wholeProfileReviewStatus"])

    def test_declared_choice_must_be_explicit_and_within_options(self):
        preview = self.preview(
            [source("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        cid = preview["proposedCandidateIds"][0]
        rules = [entry(
            "study.skill-choice", "requires-choice",
            subject=sel("trait-source", "species.human#trait:skillful"),
            choiceId="study.skill-choice",
            options=["insight", "athletics", "perception"],
            min=1, max=2,
        )]
        missing = self.evaluate(preview, rules)
        self.check_status(missing, "missing-choice")
        valid = self.evaluate(preview, rules, choices=[{
            "candidateId": cid, "choiceId": "study.skill-choice",
            "values": ["insight", "perception"],
        }])
        self.check_status(valid, "satisfied-evidence")
        self.assertTrue(valid["authoredChoiceObservations"])
        invalid = self.evaluate(preview, rules, choices=[{
            "candidateId": cid, "choiceId": "study.skill-choice",
            "values": ["invented"],
        }])
        self.check_status(invalid, "invalid-choice")
        self.assertFalse(valid["grantsIssued"])

    def test_multiple_unrelated_traits_can_exceed_declared_selection_cap(self):
        preview = self.preview(
            [source("human", "species.human"),
             source("orc", "species.orc")],
            lambda x: x["sourceTraitOrFieldId"] in (
                "species.human#trait:skillful",
                "species.orc#trait:adrenaline-rush",
            ),
        )
        rule = entry(
            "study.max-one", "selection-count",
            **{"from": [
                sel("trait-source", "species.human#trait:skillful"),
                sel("trait-source", "species.orc#trait:adrenaline-rush"),
            ]}, min=0, max=1,
        )
        out = self.evaluate(preview, [rule])
        self.check_status(out, "too-many-selected")

    def test_selection_cap_observes_suppression_proposed_by_decision(self):
        preview = self.preview(
            [source("human", "species.human")],
            lambda x: x.get("intrinsicSlotId") == "creature.size",
        )
        self.assertEqual(1, len(preview["pairwiseRelationships"]))
        rule = entry(
            "study.size-one", "selection-count",
            **{"from": [sel("intrinsic-slot", "creature.size")]},
            min=1, max=1,
        )
        both = self.evaluate(preview, [rule], dispositions=("preserve-both",))
        self.check_status(both, "too-many-selected")
        one = self.evaluate(preview, [rule], dispositions=("choose-first",))
        self.check_status(one, "satisfied-evidence")
        self.assertEqual(1, len(one["tentativelyRetainedCandidateIds"]))
        self.assertEqual(1, len(one["tentativelySuppressedCandidateIds"]))
        self.assertEqual("not-authorized", one["authorization"])

    def test_nested_mixed_source_is_not_reexpanded_into_unmanifested_traits(self):
        parent = draft_manifestation_profile(
            "parent", [source("orc", "species.orc")],
            [{"kind": "trait",
              "identity": "species.orc#trait:low-light-vision",
              "parameters": {}}],
            {}, self.blueprints, self.catalog, self.contracts,
        )
        preview = self.preview(
            [{"contributorId": "parent", "profileId": "parent"}],
            lambda x: True,
            registry={"parent": parent},
        )
        one_of = [sel("trait-source", "species.orc#trait:low-light-vision")]
        result = self.evaluate(preview, [entry(
            "study.count-nested", "selection-count",
            **{"from": one_of}, min=1, max=1,
        )])
        self.check_status(result, "satisfied-evidence")
        self.assertFalse(result["grantsIssued"])

    def test_linked_trait_parameters_are_not_implicitly_coupled(self):
        pool = {
            "status": "origin-characteristic-candidates-only",
            "offers": [
                {"kind": "trait", "identity": "study.a#trait:breath",
                 "sourceOccurrenceId": None,
                 "parameters": {"element": {"damageType": "cold"}},
                 "provenance": {
                     "kind": "custom-proposal", "contributorId": "one",
                     "proposedAuthorityRef": "study.only",
                 }},
                {"kind": "trait", "identity": "study.b#trait:ward",
                 "sourceOccurrenceId": None,
                 "parameters": {"element": {"damageType": "fire"}},
                 "provenance": {
                     "kind": "custom-proposal", "contributorId": "two",
                     "proposedAuthorityRef": "study.only",
                 }},
            ], "reviewFlags": [], "deferredSourceFields": [],
        }
        preview = self.from_pool(pool, lambda x: True)
        rule = entry(
            "study.linked", "linked-parameters",
            first=sel("trait-source", "study.a#trait:breath"),
            firstPath="element.damageType",
            second=sel("trait-source", "study.b#trait:ward"),
            secondPath="element.damageType",
        )
        out = self.evaluate(preview, [rule])
        self.check_status(out, "linked-parameter-conflict")
        self.assertEqual("declared-dependency-conflicts",
                         out["wholeProfileReviewStatus"])
        # This test asserts that an *explicit synthetic declaration* can
        # detect mismatch, not that any Dragonborn traits must share affinity.

    def test_missing_linked_parameter_is_not_assumed_equal(self):
        pool = {
            "status": "origin-characteristic-candidates-only",
            "offers": [
                {"kind": "trait", "identity": "study.a#trait:breath",
                 "sourceOccurrenceId": None,
                 "parameters": {"element": {"damageType": "cold"}},
                 "provenance": {"kind": "custom-proposal",
                                "contributorId": "a",
                                "proposedAuthorityRef": "study.only"}},
                {"kind": "trait", "identity": "study.b#trait:ward",
                 "sourceOccurrenceId": None, "parameters": {},
                 "provenance": {"kind": "custom-proposal",
                                "contributorId": "b",
                                "proposedAuthorityRef": "study.only"}},
            ], "reviewFlags": [], "deferredSourceFields": [],
        }
        preview = self.from_pool(pool, lambda x: True)
        result = self.evaluate(preview, [entry(
            "study.link", "linked-parameters",
            first=sel("trait-source", "study.a#trait:breath"),
            firstPath="element.damageType",
            second=sel("trait-source", "study.b#trait:ward"),
            secondPath="element.damageType",
        )])
        self.check_status(result, "unresolved-parameter")
        self.assertEqual("declared-dependencies-require-review",
                         result["wholeProfileReviewStatus"])

    def test_shared_resource_must_not_be_merged_automatically(self):
        preview = self.preview(
            [source("human", "species.human"),
             source("orc", "species.orc")],
            lambda x: x["sourceTraitOrFieldId"] in (
                "species.human#trait:skillful",
                "species.orc#trait:adrenaline-rush",
            ),
        )
        rule = entry(
            "study.shared-resource", "resource-coordination",
            resourceId="study.resource",
            **{"from": [
                sel("trait-source", "species.human#trait:skillful"),
                sel("trait-source", "species.orc#trait:adrenaline-rush"),
            ]},
        )
        out = self.evaluate(preview, [rule])
        self.check_status(out, "requires-resource-policy")
        self.assertFalse(out["grantsIssued"])

    def test_stale_decision_snapshot_cannot_be_applied(self):
        preview = self.preview(
            [source("human", "species.human")],
            lambda x: x.get("intrinsicSlotId") == "creature.size",
        )
        contract = self.decision_contract(preview, ("choose-first",))
        changed = copy.deepcopy(preview)
        changed["selectedCandidateEvidence"][0]["value"] = "huge"
        with self.assertRaisesRegex(
            ValueError, "stale or different candidate snapshot"
        ):
            self.evaluate(changed, [], contract=contract)

    def test_incomplete_decisions_block_closure_without_guessing(self):
        preview = self.preview(
            [source("human", "species.human")],
            lambda x: x.get("intrinsicSlotId") == "creature.size",
        )
        out = self.evaluate(preview, [], contract=self.decision_contract(
            preview, (),
        ) if not preview["pairwiseRelationships"] else {
            "status": "experimental-decision-proposal",
            "proposalFingerprint": proposal_fingerprint(preview),
            "decisions": [],
        })
        self.assertEqual("blocked-by-reconciliation",
                         out["wholeProfileReviewStatus"])
        self.assertEqual(2, len(out["unresolvedCandidateIds"]))

    def test_source_level_conflicts_remain_separate_from_selected_choices(self):
        preview = self.preview(
            [source("wood", "ancestry.elf.wood")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "ancestry.elf.wood#trait:druidcraft",
        )
        out = self.evaluate(preview, [])
        self.assertEqual(
            "contains-unresolved-source-evidence",
            out["candidatePoolEvidenceStatus"],
        )
        self.assertEqual(
            "no-declared-blockers-not-certified",
            out["wholeProfileReviewStatus"],
        )
        self.assertTrue(out["sourceReviewFlags"])
        self.assertFalse(out["grantsIssued"])

    def test_invalid_rule_shapes_and_duplicate_rule_ids_fail(self):
        preview = self.preview(
            [source("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        valid = entry(
            "study.one", "requires-any",
            subject=sel("trait-source", "species.human#trait:skillful"),
            oneOf=[sel("trait-source", "species.orc#trait:adrenaline-rush")],
        )
        for rules in (
            [valid, copy.deepcopy(valid)],
            [dict(valid, script="execute()")],
            [dict(valid, oneOf=[])],
            [entry("study.bad", "unsupported", target="x")],
            [entry("study.bad", "selection-count",
                   **{"from": [sel("intrinsic-slot", "creature.size")]},
                   min=True, max=1)],
        ):
            with self.subTest(rules=rules):
                with self.assertRaises(WholeProfileReviewError):
                    self.evaluate(preview, rules)

    def test_unexpected_choice_record_is_rejected(self):
        preview = self.preview(
            [source("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        with self.assertRaisesRegex(
            WholeProfileReviewError, "lacks a matching"
        ):
            self.evaluate(preview, [], choices=[{
                "candidateId": preview["proposedCandidateIds"][0],
                "choiceId": "invented", "values": ["x"],
            }])

    def test_outputs_and_inputs_stay_deterministic(self):
        preview = self.preview(
            [source("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        rules = [entry(
            "study.rule", "selection-count",
            **{"from": [sel("trait-source", "species.human#trait:skillful")]},
            min=1, max=1,
        )]
        contract = self.decision_contract(preview)
        before = copy.deepcopy((preview, contract, rules))
        one = self.evaluate(preview, rules)
        two = self.evaluate(preview, rules)
        self.assertEqual(one, two)
        one["dependencyRuleOutcomes"].clear()
        self.assertEqual(before, (preview, contract, rules))


if __name__ == "__main__":
    unittest.main()
