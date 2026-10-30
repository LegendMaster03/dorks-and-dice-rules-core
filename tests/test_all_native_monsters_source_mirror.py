"""Every selected native monster preserves its reviewed source stat block."""
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[1]
NATIVE=ROOT / "data/rulesets/first-party-001/native/bestiary"
WORKING=ROOT / "data/rulesets/first-party-001/working/bestiary"

class NativeBestiaryMirrorTests(unittest.TestCase):
    def test_every_native_monster_preserves_selected_definition(self):
        for p in sorted(NATIVE.glob("*.json")):
            with self.subTest(monster=p.stem):
                selected=json.loads(p.read_text(encoding="utf-8"))
                historical=json.loads((WORKING/p.name).read_text(encoding="utf-8"))
                self.assertEqual("retained-native-definition",historical.pop("baselineStatus"))
                historical["statBlockFoundationId"]="rule.monster-statblock-foundation"
                self.assertEqual(historical,selected)

    def test_every_selected_working_monster_was_promoted_and_aliases_not_duplicated(self):
        native_ids = {
            json.loads(path.read_text(encoding="utf-8"))["id"]
            for path in NATIVE.glob("*.json")
        }
        for source_path in sorted(WORKING.glob("*.json")):
            source = json.loads(source_path.read_text(encoding="utf-8"))
            status = source.get("baselineStatus")
            with self.subTest(monster=source_path.stem):
                if status == "retained-native-definition":
                    self.assertTrue((NATIVE/source_path.name).is_file(),
                                    "Selected creature is missing from native")
                    self.assertIn(source["id"], native_ids)
                elif status == "native-identity-alias":
                    self.assertFalse((NATIVE/source_path.name).exists(),
                                     "Source identity alias must not create a duplicate monster")
                    self.assertIn(source["aliasOf"], native_ids)
                else:
                    self.assertFalse((NATIVE/source_path.name).exists(),
                                     "Unreconciled source candidate became authoritative")

if __name__=="__main__":
    unittest.main()
