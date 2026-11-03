"""First-pass native Goliath data-contract and SRD 5.2.1 ancestry regression checks."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/rulesets/first-party-001/native"
BASE = NATIVE / "species/base/goliath.json"
LEAVES = NATIVE / "species/subspecies/goliath"
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
        self.assertEqual("humanoid", self.base["creatureType"])
        self.assertEqual("medium", self.base["size"])
        self.assertEqual(35, self.base["walkingSpeedFeet"])
        self.assertNotIn("defaultUpbringing", self.base)
        self.assertEqual(
            {"powerful-build", "large-form"},
            {t["id"] for t in self.base["traits"]})
        self.assertEqual(set(self.slugs),
                         {p.stem for p in LEAVES.glob("*.json")})
        ids = set()
        for slug, leaf in self.leaves.items():
            self.assertEqual("subspecies.goliath." + slug, leaf["id"])
            self.assertNotIn(leaf["id"], ids)
            ids.add(leaf["id"])
            self.assertEqual("subspecies", leaf["kind"])
            self.assertEqual("selected-native", leaf["status"])
            self.assertTrue(leaf["selectable"])
            self.assertEqual(self.base["id"], leaf["parent"])
            self.assertEqual(1, len(leaf["traits"]))
            self.assertNotIn("defaultUpbringing", leaf)
            self.assertNotIn("creatureType", leaf)
            self.assertNotIn("walkingSpeedFeet", leaf)

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
