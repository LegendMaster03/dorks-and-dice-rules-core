"""Cross-creature resource checks for the general NPC/monster spellcasting rule.

An expendable resource pool requires an explicit grant. At-will and daily
spellcasting are independent of slot resources; importer/engine conversions
are implemented in their own later native magic-system pass.
"""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class NpcMonsterSpellcastingResourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.older = {m["name"]: m for m in load(
            "data/reference/srd/structured/bestiary/bestiary-srd51.json"
        )["monster"]}
        cls.newer = {m["name"]: m for m in load(
            "data/reference/srd/structured/bestiary/bestiary-srd52.json"
        )["monster"]}
        cls.manifest = {r["nativeId"]: r for r in load(
            "data/reference/srd/native-coverage-manifest.json"
        )["coverage"] if r["kind"] == "monster"}

    def monster(self, name):
        return load(self.manifest["monster." + name]["path"])

    def test_source_supported_slot_casting_is_not_a_priest_only_concept(self):
        old_grants = {
            name: [
                s for s in m.get("spellcasting", [])
                if any("slots" in level for level in s.get("spells", {}).values())
            ] for name, m in self.older.items()
        }
        old_grants = {name: grants for name, grants in old_grants.items() if grants}
        self.assertEqual(12, len(old_grants))
        self.assertIn("Priest", old_grants)
        self.assertIn("Archmage", old_grants)
        self.assertIn("Lich", old_grants)
        self.assertIn("Mage", old_grants)
        # Older-only creatures retain their original explicit slot grants.
        for name in ("androsphinx", "gynosphinx"):
            with self.subTest(monster=name):
                old = self.monster(name)
                self.assertEqual("retained-native-definition", old["baselineStatus"])
                self.assertTrue(any(
                    any("slots" in lvl for lvl in spell.get("spells", {}).values())
                    for spell in old["definition"].get("spellcasting", [])
                ))

    def test_native_monster_resource_grants_are_separate_from_spell_lists(self):
        found = []
        for identity, location in self.manifest.items():
            doc = load(location["path"])
            if doc.get("aliasOf") or not doc.get("definition"):
                continue
            definition = doc["definition"]
            grant = definition.get("spellcastingResources")
            if grant is None:
                continue
            found.append(identity)
            with self.subTest(monster=identity):
                self.assertEqual("spell-slots", grant["mode"])
                self.assertIsInstance(grant["slotsByLevel"], dict)
                self.assertTrue(grant["slotsByLevel"])
                for level, count in grant["slotsByLevel"].items():
                    self.assertIn(level, [str(i) for i in range(1, 10)])
                    self.assertIs(type(count), int)
                    self.assertGreater(count, 0)
                self.assertIsInstance(definition.get("spellcasting", []), list)
        self.assertIn("monster.priest", found)

    def test_slotless_newer_spell_grants_remain_independent(self):
        # A 5.5e spell entry is not itself a promise of a slot resource.
        for name in ("archmage", "lich", "mage"):
            with self.subTest(monster=name):
                definition = self.monster(name)["definition"]
                source = self.newer[definition["name"]]
                for spell in source.get("spellcasting", []):
                    self.assertIn(spell, definition.get("spellcasting", []))
                # These imports have not gained an unsourced resource pool.
                self.assertNotIn("spellcastingResources", definition)
        priest = self.monster("priest")["definition"]
        self.assertEqual(
            self.newer["Priest"]["spellcasting"],
            priest["spellcasting"],
        )


if __name__ == "__main__":
    unittest.main()
