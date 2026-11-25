"""Validate committed publisher page coverage without a network dependency."""
import json
from pathlib import Path
import unittest
from tools.srd_import_sources import read_native_shards

ROOT = Path(__file__).resolve().parents[1]


class OfficialSrdPageImportTests(unittest.TestCase):
    def test_authoritative_page_records_match_pinned_edition_inventory(self):
        inventory = json.loads((ROOT / "data/reference/srd/official-page-import.json")
                               .read_text(encoding="utf-8"))
        for source in inventory["sources"]:
            with self.subTest(edition=source["edition"]):
                package = ROOT / "data/rulesets" / source["package"]
                manifest = json.loads((package / "manifest.json").read_text(encoding="utf-8"))
                records = read_native_shards(package / "native", "rulebook/pages-*.json")
                self.assertEqual(source["recordCount"], len(records))
                self.assertEqual(list(range(source["firstGameplayPage"], source["pdfPages"] + 1)),
                                 [record["page"] for record in records])
                self.assertEqual(source["pdfSha256"], manifest["officialPageImport"]["pdfSha256"])
                for record in records:
                    self.assertEqual(f"rulebook.page-{record['page']:03}", record["id"])
                    self.assertEqual("rulebook-page", record["kind"])
                    self.assertEqual(source["edition"], record["source"]["release"])
                    self.assertEqual(source["pdfSha256"], record["source"]["pdfSha256"])
                    self.assertEqual("CC-BY-4.0", record["source"]["license"])
                    self.assertEqual(manifest["attribution"], record["source"]["attribution"])
                    self.assertTrue(record["textLayout"].strip())
                    self.assertNotIn("\r", record["textLayout"])
                    self.assertNotIn("\t", record["textLayout"])
                    self.assertTrue(record["source"]["pageUrl"].endswith(f"#page={record['page']}"))
