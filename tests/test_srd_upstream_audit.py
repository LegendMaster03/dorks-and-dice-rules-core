import sys
import unittest
from pathlib import Path
from collections import defaultdict

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from srd_upstream_crosswalk import Crosswalk, LEGACY, MODERN
from audit_srd_upstream import candidate_flags, classify_rules_matches, compare_candidate, key, upstream_occurrences


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

    def test_rules_source_collision_classification(self):
        rows = [{"candidateId": "rules", "source": {"path": "items.json"}},
                {"candidateId": "sidecar", "source": {"path": "foundry-items.json"}},
                {"candidateId": "duplicate", "source": {"path": "items.json"}}]
        dispositions = {"rules": {"status": "explicit-source-pointer", "nativeIds": ["item.example"]},
                        "sidecar": {"status": "unreviewed", "nativeIds": []},
                        "duplicate": {"status": "documented-not-promoted", "nativeIds": []}}
        self.assertEqual("unique-rules-source", classify_rules_matches(rows[:2], dispositions)["status"])
        self.assertEqual("one-promoted-with-reviewed-exclusions", classify_rules_matches(rows, dispositions)["status"])
        dispositions["duplicate"]["status"] = "unreviewed"
        self.assertEqual("ambiguous-rules-sources", classify_rules_matches(rows, dispositions)["status"])
        self.assertEqual("metadata-only", classify_rules_matches(rows[1:2], dispositions)["status"])

    def test_crosswalk_table_titles_scope_and_nested_identity(self):
        rows = [
            {"id": "table.waterborne-vehicles", "kind": "table", "name": "Waterborne Vehicles", "rows": [["boat"]]},
            {"id": "table.example", "kind": "table", "name": "Example", "rows": [["one"]]},
            {"id": "rule.madness", "kind": "rule", "name": "Madness", "entries": [
                {"type": "table", "caption": "Short-Term Madness", "rows": [["one"], ["two"]]}]},
        ]
        crosswalk = Crosswalk(LEGACY, rows)
        self.assertEqual("native-table-title-match", crosswalk.table("Group; Example", {"rows": [["one"]]})["status"])
        self.assertEqual("publisher-scope-review", crosswalk.table("Group; Airborne and Waterborne Vehicles", {})["status"])
        self.assertEqual("unresolved-table", crosswalk.table("Nonexistent", {})["status"])
        nested = crosswalk.table("Short-Term Madness", {})
        self.assertEqual("/entries/0", nested["nativePointer"])
        self.assertEqual(2, nested["nativeRowCount"])

    def test_crosswalk_card_preserves_duplicate_illusion_rows_and_local_parent(self):
        rows = [{"id": "item.deck-of-illusions", "kind": "item", "name": "Deck of Illusions", "entries": [
            {"type": "table", "rows": [["Two of hearts", "{@creature Goblin}"], ["Two of spades", "{@creature Goblin}"]]}]},
            {"id": "item.mysterious-deck", "kind": "item", "name": "Mysterious Deck", "entries": [
                {"type": "entries", "name": "Balance", "entries": ["example"]}]}]
        crosswalk = Crosswalk(MODERN, rows)
        card = crosswalk.card({"name": "Goblin", "set": "Deck of Illusions"})
        self.assertEqual(["/entries/0/rows/0", "/entries/0/rows/1"], card["nativePointers"])
        effect = crosswalk.card({"name": "Balance", "set": "Deck of Many Things"})
        self.assertEqual(["item.mysterious-deck"], effect["nativeIds"])
        self.assertEqual("nested-card-effect-heading", effect["status"])

    def test_unnamed_subrace_only_links_verified_merged_fields(self):
        base = {"id": "race.human", "kind": "race", "name": "Human", "ability": [{"str": 1}]}
        upstream = {"source": "PHB", "raceSource": "PHB", "raceName": "Human", "srd": True,
                    "ability": [{"str": 1}]}
        crosswalk = Crosswalk(LEGACY, [base])
        self.assertEqual("merged-unnamed-subrace", crosswalk.unnamed_subrace(upstream)["status"])
        self.assertEqual("unnamed-subrace-field-review",
                         crosswalk.unnamed_subrace(dict(upstream, ability=[{"str": 2}]))["status"])
        self.assertEqual("unresolved-identity",
                         crosswalk.unnamed_subrace(dict(upstream, entries=["unknown"]))["status"])
        self.assertEqual("unresolved-identity", Crosswalk(MODERN, [base]).unnamed_subrace(upstream)["status"])
