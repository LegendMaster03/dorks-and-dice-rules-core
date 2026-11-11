"""Provisional trait/parameter inheritance eligibility (not actual grants).

The comparison between linked and independent rules is intentional test
evidence, not an approval of either rule for the native Breath Weapon.
"""
import copy
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_parameter_inheritance import (  # noqa: E402
    ParameterInheritanceError,
    candidate_pool,
    read_blueprint,
    validate_parameter_proposal,
)
from character_origins_source_inventory import read_catalog  # noqa: E402


FIRE = "fire"
LIGHTNING = "lightning"
LINE = {"shape": "line", "lengthFeet": 30, "widthFeet": 5}
CONE = {"shape": "cone", "lengthFeet": 15}


def contributor(key, source):
    return {"contributorId": key, "selectedSourceId": source}


class Pillar2ParameterInheritanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        cls.blueprint = read_blueprint()

    def pool(self, contributors, blueprint=None, catalog=None, contracts=None):
        return candidate_pool(
            contributors,
            blueprint if blueprint is not None else self.blueprint,
            catalog if catalog is not None else self.catalog,
            contracts if contracts is not None else self.contracts,
        )

    def test_standard_red_and_gold_expose_only_authored_parameters(self):
        p = self.pool([
            contributor("red", "ancestry.dragonborn.red"),
            contributor("gold", "ancestry.dragonborn.gold"),
        ])
        self.assertEqual("eligibility-evidence-only", p["status"])
        self.assertEqual("ability.breath-weapon", p["mechanicId"])
        self.assertEqual("linked", p["parameterInheritance"]["mode"])
        self.assertTrue(p["traitCandidateAvailable"])
        self.assertFalse(p["unresolvedContributors"])
        self.assertEqual(
            {"damageType": FIRE, "area": LINE},
            p["candidateConfigurations"][0]["parameters"],
        )
        self.assertEqual(
            {"damageType": FIRE, "area": CONE},
            p["candidateConfigurations"][1]["parameters"],
        )
        self.assertEqual(
            {"damageType", "area"}, set(p["parameterOptions"]),
        )
        self.assertEqual(
            [FIRE],
            [option["value"] for option in p["parameterOptions"]["damageType"]],
        )
        self.assertEqual(
            ["red", "gold"],
            [origin["contributorId"] for origin in
             p["parameterOptions"]["damageType"][0]["contributedBy"]],
        )
        self.assertEqual(
            "ancestry.dragonborn.chromatic",
            p["candidateConfigurations"][0]["resolvedFromSources"]["area"],
        )
        self.assertEqual(
            "ancestry.dragonborn.metallic",
            p["candidateConfigurations"][1]["resolvedFromSources"]["area"],
        )
        self.assertEqual(
            "species.dragonborn#trait:breath-weapon",
            p["definitionOccurrenceId"],
        )
        self.assertNotIn("activation", p["parameterOptions"])
        self.assertNotIn("uses", p["parameterOptions"])

    def test_linked_mode_rejects_crossed_pairs(self):
        p = self.pool([
            contributor("red", "ancestry.dragonborn.red"),
            contributor("bronze", "ancestry.dragonborn.bronze"),
        ])
        possible = validate_parameter_proposal(
            p, {"damageType": FIRE, "area": LINE}
        )
        self.assertTrue(possible["eligible"])
        self.assertEqual(["red"], possible["configurationContributors"])
        crossed = validate_parameter_proposal(
            p, {"damageType": FIRE, "area": CONE}
        )
        self.assertFalse(crossed["eligible"])
        self.assertEqual(
            "linked-configuration-not-offered", crossed["reason"]
        )
        crossed_reverse = validate_parameter_proposal(
            p, {"damageType": LIGHTNING, "area": LINE}
        )
        self.assertFalse(crossed_reverse["eligible"])

    def test_independent_mode_allows_authorized_parameter_recombination(self):
        bp = copy.deepcopy(self.blueprint)
        bp["parameterInheritance"]["mode"] = "independent"
        p = self.pool([
            contributor("red", "ancestry.dragonborn.red"),
            contributor("bronze", "ancestry.dragonborn.bronze"),
        ], blueprint=bp)
        crossed = validate_parameter_proposal(
            p, {"damageType": FIRE, "area": CONE}
        )
        self.assertTrue(crossed["eligible"])
        self.assertEqual(
            ["red"], [x["contributorId"] for x in
                     crossed["parameterProvenance"]["damageType"]],
        )
        self.assertEqual(
            ["bronze"], [x["contributorId"] for x in
                        crossed["parameterProvenance"]["area"]],
        )
        self.assertEqual("eligible-proposal-not-earned", crossed["status"])
        reverse = validate_parameter_proposal(
            p, {"damageType": LIGHTNING, "area": LINE}
        )
        self.assertTrue(reverse["eligible"])
        not_offered = validate_parameter_proposal(
            p, {"damageType": "cold", "area": CONE}
        )
        self.assertEqual(
            {"eligible": False, "reason": "parameter-value-not-offered",
             "parameter": "damageType"},
            not_offered,
        )

    def test_identical_contributors_remain_independent_occurrences(self):
        p = self.pool([
            contributor("first", "ancestry.dragonborn.red"),
            contributor("second", "ancestry.dragonborn.red"),
        ])
        self.assertEqual(2, len(p["candidateConfigurations"]))
        result = validate_parameter_proposal(
            p, {"damageType": FIRE, "area": LINE}
        )
        self.assertTrue(result["eligible"])
        self.assertEqual(
            ["first", "second"], result["configurationContributors"]
        )
        # This shows a matching standard parameter set is *permitted*.
        # It does not prove full mechanical character reconstruction.

    def test_non_contributing_parent_does_not_confer_the_trait(self):
        p = self.pool([
            contributor("human", "species.human"),
            contributor("red", "ancestry.dragonborn.red"),
        ])
        self.assertEqual(["red"], [
            x["contributorId"] for x in p["candidateConfigurations"]
        ])
        human_only = self.pool([contributor("human", "species.human")])
        self.assertFalse(human_only["traitCandidateAvailable"])
        self.assertEqual(
            "no-complete-contributor-configuration",
            validate_parameter_proposal(
                human_only, {"damageType": FIRE, "area": LINE}
            )["reason"],
        )

    def test_missing_parameter_does_not_produce_partial_candidate(self):
        broken = copy.deepcopy(self.catalog)
        del broken["ancestry.dragonborn.red"]["damageType"]
        p = self.pool(
            [contributor("red", "ancestry.dragonborn.red")],
            catalog=broken,
        )
        self.assertFalse(p["traitCandidateAvailable"])
        self.assertEqual([], p["candidateConfigurations"])
        self.assertEqual(
            "lineage-field-not-found",
            p["unresolvedContributors"][0]["problems"]["damageType"],
        )
        self.assertEqual([], p["parameterOptions"]["damageType"])

    def test_mismatched_reference_fails_closed_in_pool(self):
        broken = copy.deepcopy(self.catalog)
        broken["species.dragonborn"]["traits"][2]["areaFrom"] = (
            "unapproved-custom-reference"
        )
        p = self.pool(
            [contributor("red", "ancestry.dragonborn.red")],
            catalog=broken,
        )
        self.assertFalse(p["traitCandidateAvailable"])
        self.assertEqual(
            "authored-reference-mismatch",
            p["unresolvedContributors"][0]["problems"]["area"],
        )

    def test_homebrew_contributor_uses_exact_same_generic_eligibility(self):
        catalog = copy.deepcopy(self.catalog)
        catalog["species.test-homebrew"] = {
            "id": "species.test-homebrew", "kind": "species",
            "selectable": True, "name": "Test Homebrew",
            "damageType": "cold",
            "breathWeaponArea": {"shape": "cone", "lengthFeet": 20},
            "traits": [{
                "id": "elemental-wave", "damage": {
                    "typeFrom": "author-provided-type"
                }, "areaFrom": "author-provided-area",
                "activation": {"action": "action"},
            }],
        }
        source_id = "species.test-homebrew#trait:elemental-wave"
        bp = copy.deepcopy(self.blueprint)
        bp["definitionOccurrenceId"] = source_id
        contracts = copy.deepcopy(self.contracts) + [{
            "sourceOccurrenceId": source_id,
            "bindings": [
                {"path": "damage.typeFrom",
                 "authoredReference": "author-provided-type",
                 "lookup": {
                     "scope": "selected-lineage", "field": "damageType",
                     "order": "nearest-to-selected",
                     "expectedType": "string",
                 }},
                {"path": "areaFrom",
                 "authoredReference": "author-provided-area",
                 "lookup": {
                     "scope": "selected-lineage", "field": "breathWeaponArea",
                     "order": "nearest-to-selected",
                     "expectedType": "object",
                 }},
            ],
        }]
        p = self.pool(
            [contributor("homebrew", "species.test-homebrew")],
            blueprint=bp, catalog=catalog, contracts=contracts,
        )
        self.assertTrue(p["traitCandidateAvailable"])
        self.assertTrue(validate_parameter_proposal(
            p, {"damageType": "cold",
                "area": {"shape": "cone", "lengthFeet": 20}},
        )["eligible"])

    def test_invalid_input_and_policy_do_not_create_eligibility(self):
        invalid_blueprint = copy.deepcopy(self.blueprint)
        invalid_blueprint["parameterInheritance"]["mode"] = "anything"
        with self.assertRaises(ParameterInheritanceError):
            self.pool([
                contributor("red", "ancestry.dragonborn.red")
            ], blueprint=invalid_blueprint)
        with self.assertRaises(ParameterInheritanceError):
            self.pool([
                contributor("same", "ancestry.dragonborn.red"),
                contributor("same", "ancestry.dragonborn.gold"),
            ])
        with self.assertRaises(ParameterInheritanceError):
            self.pool([])
        with self.assertRaises(ParameterInheritanceError):
            validate_parameter_proposal(
                self.pool([
                    contributor("red", "ancestry.dragonborn.red")
                ]),
                {"damageType": FIRE},
            )
        with self.assertRaises(ParameterInheritanceError):
            validate_parameter_proposal(
                self.pool([
                    contributor("red", "ancestry.dragonborn.red")
                ]),
                {"damageType": FIRE, "area": LINE,
                 "uses": {"value": 999}},
            )

    def test_parameter_values_require_exact_json_types(self):
        # Python normally considers True == 1; the heritage evaluator may not
        # silently reinterpret a boolean parameter as a numeric grant.
        p = {
            "traitCandidateAvailable": True,
            "parameterInheritance": {"mode": "linked"},
            "parameterOptions": {"example": [{
                "value": 1, "contributedBy": [{"contributorId": "one"}],
            }]},
            "candidateConfigurations": [{
                "contributorId": "one", "parameters": {"example": 1},
            }],
        }
        self.assertFalse(
            validate_parameter_proposal(p, {"example": True})["eligible"]
        )
        p["parameterInheritance"]["mode"] = "independent"
        self.assertFalse(
            validate_parameter_proposal(p, {"example": True})["eligible"]
        )
        self.assertTrue(
            validate_parameter_proposal(p, {"example": 1})["eligible"]
        )

    def test_pool_does_not_mutate_source_or_policy(self):
        before_catalog = copy.deepcopy(self.catalog)
        before_policy = copy.deepcopy(self.blueprint)
        before_contracts = copy.deepcopy(self.contracts)
        contributors = [
            contributor("first", "ancestry.dragonborn.red"),
            contributor("second", "ancestry.dragonborn.bronze"),
        ]
        p = self.pool(contributors)
        self.assertEqual(p, self.pool(contributors))
        p["candidateConfigurations"][0]["parameters"]["damageType"] = "changed"
        self.assertEqual(before_catalog, self.catalog)
        self.assertEqual(before_policy, self.blueprint)
        self.assertEqual(before_contracts, self.contracts)


if __name__ == "__main__":
    unittest.main()
