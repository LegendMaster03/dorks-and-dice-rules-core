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

    def test_competencies_have_one_native_profile_not_separate_source_facets(self):
        native = read(NATIVE / "competencies.json")["competencies"]
        identities = {c["id"]: c for c in native}
        self.assertEqual(32, len(native))
        self.assertEqual(len(identities), len(native))
        self.assertIn("competency.smithing",
                      {c["id"] for c in read(WORKING / "competencies.json")["competencies"]})
        self.assertNotIn("competency.smithing", identities)
        expected_profile = {
            "proficiency": "binary",
            "level": "progressive",
            "levelScale": "pending-reconciliation",
            "abilityResolution": "per-activity"
        }
        old_facets = {"facets", "trainingIdentity", "trainingProfile",
                      "parentRelationship", "childRelationship"}
        for competency in native:
            self.assertEqual("competency", competency["kind"])
            self.assertEqual(expected_profile, competency["profile"])
            self.assertFalse(old_facets.intersection(competency), competency["id"])
            self.assertEqual(len(competency.get("associatedTools", [])),
                             len(set(competency.get("associatedTools", []))))
        blacksmithing = identities["competency.blacksmithing"]
        self.assertEqual(["competency.armorsmithing", "competency.weaponsmithing"],
                         blacksmithing["childCompetencies"])
        self.assertIn("Smith's Tools", blacksmithing["associatedTools"])
        self.assertEqual(["Alchemist's Supplies"],
                         identities["competency.alchemy"]["associatedTools"])
        self.assertEqual(["skill.forgery"],
                         identities["competency.forgery"]["relatedSkills"])
        # Parent/child links do not establish a check formula or level inheritance.
        self.assertNotIn("composition", blacksmithing)
        dwarf = read(NATIVE / "upbringings/dwarf/general.json")
        dwarf_grant = next(x for x in dwarf["benefits"]
                           if x["id"] == "crafting-tradition")
        self.assertIn("competency.blacksmithing", dwarf_grant["chooseOne"])
        self.assertNotIn("competency.smithing", dwarf_grant["chooseOne"])

    def test_competency_specialization_graph_has_no_cycles_or_dangling_links(self):
        native = read(NATIVE / "competencies.json")["competencies"]
        by_id = {entry["id"]: entry for entry in native}
        children_seen = set()
        for parent in native:
            children = parent.get("childCompetencies", [])
            self.assertEqual(len(children), len(set(children)), parent["id"])
            if children:
                self.assertNotIn("composition", parent)
            for child_id in children:
                self.assertIn(child_id, by_id)
                child = by_id[child_id]
                self.assertEqual(parent["id"], child.get("parentCompetency"))
                self.assertNotIn(child_id, children_seen)
                children_seen.add(child_id)
                self.assertNotIn("composition", parent)
        for entry in native:
            parent_id = entry.get("parentCompetency")
            if parent_id:
                self.assertIn(parent_id, by_id)
                self.assertIn(entry["id"], by_id[parent_id].get("childCompetencies", []))
            seen = set()
            node = entry
            while node.get("parentCompetency"):
                self.assertNotIn(node["id"], seen)
                seen.add(node["id"])
                node = by_id[node["parentCompetency"]]
            self.assertNotIn(node["id"], seen)

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
