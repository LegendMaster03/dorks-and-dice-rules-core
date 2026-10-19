"""Catalog-wide invariants for the approved SRD 5.5e monster-first policy.

Reconcile removed legacy capabilities only against older counterparts.
New-only monsters are already adopted and must not be changed by legacy passes.
"""
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "data/reference/srd/structured/bestiary"


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def slug(name):
    return re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-")


class MonsterCatalogInvariants(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.old = {x["name"]: x for x in load(SRC / "bestiary-srd51.json")["monster"]}
        cls.new = {x["name"]: x for x in load(SRC / "bestiary-srd52.json")["monster"]}
        cls.ledger = load(ROOT / "data/reference/srd/monster-legacy-variant-review.json")
        manifest = load(ROOT / "data/reference/srd/native-coverage-manifest.json")
        cls.rows = {r["nativeId"]: r for r in manifest["coverage"]}

    def native(self, name):
        identity = "monster." + slug(name)
        self.assertIn(identity, self.rows)
        document = load(ROOT / self.rows[identity]["path"])
        self.assertEqual(identity, document["id"])
        return document

    def test_genuinely_new_roster_has_no_legacy_reconciliation(self):
        # Some 5.2.1-only *names* are renamed 5.1 creatures, not new roles.
        alias_targets = {row["current"] for row in self.ledger["roleMappings"]}
        new_only = set(self.new) - set(self.old)
        unpaired_new_roles = sorted(new_only - alias_targets)
        self.assertEqual(49, len(new_only))
        self.assertEqual(23, len(alias_targets))
        self.assertEqual(26, len(unpaired_new_roles))
        for name in unpaired_new_roles:
            with self.subTest(name=name):
                native = self.native(name)
                self.assertEqual("retained-native-definition", native["baselineStatus"])
                self.assertNotIn("candidateDefinitions", native)
                current = native["definition"]
                source = self.new[name]
                for key in (
                    "ac", "hp", "speed", "str", "dex", "con", "int", "wis", "cha",
                    "cr", "trait", "action", "bonus", "reaction", "legendary",
                    "spellcasting",
                ):
                    self.assertEqual(source.get(key), current.get(key),
                                     f"New-only monster unexpectedly changed: {name}.{key}")
        self.assertIn("Pirate", unpaired_new_roles)
        self.assertIn("Pirate Captain", unpaired_new_roles)

    def test_genuinely_old_only_monsters_have_one_unmodified_definition(self):
        aliases = {row["old"] for row in self.ledger["roleMappings"]}
        old_only = sorted(set(self.old) - set(self.new) - aliases)
        self.assertEqual(15, len(old_only))
        for name in old_only:
            with self.subTest(name=name):
                native = self.native(name)
                self.assertEqual("retained-native-definition", native["baselineStatus"])
                self.assertNotIn("aliasOf", native)
                self.assertNotIn("candidateDefinitions", native)
                current = native["definition"]
                source = self.old[name]
                for key in (
                    "ac", "hp", "speed", "str", "dex", "con", "int", "wis", "cha",
                    "cr", "trait", "action", "bonus", "reaction", "legendary",
                    "spellcasting",
                ):
                    self.assertEqual(source.get(key), current.get(key),
                                     f"Old-only monster unexpectedly changed: {name}.{key}")

    def test_shared_monsters_preserve_the_entire_newer_attack_suite(self):
        paired = sorted(set(self.old) & set(self.new))
        self.assertEqual(285, len(paired))
        for name in paired:
            with self.subTest(name=name):
                native = self.native(name)
                self.assertEqual("retained-native-definition", native["baselineStatus"])
                current = native["definition"]
                source = self.new[name]
                for key in ("ac", "hp", "speed", "str", "dex", "con",
                            "int", "wis", "cha", "cr"):
                    self.assertEqual(source.get(key), current.get(key),
                                     f"Newer baseline statistic modified: {name}.{key}")
                for field in ("trait", "action", "bonus", "reaction", "legendary"):
                    for feature in source.get(field, []):
                        self.assertIn(
                            feature, current.get(field, []),
                            f"An original 5.5e ability changed or disappeared: {name}.{field}.{feature.get('name')}"
                        )
                # Prior spelling/casting lists may be extended, but the 5.5e
                # spellcasting grants and procedures must remain available.
                spellcast = current.get("spellcasting", [])
                for old_feature in source.get("spellcasting", []):
                    match = next((x for x in spellcast
                                  if x.get("name") == old_feature.get("name")), None)
                    self.assertIsNotNone(match, f"Missing original spellcasting on {name}")
                    for key in ("ability", "displayAs", "headerEntries"):
                        self.assertEqual(old_feature.get(key), match.get(key),
                                         f"5.5e spellcasting procedure changed on {name}")

    def test_all_legacy_feature_dispositions_are_accounted_for(self):
        statuses = {
            "already-present", "newer-successor", "revised-attack-suite",
            "restored", "spell-access-extended", "review-needed",
            "possible-redesign",
        }
        review = self.ledger["entries"]
        self.assertEqual(249, len(review))
        for entry in review:
            self.assertIn(entry["nativeId"], self.rows)
            n = 0
            for kind, old_names in entry["olderAbilitiesNotIncludedAutomatically"].items():
                dispositions = entry["featureDisposition"][kind]
                self.assertEqual(old_names, [x["name"] for x in dispositions])
                self.assertTrue(all(x["status"] in statuses for x in dispositions))
                n += sum(x["status"] in ("review-needed", "possible-redesign")
                         for x in dispositions)
            self.assertEqual(n, entry["remainingFeatureQuestions"])


if __name__ == "__main__":
    unittest.main()
