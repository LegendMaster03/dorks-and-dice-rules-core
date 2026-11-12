"""Apply native choice evidence only to proposed active Origin characteristics."""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_applicable_choices import (  # noqa: E402
    ApplicableChoiceError, project_applicable_choice_evidence,
)
from character_origins_characteristic_identity import (  # noqa: E402
    project_characteristic_identity_evidence, read_identity_mapping,
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


def direct(name, sid):
    return {"contributorId": name, "selectedSourceId": sid}


class ApplicableChoiceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.choice_report = audit_native_choice_schema(cls.catalog)
        cls.blueprints = read_blueprints()
        cls.contracts = read_contracts()
        cls.mapping = read_identity_mapping()

    def pipeline(self, contributors, predicate, registry=None, pool=None):
        pool = (pool if pool is not None
                else characteristic_candidate_pool(
                    contributors, {} if registry is None else registry,
                    self.blueprints, self.catalog, self.contracts,
                ))
        ids = project_characteristic_identity_evidence(pool, self.mapping)
        selected = [
            row["candidateId"] for row in ids["candidates"]
            if predicate(row)
        ]
        preview = review_proposed_manifestation(ids, selected)
        contract = {
            "status": "experimental-decision-proposal",
            "proposalFingerprint": proposal_fingerprint(preview),
            "decisions": [],
        }
        whole = review_whole_origin_profile(
            preview, contract,
            {"status": "synthetic-dependency-study", "rules": []},
        )
        return preview, whole

    def apply(self, preview, whole, catalog=None, schema=None):
        return project_applicable_choice_evidence(
            self.choice_report if schema is None else schema,
            preview, whole, self.catalog if catalog is None else catalog,
        )

    def test_human_selected_skillful_requires_native_choice_evidence(self):
        preview, whole = self.pipeline(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        report = self.apply(preview, whole)
        self.assertEqual(1, len(report["applicableNativeChoiceEvidence"]))
        item = report["applicableNativeChoiceEvidence"][0]
        self.assertEqual(
            "active-trait-choice-needs-authority", item["applicability"]
        )
        self.assertEqual(
            "catalog-entitlement", item["choiceEvidence"]["kind"]
        )
        self.assertFalse(item["grantAuthorized"])
        self.assertFalse(report["grantsIssued"])

    def test_unselected_human_traits_do_not_require_their_choices(self):
        preview, whole = self.pipeline(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:adaptable-learning",
        )
        report = self.apply(preview, whole)
        self.assertEqual(
            ["allocation"], [
                x["choiceEvidence"]["authoredField"]
                for x in report["applicableNativeChoiceEvidence"]
            ],
        )
        self.assertNotIn(
            "chooseFrom", {
                x["choiceEvidence"]["authoredField"]
                for x in report["applicableNativeChoiceEvidence"]
            },
        )
        self.assertEqual("not-certified", report["authoritativeChoiceClosure"])

    def test_metallic_breath_option_is_use_time_not_birth_choice(self):
        preview, whole = self.pipeline(
            [direct("silver", "ancestry.dragonborn.silver")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "ancestry.dragonborn.metallic#trait:metallic-breath-weapon",
        )
        report = self.apply(preview, whole)
        self.assertEqual(1, len(report["applicableNativeChoiceEvidence"]))
        item = report["applicableNativeChoiceEvidence"][0]
        self.assertEqual(
            "use-time-option-not-creation", item["applicability"]
        )
        self.assertEqual("on-use", item["choiceEvidence"]["timing"])
        self.assertFalse(item["grantAuthorized"])

    def test_intrinsic_size_choice_refers_to_selected_value_not_two_grants(self):
        preview, whole = self.pipeline(
            [direct("human", "species.human")],
            lambda x: x.get("intrinsicSlotId") == "creature.size"
            and x.get("value") == "small",
        )
        report = self.apply(preview, whole)
        self.assertEqual(1, len(report["applicableNativeChoiceEvidence"]))
        item = report["applicableNativeChoiceEvidence"][0]
        self.assertEqual(
            "intrinsic-value-already-proposed", item["applicability"]
        )
        self.assertEqual(
            "creature.size", item["choiceEvidence"]["constraints"]["slotId"]
        )
        self.assertEqual("not-certified", report["authoritativeChoiceClosure"])

    def test_elf_spell_and_source_level_ability_choice_are_not_conflated(self):
        preview, whole = self.pipeline(
            [direct("elf", "ancestry.elf.high")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "ancestry.elf.high#trait:arcane-cantrip",
        )
        report = self.apply(preview, whole)
        self.assertEqual(
            ["spellList"], [x["choiceEvidence"]["authoredField"]
                           for x in report["applicableNativeChoiceEvidence"]]
        )
        source_controls = report["sourceConfigurationControlsNotApplied"]
        self.assertIn(
            "spellcastingAbilityChoice",
            {x["authoredField"] for x in source_controls},
        )
        self.assertEqual([], report["nestedChoiceHistoryGaps"])

    def test_tiefling_source_ancestry_control_is_not_a_heritage_grant(self):
        preview, whole = self.pipeline(
            [direct("infernal", "ancestry.tiefling.infernal")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.tiefling#trait:low-light-vision",
        )
        report = self.apply(preview, whole)
        self.assertIn(
            "ancestrySelection", {
                x["authoredField"] for x in
                report["sourceConfigurationControlsNotApplied"]
            }
        )
        # The otherworldly-presence dependent ability reference remains
        # unresolved in the existing parameter-binding candidate pipeline.
        # Its authoring choice stays visible in the schema, but must NOT
        # be treated as a selected grant merely because the source exists.
        self.assertIn(
            "spellcastingAbilityFrom", {
                x["authoredField"] for x in
                self.choice_report["choiceObservations"]
                if x["sourceId"] == "species.tiefling"
            }
        )
        self.assertNotIn(
            "spellcastingAbilityFrom", {
                x["choiceEvidence"]["authoredField"] for x in
                report["applicableNativeChoiceEvidence"]
            }
        )

    def test_verified_nested_parent_does_not_reselect_earlier_trait_choices(self):
        parent = draft_manifestation_profile(
            "parent", [direct("human", "species.human")],
            [{"kind": "trait",
              "identity": "species.human#trait:skillful",
              "parameters": {}}],
            {}, self.blueprints, self.catalog, self.contracts,
        )
        preview, whole = self.pipeline(
            [{"contributorId": "mixed-parent", "profileId": "parent"}],
            lambda x: True, registry={"parent": parent},
        )
        report = self.apply(preview, whole)
        self.assertEqual([], report["applicableNativeChoiceEvidence"])
        self.assertEqual(1, len(report["nestedChoiceHistoryGaps"]))
        self.assertEqual(
            "prior-choice-history-not-verified",
            report["nestedChoiceHistoryGaps"][0]["status"],
        )

    def test_custom_trait_does_not_forge_standard_parental_entitlement(self):
        pool = {
            "status": "origin-characteristic-candidates-only",
            "offers": [{
                "kind": "trait",
                "identity": "species.human#trait:skillful",
                "sourceOccurrenceId": None, "parameters": {},
                "provenance": {
                    "kind": "custom-proposal",
                    "contributorId": "custom",
                    "proposedAuthorityRef": "study.custom-catalog",
                },
            }],
            "reviewFlags": [], "deferredSourceFields": [],
        }
        preview, whole = self.pipeline([], lambda x: True, pool=pool)
        report = self.apply(preview, whole)
        self.assertEqual([], report["applicableNativeChoiceEvidence"])
        self.assertEqual(1, len(report["customChoiceAuthoringGaps"]))
        self.assertEqual(
            "study.custom-catalog",
            report["customChoiceAuthoringGaps"][0]["proposedAuthorityRef"]
        )

    def test_claiming_a_native_trait_with_modified_definition_fails_closed(self):
        preview, whole = self.pipeline(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        tampered = copy.deepcopy(preview)
        tampered["selectedCandidateEvidence"][0][
            "originalOffer"
        ]["authoredDefinition"]["choiceCount"] = 99
        with self.assertRaisesRegex(
            ApplicableChoiceError, "matching|authoring"
        ):
            self.apply(tampered, whole)

    def test_full_corpus_choice_audit_does_not_leak_other_source_controls(self):
        preview, whole = self.pipeline(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        report = self.apply(preview, whole)
        self.assertEqual(
            [],
            report["sourceConfigurationControlsNotApplied"],
        )
        self.assertNotIn(
            "ancestrySelection",
            {x["choiceEvidence"]["authoredField"]
             for x in report["applicableNativeChoiceEvidence"]},
        )

    def test_foreign_unselected_candidates_are_not_projected(self):
        preview, whole = self.pipeline(
            [direct("human", "species.human"),
             direct("elf", "ancestry.elf.high")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        report = self.apply(preview, whole)
        self.assertEqual(1, len(report["applicableNativeChoiceEvidence"]))
        self.assertEqual(
            "species.human#trait:skillful",
            report["applicableNativeChoiceEvidence"][0][
                "choiceEvidence"]["authoredOccurrenceId"]
        )

    def test_native_offer_from_unrelated_selected_species_is_rejected(self):
        pool = characteristic_candidate_pool(
            [direct("human", "species.human")], {},
            self.blueprints, self.catalog, self.contracts,
        )
        item = next(x for x in pool["offers"]
                    if x["identity"] == "species.human#trait:skillful")
        item["provenance"]["selectedSourceId"] = "ancestry.elf.high"
        preview, whole = self.pipeline(
            [], lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
            pool=pool,
        )
        with self.assertRaisesRegex(
            ApplicableChoiceError, "outside selected lineage"
        ):
            self.apply(preview, whole)

    def test_native_intrinsic_value_is_verified_against_authored_source(self):
        pool = characteristic_candidate_pool(
            [direct("human", "species.human")], {},
            self.blueprints, self.catalog, self.contracts,
        )
        item = next(x for x in pool["offers"]
                    if x["kind"] == "intrinsic"
                    and x["identity"] == "creature.size"
                    and x["value"] == "small")
        item["value"] = "huge"
        preview, whole = self.pipeline(
            [], lambda x: x.get("intrinsicSlotId") ==
            "creature.size" and x.get("value") == "huge",
            pool=pool,
        )
        with self.assertRaisesRegex(
            ApplicableChoiceError, "not from authored source"
        ):
            self.apply(preview, whole)

    def test_bad_projection_and_choice_audit_are_rejected(self):
        preview, whole = self.pipeline(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        bad = copy.deepcopy(whole)
        bad["authorization"] = "approved"
        with self.assertRaises(ApplicableChoiceError):
            self.apply(preview, bad)
        bad_choice = copy.deepcopy(self.choice_report)
        bad_choice["authorization"] = "approved"
        with self.assertRaises(ApplicableChoiceError):
            self.apply(preview, whole, schema=bad_choice)

    def test_original_inputs_remain_unchanged(self):
        preview, whole = self.pipeline(
            [direct("human", "species.human")],
            lambda x: x["sourceTraitOrFieldId"] ==
            "species.human#trait:skillful",
        )
        old = (copy.deepcopy(preview), copy.deepcopy(whole),
               copy.deepcopy(self.choice_report))
        first = self.apply(preview, whole)
        self.assertEqual(first, self.apply(preview, whole))
        first["applicableNativeChoiceEvidence"].clear()
        self.assertEqual(
            old, (preview, whole, self.choice_report),
        )


if __name__ == "__main__":
    unittest.main()
