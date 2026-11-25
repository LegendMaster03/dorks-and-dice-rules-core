import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from audit_srd_structured_relationships import audit


class StructuredRelationshipsTests(unittest.TestCase):
    def test_fixed_spells_selectors_and_edition_boundary(self):
        rows = [
            {"id": "spell.light", "kind": "spell", "name": "Light"},
            {"id": "feat.example", "kind": "feat", "name": "Example",
             "catalogReferences": ["spell.light"],
             "additionalSpells": [{"name": "Choice", "ability": "int",
                 "innate": {"3": {"daily": {"1": ["Light|SRD52#c", "Light|SRD51"]}}},
                 "known": {"1": [{"choose": "level=0|class=Wizard", "count": 2}]}}]},
        ]
        result = audit(rows, "SRD52")["occurrences"]
        self.assertEqual(3, len(result))
        self.assertEqual("unique-literal-candidate", result[0]["status"])
        self.assertEqual("c", result[0]["castQualifier"])
        self.assertEqual("innate", result[0]["grantMode"])
        self.assertTrue(result[0]["alreadyExplicit"])
        self.assertEqual("foreign-source", result[1]["status"])
        self.assertEqual("selector-review", result[2]["status"])
        self.assertEqual([], result[2]["targets"])

    def test_equipment_choices_and_prerequisites(self):
        rows = [
            {"id": "baseitem.dagger", "kind": "baseitem", "name": "Dagger"},
            {"id": "spell.light", "kind": "spell", "name": "Light"},
            {"id": "background.example", "kind": "background", "name": "Example",
             "startingEquipment": {"default": ["ignored prose"], "defaultData": [
                 {"A": [{"item": "Dagger|SRD51", "quantity": 2}], "B": [{"equipmentType": "weaponSimple"}]}]},
             "prerequisite": [{"spell": ["Light#c"], "level": 4}]},
        ]
        refs = audit(rows, "SRD51")["occurrences"]
        self.assertEqual(4, len(refs))
        self.assertEqual(["baseitem.dagger"], refs[0]["targets"])
        self.assertEqual("/startingEquipment/defaultData/0/A/0/item", refs[0]["pointer"])
        self.assertEqual("selector-review", refs[1]["status"])
        self.assertEqual(["spell.light"], refs[2]["targets"])
        self.assertEqual("constraint-review", refs[3]["status"])

    def test_literal_link_requirement_keeps_filter_selectors_separate(self):
        rows = [
            {"id": "baseitem.dagger", "kind": "baseitem", "name": "Dagger"},
            {"id": "class.rogue", "kind": "class", "name": "Rogue",
             "startingEquipment": ["Dagger|SRD52"],
             "startingProficiencies": {"weaponProficiencies": [
                 {"all": {"fromFilter": "type=martial weapon|property=light;finesse"}}]}}
        ]
        report = audit(rows, "SRD52")
        self.assertEqual(1, report["unlinkedLiteralOccurrences"])
        self.assertEqual(1, report["counts"]["selector-review"])
        rows[1]["catalogReferences"] = ["baseitem.dagger"]
        self.assertEqual(0, audit(rows, "SRD52")["unlinkedLiteralOccurrences"])

    def test_spell_selectors_use_local_lists_levels_and_rituals_without_grants(self):
        rows = [
            {"id": "spell.light", "kind": "spell", "name": "Light", "level": 0},
            {"id": "spell.alarm", "kind": "spell", "name": "Alarm", "level": 1,
             "meta": {"ritual": True}},
            {"id": "spell.bless", "kind": "spell", "name": "Bless", "level": 1},
            {"id": "spell.detect-magic", "kind": "spell", "name": "Detect Magic", "level": 1,
             "meta": {"ritual": True}},
            {"id": "class.wizard", "kind": "class", "name": "Wizard",
             "spellList": [{"level": 0, "spellIds": ["spell.light"]},
                           {"level": 1, "spellIds": ["spell.alarm", "spell.detect-magic"]}]},
            {"id": "class.cleric", "kind": "class", "name": "Cleric",
             "spellList": [{"level": 1, "spellIds": ["spell.bless", "spell.detect-magic"]}]},
            {"id": "feat.example", "kind": "feat", "name": "Example",
             "additionalSpells": [{"known": {"_": [
                 {"choose": "level=0|class=Wizard"},
                 {"choose": "Components & Miscellaneous=ritual|level=1", "count": 2},
                 {"choose": "level=1|class=Wizard|school=Evocation"},
                 {"choose": "level=1|class=Unknown"},
                 {"choose": "level=0|class=Wizard", "count": 2}]},
                 "expanded": {"10": [{"all": "level=0;1|class=Cleric;Wizard"}]},
                 "innate": {"_": [{"all": "level=1"}]}}]},
        ]
        original = copy.deepcopy(rows)
        report = audit(rows, "SRD52")
        refs = report["occurrences"]
        self.assertEqual([
            ("static-candidates", ["spell.light"]),
            ("static-candidates", ["spell.alarm", "spell.detect-magic"]),
            ("unsupported-filter-syntax", []),
            ("unresolved-class-list", []),
            ("insufficient-candidates", ["spell.light"]),
            ("static-candidates", ["spell.alarm", "spell.bless", "spell.detect-magic", "spell.light"]),
            ("unsupported-selector-shape", []),
        ], [(r["selectorStatus"], r["candidateSpellIds"]) for r in refs])
        self.assertEqual(2, refs[1]["choiceCount"])
        self.assertEqual("expanded", refs[5]["grantMode"])
        self.assertEqual("all", refs[5]["selectorOperator"])
        self.assertEqual(0, report["unlinkedLiteralOccurrences"])
        self.assertTrue(all(not r["targets"] and not r["alreadyExplicit"] for r in refs))
        self.assertEqual(original, rows)
        rows[4]["spellList"][0]["spellIds"].append("spell.foreign")
        self.assertEqual("invalid-class-list", audit(rows, "SRD52")["occurrences"][0]["selectorStatus"])
