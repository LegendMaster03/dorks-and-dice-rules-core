"""Regression tests for the Phase 1A native/working structural boundary."""
import json
import sys
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from validate_ruleset_split import validate_native_tree, main


class NativeWorkingBoundaryTests(unittest.TestCase):
    def test_current_native_and_working_contracts(self):
        self.assertEqual(0, main([]))

    def test_migration_snapshot_preserves_all_original_git_blobs(self):
        self.assertEqual(0, main(["--check-migration-sha"]))

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


if __name__ == "__main__":
    unittest.main()
