"""Standalone third-edition condition semantics, source identity and exclusions."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class ThreeXStandaloneConditionsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = load("data/rulesets/first-party-001/native/conditions.json")
        cls.native = {x["id"]: x for x in cls.catalog["conditions"]}
        cls.chapter = load("data/rulesets/first-party-001/native/rules/conditions.json")
        cls.decisions = load("data/reference/3x/standalone-conditions.json")
        cls.srd51_52 = load("data/reference/srd/structured/conditionsdiseases.json")["condition"]

    def get(self, name):
        return self.native["condition." + name]

    def test_six_unique_native_conditions_and_unchanged_srd_snapshot(self):
        self.assertEqual(len(self.catalog["conditions"]), len(self.native),
                         "Native condition IDs must be unique")
        self.assertEqual(set(self.native), set(self.chapter["conditionIds"]))
        for key in ("dazed", "dazzled", "entangled", "fascinated", "nauseated", "sickened"):
            entry = self.get(key)
            self.assertEqual("condition", entry["kind"])
            self.assertTrue(entry["effects"])
            self.assertNotIn("candidateDefinitions", entry)
            self.assertNotIn("baselineStatus", entry)
            self.assertFalse([x for x in self.srd51_52 if x["name"] == entry["name"]])
        self.assertEqual(30, len(self.srd51_52))
        self.assertEqual(6, len(self.decisions["decisions"]))
        self.assertEqual(16, len(load("data/reference/srd/reconciliation-decisions.json")["decisions"]))

    def test_dazed_does_not_inherit_incapacitated_concentration_loss(self):
        rule = self.get("dazed")
        self.assertEqual(
            [{"type": "action-restriction", "cannotTake": ["action", "bonus-action", "reaction"]}],
            rule["effects"]
        )
        self.assertNotIn("condition.incapacitated", rule.get("appliesConditions", []))
        self.assertFalse(any(e["type"] in ("concentration", "defense-adjustment")
                             for e in rule["effects"]))
        self.assertFalse(any(e["type"] in ("speed", "movement-restriction")
                             for e in rule["effects"]))

    def test_dazzled_uses_srd35_extra_checks_without_blinding(self):
        effects = self.get("dazzled")["effects"]
        self.assertEqual(2, len(effects))
        self.assertEqual(-1, effects[0]["value"])
        self.assertEqual("attack-roll", effects[0]["appliesTo"])
        self.assertEqual({"skill.search", "skill.spot"}, set(effects[1]["skills"]))
        self.assertTrue(effects[1]["appliesOnceToComposedCheck"])
        self.assertFalse(any(e["type"] == "sense" for e in effects))

    def test_entangled_anchors_dex_penalty_and_disruption_scope(self):
        effects = self.get("entangled")["effects"]
        unanchored = next(x for x in effects if x["type"] == "speed" and x["operation"] == "multiply")
        anchored = next(x for x in effects if x["type"] == "speed" and x["operation"] == "set")
        self.assertEqual({"anchored": False}, unanchored["when"])
        self.assertEqual(0.5, unanchored["factor"])
        self.assertEqual({"anchored": True}, anchored["when"])
        self.assertEqual(0, anchored["value"])
        self.assertEqual({"run", "charge"}, set(next(e for e in effects
                                                   if e["type"] == "movement-restriction")["prohibited"]))
        penalty = next(e for e in effects if e["type"] == "effective-ability-score")
        self.assertEqual("dexterity", penalty["ability"])
        self.assertEqual(-4, penalty["value"])
        self.assertTrue(penalty["affectsDerivedStatistics"])
        self.assertEqual(-2, next(e for e in effects
                                  if e.get("appliesTo") == "attack-roll")["value"])
        spell = next(e for e in effects if e["type"] == "spellcasting-disruption")
        self.assertEqual({"base": 15, "perSpellLevel": 1}, spell["dc"])
        self.assertEqual("awaiting-native-spellcasting-reconciliation",
                         spell["integrationStatus"])

    def test_fascinated_ends_on_obvious_threat_or_ally_action(self):
        rule = self.get("fascinated")
        reactions = next(e for e in rule["effects"] if e.get("when") ==
                         {"purpose": "reaction-to-surroundings"})
        self.assertEqual(-4, reactions["value"])
        self.assertEqual({"skill.listen", "skill.spot"}, set(reactions["skills"]))
        self.assertTrue(reactions["appliesOnceToComposedCheck"])
        self.assertTrue(any(e["type"] == "saving-throw-opportunity" for e in rule["effects"]))
        self.assertEqual({"obvious-threat-appears", "ally-shakes-creature-free",
                          "fascinating-effect-ends"},
                         {x["event"] for x in rule["endsWhen"]})
        self.assertEqual("action", next(x for x in rule["endsWhen"]
                                        if x["event"] == "ally-shakes-creature-free")["actionCost"])

    def test_nauseated_limits_turn_without_confusing_with_incapacitated(self):
        rule = self.get("nauseated")
        budget = next(e for e in rule["effects"] if e["type"] == "turn-activity-limit")
        self.assertEqual(1, budget["maxMoveEquivalentActivitiesPerTurn"])
        self.assertEqual("pending-review", budget["nativeActionMapping"])
        self.assertIn("movement", budget["allowed"])
        self.assertIn("cast-spell", budget["prohibited"])
        self.assertIn({"type": "concentration", "operation": "end"}, rule["effects"])
        self.assertNotIn("condition.incapacitated", rule.get("appliesConditions", []))

    def test_sickened_numeric_damage_saves_and_check_scopes(self):
        effects = self.get("sickened")["effects"]
        self.assertEqual({"attack-roll", "weapon-damage-roll", "saving-throw",
                          "skill-check", "ability-check"},
                         {e["appliesTo"] for e in effects})
        self.assertTrue(all(e["operation"] == "add" and e["value"] == -2 for e in effects))
        self.assertTrue(all(e["appliesOnceToComposedCheck"] for e in effects
                            if e["appliesTo"] in ("skill-check", "ability-check")))
        self.assertFalse(any(e["type"] == "roll-mode" for e in effects))
        self.assertNotIn("condition.poisoned", self.get("sickened").get("appliesConditions", []))

    def test_deferred_other_conditions_and_provenance(self):
        self.assertEqual("native-data-first-pass; spellcasting-and-action-economy-integration-pending",
                         self.decisions["status"])
        self.assertIn("Staggered", self.decisions["remainingCandidates"])
        self.assertIn("Pinned", self.decisions["remainingCandidates"])
        self.assertEqual({"D&D 3.0", "D&D 3.5"},
                         {s["edition"] for s in self.decisions["sourceMaterials"]})
        self.assertNotIn("condition.staggered", self.native)

    def test_flat_footed_helpless_and_original_rulings_preserved(self):
        self.assertIn("condition.flat-footed", self.native)
        self.assertIn("condition.helpless", self.native)
        self.assertEqual(0.5, next(e for e in self.get("stunned")["effects"]
                                   if e["type"] == "speed")["factor"])
        self.assertFalse(any(e["type"] == "awareness" for e in self.get("petrified")["effects"]))
        self.assertEqual(6, len(self.get("exhaustion")["levels"]))
        self.assertEqual(3, len(self.get("wet")["effects"]))


if __name__ == "__main__":
    unittest.main()
