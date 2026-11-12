"""Candidate-bound, explicit Origin choice proposals, not authorized grants."""
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
from character_origins_choice_instances import (  # noqa: E402
    ChoiceInstanceError, choice_instance_snapshot_fingerprint,
    review_choice_instances, validate_study_catalogs,
)
from character_origins_choice_schema import audit_native_choice_schema  # noqa: E402
from character_origins_compound_traits import read_contracts  # noqa: E402
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
    review_whole_origin_profile,
)


def direct(cid, sid):
    return {"contributorId": cid, "selectedSourceId": sid}


def catalogs(*extra):
    return {
        "status": "synthetic-catalog-evidence-only",
        "catalogs": [
            {
                "reference": "any-native-skill",
                "optionIds": ["athletics", "perception", "insight"],
                "evidenceRef": "study.skill-fixture",
            },
            {
                "reference": "feat:origin",
                "optionIds": ["alert", "lucky"],
                "evidenceRef": "study.feat-fixture",
            },
            {
                "reference": "spell-list:wizard",
                "optionIds": ["light", "mage-hand", "prestidigitation"],
                "evidenceRef": "study.spell-fixture",
            },
            *extra,
        ],
    }


def choice(item, options, authority="study.table-proposal"):
    return {
        "candidateId": item["candidateId"],
        "choiceEvidenceId": item["choiceEvidence"]["choiceEvidenceId"],
        "selectedOptionIds": list(options),
        "proposedAuthorityRef": authority,
    }


class ProposedChoiceInstancesTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.choices = audit_native_choice_schema(cls.catalog)
        cls.blueprints = read_blueprints()
        cls.contracts = read_contracts()
        cls.mapping = read_identity_mapping()

    def projection(self, contributors, predicate, registry=None, pool=None):
        if pool is None:
            pool = characteristic_candidate_pool(
                contributors, registry or {}, self.blueprints,
                self.catalog, self.contracts,
            )
        identity = project_characteristic_identity_evidence(
            pool, self.mapping
        )
        ids = [item["candidateId"] for item in identity["candidates"]
               if predicate(item)]
        preview = review_proposed_manifestation(identity, ids)
        contract = {
            "status": "experimental-decision-proposal",
            "proposalFingerprint": proposal_fingerprint(preview),
            "decisions": [],
        }
        whole = review_whole_origin_profile(
            preview, contract,
            {"status": "synthetic-dependency-study", "rules": []},
        )
        return preview, project_applicable_choice_evidence(
            self.choices, preview, whole, self.catalog,
        )

    def review(self, applicable, preview, *instances, evidence=None):
        return review_choice_instances(
            applicable, preview, list(instances),
            catalogs() if evidence is None else evidence,
        )

    @staticmethod
    def one_item(applicable, field):
        found = [x for x in applicable["applicableNativeChoiceEvidence"]
                 if x["choiceEvidence"]["authoredField"] == field]
        if len(found) != 1:
            raise AssertionError(
                f"Expected one active {field} choice, found {found}"
            )
        return found[0]

    def test_skillful_selected_choice_is_bound_to_exact_trait_and_provenance(self):
        preview, applicable = self.projection(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        proposed = choice(self.one_item(applicable, "chooseFrom"), ["perception"])
        result = self.review(applicable, preview, proposed)
        self.assertEqual(
            "proposed-choice-evidence-complete-not-authorized",
            result["choiceReviewStatus"],
        )
        self.assertEqual([], result["unresolvedChoiceRequirements"])
        instance = result["proposedChoiceInstances"][0]
        self.assertEqual("species.human#trait:skillful",
                         instance["choiceSourceOccurrenceId"])
        self.assertEqual("human",
                         instance["contributorProvenance"]["contributorId"])
        self.assertEqual("study.skill-fixture",
                         instance["syntheticCatalogEvidenceRef"])
        self.assertEqual("character-creation", instance["timing"])
        self.assertIsNone(instance["grantInstanceId"])
        self.assertEqual("not-certified", result["catalogEligibility"])
        self.assertFalse(result["grantsIssued"])

    def test_unselected_choice_reports_missing_without_defaulting(self):
        preview, applicable = self.projection(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        result = self.review(applicable, preview)
        self.assertEqual(
            "incomplete-or-unverified-choice-evidence",
            result["choiceReviewStatus"],
        )
        self.assertEqual("needs-persistent-choice",
                         result["unresolvedChoiceRequirements"][0]["reason"])

    def test_open_skill_catalog_rejects_unlisted_and_fake_options(self):
        preview, applicable = self.projection(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        item = self.one_item(applicable, "chooseFrom")
        for options in (["unlisted"], ["perception", "insight"], []):
            with self.subTest(options=options):
                with self.assertRaises(ChoiceInstanceError):
                    self.review(applicable, preview, choice(item, options))
        with self.assertRaisesRegex(
            ChoiceInstanceError, "Catalog-backed choice lacks"
        ):
            self.review(
                applicable, preview, choice(item, ["perception"]),
                evidence={"status": "synthetic-catalog-evidence-only",
                          "catalogs": []},
            )

    def test_origin_feat_entitlement_is_separate_catalog_reference(self):
        preview, applicable = self.projection(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:versatile",
        )
        result = self.review(
            applicable, preview,
            choice(self.one_item(applicable, "featCategory"), ["alert"]),
        )
        self.assertEqual("study.feat-fixture",
                         result["proposedChoiceInstances"][0][
                             "syntheticCatalogEvidenceRef"])
        self.assertEqual("not-certified", result["catalogEligibility"])

    def test_high_elf_spell_choice_cannot_claim_resolved_timing(self):
        preview, applicable = self.projection(
            [direct("high-elf", "ancestry.elf.high")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "ancestry.elf.high#trait:arcane-cantrip",
        )
        result = self.review(
            applicable, preview,
            choice(self.one_item(applicable, "spellList"), ["mage-hand"]),
        )
        self.assertEqual(
            "timing-unresolved",
            result["proposedChoiceInstances"][0]["status"],
        )
        self.assertEqual(
            "incomplete-or-unverified-choice-evidence",
            result["choiceReviewStatus"],
        )
        self.assertEqual("not-authorized", result["authority"])

    def test_goliath_ability_choice_uses_exact_authored_finite_options(self):
        preview, applicable = self.projection(
            [direct("goliath", "ancestry.goliath.cloud")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.goliath#trait:giant-ancestry-aptitude",
        )
        item = self.one_item(applicable, "abilityChoice")
        result = self.review(applicable, preview, choice(item, ["strength"]))
        self.assertEqual(
            "goliath-ancestry-selection",
            result["proposedChoiceInstances"][0]["timing"],
        )
        with self.assertRaisesRegex(
            ChoiceInstanceError, "outside authored/catalog evidence"
        ):
            self.review(applicable, preview, choice(item, ["wisdom"]))

    def test_human_size_choice_must_match_retained_intrinsic_value(self):
        preview, applicable = self.projection(
            [direct("human", "species.human")],
            lambda x: x.get("intrinsicSlotId") == "creature.size"
            and x.get("value") == "small",
        )
        item = self.one_item(applicable, "sizeChoice")
        result = self.review(applicable, preview, choice(item, ["small"]))
        self.assertEqual("proposed-choice-evidence-complete-not-authorized",
                         result["choiceReviewStatus"])
        with self.assertRaisesRegex(
            ChoiceInstanceError, "differs from proposed characteristic"
        ):
            self.review(applicable, preview, choice(item, ["medium"]))

    def test_metallic_breath_on_use_option_is_not_persisted_at_creation(self):
        preview, applicable = self.projection(
            [direct("bronze", "ancestry.dragonborn.bronze")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "ancestry.dragonborn.metallic#trait:metallic-breath-weapon",
        )
        item = self.one_item(applicable, "options")
        result = self.review(applicable, preview)
        self.assertEqual([], result["proposedChoiceInstances"])
        self.assertEqual([], result["unresolvedChoiceRequirements"])
        self.assertEqual("retained-as-use-time-option",
                         result["useTimeOptionsNotPersisted"][0]["status"])
        with self.assertRaisesRegex(
            ChoiceInstanceError, "Use-time options cannot"
        ):
            self.review(applicable, preview, choice(item, ["repulsion-breath"]))

    def test_player_allocations_require_separate_rank_and_limit_policy(self):
        preview, applicable = self.projection(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:adaptable-learning",
        )
        item = self.one_item(applicable, "allocation")
        out = self.review(applicable, preview)
        self.assertEqual(
            "needs-allocation-or-schema-policy",
            out["unresolvedChoiceRequirements"][0]["reason"],
        )
        with self.assertRaisesRegex(
            ChoiceInstanceError, "cannot be independently selected"
        ):
            self.review(applicable, preview, choice(item, ["athletics"]))

    def test_unknown_or_unselected_entitlement_cannot_be_fabricated(self):
        preview, applicable = self.projection(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:versatile",
        )
        item = self.one_item(applicable, "featCategory")
        forged = choice(item, ["alert"])
        forged["choiceEvidenceId"] = (
            "species.human#trait:skillful#choice:chooseFrom"
        )
        with self.assertRaisesRegex(
            ChoiceInstanceError, "not applicable to a retained native offer"
        ):
            self.review(applicable, preview, forged)
        forged = choice(item, ["alert"])
        forged["candidateId"] = "evidence-999"
        with self.assertRaises(ChoiceInstanceError):
            self.review(applicable, preview, forged)

    def test_source_wide_tiefling_ancestry_control_is_not_choice_instance(self):
        preview, applicable = self.projection(
            [direct("infernal", "ancestry.tiefling.infernal")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.tiefling#trait:low-light-vision",
        )
        self.assertIn(
            "ancestrySelection",
            {x["authoredField"] for x in
             applicable["sourceConfigurationControlsNotApplied"]},
        )
        result = self.review(applicable, preview)
        self.assertEqual([], result["proposedChoiceInstances"])
        self.assertEqual("not-certified", result["traitSelectionEligibility"])

    def test_nested_parent_history_cannot_be_invented_by_child(self):
        parent = draft_manifestation_profile(
            "parent", [direct("human", "species.human")],
            [{"kind": "trait", "identity": "species.human#trait:skillful",
              "parameters": {}}],
            {}, self.blueprints, self.catalog, self.contracts,
        )
        preview, applicable = self.projection(
            [{"contributorId": "mixed", "profileId": "parent"}],
            lambda x: True, registry={"parent": parent},
        )
        report = self.review(applicable, preview)
        self.assertEqual(
            "incomplete-or-unverified-choice-evidence",
            report["choiceReviewStatus"],
        )
        self.assertEqual(
            "prior-choice-history-not-verified",
            report["nestedChoiceHistoryGaps"][0]["status"],
        )
        with self.assertRaises(ChoiceInstanceError):
            self.review(applicable, preview, {
                "candidateId": preview["proposedCandidateIds"][0],
                "choiceEvidenceId": "species.human#trait:skillful#choice:chooseFrom",
                "selectedOptionIds": ["perception"],
                "proposedAuthorityRef": "study.synthetic",
            })

    def test_custom_trait_does_not_borrow_native_catalog_entitlement(self):
        pool = {
            "status": "origin-characteristic-candidates-only",
            "offers": [{
                "kind": "trait",
                "identity": "species.human#trait:skillful",
                "sourceOccurrenceId": None, "parameters": {},
                "provenance": {"kind": "custom-proposal",
                               "contributorId": "custom",
                               "proposedAuthorityRef": "study.custom"},
            }],
            "reviewFlags": [], "deferredSourceFields": [],
        }
        preview, applicable = self.projection([], lambda x: True, pool=pool)
        report = self.review(applicable, preview)
        self.assertEqual([], report["proposedChoiceInstances"])
        self.assertEqual(
            "custom-choice-schema-not-yet-authorized",
            report["customChoiceAuthoringGaps"][0]["status"],
        )

    def test_duplicate_instances_or_invalid_authority_are_rejected(self):
        preview, applicable = self.projection(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        item = self.one_item(applicable, "chooseFrom")
        proposed = choice(item, ["perception"])
        with self.assertRaisesRegex(
            ChoiceInstanceError, "Duplicate proposed choice instance"
        ):
            self.review(applicable, preview, proposed, proposed)
        with self.assertRaises(ChoiceInstanceError):
            self.review(applicable, preview, choice(item, ["perception"], ""))

    def test_synthetic_catalog_evidence_requires_valid_unique_references(self):
        for value in (
            {"status": "approved", "catalogs": []},
            {"status": "synthetic-catalog-evidence-only", "catalogs": [
                {"reference": "x", "optionIds": ["a", "a"], "evidenceRef": "r"}]},
            {"status": "synthetic-catalog-evidence-only", "catalogs": [
                {"reference": "x", "optionIds": ["a"], "evidenceRef": "r"},
                {"reference": "x", "optionIds": ["b"], "evidenceRef": "r"}]},
        ):
            with self.subTest(evidence=value):
                with self.assertRaises(ChoiceInstanceError):
                    validate_study_catalogs(value)

    def test_proposal_snapshot_rejects_stale_or_mismatched_candidate_evidence(self):
        preview, applicable = self.projection(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        item = self.one_item(applicable, "chooseFrom")
        instance = choice(item, ["perception"])
        digest = choice_instance_snapshot_fingerprint(preview, [instance])
        self.assertEqual(
            digest, choice_instance_snapshot_fingerprint(preview, [instance]),
        )
        changed = copy.deepcopy(preview)
        changed["selectedCandidateEvidence"][0]["parameters"] = {
            "fake": "value"
        }
        with self.assertRaisesRegex(
            ChoiceInstanceError, "matching non-authoritative"
        ):
            self.review(applicable, changed, instance)
        self.assertNotEqual(
            digest, choice_instance_snapshot_fingerprint(
                preview, [choice(item, ["insight"])]
            ),
        )

    def test_purity_and_complete_choice_provenance_preserved(self):
        preview, applicable = self.projection(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        old = (copy.deepcopy(preview), copy.deepcopy(applicable))
        submitted = choice(self.one_item(applicable, "chooseFrom"),
                           ["perception"])
        one = self.review(applicable, preview, submitted)
        two = self.review(applicable, preview, submitted)
        self.assertEqual(one, two)
        one["proposedChoiceInstances"][0]["contributorProvenance"].clear()
        self.assertEqual(old, (preview, applicable))
        self.assertEqual(
            "not-authorized", two["authority"]
        )


if __name__ == "__main__":
    unittest.main()
