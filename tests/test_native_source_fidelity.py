"""Regression checks for official-SRD corrections in staged native data."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
def read(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))

class NativeSourceFidelityTests(unittest.TestCase):
    def test_obsolete_versions_excluded(self):
        cases = {
            "feats/magic-initiate.json": "Cleric",
            "feats/savage-attacker.json": "Once per turn",
            "feats/skilled.json": "Repeatable",
            "items/equipment/musket.json": "Slow",
            "items/equipment/pistol.json": "Vex",
        }
        for name, expected in cases.items():
            with self.subTest(name=name):
                candidates = read("data/native/" + name)["candidateDefinitions"]
                self.assertEqual(["candidate-b-2"], [x["key"] for x in candidates])
                self.assertIn(expected, json.dumps(candidates[0]["content"]))

    def test_restored_2024_armor_definition(self):
        candidates = read("data/native/items/magic/plate-armor-of-etherealness.json")["candidateDefinitions"]
        self.assertEqual(["candidate-a", "candidate-b"], [x["key"] for x in candidates])
        self.assertIn("10 minutes", json.dumps(candidates[0]["content"]))
        modern = candidates[1]["content"]
        self.assertEqual(["Half Plate Armor", "Plate Armor"], modern["eligibleArmor"])
        self.assertEqual("legendary", modern["rarity"])
        self.assertTrue(modern["reqAttune"])
        self.assertIn("Magic|SRD52", json.dumps(modern))
        self.assertNotIn("10 minutes", json.dumps(modern))
        self.assertNotIn("A|SRD52", json.dumps(modern))

    def test_poison_names_and_encounter_references(self):
        for filename, good, bad in [
            ("crawler-mucus-contact.json", "Crawler Mucus", "Carrion Crawler Mucus"),
            ("spider-s-sting.json", "Spider's Sting", "Lolth's Sting"),
        ]:
            item = read("data/native/items/magic/" + filename)
            modern = next(x["content"] for x in item["candidateDefinitions"] if x["key"].startswith("candidate-b"))
            self.assertIn(good, json.dumps(modern))
            self.assertNotIn(bad, json.dumps(modern))
        toolbox = json.dumps(read("data/native/rules/gameplay-toolbox.json"))
        for target in ["Giant Rat|srd52|Giant Rats", "Druid|srd52|Druids", "Spider's Sting|srd52"]:
            self.assertIn(target, toolbox)
        for stale in ["Twig Blight|srd52", "Nothic|srd52", "Carrion Crawler Mucus|srd52", "Lolth's Sting|srd52"]:
            self.assertNotIn(stale, toolbox)

    def test_index_and_manifest_consistency(self):
        monster_list = json.dumps(read("data/native/rules/monsters-a-z.json"))
        self.assertNotIn("Psychic Gray Ooze", monster_list)
        self.assertIn("Gray Ooze|srd52", monster_list)
        index = json.dumps(read("data/native/rules/magic-items-a-z.json"))
        for stale in ["Apparatus of Kwalish", "Daern's Instant Fortress", "Heward's Handy Haversack", "Nolzur's Marvelous Pigments", "Deck of Many Things|srd52", "Orb of Dragonkind|srd52"]:
            self.assertNotIn(stale, index)
        spells = json.dumps(read("data/native/rules/spell-descriptions.json"))
        for stale in ["Mordenkainen's Faithful Hound|", "Mordenkainen's Sword|", "Nystul's Magic Aura|", "Tasha's Hideous Laughter|", "Tenser's Floating Disk|"]:
            self.assertNotIn(stale, spells)
        spell_lists = json.dumps(read("data/native/rules/spell-lists.json"))
        for stale in ["Mordenkainen's Faithful Hound|", "Mordenkainen's Sword|", "Nystul's Magic Aura|", "Tasha's Hideous Laughter|", "Tenser's Floating Disk|"]:
            self.assertNotIn(stale, spell_lists)
        manifest = read("data/reference/srd/native-coverage-manifest.json")
        ids = {"feat.magic-initiate", "feat.savage-attacker", "feat.skilled", "baseitem.musket", "baseitem.pistol", "item.plate-armor-of-etherealness"}
        for row in manifest["coverage"]:
            if row["nativeId"] in ids:
                actual = read(row["path"])["candidateDefinitions"]
                self.assertEqual(row["sourceRecords"], len(actual))
                self.assertEqual([x["key"] for x in row["candidates"]], [x["key"] for x in actual])

if __name__ == "__main__":
    unittest.main()
