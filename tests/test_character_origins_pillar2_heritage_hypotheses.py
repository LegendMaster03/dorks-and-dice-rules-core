"""Rock Gnome divergence hypothesis is storytelling, never a grant rule."""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_heritage_hypotheses import (  # noqa: E402
    overlay_narrative_hypotheses, read_hypotheses,
)
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


def contributor(identity, source):
    return {"contributorId": identity, "selectedSourceId": source}


class Pillar2NarrativeHeritageTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        blueprint = read_blueprint()
        cls.blueprints = {blueprint["definitionOccurrenceId"]: blueprint}
        cls.evidence = read_hypotheses()

    def compare(self, *sources, catalog=None):
        return compare_standard_target(
            "ancestry.gnome.rock",
            [contributor(f"source-{i}", name)
             for i, name in enumerate(sources)],
            self.catalog if catalog is None else catalog,
            self.contracts, self.blueprints,
        )

    def annotate(self, report, evidence=None, catalog=None):
        return overlay_narrative_hypotheses(
            report,
            self.evidence if evidence is None else evidence,
            self.catalog if catalog is None else catalog,
        )

    def test_possible_long_term_history_is_not_direct_heritage_grant(self):
        comparison = self.compare("species.gnome", "species.dwarf")
        annotated = self.annotate(comparison)
        self.assertEqual(comparison, annotated["reconstruction"])
        self.assertEqual(5, annotated["reconstruction"]["exactCandidateMatches"])
        self.assertEqual(6, annotated["reconstruction"][
            "totalModeledTargetCharacteristics"])
        self.assertFalse(annotated["annotationsAffectEligibility"])
        self.assertEqual(1, len(annotated["narrativeHypotheses"]))
        hypothesis = annotated["narrativeHypotheses"][0]
        self.assertEqual(
            "illustrative-historical-hypothesis-not-canon",
            hypothesis["status"],
        )
        self.assertFalse(hypothesis["confersEligibility"])
        relation = hypothesis["variantEvidence"][0]
        self.assertEqual("tremorsense", relation["sharedAuthoredValue"])
        self.assertEqual("sense", relation["sharedAuthoredField"])
        self.assertEqual("none", relation["inheritanceEffect"])

    def test_no_hypothesis_annotation_for_gnome_without_dwarf(self):
        report = self.compare("species.gnome")
        self.assertEqual(5, report["exactCandidateMatches"])
        self.assertEqual([], self.annotate(report)["narrativeHypotheses"])

    def test_direct_rock_source_remains_complete_without_hypothesis(self):
        report = self.compare("ancestry.gnome.rock")
        self.assertEqual(6, report["exactCandidateMatches"])
        self.assertTrue(report["canExactlyReconstructAllModeledTargetCharacteristics"])
        self.assertEqual([], self.annotate(report)["narrativeHypotheses"])

    def test_hypothesis_does_not_allow_unoffered_earthbound_manifestation(self):
        inputs = [
            contributor("gnome", "species.gnome"),
            contributor("dwarf", "species.dwarf"),
        ]
        self.assertEqual(1, len(self.annotate(
            compare_standard_target(
                "ancestry.gnome.rock", inputs,
                self.catalog, self.contracts, self.blueprints
            )
        )["narrativeHypotheses"]))
        with self.assertRaisesRegex(ParameterInheritanceError, "Unoffered trait"):
            draft_manifestation_profile(
                "child", inputs, [{
                    "kind": "trait",
                    "identity":
                    "ancestry.gnome.rock#trait:earthbound-tremorsense",
                    "parameters": {},
                }], {}, self.blueprints, self.catalog, self.contracts,
            )

    def test_relationship_can_not_propose_acquisition(self):
        modified = copy.deepcopy(self.evidence)
        modified["hypotheses"][0]["traitVariantEvidence"][0][
            "inheritanceEffect"] = "automatically-grant"
        with self.assertRaisesRegex(
            ParameterInheritanceError, "can not authorize inheritance"
        ):
            self.annotate(
                self.compare("species.gnome", "species.dwarf"),
                evidence=modified,
            )

    def test_shared_field_must_be_found_in_both_authored_traits(self):
        changed = copy.deepcopy(self.catalog)
        changed["species.dwarf"]["traits"][2]["sense"] = "darkvision"
        with self.assertRaisesRegex(
            ParameterInheritanceError, "shared trait field"
        ):
            self.annotate(
                self.compare("species.gnome", "species.dwarf",
                             catalog=changed),
                catalog=changed,
            )

    def test_nonexistent_sources_and_traits_are_rejected(self):
        for path, value in (
            (("possibleHistoricalSourceIds",), ["species.gnome", "species.fake"]),
            (("traitVariantEvidence", "relatedSourceOccurrenceId"),
             "species.dwarf#trait:unknown"),
        ):
            with self.subTest(path=path):
                modified = copy.deepcopy(self.evidence)
                hypothesis = modified["hypotheses"][0]
                if len(path) == 1:
                    hypothesis[path[0]] = value
                else:
                    hypothesis[path[0]][0][path[1]] = value
                with self.assertRaises(ParameterInheritanceError):
                    self.annotate(
                        self.compare("species.gnome", "species.dwarf"),
                        evidence=modified,
                    )

    def test_inputs_unchanged_after_annotation(self):
        comparison = self.compare("species.gnome", "species.dwarf")
        evidence = copy.deepcopy(self.evidence)
        catalog = copy.deepcopy(self.catalog)
        before_comparison = copy.deepcopy(comparison)
        before_evidence = copy.deepcopy(evidence)
        before_catalog = copy.deepcopy(catalog)
        annotated = self.annotate(comparison, evidence=evidence, catalog=catalog)
        annotated["narrativeHypotheses"][0]["variantEvidence"][0][
            "sharedAuthoredValue"] = "edited"
        annotated["reconstruction"]["comparison"][0]["coverage"] = "edited"
        self.assertEqual(before_comparison, comparison)
        self.assertEqual(before_evidence, evidence)
        self.assertEqual(before_catalog, catalog)


if __name__ == "__main__":
    unittest.main()
