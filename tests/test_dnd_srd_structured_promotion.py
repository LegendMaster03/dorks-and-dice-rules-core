import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1] / "data/rulesets"


def records(package, file):
    return json.loads((ROOT / package / "native" / file).read_text(encoding="utf-8"))


class DndSrdStructuredPromotionTests(unittest.TestCase):
    def test_complete_skill_and_condition_inventories_and_local_refs(self):
        for package in ("dnd-5e", "dnd-5-5e"):
            with self.subTest(package=package):
                skills = records(package, "skills.json")
                conditions = records(package, "conditions.json")
                self.assertEqual(18, len(skills))
                self.assertEqual(15, len(conditions))
                pages = {p["id"] for f in (ROOT / package / "native/rulebook").glob("pages-*.json")
                         for p in json.loads(f.read_text(encoding="utf-8"))}
                known = pages | {r["id"] for r in conditions + skills}
                for record in skills + conditions:
                    self.assertTrue(record["entries"])
                    self.assertTrue(record["source"]["pages"])
                    self.assertTrue(set(record["catalogReferences"]) <= known)
                self.assertEqual({"str", "dex", "int", "wis", "cha"}, {x["ability"] for x in skills})
                self.assertNotIn("condition.exhaustion", [r["id"] for r in records(package, "resolution.json")])

    def test_edition_specific_condition_differences(self):
        old = {x["name"]: json.dumps(x["entries"]) for x in records("dnd-5e", "conditions.json")}
        modern = {x["name"]: json.dumps(x["entries"]) for x in records("dnd-5-5e", "conditions.json")}
        self.assertNotIn("raised from the dead", old["Exhaustion"])
        self.assertIn("Speed halved", old["Exhaustion"])
        self.assertIn("2 times your Exhaustion level", modern["Exhaustion"])
        self.assertNotIn("other than the grappler", old["Grappled"])
        self.assertIn("other than the grappler", modern["Grappled"])
        self.assertIn("can't move", old["Stunned"])
        self.assertNotIn("Speed 0", modern["Stunned"])
