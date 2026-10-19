"""Third-edition fear-family regression tests against selected native mechanics."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class FearConditionsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.native = {x["id"]: x for x in load("data/native/conditions.json")["conditions"]}
        cls.chapter = load("data/native/rules/conditions.json")
        cls.family = load("data/native/fear.json")
        cls.evidence = load("data/reference/3x/fear-conditions.json")

    def condition(self, name):
        return self.native["condition." + name]

    def test_unique_native_fear_states_and_unchanged_frightened(self):
        self.assertEqual(27, len(self.native))
        self.assertEqual(set(self.native), set(self.chapter["conditionIds"]))
        self.assertEqual({"condition.shaken", "condition.frightened",
                          "condition.panicked", "condition.cowering"},
                         set(self.family["conditions"]))
        frightened = self.condition("frightened")["effects"]
        self.assertEqual(3, len(frightened))
        self.assertEqual({"fearSourceWithinLineOfSight": True}, frightened[0]["when"])
        self.assertEqual("cannot-willingly-move-closer", frightened[2]["restriction"])
        self.assertFalse(any(x["type"] == "compelled-movement" for x in frightened))

    def test_shaken_penalties_apply_once_per_roll(self):
        shaken = self.condition("shaken")
        self.assertEqual({"attack-roll", "saving-throw", "skill-check", "ability-check"},
                         {e["appliesTo"] for e in shaken["effects"]})
        self.assertTrue(all(x["value"] == -2 and x["operation"] == "add"
                            for x in shaken["effects"]))
        self.assertTrue(all(x["nonStackingGroup"] == "fear-impairment"
                            for x in shaken["effects"]))
        self.assertFalse(any(x["type"] in ("compelled-movement", "speed")
                             for x in shaken["effects"]))
        self.assertTrue(all(x["appliesOnceToComposedCheck"] for x in shaken["effects"]
                            if x["appliesTo"] in ("skill-check", "ability-check")))

    def test_panicked_drops_everything_at_application_not_randomly(self):
        panicked = self.condition("panicked")
        self.assertEqual([{"event": "drop-held-objects"}], panicked["onApply"])
        movement = next(e for e in panicked["effects"] if e["type"] == "compelled-movement")
        self.assertEqual("maximum-available", movement["speed"])
        self.assertEqual("random-viable-escape-route", movement["route"])
        self.assertTrue(movement["cannotWillinglyApproachFearSource"])
        scope = next(e for e in panicked["effects"] if e["type"] == "turn-activity-limit")
        self.assertEqual(["flee", "spell-or-feature-used-only-to-escape"], scope["allowed"])
        self.assertIn("attack", scope["prohibited"])
        self.assertEqual("use-normal-costs-of-permitted-options", scope["actionCosts"])
        self.assertFalse(any(x["type"] == "concentration" for x in panicked["effects"]))

    def test_panicked_save_penalties_not_stacked_with_shaken(self):
        panicked = self.condition("panicked")
        penalties = [x for x in panicked["effects"] if x["type"] == "roll-modifier"]
        self.assertEqual({"saving-throw", "skill-check", "ability-check"},
                         {x["appliesTo"] for x in penalties})
        self.assertTrue(all(x["value"] == -2 and
                            x["nonStackingGroup"] == "fear-impairment" for x in penalties))
        self.assertEqual("apply-once-per-affected-roll",
                         self.family["combinedEffectPolicy"]["numericPenalty"])
        self.assertTrue(self.family["escalation"]["notAdoptedAutomatically"])

    def test_cornered_panicked_derived_cowering_can_end_when_route_opens(self):
        panicked = self.condition("panicked")
        self.assertEqual([{
            "condition": "condition.cowering",
            "when": {"escapeRouteAvailable": False},
            "endsWhen": {"escapeRouteAvailable": True},
            "doesNotOverwriteIndependentApplications": True
        }], panicked["derivedConditions"])
        self.assertNotIn("condition.cowering", panicked.get("appliesConditions", []))
        self.assertEqual("while-panicked-and-unable-to-flee",
                         self.family["interactions"]["panickedCornered"]["duration"])

    def test_cowering_ac_penalty_not_helpless_or_blind_double_count(self):
        cower = self.condition("cowering")
        e = cower["effects"]
        self.assertIn({"type": "speed", "operation": "set", "value": 0,
                       "canIncrease": False}, e)
        self.assertIn({"type": "action-restriction",
                       "cannotTake": ["action", "bonus-action", "reaction"]}, e)
        self.assertTrue(any(x["type"] == "defense-adjustment" and
                            x["operation"] == "suppress-contributions" and
                            x["oncePerComponent"] for x in e))
        self.assertIn({"type": "defense-adjustment", "operation": "add-modifier",
                       "defense": "armor-class", "value": -2}, e)
        self.assertNotIn("condition.helpless", cower.get("appliesConditions", []))
        self.assertNotIn("condition.incapacitated", cower.get("appliesConditions", []))
        self.assertFalse(any(x["type"] == "concentration" for x in e))
        self.assertNotIn("onApply", cower)

    def test_3e_35e_differences_recorded_without_mixing_source_licenses(self):
        diffs = {x["name"]: x for x in self.evidence["sourceEditionDifferences"]}
        self.assertIn("50%", diffs["Panicked"]["edition3e"])
        self.assertIn("Always drops", diffs["Panicked"]["edition35e"])
        self.assertIn("drops", diffs["Cowering"]["edition3e"].lower())
        self.assertIn("does not explicitly drop", diffs["Cowering"]["edition35e"])
        self.assertIn("Fear escalation", diffs)
        self.assertEqual("selected-native-conditions-added; edition-specific-escalation-policy-deferred",
                         self.evidence["status"])
        self.assertTrue(all(x["edition"].startswith("D&D 3.") for x in self.evidence["sourceMaterials"]))

    def test_no_silent_fear_escalation_and_approved_other_mechanics(self):
        self.assertEqual("deferred", self.family["escalation"]["status"])
        self.assertNotIn("condition.shaken", self.condition("frightened").get("appliesConditions", []))
        self.assertFalse(any(x["type"] == "compelled-movement"
                             for x in self.condition("frightened")["effects"]))
        self.assertEqual(0.5, next(x for x in self.condition("stunned")["effects"]
                                   if x["type"] == "speed")["factor"])
        self.assertFalse(any(x["type"] == "awareness"
                             for x in self.condition("petrified")["effects"]))
        self.assertEqual(6, len(self.condition("exhaustion")["levels"]))
        self.assertEqual(3, len(self.condition("wet")["effects"]))


if __name__ == "__main__":
    unittest.main()
