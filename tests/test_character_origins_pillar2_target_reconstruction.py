"""Pillar 2 Rock Gnome reconstruction from Gnome + Dwarf sources.

Source coverage is distinct from successful manifestation, combat equivalence,
or the cultural Rock Gnome Upbringing. These tests must not infer that a
shared tremorsense label authorizes replacing a trait's constraints.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_manifestation_inventory import (  # noqa: E402
    draft_manifestation_profile,
)
from character_origins_parameter_inheritance import (  # noqa: E402
    ParameterInheritanceError, read_blueprint,
)
from character_origins_source_inventory import read_catalog  # noqa: E402
from character_origins_target_reconstruction import (  # noqa: E402
    compare_standard_target,
)

TARGET = "ancestry.gnome.rock"


def contributor(key, source):
    return {"contributorId": key, "selectedSourceId": source}


class Pillar2RockGnomeReconstructionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        blueprint = read_blueprint()
        cls.blueprints = {blueprint["definitionOccurrenceId"]: blueprint}

    def compare(self, *contributors, catalog=None):
        return compare_standard_target(
            TARGET, list(contributors),
            self.catalog if catalog is None else catalog,
            self.contracts, self.blueprints,
        )

    def _missing(self, report):
        return [entry for entry in report["comparison"]
                if entry["coverage"] != "exact-candidate"]

    def test_gnome_and_dwarf_match_five_of_six_rock_gnome_characteristics(self):
        report = self.compare(
            contributor("gnome", "species.gnome"),
            contributor("dwarf", "species.dwarf"),
        )
        self.assertEqual(6, report["totalModeledTargetCharacteristics"])
        self.assertEqual(5, report["exactCandidateMatches"])
        self.assertFalse(
            report["canExactlyReconstructAllModeledTargetCharacteristics"]
        )
        self.assertEqual(1, len(self._missing(report)))
        self.assertEqual(
            "ancestry.gnome.rock#trait:earthbound-tremorsense",
            self._missing(report)[0]["identity"],
        )
        self.assertEqual(
            "related-but-not-equivalent",
            self._missing(report)[0]["coverage"],
        )
        self.assertEqual(
            "origin-reconstruction-candidate-evidence-only",
            report["status"],
        )

    def test_gnome_alone_provides_same_five_exact_target_characteristics(self):
        report = self.compare(contributor("gnome", "species.gnome"))
        self.assertEqual(5, report["exactCandidateMatches"])
        self.assertEqual(6, report["totalModeledTargetCharacteristics"])
        missing = self._missing(report)
        self.assertEqual(1, len(missing))
        self.assertEqual("not-offered", missing[0]["coverage"])
        self.assertEqual([], missing[0]["relatedTraitEvidence"])

    def test_dwarf_adds_related_tremorsense_but_not_rock_variant(self):
        report = self.compare(
            contributor("gnome", "species.gnome"),
            contributor("dwarf", "species.dwarf"),
        )
        missing = self._missing(report)[0]
        related = missing["relatedTraitEvidence"]
        self.assertEqual(1, len(related))
        self.assertEqual(
            "species.dwarf#trait:stonebound-tremorsense",
            related[0]["candidateOccurrenceId"],
        )
        self.assertEqual(
            {"sense": "tremorsense"}, related[0]["sharedComponents"],
        )
        self.assertEqual(
            "related-not-equivalent", related[0]["relationship"]
        )
        dwarf = related[0]["candidateDefinition"]
        target_trait = self.catalog[TARGET]["traits"][0]
        self.assertEqual(60, dwarf["rangeFeet"])
        self.assertEqual(15, target_trait["rangeFeet"])
        self.assertEqual("bonus-action", dwarf["activation"])
        self.assertEqual("passive", target_trait["activation"])
        self.assertEqual("stone", dwarf["requiresContact"])
        self.assertEqual(
            ["earth", "stone"], target_trait["requiresSharedContact"]
        )
        self.assertEqual(10, dwarf["durationMinutes"])
        self.assertIn("uses", dwarf)
        self.assertNotIn("uses", target_trait)

    def test_dwarf_alone_cannot_recreate_rock_gnome_baseline(self):
        report = self.compare(contributor("dwarf", "species.dwarf"))
        self.assertEqual(1, report["exactCandidateMatches"])
        matched = [item["identity"] for item in report["comparison"]
                   if item["coverage"] == "exact-candidate"]
        self.assertEqual(["movement.walk.speed"], matched)
        self.assertEqual(
            "related-but-not-equivalent",
            next(x for x in report["comparison"]
                 if x["identity"] ==
                 "ancestry.gnome.rock#trait:earthbound-tremorsense")["coverage"],
        )

    def test_rock_gnome_itself_has_full_source_coverage(self):
        report = self.compare(contributor("rock", TARGET))
        self.assertEqual(6, report["exactCandidateMatches"])
        self.assertEqual([], self._missing(report))
        self.assertTrue(
            report["canExactlyReconstructAllModeledTargetCharacteristics"]
        )

    def test_each_of_five_exact_matches_keeps_gnome_provenance(self):
        report = self.compare(
            contributor("gnome", "species.gnome"),
            contributor("dwarf", "species.dwarf"),
        )
        exact = [x for x in report["comparison"]
                 if x["coverage"] == "exact-candidate"]
        self.assertEqual(
            {"creature.type", "creature.size", "movement.walk.speed",
             "species.gnome#trait:low-light-vision",
             "species.gnome#trait:gnomish-cunning"},
            {x["identity"] for x in exact},
        )
        for row in exact:
            self.assertIn(
                "gnome",
                [p["contributorId"] for p in
                 row["exactCandidateProvenance"]],
            )
        speed = next(x for x in exact
                     if x["identity"] == "movement.walk.speed")
        self.assertEqual(
            ["gnome", "dwarf"],
            [p["contributorId"] for p in
             speed["exactCandidateProvenance"]],
        )

    def test_dwarf_alternative_size_type_and_vision_are_not_target_grants(self):
        report = self.compare(
            contributor("gnome", "species.gnome"),
            contributor("dwarf", "species.dwarf"),
        )
        extras = {(x["kind"], x["identity"],
                   str(x["value"])) for x in report["candidateOnlyExtras"]}
        self.assertIn(("intrinsic", "creature.size", "medium"), extras)
        self.assertIn(("intrinsic", "creature.type", "humanoid"), extras)
        self.assertIn(("intrinsic", "sense.darkvision.range", "60"), extras)
        flags = {x["identity"] for x in report["candidateReviewFlags"]}
        self.assertIn("creature.size", flags)
        self.assertIn("creature.type", flags)
        self.assertIn("sense.low-light-vs-darkvision", flags)

    def test_reconstruction_does_not_inherit_cultural_upbringing(self):
        report = self.compare(
            contributor("gnome", "species.gnome"),
            contributor("dwarf", "species.dwarf"),
        )
        self.assertEqual(
            "upbringing.gnome.rock", report["suggestedUpbringing"]
        )
        self.assertEqual(
            "separate-cultural-choice-not-inherited",
            report["upbringingStatus"],
        )
        self.assertFalse(any(
            item["identity"].startswith("upbringing.")
            for item in report["comparison"]
        ))

    def test_draft_can_manifest_five_matching_characteristics_explicitly(self):
        choices = [
            {"kind": "intrinsic", "identity": "creature.type",
             "value": "fey"},
            {"kind": "intrinsic", "identity": "creature.size",
             "value": "small"},
            {"kind": "intrinsic", "identity": "movement.walk.speed",
             "value": 25},
            {"kind": "trait",
             "identity": "species.gnome#trait:low-light-vision",
             "parameters": {}},
            {"kind": "trait",
             "identity": "species.gnome#trait:gnomish-cunning",
             "parameters": {}},
        ]
        sources = [
            contributor("gnome", "species.gnome"),
            contributor("dwarf", "species.dwarf"),
        ]
        profile = draft_manifestation_profile(
            "mixed", sources, choices, {}, self.blueprints,
            self.catalog, self.contracts,
        )
        self.assertEqual(5, len(profile["manifestedCharacteristics"]))
        self.assertEqual("not-authorized", profile["authority"])
        self.assertTrue(profile["reviewFlags"])
        with self.assertRaisesRegex(
            ParameterInheritanceError, "Unoffered trait"
        ):
            draft_manifestation_profile(
                "mixed", sources,
                [*choices, {
                    "kind": "trait",
                    "identity":
                    "ancestry.gnome.rock#trait:earthbound-tremorsense",
                    "parameters": {},
                }],
                {}, self.blueprints, self.catalog, self.contracts,
            )

    def test_similar_tremorsense_does_not_become_equivalent_by_field_edits(self):
        changed = copy.deepcopy(self.catalog)
        # Even if a source author adopts the same passive/range/contact
        # shape, origin-level semantic equivalence must be reviewed, not
        # inferred solely from matching field values.
        dwarf = next(x for x in changed["species.dwarf"]["traits"]
                     if x["id"] == "stonebound-tremorsense")
        replacement = copy.deepcopy(changed[TARGET]["traits"][0])
        dwarf.clear()
        dwarf.update(replacement)
        dwarf["id"] = "stonebound-tremorsense"
        report = self.compare(
            contributor("gnome", "species.gnome"),
            contributor("dwarf", "species.dwarf"), catalog=changed,
        )
        self.assertEqual(5, report["exactCandidateMatches"])
        self.assertEqual(
            "related-but-not-equivalent",
            self._missing(report)[0]["coverage"],
        )

    def test_catalog_and_blueprint_are_unchanged_by_comparison(self):
        catalog, blueprints = copy.deepcopy(self.catalog), copy.deepcopy(
            self.blueprints
        )
        before_catalog, before_blueprints = (
            copy.deepcopy(catalog), copy.deepcopy(blueprints)
        )
        report = compare_standard_target(
            TARGET, [
                contributor("gnome", "species.gnome"),
                contributor("dwarf", "species.dwarf"),
            ], catalog, self.contracts, blueprints,
        )
        report["comparison"][0]["targetValue"] = "modified"
        self.assertEqual(before_catalog, catalog)
        self.assertEqual(before_blueprints, blueprints)


if __name__ == "__main__":
    unittest.main()
