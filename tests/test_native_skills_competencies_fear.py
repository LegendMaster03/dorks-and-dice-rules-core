"""Native content invariants for selected Skills, Competencies and fear rules."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"
WORKING = ROOT / "data/rulesets/first-party-001/working"

def read(path):
    return json.loads(path.read_text(encoding="utf-8"))

class PromotedSkillsAndFearTests(unittest.TestCase):
    def test_skills_are_exact_selected_source_definitions(self):
        native = read(NATIVE / "skills.json")["skills"]
        source = read(WORKING / "skills.json")["skills"]
        self.assertEqual(source, native)
        identities = {s["id"]: s for s in native}
        self.assertEqual(len(native), len(identities))
        for parent in ("skill.perception","skill.stealth","skill.acrobatics","skill.athletics"):
            self.assertIn(parent, identities)
            self.assertEqual("arithmetic-mean", identities[parent]["composition"]["operation"])
            for child in identities[parent]["childSkills"]:
                self.assertIn(child,identities)

    def test_competencies_preserve_crafting_and_tool_facets(self):
        native = read(NATIVE / "competencies.json")["competencies"]
        self.assertEqual(read(WORKING / "competencies.json")["competencies"], native)
        identities = {c["id"]:c for c in native}
        self.assertEqual(len(identities),len(native))
        alchemy=identities["competency.alchemy"]
        self.assertEqual({"crafting-skill","tool-proficiency"},{f["type"] for f in alchemy["facets"]})

    def test_selected_fear_rule_retains_unresolved_escalation_as_inert(self):
        native = read(NATIVE / "fear.json")
        source = read(WORKING / "fear.json")
        self.assertEqual("rule.fear-family",native["id"])
        self.assertEqual("rule",native["kind"])
        self.assertNotIn("status", native)
        historical=dict(source)
        historical.pop("status")
        self.assertEqual(historical,native)
        self.assertEqual("deferred",native["escalation"]["status"])
        self.assertTrue(native["escalation"]["notAdoptedAutomatically"])
        conditions={c["id"] for c in read(NATIVE / "conditions.json")["conditions"]}
        self.assertTrue(set(native["conditions"]).issubset(conditions))

if __name__ == "__main__":
    unittest.main()
