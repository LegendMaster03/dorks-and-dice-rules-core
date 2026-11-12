"""Archived mixed contributor choices remain evidence, never inherited grants."""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_applicable_choices import (  # noqa: E402
    project_applicable_choice_evidence,
)
from character_origins_characteristic_identity import (  # noqa: E402
    project_characteristic_identity_evidence, read_identity_mapping,
)
from character_origins_choice_schema import audit_native_choice_schema  # noqa: E402
from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_manifestation_inventory import (  # noqa: E402
    characteristic_candidate_pool, draft_manifestation_profile,
)
from character_origins_nested_choice_history import (  # noqa: E402
    NestedChoiceHistoryError, draft_profile_fingerprint,
    review_nested_choice_history,
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
    review_whole_origin_profile,
)


def study_catalogs():
    return {
        "status": "synthetic-catalog-evidence-only",
        "catalogs": [{
            "reference": "any-native-skill",
            "optionIds": ["perception", "athletics"],
            "evidenceRef": "study.skill-options",
        }],
    }


class NestedChoiceHistoryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        cls.blueprints = read_blueprints()
        cls.choice_audit = audit_native_choice_schema(cls.catalog)
        cls.mapping = read_identity_mapping()

    def fixture(self, trait="skillful"):
        parent = draft_manifestation_profile(
            "mixed-parent", [{
                "contributorId": "human",
                "selectedSourceId": "species.human",
            }],
            [{"kind": "trait",
              "identity": f"species.human#trait:{trait}",
              "parameters": {}}],
            {}, self.blueprints, self.catalog, self.contracts,
        )
        profiles = {"mixed-parent": parent}
        pool = characteristic_candidate_pool(
            [{"contributorId": "parent-ref", "profileId": "mixed-parent"}],
            profiles, self.blueprints, self.catalog, self.contracts,
        )
        identity = project_characteristic_identity_evidence(pool, self.mapping)
        preview = review_proposed_manifestation(
            identity, [x["candidateId"] for x in identity["candidates"]
                       if x["kind"] == "trait"],
        )
        contract = {
            "status": "experimental-decision-proposal",
            "proposalFingerprint": proposal_fingerprint(preview),
            "decisions": [],
        }
        whole = review_whole_origin_profile(
            preview, contract,
            {"status": "synthetic-dependency-study", "rules": []},
        )
        applicable = project_applicable_choice_evidence(
            self.choice_audit, preview, whole, self.catalog,
        )
        return profiles, applicable

    @staticmethod
    def sidecar(parent, choice_id="species.human#trait:skillful#choice:chooseFrom",
                selected=None):
        return {
            "status": "proposed-archived-choice-history",
            "profileId": "mixed-parent",
            "profileFingerprint": draft_profile_fingerprint(parent),
            "records": [{
                "characteristicIdentity": "species.human#trait:skillful",
                "choiceEvidenceId": choice_id,
                "selectedOptionIds": selected or ["perception"],
                "proposedAuthorityRef": "study.history-proposal",
            }],
        }

    def review(self, profiles, applicable, sidecars):
        return review_nested_choice_history(
            applicable, profiles, sidecars,
            self.choice_audit, study_catalogs(),
        )

    def test_missing_history_does_not_forget_or_reselect_parent_choice(self):
        profiles, applicable = self.fixture()
        result = self.review(profiles, applicable, [])
        self.assertEqual([], result["structurallyMatchedHistory"])
        self.assertEqual(1, len(result["missingChoiceHistory"]))
        self.assertEqual("historical-choice-record-unavailable",
                         result["missingChoiceHistory"][0]["status"])
        self.assertFalse(result["descendantChoiceInstancesCreated"])
        self.assertEqual("not-evaluated", result["transmissionEligibility"])

    def test_version_bound_history_preserves_values_without_new_grant(self):
        profiles, applicable = self.fixture()
        result = self.review(
            profiles, applicable, [self.sidecar(profiles["mixed-parent"])]
        )
        self.assertEqual([], result["missingChoiceHistory"])
        self.assertEqual(1, len(result["structurallyMatchedHistory"]))
        evidence = result["structurallyMatchedHistory"][0]
        self.assertEqual(["perception"], evidence["selectedOptionIds"])
        self.assertEqual("species.human#trait:skillful",
                         evidence["characteristicIdentity"])
        self.assertEqual("study.skill-options",
                         evidence["syntheticCatalogEvidenceRef"])
        self.assertEqual("source-shape-matches-proposal-not-authorized",
                         evidence["status"])
        self.assertFalse(result["historicalSelectionAuthorized"])
        self.assertFalse(result["descendantChoiceInstancesCreated"])

    def test_parent_edit_invalidates_stale_history(self):
        profiles, applicable = self.fixture()
        sidecar = self.sidecar(profiles["mixed-parent"])
        changed = copy.deepcopy(profiles)
        changed["mixed-parent"]["origination"] = {"other": "new information"}
        with self.assertRaisesRegex(
            NestedChoiceHistoryError, "stale or lacks a profile"
        ):
            self.review(changed, applicable, [sidecar])

    def test_unlisted_catalog_value_does_not_pass_history_check(self):
        profiles, applicable = self.fixture()
        sidecar = self.sidecar(
            profiles["mixed-parent"], selected=["unknown-skill"]
        )
        with self.assertRaisesRegex(
            NestedChoiceHistoryError, "do not match authored evidence"
        ):
            self.review(profiles, applicable, [sidecar])

    def test_wrong_or_invented_trait_choice_is_rejected(self):
        profiles, applicable = self.fixture()
        record = self.sidecar(
            profiles["mixed-parent"],
            choice_id="species.human#trait:versatile#choice:featCategory",
        )
        with self.assertRaisesRegex(
            NestedChoiceHistoryError, "not supported by native authoring"
        ):
            self.review(profiles, applicable, [record])

    def test_non_manifested_trait_cannot_be_added_from_ancestor(self):
        profiles, applicable = self.fixture()
        sidecar = self.sidecar(profiles["mixed-parent"])
        sidecar["records"][0]["characteristicIdentity"] = (
            "species.human#trait:versatile"
        )
        with self.assertRaisesRegex(
            NestedChoiceHistoryError, "not relevant to this manifested trait"
        ):
            self.review(profiles, applicable, [sidecar])

    def test_no_sidecar_for_non_ancestral_source_is_accepted(self):
        profiles, applicable = self.fixture()
        extra = {
            "status": "proposed-archived-choice-history",
            "profileId": "unrelated",
            "profileFingerprint": "abc",
            "records": [],
        }
        with self.assertRaisesRegex(
            NestedChoiceHistoryError, "unrelated contributor history"
        ):
            self.review(profiles, applicable, [extra])

    def test_duplicate_history_entries_are_rejected(self):
        profiles, applicable = self.fixture()
        sidecar = self.sidecar(profiles["mixed-parent"])
        sidecar["records"].append(copy.deepcopy(sidecar["records"][0]))
        with self.assertRaisesRegex(
            NestedChoiceHistoryError, "not relevant to this manifested trait"
        ):
            self.review(profiles, applicable, [sidecar])

    def test_player_allocation_cannot_be_mistaken_for_choice_option_history(self):
        profiles, applicable = self.fixture("adaptable-learning")
        record = {
            "status": "proposed-archived-choice-history",
            "profileId": "mixed-parent",
            "profileFingerprint": draft_profile_fingerprint(
                profiles["mixed-parent"]
            ),
            "records": [{
                "characteristicIdentity": (
                    "species.human#trait:adaptable-learning"
                ),
                "choiceEvidenceId": (
                    "species.human#trait:adaptable-learning#choice:allocation"
                ),
                "selectedOptionIds": ["perception"],
                "proposedAuthorityRef": "study.invalid",
            }],
        }
        with self.assertRaisesRegex(
            NestedChoiceHistoryError, "cannot be archived"
        ):
            self.review(profiles, applicable, [record])

    def test_profile_status_never_becomes_approved_by_sidecar(self):
        profiles, applicable = self.fixture()
        altered = copy.deepcopy(profiles)
        altered["mixed-parent"]["authority"] = "approved"
        with self.assertRaisesRegex(
            NestedChoiceHistoryError, "non-authoritative draft"
        ):
            self.review(altered, applicable, [])

    def test_review_is_deterministic_and_does_not_modify_inputs(self):
        profiles, applicable = self.fixture()
        sidecar = self.sidecar(profiles["mixed-parent"])
        records = [sidecar]
        before = copy.deepcopy((profiles, applicable, records))
        first = self.review(profiles, applicable, records)
        second = self.review(profiles, applicable, records)
        self.assertEqual(first, second)
        first["structurallyMatchedHistory"][0]["selectedOptionIds"].clear()
        self.assertEqual(before, (profiles, applicable, records))


if __name__ == "__main__":
    unittest.main()
