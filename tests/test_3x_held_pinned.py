"""D&D 3.0 Held and D&D 3.0/3.5 Pinned native compatibility checks."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class HeldPinnedTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.native = {x["id"]: x for x in load("data/rulesets/first-party-001/native/conditions.json")["conditions"]}
        cls.chapter = load("data/rulesets/first-party-001/native/rules/conditions.json")
        cls.rules = load("data/rulesets/first-party-001/native/held-pinned.json")
        cls.sources = load("data/reference/3x/held-pinned-reconciliation.json")

    def test_catalog_ids_and_shared_conditions(self):
        self.assertGreater(len(self.native), 0)
        self.assertEqual(set(self.native), set(self.chapter["conditionIds"]))
        for name in ("condition.held", "condition.pinned"):
            entity = self.native[name]
            self.assertEqual("condition", entity["kind"])
            self.assertNotIn("candidateDefinitions", entity)
            self.assertNotIn("source", entity)
            self.assertNotIn("edition", entity)

    def test_held_is_helpless_but_not_incapacitated(self):
        held = self.native["condition.held"]
        self.assertEqual(["condition.helpless"], held["appliesConditions"])
        self.assertNotIn("condition.incapacitated", held["appliesConditions"])
        self.assertIn({"type": "speed", "operation": "set", "value": 0,
                       "canIncrease": False}, held["effects"])
        self.assertIn({"type": "speech", "availability": "none"}, held["effects"])
        physical = next(e for e in held["effects"] if e["type"] == "action-restriction")
        self.assertEqual("physical-actions", physical["scope"])
        self.assertEqual(["purely-mental-action"], physical["exceptions"])
        self.assertTrue(physical["doesNotAutomaticallyEndConcentration"])
        self.assertNotIn("awareness", {e["type"] for e in held["effects"]})
        self.assertNotIn("concentration", {e["type"] for e in held["effects"]})

    def test_helpless_defenses_come_from_parent_not_duplicated(self):
        held = self.native["condition.held"]
        self.assertFalse(any(e["type"] == "defense-adjustment" for e in held["effects"]))
        helpless = self.native["condition.helpless"]
        dexterity_zero = next(e for e in helpless["effects"]
                              if e["operation"] == "set-effective-ability-score")
        self.assertEqual(-5, dexterity_zero["modifier"])
        self.assertEqual(-4, next(e for e in helpless["effects"]
                                  if e.get("when") == {"incomingAttackType": "melee"})["value"])
        self.assertFalse(any(e["type"] == "critical-hit" for e in held["effects"]))

    def test_pinned_is_grappled_not_helpless_or_incapacitated(self):
        pinned = self.native["condition.pinned"]
        self.assertEqual(["condition.grappled"], pinned["appliesConditions"])
        self.assertEqual([{"condition": "condition.grappled",
                           "sourceRelation": "same-pinner",
                           "mustBeIndependentlyActive": True}],
                         pinned["requiresConditions"])
        self.assertTrue(pinned["relationships"]["requiresLinkedIndependentGrapple"])
        self.assertNotIn("condition.helpless", pinned["appliesConditions"])
        self.assertNotIn("condition.incapacitated", pinned["appliesConditions"])
        self.assertIn({"type": "speed", "operation": "set", "value": 0,
                       "canIncrease": False}, pinned["effects"])
        self.assertTrue(any(e["type"] == "movement-restriction" for e in pinned["effects"]))
        self.assertFalse(any(e["type"] == "critical-hit" for e in pinned["effects"]))

    def test_pinned_ac_adjustment_scoped_to_outside_attacker(self):
        pinned = self.native["condition.pinned"]
        adjustment = next(e for e in pinned["effects"]
                          if e["type"] == "defense-adjustment")
        self.assertEqual("add-modifier", adjustment["operation"])
        self.assertEqual(-4, adjustment["value"])
        self.assertEqual({"attackerRelationToPinner": "other-than-pinner"},
                         adjustment["when"])
        self.assertEqual("pin-exposure-per-attacker", adjustment["nonStackingGroup"])
        grappled = self.native["condition.grappled"]
        self.assertTrue(any(e.get("when") == {"attackerRelationToGrappler": "other-than-grappler"}
                            and e["type"] == "defense-adjustment"
                            for e in grappled["effects"]))

    def test_optional_speech_suppression_is_not_unconditional(self):
        speech = next(e for e in self.native["condition.pinned"]["effects"]
                      if e["type"] == "speech")
        self.assertEqual("none", speech["availability"])
        self.assertEqual({"pinningCreatureElectsToSilence": True}, speech["when"])
        self.assertTrue(all(x.get("type") != "action-restriction"
                            for x in self.native["condition.pinned"]["effects"]))

    def test_pin_escape_preserves_grapple_but_release_ends_pinner_grapple(self):
        pinned = self.native["condition.pinned"]
        events = {e["event"]: e for e in pinned["endsWhen"]}
        self.assertEqual(1, events["pin-duration-expires"]["durationRounds"])
        self.assertEqual("grappled-remains-unless-separately-ended",
                         events["escapes-pin"]["result"])
        self.assertEqual("grappled-remains-unless-separately-ended",
                         events["pin-broken-by-another-creature"]["result"])
        self.assertEqual("grapple-with-pinner-ends",
                         events["pinner-releases-hold"]["result"])
        self.assertEqual("grapple-with-pinner-ends",
                         events["pinner-becomes-incapacitated"]["result"])
        self.assertTrue(pinned["relationships"]["preservesIndependentGrapples"])

    def test_escape_artist_remains_separate_from_athletics(self):
        options = self.native["condition.pinned"]["escapeOptions"]
        self.assertEqual({"opposed-grapple-check", "escape-artist"},
                         {x["method"] for x in options})
        escape = next(e for e in options if e["method"] == "escape-artist")
        self.assertEqual("skill.escape-artist", escape["skill"])
        self.assertEqual("dexterity", escape["ability"])
        self.assertEqual("pinning-creature-grapple-check", escape["opposition"])
        self.assertEqual("pending-combat-maneuver-reconciliation",
                         escape["nativeActionMapping"])
        self.assertEqual("pending-combat-maneuver-reconciliation",
                         options[0]["nativeActionMapping"])

    def test_pinned_spellcasting_is_recorded_not_runtime_mapped(self):
        spell = next(e for e in self.native["condition.pinned"]["effects"]
                     if e["type"] == "spellcasting-restriction")
        self.assertEqual(["somatic"], spell["prohibitedComponents"])
        self.assertEqual({"baseDC": 20, "perSpellLevel": 1}, spell["concentrationCheck"])
        self.assertEqual("pending-native-spellcasting-reconciliation",
                         spell["runtimeMapping"])

    def test_3x_held_conversion_and_pinned_separate_from_5e_grappler_feat(self):
        profile = self.rules["historicalEquivalence"]
        self.assertEqual("native-condition.held", profile["threeZeroHeld"])
        self.assertEqual("applies-condition.paralyzed", profile["threeFiveHoldPerson"])
        self.assertEqual("native-condition.pinned", profile["threeZeroAndThreeFivePinned"])
        self.assertIn("restrained", profile["fiveOneGrapplerFeat"])
        self.assertEqual(5, len(self.sources["decisions"]))
        self.assertEqual({"D&D 3.0", "D&D 3.5"},
                         {x["edition"] for x in self.sources["sourceMaterials"]})
        self.assertEqual(16, len(load("data/reference/srd/reconciliation-decisions.json")["decisions"]))

    def test_preserves_approved_older_native_rulings(self):
        self.assertEqual(0.5, next(e for e in self.native["condition.stunned"]["effects"]
                                   if e["type"] == "speed")["factor"])
        self.assertFalse(any(e["type"] == "awareness"
                             for e in self.native["condition.petrified"]["effects"]))
        self.assertEqual(6, len(self.native["condition.exhaustion"]["levels"]))
        self.assertEqual(3, len(self.native["condition.wet"]["effects"]))
        self.assertEqual({"condition.incapacitated", "condition.helpless"},
                         set(self.native["condition.paralyzed"]["appliesConditions"]))


if __name__ == "__main__":
    unittest.main()
