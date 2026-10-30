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

if __name__=="__main__":
    unittest.main()
