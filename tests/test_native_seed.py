"""Validate provisional native skill and competency JSON seed without a runtime."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SKILLS = ROOT / "data" / "native" / "skills"
COMPETENCIES = ROOT / "data" / "native" / "competencies"


def load_catalog(path):
    result = {}
    for item in sorted(path.glob("*.json")):
        entity = json.loads(item.read_text(encoding="utf-8"))
        if entity["id"] in result:
            raise AssertionError("Duplicate native identity: " + entity["id"])
        expected = ("skill." if path == SKILLS else "competency.") + item.stem
        if entity["id"] != expected:
            raise AssertionError(f"{item}: expected id {expected}, got {entity['id']}")
        result[entity["id"]] = entity
    return result


class NativeSeedChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.skills = load_catalog(SKILLS)
        cls.competencies = load_catalog(COMPETENCIES)

    def test_eighteen_unique_licensed_candidate_skills(self):
        self.assertEqual(18, len(self.skills))
        allowed_abilities = {"strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma"}
        for entity in self.skills.values():
            self.assertEqual("skill", entity["kind"])
            self.assertIn(entity["defaultAbility"], allowed_abilities)
            self.assertTrue(entity["summary"])
            self.assertTrue(entity["details"])
            self.assertIn("provisional", entity["baselineStatus"])
            for disallowed in ["srd", "srd52", "source", "PHB"]:
                self.assertNotIn(disallowed, entity)

    def test_skill_facing_competencies_link_to_native_skills(self):
        references = [x["nativeSkill"] for x in self.competencies.values()
                      if x.get("nativeSkill") is not None]
        self.assertEqual(18, len(references))
        self.assertEqual(set(self.skills), set(references))

    def test_composite_requirements_and_no_unresolved_component_id(self):
        comps = {
            "competency.acrobatics": ["competency.balance", "competency.tumble"],
            "competency.athletics": ["competency.climb", "competency.jump", "competency.swim"],
            "competency.perception": ["competency.listen", "competency.spot"],
            "competency.stealth": ["competency.hide", "competency.move-silently"],
        }
        for parent, expected in comps.items():
            rule = self.competencies[parent]["composite"]
            self.assertEqual(expected, rule["components"])
            self.assertEqual("arithmetic-mean", rule["operation"])
            self.assertEqual("truncate-toward-zero", rule["rounding"])
            for component in expected:
                self.assertIn(component, self.competencies)
                self.assertTrue(self.competencies[component]["independentRankedState"])

    def test_historical_identifications_and_families(self):
        self.assertIn("Bluff", self.competencies["competency.deception"]["reviewedHistoricalAliases"])
        self.assertIn("Handle Animal", self.competencies["competency.animal-handling"]["reviewedHistoricalAliases"])
        self.assertIn("Alchemist's Supplies",
            self.competencies["competency.alchemy"]["reviewedHistoricalAliases"])
        for family, ability, trained in [
            ("craft", "intelligence", False), ("perform", "charisma", False),
            ("profession", "wisdom", True)
        ]:
            entity = self.competencies["competency." + family]
            self.assertEqual("competency-family", entity["kind"])
            self.assertEqual(ability, entity["defaultProfile"]["defaultAbility"])
            self.assertEqual(trained, entity["defaultProfile"]["trainedOnly"])
            self.assertTrue(entity["independentlyRankedChildren"])


if __name__ == "__main__":
    unittest.main()
