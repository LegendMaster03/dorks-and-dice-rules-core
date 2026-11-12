"""Creature type origination must use one inherited, human-readable method system."""
import json
from pathlib import Path
import unittest

NATIVE = Path(__file__).resolve().parents[1] / "data/rulesets/first-party-001/native"
WORKING = NATIVE.parent / "working"

def read(path):
    return json.loads(path.read_text(encoding="utf-8"))

class CreatureOriginationMethodTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        document = read(NATIVE / "creature-types.json")
        cls.foundation = next(e for e in document["entries"] if e["id"] == "rule.creature-type-inheritance")
        cls.types = {e["id"]: e for e in document["entries"] if e.get("kind") == "creatureType"}
        cls.contract = read(NATIVE / "rules/creature-origination.json")
        cls.methods = {m["id"] for m in cls.contract["methodDefinitions"]}

    @classmethod
    def resolve(cls, identity, ancestors=None):
        if ancestors is None:
            ancestors = set()
        if identity in ancestors:
            raise AssertionError("Creature-type inheritance cycle")
        if identity == "creature":
            return cls.foundation["genericParent"]["originationMethods"]
        entry = cls.types[identity]
        if "originationMethods" in entry:
            return entry["originationMethods"]
        parent = entry["parent"]
        return cls.resolve(parent, ancestors | {identity})

    def test_legacy_categories_are_retired(self):
        for identity in ("creature-type.monstrous-humanoid", "creature-type.true-elemental"):
            self.assertNotIn(identity, self.types)
            self.assertNotIn(identity, self.foundation["typeIds"])
        self.assertEqual(set(self.types), set(self.foundation["typeIds"]))
        working = read(WORKING / "creature-types.json")
        self.assertEqual(set(self.types), {t["id"] for t in working["types"]})

    def test_all_types_resolve_to_defined_methods(self):
        self.assertEqual(len(self.methods), len(self.contract["methodDefinitions"]))
        self.assertEqual(self.foundation["originationMethodRuleId"], self.contract["id"])
        for identity, entry in self.types.items():
            with self.subTest(identity=identity):
                self.assertIn(entry["parent"], {"creature", *self.types})
                resolved = self.resolve(identity)
                self.assertEqual(len(resolved), len(set(resolved)))
                self.assertTrue(set(resolved) <= self.methods)

    def test_inherited_methods_and_multiple_methods(self):
        self.assertEqual(["biological"], self.resolve("creature-type.giant"))
        self.assertEqual(["biological"], self.resolve("creature-type.magical-beast"))
        self.assertEqual(["biological"], self.resolve("creature-type.vermin"))
        self.assertEqual(["biological", "spawning"], self.resolve("creature-type.fiend"))
        self.assertEqual(["plant-reproduction"], self.resolve("creature-type.plant"))
        self.assertEqual(["construct-assembly"], self.resolve("creature-type.construct"))
        self.assertEqual(["elemental-manifestation"], self.resolve("creature-type.elemental"))
        self.assertEqual(["division"], self.resolve("creature-type.ooze"))
        self.assertEqual(["reanimation", "transformation"], self.resolve("creature-type.undead"))

    def test_reproduction_and_classification_are_independent(self):
        self.assertNotIn("biological", self.resolve("creature-type.plant"))
        self.assertNotIn("biological", self.resolve("creature-type.construct"))
        self.assertNotIn("biological", self.resolve("creature-type.elemental"))
        self.assertEqual("creature-type.outsider", self.types["creature-type.elemental"]["parent"])
        self.assertEqual([], self.resolve("creature-type.celestial"))
        self.assertTrue(self.contract["methodInheritance"]["multipleMethodsAllowed"])
        self.assertEqual("rule.creature-origination", self.contract["id"])

    def test_legacy_type_benefits_not_implicitly_added_to_elementals(self):
        elemental = self.types["creature-type.elemental"]
        self.assertNotIn("breathing", elemental.get("needs", {}))
        for name in ("djinni", "efreeti", "air-elemental", "fire-elemental"):
            monster = read(NATIVE / "bestiary" / (name + ".json"))
            definition = monster["definition"]
            type_value = definition["type"]
            effective_type = type_value["type"] if isinstance(type_value, dict) else type_value
            self.assertEqual("elemental", effective_type, name)

    def test_src_mirror_tracks_new_type_defaults(self):
        working = read(WORKING / "creature-types.json")
        originals = {t["id"]: t for t in working["types"]}
        for identity, selected in self.types.items():
            self.assertEqual(originals[identity]["originationMethods"] if "originationMethods" in originals[identity] else None,
                             selected.get("originationMethods"), identity)

if __name__ == "__main__":
    unittest.main()
