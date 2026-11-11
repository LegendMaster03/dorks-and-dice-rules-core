"""Pillar 2 multiple trait/intrinsic candidate and profile projection tests.

No balance, inheritance authorization, attack execution, or resolution of
conflicting traits is implied by a passing profile-consistency test.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_manifestation_inventory import (  # noqa: E402
    ParameterInheritanceError,
    characteristic_candidate_pool,
    draft_manifestation_profile,
)
from character_origins_parameter_inheritance import read_blueprint  # noqa: E402
from character_origins_source_inventory import (  # noqa: E402
    read_catalog, source_inventory,
)


LINE = {"shape": "line", "lengthFeet": 30, "widthFeet": 5}
CONE = {"shape": "cone", "lengthFeet": 15}


def direct(key, selected):
    return {"contributorId": key, "selectedSourceId": selected}


def nested(key, profile_id):
    return {"contributorId": key, "profileId": profile_id}


def intrinsic(dimension, value):
    return {"kind": "intrinsic", "identity": dimension, "value": value}


def trait(occurrence, parameters=None):
    return {"kind": "trait", "identity": occurrence,
            "parameters": {} if parameters is None else parameters}


class Pillar2MultiManifestationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        blueprint = read_blueprint()
        cls.blueprints = {blueprint["definitionOccurrenceId"]: blueprint}

    def pool(self, sources, registry=None, catalog=None, blueprints=None):
        return characteristic_candidate_pool(
            sources, {} if registry is None else registry,
            self.blueprints if blueprints is None else blueprints,
            self.catalog if catalog is None else catalog,
            self.contracts,
        )

    def draft(self, name, sources, choices, registry=None, catalog=None,
              blueprints=None):
        return draft_manifestation_profile(
            name, sources, choices,
            {} if registry is None else registry,
            self.blueprints if blueprints is None else blueprints,
            self.catalog if catalog is None else catalog,
            self.contracts,
        )

    def test_every_selectable_source_trait_and_intrinsic_field_is_accounted_for(self):
        for source_id, record in self.catalog.items():
            if record.get("selectable") is not True:
                continue
            with self.subTest(source=source_id):
                native = source_inventory(source_id, self.catalog)
                p = self.pool([direct("one", source_id)])
                catalog_traits = {
                    t["occurrenceId"] for s in native["lineage"]
                    for t in s["traits"]
                }
                offered_traits = {
                    o["identity"] for o in p["offers"]
                    if o["kind"] == "trait"
                }
                deferred_traits = {
                    x["field"] for x in p["deferredSourceFields"]
                    if "#trait:" in x["field"]
                }
                self.assertEqual(catalog_traits,
                                 offered_traits | deferred_traits)
                fields = {
                    (s["sourceId"], k) for s in native["lineage"]
                    for k in s["intrinsicFields"]
                }
                offered_fields = {
                    (o["sourceId"], o["authoredField"])
                    for o in p["offers"]
                    if o["kind"] == "intrinsic"
                }
                deferred_fields = {
                    (o["sourceId"], o["field"])
                    for o in p["deferredSourceFields"]
                    if "#trait:" not in o["field"]
                }
                self.assertEqual(fields, offered_fields | deferred_fields)

    def test_human_has_choice_of_size_and_three_distinct_traits(self):
        p = self.pool([direct("human", "species.human")])
        sizes = [o["value"] for o in p["offers"]
                 if o["kind"] == "intrinsic"
                 and o["identity"] == "creature.size"]
        self.assertEqual(["small", "medium"], sizes)
        self.assertEqual(
            {"species.human#trait:skillful",
             "species.human#trait:versatile",
             "species.human#trait:adaptable-learning"},
            {o["identity"] for o in p["offers"]
             if o["kind"] == "trait"},
        )
        self.assertFalse(
            any(o["identity"].startswith("upbringing.")
                for o in p["offers"])
        )
        self.assertEqual([], p["reviewFlags"])

    def test_complete_human_origin_manifestation_preserves_choices_and_grants(self):
        selections = [
            intrinsic("creature.type", "humanoid"),
            intrinsic("creature.size", "medium"),
            intrinsic("movement.walk.speed", 30),
            trait("species.human#trait:skillful"),
            trait("species.human#trait:versatile"),
            trait("species.human#trait:adaptable-learning"),
        ]
        profile = self.draft(
            "human-profile", [direct("one", "species.human")], selections,
        )
        self.assertEqual("not-authorized", profile["authority"])
        self.assertEqual(6, len(profile["manifestedCharacteristics"]))
        self.assertEqual(selections, profile["selections"])
        self.assertEqual([], profile["reviewFlags"])
        self.assertEqual(
            ["one"],
            [e["contributorId"] for e in
             profile["manifestedCharacteristics"][0]["contributedBy"]],
        )
        # Choice outcomes inside Human Skillful/Versatile are NOT guessed.
        for entry in profile["manifestedCharacteristics"][3:]:
            self.assertEqual({}, entry["parameters"])

    def test_two_matching_standard_humans_preserve_source_occurrences(self):
        selections = [
            intrinsic("creature.type", "humanoid"),
            intrinsic("creature.size", "small"),
            trait("species.human#trait:skillful"),
        ]
        p = self.draft(
            "human-child", [
                direct("one", "species.human"),
                direct("two", "species.human"),
            ], selections,
        )
        self.assertEqual(3, len(p["manifestedCharacteristics"]))
        for entry in p["manifestedCharacteristics"]:
            self.assertEqual(
                ["one", "two"],
                [x["contributorId"] for x in entry["contributedBy"]],
            )

    def test_wood_elf_speed_override_is_flagged_not_assumed(self):
        p = self.pool([direct("wood", "ancestry.elf.wood")])
        speed_offers = [
            x for x in p["offers"] if x["kind"] == "intrinsic"
            and x["identity"] == "movement.walk.speed"
        ]
        self.assertEqual([30, 35], [x["value"] for x in speed_offers])
        self.assertEqual(
            "multiple-source-values-needs-origin-policy",
            next(flag["kind"] for flag in p["reviewFlags"]
                 if flag["identity"] == "movement.walk.speed"),
        )
        # Explicitly selecting 35 is a design hypothesis, not an
        # automatically approved replacement rule.
        profile = self.draft("wood", [
            direct("wood", "ancestry.elf.wood")
        ], [intrinsic("movement.walk.speed", 35)])
        self.assertEqual(35, profile["manifestedCharacteristics"][0]["value"])
        self.assertTrue(profile["reviewFlags"])

    def test_drow_darkvision_and_low_light_vision_are_distinct(self):
        p = self.pool([direct("drow", "ancestry.elf.drow")])
        self.assertIn(
            120, [x["value"] for x in p["offers"]
                  if x["kind"] == "intrinsic"
                  and x["identity"] == "sense.darkvision.range"],
        )
        self.assertIn(
            "species.elf#trait:low-light-vision",
            {x["identity"] for x in p["offers"]
             if x["kind"] == "trait"},
        )
        self.assertIn(
            "senses-interaction-needs-review",
            {flag["kind"] for flag in p["reviewFlags"]},
        )

    def test_identical_local_trait_names_across_species_do_not_merge(self):
        p = self.pool([
            direct("orc", "species.orc"),
            direct("goliath", "species.goliath"),
        ])
        both = [x["identity"] for x in p["offers"]
                if x["kind"] == "trait"
                and x["identity"].endswith("#trait:powerful-build")]
        self.assertEqual(
            {"species.orc#trait:powerful-build",
             "species.goliath#trait:powerful-build"},
            set(both),
        )

    def test_prose_only_traits_remain_manifestable_for_review(self):
        p = self.draft("halfling", [direct("one", "species.halfling")], [
            trait("species.halfling#trait:halfling-luck"),
        ])
        self.assertEqual(
            ["prose-only-effect"],
            p["manifestedCharacteristics"][0]["reviewRequired"],
        )

    def test_two_generation_profile_exports_only_the_manifested_subset(self):
        first = self.draft("elf-parent", [
            direct("elf", "species.elf"),
        ], [
            intrinsic("creature.type", "fey"),
            intrinsic("movement.walk.speed", 30),
            trait("species.elf#trait:trance"),
        ])
        second_pool = self.pool([
            nested("parent", "elf-parent"),
            direct("human", "species.human"),
        ], {"elf-parent": first})
        self.assertNotIn(
            "species.elf#trait:fey-ancestry",
            {x["identity"] for x in second_pool["offers"]},
        )
        self.assertNotIn(
            "species.elf#trait:magical-sleep",
            {x["identity"] for x in second_pool["offers"]},
        )
        self.assertIn(
            "species.elf#trait:trance",
            {x["identity"] for x in second_pool["offers"]},
        )
        self.assertIn(
            "species.human#trait:skillful",
            {x["identity"] for x in second_pool["offers"]},
        )
        self.assertIn(
            "creature.type",
            {x["identity"] for x in second_pool["offers"]},
        )
        self.assertIn(
            "multiple-source-values-needs-origin-policy",
            {x["kind"] for x in second_pool["reviewFlags"]},
        )

    def test_two_generation_configurable_breath_preserves_only_chosen_values(self):
        first = self.draft("mixed-parent", [
            direct("red", "ancestry.dragonborn.red"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        ], [
            intrinsic("creature.type", "dragon"),
            trait("species.dragonborn#trait:breath-weapon", {
                "damageType": "fire", "area": CONE,
            }),
        ])
        second_sources = [
            nested("parent", "mixed-parent"),
            direct("human", "species.human"),
        ]
        registry = {"mixed-parent": first}
        eligible = self.pool(second_sources, registry)
        breath = next(x for x in eligible["offers"]
                      if x["identity"] ==
                      "species.dragonborn#trait:breath-weapon")
        self.assertEqual(
            {"damageType": "fire", "area": CONE},
            breath["parameters"],
        )
        with self.assertRaisesRegex(
            ParameterInheritanceError, "Unoffered trait parameter"
        ):
            self.draft("child", second_sources, [
                trait("species.dragonborn#trait:breath-weapon", {
                    "damageType": "lightning", "area": CONE,
                }),
            ], registry)
        child = self.draft("child", second_sources, [
            trait("species.dragonborn#trait:breath-weapon", {
                "damageType": "fire", "area": CONE,
            }),
            trait("species.human#trait:skillful"),
            intrinsic("creature.type", "humanoid"),
        ], registry)
        self.assertEqual(3, len(child["manifestedCharacteristics"]))
        inherited = child["manifestedCharacteristics"][0]
        self.assertEqual(
            "manifested-profile",
            inherited["parameterEvidence"]["damageType"][0]["kind"],
        )
        self.assertEqual(
            "mixed-parent",
            inherited["parameterEvidence"]["damageType"][0]["profileId"],
        )
        self.assertEqual(
            "multiple-source-values-needs-origin-policy",
            next(x["kind"] for x in child["reviewFlags"]
                 if x["identity"] == "creature.type"),
        )

    def test_nested_profile_ensures_own_provenance_was_not_altered(self):
        first = self.draft("parent", [
            direct("red", "ancestry.dragonborn.red"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        ], [
            trait("species.dragonborn#trait:breath-weapon", {
                "damageType": "fire", "area": CONE,
            }),
        ])
        changed = copy.deepcopy(first)
        changed["manifestedCharacteristics"][0]["parameters"]["damageType"] = (
            "lightning"
        )
        with self.assertRaisesRegex(
            ParameterInheritanceError, "unverified manifestation"
        ):
            self.pool([nested("one", "parent")], {"parent": changed})

    def test_intrinsic_unknown_fields_remain_visible_without_auto_grants(self):
        altered = copy.deepcopy(self.catalog)
        altered["species.human"]["futureTraitAttribute"] = {
            "unrecognized": "effect"
        }
        p = self.pool([direct("human", "species.human")], catalog=altered)
        x = next(i for i in p["deferredSourceFields"]
                 if i["field"] == "futureTraitAttribute")
        self.assertEqual("unclassified-intrinsic", x["classification"])
        self.assertEqual(
            {"unrecognized": "effect"}, x["authoredValue"],
        )
        with self.assertRaisesRegex(
            ParameterInheritanceError, "Unoffered intrinsic"
        ):
            self.draft("x", [direct("human", "species.human")], [
                intrinsic("futureTraitAttribute", "effect"),
            ], catalog=altered)

    def test_trait_with_unmodeled_parameters_is_deferred(self):
        p = self.pool([
            direct("red", "ancestry.dragonborn.red")
        ])
        deferred = {x["field"]: x for x in p["deferredSourceFields"]}
        self.assertIn(
            "species.dragonborn#trait:draconic-resistance", deferred,
        )
        self.assertEqual(
            "trait-parameters-not-modeled",
            deferred["species.dragonborn#trait:draconic-resistance"][
                "classification"],
        )
        with self.assertRaisesRegex(
            ParameterInheritanceError, "Unoffered trait"
        ):
            self.draft("red", [
                direct("red", "ancestry.dragonborn.red")
            ], [trait("species.dragonborn#trait:draconic-resistance")])

    def test_no_trait_auto_manifests_and_duplicate_identity_is_rejected(self):
        p = self.draft("empty", [
            direct("one", "ancestry.elf.high")
        ], [])
        self.assertEqual([], p["manifestedCharacteristics"])
        with self.assertRaisesRegex(
            ParameterInheritanceError, "Duplicated"
        ):
            self.draft("bad", [
                direct("one", "species.human")
            ], [
                trait("species.human#trait:skillful"),
                trait("species.human#trait:skillful"),
            ])

    def test_nonbiological_origination_and_missing_profile_rejection(self):
        p = self.draft("construct", [], [])
        self.assertEqual([], p["manifestedCharacteristics"])
        with self.assertRaises(ParameterInheritanceError):
            self.pool([nested("ghost", "missing")], {})
        # Origination remains distinct from selected Heritage sources.
        self.assertIsNone(p["origination"])

    def test_source_and_profile_cycles_are_rejected(self):
        first = self.draft("parent", [
            direct("human", "species.human")
        ], [trait("species.human#trait:skillful")])
        changed = copy.deepcopy(first)
        changed["contributors"] = [nested("recursive", "parent")]
        with self.assertRaisesRegex(
            ParameterInheritanceError, "Cyclic profile"
        ):
            self.pool([nested("parent", "parent")], {"parent": changed})

    def test_manifestation_is_deterministic_and_input_is_not_modified(self):
        catalog = copy.deepcopy(self.catalog)
        contributors = [
            direct("elf", "species.elf"),
            direct("wood", "ancestry.elf.wood"),
        ]
        selections = [
            intrinsic("movement.walk.speed", 35),
            trait("species.elf#trait:trance"),
        ]
        before_sources = copy.deepcopy(contributors)
        before_choices = copy.deepcopy(selections)
        a = self.draft("child", contributors, selections, catalog=catalog)
        b = self.draft("child", contributors, selections, catalog=catalog)
        self.assertEqual(a, b)
        a["manifestedCharacteristics"][0]["value"] = 999
        self.assertEqual(before_sources, contributors)
        self.assertEqual(before_choices, selections)
        self.assertEqual(self.catalog, catalog)


if __name__ == "__main__":
    unittest.main()
