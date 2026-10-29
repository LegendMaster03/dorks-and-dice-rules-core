"""Structural tests of the native Skills/Competencies data boundary."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data" / "rulesets" / "first-party-001" / "working"
CONVERSIONS = ROOT / "data" / "conversions"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class NativeCatalogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.skills = read(NATIVE / "skills.json")["skills"]
        cls.comps = read(NATIVE / "competencies.json")["competencies"]
        cls.s = {x["id"]: x for x in cls.skills}
        cls.c = {x["id"]: x for x in cls.comps}
        cls.mappings = read(CONVERSIONS / "skill-name-mappings.json")["mappings"]
        cls.audit = read(ROOT / "data" / "reference" / "skill-reconciliation-audit.json")["reviewed"]

    def test_catalog_structure_and_no_duplicate_ids(self):
        self.assertFalse((NATIVE / "skills").exists())
        self.assertFalse((NATIVE / "competencies").exists())
        self.assertEqual(len(self.skills), len(self.s))
        self.assertEqual(len(self.comps), len(self.c))
        self.assertEqual(88, len(self.skills))
        self.assertEqual(33, len(self.comps))
        self.assertTrue(all(k.startswith("skill.") for k in self.s))
        self.assertTrue(all(k.startswith("competency.") for k in self.c))

    def test_core_skill_records_contain_no_history_or_import_fields(self):
        banned = {"source", "edition", "historical", "sourceAliases",
                  "reviewedHistoricalAliases", "srd", "srd52",
                  "upstreamKey", "sourceId", "origin", "sourceRevision"}
        for skill in self.skills:
            self.assertFalse(set(skill) & banned, skill["id"])
            self.assertIn(skill["kind"], {"skill", "skill-family"})

    def test_eighteen_srd_based_skill_descriptions(self):
        with_description = [x for x in self.skills if "description" in x]
        self.assertEqual(18, len(with_description))
        for skill in with_description:
            self.assertTrue(skill["description"])
            self.assertIn(skill["defaultAbility"],
                          {"strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma"})

    def test_adopted_skill_audit_is_accounted_for(self):
        self.assertEqual(45, len(self.audit))
        conv = {(x["externalName"], x["nativeSkill"]) for x in self.mappings}
        for entry in self.audit:
            disposition = entry["disposition"]
            if disposition in ("native-skill", "native-skill-family"):
                self.assertIn(entry["nativeSkill"], self.s)
            elif disposition == "import-alias":
                self.assertIn((entry["name"], entry["nativeSkill"]), conv)
            elif disposition == "native-competency":
                self.assertIn(entry["nativeCompetency"], self.c)
            elif disposition in ("competency-crafting-family", "language-proficiency"):
                pass
            else:
                self.fail("Unknown disposition " + disposition)
        for name in ("skill.psionics", "skill.the-planes"):
            self.assertIn(name, self.s)
        self.assertNotIn("skill.speak-language", self.s)

    def test_all_composites_are_skills_not_competencies(self):
        parents = {"perception": ["listen", "spot"], "stealth": ["hide", "move-silently"],
                   "acrobatics": ["balance", "tumble"],
                   "athletics": ["climb", "jump", "swim"]}
        for key, children in parents.items():
            entity = self.s["skill." + key]
            self.assertEqual(["skill." + ch for ch in children], entity["childSkills"])
            self.assertEqual("arithmetic-mean", entity["composition"]["operation"])
            self.assertEqual("truncate-toward-zero", entity["composition"]["rounding"])
            self.assertEqual("before-mean", entity["composition"]["componentModifiers"])
            self.assertEqual("after-mean", entity["composition"]["parentModifiers"])
            self.assertNotIn("competency." + key, self.c)
            for ch in children:
                child = self.s["skill." + ch]
                self.assertEqual("skill." + key, child["parentSkill"])
                self.assertTrue(child["independentlyRanked"])
                self.assertNotIn("competency." + ch, self.c)

    def test_real_competencies_are_crafting_or_tool_training(self):
        crafting = [x for x in self.comps if any(f["type"] == "crafting-skill" for f in x["facets"])]
        self.assertEqual(20, len(crafting))
        self.assertTrue(all(any(f["type"] in {"crafting-skill","tool-proficiency","skill"} for f in x["facets"])
                            for x in self.comps))
        for key, tool in [("alchemy","Alchemist's Supplies"), ("carpentry","Carpenter's Tools")]:
            facets = self.c["competency." + key]["facets"]
            self.assertIn("crafting-skill", [f["type"] for f in facets])
            self.assertIn(tool, [f.get("tool") for f in facets])
        smith = self.c["competency.smithing"]["facets"]
        self.assertEqual(["tool-proficiency"], [x["type"] for x in smith])
        self.assertEqual("skill.forgery",
                         self.c["competency.forgery"]["facets"][0]["skill"])

    def test_advanced_skill_specialties_preserved(self):
        for family, count in [("perform", 9), ("profession", 25)]:
            self.assertEqual("skill-family", self.s["skill."+family]["kind"])
            members = [x for x in self.skills if x.get("family") == "skill."+family]
            self.assertEqual(count, len(members))
            self.assertTrue(all(x["independentlyRanked"] for x in members))
        self.assertNotIn("skill.craft", self.s)


if __name__ == "__main__":
    unittest.main()
