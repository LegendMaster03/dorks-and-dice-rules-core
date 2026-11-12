"""Source-fidelity checks for the experimental Pillar 2 decomposition inventory.

These do not claim that the rules can yet reconstruct a complete executable
character or that an offspring automatically receives all parent traits.
"""
import copy
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from character_origins_source_inventory import (  # noqa: E402
    SOURCE_METADATA,
    OriginInventoryError,
    read_catalog,
    source_inventory,
)


class OriginCharacteristicInventoryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()

    def test_all_directly_selectable_sources_preserve_every_native_field(self):
        # Includes simple Species, two-stage ancestries, and three-stage
        # Dragonborn definitions. Does not depend on fixed catalog counts.
        self.assertTrue(self.catalog)
        for identifier, record in self.catalog.items():
            if record.get("selectable") is not True:
                continue
            with self.subTest(source=identifier):
                projection = source_inventory(identifier, self.catalog)
                self.assertEqual("not-evaluated", projection["evaluationStatus"])
                self.assertEqual(identifier, projection["selectedSourceId"])
                self.assertEqual(
                    identifier, projection["lineage"][-1]["sourceId"]
                )
                occurrences = []
                for entry in projection["lineage"]:
                    source_id = entry["sourceId"]
                    original = self.catalog[source_id]
                    self.assertEqual(original["kind"], entry["sourceKind"])
                    self.assertEqual(
                        {key: value for key, value in original.items()
                         if key not in SOURCE_METADATA},
                        entry["intrinsicFields"],
                    )
                    self.assertEqual(
                        original.get("traits", []),
                        [trait["definition"] for trait in entry["traits"]],
                    )
                    occurrences.extend(
                        trait["occurrenceId"] for trait in entry["traits"]
                    )
                    for trait in entry["traits"]:
                        self.assertEqual(source_id, trait["sourceId"])
                        self.assertEqual(
                            f"{source_id}#trait:{trait['localTraitId']}",
                            trait["occurrenceId"],
                        )
                self.assertEqual(
                    len(occurrences), len(set(occurrences)),
                    "Distinct source occurrences cannot be flattened by name",
                )

    def test_human_preserves_three_benefits_and_open_choices(self):
        p = source_inventory("species.human", self.catalog)
        self.assertEqual(
            ["species.human"],
            [entry["sourceId"] for entry in p["lineage"]],
        )
        human = p["lineage"][0]
        self.assertEqual(
            ["small", "medium"],
            human["intrinsicFields"]["sizeChoice"]["from"],
        )
        self.assertEqual(
            {"skillful", "versatile", "adaptable-learning"},
            {trait["localTraitId"] for trait in human["traits"]},
        )
        skill = next(
            t["definition"] for t in human["traits"]
            if t["localTraitId"] == "skillful"
        )
        self.assertEqual("skill-proficiency", skill["grantType"])
        self.assertEqual(1, skill["choiceCount"])
        self.assertNotIn("defaultUpbringing", human["intrinsicFields"])

    def test_high_elf_preserves_base_and_ancestry_without_culture(self):
        p = source_inventory("ancestry.elf.high", self.catalog)
        self.assertEqual(
            ["species.elf", "ancestry.elf.high"],
            [entry["sourceId"] for entry in p["lineage"]],
        )
        base, high = p["lineage"]
        self.assertEqual("fey", base["intrinsicFields"]["creatureType"])
        self.assertEqual("medium", base["intrinsicFields"]["size"])
        self.assertEqual(30, base["intrinsicFields"]["walkingSpeedFeet"])
        self.assertEqual(5, len(base["traits"]))
        self.assertEqual(
            {"arcane-cantrip": 1, "detect-magic": 3, "misty-step": 5},
            {t["localTraitId"]: t["definition"]["level"]
             for t in high["traits"]},
        )
        self.assertEqual(
            ["intelligence", "wisdom", "charisma"],
            high["intrinsicFields"]["spellcastingAbilityChoice"],
        )
        self.assertNotIn("defaultUpbringing", high["intrinsicFields"])

    def test_chromatic_dragonborn_retains_parameters_unresolved(self):
        p = source_inventory("ancestry.dragonborn.red", self.catalog)
        self.assertEqual(
            ["species.dragonborn", "ancestry.dragonborn.chromatic",
             "ancestry.dragonborn.red"],
            [entry["sourceId"] for entry in p["lineage"]],
        )
        base, family, leaf = p["lineage"]
        self.assertEqual("dragon", base["intrinsicFields"]["creatureType"])
        breath = next(
            t["definition"] for t in base["traits"]
            if t["localTraitId"] == "breath-weapon"
        )
        self.assertEqual("selected-dragonborn-leaf", breath["damage"]["typeFrom"])
        self.assertEqual(
            "selected-dragonborn-family-or-leaf", breath["areaFrom"]
        )
        self.assertEqual(
            {"shape": "line", "lengthFeet": 30, "widthFeet": 5},
            family["intrinsicFields"]["breathWeaponArea"],
        )
        self.assertEqual("fire", leaf["intrinsicFields"]["damageType"])
        self.assertEqual(15, leaf["intrinsicFields"]["climbingSpeedFeet"])
        self.assertEqual(
            15, leaf["traits"][0]["definition"]["speedFeet"]
        )
        self.assertEqual(5, family["traits"][0]["definition"]["requiredCharacterLevel"])

    def test_metallic_dragonborn_preserves_separate_breath_resource(self):
        p = source_inventory("ancestry.dragonborn.gold", self.catalog)
        self.assertEqual(
            ["species.dragonborn", "ancestry.dragonborn.metallic",
             "ancestry.dragonborn.gold"],
            [entry["sourceId"] for entry in p["lineage"]],
        )
        family = p["lineage"][1]
        self.assertEqual("cone", family["intrinsicFields"]["breathWeaponArea"]["shape"])
        breath = family["traits"][0]["definition"]
        self.assertEqual("breath-weapon", breath["uses"]["independentOf"])
        self.assertEqual(2, len(breath["options"]))
        self.assertEqual("amphibious", p["lineage"][2]["traits"][0]["localTraitId"])

    def test_gem_leaf_inventory_reuses_shared_family_traits(self):
        for slug, damage in (
            ("amethyst", "force"), ("crystal", "radiant"),
            ("emerald", "psychic"), ("sapphire", "thunder"),
            ("topaz", "necrotic"),
        ):
            with self.subTest(slug=slug):
                sid = "ancestry.dragonborn." + slug
                p = source_inventory(sid, self.catalog)
                self.assertEqual([
                    "species.dragonborn", "ancestry.dragonborn.gem", sid],
                    [x["sourceId"] for x in p["lineage"]],
                )
                base, family, leaf = p["lineage"]
                self.assertEqual("dragon", base["intrinsicFields"]["creatureType"])
                self.assertEqual({"shape": "cone", "lengthFeet": 15},
                                 family["intrinsicFields"]["breathWeaponArea"])
                self.assertEqual(
                    {"psionic-mind", "gem-flight"},
                    {t["localTraitId"] for t in family["traits"]},
                )
                self.assertEqual(damage, leaf["intrinsicFields"]["damageType"])
                self.assertEqual([], leaf["traits"])

    def test_qualified_occurrences_preserve_same_named_different_traits(self):
        # Orc and Goliath each author Powerful Build, but those effects differ.
        orc = source_inventory("species.orc", self.catalog)
        goliath = source_inventory("ancestry.goliath.cloud", self.catalog)
        orc_trait = next(
            x for x in orc["lineage"][0]["traits"]
            if x["localTraitId"] == "powerful-build"
        )
        goliath_trait = next(
            x for x in goliath["lineage"][0]["traits"]
            if x["localTraitId"] == "powerful-build"
        )
        self.assertNotEqual(orc_trait["occurrenceId"], goliath_trait["occurrenceId"])
        self.assertNotEqual(orc_trait["definition"], goliath_trait["definition"])

    def test_projection_is_deterministic_and_does_not_mutate_catalog(self):
        before = copy.deepcopy(self.catalog)
        first = source_inventory("ancestry.elf.high", self.catalog)
        self.assertEqual(
            first, source_inventory("ancestry.elf.high", self.catalog)
        )
        first["lineage"][0]["traits"][0]["definition"]["id"] = "modified"
        first["lineage"][0]["intrinsicFields"]["walkingSpeedFeet"] = 999
        self.assertEqual(before, self.catalog)

    def test_invalid_direct_selection_is_rejected(self):
        for identifier in (
            "species.dragonborn",
            "ancestry.dragonborn.chromatic",
            "ancestry.dragonborn.gem",
        ):
            with self.subTest(source=identifier):
                with self.assertRaises(OriginInventoryError):
                    source_inventory(identifier, self.catalog)
        with self.assertRaises(OriginInventoryError):
            source_inventory("species.nonexistent", self.catalog)

    def test_missing_parent_and_cycle_are_rejected(self):
        missing = copy.deepcopy(self.catalog)
        missing["ancestry.elf.high"]["parent"] = "species.missing"
        with self.assertRaisesRegex(OriginInventoryError, "Missing parent"):
            source_inventory("ancestry.elf.high", missing)
        cyclic = copy.deepcopy(self.catalog)
        cyclic["ancestry.elf.high"]["parent"] = "ancestry.elf.high"
        with self.assertRaisesRegex(OriginInventoryError, "Cycle"):
            source_inventory("ancestry.elf.high", cyclic)


if __name__ == "__main__":
    unittest.main()
