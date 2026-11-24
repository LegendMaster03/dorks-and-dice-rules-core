"""Rock Gnome and Dwarf share a canonical Tremorsense definition.

Source occurrence identity remains relevant to reconstruction and inheritance;
a related source does not automatically grant an unselected Rock Gnome trait.
"""
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_parameter_inheritance import read_blueprint  # noqa: E402
from character_origins_source_inventory import read_catalog  # noqa: E402
from character_origins_target_reconstruction import compare_standard_target  # noqa: E402

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

    def compare(self, *contributors):
        return compare_standard_target(
            TARGET, list(contributors), self.catalog, self.contracts,
            self.blueprints,
        )

    def test_canonical_sense_is_shared_by_both_sources(self):
        dwarf = next(trait for trait in self.catalog["species.dwarf"]["traits"]
                     if trait["id"] == "stonebound-tremorsense")
        rock = next(trait for trait in self.catalog[TARGET]["traits"]
                    if trait["id"] == "stonebound-tremorsense")
        self.assertEqual(dwarf, rock)
        self.assertEqual("bonus-action", rock["activation"])
        self.assertEqual({"lookup": "nearest", "sourceField": "tremorsenseSubTrait"},
                         rock["originBindings"]["subTrait"])
        self.assertEqual({"rangeFeet": 60, "contactMaterials": ["stone"]},
                         self.catalog["species.dwarf"]["tremorsenseSubTrait"])
        self.assertEqual({"rangeFeet": 60, "contactMaterials": ["earth", "stone"]},
                         self.catalog[TARGET]["tremorsenseSubTrait"])
        self.assertEqual(10, rock["durationMinutes"])

    def test_parent_species_does_not_invent_ancestry_sense(self):
        report = self.compare(contributor("gnome", "species.gnome"))
        trait_ids = {x["identity"] for x in report["comparison"]
                     if x["coverage"] == "exact-candidate"}
        self.assertNotIn("ancestry.gnome.rock#trait:stonebound-tremorsense",
                         trait_ids)

    def test_complete_native_source_can_reconstruct_its_own_traits(self):
        report = self.compare(contributor("rock", TARGET))
        self.assertEqual(report["totalModeledTargetCharacteristics"],
                         report["exactCandidateMatches"])
        # Explicit spellcasting choices remain pending, not inferred.
        self.assertTrue(any(x["field"] == "spellcastingAbilityChoice"
                            for x in report["targetDeferredEvidence"]))

    def test_comparison_preserves_source_provenance(self):
        report = self.compare(contributor("gnome", "species.gnome"),
                              contributor("dwarf", "species.dwarf"))
        self.assertEqual("origin-reconstruction-candidate-evidence-only",
                         report["status"])
        self.assertEqual("upbringing.gnome.rock", report["suggestedUpbringing"])
        self.assertFalse(any(x["identity"].startswith("upbringing.")
                             for x in report["comparison"]))


if __name__ == "__main__":
    unittest.main()
