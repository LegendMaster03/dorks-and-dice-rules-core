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

    def test_conditional_replacement_is_not_an_initial_grant(self):
        rows = [
            {"id": "spell.prestidigitation", "kind": "spell", "name": "Prestidigitation", "level": 0},
            {"id": "spell.light", "kind": "spell", "name": "Light", "level": 0},
            {"id": "class.wizard", "kind": "class", "name": "Wizard",
             "spellList": [{"level": 0, "spellIds": ["spell.light", "spell.prestidigitation"]}]},
            {"id": "species.elf", "kind": "species", "name": "Elf",
             "catalogReferences": ["spell.prestidigitation"],
             "additionalSpells": [{"known": {"1": ["prestidigitation|srd52#c"]},
                 "replacementOptions": [{"trigger": "long-rest", "target": "known-lineage-cantrip",
                     "different": True, "selector": {"choose": "level=0|class=Wizard"}}]}]},
        ]
        report = audit(rows, "SRD52")
        grant, replacement = report["occurrences"]
        self.assertEqual("spell-grant", grant["relation"])
        self.assertEqual(["spell.prestidigitation"], grant["targets"])
        self.assertEqual("spell-replacement-selector", replacement["relation"])
        self.assertEqual(["spell.light", "spell.prestidigitation"], replacement["candidateSpellIds"])
        self.assertEqual([], replacement["targets"])
        self.assertEqual("long-rest", replacement["raw"]["trigger"])
        self.assertFalse(replacement["alreadyExplicit"])
        self.assertEqual(0, report["unlinkedLiteralOccurrences"])

    def test_spellbook_selectors_keep_school_and_slot_bounds(self):
        rows = [
            {"id": "spell.light", "kind": "spell", "name": "Light", "level": 0, "school": "V"},
            {"id": "spell.burning-hands", "kind": "spell", "name": "Burning Hands", "level": 1, "school": "V"},
            {"id": "spell.shatter", "kind": "spell", "name": "Shatter", "level": 2, "school": "V"},
            {"id": "spell.fireball", "kind": "spell", "name": "Fireball", "level": 3, "school": "V"},
            {"id": "spell.shield", "kind": "spell", "name": "Shield", "level": 1, "school": "A"},
            {"id": "spell.cure-wounds", "kind": "spell", "name": "Cure Wounds", "level": 1, "school": "V"},
            {"id": "class.wizard", "kind": "class", "name": "Wizard",
             "spellList": [{"level": 0, "spellIds": ["spell.light"]},
                           {"level": 1, "spellIds": ["spell.burning-hands", "spell.shield"]},
                           {"level": 2, "spellIds": ["spell.shatter"]},
                           {"level": 3, "spellIds": ["spell.fireball"]}]},
            {"id": "subclass.wizard.evoker", "kind": "subclass", "name": "Evoker",
             "additionalSpells": [{"spellbook": {
                 "3": [{"choose": "level=1;2|class=Wizard|school=V", "count": 2}],
                 "5": [{"choose": "level=1;2;3|class=Wizard|school=v"}],
                 "invalid": [{"choose": "level=0;1|class=Wizard|school=V"},
                             {"choose": "level=1|class=Wizard|school=bogus"}]}}]},
        ]
        original = copy.deepcopy(rows)
        report = audit(rows, "SRD52")
        refs = report["occurrences"]
        self.assertEqual([
            ("static-candidates", ["spell.burning-hands", "spell.shatter"]),
            ("static-candidates", ["spell.burning-hands", "spell.fireball", "spell.shatter"]),
            ("unsupported-filter-syntax", []),
            ("unsupported-filter-syntax", []),
        ], [(r["selectorStatus"], r["candidateSpellIds"]) for r in refs])
        self.assertTrue(all(r["grantMode"] == "spellbook" and not r["targets"] for r in refs))
        self.assertEqual(original, rows)

    def test_character_category_pools_are_edition_local_and_do_not_award_grants(self):
        rows = [
            {"id": "skill.arcana", "kind": "skill", "name": "Arcana"},
            {"id": "skill.history", "kind": "skill", "name": "History"},
            {"id": "language.common", "kind": "language", "name": "Common", "type": "standard"},
            {"id": "language.abyssal", "kind": "language", "name": "Abyssal", "type": "rare"},
            {"id": "baseitem.dagger", "kind": "baseitem", "name": "Dagger", "type": "M|SRD52", "weaponCategory": "simple", "property": ["F|SRD52", "L|SRD52"]},
            {"id": "baseitem.rapier", "kind": "baseitem", "name": "Rapier", "type": "M|SRD52", "weaponCategory": "martial", "property": ["F|SRD52"]},
            {"id": "baseitem.hand-crossbow", "kind": "baseitem", "name": "Hand Crossbow", "type": "R|SRD52", "weaponCategory": "martial", "property": ["L|SRD52"]},
            {"id": "baseitem.flail", "kind": "baseitem", "name": "Flail", "type": "M|SRD52", "weaponCategory": "martial"},
            {"id": "baseitem.foreign", "kind": "baseitem", "name": "Foreign", "type": "M|SRD51", "weaponCategory": "martial", "property": ["L|SRD51"]},
            {"id": "baseitem.staff", "kind": "baseitem", "name": "Staff", "type": "SCF|SRD52", "weaponCategory": "simple"},
            {"id": "baseitem.leather-armor", "kind": "baseitem", "name": "Leather Armor", "type": "LA|SRD52"},
            {"id": "item.magic-rapier", "kind": "item", "name": "Magic Rapier", "type": "M|SRD52", "weaponCategory": "martial", "property": ["F|SRD52"]},
            {"id": "baseitem.lute", "kind": "baseitem", "name": "Lute", "type": "INS|SRD52"},
            {"id": "baseitem.smith-s-tools", "kind": "baseitem", "name": "Smith's Tools", "type": "AT|SRD52"},
            {"id": "item-group.musical-instrument", "kind": "item-group", "name": "Musical Instrument", "items": ["Lute|SRD52"], "catalogReferences": ["baseitem.lute"]},
            {"id": "item-group.artisan-s-tools", "kind": "item-group", "name": "Artisan's Tools", "items": ["Smith's Tools|SRD52"], "catalogReferences": ["baseitem.smith-s-tools"]},
            {"id": "feat.alert", "kind": "feat", "name": "Alert", "category": "O"},
            {"id": "feat.grappler", "kind": "feat", "name": "Grappler", "category": "G"},
            {"id": "class.rogue", "kind": "class", "name": "Rogue",
             "startingProficiencies": {"skills": [{"choose": {"from": ["arcana", "history"], "count": 1}}],
                "armor": ["light"], "weapons": ["ignored display"],
                "weaponProficiencies": [{"simple": True, "all": {"fromFilter": "type=martial weapon|property=light;finesse"}}]},
             "startingEquipment": [{"A": [{"equipmentTypes": ["instrumentMusical", "toolArtisan"]}],
                                    "B": [{"equipmentType": "weaponMartial", "quantity": 2}]}]},
            {"id": "species.human", "kind": "species", "name": "Human",
             "skillProficiencies": [{"any": 1}], "languageProficiencies": [{"anyStandard": 1}],
             "feats": [{"anyFromCategory": {"category": ["O"], "count": 1}}]},
        ]
        original = copy.deepcopy(rows)
        report = audit(rows, "SRD52")
        pools = {o["pointer"]: o for o in report["occurrences"] if "candidateStatus" in o}
        expected = {
            "/startingProficiencies/skills/0/choose": ["skill.arcana", "skill.history"],
            "/startingProficiencies/armor/0": ["baseitem.leather-armor"],
            "/startingProficiencies/weaponProficiencies/0/simple": ["baseitem.dagger"],
            "/startingProficiencies/weaponProficiencies/0/all": ["baseitem.hand-crossbow", "baseitem.rapier"],
            "/startingEquipment/0/A/0": ["baseitem.lute", "baseitem.smith-s-tools"],
            "/startingEquipment/0/B/0": ["baseitem.flail", "baseitem.hand-crossbow", "baseitem.rapier"],
            "/skillProficiencies/0/any": ["skill.arcana", "skill.history"],
            "/languageProficiencies/0/anyStandard": ["language.common"],
            "/feats/0/anyFromCategory": ["feat.alert"],
        }
        self.assertEqual(expected, {p: o["candidateIds"] for p, o in pools.items()})
        self.assertTrue(all(o["candidateStatus"] == "static-candidates" and not o["targets"] for o in pools.values()))
        self.assertEqual(0, report["unlinkedLiteralOccurrences"])
        self.assertEqual(original, rows)
        rows[14]["items"] = ["Lute|SRD51"]
        self.assertEqual("unresolved-equipment-group", next(o for o in audit(rows, "SRD52")["occurrences"]
                         if o["pointer"] == "/startingEquipment/0/A/0")["candidateStatus"])

    def test_warlock_damage_predicates_distinguish_weapon_attack_and_selection_range(self):
        from audit_srd_structured_relationships import WarlockCantripPrerequisiteInventory
        rows = []
        reviewed = WarlockCantripPrerequisiteInventory.REVIEWED
        for identity, (damage, attack, range_) in reviewed.items():
            distance = {"type": "feet", "amount": range_} if type(range_) is int else {"type": range_}
            row = {"id": identity, "kind": "spell", "name": identity.removeprefix("spell."),
                   "level": 0, "officialTextLayout": " ".join(s for s in (damage, attack) if s),
                   "range": {"type": "point", "distance": distance}}
            if damage:
                row["damageInflict"] = ["force"]
            if attack and identity != "spell.true-strike":
                row["spellAttack"] = ["R"]
            rows.append(row)
        rows.append({"id": "class.warlock", "kind": "class", "name": "Warlock",
                     "spellList": [{"level": 0, "spellIds": list(reviewed)}]})
        for identity, (expression, label, _, _) in WarlockCantripPrerequisiteInventory.FEATURES.items():
            rows.append({"id": identity, "kind": "optional-feature", "name": identity,
                         "catalogReferences": ["class.warlock"],
                         "prerequisite": [{"spell": [{"choose": expression, "entry": label,
                                                     "entrySummary": label.removeprefix("a ")}],
                                           "level": {"level": 2, "class": {"name": "Warlock", "source": "SRD52"}}}]})
        original = copy.deepcopy(rows)
        report = audit(rows, "SRD52")
        predicates = {r["entity"]: r for r in report["occurrences"] if "predicateStatus" in r}
        damaging = ["spell.chill-touch", "spell.eldritch-blast", "spell.poison-spray", "spell.true-strike"]
        for predicate in predicates.values():
            self.assertEqual("reviewed-static-predicate", predicate["predicateStatus"])
            self.assertEqual(damaging, predicate["prerequisiteCandidateSpellIds"])
            self.assertTrue(predicate["selectedCantripMustBeKnown"])
            self.assertTrue(predicate["differentCantripOnRepeat"])
            self.assertEqual([], predicate["targets"])
        self.assertEqual(["spell.eldritch-blast", "spell.poison-spray"],
                         predicates["optional-feature.eldritch-spear"]["selectionCandidateSpellIds"])
        repelling = predicates["optional-feature.repelling-blast"]
        self.assertTrue(repelling["requiresAttackRoll"])
        self.assertTrue(repelling["sourceFilterOmitsWeaponAttack"])
        self.assertEqual(damaging, repelling["selectionCandidateSpellIds"])
        self.assertEqual(0, report["unlinkedLiteralOccurrences"])
        self.assertEqual([2, 2, 2], [r["minimumClassLevel"] for r in report["occurrences"]
                                    if "minimumClassLevel" in r])
        self.assertEqual(original, rows)
        next(r for r in rows if r["id"] == "spell.true-strike")["officialTextLayout"] = "Changed source"
        self.assertEqual({"cantrip-evidence-mismatch": 3}, audit(rows, "SRD52")["spellPrerequisiteCounts"])
        self.assertEqual({"unreviewed-prerequisite-selector": 3}, audit(rows, "SRD51")["spellPrerequisiteCounts"])
