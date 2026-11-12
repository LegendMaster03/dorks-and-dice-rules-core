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

    def test_rite_of_rebirth_is_a_post_origin_transformation(self):
        rite = next(m for m in self.contract["methodDefinitions"]
                    if m["id"] == "rite-of-rebirth")
        self.assertEqual("transformation", rite["specializes"])
        self.assertEqual("post-origination", rite["stage"])
        self.assertFalse(rite["createsNewIndividual"])
        self.assertFalse(rite["typeDefault"])
        self.assertEqual("replace-complete-species-and-ancestry-configuration",
                         rite["operation"])
        self.assertEqual("rite-of-rebirth", self.contract["riteOfRebirth"]["methodId"])
        self.assertEqual("existing-creature", self.contract["riteOfRebirth"]["subject"])
        self.assertNotIn("rite-of-rebirth",
                         self.foundation["genericParent"]["originationMethods"])
        for identity in self.types:
            with self.subTest(identity=identity):
                self.assertNotIn("rite-of-rebirth", self.resolve(identity))

    def test_rebirth_acquisition_gate_and_replacement_scope(self):
        contract = self.contract["riteOfRebirth"]
        self.assertEqual("acquisitionMethods", contract["targetAcquisition"]["field"])
        self.assertIn("initial", contract["targetAcquisition"]["restrictedAncestryRule"])
        self.assertIn("mixed-Heritage", contract["targetAcquisition"]["restrictedAncestryRule"])
        self.assertIn("override", contract["targetAcquisition"]["restrictedAncestryRule"])
        replacement = contract["replacement"]
        self.assertEqual("replace-complete-current-configuration",
                         replacement["speciesAndAncestry"])
        self.assertEqual("remove-only-grants-attributable-to-old-configuration",
                         replacement["speciesAncestryTraitsAndGrants"])
        self.assertTrue(replacement["noOldNewPackageStacking"])
        for key in ("independentGrants", "background", "upbringing",
                    "existingContributorAndOriginationHistory"):
            self.assertEqual("preserve", replacement[key])
        self.assertEqual("record-in-history", replacement["riteEvent"])
        self.assertEqual("replace-then-current-species-and-ancestry",
                         replacement["subsequentRite"])

    def test_rite_does_not_restrict_ordinary_dragonborn_ancestries(self):
        self.assertIn("Ancestries without an acquisitionMethods restriction",
                      self.contract["riteOfRebirth"]["targetAcquisition"]["unspecifiedField"])
        dragonborn = NATIVE / "species/ancestry/dragonborn"
        for path in dragonborn.rglob("*.json"):
            data = read(path)
            with self.subTest(path=str(path)):
                self.assertNotIn("acquisitionMethods", data)

    def test_dm_override_policy_is_universal(self):
        policy = self.contract["dmOverridePolicy"]
        self.assertTrue(policy["allLevelsOverridable"])
        self.assertEqual("universal-character-origin", policy["level"])
        self.assertIn("not limited", policy["rule"])
        for concept in (
            "creature-type membership", "origination methods",
            "contributor counts", "compatibility", "transmitted characteristics",
            "choices", "resulting creature type", "Rite of Rebirth",
            "custom/homebrew definitions"
        ):
            with self.subTest(concept=concept):
                self.assertIn(concept, policy["scope"])
        self.assertIn("not form a whitelist", policy["scope"])
        self.assertEqual(
            {"authority", "scope", "target", "effectiveDecision"},
            set(policy["recordWhenAutomated"]["minimum"])
        )
        self.assertIn("first-party", policy["resolution"]["precedence"])
        self.assertIn("unchanged", policy["resolution"]["firstPartyCatalog"])
        self.assertIn("effective rules", policy["resolution"]["consistency"])
        self.assertIn("application authentication", policy["resolution"]["softwareAuthorization"])

    def test_dm_can_override_rite_effect_not_only_rite_access(self):
        rite = self.contract["riteOfRebirth"]
        self.assertIn("full replacement behavior", rite["dmOverride"])
        self.assertIn("partial retention", rite["dmOverride"])
        self.assertIn("change this requirement",
                      rite["targetAcquisition"]["restrictedAncestryRule"])
        self.assertIn("remove, modify, or add",
                      self.contract["methodAvailability"]["dmOverride"])
        self.assertIn("method requirement",
                      self.contract["compatibility"]["dmOverride"])

    def test_src_mirror_tracks_new_type_defaults(self):
        working = read(WORKING / "creature-types.json")
        originals = {t["id"]: t for t in working["types"]}
        for identity, selected in self.types.items():
            self.assertEqual(originals[identity]["originationMethods"] if "originationMethods" in originals[identity] else None,
                             selected.get("originationMethods"), identity)

if __name__ == "__main__":
    unittest.main()
