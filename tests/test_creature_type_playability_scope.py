"""Universal creature classification versus monster/NPC stat-block default scope."""
import json
from pathlib import Path
import unittest

NATIVE = Path(__file__).resolve().parents[1] / "data/rulesets/first-party-001/native"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class CreatureTypePlayableSpeciesScopeTests(unittest.TestCase):
    def test_all_types_are_permitted_and_classification_is_universal(self):
        type_catalog = read(NATIVE / "creature-types.json")
        policy = next(e for e in type_catalog["entries"]
                      if e["id"] == "rule.creature-type-inheritance")
        scope = policy["applicationScope"]
        self.assertEqual("all-creatures-including-player-characters",
                         scope["creatureTypeClassification"])
        self.assertEqual("all-creatures-based-on-explicit-effective-type",
                         scope["typeBasedEffectEligibility"])
        playable = scope["playableSpecies"]
        self.assertEqual("all-defined-creature-types", playable["permittedCreatureTypes"])
        self.assertTrue(playable["noMonsterTypeExclusion"])
        self.assertEqual("species-and-required-ancestry-selection",
                         playable["eligibilityDeterminedBy"])

    def test_type_defaults_do_not_auto_grant_to_player_characters(self):
        data = read(NATIVE / "creature-types.json")
        policy = next(e for e in data["entries"]
                      if e["id"] == "rule.creature-type-inheritance")
        scope = policy["applicationScope"]["monsterAndNpcStatblockDefaults"]
        self.assertEqual("monster-or-npc-stat-block-construction",
                         scope["appliesDuring"])
        self.assertTrue(scope["doesNotAutomaticallyGrantToPlayableCharacters"])
        self.assertTrue(scope["existingMonsterStatblocksUnchanged"])
        self.assertEqual({"hitDie", "senses", "needs", "savingThrowDefaults",
                          "weaponCompetencies", "naturalWeaponCompetency",
                          "damageModifiers", "conditionImmunities"},
                         set(scope["fields"]))
        outsider = next(e for e in data["entries"]
                        if e["id"] == "creature-type.outsider")
        self.assertEqual(8, outsider["hitDie"])
        self.assertEqual({"food": False, "sleep": False}, outsider["needs"])
        tiefling = read(NATIVE / "species/base/tiefling.json")
        self.assertEqual("fiend", tiefling["creatureType"])
        self.assertEqual({"breathing": True, "food": True, "sleep": True},
                         tiefling["needs"])
        self.assertNotIn("hitDie", tiefling)
        self.assertNotIn("darkvisionFeet", tiefling)


if __name__ == "__main__":
    unittest.main()
