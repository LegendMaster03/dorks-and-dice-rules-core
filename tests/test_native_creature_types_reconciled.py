"""Approved creature-type inheritance definitions must reflect selected source."""
import json
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]/"data/rulesets/first-party-001"
def read(path):
    return json.loads(path.read_text(encoding="utf-8"))
class NativeCreatureTypeTests(unittest.TestCase):
    def test_selected_hierarchy_matches_inert_working_evidence(self):
        working=read(ROOT/"working/creature-types.json")
        native=read(ROOT/"native/creature-types.json")
        entities=native["entries"]
        foundation=next(e for e in entities if e["id"]=="rule.creature-type-inheritance")
        types={e["id"]:e for e in entities if e["kind"]=="creatureType"}
        source={e["id"]:e for e in working["types"]}
        self.assertEqual(set(source),set(types))
        self.assertEqual(working["genericParent"],foundation["genericParent"])
        self.assertEqual(working["inheritance"],foundation["inheritance"])
        self.assertEqual([e["id"] for e in working["types"]],foundation["typeIds"])
        self.assertEqual(working["deferred"],foundation["unresolved"])
        for identity,entry in types.items():
            with self.subTest(identity=identity):
                expected=dict(source[identity],kind="creatureType",rulesStatus="selected-native-type; inheritance-resolver-under-development")
                self.assertEqual(expected,entry)
                parents=entry.get("parent",[])
                if isinstance(parents,str): parents=[parents]
                for parent in parents:
                    self.assertIn(parent, set(source)|{"creature"})
        self.assertNotIn("candidateSpecializations",json.dumps(native))
if __name__=="__main__":
    unittest.main()
