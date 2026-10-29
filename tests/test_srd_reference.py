"""Verify pinned SRD-only structured content, without importing it into native rules."""
import hashlib
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REF = ROOT / "data" / "reference" / "srd"
PACK = REF / "structured"


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


class SrdSourceIntegrityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = load(REF / "structured-manifest.json")
        cls.audit = load(REF / "reconciliation-inventory.json")

    def test_all_pinned_files_present_and_hashes_match(self):
        expected = self.manifest["files"]
        self.assertEqual(90, len(expected))
        actual = {p.relative_to(PACK).as_posix() for p in PACK.rglob("*.json")}
        self.assertEqual({p["path"] for p in expected}, actual)
        for entry in expected:
            with self.subTest(filename=entry["path"]):
                raw = (PACK / entry["path"]).read_bytes()
                self.assertEqual(entry["bytes"], len(raw))
                self.assertEqual(entry["sha256"], hashlib.sha256(raw).hexdigest())
                json.loads(raw)

    def test_both_complete_book_json_documents_in_source_pack(self):
        for version, filename in [("5.1", "book-srd51.json"), ("5.2.1", "book-srd52.json")]:
            with self.subTest(version=version):
                data = load(PACK / "book" / filename)
                self.assertIn("data", data)
                self.assertGreater(len(data["data"]), 10)
        for category in ["bestiary", "class", "spells"]:
            self.assertTrue((PACK / category).is_dir())

    def test_version_counts_and_inventory_are_documented(self):
        self.assertEqual("all-manifest-files-SHA256-match", self.audit["integrityCheck"])
        self.assertEqual(self.manifest["commit"], self.audit["sourcePackCommit"])
        self.assertEqual("not-independently-verified", self.audit["officialSrdPdfParity"])
        counts = self.audit["topLevelEntitiesByCategory"]
        self.assertGreaterEqual(counts["monster"]["5.1"], 300)
        self.assertGreaterEqual(counts["monster"]["5.2.1"], 300)
        self.assertGreaterEqual(counts["spell"]["5.1"], 300)
        self.assertGreaterEqual(counts["spell"]["5.2.1"], 300)
        self.assertEqual(18, counts["skill"]["5.1"])
        self.assertEqual(18, counts["skill"]["5.2.1"])
        self.assertEqual(12, counts["class"]["5.1"])
        self.assertEqual(12, counts["class"]["5.2.1"])
        self.assertNotEqual([], self.audit["unmatchedOrChanged"]["spell"])
        self.assertNotIn("nativeRuleReconciliations", self.audit)

    def test_reference_pack_is_not_native_rule_data(self):
        self.assertTrue((ROOT / "data/rulesets/first-party-001/working/skills.json").exists())
        self.assertTrue((ROOT / "data/rulesets/first-party-001/working/competencies.json").exists())
        self.assertFalse((ROOT / "data/rulesets/first-party-001/working/srd51").exists())
        self.assertFalse((ROOT / "data/rulesets/first-party-001/working/srd52").exists())


if __name__ == "__main__":
    unittest.main()
