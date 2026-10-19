"""Regression checks for native monster role defaults and semantic family identities."""
from collections import Counter
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / "data/native"
REFERENCE = ROOT / "data/reference/srd"

def read(relative):
    return json.loads((ROOT / relative).read_text(encoding="utf-8"))

class MonsterFamilyReconciliationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.coverage = read("data/reference/srd/native-coverage-manifest.json")
        cls.rows = {r["nativeId"]: r for r in cls.coverage["coverage"]}
        cls.families = read("data/native/monster-families.json")["monsterFamilies"]
        cls.ledger = read("data/reference/srd/monster-legacy-variant-review.json")

    def test_paired_monster_roster_has_one_default_per_identity(self):
        old = {m["name"] for m in read(
            "data/reference/srd/structured/bestiary/bestiary-srd51.json")["monster"]}
        newer = {m["name"] for m in read(
            "data/reference/srd/structured/bestiary/bestiary-srd52.json")["monster"]}
        defaults = [r for r in self.rows.values()
                    if r["status"] == "native-monster-role-default"]
        self.assertEqual(len(old & newer), len(defaults))
        self.assertEqual(285, len(defaults))
        for row in defaults:
            record = read(row["path"])
            self.assertEqual("retained-native-definition", record["baselineStatus"])
            self.assertNotIn("candidateDefinitions", record)
            self.assertIn("definition", record)
            self.assertIsInstance(record["definition"].get("action", []), list)

    def test_renamed_predecessors_are_aliases_without_competing_statblocks(self):
        mappings = self.ledger["roleMappings"]
        aliases = [r for r in self.rows.values()
                   if r["status"] == "native-monster-identity-alias"]
        self.assertEqual(23, len(mappings))
        self.assertEqual(len(mappings), len(aliases))
        for row in aliases:
            record = read(row["path"])
            target = record["aliasOf"]
            self.assertEqual(target, row["aliasOf"])
            self.assertIn(target, self.rows)
            self.assertNotIn("definition", record)
            self.assertNotIn("candidateDefinitions", record)
            target_record = read(self.rows[target]["path"])
            self.assertIn("definition", target_record)

    def test_goblinoid_families_preserve_distinct_roles(self):
        families = {f["id"]: f for f in self.families}
        self.assertEqual(3, len(families))
        goblins = families["monster-family.goblin"]
        self.assertEqual(
            ["monster.goblin-minion", "monster.goblin-warrior", "monster.goblin-boss"],
            goblins["memberIds"])
        legacy_goblin = read("data/native/bestiary/goblin.json")
        self.assertEqual("monster.goblin-warrior", legacy_goblin["aliasOf"])
        for name in ("hobgoblin", "bugbear"):
            old = read("data/native/bestiary/" + name + ".json")
            self.assertEqual("native-identity-alias", old["baselineStatus"])
            self.assertEqual("monster." + name + "-warrior", old["aliasOf"])
            self.assertNotIn("definition", old)
            self.assertIn("monster." + name,
                          families["monster-family." + name]["legacyMechanicsReview"])
            older_review = next(
                item for item in self.ledger["entries"]
                if item["nativeId"] == "monster." + name
            )
            self.assertTrue(older_review["olderAbilitiesNotIncludedAutomatically"])
            self.assertEqual("monster." + name + "-warrior",
                             older_review["correspondingDefault"])

    def test_older_traits_and_changes_remain_reviewable(self):
        review = self.ledger["entries"]
        self.assertGreater(len(review), 0)
        ids = [r["nativeId"] for r in review]
        self.assertEqual(len(ids), len(set(ids)))
        bat = read("data/native/bestiary/bat.json")
        self.assertEqual(
            {"Echolocation", "Keen Hearing"},
            {t["name"] for t in bat["definition"]["trait"]})
        aboleth_review = next(r for r in review if r["nativeId"] == "monster.aboleth")
        self.assertIn("Enslave (3/Day)",
                      aboleth_review["olderAbilitiesNotIncludedAutomatically"]["action"])

    def test_summary_accounts_for_both_aliases_and_new_defaults(self):
        report = read("docs/rewrite/native-reconciliation-statistics.json")
        statuses = Counter(row["status"] for row in self.rows.values())
        self.assertEqual(2522, sum(statuses.values()))
        self.assertEqual(285, statuses["native-monster-role-default"])
        self.assertEqual(23, statuses["native-monster-identity-alias"])
        self.assertEqual(
            statuses["multiple-candidates"] + statuses["single-candidate"],
            report["candidateRecordsStillUnresolved"])
        self.assertEqual(1664, report["candidateRecordsStillUnresolved"])
        self.assertEqual(len(self.ledger["entries"]), report["monsterLegacyVariantReviewEntries"])

if __name__ == "__main__":
    unittest.main()
