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

    def test_structured_default_skills_actions_and_rating_ladder(self):
        sections = {r["id"]: r for r in convert()}
        structured = json.loads((TARGET.parent / "structured.json").read_text(encoding="utf-8"))
        known = sections.keys() | {r["id"] for r in structured}
        self.assertEqual(26, len(structured))
        self.assertEqual(19, sum(r["kind"] == "skill" for r in structured))
        self.assertEqual(4, sum(r["kind"] == "action" for r in structured))
        for record in structured:
            self.assertTrue(set(record["catalogReferences"]) <= known)
            origin = sections[record["catalogReferences"][0]]
            self.assertEqual(origin["source"], record["source"])
            if record["kind"] == "skill":
                self.assertIn("**" + record["name"] + ":** " + record["description"], origin["markdown"])
            if record["kind"] == "action":
                self.assertEqual(origin["markdown"], record["markdown"])
        ladder = next(r for r in structured if r["id"] == "rule.adjective-ladder")
        self.assertEqual(list(range(8, -5, -1)), [r["rating"] for r in ladder["ratings"]])
        pyramid = next(r for r in structured if r["id"] == "rule.starting-skill-pyramid")
        self.assertEqual(10, sum(r["count"] for r in pyramid["allocations"]))
        capacity = next(r for r in structured if r["id"] == "rule.stress-capacity")
        self.assertEqual([3, 4, 6, 6], [r["boxes"] for r in capacity["thresholds"]])
