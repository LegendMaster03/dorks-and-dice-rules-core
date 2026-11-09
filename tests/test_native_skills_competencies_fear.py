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

    def test_native_smithing_identity_and_retired_item_categories(self):
        native = read(NATIVE / "competencies.json")["competencies"]
        identities = {item["id"]: item for item in native}
        self.assertEqual(len(native), len(identities))
        self.assertIn("competency.smithing", identities)
        smithing = identities["competency.smithing"]
        self.assertEqual("Smithing", smithing["name"])
        self.assertIn("Smith's Tools", smithing["associatedTools"])
        self.assertNotIn("childCompetencies", smithing)
        for retired in ("competency.blacksmithing", "competency.armorsmithing",
                        "competency.weaponsmithing"):
            self.assertNotIn(retired, identities)
        # The historical source is retained as evidence, not deployed as
        # duplicate native character competencies.
        working = {entry["id"] for entry in read(WORKING / "competencies.json")["competencies"]}
        self.assertTrue({"competency.smithing", "competency.blacksmithing",
                         "competency.armorsmithing", "competency.weaponsmithing"}.issubset(working))
        dwarf = read(NATIVE / "upbringings/dwarf/general.json")
        crafting = next(x for x in dwarf["benefits"] if x["id"] == "crafting-tradition")
        self.assertIn("competency.smithing", crafting["chooseOne"])
        self.assertTrue(all(choice in identities for choice in crafting["chooseOne"]))
        # Use Rope remains a Skill pending a separate classification review.
        skills = read(NATIVE / "skills.json")["skills"]
        self.assertIn("skill.use-rope", {x["id"] for x in skills})

    def test_bowmaking_is_an_output_not_a_native_advancement_track(self):
        native = read(NATIVE / "competencies.json")["competencies"]
        identities = {entry["id"]: entry for entry in native}
        self.assertEqual(28, len(native))
        self.assertEqual(28, len(identities))
        self.assertNotIn("competency.bowmaking", identities)
        for retained in ("competency.carpentry", "competency.woodcarving",
                         "competency.shipmaking", "competency.cobbling"):
            self.assertIn(retained, identities)
        working = {entry["id"] for entry in read(WORKING / "competencies.json")["competencies"]}
        self.assertIn("competency.bowmaking", working)

    def test_trapmaking_retires_without_disabling_other_capabilities(self):
        native = read(NATIVE / "competencies.json")["competencies"]
        identities = {entry["id"]: entry for entry in native}
        self.assertNotIn("competency.trapmaking", identities)
        self.assertIn("competency.tinkering", identities)
        self.assertIn("competency.carpentry", identities)
        self.assertIn("competency.smithing", identities)
        self.assertIn("competency.thieves-tools", identities)
        self.assertIn("competency.shipmaking", identities)
        self.assertIn("competency.cobbling", identities)
        historical = {entry["id"] for entry in read(WORKING / "competencies.json")["competencies"]}
        self.assertIn("competency.trapmaking", historical)
        reference = read(ROOT / "data/reference/competency-reconciliation-cases.json")["cases"]
        case = next(x for x in reference if x["id"] == "trapmaking-method-based")
        self.assertFalse(case["expect"]["automaticLevelConversion"])
        self.assertFalse(case["expect"]["automaticProficiencyTransfer"])
        skills = read(NATIVE / "skills.json")["skills"]
        self.assertIn("skill.use-rope", {entry["id"] for entry in skills})

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
