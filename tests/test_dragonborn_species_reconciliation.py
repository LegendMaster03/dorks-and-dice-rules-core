"""Data-contract regression checks for selected Dragonborn Species and Subspecies.

These validate native JSON and inheritance, not the pending runtime evaluator.
"""
import json
from pathlib import Path
import unittest

NATIVE = Path(__file__).resolve().parents[1] / "data/rulesets/first-party-001/native"
DRAGONBORN = NATIVE / "species"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


class DragonbornReconciliationTests(unittest.TestCase):
    def test_selected_families_and_fifteen_leaves(self):
        base = read(DRAGONBORN / "base/dragonborn.json")
        self.assertEqual("species.dragonborn", base["id"])
        self.assertEqual("dragon", base["creatureType"])
        self.assertEqual(30, base["walkingSpeedFeet"])
        self.assertFalse(base["selectable"])
        families = {
            "chromatic": ("black", "blue", "green", "red", "white"),
            "metallic": ("brass", "bronze", "copper", "gold", "silver"),
            "gem": ("amethyst", "crystal", "emerald", "sapphire", "topaz"),
        }
        ids = {base["id"]}
        for family, names in families.items():
            with self.subTest(family=family):
                node = read(DRAGONBORN / f"ancestry/dragonborn/{family}.json")
                self.assertEqual("species.dragonborn", node["parent"])
                self.assertFalse(node["selectable"])
                self.assertEqual("selected-native", node["status"])
                self.assertNotIn("defaultUpbringing", node)
                self.assertNotIn(node["id"], ids)
                ids.add(node["id"])
                for name in names:
                    leaf = read(DRAGONBORN / f"ancestry/dragonborn/{family}/{name}.json")
                    self.assertEqual(node["id"], leaf["parent"])
                    self.assertTrue(leaf["selectable"])
                    self.assertEqual("selected-native", leaf["status"])
                    self.assertEqual("ancestry", leaf["kind"])
                    if family == "gem":
                        self.assertEqual([], leaf["traits"])
                        self.assertEqual(2, len(node["traits"]))
                    else:
                        self.assertGreaterEqual(len(leaf["traits"]), 1)
                    self.assertNotIn("defaultUpbringing", leaf)
                    self.assertNotIn(leaf["id"], ids)
                    ids.add(leaf["id"])
        self.assertEqual(19, len(ids))

    def test_gem_family_and_five_leaf_mechanics(self):
        gem = read(DRAGONBORN / "ancestry/dragonborn/gem.json")
        base = read(DRAGONBORN / "base/dragonborn.json")
        self.assertEqual("species.dragonborn", gem["parent"])
        self.assertFalse(gem["selectable"])
        self.assertEqual("selected-native", gem["status"])
        self.assertEqual({"shape": "cone", "lengthFeet": 15},
                         gem["breathWeaponArea"])
        self.assertNotIn("defaultUpbringing", gem)
        self.assertNotIn("damageType", gem)
        t = {x["id"]: x for x in gem["traits"]}
        self.assertEqual({"psionic-mind", "gem-flight"}, set(t))
        mind = t["psionic-mind"]
        self.assertTrue(mind["atWill"])
        self.assertEqual("telepathic-message", mind["communicationMode"])
        self.assertEqual(30, mind["rangeFeet"])
        self.assertEqual(1, mind["targetCount"])
        self.assertTrue(mind["requiresSight"])
        self.assertTrue(mind["requiresRecipientLanguage"])
        self.assertFalse(mind["requiresSharedLanguage"])
        self.assertTrue(mind["oneWay"])
        self.assertFalse(mind["grantsThoughtReading"])
        flight = t["gem-flight"]
        self.assertEqual(5, flight["requiredCharacterLevel"])
        self.assertEqual("bonus-action", flight["activation"]["action"])
        self.assertEqual({"minutes": 1}, flight["duration"])
        self.assertEqual(1, flight["uses"]["value"])
        self.assertEqual("long-rest", flight["uses"]["recharge"])
        self.assertEqual("breath-weapon", flight["uses"]["independentOf"])
        self.assertEqual({"equalTo": "current-walking-speed"},
                         flight["flyingSpeed"])
        self.assertTrue(flight["canHover"])
        self.assertFalse(flight["grantsPermanentFlight"])
        self.assertEqual("dragon", base["creatureType"])
        self.assertEqual("selected-dragonborn-family-or-leaf",
                         next(x for x in base["traits"]
                              if x["id"] == "breath-weapon")["areaFrom"])
        expected = {
            "amethyst": "force", "crystal": "radiant",
            "emerald": "psychic", "sapphire": "thunder",
            "topaz": "necrotic",
        }
        folder = DRAGONBORN / "ancestry/dragonborn/gem"
        self.assertEqual(set(expected), {p.stem for p in folder.glob("*.json")})
        for slug, affinity in expected.items():
            with self.subTest(slug=slug):
                leaf = read(folder / (slug + ".json"))
                self.assertEqual("ancestry.dragonborn." + slug, leaf["id"])
                self.assertTrue(leaf["selectable"])
                self.assertEqual("selected-native", leaf["status"])
                self.assertEqual(gem["id"], leaf["parent"])
                self.assertEqual(affinity, leaf["damageType"])
                self.assertEqual([], leaf["traits"])
                self.assertNotIn("defaultUpbringing", leaf)

    def test_dragonborn_inherits_unrestricted_low_light_vision(self):
        base = read(DRAGONBORN / "base/dragonborn.json")
        rule = read(NATIVE / "rules/low-light-vision.json")
        self.assertNotIn("darkvisionFeet", base)
        vision = next(t for t in base["traits"] if t["id"] == "low-light-vision")
        self.assertEqual(rule["id"], vision["rule"])
        self.assertEqual("dim", rule["effects"][0]["actualLightLevel"])
        self.assertEqual("bright", rule["effects"][0]["perceivedLightLevel"])
        self.assertTrue(any("No separate Low-Light Vision range" in x
                            for x in rule["clarifications"]))
        for family in ("chromatic", "metallic", "gem"):
            root = DRAGONBORN / "ancestry/dragonborn"
            node = read(root / (family + ".json"))
            self.assertNotIn("darkvisionFeet", node)
            self.assertNotIn("lowLightVision", node)
            for file in (root / family).glob("*.json"):
                leaf = read(file)
                self.assertNotIn("darkvisionFeet", leaf, file.name)
                self.assertNotIn("lowLightVision", leaf, file.name)

    def test_family_breath_geometry_and_resources(self):
        chromatic = read(DRAGONBORN / "ancestry/dragonborn/chromatic.json")
        metallic = read(DRAGONBORN / "ancestry/dragonborn/metallic.json")
        self.assertEqual({"shape": "line", "lengthFeet": 30, "widthFeet": 5}, chromatic["breathWeaponArea"])
        self.assertEqual({"shape": "cone", "lengthFeet": 15}, metallic["breathWeaponArea"])
        breath = next(t for t in metallic["traits"] if t["id"] == "metallic-breath-weapon")
        self.assertEqual(5, breath["requiredCharacterLevel"])
        self.assertEqual({"action": "attack", "replaces": "one-attack", "timing": "on-your-turn"}, breath["activation"])
        self.assertEqual(1, breath["uses"]["value"])
        self.assertEqual("long-rest", breath["uses"]["recharge"])
        self.assertEqual("breath-weapon", breath["uses"]["independentOf"])
        self.assertEqual("on-use", breath["choiceTiming"])
        self.assertEqual(2, len(breath["options"]))
        enervating, repulsion = breath["options"]
        self.assertEqual("constitution", enervating["savingThrow"]["ability"])
        self.assertEqual("condition.incapacitated", enervating["onFailure"]["applyCondition"])
        self.assertEqual("start-of-your-next-turn", enervating["onFailure"]["endsAt"])
        self.assertEqual("strength", repulsion["savingThrow"]["ability"])
        self.assertEqual(20, repulsion["onFailure"]["pushFeet"])
        self.assertEqual("condition.prone", repulsion["onFailure"]["applyCondition"])
        self.assertEqual(
            {o["savingThrow"]["dc"] for o in breath["options"]},
            {"8 + proficiency-bonus + constitution-modifier"})
        conditions = {c["id"] for c in read(NATIVE / "conditions.json")["conditions"]}
        self.assertIn(enervating["onFailure"]["applyCondition"], conditions)
        self.assertIn(repulsion["onFailure"]["applyCondition"], conditions)

    def test_metallic_leaf_types_and_adaptations(self):
        expected = {"brass": "fire", "bronze": "lightning", "copper": "acid", "gold": "fire", "silver": "cold"}
        leaf = lambda name: read(DRAGONBORN / f"ancestry/dragonborn/metallic/{name}.json")
        for name, damage in expected.items():
            self.assertEqual(damage, leaf(name)["damageType"])
        brass = leaf("brass")
        self.assertEqual(15, brass["burrowingSpeedFeet"])
        self.assertEqual(["loose-natural-sand"], brass["traits"][0]["allowedMaterials"])
        self.assertFalse(brass["traits"][0]["leavesPermanentTraversableTunnel"])
        for name in ("bronze", "gold"):
            self.assertEqual(["air", "water"], leaf(name)["traits"][0]["canBreathe"])
            self.assertFalse(leaf(name)["traits"][0]["grantsSwimSpeed"])
        self.assertEqual(15, leaf("copper")["climbingSpeedFeet"])
        silver = leaf("silver")["traits"][0]
        self.assertEqual({"action": "reaction", "trigger": "when-you-fall"}, silver["activation"])
        self.assertTrue(silver["atWill"])
        self.assertEqual(60, silver["fallingSpeedFeetPerRound"])
        self.assertEqual({"minutes": 1}, silver["duration"])
        self.assertTrue(silver["preventsFallingDamageOnLanding"])
        self.assertFalse(silver["grantsFlight"])


if __name__ == "__main__":
    unittest.main()
