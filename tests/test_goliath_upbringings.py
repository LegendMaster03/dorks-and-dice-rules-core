"""First-pass Goliath and independent giant-cultural Upbringings."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"
WORKING = ROOT / "data/rulesets/first-party-001/working"
UPBRING = NATIVE / "upbringings"
KINDS = ("cloud", "fire", "frost", "hill", "stone", "storm")


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class GoliathUpbringingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.general = read(UPBRING / "goliath/general.json")
        cls.cultures = {k: read(UPBRING / "giant" / (k + ".json")) for k in KINDS}
        cls.skills = {r["id"] for r in read(WORKING / "skills.json")["skills"]}
        cls.competencies = {r["id"] for r in read(WORKING / "competencies.json")["competencies"]}

    def test_seven_independent_playable_upbringings(self):
        self.assertEqual({"general.json"}, {p.name for p in (UPBRING / "goliath").glob("*.json")})
        self.assertEqual(set(KINDS), {p.stem for p in (UPBRING / "giant").glob("*.json")})
        self.assertEqual("upbringing.goliath.general", self.general["id"])
        for slug, record in self.cultures.items():
            self.assertEqual("upbringing.giant." + slug, record["id"])
        records = [self.general, *self.cultures.values()]
        self.assertEqual(7, len({r["id"] for r in records}))
        for record in records:
            self.assertEqual("upbringing", record["kind"])
            self.assertEqual("selected-native", record["status"])
            self.assertTrue(record["selectable"])
            for forbidden in ("parent", "defaultFor", "requiredSpecies", "speciesRestriction"):
                self.assertNotIn(forbidden, record)
            self.assertEqual(3, len(record["benefits"]))

    def test_only_species_contains_default_and_each_subspecies_inherits_it(self):
        species = read(NATIVE / "species/base/goliath.json")
        self.assertEqual(self.general["id"], species["defaultUpbringing"])
        for kind in KINDS:
            sub = read(NATIVE / "species/subspecies/goliath" / (kind + ".json"))
            self.assertEqual("species.goliath", sub["parent"])
            self.assertNotIn("defaultUpbringing", sub)
        policy = read(NATIVE / "rules/species-subspecies-upbringing.json")["upbringings"]
        self.assertFalse(policy["speciesRestrictionsByDefault"])
        self.assertEqual(1, policy["selectedCount"])

    def test_language_skills_and_competencies_are_concrete_and_valid(self):
        self.assertEqual(["gol-kaa"], [b["language"] for b in self.general["benefits"]
                                     if "language" in b])
        for row in self.cultures.values():
            self.assertEqual(["giant"], [b["language"] for b in row["benefits"]
                                       if "language" in b])
        for record in [self.general, *self.cultures.values()]:
            self.assertEqual(1, sum("language" in b for b in record["benefits"]))
            self.assertEqual(1, sum(b.get("grantType") == "competency-training"
                                    for b in record["benefits"]))
            for benefit in record["benefits"]:
                if benefit.get("grantType") in ("competency-training", "skill-training"):
                    self.assertEqual(1, benefit["choiceCount"])
                    allowed = (self.competencies if benefit["grantType"] == "competency-training"
                               else self.skills)
                    self.assertTrue(set(benefit["chooseOne"]).issubset(allowed),
                                    record["id"])
                if "skill" in benefit:
                    self.assertIn(benefit["skill"], self.skills)
                    self.assertEqual(2, benefit["bonus"])
                    self.assertTrue(benefit["scope"])
                    self.assertTrue(benefit["stacksWithTraining"])
                for forbidden in ("feat", "originFeat", "spell", "strikeOfTheGiantsFeat"):
                    self.assertNotIn(forbidden, benefit)
        choice = next(b for b in self.general["benefits"] if b.get("grantType") == "skill-training")
        self.assertEqual({"skill.athletics", "skill.insight", "skill.survival"},
                         set(choice["chooseOne"]))
        self.assertTrue(choice["doesNotAutomaticallyTrainChildSkills"])

    def test_rune_and_giant_combat_powers_not_granted_through_culture(self):
        for record in [self.general, *self.cultures.values()]:
            ids = {b["id"] for b in record["benefits"]}
            self.assertFalse(ids & {"strike-of-the-giants", "rune-shaper",
                                    "giant-ancestry-aptitude"})
            self.assertTrue(all("damage" not in b for b in record["benefits"]))


if __name__ == "__main__":
    unittest.main()
