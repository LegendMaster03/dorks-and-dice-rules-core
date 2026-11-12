"""Pillar 2 evidence: existing Dragonborn source parameters, no combat.

The authored source binding contracts already exist. These tests make all
three compound traits available to the same experimental Origin selection
model and ensure that the new registry creates no cross-trait grant or
damage-type coupling policy.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compound_traits import (  # noqa: E402
    project_compound_traits, read_contracts,
)
from character_origins_configured_trait import configure_trait  # noqa: E402
from character_origins_manifestation_inventory import (  # noqa: E402
    characteristic_candidate_pool, draft_manifestation_profile,
)
from character_origins_parameter_inheritance import (  # noqa: E402
    ParameterInheritanceError, candidate_pool, read_blueprint,
    read_blueprints, validate_parameter_proposal,
)
from character_origins_source_inventory import read_catalog  # noqa: E402

BREATH = "species.dragonborn#trait:breath-weapon"
RESISTANCE = "species.dragonborn#trait:draconic-resistance"
WARDING = "ancestry.dragonborn.chromatic#trait:chromatic-warding"


def contributor(identity, source):
    return {"contributorId": identity, "selectedSourceId": source}


class Pillar2DragonbornBindingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.blueprints = read_blueprints()
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()

    def pool(self, occurrence, *contributors, catalog=None):
        return candidate_pool(
            list(contributors), self.blueprints[occurrence],
            self.catalog if catalog is None else catalog, self.contracts,
        )

    def preview(self, occurrence, *contributors, damage, catalog=None):
        return configure_trait(
            list(contributors), {"damageType": damage},
            self.blueprints[occurrence],
            self.catalog if catalog is None else catalog, self.contracts,
        )

    def test_registry_includes_exact_three_independent_trait_definitions(self):
        self.assertEqual({BREATH, RESISTANCE, WARDING}, set(self.blueprints))
        self.assertEqual("ability.breath-weapon",
                         read_blueprint()["mechanicId"])
        self.assertEqual(
            {"damageType": {"bindingPath": "damageResistance.damageTypeFrom"}},
            self.blueprints[RESISTANCE]["parameters"],
        )
        self.assertEqual(
            {"damageType": {
                "bindingPath": "damageResistanceOverride.damageTypeFrom"
            }},
            self.blueprints[WARDING]["parameters"],
        )
        self.assertEqual(
            "independent",
            self.blueprints[BREATH]["parameterInheritance"]["mode"],
        )
        for occurrence in (RESISTANCE, WARDING):
            self.assertEqual(
                "linked",
                self.blueprints[occurrence]["parameterInheritance"]["mode"],
            )

    def test_red_native_source_resolves_all_three_binding_definitions(self):
        report = project_compound_traits(
            "ancestry.dragonborn.red", self.catalog, self.contracts
        )
        by_occurrence = {
            t["sourceOccurrenceId"]: t for t in report["compoundTraits"]
        }
        expected = {
            RESISTANCE: "damageResistance.damageTypeFrom",
            WARDING: "damageResistanceOverride.damageTypeFrom",
        }
        for occurrence, path in expected.items():
            with self.subTest(occurrence=occurrence):
                trait = by_occurrence[occurrence]
                self.assertEqual("parameters-bound", trait["bindingStatus"])
                self.assertEqual(
                    "fire",
                    next(v for v in trait["parameterBindings"]
                         if v["path"] == path)["resolvedValue"],
                )

    def test_red_resistance_preview_preserves_unmodified_percent(self):
        red = contributor("red", "ancestry.dragonborn.red")
        p = self.preview(RESISTANCE, red, damage="fire")
        self.assertTrue(p["configured"])
        self.assertEqual("ability.draconic-resistance", p["mechanicId"])
        self.assertEqual(50, p["configuredDefinition"]["damageResistance"]["percent"])
        self.assertEqual("fire",
                         p["configuredDefinition"]["damageResistance"]["damageType"])
        self.assertEqual(
            "selected-dragonborn-leaf",
            p["authoredDefinition"]["damageResistance"]["damageTypeFrom"],
        )
        self.assertNotIn("damageTypeFrom",
                         p["configuredDefinition"]["damageResistance"])
        self.assertEqual(["red"], p["configurationContributors"])
        self.assertEqual("configured-preview-not-earned", p["status"])
        self.assertEqual("not-evaluated", p["evaluationStatus"])

    def test_chromatic_warding_preview_preserves_all_fixed_conditions(self):
        red = contributor("red", "ancestry.dragonborn.red")
        p = self.preview(WARDING, red, damage="fire")
        self.assertTrue(p["configured"])
        authored, configured = p["authoredDefinition"], p["configuredDefinition"]
        self.assertEqual(100, configured["damageResistanceOverride"]["percent"])
        self.assertEqual("fire",
                         configured["damageResistanceOverride"]["damageType"])
        self.assertEqual(
            "replace-lower-same-type",
            configured["damageResistanceOverride"]["stacking"],
        )
        for field in (
            "requiredCharacterLevel", "activation", "duration", "uses",
            "doesNotGrantConditionImmunity", "description",
        ):
            with self.subTest(field=field):
                self.assertEqual(authored[field], configured[field])
        self.assertEqual("ability.chromatic-warding", p["mechanicId"])
        self.assertEqual("configured-preview-not-earned", p["status"])

    def test_metallic_source_has_resistance_but_not_chromatic_warding(self):
        bronze = contributor("bronze", "ancestry.dragonborn.bronze")
        resistance = self.pool(RESISTANCE, bronze)
        self.assertTrue(resistance["traitCandidateAvailable"])
        self.assertEqual(
            [{"value": "lightning", "contributedBy": [{
                "contributorId": "bronze",
                "selectedSourceId": "ancestry.dragonborn.bronze",
                "resolvedFromSourceId": "ancestry.dragonborn.bronze"
            }]}],
            resistance["parameterOptions"]["damageType"],
        )
        self.assertFalse(self.pool(WARDING, bronze)["traitCandidateAvailable"])
        p = self.preview(WARDING, bronze, damage="lightning")
        self.assertFalse(p["configured"])
        self.assertEqual("no-complete-contributor-configuration", p["reason"])

    def test_gem_breath_and_resistance_use_existing_generic_bindings(self):
        # Gem contributes a normal leaf damage type and a complete family
        # cone. No Gem-specific resolver branch or Chromatic Warding is needed.
        expected = {
            "amethyst": "force", "crystal": "radiant",
            "emerald": "psychic", "sapphire": "thunder",
            "topaz": "necrotic",
        }
        area = {"shape": "cone", "lengthFeet": 15}
        for slug, damage in expected.items():
            with self.subTest(slug=slug):
                gem = contributor(slug, "ancestry.dragonborn." + slug)
                breath = self.pool(BREATH, gem)
                resistance = self.pool(RESISTANCE, gem)
                self.assertTrue(breath["traitCandidateAvailable"])
                self.assertTrue(resistance["traitCandidateAvailable"])
                self.assertEqual(
                    [damage],
                    [row["value"] for row in
                     breath["parameterOptions"]["damageType"]],
                )
                self.assertEqual(
                    [area],
                    [row["value"] for row in
                     breath["parameterOptions"]["area"]],
                )
                self.assertEqual(
                    [damage],
                    [row["value"] for row in
                     resistance["parameterOptions"]["damageType"]],
                )
                self.assertFalse(self.pool(WARDING, gem)
                                 ["traitCandidateAvailable"])
                self.assertTrue(validate_parameter_proposal(
                    breath, {"damageType": damage, "area": area}
                )["eligible"])

        # Cross-category sources preserve the approved independent eligible
        # damage-type versus whole-area selection; this is not an earned grant.
        red = contributor("red", "ancestry.dragonborn.red")
        crystal = contributor("crystal", "ancestry.dragonborn.crystal")
        mixed = self.pool(BREATH, red, crystal)
        line = {"shape": "line", "lengthFeet": 30, "widthFeet": 5}
        self.assertTrue(validate_parameter_proposal(
            mixed, {"damageType": "radiant", "area": line}
        )["eligible"])
        self.assertTrue(validate_parameter_proposal(
            mixed, {"damageType": "fire", "area": area}
        )["eligible"])

    def test_multiple_contributors_keep_each_trait_eligibility_separate(self):
        red = contributor("red", "ancestry.dragonborn.red")
        bronze = contributor("bronze", "ancestry.dragonborn.bronze")
        resistance = self.pool(RESISTANCE, red, bronze)
        self.assertEqual(
            {"fire", "lightning"},
            {v["value"] for v in resistance["parameterOptions"]["damageType"]},
        )
        for damage, expected in (("fire", ["red"]), ("lightning", ["bronze"])):
            with self.subTest(damage=damage):
                eligible = validate_parameter_proposal(
                    resistance, {"damageType": damage}
                )
                self.assertTrue(eligible["eligible"])
                self.assertEqual(expected, eligible["configurationContributors"])
        warding = self.pool(WARDING, red, bronze)
        self.assertEqual(
            {"fire"}, {v["value"]
                       for v in warding["parameterOptions"]["damageType"]},
        )
        self.assertFalse(validate_parameter_proposal(
            warding, {"damageType": "lightning"}
        )["eligible"])
        # This test does NOT assert a cross-trait damage-type dependency.

    def test_origin_candidate_pool_sees_all_three_explicit_traits(self):
        red = contributor("red", "ancestry.dragonborn.red")
        p = characteristic_candidate_pool(
            [red], {}, self.blueprints, self.catalog, self.contracts
        )
        by_occurrence = {
            item["identity"]: item
            for item in p["offers"] if item["kind"] == "trait"
        }
        for key in (BREATH, RESISTANCE, WARDING):
            self.assertIn(key, by_occurrence)
            self.assertNotIn(
                key, [item["field"] for item in p["deferredSourceFields"]]
            )
        self.assertEqual(
            {"damageType": "fire"},
            by_occurrence[RESISTANCE]["parameters"],
        )
        self.assertEqual(
            {"damageType": "fire"},
            by_occurrence[WARDING]["parameters"],
        )

    def test_draft_manifestation_keeps_distinct_trait_instances(self):
        red = contributor("red", "ancestry.dragonborn.red")
        bronze = contributor("bronze", "ancestry.dragonborn.bronze")
        profile = draft_manifestation_profile(
            "mixed", [red, bronze], [
                {"kind": "trait", "identity": BREATH,
                 "parameters": {
                     "damageType": "lightning",
                     "area": {"shape": "cone", "lengthFeet": 15},
                 }},
                {"kind": "trait", "identity": RESISTANCE,
                 "parameters": {"damageType": "fire"}},
                {"kind": "trait", "identity": WARDING,
                 "parameters": {"damageType": "fire"}},
            ], {}, self.blueprints, self.catalog, self.contracts,
        )
        self.assertEqual(3, len(profile["manifestedCharacteristics"]))
        self.assertEqual("not-authorized", profile["authority"])
        self.assertEqual(
            ["lightning", "fire", "fire"],
            [item["parameters"]["damageType"]
             for item in profile["manifestedCharacteristics"]],
        )
        # Per-trait parameter binding allows this as a hypothetical candidate;
        # whether this combination is coherent is a separate compatibility
        # and cross-trait dependency decision, NOT established by this test.

    def test_missing_damage_type_does_not_fabricate_trait_parameters(self):
        bad = copy.deepcopy(self.catalog)
        del bad["ancestry.dragonborn.red"]["damageType"]
        red = contributor("red", "ancestry.dragonborn.red")
        for occurrence in (RESISTANCE, WARDING):
            with self.subTest(occurrence=occurrence):
                pool = self.pool(occurrence, red, catalog=bad)
                self.assertFalse(pool["traitCandidateAvailable"])
                self.assertEqual(
                    "lineage-field-not-found",
                    pool["unresolvedContributors"][0]["problems"]["damageType"],
                )

    def test_wrong_type_and_symbolic_mismatch_fail_closed(self):
        wrong = copy.deepcopy(self.catalog)
        wrong["ancestry.dragonborn.red"]["damageType"] = {"wrong": "type"}
        red = contributor("red", "ancestry.dragonborn.red")
        for occurrence in (RESISTANCE, WARDING):
            with self.subTest(occurrence=occurrence):
                pool = self.pool(occurrence, red, catalog=wrong)
                self.assertFalse(pool["traitCandidateAvailable"])
                self.assertEqual(
                    "nearest-field-type-mismatch",
                    pool["unresolvedContributors"][0]["problems"]["damageType"],
                )
        mismatch = copy.deepcopy(self.catalog)
        resistance = next(x for x in mismatch["species.dragonborn"]["traits"]
                          if x["id"] == "draconic-resistance")
        resistance["damageResistance"]["damageTypeFrom"] = "unknown-symbol"
        p = self.pool(RESISTANCE, red, catalog=mismatch)
        self.assertFalse(p["traitCandidateAvailable"])
        self.assertEqual(
            "authored-reference-mismatch",
            p["unresolvedContributors"][0]["problems"]["damageType"],
        )

    def test_unoffered_damage_type_is_rejected_without_materialization(self):
        red = contributor("red", "ancestry.dragonborn.red")
        for occurrence in (RESISTANCE, WARDING):
            with self.subTest(occurrence=occurrence):
                p = self.preview(occurrence, red, damage="lightning")
                self.assertFalse(p["configured"])
                self.assertEqual("linked-configuration-not-offered", p["reason"])
                self.assertNotIn("configuredDefinition", p)

    def test_inputs_not_mutated_by_multi_trait_binding(self):
        catalog = copy.deepcopy(self.catalog)
        blueprints = copy.deepcopy(self.blueprints)
        contracts = copy.deepcopy(self.contracts)
        before = (copy.deepcopy(catalog), copy.deepcopy(blueprints),
                  copy.deepcopy(contracts))
        red = contributor("red", "ancestry.dragonborn.red")
        for occurrence in (RESISTANCE, WARDING):
            a = configure_trait(
                [red], {"damageType": "fire"}, blueprints[occurrence],
                catalog, contracts
            )
            b = configure_trait(
                [red], {"damageType": "fire"}, blueprints[occurrence],
                catalog, contracts
            )
            self.assertEqual(a, b)
        self.assertEqual(before, (catalog, blueprints, contracts))


if __name__ == "__main__":
    unittest.main()
