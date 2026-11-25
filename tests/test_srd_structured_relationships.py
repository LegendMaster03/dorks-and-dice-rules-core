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
