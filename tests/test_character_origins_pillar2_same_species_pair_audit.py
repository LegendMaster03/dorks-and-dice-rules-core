"""Systematic within-Species Ancestry review coverage, not permission rules."""
import copy
import itertools
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_parameter_inheritance import read_blueprints  # noqa: E402
from character_origins_same_species_pair_audit import (  # noqa: E402
    audit_same_species_pairs,
)
from character_origins_source_inventory import (  # noqa: E402
    read_catalog, source_inventory,
)


class SameSpeciesPairAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        cls.blueprints = read_blueprints()

    def audit(self, catalog=None):
        return audit_same_species_pairs(
            self.catalog if catalog is None else catalog,
            self.contracts, self.blueprints
        )

    def test_all_selectable_sources_covered_without_frozen_counts(self):
        report = self.audit()
        grouped = {}
        for sid in sorted(self.catalog):
            if self.catalog[sid].get("selectable") is True:
                root = source_inventory(sid, self.catalog)["lineage"][0][
                    "sourceId"
                ]
                grouped.setdefault(root, []).append(sid)
        self.assertEqual(
            set(grouped),
            {s["rootSpeciesSourceId"] for s in report["species"]},
        )
        for species in report["species"]:
            with self.subTest(species=species["rootSpeciesSourceId"]):
                selectable = grouped[species["rootSpeciesSourceId"]]
                self.assertEqual(selectable, species["selectableSourceIds"])
                pairs = list(itertools.combinations_with_replacement(
                    selectable, 2
                ))
                self.assertEqual(
                    pairs, [(p["firstSourceId"], p["secondSourceId"])
                            for p in species["pairs"]],
                )
                self.assertEqual(len(pairs),
                                 species["sameSpeciesPairCount"])
                self.assertEqual("not-yet-authored",
                                 species["compatibilityPolicy"])
        self.assertEqual(
            sum(s["sameSpeciesPairCount"] for s in report["species"]),
            report["sameSpeciesPairCount"],
        )
        self.assertEqual(0, report["crossSpeciesPairsEnumerated"])
        self.assertEqual("not-authorized", report["authority"])

    def test_dragonborn_red_bronze_pair_has_common_source_not_final_verdict(self):
        report = self.audit()
        species = next(s for s in report["species"]
                       if s["rootSpeciesSourceId"] == "species.dragonborn")
        pair = next(p for p in species["pairs"]
                    if {p["firstSourceId"], p["secondSourceId"]} == {
                        "ancestry.dragonborn.red",
                        "ancestry.dragonborn.bronze"
                    })
        self.assertEqual(["species.dragonborn"],
                         pair["sharedSourceNodeIds"])
        self.assertFalse(pair["sameSelectedSource"])
        self.assertEqual("not-evaluated",
                         pair["defaultCompatibility"])
        self.assertEqual("not-evaluated", pair["balance"])
        # The absence of a detected generic conflict can not be
        # interpreted as approval for family/skin trait combinations.

    def test_identical_sources_still_count_as_a_valid_review_case(self):
        report = self.audit()
        species = next(s for s in report["species"]
                       if s["rootSpeciesSourceId"] == "species.dragonborn")
        pair = next(p for p in species["pairs"]
                    if p["firstSourceId"] == "ancestry.dragonborn.red"
                    and p["secondSourceId"] == "ancestry.dragonborn.red")
        self.assertTrue(pair["sameSelectedSource"])
        self.assertEqual(
            ["species.dragonborn", "ancestry.dragonborn.chromatic",
             "ancestry.dragonborn.red"],
            pair["sharedSourceNodeIds"],
        )

    def test_existing_wood_elf_conflict_not_misclassified_as_pair_caused(self):
        report = self.audit()
        species = next(s for s in report["species"]
                       if s["rootSpeciesSourceId"] == "species.elf")
        pair = next(p for p in species["pairs"]
                    if {p["firstSourceId"], p["secondSourceId"]} == {
                        "ancestry.elf.high", "ancestry.elf.wood"
                    })
        self.assertIn(
            "movement.walk.speed",
            {f["identity"] for f in pair["sourceReviewFlags"]
             if f["kind"] == "multiple-source-values-needs-origin-policy"},
        )
        self.assertNotIn(
            "movement.walk.speed",
            {f["identity"] for f in pair["newPairReviewFlags"]},
        )
        self.assertEqual("not-evaluated",
                         pair["defaultCompatibility"])

    def test_synthetic_new_family_and_leaf_automatically_join_matrix(self):
        altered = copy.deepcopy(self.catalog)
        altered["ancestry.dragonborn.prismatic"] = {
            "id": "ancestry.dragonborn.prismatic",
            "kind": "ancestry", "selectable": False,
            "parent": "species.dragonborn",
            "breathWeaponArea": {"shape": "cone", "lengthFeet": 20},
            "traits": []
        }
        altered["ancestry.dragonborn.prismatic.ruby"] = {
            "id": "ancestry.dragonborn.prismatic.ruby",
            "kind": "ancestry", "selectable": True,
            "parent": "ancestry.dragonborn.prismatic",
            "damageType": "fire", "skinMaterial": "ruby-crystal",
            "traits": [],
        }
        before = self.audit()
        after = self.audit(altered)
        prior = next(s for s in before["species"]
                     if s["rootSpeciesSourceId"] == "species.dragonborn")
        later = next(s for s in after["species"]
                     if s["rootSpeciesSourceId"] == "species.dragonborn")
        self.assertEqual(
            prior["sameSpeciesPairCount"] +
            len(prior["selectableSourceIds"]) + 1,
            later["sameSpeciesPairCount"],
        )
        self.assertTrue(any(
            p["firstSourceId"] == "ancestry.dragonborn.prismatic.ruby"
            or p["secondSourceId"] == "ancestry.dragonborn.prismatic.ruby"
            for p in later["pairs"]
        ))
        self.assertTrue(all(
            p["defaultCompatibility"] == "not-evaluated"
            for p in later["pairs"]
        ))
        self.assertEqual(0, after["crossSpeciesPairsEnumerated"])

    def test_genuine_cross_contributor_intrinsic_conflict_detected(self):
        altered = copy.deepcopy(self.catalog)
        # A synthetic pair of different leaves under one foundation
        # has incompatible walking-speed offers, absent from either
        # singleton's conflict report.
        altered["ancestry.halfling.fleet"] = {
            "id": "ancestry.halfling.fleet",
            "kind": "ancestry", "selectable": True,
            "parent": "species.halfling",
            "climbingSpeedFeet": 20,
            "traits": [],
        }
        altered["ancestry.halfling.slow"] = {
            "id": "ancestry.halfling.slow",
            "kind": "ancestry", "selectable": True,
            "parent": "species.halfling",
            "climbingSpeedFeet": 10,
            "traits": [],
        }
        report = self.audit(altered)
        species = next(s for s in report["species"]
                       if s["rootSpeciesSourceId"] == "species.halfling")
        pair = next(p for p in species["pairs"]
                    if {p["firstSourceId"], p["secondSourceId"]} == {
                        "ancestry.halfling.fleet",
                        "ancestry.halfling.slow"
                    })
        self.assertIn(
            "movement.climb.speed",
            {f["identity"] for f in pair["newPairReviewFlags"]},
        )
        self.assertEqual("not-evaluated",
                         pair["defaultCompatibility"])

    def test_species_names_not_hardcoded_to_an_approved_pair_list(self):
        altered = copy.deepcopy(self.catalog)
        altered["species.homebrew"] = {
            "id": "species.homebrew", "kind": "species",
            "selectable": False, "traits": []
        }
        altered["ancestry.homebrew.alpha"] = {
            "id": "ancestry.homebrew.alpha",
            "kind": "ancestry", "selectable": True,
            "parent": "species.homebrew", "traits": [],
        }
        altered["ancestry.homebrew.beta"] = {
            "id": "ancestry.homebrew.beta",
            "kind": "ancestry", "selectable": True,
            "parent": "species.homebrew", "traits": [],
        }
        report = self.audit(altered)
        custom = next(s for s in report["species"]
                      if s["rootSpeciesSourceId"] == "species.homebrew")
        self.assertEqual(3, custom["sameSpeciesPairCount"])
        self.assertEqual(0, report["crossSpeciesPairsEnumerated"])

    def test_no_fabricated_balance_or_runtime_authorization(self):
        report = self.audit()
        for species in report["species"]:
            for pair in species["pairs"]:
                self.assertEqual("not-evaluated", pair["balance"])
                self.assertEqual("not-evaluated",
                                 pair["defaultCompatibility"])
                self.assertNotIn("grantedTraits", pair)
                self.assertNotIn("approvedExceptions", pair)

    def test_report_is_deterministic_and_inputs_not_mutated(self):
        catalog = copy.deepcopy(self.catalog)
        blueprints = copy.deepcopy(self.blueprints)
        contracts = copy.deepcopy(self.contracts)
        before = (copy.deepcopy(catalog), copy.deepcopy(blueprints),
                  copy.deepcopy(contracts))
        a = audit_same_species_pairs(catalog, contracts, blueprints)
        b = audit_same_species_pairs(catalog, contracts, blueprints)
        self.assertEqual(a, b)
        a["species"][0]["pairs"].clear()
        self.assertEqual(before, (catalog, blueprints, contracts))


if __name__ == "__main__":
    unittest.main()
