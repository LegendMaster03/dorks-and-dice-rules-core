"""Verify that the native section catalog reproduces the licensed source."""
import json
import unittest
from tools.import_fate_condensed import convert, TARGET


class FateImportTests(unittest.TestCase):
    def test_source_and_catalog_match(self):
        records = convert()
        self.assertEqual(records, json.loads(TARGET.read_text(encoding="utf-8")))
        ids = {r["id"] for r in records}
        self.assertTrue(all(r["parentId"] is None or r["parentId"] in ids for r in records))
