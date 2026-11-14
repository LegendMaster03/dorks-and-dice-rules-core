"""Tests for read-only, source-independent Pillar 2 characteristic facets.

Facet classification is a proposal; it does not prove mechanical equivalence,
choose character options, resolve numeric stacking, or authorize any grant.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_source_inventory import read_catalog  # noqa: E402
from character_origins_characteristic_facets import (  # noqa: E402
    INTRINSIC_FACETS,
    TRAIT_FACETS,
    analyze_shared_trait_ids,
    project_facets,
)


class OriginCharacteristicFacetTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()

    def test_every_selectable_definition_preserves_all_source_fields(self):
        # Assert no mechanical source field can disappear from a facet pass.
        for selected_id, source in self.catalog.items():
            if source.get("selectable") is not True:
                continue
            with self.subTest(selected=selected_id):
                p = project_facets(selected_id, self.catalog)
                self.assertEqual("not-evaluated", p["evaluationStatus"])
                self.assertEqual(selected_id, p["selectedSourceId"])
                self.assertEqual(selected_id, p["sources"][-1]["sourceId"])
                for entry in p["sources"]:
                    record = self.catalog[entry["sourceId"]]
                    expected_top = set(record) - {
                        "id", "name", "kind", "status", "selectable",
                        "parent", "reviewNotes", "defaultUpbringing",
                        "ancestryTheme", "traits",
                    }
                    self.assertEqual(
                        expected_top,
                        {facet["nativeField"]
                         for facet in entry["intrinsicFacets"]},
                    )
                    self.assertEqual(
                        len(record.get("traits", [])), len(entry["traits"])
                    )
                    for definition, candidate in zip(
                        record.get("traits", []), entry["traits"]
                    ):
                        expected = set(definition) - {
                            "id", "name", "description"
                        }
                        self.assertEqual(
                            expected,
                            {facet["nativeField"]
                             for facet in candidate["candidateFacets"]},
                        )
                        for facet in candidate["candidateFacets"]:
                            self.assertEqual(
                                definition[facet["nativeField"]],
                                facet["authoredValue"],
                            )

    def test_known_mechanics_map_to_consistent_candidate_facets(self):
        self.assertEqual(
            "carrying.effective-size-categories",
            TRAIT_FACETS["effectiveSizeCategoriesHigherForCarryingCapacity"],
        )
        self.assertEqual(
            "defense.damage-resistance", TRAIT_FACETS["damageResistance"]
        )
        self.assertEqual("creature.type", INTRINSIC_FACETS["creatureType"])
        self.assertEqual(
            "movement.walk.speed", INTRINSIC_FACETS["walkingSpeedFeet"]
        )

        orc = project_facets("species.orc", self.catalog)
        giant = project_facets("ancestry.goliath.cloud", self.catalog)
        def powerful(projection):
            return next(
                trait for source in projection["sources"]
                for trait in source["traits"]
                if trait["localTraitId"] == "powerful-build"
            )
        a, b = powerful(orc), powerful(giant)
        self.assertNotEqual(a["sourceOccurrenceId"], b["sourceOccurrenceId"])
        carrying = "effectiveSizeCategoriesHigherForCarryingCapacity"
        a_part = next(f for f in a["candidateFacets"]
                      if f["nativeField"] == carrying)
        b_part = next(f for f in b["candidateFacets"]
                      if f["nativeField"] == carrying)
        self.assertEqual(a_part["candidateSemanticId"],
                         b_part["candidateSemanticId"])
        self.assertEqual(1, a_part["authoredValue"])
        self.assertEqual(1, b_part["authoredValue"])
        # A shared component does not make the complete grants equivalent.
        self.assertNotEqual(
            {f["nativeField"] for f in a["candidateFacets"]},
            {f["nativeField"] for f in b["candidateFacets"]},
        )

    def test_dragonborn_parameters_remain_symbolic(self):
        red = project_facets("ancestry.dragonborn.red", self.catalog)
        self.assertEqual(
            ["species.dragonborn", "ancestry.dragonborn.chromatic",
             "ancestry.dragonborn.red"],
            [entry["sourceId"] for entry in red["sources"]],
        )
        refs = {r["authoredReference"] for r in red["symbolicBindings"]}
        self.assertIn("selected-dragonborn-leaf", refs)
        self.assertIn("selected-dragonborn-family-or-leaf", refs)
        for ref in red["symbolicBindings"]:
            self.assertEqual("unbound", ref["resolution"])
        fields = {
            f["nativeField"]: f for f in red["sources"][-1]["intrinsicFacets"]
        }
        self.assertEqual("fire", fields["damageType"]["authoredValue"])
        self.assertEqual(
            "parameter.damage-type", fields["damageType"]["candidateSemanticId"]
        )
        self.assertEqual(
            "mirrored-movement-same-value",
            red["diagnostics"][0]["kind"],
        )
        self.assertEqual(15, red["diagnostics"][0]["traitValue"])

        gold = project_facets("ancestry.dragonborn.gold", self.catalog)
        self.assertFalse(gold["diagnostics"])
        self.assertEqual(3, len(gold["sources"]))

    def test_normalized_sleep_and_prose_only_traits_are_audited(self):
        elf = project_facets("ancestry.elf.high", self.catalog)
        traits = [t for s in elf["sources"] for t in s["traits"]]
        magical = next(t for t in traits
                       if t["localTraitId"] == "magical-sleep")
        self.assertEqual([], magical["reviewRequired"])
        normalized = next(
            f for f in magical["candidateFacets"]
            if f["nativeField"] == "magicalSleep"
        )
        self.assertEqual(
            {"immune": True, "mayConsentToSpecificEffect": True},
            normalized["authoredValue"],
        )
        keen = next(t for t in traits if t["localTraitId"] == "keen-senses")
        self.assertEqual([], keen["reviewRequired"])
        high_spell = next(
            t for t in traits if t["localTraitId"] == "misty-step"
        )
        self.assertEqual("ability.level-as-authored",
                         next(f for f in high_spell["candidateFacets"]
                              if f["nativeField"] == "level")["candidateSemanticId"])

    def test_local_trait_id_collision_does_not_merge_grants(self):
        collisions = {
            finding["localTraitId"]: finding
            for finding in analyze_shared_trait_ids(self.catalog)
        }
        self.assertIn("powerful-build", collisions)
        self.assertFalse(
            collisions["powerful-build"]["mechanicalFieldsIdentical"]
        )
        self.assertIn("amphibious", collisions)
        self.assertTrue(
            collisions["amphibious"]["mechanicalFieldsIdentical"]
        )
        self.assertIn("darkness", collisions)
        self.assertFalse(
            collisions["darkness"]["mechanicalFieldsIdentical"]
        )
        self.assertGreaterEqual(
            len(collisions["powerful-build"]["occurrenceIds"]), 2
        )

    def test_unknown_fields_are_explicitly_unmapped_not_discarded(self):
        changed = copy.deepcopy(self.catalog)
        changed["species.human"]["futureMechanicalProperty"] = {"value": 42}
        changed["species.human"]["traits"][0]["futureGrantMode"] = "custom"
        p = project_facets("species.human", changed)
        f = next(f for f in p["sources"][0]["intrinsicFacets"]
                 if f["nativeField"] == "futureMechanicalProperty")
        self.assertEqual("unmapped", f["classification"])
        self.assertIsNone(f["candidateSemanticId"])
        self.assertEqual({"value": 42}, f["authoredValue"])
        t = next(f for f in p["sources"][0]["traits"][0]["candidateFacets"]
                 if f["nativeField"] == "futureGrantMode")
        self.assertEqual("unmapped", t["classification"])
        self.assertEqual("custom", t["authoredValue"])

    def test_projection_is_deterministic_and_does_not_mutate_sources(self):
        original = copy.deepcopy(self.catalog)
        a = project_facets("ancestry.elf.high", self.catalog)
        b = project_facets("ancestry.elf.high", self.catalog)
        self.assertEqual(a, b)
        a["sources"][0]["traits"][0]["candidateFacets"][0]["authoredValue"] = "wrong"
        self.assertEqual(original, self.catalog)


if __name__ == "__main__":
    unittest.main()
