"""Packaging boundaries are independent of the number of game systems."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "tools/stage_runtime_catalogs.sh"


@unittest.skipUnless(os.name == "posix", "Runtime image staging uses POSIX sh")
class RuntimeCatalogStagingTests(unittest.TestCase):
    def stage(self, root, output):
        return subprocess.run(["sh", str(SCRIPT), str(root), str(output)],
                              capture_output=True, text=True)

    def test_authoritative_only_and_multiple_packages(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / "source"
            output = Path(tmp) / "output"
            expected = set()
            for ruleset in ("first-party-001", "dnd-5e", "dnd-5-5e", "fate-condensed"):
                package = root / ruleset
                (package / "native").mkdir(parents=True)
                (package / "working").mkdir()
                (package / "manifest.json").write_text("{}")
                (package / "native/rules.json").write_text("{}")
                (package / "working/draft.json").write_text("{}")
                expected.update({f"{ruleset}/manifest.json", f"{ruleset}/native/rules.json"})
            (root / "draft-only/working").mkdir(parents=True)
            result = self.stage(root, output)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual({str(p.relative_to(output)) for p in output.rglob("*") if p.is_file()},
                             expected)

    def test_missing_manifest_and_native_symlink_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / "source"
            package = root / "broken"
            (package / "native").mkdir(parents=True)
            self.assertNotEqual(self.stage(root, Path(tmp) / "missing").returncode, 0)
            (package / "manifest.json").write_text("{}")
            (package / "native/reference.json").symlink_to(package / "manifest.json")
            self.assertNotEqual(self.stage(root, Path(tmp) / "symlink").returncode, 0)
