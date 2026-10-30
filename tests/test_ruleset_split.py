"""Regression tests for the Phase 1A native/working structural boundary."""
import json
import sys
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from validate_ruleset_split import (validate_native_tree, check_migration_ledger,
                                    matches_preserved_working_evidence, main)


class NativeWorkingBoundaryTests(unittest.TestCase):
    def test_current_native_and_working_contracts(self):
        self.assertEqual(0, main([]))

    def test_original_migration_inventory_is_stable_across_later_rule_promotions(self):
        errors, original_files = check_migration_ledger()
        self.assertEqual([], errors)
        self.assertEqual(2277, original_files)

    def test_reject_duplicate_authority_and_staged_candidates(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            (root / "a.json").write_text(json.dumps({
                "id": "rule.test", "name": "Test", "kind": "rule",
                "candidateDefinitions": [{"key": "candidate-a", "content": {}}]
            }), encoding="utf-8")
            (root / "b.json").write_text(json.dumps({
                "id": "rule.test", "name": "Test 2", "kind": "rule",
                "baselineStatus": "retained-native-definition"
            }), encoding="utf-8")
            _, errors = validate_native_tree(root)
            self.assertTrue(any("candidate" in err for err in errors))
            self.assertTrue(any("duplicate" in err for err in errors))
            self.assertTrue(any("baselineStatus" in err for err in errors))

    def test_reject_nested_draft_data_in_authoritative_rules(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "nested.json"
            path.write_text(json.dumps({
                "id": "rule.nested", "name": "Nested", "kind": "rule",
                "definition": {"nested": {"candidateDefinitions": [{"content": {}}]}}
            }), encoding="utf-8")
            _, errors = validate_native_tree(Path(directory))
            self.assertTrue(any("definition.nested.candidateDefinitions" in e for e in errors))

    def test_only_exact_preserved_source_mirrors_are_allowed(self):
        native = {"id": "class.test", "name": "Test", "kind": "class",
                  "definition": {"progression": [1, 2]}}
        source = dict(native, baselineStatus="retained-native-definition")
        working_path = Path("some/staged/class/test.json")
        self.assertTrue(matches_preserved_working_evidence(native, source, working_path))
        self.assertFalse(matches_preserved_working_evidence(
            dict(native, definition={"progression": [1, 3]}), source, working_path))
        self.assertFalse(matches_preserved_working_evidence(
            native, dict(source, candidateDefinitions=[{"key": "candidate-a"}]), working_path))
        self.assertFalse(matches_preserved_working_evidence(
            native, dict(source, baselineStatus="pending-native-reconciliation"), working_path))

    def test_monster_statblock_reference_is_non_numeric_mirror_annotation(self):
        original = {"id": "monster.test", "name": "Test", "kind": "monster",
                    "definition": {"ac": [10], "hp": {"average": 1}}}
        staged = dict(original, baselineStatus="retained-native-definition")
        selected = dict(original, statBlockFoundationId="rule.monster-statblock-foundation")
        self.assertTrue(matches_preserved_working_evidence(
            selected, staged, Path("some/staged/monster.json")))
        self.assertFalse(matches_preserved_working_evidence(
            dict(selected, definition={"ac": [11]}), staged,
            Path("some/staged/monster.json")))


if __name__ == "__main__":
    unittest.main()
