import sys
import unittest
from pathlib import Path
from collections import defaultdict

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from audit_srd_upstream import candidate_flags, compare_candidate, key, upstream_occurrences


class UpstreamComparisonTests(unittest.TestCase):
    def test_flags_and_aliases(self):
        for record, expected in [
            ({"srd": "Arcane Hand"}, [("srd-5.1", "Arcane Hand", "srd")]),
            ({"inherits": {"srd52": True}}, [("srd-5.2.1", True, "inherits/srd52")]),
            ({"srd": False, "inherits": {"srd": True}}, []),
            ({"srd": False, "srd52": ""}, []),
        ]:
            with self.subTest(record=record):
                self.assertEqual(list(candidate_flags(record)), expected)

    def test_context_is_edition_local(self):
        row = {"className": "Fighter", "classSource": "PHB", "level": 2}
        pinned = dict(row, classSource="SRD51")
        self.assertEqual(key("srd-5.1", "classFeature", row, "Action Surge"),
                         key("srd-5.1", "classFeature", pinned, "Action Surge"))
        self.assertNotEqual(key("srd-5.1", "classFeature", dict(row, classSource="XPHB"), "Action Surge"),
                            key("srd-5.1", "classFeature", pinned, "Action Surge"))
        self.assertNotEqual(key("srd-5.1", "classFeature", dict(row, level=3), "Action Surge"),
                            key("srd-5.1", "classFeature", pinned, "Action Surge"))

    def test_match_does_not_silently_promote_or_choose_collision(self):
        edition = "srd-5.1"
        row = {"name": "Melf's Acid Arrow", "srd": "Acid Arrow", "source": "PHB", "entries": ["upstream"]}
        candidate = {"candidateId": "candidate.a", "source": {"pointer": "/spell/0"},
                     "payload": {"name": "Acid Arrow", "entries": ["pinned"]}}
        indexes = {edition: defaultdict(list)}
        names = {edition: {"acid arrow": ["spell.acid-arrow"]}}
        dispositions = {edition: {"candidate.a": {"status": "explicit-source-pointer", "nativeIds": ["spell.acid-arrow"]}}}
        result = compare_candidate(edition, "spell", row, row["srd"], indexes, names, dispositions)
        self.assertEqual(result["status"], "not-in-pinned-corpus")
        self.assertEqual(result["nativeNameHints"], ["spell.acid-arrow"])
        indexes[edition][key(edition, "spell", row, row["srd"])].append(candidate)
        result = compare_candidate(edition, "spell", row, row["srd"], indexes, names, dispositions)
        self.assertEqual(result["status"], "unique-pinned-identity")
        self.assertIn("entries", result["differentSharedFields"])
        self.assertNotIn("['upstream']", str(result))
        indexes[edition][key(edition, "spell", row, row["srd"])].append(candidate)
        result = compare_candidate(edition, "spell", row, row["srd"], indexes, names, dispositions)
        self.assertEqual(result["status"], "ambiguous-pinned-identity")
        self.assertNotIn("differentSharedFields", result)

    def test_pointer_escaping_and_non_objects(self):
        self.assertEqual(list(upstream_occurrences({"a/b~c": [0, {"name": "A"}]})),
                         [("a/b~c", "/a~1b~0c/1", {"name": "A"})])
