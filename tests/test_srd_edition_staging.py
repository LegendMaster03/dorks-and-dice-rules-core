import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from stage_srd_editions import build, canonical_digest, identity, write_catalogs
from build_srd_loader_fixtures import fixture_records


class SrdEditionStagingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.packages = build()

    def test_source_occurrences_and_payloads_remain_separate(self):
        for edition, rows in self.packages.items():
            self.assertGreater(len(rows), 100)
            for row in rows:
                self.assertEqual(edition, row["source"]["edition"])
                self.assertEqual("unreviewed", row["reviewStatus"])
                self.assertEqual(canonical_digest(row["payload"]), row["source"]["payloadSha256"])
                expected_tag = "SRD51" if edition == "srd-5.1" else "SRD52"
                self.assertEqual(expected_tag, row["payload"].get("source", expected_tag))
        self.assertNotEqual(
            {r["candidateId"] for r in self.packages["srd-5.1"]},
            {r["candidateId"] for r in self.packages["srd-5.2.1"]})

    def test_contextual_identity_does_not_merge_same_named_features(self):
        base = {"name": "Ability Score Improvement", "className": "Fighter", "level": 4}
        self.assertNotEqual(identity("classFeature", base),
                            identity("classFeature", dict(base, level=6)))
        self.assertNotEqual(identity("classFeature", base),
                            identity("classFeature", dict(base, className="Rogue")))

    def test_fixture_projection_preserves_every_occurrence_without_merging(self):
        for rows in self.packages.values():
            projected = fixture_records(rows)
            self.assertEqual(len(rows), len(projected))
            self.assertEqual(len(rows), len({r["id"] for r in projected}))
            for source, fixture in zip(rows, projected):
                self.assertTrue(fixture["fixtureOnly"])
                self.assertEqual(source["payload"], fixture["definition"])
                self.assertEqual(source["source"], fixture["sourceOccurrence"])

    def test_output_is_review_only_and_round_trips(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            summary = write_catalogs(root, self.packages)
            for package, entry in summary.items():
                rows = json.loads((root / package / "working" / "srd-candidates.json")
                                  .read_text(encoding="utf-8"))["candidates"]
                self.assertEqual(self.packages[entry["edition"]], rows)
                self.assertFalse((root / package / "native").exists())
