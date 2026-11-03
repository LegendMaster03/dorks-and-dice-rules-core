"""Goliath Clan and Giant Foundling Upbringings: independent culture and inheritance."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"
WORKING = ROOT / "data/rulesets/first-party-001/working"
UPBRING = NATIVE / "upbringings"
ANCESTRIES = ("cloud", "fire", "frost", "hill", "stone", "storm")


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class GoliathUpbringingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.clan = read(UPBRING / "goliath/clan.json")
        cls.foundling = read(UPBRING / "giant-foundling.json")
        cls.skills = {r["id"] for r in read(WORKING / "skills.json")["skills"]}
        cls.competencies = {
            r["id"] for r in read(WORKING / "competencies.json")["competencies"]
        }

    def test_only_two_selected_goliath_related_upbringings(self):
        self.assertEqual({"clan.json"}, {p.name for p in
                         (UPBRING / "goliath").glob("*.json")})
        self.assertFalse((UPBRING / "giant").exists(),
                         "The six giant-society candidates must not remain native")
        self.assertEqual("upbringing.goliath.clan", self.clan["id"])
        self.assertEqual("Goliath Clan Upbringing", self.clan["name"])
        self.assertEqual("upbringing.giant-foundling", self.foundling["id"])
        self.assertEqual("Giant Foundling Upbringing", self.foundling["name"])
        for record in (self.clan, self.foundling):
            self.assertEqual("upbringing", record["kind"])
            self.assertEqual("selected-native", record["status"])
            self.assertTrue(record["selectable"])
            for forbidden in ("parent", "defaultFor", "requiredSpecies",
                              "speciesRestriction", "feat"):
                self.assertNotIn(forbidden, record)

    def test_goliath_default_is_a_suggestion_not_inherited_culture(self):
        base = read(NATIVE / "species/base/goliath.json")
        self.assertEqual(self.clan["id"], base["defaultUpbringing"])
        for type_name in ANCESTRIES:
            sub = read(NATIVE / "species/subspecies/goliath" / (type_name + ".json"))
            self.assertEqual("species.goliath", sub["parent"])
            self.assertNotIn("defaultUpbringing", sub)
        policy = read(NATIVE / "rules/species-subspecies-upbringing.json")["upbringings"]
        self.assertFalse(policy["speciesRestrictionsByDefault"])
        self.assertEqual(1, policy["selectedCount"])

    def test_goliath_clan_preserves_gol_kaa_and_existing_choices(self):
        self.assertEqual(3, len(self.clan["benefits"]))
        by_id = {benefit["id"]: benefit for benefit in self.clan["benefits"]}
        self.assertEqual("gol-kaa", by_id["gol-kaa"]["language"])
        skill = by_id["clan-training"]
        self.assertEqual("skill-training", skill["grantType"])
        self.assertEqual(1, skill["choiceCount"])
        self.assertTrue(skill["doesNotAutomaticallyTrainChildSkills"])
        self.assertEqual({"skill.athletics", "skill.insight", "skill.survival"},
                         set(skill["chooseOne"]))
        self.assertTrue(set(skill["chooseOne"]) <= self.skills)
        competency = by_id["clan-craft"]
        self.assertEqual("competency-training", competency["grantType"])
        self.assertEqual(1, competency["choiceCount"])
        self.assertEqual({"competency.cooking", "competency.leatherworking",
                          "competency.navigation"}, set(competency["chooseOne"]))
        self.assertTrue(set(competency["chooseOne"]) <= self.competencies)

    def test_giant_foundling_converts_original_cultural_benefits(self):
        benefits = {benefit["id"]: benefit for benefit in self.foundling["benefits"]}
        self.assertEqual({"giant-language", "foundling-additional-language",
                          "foundling-intimidation", "foundling-survival"}, set(benefits))
        self.assertEqual("giant", benefits["giant-language"]["language"])
        self.assertEqual(1, benefits["foundling-additional-language"]["extraLanguages"])
        self.assertEqual({"skill.intimidation", "skill.survival"},
                         {benefit["skill"] for benefit in benefits.values()
                          if benefit.get("grantType") == "skill-training"})
        self.assertTrue({benefit["skill"] for benefit in benefits.values()
                         if "skill" in benefit} <= self.skills)
        self.assertFalse(any("gol-kaa" in str(x) for x in benefits.values()))
        self.assertTrue(all("feat" not in benefit and "spell" not in benefit
                            and "damage" not in benefit for benefit in benefits.values()))

    def test_no_giant_foundling_strike_feat_or_ancestry_duplication(self):
        for record in (self.clan, self.foundling):
            self.assertNotIn("grantsFeat", record)
            self.assertNotIn("feat", record)
            self.assertNotIn("sizeChange", record)
            for benefit in record["benefits"]:
                self.assertNotIn(benefit["id"], {
                    "strike-of-the-giants", "rune-shaper", "giant-ancestry-aptitude"
                })
                self.assertNotIn("grantsFeat", benefit)
                self.assertNotIn("giantAncestry", benefit)


if __name__ == "__main__":
    unittest.main()
