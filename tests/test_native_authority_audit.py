"""Tests for the source-neutral native definition audit."""
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from audit_native_baseline import audit, main


class NativeBaselineAuditTests(unittest.TestCase):
    def test_multiple_candidate_rules_are_unresolved_not_adopted(self):
        with tempfile.TemporaryDirectory() as location:
            root = Path(location)
            (root / "conditions.json").write_text(json.dumps({
                "conditions": [
                    {"id": "condition.deafened", "kind": "condition",
                     "effects": [{"type": "sense"}]},
                    {"id": "condition.blinded", "kind": "condition",
                     "baselineStatus": "pending-native-reconciliation",
                     "candidateDefinitions": [{"content": {}}, {"content": {}}]}
                ]
            }), encoding="utf-8")
            report = audit(root)
            self.assertEqual(2, report["counts"]["records"])
            self.assertEqual(1, report["counts"]["multiple_candidates"])
            self.assertEqual(["condition.blinded"], report["unresolvedNativeIds"])
            self.assertEqual(0, main(["--native", str(root)]))
            self.assertEqual(1, main(["--native", str(root), "--strict"]))

    def test_duplicate_identity_and_invalid_file_detected(self):
        with tempfile.TemporaryDirectory() as location:
            root = Path(location)
            for name in ("first.json", "second.json"):
                (root / name).write_text('{"id":"spell.fireball","kind":"spell"}',
                                         encoding="utf-8")
            (root / "bad.json").write_text('not valid json', encoding="utf-8")
            report = audit(root)
            self.assertIn("spell.fireball", report["duplicateIds"])
            self.assertEqual(1, len(report["invalid"]))
            self.assertEqual(1, main(["--native", str(root), "--strict"]))

    def test_single_authoritative_definition_passes(self):
        with tempfile.TemporaryDirectory() as location:
            root = Path(location)
            (root / "condition.json").write_text(
                '{"id":"condition.exhaustion","kind":"leveled-condition","levels":[]}',
                encoding="utf-8")
            self.assertEqual(0, main(["--native", str(root), "--strict"]))


if __name__ == "__main__":
    unittest.main()
