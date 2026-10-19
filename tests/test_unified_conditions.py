"""Regression checks for the resolved native 5.x conditions and Wet house rule."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class UnifiedConditionsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = load("data/native/conditions.json")
        cls.conditions = {x["id"]: x for x in cls.data["conditions"]}
        cls.chapter = load("data/native/rules/conditions.json")
        cls.decisions = {
            x["nativeRuleId"]: x for x in
            load("data/reference/srd/reconciliation-decisions.json")["decisions"]
        }
        cls.original = load("data/reference/srd/structured/conditionsdiseases.json")["condition"]

    def rule(self, name):
        return self.conditions["condition." + name.lower()]

    def test_one_native_definition_for_each_condition(self):
        self.assertEqual(18, len(self.conditions))
        self.assertEqual(18, len(self.data["conditions"]))
        self.assertEqual(set(self.conditions) - {"condition.flat-footed", "condition.helpless"}, set(self.decisions))
        self.assertIn("condition.wet", self.conditions)
        self.assertIn("condition.flat-footed", self.conditions)
        self.assertIn("condition.helpless", self.conditions)
        for entity in self.conditions.values():
            self.assertNotIn("candidateDefinitions", entity)
            self.assertNotIn("baselineStatus", entity)
            for key in ("source", "edition", "srd", "srd52", "sourceAliases"):
                self.assertNotIn(key, entity)
            for dependent in entity.get("appliesConditions", []):
                self.assertIn(dependent, self.conditions)
        self.assertEqual(30, len(self.original))
        for name in (x["name"] for x in self.data["conditions"] if x["name"] not in ("Wet", "Flat-Footed", "Helpless")):
            self.assertEqual({"SRD51", "SRD52"},
                             {x["source"] for x in self.original if x["name"] == name})

    def test_chapter_is_a_general_rule_not_duplicate_conditions(self):
        self.assertEqual("ruleChapter", self.chapter["kind"])
        self.assertNotIn("candidateDefinitions", self.chapter)
        self.assertNotIn("baselineStatus", self.chapter)
        self.assertEqual(set(self.conditions), set(self.chapter["conditionIds"]))
        self.assertTrue(all(isinstance(s, str) for s in self.chapter["entries"]))
        self.assertIn("Exhaustion", " ".join(self.chapter["entries"]))

    def test_blinded_sight_and_attack_effects(self):
        e = self.rule("Blinded")["effects"]
        self.assertIn({"type": "sense", "sense": "sight", "availability": "none"}, e)
        self.assertTrue(any(x.get("requiresSense") == "sight"
                            and x.get("result") == "automatic-failure" for x in e))
        self.assertEqual({"disadvantage"},
                         {x["mode"] for x in e if x["type"] == "roll-mode"})
        self.assertIn({"type": "speed", "operation": "multiply", "factor": 0.5}, e)
        self.assertTrue(any(x["type"] == "defense-adjustment" and
                            x.get("value") == -2 for x in e))

    def test_charmed_blocks_non_damage_harm(self):
        e = self.rule("Charmed")["effects"]
        restriction = next(x for x in e if x["type"] == "targeting-restriction")
        self.assertEqual("charmer", restriction["protectedTarget"])
        self.assertIn("harmful-ability", restriction["prohibited"])
        self.assertIn("harmful-magical-effect", restriction["prohibited"])
        self.assertTrue(any(x.get("mode") == "advantage" and
                            x.get("subject") == "charmer" for x in e))

    def test_incapacitated_inheritance_and_independent_restrictions(self):
        base = self.rule("Incapacitated")
        actions = next(x for x in base["effects"] if x["type"] == "action-restriction")
        self.assertEqual({"action", "bonus-action", "reaction"},
                         set(actions["cannotTake"]))
        self.assertIn({"type": "concentration", "operation": "end"}, base["effects"])
        self.assertNotIn("speech", {e["type"] for e in base["effects"]})
        self.assertNotIn("speed", {e["type"] for e in base["effects"]})
        self.assertNotIn("awareness", {e["type"] for e in base["effects"]})
        self.assertFalse(any(e.get("appliesTo") == "initiative" for e in base["effects"]))
        for name in ("Paralyzed", "Petrified", "Stunned", "Unconscious"):
            self.assertIn("condition.incapacitated", self.rule(name)["appliesConditions"])

    def test_invisible_needs_no_surprise_rule(self):
        rule = self.rule("Invisible")
        self.assertTrue(any(e["type"] == "location-clue" and
                            e["detectableBy"] == ["sound", "tracks"]
                            for e in rule["effects"]))
        self.assertTrue(any(e["type"] == "targeting-protection" for e in rule["effects"]))
        attacks = [e for e in rule["effects"] if e["type"] == "roll-mode"]
        self.assertEqual(1, len(attacks))
        self.assertEqual("attacks-against-affected-creature", attacks[0]["subject"])
        self.assertTrue(any(e["type"] == "target-defense-adjustment" and
                            e["when"] == {"targetCanSeeAffectedCreature": False}
                            for e in rule["effects"]))
        self.assertFalse(any(e.get("appliesTo") == "initiative" for e in rule["effects"]))

    def test_paralyzed_automatic_critical_is_not_natural_twenty(self):
        rule = self.rule("Paralyzed")
        self.assertIn({"type": "speech", "availability": "none"}, rule["effects"])
        self.assertIn({"type": "speed", "operation": "set",
                       "value": 0, "canIncrease": False}, rule["effects"])
        critical = next(e for e in rule["effects"] if e["type"] == "critical-hit")
        self.assertEqual(5, critical["when"]["attackerWithinFeet"])
        self.assertTrue(critical["when"]["attackHits"])
        self.assertIs(False, critical["countsAsNatural20"])

    def test_petrified_preserves_awareness_possibility(self):
        rule = self.rule("Petrified")
        self.assertFalse(any(e["type"] == "awareness" for e in rule["effects"]))
        self.assertIn({"type": "immunity", "condition": "condition.poisoned"},
                      rule["effects"])
        self.assertIn({"type": "immunity", "category": "disease"}, rule["effects"])
        suspended = next(e for e in rule["effects"] if e["type"] == "existing-affliction")
        self.assertEqual("suspend-not-neutralize", suspended["operation"])
        self.assertTrue(suspended["resumeWhenConditionEnds"])
        self.assertEqual(10, next(e for e in rule["effects"]
                                  if e["type"] == "weight")["factor"])

    def test_stunned_half_speed_and_faltering_speech(self):
        e = self.rule("Stunned")["effects"]
        self.assertIn({"type": "speed", "operation": "multiply", "factor": 0.5}, e)
        self.assertIn({"type": "speech", "availability": "faltering-only"}, e)
        self.assertEqual({"strength", "dexterity"},
                         {x["ability"] for x in e if x["type"] == "saving-throw"
                          and x["result"] == "automatic-failure"})
        self.assertFalse(any(x.get("subject") == "attacks-against-affected-creature"
                             and x.get("mode") == "advantage" for x in e))
        self.assertTrue(any(x["type"] == "defense-adjustment" and
                            x.get("value") == -2 for x in e))

    def test_prone_standing_cost_and_unconscious_recovery(self):
        prone = self.rule("Prone")
        standing = prone["endsWhen"][0]
        self.assertTrue(standing["requiresPositiveSpeed"])
        self.assertEqual("half-speed", standing["movementCost"]["operation"])
        un = self.rule("Unconscious")
        self.assertIn("condition.prone", un["appliesConditions"])
        self.assertIn("condition.prone", un["persistsAfterEnding"])
        self.assertIn({"event": "drop-held-objects"}, un["onApply"])
        self.assertIn({"type": "awareness", "availability": "none"}, un["effects"])
        crit = next(x for x in un["effects"] if x["type"] == "critical-hit")
        self.assertIs(False, crit["countsAsNatural20"])

    def test_wet_saving_throws_and_all_three_removal_triggers(self):
        rule = self.rule("Wet")
        effects = rule["effects"]
        self.assertEqual(3, len(effects))
        self.assertEqual(["cold", "lightning"],
                         effects[0]["when"]["sourceDealsAnyDamageType"])
        self.assertEqual("disadvantage", effects[0]["mode"])
        self.assertEqual("resist-extreme-cold", effects[1]["when"]["purpose"])
        self.assertEqual("disadvantage", effects[1]["mode"])
        self.assertEqual("fire", effects[2]["when"]["sourceDealsDamageType"])
        self.assertEqual("advantage", effects[2]["mode"])
        self.assertTrue(all(e["appliesTo"] == "saving-throw" for e in effects))
        endings = {x["event"]: x for x in rule["endsWhen"]}
        self.assertEqual(3, len(endings))
        fire = endings["takes-damage"]
        self.assertEqual("fire", fire["damageType"])
        self.assertEqual(5, fire["minimumDamageTaken"])
        self.assertTrue(fire["sameDamageInstance"])
        self.assertTrue(endings["removes-wet-clothing-or-armor"]["unless"]["hasWetFur"])
        self.assertEqual(60, endings["remains-around-heat-source"]["durationMinutes"])
        self.assertEqual("approved-house-rule", self.decisions["condition.wet"]["state"])
        self.assertEqual([], self.decisions["condition.wet"]["evidence"])


if __name__ == "__main__":
    unittest.main()
