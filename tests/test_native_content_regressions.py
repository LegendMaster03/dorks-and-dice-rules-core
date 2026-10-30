"""Regression checks for selected coherent native content in the evolving catalog.

This suite validates content identities and relationships; it does not execute
class mechanics, convert source editions, or authorize a release.
"""
import json
from collections import Counter
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "data/rulesets/first-party-001"
NATIVE = PACKAGE / "native"
WORKING = PACKAGE / "working"
CLASSES = ("barbarian", "bard", "cleric", "druid", "rogue", "sorcerer", "wizard")
MONSTERS = (
    "wolf", "baboon", "goblin-minion", "goblin-warrior", "goblin-boss",
    "hobgoblin-warrior", "hobgoblin-captain", "bugbear-stalker", "bugbear-warrior",
    "weretiger", "brown-bear", "tiger", "priest", "aboleth", "lich", "adult-red-dragon",
)
def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def records(document):
    if isinstance(document, list):
        return document
    if isinstance(document, dict) and "id" in document:
        return [document]
    return [entry for name, group in document.items() if not name.startswith("_")
            for entry in group]


class NativeContentRegressionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = {}
        cls.counts = Counter()
        for path in sorted(NATIVE.rglob("*.json")):
            for entry in records(read(path)):
                identity = entry["id"]
                if identity in cls.catalog:
                    raise AssertionError(f"Duplicate native identity: {identity}")
                if entry.get("kind") == "metadata":
                    continue
                cls.catalog[identity] = entry
                cls.counts[entry["kind"]] += 1

    def test_inventory(self):
        # The catalog is actively authored; no total or category size is fixed.
        self.assertEqual(len(self.catalog), sum(self.counts.values()))
        for kind in ("condition", "leveled-condition", "rule", "ruleChapter",
                     "class", "classFeature", "monster", "monsterFamily"):
            with self.subTest(kind=kind):
                self.assertGreater(self.counts[kind], 0)
        for name in CLASSES:
            self.assertIn("class." + name, self.catalog)
        for name in MONSTERS:
            self.assertIn("monster." + name, self.catalog)
        self.assertIn("class.warlock", self.catalog)

    def test_seven_selected_band_one_progressions(self):
        for name in CLASSES:
            with self.subTest(class_name=name):
                identity = "class." + name
                cls = self.catalog[identity]
                self.assertEqual("class", cls["kind"])
                definition = cls["definition"]
                self.assertEqual([1, 20], definition["levelRange"])
                self.assertEqual(1, definition["band"])
                progression = definition["progression"]
                self.assertEqual(list(range(1, 21)), [row["level"] for row in progression])
                inline = {feature["id"] for feature in definition.get("inlineFeatures", [])}
                for level in progression:
                    for granted in level.get("featureGrants", []):
                        self.assertIn(granted, set(self.catalog) | inline,
                                      f"{name}: unresolved grant {granted}")
                        if granted in self.catalog:
                            self.assertEqual(identity, self.catalog[granted]["parentClass"])
                source = read(WORKING / "classes/base" / f"{name}.json")
                self.assertEqual("retained-native-definition", source["baselineStatus"])
                source.pop("baselineStatus")
                if name == "druid":
                    native = cls["definition"]
                    self.assertTrue(native["companionPathChoice"]["exclusive"])
                    self.assertEqual(1, native["companionPathChoice"]["selectionFromDruidLevel"])
                    self.assertFalse(native["temporaryWildCompanionCanCoexist"])
                    self.assertNotIn("druid.animal-companion", native["progression"][0]["featureGrants"])
                    self.assertNotIn("class-feature.druid.wild-companion-level-2", native["progression"][1]["featureGrants"])
                    self.assertIn("druid.animal-companion", native["choiceOnlyFeatureIds"])
                    self.assertIn("class-feature.druid.wild-companion-level-2", native["choiceOnlyFeatureIds"])
                    source["definition"] = dict(native)
                self.assertEqual(source, cls)

    def test_feature_dependencies_and_provenance(self):
        for name in CLASSES:
            features = sorted((NATIVE / "features/classes" / name).glob("*.json"))
            self.assertTrue(features, name)
            for path in features:
                entry = read(path)
                self.assertEqual("classFeature", entry["kind"])
                self.assertEqual("class." + name, entry["parentClass"])
                source = read(WORKING / "features/classes" / name / path.name)
                self.assertEqual("retained-native-definition", source.pop("baselineStatus"))
                if name == "druid" and path.name == "wild-companion-level-2.json":
                    mechanics = entry["definition"]["mechanics"]
                    self.assertFalse(mechanics["bothMayCoexist"])
                    self.assertEqual("temporary-wild-companion", mechanics["requiresCompanionPathSelection"])
                    source["definition"]["mechanics"] = dict(mechanics)
                self.assertEqual(source, entry)
                self.assertIn(entry["id"], self.catalog)

    def test_selected_monsters_preserve_stats_and_roles(self):
        for name in MONSTERS:
            with self.subTest(creature=name):
                selected = self.catalog["monster." + name]
                self.assertEqual("monster", selected["kind"])
                self.assertEqual("rule.monster-statblock-foundation",
                                 selected["statBlockFoundationId"])
                source = read(WORKING / "bestiary" / (name + ".json"))
                self.assertEqual("retained-native-definition", source.pop("baselineStatus"))
                selected_copy = dict(selected)
                selected_copy.pop("statBlockFoundationId")
                self.assertEqual(source, selected_copy)
                definition = selected["definition"]
                self.assertTrue(definition["ac"])
                self.assertGreater(definition["hp"]["average"], 0)
                self.assertIn("cr", definition)

    def test_monster_families_have_distinct_real_members(self):
        families = read(NATIVE / "monster-families.json")["monsterFamilies"]
        self.assertTrue(families)
        for family in families:
            self.assertEqual("monsterFamily", family["kind"])
            self.assertEqual(len(family["memberIds"]), len(set(family["memberIds"])))
            for identity in family["memberIds"]:
                self.assertIn(identity, self.catalog)
                self.assertEqual(family["id"], self.catalog[identity]["familyId"])
        self.assertEqual(
            {"monster.goblin-minion", "monster.goblin-warrior", "monster.goblin-boss"},
            set(self.catalog["monster-family.goblin"]["memberIds"]))

    def test_no_staging_metadata_or_competing_variants_become_authoritative(self):
        for identity, entry in self.catalog.items():
            with self.subTest(identity=identity):
                self.assertNotIn("baselineStatus", entry)
                self.assertNotIn("candidateDefinitions", entry)
                self.assertNotIn("candidateSpecializations", entry)
        experiment = read(ROOT / "data/reference/experiments/warlock-modular-alpha-0.3.1.json")
        self.assertFalse(experiment["runtimeAuthority"])
        self.assertEqual(20, len(experiment["levels"]))
        self.assertEqual(1, experiment["levels"][0]["invocationsElectiveTotal"])
        self.assertEqual(12, experiment["levels"][-1]["invocationsElectiveTotal"])
        self.assertEqual("pending-native-reconciliation",
                         read(WORKING / "classes/base/warlock.json")["baselineStatus"])


if __name__ == "__main__":
    unittest.main()
