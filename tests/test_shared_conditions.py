"""Validate identical-meaning reconciled native conditions (data-only)."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def get(p):
    return json.loads((ROOT / p).read_text(encoding="utf-8"))


class SharedConditionReconciliationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.native = {x["id"]: x for x in get("data/native/conditions.json")["conditions"]}
        cls.decisions = {
            x["nativeRuleId"]: x for x in get("data/reference/srd/reconciliation-decisions.json")["decisions"]
        }
        cls.source = get("data/reference/srd/structured/conditionsdiseases.json")["condition"]

    def test_native_coverage_and_external_edition_separation(self):
        self.assertEqual(24, len(self.native))
        self.assertEqual(set(self.native) - { "condition.flat-footed", "condition.helpless", "condition.dazed", "condition.dazzled", "condition.entangled", "condition.fascinated", "condition.nauseated", "condition.sickened" }, set(self.decisions))
        self.assertEqual(set(), {
            key for key, value in self.native.items()
            if value.get("baselineStatus") == "pending-native-reconciliation"
        })
        for condition in self.native.values():
            self.assertNotIn("candidateDefinitions", condition)
            for forbidden in ("source", "edition", "srd", "srd52", "sourceAliases"):
                self.assertNotIn(forbidden, condition)
        for name in ("Blinded", "Charmed", "Incapacitated", "Invisible",
                     "Paralyzed", "Petrified", "Prone", "Stunned", "Unconscious"):
            condition = self.native["condition." + name.lower()]
            self.assertTrue(condition["effects"])
            self.assertEqual("condition", condition["kind"])
            self.assertEqual({"SRD51", "SRD52"},
                             {s["source"] for s in self.source if s["name"] == name})
        wet = self.native["condition.wet"]
        self.assertEqual("Wet", wet["name"])
        self.assertFalse([s for s in self.source if s["name"] == "Wet"])
        self.assertEqual("approved-house-rule", self.decisions["condition.wet"]["state"])
        self.assertEqual([], self.decisions["condition.wet"]["evidence"])

    def test_deafened(self):
        self.assertEqual([
            {"type": "sense", "sense": "hearing", "availability": "none"},
            {"type": "check", "appliesTo": "ability-check",
             "requiresSense": "hearing", "result": "automatic-failure"},
        ], self.native["condition.deafened"]["effects"])

    def test_frightened_line_of_sight_and_direction(self):
        effects = self.native["condition.frightened"]["effects"]
        self.assertEqual(3, len(effects))
        for e, target in zip(effects[:2], ("ability-check", "attack-roll")):
            self.assertEqual("roll-mode", e["type"])
            self.assertEqual("disadvantage", e["mode"])
            self.assertEqual(target, e["appliesTo"])
            self.assertEqual({"fearSourceWithinLineOfSight": True}, e["when"])
        self.assertEqual("cannot-willingly-move-closer", effects[2]["restriction"])
        self.assertEqual("fear-source", effects[2]["relativeTo"])

    def test_poisoned_attacks_and_ability_checks(self):
        effects = self.native["condition.poisoned"]["effects"]
        self.assertEqual({"attack-roll", "ability-check"}, {e["appliesTo"] for e in effects})
        self.assertTrue(all(e["mode"] == "disadvantage" for e in effects))

    def test_restrained_effect_subjects_and_save(self):
        effects = self.native["condition.restrained"]["effects"]
        self.assertEqual({"type": "speed", "operation": "set", "value": 0, "canIncrease": False},
                         effects[0])
        self.assertEqual("advantage", effects[1]["mode"])
        self.assertEqual("attacks-against-affected-creature", effects[1]["subject"])
        self.assertEqual("disadvantage", effects[2]["mode"])
        self.assertEqual("attacks-by-affected-creature", effects[2]["subject"])
        self.assertEqual({"type": "roll-mode", "appliesTo": "saving-throw",
                          "ability": "dexterity", "mode": "disadvantage"}, effects[3])


    def test_grappled_target_relative_attack_penalty_and_speed(self):
        rule = self.native["condition.grappled"]
        self.assertEqual("condition", rule["kind"])
        self.assertEqual(3, len(rule["effects"]))
        self.assertEqual(
            {"type": "speed", "operation": "set", "value": 0, "canIncrease": False},
            rule["effects"][0]
        )
        attack_penalty = rule["effects"][1]
        self.assertEqual("roll-mode", attack_penalty["type"])
        self.assertEqual("attack-roll", attack_penalty["appliesTo"])
        self.assertEqual("disadvantage", attack_penalty["mode"])
        self.assertEqual("attacks-by-affected-creature", attack_penalty["subject"])
        self.assertEqual({"targetRelationToGrappler": "other-than-grappler"},
                         attack_penalty["when"])
        self.assertNotIn("attacks-against-affected-creature",
                         [e.get("subject") for e in rule["effects"]])
        self.assertNotIn("ability-check",
                         [e.get("appliesTo") for e in rule["effects"]])
        self.assertEqual("defense-adjustment", rule["effects"][2]["type"])
        self.assertEqual({"attackerRelationToGrappler": "other-than-grappler"},
                         rule["effects"][2]["when"])

    def test_grappled_ending_triggers_and_source_reconciliation(self):
        rule = self.native["condition.grappled"]
        self.assertEqual([
            {"event": "grappler-becomes-incapacitated"},
            {"event": "grappled-creature-moves-outside-grapple-reach", "cause": "any-effect"},
        ], rule["endsWhen"])
        choice = self.decisions["condition.grappled"]
        self.assertEqual("approved-native-rule", choice["state"])
        self.assertIn("SRD 5.1 movement", choice["adoptedVariant"])
        self.assertIn("SRD 5.2.1 attack penalty", choice["adoptedVariant"])
        self.assertEqual({"SRD51", "SRD52"}, {s["source"] for s in choice["evidence"]})
        self.assertEqual({"SRD51", "SRD52"},
                         {s["source"] for s in self.source if s["name"] == "Grappled"})
        self.assertTrue(choice["notYetReconciled"])

    def test_evidence_pairs_and_approved_exhaustion_unchanged(self):
        original = self.native["condition.exhaustion"]
        self.assertEqual([1, 2, 3, 4, 5, 6], [e["level"] for e in original["levels"]])
        self.assertEqual("SRD 5.1 exhaustion", self.decisions["condition.exhaustion"]["adoptedVariant"])
        for name in ("Deafened", "Frightened", "Poisoned", "Restrained"):
            identifier = "condition." + name.lower()
            choice = self.decisions[identifier]
            self.assertEqual("reconciled-identical-mechanics", choice["state"])
            self.assertEqual({"SRD51", "SRD52"}, {x["source"] for x in choice["evidence"]})
            matches = [x for x in self.source if x["name"] == name]
            self.assertEqual({"SRD51", "SRD52"}, {x["source"] for x in matches})


if __name__ == "__main__":
    unittest.main()
