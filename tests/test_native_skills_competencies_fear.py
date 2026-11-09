"""Native content invariants for stable Skills and fear rules.

Competency schema assertions are deferred while the native data is under design.
"""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"
WORKING = ROOT / "data/rulesets/first-party-001/working"

def read(path):
    return json.loads(path.read_text(encoding="utf-8"))

class PromotedSkillsAndFearTests(unittest.TestCase):
    def test_native_skills_preserve_source_except_reconciled_forgery(self):
        native = read(NATIVE / "skills.json")["skills"]
        source = read(WORKING / "skills.json")["skills"]
        # Historical source Skills remain intact; native Forgery is one Competency.
        self.assertEqual([s for s in source if s["id"] != "skill.forgery"], native)
        self.assertEqual(87, len(native))
        self.assertIn("skill.forgery", {s["id"] for s in source})
        competencies = read(NATIVE / "competencies.json")["competencies"]
        self.assertIn("competency.forgery", {c["id"] for c in competencies})
        self.assertNotIn("skill.forgery", {s["id"] for s in native})
        forgery = next(c for c in competencies if c["id"] == "competency.forgery")
        self.assertNotIn("relatedSkills", forgery)
        self.assertEqual(["Forgery Kit"], forgery["associatedTools"])
        identities = {s["id"]: s for s in native}
        self.assertEqual(len(native), len(identities))
        for parent in ("skill.perception","skill.stealth","skill.acrobatics","skill.athletics"):
            self.assertIn(parent, identities)
            self.assertEqual("arithmetic-mean", identities[parent]["composition"]["operation"])
            for child in identities[parent]["childSkills"]:
                self.assertIn(child,identities)

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
