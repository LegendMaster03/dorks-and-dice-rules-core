"""Validate named official spell inventories offline; CI separately re-extracts PDFs."""
import json
from pathlib import Path
import unittest
from tools.import_official_srd_spells import slug

ROOT = Path(__file__).resolve().parents[1]


class OfficialSrdSpellImportTests(unittest.TestCase):
    def test_all_named_spell_records_match_edition_inventory(self):
        for package, tag, count in (("dnd-5e", "51", 319), ("dnd-5-5e", "52", 339)):
            with self.subTest(package=package):
                root = ROOT / "data/rulesets" / package
                manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
                spells = [r for p in sorted((root / "native/spells").glob("spells-*.json"))
                          for r in json.loads(p.read_text(encoding="utf-8"))]
                expected = json.loads((ROOT / f"data/reference/srd/structured/spells/spells-srd{tag}.json")
                                      .read_text(encoding="utf-8"))["spell"]
                self.assertEqual(count, len(spells))
                self.assertEqual(count, manifest["officialSpellImport"]["spellRecords"])
                self.assertEqual({(slug(x["name"]), x["level"], x["school"]) for x in expected},
                                 {(slug(x["name"]), x["level"], x["school"]) for x in spells})
                self.assertEqual(count, len({x["id"] for x in spells}))
                for record in spells:
                    self.assertEqual("spell." + slug(record["name"]), record["id"])
                    self.assertEqual("spell", record["kind"])
                    self.assertEqual(manifest["officialPageImport"]["pdfSha256"], record["source"]["pdfSha256"])
                    self.assertEqual(manifest["attribution"], record["source"]["attribution"])
                    self.assertEqual({"Casting Time", "Range", "Components", "Duration"}, set(record["header"]))
                    self.assertTrue(record["officialTextLayout"].startswith(record["name"]))
                    self.assertEqual([f"rulebook.page-{n:03}" for n in record["source"]["pages"]],
                                     record["catalogReferences"])

    def test_edition_differences_come_from_publisher_text(self):
        def find(package, name):
            for path in (ROOT / "data/rulesets" / package / "native/spells").glob("spells-*.json"):
                for record in json.loads(path.read_text(encoding="utf-8")):
                    if record["id"] == "spell." + name:
                        return record
            self.fail("Missing spell " + name)

        old = find("dnd-5e", "acid-splash")
        new = find("dnd-5-5e", "acid-splash")
        self.assertEqual(("C", "V"), (old["school"], new["school"]))
        self.assertIn("one creature", old["officialTextLayout"])
        self.assertIn("5-foot-radius Sphere", new["officialTextLayout"])
        self.assertEqual("Concentration, up to 1 hour", find("dnd-5e", "barkskin")["header"]["Duration"])
        self.assertEqual("1 hour", find("dnd-5-5e", "barkskin")["header"]["Duration"])
        self.assertEqual("Bonus Action", find("dnd-5-5e", "barkskin")["header"]["Casting Time"])
