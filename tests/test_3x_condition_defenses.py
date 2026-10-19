"""Regression matrix for the reviewed native Touch/Flat-Footed/Helpless defense rules.

This test includes a miniature *test-only* interpreter of the approved data
contract. The rewrite has no executable combat evaluator at this milestone.
"""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


def evaluate(defender_conditions=(), attacker_conditions=(), *,
             target="normal", attack_kind="melee", target_sees_attacker=True,
             attacker_is_grappler=False, dexterity=3, dodge=0, armor=4, shield=2,
             natural_armor=0, size=0, deflection=0, other=0):
    """Test the declared contribution and stacking model, not production code."""
    by_id = {e["id"]: e for e in load("data/native/conditions.json")["conditions"]}
    defenses = load("data/native/defenses.json")
    assert target in defenses["attackTargeting"]["supported"]

    active = set(defender_conditions)
    pending = list(active)
    while pending:
        c = by_id["condition." + pending.pop()]
        for d in c.get("appliesConditions", []):
            child = d.removeprefix("condition.")
            if child not in active:
                active.add(child)
                pending.append(child)

    def predicate(when):
        if not when:
            return True
        return all({
            "incomingAttackType": attack_kind,
            "attackerRelationToGrappler": ("grappler" if attacker_is_grappler
                                           else "other-than-grappler"),
            "targetCanSeeAffectedCreature": target_sees_attacker,
        }.get(key) == value for key, value in when.items())

    effects = []
    for state in active:
        effects.extend(e for e in by_id["condition." + state].get("effects", [])
                       if e["type"] == "defense-adjustment" and predicate(e.get("when")))
    if "invisible" in attacker_conditions:
        for e in by_id["condition.invisible"]["effects"]:
            if e["type"] == "target-defense-adjustment" and predicate(e.get("when")):
                effects.append(e)
    ignore = set(defenses["attackTargeting"][target]["ignores"])
    suppress = any(e["operation"] == "suppress-contributions" for e in effects)
    helpless = [e for e in effects if e["operation"] == "set-effective-ability-score"]
    assert len(helpless) <= 1, "Overlapping helpless effects must be applied once"
    dex = helpless[0]["modifier"] if helpless else min(dexterity, 0) if suppress else dexterity
    dodge_value = 0 if (suppress or helpless) else dodge
    categories = {
        "armor": armor, "shield": shield, "natural-armor": natural_armor,
        "size": size, "deflection": deflection, "other": other
    }
    computed = defenses["baseArmorClass"] + dex + dodge_value
    computed += sum(value for key, value in categories.items() if key not in ignore)
    adjustments = {}
    for e in effects:
        if e["operation"] != "add-modifier":
            continue
        group = e.get("nonStackingGroup")
        if group:
            previous = adjustments.get(group)
            if previous is None or abs(e["value"]) > abs(previous):
                adjustments[group] = e["value"]
        else:
            adjustments.setdefault("__ungrouped__", []).append(e["value"])
    return computed + sum(v for k, v in adjustments.items() if k != "__ungrouped__") + sum(adjustments.get("__ungrouped__", []))


class ThreeXConditionDefenseTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = load("data/native/defenses.json")
        cls.conditions = {e["id"]: e for e in load("data/native/conditions.json")["conditions"]}
        cls.source = load("data/reference/3x/condition-defense-reconciliation.json")

    def test_normal_touch_flat_footed_and_combined_profiles(self):
        self.assertEqual(19, evaluate())
        self.assertEqual(13, evaluate(target="touch"))
        self.assertEqual(16, evaluate(("flat-footed",)))
        self.assertEqual(10, evaluate(("flat-footed",), target="touch"))
        self.assertEqual(4, len(self.data["derivedProfiles"]))
        self.assertEqual("normal", self.data["attackTargeting"]["default"])

    def test_touch_not_triggered_by_conditions(self):
        for state in ("blinded", "stunned", "grappled", "paralyzed", "restrained"):
            effects = self.conditions["condition." + state]["effects"]
            self.assertFalse(any(e.get("operation") == "target-touch" for e in effects))
        self.assertTrue(self.data["attackTargeting"]["touch"]["onlyWhenAttackExplicitlyTargetsTouch"])

    def test_positive_dexterity_and_dodge_denied_only_once(self):
        self.assertEqual(21, evaluate(dodge=2))
        self.assertEqual(16, evaluate(("flat-footed",), dodge=2))
        self.assertEqual(16, evaluate(("flat-footed", "grappled"), dodge=2))
        self.assertEqual(6, evaluate(("blinded", "stunned"), target="touch", dodge=2))
        # Negative Dexterity remains a penalty; it is not erased.
        self.assertEqual(14, evaluate(("flat-footed",), dexterity=-2))

    def test_blinded_speed_ac_penalty_and_no_duplicate_advantage(self):
        blinded = self.conditions["condition.blinded"]["effects"]
        self.assertIn({"type": "speed", "operation": "multiply", "factor": 0.5}, blinded)
        self.assertEqual(14, evaluate(("blinded",)))
        self.assertEqual(8, evaluate(("blinded",), target="touch"))
        self.assertFalse(any(e.get("mode") == "advantage" and
                             e.get("subject") == "attacks-against-affected-creature"
                             for e in blinded))
        self.assertTrue(any(e.get("mode") == "disadvantage" and
                            e.get("subject") == "attacks-by-affected-creature"
                            for e in blinded))

    def test_stunned_half_speed_and_defensive_penalty(self):
        self.assertEqual(14, evaluate(("stunned",)))
        self.assertEqual(8, evaluate(("stunned",), target="touch"))
        self.assertEqual(12, evaluate(("blinded", "stunned")))
        state = self.conditions["condition.stunned"]
        self.assertIn({"type": "speed", "operation": "multiply", "factor": 0.5}, state["effects"])
        self.assertNotIn({"event": "drop-held-objects"}, state.get("onApply", []))

    def test_grappling_defense_is_attacker_relative(self):
        self.assertEqual(19, evaluate(("grappled",), attacker_is_grappler=True))
        self.assertEqual(16, evaluate(("grappled",), attacker_is_grappler=False))
        self.assertEqual(13, evaluate(("grappled",), attacker_is_grappler=True, target="touch"))
        self.assertEqual(10, evaluate(("grappled",), attacker_is_grappler=False, target="touch"))

    def test_invisible_attacker_denies_target_dex_only_when_unseen(self):
        self.assertEqual(16, evaluate(attacker_conditions=("invisible",), target_sees_attacker=False))
        self.assertEqual(10, evaluate(attacker_conditions=("invisible",), target="touch",
                                      target_sees_attacker=False))
        self.assertEqual(19, evaluate(attacker_conditions=("invisible",), target_sees_attacker=True))
        state = self.conditions["condition.invisible"]
        self.assertFalse(any(e.get("mode") == "advantage" and
                             e.get("subject") == "attacks-by-affected-creature"
                             for e in state["effects"]))
        self.assertTrue(any(e.get("mode") == "disadvantage" and
                            e.get("subject") == "attacks-against-affected-creature"
                            for e in state["effects"]))

    def test_prone_is_melee_ranged_not_attacker_distance(self):
        self.assertEqual(15, evaluate(("prone",), attack_kind="melee"))
        self.assertEqual(23, evaluate(("prone",), attack_kind="ranged"))
        self.assertEqual(9, evaluate(("prone",), attack_kind="melee", target="touch"))
        state = self.conditions["condition.prone"]
        self.assertFalse(any("attackerWithinFeet" in e.get("when", {}) for e in state["effects"]))
        self.assertFalse(any(e.get("subject") == "attacks-against-affected-creature"
                             for e in state["effects"]))

    def test_helpless_dexterity_zero_and_nonstacking_melee(self):
        self.assertEqual(7, evaluate(("helpless",), attack_kind="melee"))
        self.assertEqual(11, evaluate(("helpless",), attack_kind="ranged"))
        self.assertEqual(1, evaluate(("helpless",), target="touch", attack_kind="melee"))
        self.assertEqual(7, evaluate(("helpless", "prone"), attack_kind="melee"))
        self.assertEqual(15, evaluate(("helpless", "prone"), attack_kind="ranged"))
        self.assertEqual(7, evaluate(("unconscious",), attack_kind="melee"))
        self.assertEqual(1, evaluate(("unconscious",), target="touch", attack_kind="melee"))
        self.assertEqual(7, evaluate(("paralyzed",), attack_kind="melee"))

    def test_flat_footed_is_more_than_dexterity_denial(self):
        state = self.conditions["condition.flat-footed"]
        rx = next(e for e in state["effects"] if e["type"] == "reaction-restriction")
        self.assertEqual(["opportunity-attack"], rx["prohibited"])
        self.assertTrue(rx["doesNotProhibitOtherReactions"])
        grappled = self.conditions["condition.grappled"]["effects"]
        self.assertFalse(any(e["type"] == "reaction-restriction" for e in grappled))

    def test_petrified_awareness_and_other_approved_rules_preserved(self):
        petrified = self.conditions["condition.petrified"]
        self.assertNotIn("condition.unconscious", petrified["appliesConditions"])
        self.assertNotIn("condition.helpless", petrified["appliesConditions"])
        self.assertFalse(any(e["type"] == "awareness" for e in petrified["effects"]))
        self.assertIn({"type": "speed", "operation": "multiply", "factor": 0.5},
                      self.conditions["condition.stunned"]["effects"])
        self.assertEqual(6, len(self.conditions["condition.exhaustion"]["levels"]))
        self.assertEqual(3, len(self.conditions["condition.wet"]["effects"]))

    def test_3x_decisions_separate_from_original_5x_source_record(self):
        self.assertEqual(14, len(self.source["decisions"]))
        native = set(self.conditions)
        self.assertEqual(18, len(native))
        assert all(("condition." + key) in native for key in ("flat-footed", "helpless"))
        src = load("data/reference/srd/structured/conditionsdiseases.json")["condition"]
        self.assertEqual(30, len(src))
        self.assertEqual(16, len(load("data/reference/srd/reconciliation-decisions.json")["decisions"]))


if __name__ == "__main__":
    unittest.main()
