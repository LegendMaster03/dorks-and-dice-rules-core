import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from audit_srd_native_fidelity import audit, spell_body, tokens, render


class NativeFidelityTests(unittest.TestCase):
    def test_publisher_spell_diff_preserves_small_mechanical_changes(self):
        row = {"officialTextLayout": "Example\nDuration: Instantaneous\nChoose one creature within range.\nDeal 2d6 dam-\nage.",
               "entries": ["Choose one creature you can see within range. Deal {@damage 2d6} damage."]}
        result = spell_body(row)
        self.assertEqual("text-difference", result["status"])
        self.assertEqual(["you", "can", "see"], result["differences"][0]["nativeTokens"])
        row["entries"] = ["Choose one creature within range. Deal {@damage 2d6} damage."]
        self.assertEqual("exact-normalized-text", spell_body(row)["status"])
        row["entries"][0] = row["entries"][0].replace("2d6", "3d6")
        self.assertEqual(["2d6"], spell_body(row)["differences"][0]["publisherTokens"])

    def test_display_tags_scaling_and_unknown_markup(self):
        value, unknown = render("{@condition Poisoned|SRD52} {@variantrule Hit Points|SRD52|Hit Point} {@scaledamage 2d6|1-9|1d6}")
        self.assertEqual([], unknown)
        self.assertEqual(["poisoned", "hit", "point", "1d6"], tokens(value))
        self.assertIn("unknown", render("{@unknown X}")[1])
        self.assertEqual(["2d6", "+", "3"], tokens("2 d 6 +3"))

    def test_scope_locality_and_table_cells(self):
        rows = [
            {"id": "rulebook.page-001", "kind": "rulebook-page", "textLayout": "Not a native fragment"},
            {"id": "rule.example", "kind": "rule", "source": {"pages": [1]},
             "entries": ["A creature must make a saving throw.", {"type": "table", "rows": [[10, "Silver"]]}]},
            {"id": "rule.other", "kind": "rule", "source": {"pages": [1]},
             "entries": ["This is another edition's unique rule."]},
        ]
        report = audit(rows, {1: "A creature must make a saving throw. 10 Silver", 5: "This is another edition's unique rule."})
        self.assertEqual(2, report["nativeRecords"])
        record = report["records"][0]
        self.assertEqual("exact-located-text", record["fragments"][0]["status"])
        self.assertEqual("/entries/1/rows/0/0", record["fragments"][1]["pointer"])
        self.assertTrue(record["fragments"][1]["tableCellOrLabel"])
        self.assertEqual("exact-elsewhere", report["records"][1]["fragments"][0]["status"])

    def test_reviewed_spell_source_corrections(self):
        root = Path(__file__).resolve().parents[1]
        import json
        for package, identity in (("dnd-5e", "spell.acid-splash"),
                                  ("dnd-5-5e", "spell.animate-dead")):
            with self.subTest(identity=identity):
                rows = json.loads((root / "data/rulesets" / package / "native/spells/spells-001-025.json").read_text())
                row = next(r for r in rows if r["id"] == identity)
                self.assertEqual("exact-normalized-text", spell_body(row)["status"])
                if identity == "spell.acid-splash":
                    self.assertNotIn("SGT", row["miscTags"])

    def test_legacy_errata_does_not_replace_pinned_publisher_rules(self):
        import json
        root = Path(__file__).resolve().parents[1] / "data/rulesets/dnd-5e/native/spells"
        targets = ["spell.call-lightning","spell.clone","spell.color-spray","spell.contagion","spell.disintegrate","spell.glyph-of-warding","spell.heroes-feast"]
        rows = {r["id"]: r for path in root.glob("spells-*.json") for r in json.loads(path.read_text())}
        for identity in targets:
            with self.subTest(identity=identity):
                self.assertEqual("exact-normalized-text", spell_body(rows[identity])["status"])
        self.assertNotIn("poisoned", rows["spell.contagion"]["conditionInflict"])
