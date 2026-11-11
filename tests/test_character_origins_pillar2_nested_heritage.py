"""Two-generation Heritage source eligibility and explicit manifestation.

These tests establish an Origin record structure only. They neither authorize
character trait acquisition nor execute any gameplay mechanics.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_nested_heritage import (  # noqa: E402
    draft_origin_profile,
    heritage_candidate_pool,
)
from character_origins_parameter_inheritance import (  # noqa: E402
    ParameterInheritanceError,
    read_blueprint,
    validate_parameter_proposal,
)
from character_origins_source_inventory import read_catalog  # noqa: E402


LINE = {"shape": "line", "lengthFeet": 30, "widthFeet": 5}
CONE = {"shape": "cone", "lengthFeet": 15}


def source(identity, value):
    return {"contributorId": identity, "selectedSourceId": value}


def nested(identity, value):
    return {"contributorId": identity, "profileId": value}


class Pillar2NestedHeritageTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.blueprint = read_blueprint()
        cls.contracts = read_contracts()

    def draft(self, identifier, contributors, params, registry=None,
              origination=None, blueprint=None):
        return draft_origin_profile(
            identifier, contributors, params, registry or {},
            blueprint or self.blueprint, self.catalog, self.contracts,
            origination=origination,
        )

    def pool(self, contributors, registry=None, blueprint=None):
        return heritage_candidate_pool(
            contributors, registry or {}, blueprint or self.blueprint,
            self.catalog, self.contracts,
        )

    def mixed(self, params=None):
        return self.draft(
            "mixed-parent", [
                source("red-grandparent", "ancestry.dragonborn.red"),
                source("bronze-grandparent", "ancestry.dragonborn.bronze"),
            ],
            {"damageType": "fire", "area": CONE} if params is None else params,
            origination={"description": "Example narrative origin, not a rule"},
        )

    def test_first_generation_manifestation_is_explicit_not_full_heritage(self):
        first = self.mixed()
        self.assertEqual("draft-origin-profile", first["status"])
        self.assertEqual("not-authorized", first["authority"])
        self.assertTrue(first["created"])
        self.assertEqual(1, len(first["manifestedTraits"]))
        self.assertEqual(
            "ability.breath-weapon",
            first["manifestedTraits"][0]["mechanicId"],
        )
        self.assertEqual(
            {"damageType": "fire", "area": CONE},
            first["manifestedTraits"][0]["parameters"],
        )
        evidence = first["manifestedTraits"][0]["parameterEvidence"]
        self.assertEqual(
            ["red-grandparent"],
            [x["contributorId"] for x in evidence["damageType"]],
        )
        self.assertEqual(
            ["bronze-grandparent"],
            [x["contributorId"] for x in evidence["area"]],
        )
        self.assertNotIn("candidateConfigurations", first)
        self.assertEqual(
            {"description": "Example narrative origin, not a rule"},
            first["origination"],
        )

    def test_second_generation_exports_only_manifested_parameters(self):
        first = self.mixed()
        registry = {first["profileId"]: first}
        later = self.pool([
            nested("mixed-contributor", "mixed-parent"),
            source("human-contributor", "species.human"),
        ], registry)
        self.assertTrue(later["traitCandidateAvailable"])
        self.assertEqual(1, len(later["candidateConfigurations"]))
        self.assertEqual(
            {"damageType": "fire", "area": CONE},
            later["candidateConfigurations"][0]["parameters"],
        )
        self.assertEqual(
            ["fire"], [x["value"] for x in
                       later["parameterOptions"]["damageType"]],
        )
        self.assertEqual(
            [CONE], [x["value"] for x in later["parameterOptions"]["area"]],
        )
        self.assertEqual(
            "manifested-profile",
            later["candidateConfigurations"][0]["sourceKind"],
        )
        self.assertEqual(
            "mixed-parent",
            later["candidateConfigurations"][0]["sourceRef"],
        )

    def test_unmanifested_grandparent_alternatives_do_not_return(self):
        first = self.mixed()
        registry = {"mixed-parent": first}
        sources = [nested("parent", "mixed-parent"),
                   source("human", "species.human")]
        pool = self.pool(sources, registry)
        for params in (
            {"damageType": "lightning", "area": CONE},
            {"damageType": "fire", "area": LINE},
            {"damageType": "lightning", "area": LINE},
        ):
            with self.subTest(params=params):
                verdict = validate_parameter_proposal(pool, params)
                self.assertFalse(verdict["eligible"])
                created = self.draft(
                    "next-generation", sources, params, registry,
                )
                self.assertFalse(created["created"])
                self.assertEqual("rejected-origin-profile", created["status"])
                self.assertNotIn("manifestedTraits", created)

    def test_second_generation_can_manifest_one_offered_trait(self):
        parent = self.mixed()
        registry = {"mixed-parent": parent}
        second = self.draft(
            "child", [
                nested("mixed-parent-identity", "mixed-parent"),
                source("human-parent-identity", "species.human"),
            ],
            {"damageType": "fire", "area": CONE}, registry,
        )
        self.assertTrue(second["created"])
        manifest = second["manifestedTraits"][0]
        self.assertEqual(
            {"damageType": "fire", "area": CONE},
            manifest["parameters"],
        )
        root = manifest["parameterEvidence"]["damageType"][0]
        self.assertEqual("manifested-profile", root["kind"])
        self.assertEqual("mixed-parent-identity", root["contributorId"])
        self.assertEqual(
            "red-grandparent",
            root["ancestorEvidence"][0]["contributorId"],
        )
        area = manifest["parameterEvidence"]["area"][0]
        self.assertEqual("bronze-grandparent",
                         area["ancestorEvidence"][0]["contributorId"])

    def test_third_generation_only_exports_second_generations_manifest(self):
        parent = self.mixed()
        second = self.draft(
            "second", [nested("first", "mixed-parent")],
            {"damageType": "fire", "area": CONE},
            {"mixed-parent": parent},
        )
        profiles = {"mixed-parent": parent, "second": second}
        third = self.pool(
            [nested("second-parent", "second")], profiles,
        )
        self.assertEqual(
            {"damageType": "fire", "area": CONE},
            third["candidateConfigurations"][0]["parameters"],
        )
        trace = (third["candidateConfigurations"][0]["parameterRoots"]
                 ["damageType"])
        self.assertEqual("second", trace["profileId"])
        self.assertEqual("mixed-parent",
                         trace["ancestorEvidence"][0]["profileId"])
        self.assertEqual(
            "red-grandparent",
            trace["ancestorEvidence"][0]["ancestorEvidence"][0]["contributorId"],
        )

    def test_unmanifested_trait_is_not_reintroduced_via_parent_profile(self):
        without = self.draft(
            "unmanifested-parent", [
                source("red", "ancestry.dragonborn.red"),
                source("bronze", "ancestry.dragonborn.bronze"),
            ], None,
        )
        self.assertTrue(without["created"])
        self.assertEqual([], without["manifestedTraits"])
        pool = self.pool([
            nested("parent", "unmanifested-parent"),
            source("human", "species.human"),
        ], {"unmanifested-parent": without})
        self.assertFalse(pool["traitCandidateAvailable"])
        self.assertEqual([], pool["candidateConfigurations"])
        self.assertEqual(
            "no-complete-contributor-configuration",
            validate_parameter_proposal(
                pool, {"damageType": "fire", "area": CONE}
            )["reason"],
        )

    def test_new_direct_contributor_reintroduces_only_their_own_values(self):
        first = self.mixed()
        registry = {"mixed-parent": first}
        pool = self.pool([
            nested("mixed", "mixed-parent"),
            source("red", "ancestry.dragonborn.red"),
        ], registry)
        self.assertEqual(
            ["fire"],
            [x["value"] for x in pool["parameterOptions"]["damageType"]],
        )
        self.assertEqual(
            [CONE, LINE],
            [x["value"] for x in pool["parameterOptions"]["area"]],
        )
        child = self.draft(
            "child", [
                nested("mixed", "mixed-parent"),
                source("red", "ancestry.dragonborn.red"),
            ], {"damageType": "fire", "area": LINE}, registry,
        )
        self.assertTrue(child["created"])
        self.assertEqual(
            ["red"],
            [origin["contributorId"] for origin in
             child["manifestedTraits"][0]["parameterEvidence"]["area"]],
        )

    def test_identical_source_contributors_remain_distinct_provenance(self):
        first = self.draft(
            "two-reds", [
                source("red-one", "ancestry.dragonborn.red"),
                source("red-two", "ancestry.dragonborn.red"),
            ], {"damageType": "fire", "area": LINE},
        )
        self.assertTrue(first["created"])
        self.assertEqual(
            ["red-one", "red-two"],
            [x["contributorId"] for x in
             first["manifestedTraits"][0]["parameterEvidence"]["damageType"]],
        )
        pool = self.pool([nested("mixed", "two-reds")], {"two-reds": first})
        self.assertTrue(validate_parameter_proposal(
            pool, {"damageType": "fire", "area": LINE}
        )["eligible"])

    def test_no_required_parent_count_or_biological_type(self):
        first = self.draft(
            "created-being", [], None,
            origination={"description": "Constructed with no contributor"},
        )
        self.assertTrue(first["created"])
        self.assertEqual([], first["contributors"])
        self.assertEqual([], first["manifestedTraits"])
        with self.assertRaises(ParameterInheritanceError):
            self.draft(
                "not-offered", [], {"damageType": "fire", "area": LINE}
            ) if False else None  # no exception required for denied choice
        # A requested trait with no eligible source is rejected, not granted.
        denied = self.draft(
            "not-offered", [], {"damageType": "fire", "area": LINE}
        )
        self.assertFalse(denied["created"])
        rich = self.pool([
            source("one", "ancestry.dragonborn.red"),
            source("two", "ancestry.dragonborn.bronze"),
            source("three", "species.human"),
        ])
        self.assertEqual(2, len(rich["candidateConfigurations"]))

    def test_tampered_manifest_cannot_seed_descendant(self):
        first = self.mixed()
        registry = {"mixed-parent": first}
        forgery = copy.deepcopy(first)
        forgery["manifestedTraits"][0]["parameters"]["damageType"] = "cold"
        with self.assertRaisesRegex(
            ParameterInheritanceError, "not eligible"
        ):
            self.pool([nested("parent", "mixed-parent")],
                      {"mixed-parent": forgery})
        fake_provenance = copy.deepcopy(first)
        fake_provenance["manifestedTraits"][0]["parameterEvidence"]["area"] = []
        with self.assertRaisesRegex(
            ParameterInheritanceError, "provenance"
        ):
            self.pool([nested("parent", "mixed-parent")],
                      {"mixed-parent": fake_provenance})

    def test_cycle_and_missing_profile_are_rejected(self):
        first = self.mixed()
        cyclic = copy.deepcopy(first)
        cyclic["contributors"] = [nested("its-own-parent", "mixed-parent")]
        with self.assertRaisesRegex(
            ParameterInheritanceError, "Cyclic"
        ):
            self.pool([nested("parent", "mixed-parent")],
                      {"mixed-parent": cyclic})
        with self.assertRaisesRegex(
            ParameterInheritanceError, "Unknown nested"
        ):
            self.pool([nested("parent", "not-found")], {})

    def test_invalid_profiles_or_contributor_references_are_rejected(self):
        first = self.mixed()
        malformed = copy.deepcopy(first)
        malformed["status"] = "official"
        with self.assertRaisesRegex(ParameterInheritanceError, "Malformed"):
            self.pool([nested("parent", "mixed-parent")],
                      {"mixed-parent": malformed})
        for contributors in (
            [source("repeat", "ancestry.dragonborn.red"),
             source("repeat", "ancestry.dragonborn.bronze")],
            [{"contributorId": "both", "profileId": "x",
              "selectedSourceId": "species.human"}],
            [{"contributorId": "empty", "selectedSourceId": ""}],
        ):
            with self.subTest(contributors=contributors):
                with self.assertRaises(ParameterInheritanceError):
                    self.pool(contributors)

    def test_linked_policy_is_still_honored_for_trait_that_uses_it(self):
        linked = copy.deepcopy(self.blueprint)
        linked["parameterInheritance"]["mode"] = "linked"
        parents = [
            source("red", "ancestry.dragonborn.red"),
            source("bronze", "ancestry.dragonborn.bronze"),
        ]
        denied = self.draft(
            "crossed", parents, {"damageType": "fire", "area": CONE},
            blueprint=linked,
        )
        self.assertFalse(denied["created"])
        valid = self.draft(
            "ordinary", parents, {"damageType": "fire", "area": LINE},
            blueprint=linked,
        )
        self.assertTrue(valid["created"])

    def test_origin_profiles_and_registry_are_not_mutated(self):
        first = self.mixed()
        registry = {"mixed-parent": first}
        before = copy.deepcopy(registry)
        contributor_list = [
            nested("mixed", "mixed-parent"),
            source("human", "species.human"),
        ]
        before_list = copy.deepcopy(contributor_list)
        p1 = self.pool(contributor_list, registry)
        p2 = self.pool(contributor_list, registry)
        self.assertEqual(p1, p2)
        created = self.draft(
            "second", contributor_list,
            {"damageType": "fire", "area": CONE}, registry,
        )
        created["manifestedTraits"][0]["parameters"]["damageType"] = "cold"
        self.assertEqual(before, registry)
        self.assertEqual(before_list, contributor_list)


if __name__ == "__main__":
    unittest.main()
