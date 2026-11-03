"""First-pass native Goliath data-contract and SRD 5.2.1 ancestry regression checks."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"
BASE = NATIVE / "species/base/goliath.json"
LEAVES = NATIVE / "species/ancestry/goliath"
WORKING = ROOT / "data/rulesets/first-party-001/working"
MANIFEST = ROOT / "data/reference/srd/native-coverage-manifest.json"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class GoliathSpeciesReconciliationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.base = read(BASE)
        cls.slugs = ("cloud", "fire", "frost", "hill", "stone", "storm")
        cls.leaves = {slug: read(LEAVES / (slug + ".json"))
                      for slug in cls.slugs}

    def test_shared_species_is_authoritative_and_inherited(self):
        self.assertEqual("species.goliath", self.base["id"])
        self.assertEqual("species", self.base["kind"])
        self.assertEqual("selected-native", self.base["status"])
        self.assertFalse(self.base["selectable"])
        self.assertEqual("giant", self.base["creatureType"])
        self.assertEqual("medium", self.base["size"])
        self.assertEqual(35, self.base["walkingSpeedFeet"])
        self.assertEqual("upbringing.goliath.general", self.base["defaultUpbringing"])
        self.assertEqual(
            {"powerful-build", "large-form", "mountain-leap", "altitude-acclimation", "giant-ancestry-aptitude"},
            {t["id"] for t in self.base["traits"]})
        self.assertEqual(set(self.slugs),
                         {p.stem for p in LEAVES.glob("*.json")})
        ids = set()
        for slug, leaf in self.leaves.items():
            self.assertEqual("ancestry.goliath." + slug, leaf["id"])
            self.assertNotIn(leaf["id"], ids)
            ids.add(leaf["id"])
            self.assertEqual("ancestry", leaf["kind"])
            self.assertEqual("selected-native", leaf["status"])
            self.assertTrue(leaf["selectable"])
            self.assertEqual(self.base["id"], leaf["parent"])
            self.assertGreaterEqual(len(leaf["traits"]), 3)
            self.assertEqual(1, sum(x.get("uses") == {
                "value": "proficiency-bonus", "recharge": "long-rest"
            } for x in leaf["traits"] if x.get("requiredCharacterLevel", 1) == 1))
            self.assertNotIn("defaultUpbringing", leaf)
            self.assertNotIn("creatureType", leaf)
            self.assertNotIn("walkingSpeedFeet", leaf)

    def test_approved_mountain_leap_and_altitude_acclimation(self):
        traits = {t["id"]: t for t in self.base["traits"]}
        jump = traits["mountain-leap"]
        self.assertEqual(["long-jump", "high-jump"], jump["standingJumpAsRunning"])
        self.assertTrue(jump["ignoresRunningApproachRequirement"])
        self.assertEqual("normal-jump-movement-cost", jump["movementCost"])
        self.assertIn("additional-movement", jump["doesNotGrant"])
        self.assertIn("immunity-to-falling-damage", jump["doesNotGrant"])
        altitude = traits["altitude-acclimation"]
        self.assertTrue(altitude["naturallyAcclimated"])
        self.assertIsNone(altitude["altitudeLimitFeet"])
        self.assertEqual(20000, altitude["includesElevationAboveFeet"])
        self.assertTrue(altitude["maintainsAcclimationAtLowerElevations"])
        self.assertEqual("penalties-from-unacclimated-high-altitude-travel",
                         altitude["ignores"])
        self.assertIn("cold-damage-resistance", altitude["doesNotGrant"])
        self.assertIn("suffocation-immunity", altitude["doesNotGrant"])
        self.assertFalse((WORKING / "species/base/goliath.json").exists())

    def test_powerful_build_and_large_form_are_distinct(self):
        traits = {t["id"]: t for t in self.base["traits"]}
        powerful = traits["powerful-build"]
        self.assertEqual("condition.grappled",
                         powerful["advantageOnAbilityChecksToEndCondition"])
        self.assertEqual(1, powerful["effectiveSizeCategoriesHigherForCarryingCapacity"])
        self.assertIn("actual-size", powerful["doesNotChange"])
        large = traits["large-form"]
        self.assertEqual(5, large["requiredCharacterLevel"])
        self.assertEqual({"action": "bonus-action", "requiresSpaceFor": "large"},
                         large["activation"])
        self.assertEqual({"to": "large", "actualSize": True}, large["sizeChange"])
        self.assertEqual(10, large["duration"]["minutes"])
        self.assertTrue(large["duration"]["canEndEarly"])
        self.assertEqual("none", large["duration"]["endEarlyAction"])
        self.assertEqual("strength", large["advantageOnChecks"]["ability"])
        self.assertEqual(10, large["walkingSpeedBonusFeet"])
        self.assertEqual({"value": 1, "recharge": "long-rest"}, large["uses"])

    def test_six_srd_ancestry_boons(self):
        traits = {name: leaf["traits"][0] for name, leaf in self.leaves.items()}
        for name, trait in traits.items():
            self.assertEqual(
                {"value": "proficiency-bonus", "recharge": "long-rest"},
                trait["uses"], name)
        cloud = traits["cloud"]
        self.assertEqual("bonus-action", cloud["activation"]["action"])
        self.assertEqual(30, cloud["teleport"]["maximumDistanceFeet"])
        self.assertEqual("unoccupied-space-you-can-see", cloud["teleport"]["destination"])
        fire = traits["fire"]
        self.assertEqual({"dice": "1d10", "damageType": "fire", "target": "same-target"},
                         fire["extraDamage"])
        frost = traits["frost"]
        self.assertEqual("1d6", frost["extraDamage"]["dice"])
        self.assertEqual("cold", frost["extraDamage"]["damageType"])
        self.assertEqual({"feet": 10, "target": "same-target",
                          "endsAt": "start-of-your-next-turn"}, frost["speedReduction"])
        hill = traits["hill"]
        self.assertEqual("large", hill["targetMaximumSize"])
        self.assertEqual("condition.prone", hill["applyCondition"])
        self.assertFalse(hill["additionalSavingThrow"])
        stone = traits["stone"]
        self.assertEqual("reaction", stone["activation"]["action"])
        self.assertEqual({"dice": "1d12", "addAbilityModifier": "constitution",
                          "appliesTo": "triggering-damage"}, stone["damageReduction"])
        storm = traits["storm"]
        self.assertEqual("reaction", storm["activation"]["action"])
        self.assertEqual(60, storm["rangeFeet"])
        self.assertEqual({"dice": "1d8", "damageType": "thunder",
                          "target": "triggering-creature"}, storm["retaliation"])
        conditions = {c["id"] for c in read(NATIVE / "conditions.json")["conditions"]}
        self.assertIn("condition.prone", conditions)
        self.assertIn("condition.grappled", conditions)

    def test_bigbys_first_pass_level_1_strike_modes_do_not_duplicate_original_charges(self):
        base_traits = {t["id"]: t for t in self.base["traits"]}
        aptitude = base_traits["giant-ancestry-aptitude"]
        self.assertEqual(["strength", "constitution"], aptitude["abilityChoice"]["from"])
        self.assertEqual("goliath-ancestry-selection", aptitude["abilityChoice"]["when"])
        self.assertEqual("8 + proficiency-bonus + chosen-giant-ancestry-ability-modifier",
                         aptitude["savingThrowDC"])
        ids = {"cloud": "clouds-jaunt", "fire": "fires-burn",
               "frost": "frosts-chill", "hill": "hills-tumble",
               "stone": "stones-endurance", "storm": "storms-thunder"}
        for slug, leaf in self.leaves.items():
            original = leaf["traits"][0]
            self.assertEqual(ids[slug], original["id"])
            self.assertEqual({"value": "proficiency-bonus", "recharge": "long-rest"},
                             original["uses"])
            self.assertEqual(0, sum("uses" in trait for trait in leaf["traits"][1:]
                                    if trait.get("requiredCharacterLevel") == 1))
            self.assertEqual(len(leaf["traits"]),
                             len({trait["id"] for trait in leaf["traits"]}))
            for trait in leaf["traits"]:
                self.assertLessEqual(trait.get("requiredCharacterLevel", 1), 4)
        for slug in ("cloud", "stone", "storm"):
            t = next(t for t in self.leaves[slug]["traits"] if t["id"] == slug + "-strike")
            self.assertEqual(1, t["requiredCharacterLevel"])
            self.assertTrue(t["oncePerTurn"])
            self.assertTrue(t["extraUsesGranted"] is False)
            self.assertEqual(ids[slug], t["sharedUsesFromTrait"])
            self.assertEqual("melee-weapon-or-thrown-weapon-attack-hit", t["trigger"])
        fire = self.leaves["fire"]["traits"][0]
        self.assertEqual("fire-strike", fire["coversStrikeOfTheGiantsMode"])
        self.assertFalse(fire["duplicateStrikeBenefitGranted"])
        frost = self.leaves["frost"]["traits"][0]["alternateStrikeMode"]
        self.assertEqual("frost-strike", frost["id"])
        self.assertEqual("frosts-chill", frost["sharedUsesFromTrait"])
        self.assertEqual("frosts-chill-speed-reduction", frost["replaces"])
        self.assertTrue(frost["extraDamageAlreadyProvidedByParent"])
        self.assertEqual(0, frost["onFailedSave"]["speedSetFeet"])
        hill = self.leaves["hill"]["traits"][0]["alternateStrikeMode"]
        self.assertEqual("hill-strike", hill["id"])
        self.assertEqual("hills-tumble", hill["sharedUsesFromTrait"])
        self.assertEqual("hills-tumble-automatic-prone", hill["replaces"])
        self.assertEqual("1d6", hill["extraDamage"]["dice"])
        self.assertIsNone(hill["targetMaximumSize"])

    def test_advanced_bigbys_features_unlock_only_at_level_4(self):
        expected = {
            "cloud": {"cloudy-escape"},
            "fire": {"born-of-flame", "searing-ignition"},
            "frost": {"born-of-ice", "frigid-retaliation"},
            "hill": {"bulwark", "iron-stomach"},
            "stone": {"cavernous-sight", "stone-throw"},
            "storm": {"maelstrom-aura"},
        }
        for slug, ids in expected.items():
            advanced = [t for t in self.leaves[slug]["traits"]
                        if t.get("requiredCharacterLevel") == 4]
            self.assertEqual(ids, {t["id"] for t in advanced})
            for t in advanced:
                self.assertNotIn("abilityScoreIncrease", t)
                self.assertNotIn("featPrerequisite", t)
                self.assertNotIn("strikeOfTheGiantsFeat", t)
        fire = {t["id"]: t for t in self.leaves["fire"]["traits"]}
        frost = {t["id"]: t for t in self.leaves["frost"]["traits"]}
        self.assertEqual({"percent": 50, "damageType": "fire"},
                         fire["born-of-flame"]["damageResistance"])
        self.assertEqual({"percent": 50, "damageType": "cold"},
                         frost["born-of-ice"]["damageResistance"])
        ignition = fire["searing-ignition"]
        self.assertEqual("one-attack", ignition["activation"]["replaces"])
        self.assertEqual("condition.blinded",
                         ignition["applyConditionOnFailedSave"]["condition"])
        self.assertTrue(ignition["oncePerTurn"])
        self.assertEqual({"value": "proficiency-bonus", "recharge": "long-rest"},
                         ignition["uses"])
        self.assertEqual(0, frost["frigid-retaliation"]["onFailedSave"]["speedSetFeet"])
        self.assertTrue(next(t for t in self.leaves["hill"]["traits"]
                             if t["id"] == "bulwark")["atWill"])
        self.assertTrue(next(t for t in self.leaves["hill"]["traits"]
                             if t["id"] == "iron-stomach")["oncePerShortRest"])
        stone = {t["id"]: t for t in self.leaves["stone"]["traits"]}
        self.assertEqual(60, stone["cavernous-sight"]["addToExistingDarkvisionFeet"])
        self.assertEqual("ranged-spell-attack", stone["stone-throw"]["attack"]["type"])
        storm = next(t for t in self.leaves["storm"]["traits"]
                     if t["id"] == "maelstrom-aura")
        self.assertEqual("total-cover", storm["area"]["blockedBy"])
        self.assertEqual({"lightning", "thunder"},
                         {r["damageType"] for r in storm["whileActive"]["damageResistance"]})
        self.assertTrue(all(r["percent"] == 50 for r in
                            storm["whileActive"]["damageResistance"]))
        self.assertEqual(0.5, storm["startsTurnInside"]["onFailedSave"]["speedMultiplier"])
        conditions = {x["id"] for x in read(NATIVE / "conditions.json")["conditions"]}
        self.assertIn("condition.blinded", conditions)
        self.assertIn("condition.incapacitated", conditions)

    def test_original_srd_candidate_promoted_without_loss_of_source_provenance(self):
        row = next(r for r in read(MANIFEST)["coverage"]
                   if r["nativeId"] == "species.goliath")
        self.assertEqual("race", row["kind"])
        self.assertEqual("approved-native-reconciled", row["status"])
        self.assertEqual("data/rulesets/first-party-001/native/species/base/goliath.json",
                         row["path"])
        self.assertEqual(1, row["sourceRecords"])
        self.assertFalse((WORKING / "species/base/goliath.json").exists())


if __name__ == "__main__":
    unittest.main()
