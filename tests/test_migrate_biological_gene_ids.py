"""Behavioral coverage for explicit saved Origin gene-identity migration."""
import json
from pathlib import Path
import tempfile
import unittest

from tools.migrate_biological_gene_ids import load_map, main, migrate_profile


class BiologicalGeneIdentityMigrationTests(unittest.TestCase):
    def test_exact_known_identity_fields_are_migrated_without_rewriting_provenance(self):
        mapping = load_map()
        example = {
            "profileId": "original-profile",
            "underlying": {
                "speciesId": "species.dragonborn",
                "inherentGrants": [
                    {
                        "identity": {
                            "mechanicalDefinitionId": "trait.dragonborn.breath-weapon",
                            "occurrence": {
                                "source": {"id": "species.dragonborn"},
                                "occurrencePath": "/traits/2",
                            },
                        },
                        "sourceCandidateIds": ["contributor:a:species.dragonborn:/traits/2"],
                        "parameterSources": {"damageType": "parent-red"},
                    },
                    {
                        "identity": {
                            "mechanicalDefinitionId":
                                "trait.dragonborn.copper.natural-climber",
                        },
                    },
                ],
            },
            "notes": "trait.dragonborn.breath-weapon is a historical string",
            "linked": {
                "requiresTraitDefinitionId":
                    "trait.goliath.giant-ancestry-aptitude",
                "traitId": "trait.tiefling.low-light-vision",
            },
        }
        migrated, count = migrate_profile(example, mapping)
        self.assertEqual(4, count)
        grants = migrated["underlying"]["inherentGrants"]
        self.assertEqual("trait.breath-weapon",
                         grants[0]["identity"]["mechanicalDefinitionId"])
        self.assertEqual("trait.natural-climber",
                         grants[1]["identity"]["mechanicalDefinitionId"])
        self.assertEqual("trait.ancestral-aptitude",
                         migrated["linked"]["requiresTraitDefinitionId"])
        self.assertEqual("trait.low-light-vision",
                         migrated["linked"]["traitId"])
        self.assertEqual(example["underlying"]["speciesId"],
                         migrated["underlying"]["speciesId"])
        self.assertEqual(example["notes"], migrated["notes"])
        self.assertEqual(example["underlying"]["inherentGrants"][0]
                         ["sourceCandidateIds"], grants[0]["sourceCandidateIds"])
        self.assertEqual(example["underlying"]["inherentGrants"][0]
                         ["identity"]["occurrence"],
                         grants[0]["identity"]["occurrence"])
        self.assertEqual("trait.dragonborn.breath-weapon", example["underlying"]
                         ["inherentGrants"][0]["identity"]["mechanicalDefinitionId"])

    def test_shared_mechanics_converge_to_shared_canonical_ids(self):
        mapping = load_map()
        self.assertEqual(mapping["trait.goliath.powerful-build"],
                         mapping["trait.orc.powerful-build"])
        self.assertEqual(mapping["trait.elf.drow.darkness"],
                         mapping["trait.tiefling.infernal.darkness"])
        self.assertEqual(mapping["trait.dragonborn.red.natural-climber"],
                         mapping["trait.dragonborn.copper.natural-climber"])
        self.assertEqual(mapping["trait.shared.low-light-vision"],
                         mapping["trait.tiefling.low-light-vision"])
        # Damage resistance alone is not identical to the Dwarf's complete
        # Poison Resilience, which includes a separate saving-throw mechanic.
        self.assertNotEqual(
            mapping["trait.dwarf.dwarven-resilience"],
            mapping["trait.tiefling.abyssal.abyssal-resistance"],
        )
        self.assertEqual("trait.damage-type-resistance",
                         mapping["trait.tiefling.abyssal.abyssal-resistance"])
        self.assertEqual("trait.damage-type-resistance",
                         mapping["trait.tiefling.chthonic.chthonic-resistance"])
        self.assertEqual("trait.damage-type-resistance",
                         mapping["trait.tiefling.infernal.infernal-resistance"])
        for old in ("trait.poison-damage-resistance",
                    "trait.fire-damage-resistance",
                    "trait.necrotic-damage-resistance"):
            self.assertEqual("trait.damage-type-resistance", mapping[old])

    def test_dry_run_preserves_input_and_explicit_output_does_not_overwrite(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original = root / "profile.json"
            destination = root / "migrated.json"
            source = {"identity": {"mechanicalDefinitionId": "trait.orc.powerful-build"}}
            original.write_text(json.dumps(source), encoding="utf-8")
            self.assertEqual(0, main(["--input", str(original), "--dry-run"]))
            self.assertFalse(destination.exists())
            self.assertEqual(0, main(["--input", str(original),
                                     "--output", str(destination)]))
            self.assertEqual("trait.powerful-build",
                             json.loads(destination.read_text(encoding="utf-8"))
                             ["identity"]["mechanicalDefinitionId"])
            self.assertEqual(source, json.loads(original.read_text(encoding="utf-8")))
            with self.assertRaises(FileExistsError):
                main(["--input", str(original), "--output", str(destination)])


if __name__ == "__main__":
    unittest.main()
