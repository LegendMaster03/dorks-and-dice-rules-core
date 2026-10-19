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

    def test_srd_51_use_an_object_action_name(self):
        records = read("data/native/actions.json")["records"]
        action = next(x for x in records if x["id"] == "action.use-an-object")
        self.assertEqual("Use an Object", action["name"])
        self.assertEqual("Use an Object", action["candidateDefinitions"][0]["content"]["name"])
        self.assertFalse(any(x["id"] == "action.use-an-item" for x in records))
        manifest = read("data/reference/srd/native-coverage-manifest.json")
        self.assertEqual(1, sum(x["nativeId"] == "action.use-an-object" for x in manifest["coverage"]))

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


    def test_black_dragon_official_spell_identity(self):
        for name in ("adult-black-dragon", "ancient-black-dragon"):
            with self.subTest(monster=name):
                records = read(f"data/native/bestiary/{name}.json")["candidateDefinitions"]
                modern = next(x["content"] for x in records if x["key"] == "candidate-b")
                body = json.dumps(modern)
                self.assertIn("Acid Arrow|SRD52", body)
                self.assertNotIn("Melf's Acid Arrow", body)

    def test_cube_of_force_official_spell_identities(self):
        candidates = read("data/native/items/magic/cube-of-force.json")["candidateDefinitions"]
        modern = next(x["content"] for x in candidates if x["key"] == "candidate-b")
        rows = modern["entries"][-1]["rows"]
        self.assertEqual(["Tiny Hut", "Private Sanctum", "Resilient Sphere"],
                         [rows[i][0].split("|")[0].replace("{@spell ", "") for i in (2, 3, 4)])
        attached = json.dumps(modern["attachedSpells"])
        for good in ("tiny hut|srd52", "private sanctum|srd52", "resilient sphere|srd52"):
            self.assertIn(good, attached)
        for bad in ("Leomund's", "Mordenkainen's", "Otiluke's", "leomund's", "mordenkainen's", "otiluke's"):
            self.assertNotIn(bad, json.dumps(modern))

    def test_pact_of_chain_familiar_list_matches_official_srd(self):
        candidates = read("data/native/features/optional/pact-of-the-chain.json")["candidateDefinitions"]
        modern = next(x["content"] for x in candidates if x["key"] == "candidate-b")
        body = json.dumps(modern)
        self.assertNotIn("Slaad Tadpole", body)
        self.assertNotIn("appendix B", body)
        self.assertIn("Sphinx of Wonder|SRD52", body)
        self.assertIn('see \\"Monsters\\"', body)

    def test_52_avatar_of_death_source_omission_repaired(self):
        candidates = read("data/native/bestiary/avatar-of-death.json")["candidateDefinitions"]
        self.assertEqual(["candidate-a", "candidate-b"], [c["key"] for c in candidates])
        old, modern = [c["content"] for c in candidates]
        self.assertIn("darkvision 60 ft.", old["senses"])
        self.assertEqual([20], modern["ac"])
        self.assertTrue(all(modern[attr] == 16 for attr in ("str", "dex", "con", "int", "wis", "cha")))
        self.assertEqual(["Truesight 60 ft."], modern["senses"])
        self.assertEqual(13, modern["passive"])
        self.assertEqual(0, modern["cr"]["xp"])
        self.assertIn("equals its summoner", modern["pbNote"])
        self.assertIn("exhaustion", modern["conditionImmune"])
        self.assertEqual(["Incorporeal Movement"], [t["name"] for t in modern["trait"]])
        self.assertEqual(["Multiattack", "Reaping Scythe"], [a["name"] for a in modern["action"]])
        self.assertIn("Automatic hit", json.dumps(modern["action"]))
        manifest = read("data/reference/srd/native-coverage-manifest.json")
        row = next(x for x in manifest["coverage"] if x["nativeId"] == "monster.avatar-of-death")
        self.assertEqual(2, row["sourceRecords"])
        self.assertEqual([x["key"] for x in candidates], [x["key"] for x in row["candidates"]])
        self.assertIn("official SRD 5.2.1 PDF", row["candidates"][1]["evidence"])


    def test_warlock_pact_magic_52_uses_official_example_spell(self):
        feat = read("data/native/features/classes/warlock/pact-magic-level-1.json")
        modern = next(x["content"] for x in feat["candidateDefinitions"] if x["key"] == "candidate-b")
        self.assertIn("Charm Person|SRD52", json.dumps(modern))
        self.assertNotIn("Witch Bolt|SRD52", json.dumps(modern))


    def test_dragon_orb_lacks_non_srd_legacy_properties_and_lore(self):
        candidates = read("data/native/items/magic/dragon-orb.json")["candidateDefinitions"]
        self.assertEqual(["candidate-b"], [c["key"] for c in candidates])
        modern = candidates[0]["content"]
        names = [e.get("name") for e in modern["entries"] if isinstance(e, dict)]
        self.assertNotIn("Random Properties", names)
        self.assertTrue(modern["entries"][0].startswith(
            "An orb is an etched crystal globe about 10 inches in diameter."))
        content = json.dumps(modern)
        for non_srd in ("Dragonlance setting", "Towers of High Sorcery",
                        "minor beneficial properties", "Orb of Dragonkind"):
            self.assertNotIn(non_srd, content)
        self.assertIn("Dragon Orb has AC 20", content)

    def test_mysterious_deck_52_excludes_legacy_dm_advice(self):
        modern = read("data/native/items/magic/mysterious-deck.json")["candidateDefinitions"][0]["content"]
        table = next(x for x in modern["entries"] if isinstance(x, dict) and x.get("type") == "table")
        self.assertEqual("Mysterious Deck", table["caption"])
        names = [x.get("name") for x in modern["entries"] if isinstance(x, dict)]
        self.assertNotIn("A Question of Enmity", names)
        body = json.dumps(modern)
        self.assertNotIn("Deck of Many Things", body)
        self.assertNotIn("Bastion", body)
        self.assertNotIn("seeAlsoDeck", modern)
        self.assertIn("Avatar of Death|SRD52", body)

if __name__ == "__main__":
    unittest.main()
