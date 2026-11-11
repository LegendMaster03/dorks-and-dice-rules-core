"""Standard Origin template readiness is a source audit, not a grant rule."""
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
from character_origins_parameter_inheritance import read_blueprints  # noqa: E402
from character_origins_source_inventory import (  # noqa: E402
    read_catalog, source_inventory,
)
from character_origins_standard_preset_readiness import (  # noqa: E402
    audit_standard_corpus,
    propose_standard_preset,
)


class Pillar2StandardPresetReadinessTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        cls.blueprints = read_blueprints()

    def propose(self, selected, catalog=None):
        return propose_standard_preset(
            selected, self.catalog if catalog is None else catalog,
            self.contracts, self.blueprints,
        )

    @staticmethod
    def kinds(report):
        return {item["kind"] for item in report["unresolvedOriginDecisions"]}

    @staticmethod
    def entries(report, kind):
        return [
            row for row in report["proposedSelections"]
            if row["kind"] == kind
        ]

    def test_all_selectable_sources_have_accounted_for_trait_occurrences(self):
        standard = audit_standard_corpus(
            self.catalog, self.contracts, self.blueprints
        )
        self.assertEqual(
            {k for k, v in self.catalog.items() if v.get("selectable") is True},
            {r["selectedSourceId"] for r in standard["entries"]},
        )
        self.assertEqual(len(standard["entries"]),
                         standard["selectableSourceCount"])
        self.assertGreater(standard["selectableSourceCount"], 20)
        for row in standard["entries"]:
            with self.subTest(source=row["selectedSourceId"]):
                actual = source_inventory(row["selectedSourceId"],
                                          self.catalog)
                all_traits = {
                    x["occurrenceId"] for node in actual["lineage"]
                    for x in node["traits"]
                }
                proposed = {
                    x["identity"]
                    for x in self.entries(row, "trait")
                }
                deferred = {
                    x["field"] for x in row["deferredSourceEvidence"]
                    if "#trait:" in x["field"]
                }
                self.assertEqual(all_traits, proposed | deferred)
                self.assertFalse(proposed & deferred)
                self.assertEqual(
                    "standard-origin-template-proposal-not-authorized",
                    row["status"],
                )

    def test_unambiguous_proposals_can_use_same_profile_selection_shape(self):
        for sid in ("species.human", "species.gnome",
                    "ancestry.gnome.rock", "ancestry.elf.high",
                    "ancestry.elf.wood", "ancestry.dragonborn.red"):
            with self.subTest(source=sid):
                proposed = self.propose(sid)
                profile = draft_manifestation_profile(
                    "standard-example",
                    [proposed["standardSource"]],
                    proposed["proposedSelections"],
                    {}, self.blueprints, self.catalog, self.contracts,
                )
                self.assertEqual(
                    len(proposed["proposedSelections"]),
                    len(profile["manifestedCharacteristics"]),
                )
                self.assertEqual("not-authorized", profile["authority"])
                self.assertEqual(
                    proposed["proposedSelections"], profile["selections"]
                )
        # The study's dry-run profile remains unauthorized and has no
        # automatic default package for unselected / unresolved choices.

    def test_human_size_and_skill_feat_choices_are_explicitly_pending(self):
        report = self.propose("species.human")
        self.assertEqual(3, len(self.entries(report, "trait")))
        self.assertEqual(
            {"species.human#trait:skillful",
             "species.human#trait:versatile",
             "species.human#trait:adaptable-learning"},
            {x["identity"] for x in self.entries(report, "trait")},
        )
        self.assertEqual(
            {"creature.type", "movement.walk.speed"},
            {x["identity"] for x in self.entries(report, "intrinsic")},
        )
        self.assertIn("explicit-intrinsic-choice-required",
                      self.kinds(report))
        self.assertIn("dependent-trait-choices-not-selected",
                      self.kinds(report))
        size = next(x for x in report["unresolvedOriginDecisions"]
                    if x["kind"] == "explicit-intrinsic-choice-required")
        self.assertEqual("creature.size", size["identity"])
        self.assertEqual(["small", "medium"],
                         [x["value"] for x in size["options"]])
        self.assertFalse(report["templateComplete"])

    def test_wood_elf_does_not_auto_select_35_or_stack_30_and_35(self):
        report = self.propose("ancestry.elf.wood")
        self.assertFalse(any(
            x["kind"] == "intrinsic"
            and x["identity"] == "movement.walk.speed"
            for x in report["proposedSelections"]
        ))
        conflicting = [
            x for x in report["unresolvedOriginDecisions"]
            if x["kind"] == "competing-intrinsic-values-require-policy"
            and x["identity"] == "movement.walk.speed"
        ]
        self.assertEqual(1, len(conflicting))
        self.assertEqual([30, 35],
                         [x["value"] for x in conflicting[0]["options"]])
        self.assertEqual(
            ["species.elf", "ancestry.elf.wood"],
            report["sourceLineage"],
        )

    def test_high_elf_has_distinct_progression_and_dependent_choices(self):
        report = self.propose("ancestry.elf.high")
        traits = {x["identity"] for x in self.entries(report, "trait")}
        self.assertIn("ancestry.elf.high#trait:arcane-cantrip", traits)
        self.assertIn("ancestry.elf.high#trait:detect-magic", traits)
        self.assertIn("ancestry.elf.high#trait:misty-step", traits)
        self.assertIn("species.elf#trait:trance", traits)
        choices = [
            x for x in report["unresolvedOriginDecisions"]
            if x["kind"] == "dependent-trait-choices-not-selected"
        ]
        self.assertTrue(any(
            x["identity"] == "ancestry.elf.high#trait:arcane-cantrip"
            for x in choices
        ))
        self.assertTrue(any(
            x["kind"] == "dependent-source-choice-not-selected"
            and x["field"] == "spellcastingAbilityChoice"
            for x in report["unresolvedOriginDecisions"]
        ))
        self.assertEqual(
            "upbringing.elf.high",
            report["nearestSuggestedUpbringing"]["upbringingId"],
        )
        self.assertEqual(
            "independent-culture-not-manifested",
            report["upbringingStatus"],
        )

    def test_red_dragonborn_tracks_all_three_parameter_bound_traits(self):
        report = self.propose("ancestry.dragonborn.red")
        proposed = {x["identity"]: x for x in self.entries(report, "trait")}
        self.assertEqual(
            {"damageType": "fire",
             "area": {"shape": "line", "lengthFeet": 30, "widthFeet": 5}},
            proposed["species.dragonborn#trait:breath-weapon"]["parameters"],
        )
        self.assertEqual(
            {"damageType": "fire"},
            proposed["species.dragonborn#trait:draconic-resistance"]["parameters"],
        )
        self.assertEqual(
            {"damageType": "fire"},
            proposed["ancestry.dragonborn.chromatic#trait:chromatic-warding"]["parameters"],
        )
        deferred = {x["field"] for x in report["deferredSourceEvidence"]}
        self.assertNotIn(
            "species.dragonborn#trait:draconic-resistance", deferred,
        )
        self.assertNotIn(
            "ancestry.dragonborn.chromatic#trait:chromatic-warding", deferred,
        )
        # Authored leaf/family source fields are not separate Origin grants.
        self.assertIn("damageType", deferred)
        self.assertIn("breathWeaponArea", deferred)
        self.assertTrue(report["templateComplete"])

    def test_rock_gnome_stays_a_distinct_standard_source(self):
        report = self.propose("ancestry.gnome.rock")
        traits = {x["identity"] for x in self.entries(report, "trait")}
        self.assertIn(
            "ancestry.gnome.rock#trait:earthbound-tremorsense", traits
        )
        self.assertNotIn(
            "species.dwarf#trait:stonebound-tremorsense", traits
        )
        self.assertEqual(
            "upbringing.gnome.rock",
            report["nearestSuggestedUpbringing"]["upbringingId"],
        )
        self.assertTrue(report["templateComplete"])

    def test_repeated_same_value_intrinsic_generates_one_selection(self):
        altered = copy.deepcopy(self.catalog)
        altered["ancestry.elf.high"]["walkingSpeedFeet"] = 30
        report = self.propose("ancestry.elf.high", catalog=altered)
        speeds = [x for x in self.entries(report, "intrinsic")
                  if x["identity"] == "movement.walk.speed"]
        self.assertEqual(1, len(speeds))
        self.assertEqual(30, speeds[0]["value"])
        rationale = [x for x in report["sourceRationale"]
                     if x["identity"] == "movement.walk.speed"]
        self.assertEqual(1, len(rationale))
        self.assertEqual(2, len(rationale[0]["sourceOccurrences"]))

    def test_unknown_source_field_is_neither_silently_lost_nor_granted(self):
        altered = copy.deepcopy(self.catalog)
        altered["species.human"]["unreviewedPhysicalProperty"] = {
            "configuration": "unknown"
        }
        report = self.propose("species.human", catalog=altered)
        self.assertTrue(any(
            x["field"] == "unreviewedPhysicalProperty"
            and x["classification"] == "unclassified-intrinsic"
            for x in report["deferredSourceEvidence"]
        ))
        self.assertTrue(any(
            x["kind"] == "source-characteristic-not-yet-modeled"
            and x["field"] == "unreviewedPhysicalProperty"
            for x in report["unresolvedOriginDecisions"]
        ))
        self.assertFalse(any(
            x.get("identity") == "unreviewedPhysicalProperty"
            for x in report["proposedSelections"]
        ))

    def test_invalid_size_choice_stays_explicitly_unresolved(self):
        altered = copy.deepcopy(self.catalog)
        altered["species.human"]["sizeChoice"] = {"from": [], "when": "x"}
        report = self.propose("species.human", catalog=altered)
        self.assertTrue(any(
            x["field"] == "sizeChoice"
            for x in report["deferredSourceEvidence"]
        ))
        self.assertTrue(any(
            x["kind"] == "source-characteristic-not-yet-modeled"
            and x["field"] == "sizeChoice"
            for x in report["unresolvedOriginDecisions"]
        ))

    def test_corpus_summary_is_deterministic_and_never_modifies_catalog(self):
        catalog, blueprints = copy.deepcopy(self.catalog), copy.deepcopy(
            self.blueprints
        )
        before_catalog, before_blueprints = (
            copy.deepcopy(catalog), copy.deepcopy(blueprints)
        )
        first = audit_standard_corpus(catalog, self.contracts, blueprints)
        second = audit_standard_corpus(catalog, self.contracts, blueprints)
        self.assertEqual(first, second)
        first["entries"][0]["proposedSelections"].clear()
        self.assertEqual(before_catalog, catalog)
        self.assertEqual(before_blueprints, blueprints)
        self.assertGreaterEqual(
            sum(first["sourceIssuesByKind"].values()), 1
        )


if __name__ == "__main__":
    unittest.main()
