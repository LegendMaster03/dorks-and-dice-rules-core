"""Shared-Species foundation evidence without automatic ancestry grants.

The direct-source audit must preserve every contributor's source provenance,
separate configured trait candidates, and all unresolved source fields. It
must not infer mixed-Species compatibility, balance or acquisition.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_manifestation_inventory import (  # noqa: E402
    characteristic_candidate_pool, draft_manifestation_profile,
)
from character_origins_parameter_inheritance import (  # noqa: E402
    ParameterInheritanceError, read_blueprints,
)
from character_origins_shared_foundations import (  # noqa: E402
    audit_direct_foundations,
)
from character_origins_source_inventory import (  # noqa: E402
    read_catalog, source_inventory,
)


def contributor(identity, selected):
    return {"contributorId": identity, "selectedSourceId": selected}


class SharedSpeciesFoundationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        cls.blueprints = read_blueprints()

    def audit(self, sources, catalog=None):
        return audit_direct_foundations(
            sources, self.catalog if catalog is None else catalog,
            self.contracts, self.blueprints,
        )

    def test_red_bronze_share_one_species_and_preserve_two_families(self):
        report = self.audit([
            contributor("red", "ancestry.dragonborn.red"),
            contributor("bronze", "ancestry.dragonborn.bronze"),
        ])
        self.assertEqual(
            "same-species-shared-foundation", report["compositionEvidence"]
        )
        self.assertEqual(
            "species.dragonborn",
            report["sharedSpeciesFoundationSourceId"],
        )
        self.assertEqual(
            [{"rootSpeciesSourceId": "species.dragonborn",
              "contributorIds": ["red", "bronze"]}],
            report["speciesFoundations"],
        )
        by_node = {n["sourceId"]: n for n in report["sourceNodes"]}
        self.assertEqual(5, len(by_node))
        self.assertEqual(
            {"species.dragonborn", "ancestry.dragonborn.chromatic",
             "ancestry.dragonborn.red", "ancestry.dragonborn.metallic",
             "ancestry.dragonborn.bronze"},
            set(by_node),
        )
        self.assertEqual(
            ["red", "bronze"],
            [p["contributorId"] for p in
             by_node["species.dragonborn"]["contributedBy"]],
        )
        self.assertEqual(
            ["red"],
            [p["contributorId"] for p in
             by_node["ancestry.dragonborn.chromatic"]["contributedBy"]],
        )
        self.assertEqual(
            ["bronze"],
            [p["contributorId"] for p in
             by_node["ancestry.dragonborn.metallic"]["contributedBy"]],
        )
        self.assertEqual(["species.dragonborn"],
                         report["sharedSourceNodeIds"])
        self.assertEqual("no-characteristics-granted",
                         report["foundationGrantStatus"])
        self.assertEqual("not-evaluated", report["compatibilityStatus"])
        self.assertEqual("not-evaluated", report["balanceStatus"])

    def test_shared_base_trait_has_single_definition_and_two_provenances(self):
        report = self.audit([
            contributor("red", "ancestry.dragonborn.red"),
            contributor("bronze", "ancestry.dragonborn.bronze"),
        ])
        traits = [x for x in report["distinctConfiguredOffers"]
                  if x["kind"] == "trait"
                  and x["identity"] ==
                  "species.dragonborn#trait:draconic-resistance"]
        self.assertEqual(2, len(traits))
        self.assertEqual(
            {"fire", "lightning"},
            {t["parameters"]["damageType"] for t in traits},
        )
        for t in traits:
            self.assertEqual(1, len(t["contributedBy"]))
        # The source Species definition itself appears only once in the
        # sourceNodes index, even when bound offers differ by contributor.
        base = next(x for x in report["sourceNodes"]
                    if x["sourceId"] == "species.dragonborn")
        self.assertEqual(
            1, base["traitOccurrenceIds"].count(
                "species.dragonborn#trait:draconic-resistance"
            ),
        )
        self.assertNotIn("manifestedCharacteristics", report)

    def test_matching_red_contributors_group_same_configured_trait_once(self):
        report = self.audit([
            contributor("first", "ancestry.dragonborn.red"),
            contributor("second", "ancestry.dragonborn.red"),
        ])
        self.assertEqual(3, len(report["sourceNodes"]))
        self.assertEqual(3, len(report["sharedSourceNodeIds"]))
        breath = [x for x in report["distinctConfiguredOffers"]
                  if x["identity"] == "species.dragonborn#trait:breath-weapon"]
        self.assertEqual(1, len(breath))
        self.assertEqual(
            ["first", "second"],
            [p["contributorId"] for p in breath[0]["contributedBy"]],
        )
        self.assertEqual(
            {"damageType": "fire",
             "area": {"shape": "line", "lengthFeet": 30, "widthFeet": 5}},
            breath[0]["parameters"],
        )
        self.assertEqual("design-evidence-only", report["authority"])

    def test_mixed_species_preserves_distinct_foundations_and_conflicts(self):
        report = self.audit([
            contributor("human", "species.human"),
            contributor("elf", "ancestry.elf.high"),
        ])
        self.assertEqual(
            "distinct-species-foundations-require-review",
            report["compositionEvidence"],
        )
        self.assertIsNone(report["sharedSpeciesFoundationSourceId"])
        self.assertEqual(
            ["species.human", "species.elf"],
            [x["rootSpeciesSourceId"] for x in report["speciesFoundations"]],
        )
        self.assertEqual([], report["sharedSourceNodeIds"])
        self.assertIn(
            "creature.type",
            [x["identity"] for x in report["reviewFlags"]
             if x["kind"] == "multiple-source-values-needs-origin-policy"],
        )
        self.assertEqual("not-evaluated", report["compatibilityStatus"])
        self.assertEqual("no-characteristics-granted",
                         report["foundationGrantStatus"])

    def test_same_species_does_not_preapprove_intrinsic_conflicts(self):
        report = self.audit([
            contributor("high", "ancestry.elf.high"),
            contributor("wood", "ancestry.elf.wood"),
        ])
        self.assertEqual(
            "species.elf", report["sharedSpeciesFoundationSourceId"]
        )
        self.assertIn(
            "movement.walk.speed",
            [x["identity"] for x in report["reviewFlags"]
             if x["kind"] == "multiple-source-values-needs-origin-policy"],
        )
        self.assertFalse(any(
            x.get("identity") == "movement.walk.speed"
            and x.get("value") == 65
            for x in report["distinctConfiguredOffers"]
        ))

    def test_same_species_shared_definition_is_not_two_grants(self):
        report = self.audit([
            contributor("high", "ancestry.elf.high"),
            contributor("wood", "ancestry.elf.wood"),
        ])
        trance = [x for x in report["distinctConfiguredOffers"]
                  if x["identity"] == "species.elf#trait:trance"]
        self.assertEqual(1, len(trance))
        self.assertEqual(
            ["high", "wood"],
            [x["contributorId"] for x in trance[0]["contributedBy"]],
        )
        self.assertEqual({}, trance[0]["parameters"])
        self.assertNotIn("granted", report["authority"])

    def test_distinct_same_named_mechanics_are_not_collapsed(self):
        report = self.audit([
            contributor("orc", "species.orc"),
            contributor("goliath", "ancestry.goliath.cloud"),
        ])
        build = [x for x in report["distinctConfiguredOffers"]
                 if x["identity"].endswith("#trait:powerful-build")]
        self.assertEqual(
            {"species.orc#trait:powerful-build",
             "species.goliath#trait:powerful-build"},
            {x["identity"] for x in build},
        )
        self.assertEqual(2, len(build))

    def test_new_family_and_leaf_work_without_catalog_dispatch(self):
        extended = copy.deepcopy(self.catalog)
        extended["ancestry.dragonborn.prismatic"] = {
            "id": "ancestry.dragonborn.prismatic",
            "kind": "ancestry",
            "selectable": False,
            "parent": "species.dragonborn",
            "breathWeaponArea": {
                "shape": "cone", "lengthFeet": 20
            },
            "traits": [],
        }
        extended["ancestry.dragonborn.prismatic.ruby"] = {
            "id": "ancestry.dragonborn.prismatic.ruby",
            "kind": "ancestry",
            "selectable": True,
            "parent": "ancestry.dragonborn.prismatic",
            "skinMaterial": "ruby-crystal",
            "damageType": "fire",
            "traits": [],
        }
        report = self.audit([
            contributor("red", "ancestry.dragonborn.red"),
            contributor("new", "ancestry.dragonborn.prismatic.ruby"),
        ], catalog=extended)
        self.assertEqual(
            "same-species-shared-foundation", report["compositionEvidence"]
        )
        self.assertEqual("species.dragonborn",
                         report["sharedSpeciesFoundationSourceId"])
        self.assertIn("ancestry.dragonborn.prismatic",
                      {x["sourceId"] for x in report["sourceNodes"]})
        self.assertIn("ancestry.dragonborn.prismatic.ruby",
                      {x["sourceId"] for x in report["sourceNodes"]})
        breath = [x for x in report["distinctConfiguredOffers"]
                  if x["identity"] == "species.dragonborn#trait:breath-weapon"]
        self.assertEqual(
            {("fire", "line", 30), ("fire", "cone", 20)},
            {(x["parameters"]["damageType"],
              x["parameters"]["area"]["shape"],
              x["parameters"]["area"]["lengthFeet"])
             for x in breath},
        )
        deferred = [x for x in report["deferredSourceFields"]
                    if x.get("field") == "skinMaterial"]
        self.assertEqual(1, len(deferred))
        self.assertEqual("unclassified-intrinsic",
                         deferred[0]["classification"])
        self.assertEqual("ruby-crystal", deferred[0]["authoredValue"])
        self.assertNotIn(
            "skinMaterial",
            {x["identity"] for x in report["distinctConfiguredOffers"]},
        )

    def test_deferred_evidence_is_not_erased_by_grouping(self):
        sources = [
            contributor("red", "ancestry.dragonborn.red"),
            contributor("bronze", "ancestry.dragonborn.bronze"),
        ]
        report = self.audit(sources)
        original = characteristic_candidate_pool(
            sources, {}, self.blueprints, self.catalog, self.contracts,
        )
        self.assertEqual(original["deferredSourceFields"],
                         report["deferredSourceFields"])
        self.assertEqual(original["reviewFlags"], report["reviewFlags"])

    def test_source_provenance_conserved_for_all_current_selectable_sources(self):
        sources = [
            contributor(f"source-{n}", source_id)
            for n, source_id in enumerate(sorted(self.catalog))
            if self.catalog[source_id].get("selectable") is True
        ]
        report = self.audit(sources)
        expected_nodes = {}
        for entry in sources:
            for node in source_inventory(
                entry["selectedSourceId"], self.catalog
            )["lineage"]:
                expected_nodes.setdefault(node["sourceId"], []).append(
                    entry["contributorId"]
                )
        self.assertEqual(
            expected_nodes,
            {x["sourceId"]: [p["contributorId"]
                             for p in x["contributedBy"]]
             for x in report["sourceNodes"]},
        )
        self.assertEqual(len(sources), len(report["contributorLineages"]))
        self.assertEqual(
            "distinct-species-foundations-require-review",
            report["compositionEvidence"],
        )

    def test_nested_is_study_scope_limit_not_species_incompatibility(self):
        with self.assertRaisesRegex(
            ParameterInheritanceError, "only direct source contributors"
        ):
            self.audit([
                {"contributorId": "nested", "profileId": "profile-1"}
            ])

    def test_unknown_unselectable_and_duplicate_source_contributors_fail(self):
        cases = [
            [contributor("a", "species.nonexistent")],
            [contributor("a", "ancestry.dragonborn.chromatic")],
            [contributor("a", "species.human"),
             contributor("a", "species.human")],
        ]
        for sources in cases:
            with self.subTest(sources=sources):
                with self.assertRaises(ValueError):
                    self.audit(sources)

    def test_repeated_audit_does_not_mutate_sources_or_contracts(self):
        catalog = copy.deepcopy(self.catalog)
        blueprints = copy.deepcopy(self.blueprints)
        contracts = copy.deepcopy(self.contracts)
        sources = [contributor("a", "ancestry.dragonborn.red"),
                   contributor("b", "ancestry.dragonborn.bronze")]
        before = (copy.deepcopy(catalog), copy.deepcopy(blueprints),
                  copy.deepcopy(contracts), copy.deepcopy(sources))
        first = audit_direct_foundations(
            sources, catalog, contracts, blueprints
        )
        second = audit_direct_foundations(
            sources, catalog, contracts, blueprints
        )
        self.assertEqual(first, second)
        first["sourceNodes"][0]["intrinsicFields"]["walkingSpeedFeet"] = -100
        self.assertEqual(
            before, (catalog, blueprints, contracts, sources)
        )


if __name__ == "__main__":
    unittest.main()
