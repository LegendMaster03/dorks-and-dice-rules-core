"""Source-neutral identity boundaries for Origin characteristic evidence.

A named trait, its mechanical definition, the source occurrence, each
contributor's offer and an eventual earned grant must not be conflated.
Identity mappings here are synthetic evidence, NOT approved cross-source
equivalence or acquisition/stacking behavior.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_characteristic_identity import (  # noqa: E402
    IdentityEvidenceError, project_characteristic_identity_evidence,
    read_identity_mapping, validate_identity_mapping,
)
from character_origins_curated_identity_review import read_curated_evidence  # noqa: E402
from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_manifestation_inventory import (  # noqa: E402
    characteristic_candidate_pool, draft_manifestation_profile,
)
from character_origins_parameter_inheritance import read_blueprints  # noqa: E402
from character_origins_source_inventory import read_catalog  # noqa: E402


def direct(cid, source_id):
    return {"contributorId": cid, "selectedSourceId": source_id}


class CharacteristicIdentityEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        cls.blueprints = read_blueprints()
        cls.mapping = read_identity_mapping()

    def pool(self, contributors, profiles=None):
        return characteristic_candidate_pool(
            contributors, {} if profiles is None else profiles,
            self.blueprints, self.catalog, self.contracts
        )

    def evidence(self, contributors, profiles=None):
        return project_characteristic_identity_evidence(
            self.pool(contributors, profiles), self.mapping
        )

    @staticmethod
    def find(report, identity, kind="trait"):
        return [
            item for item in report["candidates"]
            if item["kind"] == kind
            and item["sourceTraitOrFieldId"] == identity
        ]

    def test_intrinsic_slot_is_not_value_or_trait_definition(self):
        report = self.evidence([
            direct("human", "species.human"),
            direct("elf", "ancestry.elf.high"),
        ])
        types = [
            item for item in report["candidates"]
            if item["intrinsicSlotId"] == "creature.type"
        ]
        self.assertEqual(
            {"humanoid", "fey"}, {x["value"] for x in types}
        )
        self.assertEqual(
            {"known-intrinsic-dimension"},
            {x["identityStatus"] for x in types}
        )
        self.assertTrue(all(
            x["traitConceptId"] is None
            and x["mechanicDefinitionId"] is None
            and x["grantInstanceId"] is None
            for x in types
        ))
        self.assertEqual("not-authorized", report["authority"])

    def test_same_contributor_source_definition_is_not_two_grants(self):
        report = self.evidence([
            direct("one", "ancestry.dragonborn.red"),
            direct("two", "ancestry.dragonborn.red"),
        ])
        trait = self.find(report, "species.dragonborn#trait:breath-weapon")
        self.assertEqual(2, len(trait))
        self.assertEqual(
            ["one", "two"],
            [x["contributorProvenance"]["contributorId"] for x in trait]
        )
        self.assertEqual(
            {"cross-source-identity-unresolved"},
            {x["identityStatus"] for x in trait}
        )
        self.assertEqual(
            {"not-selected"},
            {x["manifestation"] for x in trait}
        )
        self.assertEqual(
            {"offered-not-authorized"},
            {x["candidateEligibility"] for x in trait}
        )
        self.assertEqual(0, sum(
            x["grantInstanceId"] is not None for x in report["candidates"]
        ))

    def test_same_local_trait_name_can_mean_distinct_mechanical_definitions(self):
        report = self.evidence([
            direct("orc", "species.orc"),
            direct("goliath", "ancestry.goliath.cloud"),
        ])
        orc = self.find(report, "species.orc#trait:powerful-build")[0]
        goliath = self.find(report, "species.goliath#trait:powerful-build")[0]
        self.assertEqual(orc["traitConceptId"], goliath["traitConceptId"])
        self.assertNotEqual(
            orc["mechanicDefinitionId"], goliath["mechanicDefinitionId"]
        )
        self.assertEqual(
            "explicit-synthetic-mapping", orc["identityStatus"]
        )
        self.assertNotEqual(
            orc["contributorProvenance"]["sourceOccurrenceId"],
            goliath["contributorProvenance"]["sourceOccurrenceId"],
        )

    def test_explicit_same_definition_still_preserves_distinct_source_offers(self):
        report = self.evidence([
            direct("orc", "species.orc"),
            direct("elf", "ancestry.elf.high"),
        ])
        first = self.find(report, "species.orc#trait:low-light-vision")[0]
        second = self.find(report, "species.elf#trait:low-light-vision")[0]
        self.assertEqual(
            first["mechanicDefinitionId"], second["mechanicDefinitionId"]
        )
        self.assertNotEqual(first["candidateId"], second["candidateId"])
        self.assertNotEqual(
            first["contributorProvenance"],
            second["contributorProvenance"]
        )
        group = next(
            entry for entry in report["explicitDefinitionGroups"]
            if entry["mechanicDefinitionId"] ==
            first["mechanicDefinitionId"]
        )
        self.assertEqual(
            {first["candidateId"], second["candidateId"]},
            set(group["candidateIds"])
        )
        self.assertEqual(
            "identity-evidence-only-no-stacking-or-grants", group["effect"]
        )
        self.assertTrue(all(
            x["grantInstanceId"] is None
            for x in (first, second)
        ))

    def test_unmapped_real_traits_do_not_acquire_semantic_identity_by_name(self):
        report = self.evidence([direct("human", "species.human")])
        trait = self.find(report, "species.human#trait:skillful")[0]
        self.assertEqual("cross-source-identity-unresolved",
                         trait["identityStatus"])
        self.assertIsNone(trait["traitConceptId"])
        self.assertIsNone(trait["mechanicDefinitionId"])
        self.assertIn(
            "species.human#trait:skillful",
            report["unmappedTraitEvidenceIds"],
        )

    def test_nested_mixed_contributor_preserves_actual_manifestation_evidence(self):
        parent = draft_manifestation_profile(
            "parent", [direct("elf", "species.elf")],
            [{"kind": "trait",
              "identity": "species.elf#trait:low-light-vision",
              "parameters": {}}],
            {}, self.blueprints, self.catalog, self.contracts,
        )
        report = self.evidence([
            {"contributorId": "mixed", "profileId": "parent"},
            direct("orc", "species.orc"),
        ], {"parent": parent})
        elf = self.find(report, "species.elf#trait:low-light-vision")[0]
        self.assertEqual("manifested-profile",
                         elf["contributorProvenance"]["kind"])
        self.assertEqual("parent",
                         elf["contributorProvenance"]["profileId"])
        self.assertEqual(
            "explicit-synthetic-mapping", elf["identityStatus"]
        )
        self.assertNotIn(
            "species.elf#trait:trance",
            {x["sourceTraitOrFieldId"] for x in report["candidates"]}
        )
        self.assertIsNone(elf["grantInstanceId"])

    def test_custom_characteristic_inputs_have_no_invented_parent_provenance(self):
        # Constructed content is an experimental source of *proposed offers*.
        # This does not integrate fully custom selection or authorize grants.
        source = {
            "status": "origin-characteristic-candidates-only",
            "offers": [
                {
                    "kind": "intrinsic", "identity": "creature.size",
                    "sourceOccurrenceId": None, "value": "small",
                    "provenance": {
                        "kind": "custom-proposal",
                        "contributorId": "manual",
                        "proposedAuthorityRef": "study.table-option",
                    },
                },
                {
                    "kind": "trait",
                    "identity": "study.manual-trait",
                    "sourceOccurrenceId": None,
                    "parameters": {"damageType": "cold"},
                    "provenance": {
                        "kind": "custom-proposal",
                        "contributorId": "manual",
                        "proposedAuthorityRef": "study.table-option",
                    },
                },
            ],
            "reviewFlags": [],
            "deferredSourceFields": [],
        }
        report = project_characteristic_identity_evidence(
            source, self.mapping
        )
        self.assertEqual(2, len(report["candidates"]))
        self.assertEqual(
            "study.manual-trait",
            report["candidates"][1]["sourceTraitOrFieldId"],
        )
        self.assertNotIn(
            "selectedSourceId",
            report["candidates"][1]["contributorProvenance"],
        )
        self.assertEqual("not-authorized", report["authority"])
        self.assertEqual(
            "cross-source-identity-unresolved",
            report["candidates"][1]["identityStatus"],
        )

    def test_custom_without_explicit_policy_provenance_is_rejected(self):
        pool = {
            "status": "origin-characteristic-candidates-only",
            "offers": [{
                "kind": "trait", "identity": "study.manual-trait",
                "sourceOccurrenceId": None, "parameters": {},
                "provenance": {
                    "kind": "custom-proposal", "contributorId": "manual",
                }
            }],
            "reviewFlags": [], "deferredSourceFields": []
        }
        with self.assertRaisesRegex(
            IdentityEvidenceError, "proposed.*policy source"
        ):
            project_characteristic_identity_evidence(pool, self.mapping)

    def test_corrupt_native_provenance_is_rejected(self):
        pool = self.pool([direct("orc", "species.orc")])
        target = next(x for x in pool["offers"] if x["kind"] == "trait")
        target["provenance"]["sourceOccurrenceId"] = "unrelated"
        with self.assertRaisesRegex(
            IdentityEvidenceError, "Mismatched authored"
        ):
            project_characteristic_identity_evidence(pool, self.mapping)

    def test_registry_rejects_duplicate_sources_and_contradictory_definitions(self):
        altered = copy.deepcopy(self.mapping)
        altered["traits"].append(copy.deepcopy(altered["traits"][0]))
        with self.assertRaisesRegex(
            IdentityEvidenceError, "Duplicate mapped"
        ):
            validate_identity_mapping(altered)

        altered = copy.deepcopy(self.mapping)
        altered["traits"][0]["definitionId"] = (
            altered["traits"][2]["definitionId"]
        )
        with self.assertRaisesRegex(
            IdentityEvidenceError, "conflicting concepts"
        ):
            validate_identity_mapping(altered)

    def test_registry_rejects_inferred_or_incomplete_claims(self):
        for mutation in ("status", "extra", "missing", "empty"):
            altered = copy.deepcopy(self.mapping)
            if mutation == "status":
                altered["status"] = "approved"
            elif mutation == "extra":
                altered["traits"][0]["matchByName"] = True
            elif mutation == "missing":
                del altered["traits"][0]["definitionId"]
            else:
                altered["traits"][0]["conceptId"] = ""
            with self.subTest(mutation=mutation):
                with self.assertRaises(IdentityEvidenceError):
                    validate_identity_mapping(altered)

    def test_whole_corpus_provenance_and_unresolved_evidence_preserved(self):
        for sid in sorted(self.catalog):
            if self.catalog[sid].get("selectable") is not True:
                continue
            with self.subTest(source=sid):
                pool = self.pool([direct("selected", sid)])
                report = project_characteristic_identity_evidence(
                    pool, self.mapping
                )
                self.assertEqual(
                    len(pool["offers"]), len(report["candidates"])
                )
                self.assertEqual(
                    pool["deferredSourceFields"],
                    report["deferredSourceFields"]
                )
                self.assertEqual(
                    pool["reviewFlags"], report["reviewFlags"]
                )
                for offer, candidate in zip(
                    pool["offers"], report["candidates"]
                ):
                    self.assertEqual(
                        offer["provenance"],
                        candidate["contributorProvenance"]
                    )
                    self.assertEqual(offer, candidate["originalOffer"])
                    self.assertIsNone(candidate["grantInstanceId"])

    def test_literal_source_evidence_annotates_native_amphibious_offers(self):
        pool = self.pool([
            direct("black", "ancestry.dragonborn.black"),
            direct("green", "ancestry.dragonborn.green"),
        ])
        report = project_characteristic_identity_evidence(
            pool, self.mapping, curated=read_curated_evidence(),
            catalog=self.catalog,
        )
        offers = [
            x for x in report["candidates"]
            if x["sourceTraitOrFieldId"].endswith("#trait:amphibious")
        ]
        self.assertEqual(2, len(offers))
        self.assertEqual(
            {"study.definition.amphibious-air-water"},
            {x["literalAuthoredIdentityCandidateId"] for x in offers},
        )
        self.assertEqual(
            {"verified-native-authored-content-only"},
            {x["literalAuthoredIdentityStatus"] for x in offers},
        )
        for item in offers:
            self.assertIsNone(item["grantInstanceId"])
            self.assertEqual("not-selected", item["manifestation"])
        self.assertEqual("not-authorized", report["authority"])

    def test_same_literal_group_does_not_grant_or_stack_low_light_vision(self):
        pool = self.pool([
            direct("orc", "species.orc"),
            direct("elf", "ancestry.elf.high"),
        ])
        report = project_characteristic_identity_evidence(
            pool, self.mapping, curated=read_curated_evidence(),
            catalog=self.catalog,
        )
        lowlight = [x for x in report["candidates"]
                    if x["sourceTraitOrFieldId"].endswith(
                        "#trait:low-light-vision"
                    )]
        self.assertEqual(2, len(lowlight))
        self.assertEqual(
            {"study.definition.low-light-vision"},
            {x["literalAuthoredIdentityCandidateId"] for x in lowlight}
        )
        self.assertEqual(
            ["orc", "elf"],
            [x["contributorProvenance"]["contributorId"]
             for x in lowlight]
        )
        self.assertTrue(all(x["grantInstanceId"] is None
                            for x in lowlight))

    def test_nested_or_custom_trait_cannot_impersonate_a_verified_source(self):
        parent = draft_manifestation_profile(
            "parent", [direct("orc", "species.orc")],
            [{"kind": "trait",
              "identity": "species.orc#trait:low-light-vision",
              "parameters": {}}],
            {}, self.blueprints, self.catalog, self.contracts,
        )
        pool = self.pool(
            [{"contributorId": "parent", "profileId": "parent"}],
            {"parent": parent},
        )
        report = project_characteristic_identity_evidence(
            pool, self.mapping, curated=read_curated_evidence(),
            catalog=self.catalog,
        )
        trait = self.find(report, "species.orc#trait:low-light-vision")[0]
        # The historical definition is still traceable through the
        # nested profile, but the immediate offer is not a native source.
        self.assertIsNone(trait["literalAuthoredIdentityCandidateId"])
        self.assertEqual("manifested-profile",
                         trait["contributorProvenance"]["kind"])

        pool["offers"][0]["provenance"] = {
            "kind": "custom-proposal", "contributorId": "manual",
            "proposedAuthorityRef": "study.custom",
        }
        pool["offers"][0]["sourceOccurrenceId"] = None
        report = project_characteristic_identity_evidence(
            pool, self.mapping, curated=read_curated_evidence(),
            catalog=self.catalog,
        )
        self.assertIsNone(
            report["candidates"][0]["literalAuthoredIdentityCandidateId"]
        )

    def test_curated_evidence_rejects_forged_native_trait_mechanics(self):
        pool = self.pool([direct("orc", "species.orc")])
        offer = next(x for x in pool["offers"]
                     if x["identity"] ==
                     "species.orc#trait:low-light-vision")
        offer["authoredDefinition"]["rule"] = "rule.changed"
        with self.assertRaisesRegex(
            IdentityEvidenceError, "Native offer differs"
        ):
            project_characteristic_identity_evidence(
                pool, self.mapping, curated=read_curated_evidence(),
                catalog=self.catalog,
            )

    def test_curated_evidence_requires_current_native_catalog(self):
        pool = self.pool([direct("orc", "species.orc")])
        with self.assertRaisesRegex(
            IdentityEvidenceError, "requires a source catalog"
        ):
            project_characteristic_identity_evidence(
                pool, self.mapping, curated=read_curated_evidence()
            )
        altered = copy.deepcopy(self.catalog)
        altered["species.orc"]["traits"][0]["description"] += " Revised."
        with self.assertRaisesRegex(
            IdentityEvidenceError, "no longer matches native data"
        ):
            project_characteristic_identity_evidence(
                pool, self.mapping, curated=read_curated_evidence(),
                catalog=altered,
            )

    def test_projection_is_pure_deterministic_and_does_not_modify_inputs(self):
        pool = self.pool([
            direct("red", "ancestry.dragonborn.red"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        ])
        before_pool = copy.deepcopy(pool)
        before_map = copy.deepcopy(self.mapping)
        result = project_characteristic_identity_evidence(
            pool, self.mapping
        )
        self.assertEqual(
            result, project_characteristic_identity_evidence(
                pool, self.mapping
            )
        )
        result["candidates"][0]["contributorProvenance"].clear()
        self.assertEqual(before_pool, pool)
        self.assertEqual(before_map, self.mapping)


if __name__ == "__main__":
    unittest.main()
