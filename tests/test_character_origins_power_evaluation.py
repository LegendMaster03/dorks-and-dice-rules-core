"""Regression fixtures for a read-only characteristic power evaluation prototype."""
import json
import copy
from pathlib import Path
import tempfile
import unittest

from tools import character_origins_power_evaluation as power


class PowerEvaluationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        root = Path(self.directory.name)

        def write(path, data):
            destination = root / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_text(json.dumps(data), encoding="utf-8")

        write("species/base/dwarf.json", {
            "id": "species.dwarf", "kind": "species", "name": "Dwarf", "selectable": True,
            "walkingSpeedFeet": 25, "darkvisionFeet": 60,
            "traits": [{"traitId": "trait.dwarf.toughness"},
                       {"traitId": "trait.dwarf.stonebound"}]})
        write("species/base/gnome.json", {
            "id": "species.gnome", "kind": "species", "name": "Gnome", "selectable": True,
            "walkingSpeedFeet": 25, "traits": []})
        write("species/ancestry/gnome/rock.json", {
            "id": "ancestry.gnome.rock", "kind": "ancestry",
            "parent": "species.gnome", "selectable": True,
            "traits": [{"traitId": "trait.gnome.earthbound"}]})
        write("species/base/dragonborn.json", {
            "id": "species.dragonborn", "kind": "species", "name": "Dragonborn",
            "walkingSpeedFeet": 30, "traits": [{"traitId": "trait.dragonborn.breath"}]})
        write("species/ancestry/dragonborn/chromatic.json", {
            "id": "ancestry.dragonborn.chromatic", "kind": "ancestry",
            "parent": "species.dragonborn", "selectable": False,
            "breathWeaponArea": {"shape": "line", "lengthFeet": 30, "widthFeet": 5},
            "traits": []})
        write("species/ancestry/dragonborn/chromatic/red.json", {
            "id": "ancestry.dragonborn.red", "kind": "ancestry",
            "parent": "ancestry.dragonborn.chromatic", "selectable": True,
            "damageType": "fire", "traits": []})
        write("species/base/elf.json", {
            "id": "species.elf", "kind": "species", "name": "Elf",
            "walkingSpeedFeet": 30, "traits": []})
        write("species/ancestry/elf/wood.json", {
            "id": "ancestry.elf.wood", "kind": "ancestry",
            "parent": "species.elf", "walkingSpeedFeet": 35,
            "selectable": True, "traits": []})
        write("species/ancestry/elf/drow.json", {
            "id": "ancestry.elf.drow", "kind": "ancestry",
            "parent": "species.elf", "darkvisionFeet": 120,
            "selectable": True, "traits": [{"traitId": "trait.elf.sunlight"}]})
        write("traits/dwarf/species.dwarf.json", [
            {"id": "trait.dwarf.toughness", "kind": "origin-trait",
             "trait": {"id": "dwarven-toughness", "hitPointsPerCharacterLevel": 1}},
            {"id": "trait.dwarf.stonebound", "kind": "origin-trait",
             "trait": {"id": "stonebound-tremorsense", "sense": "tremorsense",
                       "rangeFeet": 60, "requiresContact": "stone",
                       "activation": "bonus-action", "durationMinutes": 10,
                       "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}}}])
        write("traits/gnome/rock.json", [
            {"id": "trait.gnome.earthbound", "kind": "origin-trait",
             "trait": {"id": "earthbound-tremorsense", "sense": "tremorsense",
                       "rangeFeet": 15, "activation": "passive",
                       "requiresSharedContact": ["earth", "stone"]}}])
        write("traits/dragonborn/base.json", [
            {"id": "trait.dragonborn.breath", "kind": "origin-trait",
             "trait": {"id": "breath-weapon", "damage": {"progression": [
                 {"characterLevel": 1, "dice": "1d10"},
                 {"characterLevel": 5, "dice": "2d10"}]},
                 "areaFrom": "selected-dragonborn-family-or-leaf",
                 "activation": {"action": "attack", "replaces": "one-attack"},
                 "uses": {"value": "proficiency-bonus"},
                 "savingThrow": {"ability": "dexterity", "onSuccess": "half-damage"}}}])
        write("traits/elf/drow.json", [
            {"id": "trait.elf.sunlight", "kind": "origin-trait",
             "trait": {"id": "sunlight-sensitivity", "condition": "condition.dazzled",
                       "trigger": "direct-unprotected-sunlight"}}])
        write("conditions.json", {"entries": [{"id": "condition.dazzled", "effects": [
            {"type": "roll-modifier", "appliesTo": "attack-roll", "operation": "add", "value": -1},
            {"type": "roll-modifier", "appliesTo": "skill-check", "operation": "add",
             "value": -1, "skills": ["skill.search", "skill.spot"]}]}]})
        # Selected fragments of actual class progression fields: Extra Attack
        # and BAB iteratives must be alternative, not additive; spell slots
        # determine capacity, not the power or legality of an unknown spell.
        write("classes/base/barbarian/barbarian.json", {
            "id": "class.barbarian", "kind": "class", "definition": {
                "extraAttackAndIteratives": "alternative-procedures-not-cumulative",
                "progression": [
                    {"level": 1, "baseAttackBonus": 1, "iterativeBaseAttackBonuses": [1],
                     "rageUses": 2, "battleRageDamage": 2,
                     "featureGrants": ["class-feature.barbarian.rage-level-1"]},
                    {"level": 4, "baseAttackBonus": 4, "iterativeBaseAttackBonuses": [4],
                     "rageUses": 3, "battleRageDamage": 2, "featureGrants": []},
                    {"level": 5, "baseAttackBonus": 5, "iterativeBaseAttackBonuses": [5],
                     "rageUses": 3, "battleRageDamage": 2,
                     "featureGrants": ["class-feature.barbarian.extra-attack-level-5"]},
                    {"level": 11, "baseAttackBonus": 11, "rageUses": 4,
                     "battleRageDamage": 3,
                     "iterativeBaseAttackBonuses": [11, 6, 1], "featureGrants": []},
                    {"level": 17, "baseAttackBonus": 17, "rageUses": 6,
                     "battleRageDamage": 4,
                     "iterativeBaseAttackBonuses": [17, 12, 7, 2], "featureGrants": []},
                ]}})
        write("classes/base/wizard/wizard.json", {
            "id": "class.wizard", "kind": "class", "definition": {
                "iterativeAttackPolicy": "shared-native-attack-procedures",
                "progression": [
                    {"level": 1, "baseAttackBonus": 0, "iterativeBaseAttackBonuses": [],
                     "featureGrants": ["class-feature.wizard.spellcasting-level-1"],
                     "spellSlotsBySpellLevel": [2, 0, 0]},
                    {"level": 5, "baseAttackBonus": 2, "iterativeBaseAttackBonuses": [2],
                     "featureGrants": [], "spellSlotsBySpellLevel": [4, 3, 2]},
                    {"level": 11, "baseAttackBonus": 5, "iterativeBaseAttackBonuses": [5],
                     "featureGrants": [], "spellSlotsBySpellLevel": [4, 3, 3]},
                ]}})
        write("classes/base/rogue/rogue.json", {
            "id": "class.rogue", "kind": "class", "definition": {
                "iterativeAttackPolicy": "shared-native-alternative-attack-procedures",
                "progression": [
                    {"level": 1, "baseAttackBonus": 0, "iterativeBaseAttackBonuses": [],
                     "sneakAttackDice": "1d6",
                     "featureGrants": ["class-feature.rogue.sneak-attack-level-1"]},
                    {"level": 2, "baseAttackBonus": 1, "iterativeBaseAttackBonuses": [1],
                     "sneakAttackDice": "1d6",
                     "featureGrants": ["class-feature.rogue.cunning-action-level-2"]},
                    {"level": 3, "baseAttackBonus": 2, "iterativeBaseAttackBonuses": [2],
                     "sneakAttackDice": "2d6",
                     "featureGrants": ["class-feature.rogue.steady-aim-level-3"]},
                    {"level": 4, "baseAttackBonus": 3, "iterativeBaseAttackBonuses": [3],
                     "sneakAttackDice": "2d6", "featureGrants": []},
                    {"level": 5, "baseAttackBonus": 3, "iterativeBaseAttackBonuses": [3],
                     "sneakAttackDice": "3d6",
                     "featureGrants": ["class-feature.rogue.uncanny-dodge-level-5"]},
                    {"level": 11, "baseAttackBonus": 8,
                     "iterativeBaseAttackBonuses": [8, 3], "sneakAttackDice": "6d6",
                     "featureGrants": []},
                ]}})
        write("features/classes/barbarian/extra-attack-level-5.json", {
            "id": "class-feature.barbarian.extra-attack-level-5",
            "kind": "classFeature", "parentClass": "class.barbarian", "classLevel": 5,
            "definition": {"mechanics": {"kind": "attack-action",
                                        "standardAttackCount": 2,
                                        "babInteraction": "alternative-procedure-not-additive"}}})
        write("features/classes/rogue/uncanny-dodge-level-5.json", {
            "id": "class-feature.rogue.uncanny-dodge-level-5",
            "kind": "classFeature", "parentClass": "class.rogue", "classLevel": 5,
            "definition": {"mechanics": {"kind": "reaction-damage-halving",
                                        "trigger": "visible-attacker-hits-you",
                                        "cost": "reaction", "divideDamageBy": 2,
                                        "roundDown": True}}})
        write("features/classes/barbarian/rage-level-1.json", {
            "id": "class-feature.barbarian.rage-level-1", "kind": "classFeature",
            "parentClass": "class.barbarian", "classLevel": 1,
            "definition": {"mechanics": {"kind": "rage-resource",
                "activation": "bonus-action", "usesByClassLevel": [
                    [1, 2], [3, 3], [6, 4], [12, 5], [17, 6], [20, "unlimited"]],
                "shared": {"noHeavyArmor": True, "forbidSpellsAndConcentration": True},
                "battle": {"strengthAttackDamageBonus": [[1, 2], [9, 3], [16, 4]],
                           "durationUntil": "end-of-next-turn",
                           "extendBy": ["attack-enemy", "force-enemy-saving-throw", "bonus-action"]}}}})
        write("features/classes/rogue/sneak-attack-level-1.json", {
            "id": "class-feature.rogue.sneak-attack-level-1", "kind": "classFeature",
            "parentClass": "class.rogue", "classLevel": 1,
            "definition": {"mechanics": {"kind": "sneak-attack",
                "dice": "1d6 plus 1d6 each odd Rogue level, through 10d6",
                "frequency": "each-independently-qualifying-successful-hit",
                "eligibility": "undetected-or-unanticipated-attack-or-explicit-precision-vulnerability",
                "advantageAloneInsufficient": True, "requiresWeaponProficiency": True,
                "ordinaryRangeOnly": True, "creatureTypeNotBlanketImmunity": True,
                "respectsActualImmunities": True, "noExtraAttacksGranted": True}}})
        write("features/classes/rogue/cunning-action-level-2.json", {
            "id": "class-feature.rogue.cunning-action-level-2", "kind": "classFeature",
            "parentClass": "class.rogue", "classLevel": 2,
            "definition": {"mechanics": {"kind": "bonus-action-choices",
                                          "options": ["dash", "disengage", "hide"],
                                          "cost": "bonus-action"}}})
        write("features/classes/rogue/steady-aim-level-3.json", {
            "id": "class-feature.rogue.steady-aim-level-3", "kind": "classFeature",
            "parentClass": "class.rogue", "classLevel": 3,
            "definition": {"mechanics": {"kind": "aim-for-accuracy",
                "cost": "bonus-action", "advantageNextAttackCurrentTurn": True,
                "doesNotCreateSneakAttackEligibility": True,
                "requiresNotMovedThisTurn": True,
                "speedZeroUntilTurnEnd": True}}})
        self.native = root
        self.catalog = power.load_native(root)

    def profile(self, source):
        return power.reconstruct_preset(self.catalog, source)

    def test_terminal_sources_exclude_nonterminal_family(self):
        result = power.terminal_sources(self.catalog)
        self.assertEqual(5, len(result))
        self.assertNotIn("ancestry.dragonborn.chromatic", result)
        self.assertNotIn("species.elf", result)

    def test_parent_chain_replaces_intrinsic_within_direct_preset(self):
        profile = self.profile("ancestry.elf.wood")
        self.assertEqual(["species.elf", "ancestry.elf.wood"], profile["lineage"])
        self.assertEqual(35, profile["intrinsics"]["walkingSpeedFeet"])

    def test_breath_uses_actual_geometry_and_level(self):
        profile = self.profile("ancestry.dragonborn.red")
        l1 = power.evaluate(profile, self.catalog, 1)
        l5 = power.evaluate(profile, self.catalog, 5)
        self.assertAlmostEqual(7.5875, l1["metrics"]["breath_expected_net_damage_delta"])
        self.assertAlmostEqual(23.7, l5["metrics"]["breath_expected_net_damage_delta"])
        self.assertEqual(2, l5["metrics"]["breath_uses_selected"])

    def test_drow_penalty_is_not_mistaken_for_blindness(self):
        result = power.evaluate(self.profile("ancestry.elf.drow"), self.catalog, 5)
        self.assertAlmostEqual(-0.30, result["metrics"]["condition_expected_success_delta"])
        self.assertEqual(120, result["intrinsicContext"]["darkvisionFeet"])
        self.assertIn("trait.elf.sunlight", result["modeledTraits"])

    def test_stonebound_only_has_no_passive_earth_sense(self):
        dwarf = self.profile("species.dwarf")
        rock = self.profile("ancestry.gnome.rock")
        union = {**dwarf, "traits": {**dwarf["traits"], **rock["traits"]}}
        self.assertEqual(2, power.evaluate(dwarf, self.catalog, 1)["metrics"]["tremorsense_contacts_detected"])
        self.assertEqual(2, power.evaluate(rock, self.catalog, 1)["metrics"]["tremorsense_contacts_detected"])
        self.assertEqual(3, power.evaluate(union, self.catalog, 1)["metrics"]["tremorsense_contacts_detected"])
        # Owner-selected consolidation is the canonical Dwarf capability only.
        self.assertNotIn("trait.gnome.earthbound", dwarf["traits"])

    def test_unsupported_traits_are_not_assigned_zero_power(self):
        profile = self.profile("species.dwarf")
        profile["traits"]["trait.custom.unmodeled"] = {"spell": "unknown-power"}
        result = power.evaluate(profile, self.catalog, 1)
        self.assertIn({"traitId": "trait.custom.unmodeled", "reason": "not-modeled"},
                      result["unmodeled"])

    def test_shared_resource_frontier_uses_one_pool(self):
        base = {"id": "stones-endurance", "uses": {"value": "proficiency-bonus"},
                "damageReduction": {"dice": "1d12"}}
        strike = {"sharedUsesFromTrait": "stones-endurance",
                  "extraDamage": {"dice": "1d6"}, "onFailedSave": {"pushFeet": 10}}
        frontier = power.shared_pool_frontier(base, strike, 4)
        self.assertEqual(3, len(frontier))
        self.assertEqual([0, 1, 2], [p["defensiveUses"] for p in frontier])
        self.assertEqual([7, 3.5, 0], [p["expectedExtraDamageOnHits"] for p in frontier])
        self.assertEqual([0, 9.5, 19], [p["expectedDamagePrevented"] for p in frontier])

    def test_interactions_are_not_converted_into_one_score(self):
        vec = lambda a, b: {"metrics": {"damage": a, "hp": b}}
        result = power.interaction(vec(5, 6), vec(4, 2), vec(2, 2), vec(1, 0))
        self.assertEqual({"damage": 0, "hp": 2}, result)
        self.assertEqual({"damage": 3, "hp": 4}, power.endpoint_difference(vec(5, 6), vec(2, 2)))

    def test_movement_gate_respects_direct_preset_speed(self):
        wood = power.evaluate(self.profile("ancestry.elf.wood"), self.catalog, 1)
        dwarf = power.evaluate(self.profile("species.dwarf"), self.catalog, 1)
        self.assertEqual("origin-mode-satisfies-gate",
                         wood["mobilityGateResults"][0]["status"])
        self.assertEqual("not-supplied-by-origin",
                         dwarf["mobilityGateResults"][0]["status"])
        self.assertEqual(1, wood["metrics"]["origin_mobility_gates_satisfied"])

    def test_flight_resource_replay_is_not_permanent_flight(self):
        gem = self.profile("ancestry.dragonborn.red")
        gem["traits"] = {"trait.test.gem-flight": {
            "id": "gem-flight", "requiredCharacterLevel": 5,
            "activation": {"action": "bonus-action"},
            "flyingSpeed": {"equalTo": "current-walking-speed"},
            "canHover": True, "duration": {"minutes": 1},
            "uses": {"value": 1, "recharge": "long-rest"}}}
        before = power.evaluate(gem, self.catalog, 4)
        after = power.evaluate(gem, self.catalog, 5)
        self.assertEqual(0, before["metrics"]["origin_mobility_gates_satisfied"])
        self.assertEqual(1, after["metrics"]["origin_mobility_gates_satisfied"])
        self.assertEqual(["origin-mode-satisfies-gate", "not-supplied-by-origin",
                          "not-supplied-by-origin"],
                         [r["status"] for r in after["mobilityGateResults"][3:6]])
        permanent = self.profile("ancestry.dragonborn.red")
        permanent["traits"] = {"trait.test.permanent-flight": {
            "id": "permanent-flight", "flyingSpeed": {
                "equalTo": "current-walking-speed"}, "canHover": True,
            "atWill": True}}
        unconditional = power.evaluate(permanent, self.catalog, 1)
        self.assertEqual(3, unconditional["metrics"]["origin_mobility_gates_satisfied"])

    def test_flight_can_reuse_window_but_not_extend_its_duration(self):
        scene = {"mobilityChallenges": [
            {"id": "a", "mode": "fly", "distanceFeet": 20,
             "startMinute": 0, "durationMinutes": 0.1},
            {"id": "b", "mode": "fly", "distanceFeet": 20,
             "startMinute": 0.5, "durationMinutes": 0.1},
            {"id": "c", "mode": "fly", "distanceFeet": 20,
             "startMinute": 2, "durationMinutes": 0.1},
            {"id": "d", "mode": "fly", "distanceFeet": 20,
             "startMinute": 3, "durationMinutes": 10},
        ]}
        limited = self.profile("species.dwarf")
        limited["intrinsics"]["walkingSpeedFeet"] = 30
        limited["traits"] = {"limited-flight": {
            "id": "limited-flight", "flyingSpeed": {
                "equalTo": "current-walking-speed"}, "canHover": True,
            "duration": {"minutes": 1}, "uses": {
                "value": 1, "recharge": "long-rest"}}}
        result = power.evaluate(limited, self.catalog, 5, scene)
        self.assertEqual(2, result["metrics"]["origin_mobility_gates_satisfied"])
        self.assertEqual([True, True, False, False],
                         [r["status"] == "origin-mode-satisfies-gate"
                          for r in result["mobilityGateResults"]])

    def test_material_and_group_restrictions_are_not_inferred(self):
        profile = self.profile("species.dwarf")
        profile["traits"] = {"burrow": {"id": "sand-burrower", "movementMode": "burrow",
                                     "speedFeet": 15,
                                     "allowedMaterials": ["loose-natural-sand"]}}
        challenges = {"mobilityChallenges": [
            {"id": "sand", "mode": "burrow", "distanceFeet": 15,
             "material": "loose-natural-sand"},
            {"id": "stone", "mode": "burrow", "distanceFeet": 15,
             "material": "stone"},
            {"id": "party", "mode": "walk", "distanceFeet": 5,
             "requiresWholeParty": True},
        ]}
        result = power.evaluate(profile, self.catalog, 1, challenges)
        self.assertEqual(["origin-mode-satisfies-gate", "not-supplied-by-origin",
                          "unresolved-party-access"],
                         [r["status"] for r in result["mobilityGateResults"]])
        self.assertEqual(1, result["metrics"]["origin_mobility_gates_satisfied"])

    def test_scenario_validation_rejects_silent_numerical_errors(self):
        invalid_scenarios = (
            {"saveFailure": 1.1},
            {"baseCheckSuccess": -0.01},
            {"saveFailure": float("nan")},
            {"rolls": {"attack-roll": 1.5}},
            {"incomingDamage": [{"type": "fire", "amount": -20}]},
            {"breathOpportunities": [{"areaTargets": {"line": -1}}]},
            {"mobilityChallenges": [{"id": "a", "mode": "fly",
                                    "distanceFeet": 20},
                                   {"id": "a", "mode": "fly",
                                    "distanceFeet": 20}]},
            {"mobilityChallenges": [{"id": "a", "mode": "burrow",
                                    "distanceFeet": 20}]},
            {"mobilityChallenges": [{"id": "a", "mode": "teleport",
                                    "distanceFeet": 20}]},
            {"breathOppurtunities": []},
        )
        for scene in invalid_scenarios:
            with self.subTest(scene=scene), self.assertRaises(ValueError):
                power.validate_scenario(scene)

    def test_breath_trace_records_selected_and_unselected_opportunities(self):
        result = power.evaluate(self.profile("ancestry.dragonborn.red"), self.catalog, 1)
        events = result["breathEventTrace"][0]["events"]
        self.assertEqual(2, len(events))
        self.assertEqual([True, False], [e["selected"] for e in events])
        self.assertAlmostEqual(7.5875, sum(e["netExpectedDamage"] for e in events if e["selected"]))
        self.assertLess(events[1]["netExpectedDamage"], 0)
        modified = {"breathOpportunities": [
            {"areaTargets": {"line": 1}}, {"areaTargets": {"line": 2}}]}
        alternative = power.evaluate(self.profile("ancestry.dragonborn.red"),
                                     self.catalog, 1, modified)
        self.assertEqual([False, True],
                         [e["selected"] for e in alternative["breathEventTrace"][0]["events"]])

    def test_actual_native_conditions_shape_and_missing_catalog_fail_closed(self):
        path = self.native / "conditions.json"
        content = json.loads(path.read_text(encoding="utf-8"))
        path.write_text(json.dumps({"_meta": {"version": "test"},
                                    "conditions": content["entries"]}), encoding="utf-8")
        actual_shape = power.load_native(self.native)
        self.assertIn("condition.dazzled", actual_shape["conditions"])
        result = power.evaluate(power.reconstruct_preset(actual_shape, "ancestry.elf.drow"),
                                actual_shape, 5)
        self.assertAlmostEqual(-0.30, result["metrics"]["condition_expected_success_delta"])
        path.write_text(json.dumps({"_meta": {}, "conditions": []}), encoding="utf-8")
        with self.assertRaises(ValueError):
            power.load_native(self.native)

    def test_unknown_burrowing_substrate_does_not_become_false_no_access(self):
        profile = self.profile("species.dwarf")
        profile["intrinsics"]["burrowingSpeedFeet"] = 15
        event = {"mobilityChallenges": [{"id": "sand", "mode": "burrow",
                                         "distanceFeet": 15, "material": "loose-natural-sand"}]}
        result = power.evaluate(profile, self.catalog, 1, event)
        self.assertEqual("unresolved-substrate", result["mobilityGateResults"][0]["status"])

    def test_unsupported_movement_definition_is_flagged(self):
        profile = self.profile("species.dwarf")
        profile["traits"]["trait.test.unknown-flight"] = {
            "id": "unknown-flight", "flyingSpeed": {"equalTo": "unknown-modifier"}}
        outcome = power.evaluate(profile, self.catalog, 5)
        self.assertIn({"traitId": "trait.test.unknown-flight",
                       "reason": "unresolved-movement-definition"}, outcome["unmodeled"])
        self.assertEqual(0, outcome["metrics"]["origin_mobility_gates_satisfied"])

    def test_partially_supported_mechanics_expose_field_level_unknowns(self):
        profile = self.profile("species.dwarf")
        profile["traits"]["trait.test.partial"] = {
            "hitPointsPerCharacterLevel": 1,
            "savingThrows": {"advantageAgainst": ["poison-effects"]},
            "spell": "mystery-spell",
        }
        result = power.evaluate(profile, self.catalog, 1)
        self.assertIn({"traitId": "trait.test.partial",
                       "reason": "unmodeled-effect-fields",
                       "fields": ["spell"]}, result["unmodeled"])

    def test_all_fixture_presets_run_all_six_breakpoints(self):
        sources = power.terminal_sources(self.catalog)
        for source in sources:
            for level in power.LEVELS:
                result = power.evaluate(self.profile(source), self.catalog, level)
                self.assertEqual(level, result["level"])
                self.assertIn("unmodeled", result)


    def scenario_suite(self):
        path = Path(__file__).parent / "fixtures/pillar2-power-scenarios.json"
        return json.loads(path.read_text(encoding="utf-8"))

    def test_scenario_corpus_has_named_diagnostic_contexts(self):
        suite = self.scenario_suite()
        power.validate_scenario_suite(suite)
        self.assertEqual(40, len(suite["contexts"]))
        self.assertEqual(len(suite["contexts"]), len({c["id"] for c in suite["contexts"]}))

    def test_suite_reverses_breath_geometry_without_exchanging_units(self):
        result = power.evaluate_scenario_suite(
            self.catalog, self.scenario_suite(), ["ancestry.dragonborn.red"])
        contexts = {c["id"]: c for c in result["contexts"]}
        corridor = contexts["corridor-area-opportunities"]["profiles"][0]["levels"][0]
        cluster = contexts["cluster-area-opportunities"]["profiles"][0]["levels"][0]
        self.assertAlmostEqual(15.175, corridor["metrics"]["breath_expected_net_damage_delta"])
        self.assertEqual(0, cluster["metrics"]["breath_expected_net_damage_delta"])
        self.assertNotIn("overallScore", result)
        self.assertNotIn("rankings", result)

    def test_suite_preserves_sunlight_tradeoff_and_stone_contact(self):
        suite = self.scenario_suite()
        rows = power.evaluate_scenario_suite(
            self.catalog, suite, ["ancestry.elf.drow", "species.dwarf", "ancestry.gnome.rock"])
        contexts = {c["id"]: {r["id"]: r["levels"][0] for r in c["profiles"]}
                    for c in rows["contexts"]}
        exposed = contexts["sunlight-unprotected"]["ancestry.elf.drow"]
        protected = contexts["sunlight-protected"]["ancestry.elf.drow"]
        self.assertAlmostEqual(-0.6, exposed["metrics"]["condition_expected_success_delta"])
        self.assertEqual(0, protected["metrics"].get("condition_expected_success_delta", 0))
        stone = contexts["stone-earth-contacts"]
        self.assertEqual(2, stone["species.dwarf"]["metrics"]["tremorsense_contacts_detected"])
        self.assertEqual(2, stone["ancestry.gnome.rock"]["metrics"]["tremorsense_contacts_detected"])
        # Selected consolidation is one Stonebound ability, not both modes.
        self.assertNotIn("trait.gnome.earthbound", self.profile("species.dwarf")["traits"])

    def test_suite_reports_uncertainty_and_context_extremes(self):
        result = power.evaluate_scenario_suite(
            self.catalog, self.scenario_suite(), ["ancestry.elf.drow", "ancestry.dragonborn.red"])
        drow = next(row for row in result["sourceSensitivity"]
                    if row["sourceId"] == "ancestry.elf.drow")
        values = drow["levels"][0]["rangesInNativeUnits"]["condition_expected_success_delta"]
        self.assertAlmostEqual(-0.6, values["minimum"])
        self.assertIn("sunlight-unprotected", values["minimumContexts"])
        self.assertEqual(0.0, values["maximum"])
        self.assertIn("sunlight-protected", values["maximumContexts"])
        self.assertFalse(drow["levels"][0]["coverage"]["isCompleteForCurrentLevel"])
        self.assertIsInstance(drow["levels"][0]["coverage"]["incompleteOrUnsupportedMechanics"], list)
        self.assertEqual(40, result["scenarioCount"])

    def test_sensitivity_does_not_convert_unmeasured_endpoints_to_zero(self):
        profile = self.profile("ancestry.dragonborn.red")
        self.catalog["conditions"]["condition.incapacitated"] = {
            "id":"condition.incapacitated", "effects":[
                {"type":"action-restriction", "cannotTake":["action"]}]}
        profile["traits"]["metallic"] = {
            "requiredCharacterLevel":5,
            "activation":{"action":"attack", "replaces":"one-attack"},
            "uses":{"value":1,"recharge":"long-rest"},
            "area":{"shape":"cone", "affectsAllies":True},
            "options":[{"id":"enervating-breath",
                       "savingThrow":{"ability":"constitution", "dc":"test", "onSuccess":"no-effect"},
                       "onFailure":{"applyCondition":"condition.incapacitated",
                                    "endsAt":"start-of-your-next-turn"}}]}
        self.catalog["sources"]["ancestry.dragonborn.red"]["traits"].append(
            {"traitId":"trait.fixture.metallic"})
        self.catalog["traits"]["trait.fixture.metallic"] = profile["traits"]["metallic"]
        fixture = {"id":"measured-zero-sensitivity", "version":4,"contexts":[
            {"id":"no-exposure", "purpose":"No action schedule",
             "scenario":{"id":"no-exposure"}},
            {"id":"zero-actions", "purpose":"All targets acted already",
             "scenario":{"id":"zero-actions", "controlOpportunities":[{
                 "id":"zero", "option":"enervating-breath", "areaShape":"cone",
                 "enemyTargets":1, "allyTargets":0, "failureProbability":0.5,
                 "enemyActionSlots":0}]}},
            {"id":"one-action", "purpose":"One future action",
             "scenario":{"id":"one-action", "controlOpportunities":[{
                 "id":"one", "option":"enervating-breath", "areaShape":"cone",
                 "enemyTargets":1, "allyTargets":0, "failureProbability":0.5,
                 "enemyActionSlots":1}]}}
        ]}
        result = power.evaluate_scenario_suite(self.catalog, fixture,
                                               ["ancestry.dragonborn.red"])
        summary = result["sourceSensitivity"][0]["levels"][3]["rangesInNativeUnits"]
        action = summary["control_expected_enemy_action_slots_denied"]
        self.assertEqual(0, action["minimum"])
        self.assertEqual(.5, action["maximum"])
        self.assertEqual(2, action["evaluatedContextCount"])
        self.assertEqual(["no-exposure"], action["notEvaluatedContexts"])

    def test_suite_paired_comparison_is_per_context_and_level(self):
        result = power.evaluate_scenario_suite(
            self.catalog, self.scenario_suite(), ["ancestry.elf.wood"], "species.dwarf")
        context = next(row for row in result["contexts"]
                       if row["id"] == "travel-separated-gates")
        comparison = context["comparisons"][0]
        self.assertEqual("species.dwarf", comparison["counterfactual"])
        self.assertEqual(6, len(comparison["levels"]))
        self.assertGreater(comparison["levels"][0]["pairedEndpointDifferences"]
                           ["origin_mobility_gates_satisfied"], 0)
        self.assertIn("counterfactualUnmodeled", comparison["levels"][0])

    def test_suite_rejects_inconsistent_and_duplicate_contexts(self):
        base = self.scenario_suite()
        invalid = []
        bad = copy.deepcopy(base)
        bad["contexts"].append(copy.deepcopy(bad["contexts"][0]))
        invalid.append(bad)
        bad = copy.deepcopy(base)
        bad["contexts"][0]["scenario"]["id"] = "unmatched"
        invalid.append(bad)
        bad = copy.deepcopy(base)
        bad["contexts"][0]["scenario"]["damageTypeMultiplier"] = {"fire": -1}
        invalid.append(bad)
        bad = copy.deepcopy(base)
        bad["contexts"][0]["scenario"]["breathOppurtunities"] = []
        invalid.append(bad)
        bad = copy.deepcopy(base)
        bad["version"] = 10
        invalid.append(bad)
        for item in invalid:
            with self.subTest(error=item), self.assertRaises(ValueError):
                power.validate_scenario_suite(item)
        power.validate_scenario({"damageTypeMultiplier": {"fire": 0.5}})

    def test_suite_cli_output_is_machine_readable(self):
        with tempfile.TemporaryDirectory() as directory:
            destination = Path(directory) / "suite.json"
            fixture = Path(__file__).parent / "fixtures/pillar2-power-scenarios.json"
            self.assertEqual(0, power.main([
                "--native", str(self.native), "--scenario-suite", str(fixture),
                "--source", "ancestry.dragonborn.red", "--output", str(destination)]))
            result = json.loads(destination.read_text(encoding="utf-8"))
        self.assertEqual(1, result["terminalProfileCount"])
        self.assertEqual(40, result["scenarioCount"])
        self.assertEqual(6, len(result["contexts"][0]["profiles"][0]["levels"]))


    def test_poison_save_advantage_and_resistance_apply_to_one_packet(self):
        traits = {"dwarf-resilience": {"savingThrows": {"advantageAgainst": ["poison-effects"]},
                                      "damageResistance": {"poison": 50}}}
        profile = self.profile("species.dwarf")
        profile["traits"].update(traits)
        event = {"saveEvents": [{"id": "poison", "ability": "constitution",
                                 "tags": ["poison-effects"], "magical": True,
                                 "baseSuccess": 0.5, "damageType": "poison",
                                 "damageOnFailure": 20, "damageOnSuccess": 10}]}
        result = power.evaluate(profile, self.catalog, 1, event)
        self.assertAlmostEqual(0.25, result["metrics"]["saving_throw_success_delta"])
        self.assertAlmostEqual(8.75, result["metrics"]["saving_throw_damage_prevented"])
        self.assertAlmostEqual(6.25, result["savingThrowEventTrace"][0]["expectedDamageAfterOrigin"])
        self.assertIn("dwarf-resilience", result["modeledTraits"])

    def test_magic_only_mental_advantage_and_fey_charm_are_separate(self):
        profile = self.profile("species.elf")
        profile["traits"] = {
            "cunning": {"savingThrows": {"advantageAbilities": ["intelligence", "wisdom", "charisma"],
                                          "scope": "magic-only"}},
            "fey": {"savingThrows": {"advantageAgainst": ["condition.charmed"]}},
        }
        event = {"saveEvents": [
            {"id": "magic", "ability": "wisdom", "magical": True,
             "tags": ["condition.charmed"], "baseSuccess": 0.5,
             "conditionOnFailure": "condition.charmed"},
            {"id": "mundane", "ability": "wisdom", "magical": False,
             "tags": [], "baseSuccess": 0.5, "conditionOnFailure": "condition.frightened"},
        ]}
        result = power.evaluate(profile, self.catalog, 1, event)
        self.assertAlmostEqual(0.25, result["metrics"]["saving_throw_conditions_avoided"])
        self.assertEqual([0.75, 0.5], [x["effectiveSuccessChance"]
                                         for x in result["savingThrowEventTrace"]])
        self.assertEqual(2, len(result["savingThrowEventTrace"][0]["advantageSources"]))

    def test_capped_reductions_and_one_reaction_per_round(self):
        profile = self.profile("species.dwarf")
        profile["traits"] = {"stone": {"id": "stones-endurance",
                                       "damageReduction": {"dice": "1d12",
                                                           "addAbilityModifier": "constitution",
                                                           "appliesTo": "triggering-damage"},
                                       "activation": {"action": "reaction",
                                                      "trigger": "when-you-take-damage"},
                                       "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}}}
        event = {"abilityModifiers": {"constitution": 3}, "reactionDamageEvents": [
            {"id": "small", "round": 1, "amount": 4, "damageType": "slashing",
             "isAttackHit": True, "attackerVisible": True, "reactionAvailable": True},
            {"id": "large", "round": 1, "amount": 20, "damageType": "slashing",
             "isAttackHit": True, "attackerVisible": True, "reactionAvailable": True},
            {"id": "next", "round": 2, "amount": 20, "damageType": "fire",
             "isAttackHit": False, "attackerVisible": False, "reactionAvailable": True},
        ]}
        result = power.evaluate(profile, self.catalog, 1, event)
        self.assertAlmostEqual(19, result["metrics"]["reaction_damage_prevented"])
        self.assertEqual(2, result["metrics"]["reaction_uses_selected"])
        self.assertEqual([None, "stone", "stone"],
                         [x["selectedDefense"] for x in result["reactionEventTrace"]])
        self.assertEqual(4, power._damage_reduction_expectation("1d12", 3, 4))

    def test_cloudy_escape_responds_to_visible_attack_not_nonattack_damage(self):
        profile = self.profile("species.dwarf")
        profile["traits"] = {"cloud": {
            "id": "cloudy-escape", "activation": {"action": "reaction",
            "trigger": "visible-creature-hits-you-with-an-attack-roll"},
            "damageResistance": {"percent": 50, "scope": "triggering-attack",
                                 "duration": "triggering-attack-only",
                                 "stacking": "existing-resistance-rules"},
            "teleport": {"maximumDistanceFeet": 30},
            "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}}}
        event = {"reactionDamageEvents": [
            {"id": "attack", "round": 1, "amount": 20, "damageType": "slashing",
             "isAttackHit": True, "attackerVisible": True, "reactionAvailable": True},
            {"id": "invisible", "round": 2, "amount": 20, "damageType": "slashing",
             "isAttackHit": True, "attackerVisible": False, "reactionAvailable": True},
            {"id": "spell", "round": 3, "amount": 20, "damageType": "fire",
             "isAttackHit": False, "attackerVisible": True, "reactionAvailable": True}]}
        result = power.evaluate(profile, self.catalog, 1, event)
        self.assertEqual(10, result["metrics"]["reaction_damage_prevented"])
        self.assertEqual("cloud", result["reactionEventTrace"][0]["selectedDefense"])
        self.assertTrue(any(x.get("traitId") == "cloud" and "teleport" in x.get("fields", [])
                            for x in result["unmodeled"]))

    def test_native_shared_pool_frontier_does_not_duplicate_stone_charges(self):
        profile = self.profile("species.dwarf")
        profile["traits"] = {
            "stone": {"id": "stones-endurance", "uses": {"value": "proficiency-bonus"},
                      "damageReduction": {"dice": "1d12"}},
            "strike": {"id": "stone-strike", "sharedUsesFromTrait": "stones-endurance",
                       "extraDamage": {"dice": "1d6"}, "onFailedSave": {"pushFeet": 10}}}
        result = power.evaluate(profile, self.catalog, 1, {})
        self.assertEqual(1, len(result["sharedResourceFrontiers"]))
        self.assertEqual([0, 1, 2], [x["defensiveUses"]
                                         for x in result["sharedResourceFrontiers"][0]["frontier"]])
        self.assertEqual("stone", result["sharedResourceFrontiers"][0]["baseTraitId"])

    def test_invalid_save_and_reaction_event_contracts_are_rejected(self):
        bad = [
            {"saveEvents": [{"id": "s", "ability": "wisdom", "baseSuccess": 1.1,
                             "conditionOnFailure": "condition.charmed"}]},
            {"saveEvents": [{"id": "s", "ability": "wisdom", "baseSuccess": 0.5,
                             "damageOnFailure": 20, "damageType": "fire"}]},
            {"saveEvents": [{"id": "x", "ability": "wisdom", "baseSuccess": 0.5,
                             "conditionOnFailure": "condition.charmed",
                             "baseSucces": 0.9}]},
            {"reactionDamageEvents": [{"id": "x", "round": 0, "amount": 20,
                                       "damageType": "fire", "isAttackHit": True,
                                       "attackerVisible": True, "reactionAvailable": True}]},
            {"reactionDamageEvents": [{"id": "x", "round": 1, "amount": 20,
                                       "damageType": "fire", "isAttackHit": True,
                                       "attackerVisible": True}]},
            {"reactionDamageEvents": [{"id": "x", "round": 1, "amount": 20,
                                       "damageType": "fire", "isAttackHit": True,
                                       "attackerVisible": True, "reactionAvailable": True},
                                      {"id": "x", "round": 2, "amount": 20,
                                       "damageType": "fire", "isAttackHit": True,
                                       "attackerVisible": True, "reactionAvailable": True}]},
        ]
        for event in bad:
            with self.subTest(event=event), self.assertRaises(ValueError):
                power.validate_scenario(event)

    def test_existing_passive_resistance_with_reduction_remains_unresolved(self):
        profile = self.profile("species.dwarf")
        profile["traits"] = {
            "resistance": {"damageResistance": {"fire": 50}},
            "stone": {"id": "stones-endurance",
                      "damageReduction": {"dice": "1d12",
                                          "addAbilityModifier": "constitution",
                                          "appliesTo": "triggering-damage"},
                      "activation": {"action": "reaction", "trigger": "when-you-take-damage"},
                      "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}},
        }
        scene = {"reactionDamageEvents": [{"id": "fire", "round": 1, "amount": 20,
                                            "damageType": "fire", "isAttackHit": True,
                                            "attackerVisible": True, "reactionAvailable": True}]}
        result = power.evaluate(profile, self.catalog, 1, scene)
        self.assertEqual(0, result["metrics"]["reaction_damage_prevented"])
        self.assertIn({"traitId": "stone", "reason": "resistance-and-reduction-order-unresolved"},
                      result["unmodeled"])

    def test_new_suite_exercises_saves_and_reaction_diagnostics(self):
        suite = self.scenario_suite()
        self.assertEqual(9, suite["version"])
        self.assertEqual(40, len(suite["contexts"]))
        self.assertIn("poison-saves-and-resistance", [x["id"] for x in suite["contexts"]])
        result = power.evaluate_scenario_suite(self.catalog, suite, ["species.dwarf"])
        self.assertEqual(40, result["scenarioCount"])
        names = {x["id"]: x["profiles"][0]["levels"][0]
                 for x in result["contexts"]}
        self.assertIn("savingThrowEventTrace", names["poison-saves-and-resistance"])
        self.assertIn("reactionEventTrace", names["heavy-hit-reaction-windows"])


    def test_fixed_innate_spell_progression_and_explicit_demands(self):
        profile = self.profile("species.elf")
        profile["traits"].update({
            "high-cantrip": {"id": "arcane-cantrip", "spellList": "wizard",
                             "level": 1, "replacement": "after-long-rest"},
            "detect": {"spell": "detect-magic", "level": 3, "freeUses": 1,
                       "recharge": "long-rest", "slotCasting": True},
            "misty": {"spell": "misty-step", "level": 5, "freeUses": 1,
                      "recharge": "long-rest", "slotCasting": True},
        })
        events = {"spellDemands": [
            {"id": "detect", "spell": "detect-magic"},
            {"id": "escape", "spell": "misty-step"},
            {"id": "second-escape", "spell": "misty-step"},
            {"id": "chosen-cantrip", "spell": "fire-bolt"}]}
        at_one = power.evaluate(profile, self.catalog, 1, events)
        at_three = power.evaluate(profile, self.catalog, 3, events)
        at_five = power.evaluate(profile, self.catalog, 5, events)
        self.assertEqual(0, at_one["metrics"]["innate_fixed_free_cast_capacity"])
        self.assertEqual(1, at_three["metrics"]["spell_free_cast_demands_matched"])
        self.assertEqual(2, at_five["metrics"]["spell_free_cast_demands_matched"])
        self.assertEqual(2, len(at_five["innateFixedSpellEntitlements"]))
        self.assertEqual(["free-cast-access", "free-cast-access", "free-use-exhausted",
                          "unresolved-spell-list-choice"],
                         [v["status"] for v in at_five["innateSpellDemandTrace"]])
        self.assertTrue(any(v.get("reason") == "unresolved-spell-list-selection"
                            for v in at_five["unmodeled"]))

    def test_at_will_spells_and_duplicate_free_pools_do_not_stack_silently(self):
        profile = self.profile("species.elf")
        profile["traits"] = {
            "at-will": {"spell": "fire-bolt", "level": 1, "atWill": True},
            "drow": {"spell": "darkness", "level": 5, "freeUses": 1,
                     "recharge": "long-rest"},
            "infernal": {"spell": "darkness", "level": 5, "freeUses": 1,
                        "recharge": "long-rest"},
        }
        scenario = {"spellDemands": [
            {"id": "bolt-1", "spell": "fire-bolt"},
            {"id": "bolt-2", "spell": "fire-bolt"},
            {"id": "darkness", "spell": "darkness"},
            {"id": "ineligible", "spell": "fire-bolt", "eligible": False}]}
        r = power.evaluate(profile, self.catalog, 5, scenario)
        self.assertEqual(2, r["metrics"]["spell_at_will_demands_matched"])
        self.assertEqual(2, r["metrics"]["innate_fixed_free_cast_capacity"])
        self.assertEqual(0, r["metrics"]["spell_free_cast_demands_matched"])
        self.assertEqual("unresolved-duplicate-free-use-pools",
                         r["innateSpellDemandTrace"][2]["status"])
        self.assertTrue(any(x.get("reason") == "duplicate-fixed-spell-free-pools-unresolved"
                            for x in r["unmodeled"]))

    def test_human_origin_feat_and_skill_choice_remain_entitlements(self):
        profile = self.profile("species.elf")
        profile["traits"] = {
            "versatile": {"grantType": "feat-choice", "featCategory": "origin",
                          "choiceCount": 1, "when": "character-creation"},
            "skillful": {"grantType": "skill-proficiency", "chooseFrom": "any-native-skill",
                         "choiceCount": 1, "when": "character-creation"}}
        r = power.evaluate(profile, self.catalog, 5, {})
        self.assertEqual(1, r["metrics"]["origin_feat_choice_entitlements"])
        self.assertEqual(1, r["metrics"]["skill_proficiency_choice_entitlements"])
        self.assertIn("unresolved-origin-feat-effects", [x.get("reason") for x in r["unmodeled"]])
        self.assertIn("unresolved-selected-skill", [x.get("reason") for x in r["unmodeled"]])

    def test_metallic_breath_conditions_ally_exposure_and_one_pool(self):
        profile = self.profile("ancestry.dragonborn.red")
        self.catalog["conditions"].update({"condition.incapacitated": {"id": "condition.incapacitated"},
                                            "condition.prone": {"id": "condition.prone"}})
        profile["traits"]["metallic"] = {
            "requiredCharacterLevel": 5,
            "activation": {"action": "attack", "replaces": "one-attack"},
            "uses": {"value": 1, "recharge": "long-rest", "resource": "metallic-breath-weapon"},
            "area": {"shape": "cone", "lengthFeet": 15, "affectsAllies": True},
            "choiceTiming": "on-use",
            "options": [
                {"id": "enervating-breath", "savingThrow": {"ability": "constitution",
                                                          "dc": "8 + proficiency-bonus + constitution-modifier",
                                                          "onSuccess": "no-effect"},
                 "onFailure": {"applyCondition": "condition.incapacitated"}},
                {"id": "repulsion-breath", "savingThrow": {"ability": "strength",
                                                         "dc": "8 + proficiency-bonus + constitution-modifier",
                                                         "onSuccess": "no-effect"},
                 "onFailure": {"pushFeet": 20, "applyCondition": "condition.prone"}},
            ]}
        scenario = {"controlOpportunities": [
            {"id": "first", "option": "enervating-breath", "areaShape": "cone",
             "enemyTargets": 2, "allyTargets": 1, "failureProbability": 0.55},
            {"id": "second", "option": "repulsion-breath", "areaShape": "cone",
             "enemyTargets": 3, "allyTargets": 0, "failureProbability": 0.55}]}
        before = power.evaluate(profile, self.catalog, 4, scenario)
        after = power.evaluate(profile, self.catalog, 5, scenario)
        self.assertEqual(0, before["metrics"]["control_attack_replacements"])
        self.assertEqual(1, after["metrics"]["control_attack_replacements"])
        self.assertAlmostEqual(1.10, after["metrics"]["control_expected_enemy_incapacitated_applications"])
        self.assertAlmostEqual(0.55, after["metrics"]["control_expected_ally_incapacitated_applications"])
        self.assertEqual("control-use-exhausted", after["controlEventTrace"][1]["status"])
        scenario["controlOpportunities"] = [scenario["controlOpportunities"][1]]
        repulsion = power.evaluate(profile, self.catalog, 5, scenario)
        self.assertAlmostEqual(1.65, repulsion["metrics"]["control_expected_enemy_prone_applications"])
        self.assertAlmostEqual(33.0, repulsion["metrics"]["control_expected_enemy_forced_movement_feet"])


    def test_enervating_turn_windows_require_actual_condition_and_timing(self):
        profile = self.profile("ancestry.dragonborn.red")
        profile["traits"]["metallic"] = {
            "requiredCharacterLevel": 5,
            "activation": {"action": "attack", "replaces": "one-attack"},
            "uses": {"value": 1, "recharge": "long-rest"},
            "area": {"shape": "cone", "affectsAllies": True},
            "options": [{"id": "enervating-breath",
                         "savingThrow": {"ability": "constitution", "dc": "test",
                                         "onSuccess": "no-effect"},
                         "onFailure": {"applyCondition": "condition.incapacitated",
                                       "endsAt": "start-of-your-next-turn"}}]}
        self.catalog["conditions"]["condition.incapacitated"] = {
            "id": "condition.incapacitated",
            "effects": [{"type": "action-restriction",
                         "cannotTake": ["action", "bonus-action", "reaction"]},
                        {"type": "concentration", "operation": "end"}]}
        event = {"id":"enemy-and-ally-before-next-turn", "option":"enervating-breath",
                 "areaShape":"cone", "enemyTargets":2, "allyTargets":1,
                 "failureProbability":0.55, "enemyActionSlots":2, "allyActionSlots":1,
                 "enemyBonusActionSlots":1, "enemyReactionSlots":2,
                 "enemyConcentratingTargets":1, "attackReplacementExpectedDamage":5.2}
        result = power.evaluate(profile, self.catalog, 5,
                                {"controlOpportunities":[event]})
        metrics = result["metrics"]
        self.assertAlmostEqual(1.10, metrics["control_expected_enemy_action_slots_denied"])
        self.assertAlmostEqual(.55, metrics["control_expected_ally_action_slots_denied"])
        self.assertAlmostEqual(.55, metrics["control_expected_enemy_bonus_action_slots_denied"])
        self.assertAlmostEqual(1.10, metrics["control_expected_enemy_reaction_slots_denied"])
        self.assertAlmostEqual(.55, metrics["control_expected_enemy_concentrations_ended"])
        self.assertAlmostEqual(5.2, metrics["control_replaced_attack_expected_damage_forgone"])
        self.assertTrue(result["controlEventTrace"][0]["conditionWindowKnown"])
        event["enemyActionSlots"] = 0
        later = power.evaluate(profile, self.catalog, 5, {"controlOpportunities":[event]})
        self.assertEqual(0, later["metrics"]["control_expected_enemy_action_slots_denied"])
        # The same condition application still occurs, but there is no action to deny.
        self.assertAlmostEqual(1.10, later["metrics"]["control_expected_enemy_incapacitated_applications"])

        self.catalog["conditions"]["condition.incapacitated"]["effects"] = []
        missing_effect = power.evaluate(profile, self.catalog, 5,
                                        {"controlOpportunities":[event]})
        self.assertNotIn("control_expected_enemy_action_slots_denied", missing_effect["metrics"])
        self.assertIn("enemyActionSlots", missing_effect["controlEventTrace"][0]["unresolvedWindowExposures"])

    def test_repulsion_prone_rolls_are_conditional_not_lost_turns(self):
        profile = self.profile("ancestry.dragonborn.red")
        self.catalog["conditions"]["condition.prone"] = {
            "id": "condition.prone", "effects": [
                {"type": "roll-mode", "appliesTo": "attack-roll",
                 "mode": "disadvantage", "subject": "attacks-by-affected-creature"}]}
        profile["traits"]["metallic"] = {
            "requiredCharacterLevel": 5,
            "activation": {"action": "attack", "replaces": "one-attack"},
            "uses": {"value": 1, "recharge": "long-rest"},
            "area": {"shape": "cone", "affectsAllies": True},
            "options": [{"id": "repulsion-breath",
                         "savingThrow": {"ability": "strength", "dc": "test",
                                         "onSuccess": "no-effect"},
                         "onFailure": {"applyCondition": "condition.prone",
                                       "pushFeet": 20}}]}
        event = {"id":"unrecovered-prone-attacks", "option":"repulsion-breath",
                 "areaShape":"cone", "enemyTargets":3,"allyTargets":0,
                 "failureProbability":0.55,
                 "enemyNormalAttackRollsWhileProne":3,"baseHitChance":0.65}
        result = power.evaluate(profile, self.catalog, 5,
                                {"controlOpportunities":[event]})
        self.assertAlmostEqual(3*.55*.65*.35,
             result["metrics"]["control_expected_enemy_normal_attack_hits_prevented_by_prone"])
        self.assertNotIn("control_expected_enemy_action_slots_denied", result["metrics"])
        event["enemyNormalAttackRollsWhileProne"] = 0
        removed = power.evaluate(profile, self.catalog, 5, {"controlOpportunities":[event]})
        self.assertEqual(0, removed["metrics"]["control_expected_enemy_normal_attack_hits_prevented_by_prone"])
        self.assertAlmostEqual(1.65, removed["metrics"]["control_expected_enemy_prone_applications"])

    def test_control_window_validation_rejects_untraceable_exposures(self):
        normal = {"id":"q", "option":"enervating-breath", "areaShape":"cone",
                  "enemyTargets":1,"allyTargets":0,"failureProbability":0.5}
        for extra in [{"enemyActionSlots":2}, {"allyReactionSlots":1},
                      {"enemyNormalAttackRollsWhileProne":1},
                      {"enemyNormalAttackRollsWhileProne":1, "baseHitChance":1.5},
                      {"enemyActionSlots":True}, {"enemyConcentratingTargets":1.2},
                      {"enemyActionSlotz":1},
                      {"attackReplacementExpectedDamage":-1}]:
            with self.subTest(extra=extra), self.assertRaises(ValueError):
                power.validate_scenario({"controlOpportunities":[{**normal, **extra}]})

    def test_control_scenario_corpus_has_timing_contrasts(self):
        fixture = Path(__file__).parent / "fixtures/pillar2-power-scenarios.json"
        suite = json.loads(fixture.read_text(encoding="utf-8"))
        self.assertEqual(9, suite["version"])
        self.assertEqual(40, len(suite["contexts"]))
        contexts = {item["id"]: item["scenario"] for item in suite["contexts"]}
        early = contexts["metallic-enervation-with-allies"]["controlOpportunities"][0]
        late = contexts["metallic-control-no-exposed-turns"]["controlOpportunities"][0]
        self.assertEqual(2, early["enemyActionSlots"])
        self.assertEqual(0, late["enemyActionSlots"])
        self.assertEqual(3, contexts["metallic-repulsion"]["controlOpportunities"][0]["enemyNormalAttackRollsWhileProne"])

    def test_spell_and_control_fixture_validation_rejects_ambiguous_fields(self):
        for bad in [
            {"spellDemands": [{"id": "x", "spell": "fire-bolt", "spells": 2}]},
            {"spellDemands": [{"id": "x", "spell": "fire-bolt"},
                               {"id": "x", "spell": "darkness"}]},
            {"spellDemands": [{"id": "x", "spell": "fire-bolt", "eligible": 1}]},
            {"controlOpportunities": [{"id": "b", "option": "repulsion-breath",
                                       "areaShape": "cone", "enemyTargets": 1,
                                       "allyTargets": -1, "failureProbability": 0.5}]},
            {"controlOpportunities": [{"id": "b", "option": "repulsion-breath",
                                       "areaShape": "cone", "enemyTargets": 1,
                                       "allyTargets": 0, "failureProbability": 1.2}]},
        ]:
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                power.validate_scenario(bad)



    def synthetic_goliath(self, feature):
        """Minimal faithful field shapes for a diagnostic, not authored data."""
        profile = self.profile("species.dwarf")
        profile["traits"] = {
            "trait.goliath.hill.hills-tumble": {"id": "hills-tumble", "trigger": "when-you-hit-with-an-attack-roll-and-deal-damage",
                     "targetMaximumSize": "large", "applyCondition": "condition.prone",
                     "additionalSavingThrow": False,
                     "uses": {"value": "proficiency-bonus", "recharge": "long-rest"},
                     "alternateStrikeMode": {"id": "hill-strike", "trigger": "melee-weapon-or-thrown-weapon-attack-hit-and-deal-damage",
                         "oncePerTurn": True, "targetMaximumSize": None,
                         "extraDamage": {"dice": "1d6", "damageTypeFrom": "triggering-weapon"},
                         "savingThrow": {"ability": "strength"},
                         "applyConditionOnFailedSave": "condition.prone",
                         "sharedUsesFromTrait": "hills-tumble"}},
            "trait.goliath.stone.stones-endurance": {"id": "stones-endurance", "uses": {"value": "proficiency-bonus", "recharge": "long-rest"},
                          "activation": {"action": "reaction", "trigger": "when-you-take-damage"},
                          "damageReduction": {"dice": "1d12", "addAbilityModifier": "constitution", "appliesTo": "triggering-damage"}},
            "trait.goliath.stone.stone-strike": {"id": "stone-strike", "trigger": "melee-weapon-or-thrown-weapon-attack-hit",
                      "oncePerTurn": True, "extraDamage": {"dice": "1d6", "damageType": "force"},
                      "onFailedSave": {"forcedMovementFeet": 10},
                      "sharedUsesFromTrait": "stones-endurance"},
            "trait.goliath.frost.frosts-chill": {"id": "frosts-chill", "trigger": "when-you-hit-with-an-attack-roll-and-deal-damage",
                      "extraDamage": {"dice": "1d6", "damageType": "cold"},
                      "speedReduction": {"feet": 10, "endsAt":"start-of-your-next-turn"},
                      "uses": {"value": "proficiency-bonus", "recharge": "long-rest"},
                      "alternateStrikeMode": {"id": "frost-strike", "trigger": "melee-weapon-or-thrown-weapon-attack-hit-and-deal-damage",
                          "oncePerTurn": True, "extraDamage": {"dice": "1d6", "damageType": "cold"},
                          "savingThrow": {"ability": "constitution"},
                          "onFailedSave": {"speedSetFeet": 0, "endsAt":"start-of-your-next-turn"},
                          "sharedUsesFromTrait": "frosts-chill"}},
            "trait.goliath.storm.storms-thunder": {"id": "storms-thunder", "uses": {"value": "proficiency-bonus", "recharge": "long-rest"},
                        "activation": {"action": "reaction", "trigger": "when-a-creature-within-range-deals-damage-to-you"}},
            "trait.goliath.storm.storm-strike": {"id": "storm-strike", "trigger": "melee-weapon-or-thrown-weapon-attack-hit",
                      "oncePerTurn": True, "extraDamage": {"dice": "1d6", "damageType": "lightning"},
                      "savingThrow": {"ability": "constitution"},
                      "onFailedSave": {"attackRollMode": "disadvantage", "endsAt":"start-of-your-next-turn"},
                      "sharedUsesFromTrait": "storms-thunder"},
        }
        self.catalog["conditions"]["condition.prone"] = {
            "id": "condition.prone", "effects": [
                {"type": "roll-mode", "appliesTo": "attack-roll", "mode": "disadvantage",
                 "subject": "attacks-by-affected-creature"}]}
        return profile

    def test_hit_control_differentiates_hill_prone_modes_and_size(self):
        profile = self.synthetic_goliath("hill")
        events = self.scenario_suite()["contexts"]
        by_id = {x["id"]: x["scenario"] for x in events}
        auto = power.evaluate(profile, self.catalog, 1, by_id["goliath-hill-automatic-prone"])
        self.assertEqual("activated", auto["strikeEventTrace"][0]["status"])
        self.assertEqual(1, auto["metrics"]["strike_expected_enemy_prone_applications"])
        self.assertAlmostEqual(2 * .65 * .35, auto["metrics"]["strike_expected_enemy_attack_hits_prevented_by_prone"])
        self.assertNotIn("strike_gross_extra_damage_on_confirmed_hits", auto["metrics"])
        alternate = power.evaluate(profile, self.catalog, 1,
                                   by_id["goliath-hill-alternative-prone"])
        self.assertAlmostEqual(.55, alternate["metrics"]["strike_expected_enemy_prone_applications"])
        self.assertAlmostEqual(3.5, alternate["metrics"]["strike_gross_extra_damage_on_confirmed_hits"])
        invalid = dict(by_id["goliath-hill-automatic-prone"])
        invalid["strikeOpportunities"] = [dict(invalid["strikeOpportunities"][0], targetSize="huge")]
        denied = power.evaluate(profile, self.catalog, 1, invalid)
        self.assertEqual("target-too-large", denied["strikeEventTrace"][0]["status"])

    def test_frost_speed_fails_no_movement_and_respects_shared_pool(self):
        profile = self.synthetic_goliath("frost")
        suite = {c["id"]: c["scenario"] for c in self.scenario_suite()["contexts"]}
        outcome = power.evaluate(profile, self.catalog, 1, suite["goliath-frost-speed-modes"])
        self.assertEqual(2, outcome["metrics"]["strike_uses_selected"])
        self.assertAlmostEqual(10 + 30*.55, outcome["metrics"]["strike_expected_enemy_nominal_speed_reduction_feet"])
        self.assertAlmostEqual(7, outcome["metrics"]["strike_gross_extra_damage_on_confirmed_hits"])
        self.assertEqual(["activated", "activated"], [e["status"] for e in outcome["strikeEventTrace"]])

    def test_stone_shared_uses_not_multiplied_and_once_per_turn(self):
        profile = self.synthetic_goliath("stone")
        suite = {c["id"]: c["scenario"] for c in self.scenario_suite()["contexts"]}
        outcome = power.evaluate(profile, self.catalog, 1, suite["goliath-stone-shared-strikes"])
        self.assertEqual(["activated", "activated", "shared-resource-exhausted"],
                         [e["status"] for e in outcome["strikeEventTrace"]])
        self.assertEqual(2, outcome["metrics"]["strike_uses_selected"])
        self.assertAlmostEqual(11, outcome["metrics"]["strike_expected_enemy_forced_movement_feet"])
        altered = copy.deepcopy(suite["goliath-stone-shared-strikes"])
        altered["strikeOpportunities"][1]["turn"] = 1
        rows = power.evaluate(profile, self.catalog, 1, altered)["strikeEventTrace"]
        self.assertEqual("once-per-turn-exhausted", rows[1]["status"])

    def test_storm_save_dependent_disadvantage_is_timing_conditional(self):
        profile = self.synthetic_goliath("storm")
        suite = {c["id"]: c["scenario"] for c in self.scenario_suite()["contexts"]}
        baseline = power.evaluate(profile, self.catalog, 1,
                                  suite["goliath-storm-disadvantage-window"])
        self.assertAlmostEqual(2 * .55 * .65 * .35,
                               baseline["metrics"]["strike_expected_enemy_attack_hits_prevented_by_disadvantage"])
        no_attacks = copy.deepcopy(suite["goliath-storm-disadvantage-window"])
        no_attacks["strikeOpportunities"][0]["normalAttackRollsBeforeRecovery"] = 0
        result = power.evaluate(profile, self.catalog, 1, no_attacks)
        self.assertEqual(0, result["metrics"]["strike_expected_enemy_attack_hits_prevented_by_disadvantage"])

    def test_hit_without_damage_can_trigger_stone_not_hill(self):
        profile = self.synthetic_goliath("stone")
        e = copy.deepcopy(next(x["scenario"] for x in self.scenario_suite()["contexts"]
                     if x["id"] == "goliath-storm-disadvantage-window"))
        # Replace Storm with Stone; a confirmed hit still qualifies even if
        # the original weapon did no damage (e.g., was resisted).
        e["strikeOpportunities"][0].update(
            traitId="trait.goliath.stone.stone-strike", dealtDamage=False)
        stone = power.evaluate(profile, self.catalog, 1, e)
        self.assertEqual("activated", stone["strikeEventTrace"][0]["status"])
        self.assertEqual(3.5, stone["metrics"]["strike_gross_extra_damage_on_confirmed_hits"])
        e["strikeOpportunities"][0]["traitId"] = "trait.goliath.hill.hills-tumble"
        hill = power.evaluate(profile, self.catalog, 1, e)
        self.assertEqual("hit-without-required-damage", hill["strikeEventTrace"][0]["status"])

    def test_confirmed_hit_damage_without_creature_control(self):
        profile = self.synthetic_goliath("stone")
        e = copy.deepcopy(next(x["scenario"] for x in self.scenario_suite()["contexts"]
                     if x["id"] == "goliath-storm-disadvantage-window"))
        e["strikeOpportunities"][0].update(
            traitId="trait.goliath.stone.stone-strike", targetIsCreature=False)
        stone = power.evaluate(profile, self.catalog, 1, e)
        self.assertEqual("activated", stone["strikeEventTrace"][0]["status"])
        self.assertNotIn("strike_expected_enemy_forced_movement_feet", stone["metrics"])

    def test_joint_defense_offense_pool_is_not_implicitly_double_counted(self):
        profile = self.synthetic_goliath("stone")
        scene = copy.deepcopy(next(x["scenario"] for x in self.scenario_suite()["contexts"]
                     if x["id"] == "goliath-storm-disadvantage-window"))
        scene["strikeOpportunities"][0]["traitId"] = "trait.goliath.stone.stone-strike"
        scene["reactionDamageEvents"] = [{
            "id":"damage", "round":1, "amount":20, "damageType":"slashing",
            "isAttackHit":True, "attackerVisible":True, "reactionAvailable":True}]
        result = power.evaluate(profile, self.catalog, 1, scene)
        self.assertIn({"traitId": "trait.goliath.stone.stones-endurance",
                       "reason": "joint-offense-defense-resource-plan-unresolved"},
                      result["unmodeled"])

    def test_strike_events_validate_inputs_and_no_silent_unmodeled(self):
        scene = next(x["scenario"] for x in self.scenario_suite()["contexts"]
                     if x["id"] == "goliath-storm-disadvantage-window")
        for override in ({"failureProbability": 1.2}, {"traitId": ""},
                         {"hit": "true"}, {"normalAttackRollsBeforeRecovery": -1},
                         {"turn": 0}, {"targetSize": "colossal"},
                         {"nornalAttackRollsBeforeRecovery": 1}):
            bad = copy.deepcopy(scene)
            bad["strikeOpportunities"][0].update(override)
            with self.subTest(bad=override), self.assertRaises(ValueError):
                power.validate_scenario(bad)

    @staticmethod
    def tactical_profile():
        """Three exact native mechanical structures, without a mock power price."""
        return {"intrinsics": {"walkingSpeedFeet": 35}, "traits": {
            "trait.goliath.stone.stone-throw": {
                "id": "stone-throw", "requiredCharacterLevel": 4,
                "activation": {"action": "bonus-action"}, "requiresItem": "rock",
                "attack": {"type": "ranged-spell-attack", "rangeFeet": 60},
                "damage": {"dice": "1d10", "damageType": "force"},
                "savingThrow": {"ability": "strength", "onSuccess": "no-prone"},
                "applyConditionOnFailedSave": "condition.prone",
                "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}},
            "trait.goliath.fire.searing-ignition": {
                "id": "searing-ignition", "requiredCharacterLevel": 4,
                "activation": {"action": "attack", "replaces": "one-attack"},
                "oncePerTurn": True, "area": {"shape": "sphere", "radiusFeet": 15,
                                               "targets": "creatures-of-your-choice"},
                "savingThrow": {"ability": "dexterity", "onSuccess": "half-damage-only",
                                "onFailure": "full-damage-and-condition"},
                "damage": {"dice": "1d8", "add": "proficiency-bonus", "damageType": "fire"},
                "applyConditionOnFailedSave": {"condition": "condition.blinded",
                                               "endsAt": "start-of-your-next-turn"},
                "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}},
            "trait.goliath.storm.maelstrom-aura": {
                "id": "maelstrom-aura", "requiredCharacterLevel": 4,
                "activation": {"action": "bonus-action"},
                "area": {"shape": "aura", "radiusFeet": 10, "blockedBy": "total-cover"},
                "duration": {"endsAt": "start-of-your-next-turn",
                             "alsoEndsWhen": "condition.incapacitated"},
                "whileActive": {"attackRollsTargetingYou": "disadvantage",
                                "damageResistance": [
                                    {"damageType": "lightning", "percent": 50},
                                    {"damageType": "thunder", "percent": 50}]},
                "startsTurnInside": {"target": "other-creature",
                                     "savingThrow": {"ability": "strength",
                                                     "onSuccess": "no-speed-change"},
                                     "onFailedSave": {"speedMultiplier": .5,
                                                       "endsAt": "start-of-targets-next-turn"}},
                "uses": {"value": "proficiency-bonus", "recharge": "long-rest"}},
        }}

    @staticmethod
    def tactical_event(kind, round_number=1):
        common = {"id": f"{kind}-{round_number}", "traitId": {
            "stone-throw": "trait.goliath.stone.stone-throw",
            "searing-ignition": "trait.goliath.fire.searing-ignition",
            "maelstrom-aura": "trait.goliath.storm.maelstrom-aura"}[kind],
            "kind": kind, "round": round_number, "actionAvailable": True}
        extras = {
            "stone-throw": {"rockAvailable": True, "distanceFeet": 40,
                            "hitChance": .65, "targetIsCreature": True,
                            "failureProbability": .55,
                            "normalAttackRollsBeforeRecovery": 2,
                            "baseHitChance": .65,
                            "bonusActionAlternativeExpectedDamage": 3},
            "searing-ignition": {"enemyTargets": 2,
                                 "failureProbability": .55,
                                 "enemyNormalAttackRollsBeforeRecovery": 2,
                                 "baseHitChance": .65,
                                 "attackReplacementExpectedDamage": 5.2},
            "maelstrom-aura": {"enemyStartsWithinAura": 2,
                               "failureProbability": .55,
                               "attacksTargetingYouBeforeExpiry": 2,
                               "baseHitChance": .65, "targetSpeedFeet": 30,
                               "auraUninterrupted": True,
                               "incomingNonattackPackets": [
                                   {"type": "lightning", "amount": 20},
                                   {"type": "thunder", "amount": 10},
                                   {"type": "fire", "amount": 10}],
                               "bonusActionAlternativeExpectedDamage": 3},
        }[kind]
        return {**common, **extras}

    def tactical_catalog(self):
        catalog = copy.deepcopy(self.catalog)
        catalog["conditions"].update({
            "condition.prone": {"effects": [{"type": "roll-mode", "appliesTo": "attack-roll",
                                               "mode": "disadvantage",
                                               "subject": "attacks-by-affected-creature"}]},
            "condition.blinded": {"effects": [{"type": "roll-mode", "appliesTo": "attack-roll",
                                                 "mode": "disadvantage",
                                                 "subject": "attacks-by-affected-creature"}]},
        })
        return catalog

    def test_stone_throw_expected_damage_prone_and_bonus_action_limits(self):
        events = [self.tactical_event("stone-throw", round_number=r)
                  for r in (1, 1, 2, 3, 4, 5)]
        for i, event in enumerate(events):
            event["id"] = f"stone-{i}"
        events[2]["distanceFeet"] = 70  # No remote rock targeting.
        events[3]["rockAvailable"] = False
        scenario = {"tacticalOpportunities": events}
        profile = self.tactical_profile()
        catalog = self.tactical_catalog()
        before = power.evaluate(profile, catalog, 3, scenario)
        self.assertEqual(0, before["metrics"].get("tactical_activated_uses", 0))
        output = power.evaluate(profile, catalog, 4, scenario)
        self.assertEqual(["activated", "action-already-spent-this-round",
                          "out-of-range", "missing-rock", "activated",
                          "resource-exhausted"],
                         [e["status"] for e in output["tacticalEventTrace"]])
        self.assertAlmostEqual(7.15, output["metrics"]["tactical_expected_gross_damage"])
        self.assertAlmostEqual(.715,
                               output["metrics"]["tactical_expected_enemy_prone_applications"])
        self.assertAlmostEqual(1.15,
                               output["metrics"]["tactical_expected_net_bonus_action_damage"])
        self.assertEqual(0, output["metrics"].get("tactical_attack_replacements", 0))
        at_five = power.evaluate(profile, catalog, 5, scenario)
        self.assertEqual(3, at_five["metrics"]["tactical_activated_uses"])

    def test_searing_ignition_preserves_damage_and_blind_timing(self):
        scene = {"tacticalOpportunities": [self.tactical_event("searing-ignition")]}
        output = power.evaluate(self.tactical_profile(), self.tactical_catalog(), 4, scene)
        m = output["metrics"]
        self.assertAlmostEqual(10.075, m["tactical_expected_gross_damage"])
        self.assertAlmostEqual(4.875, m["tactical_expected_net_attack_replacement_damage"])
        self.assertAlmostEqual(1.1, m["tactical_expected_enemy_blinded_applications"])
        self.assertAlmostEqual(2 * .55 * .65 * .35,
                               m["tactical_expected_enemy_attack_hits_prevented"])
        self.assertEqual(1, m["tactical_attack_replacements"])
        # Enemy rolls after the next-turn expiry do not count as lost attacks.
        scene["tacticalOpportunities"][0]["enemyNormalAttackRollsBeforeRecovery"] = 0
        later = power.evaluate(self.tactical_profile(), self.tactical_catalog(), 4, scene)
        self.assertEqual(0, later["metrics"].get(
            "tactical_expected_enemy_attack_hits_prevented", 0))

    def test_maelstrom_aura_short_duration_and_passive_resistance_overlap(self):
        events = [self.tactical_event("maelstrom-aura", r) for r in (1, 1, 2, 3, 4)]
        for i, event in enumerate(events):
            event["id"] = f"storm-{i}"
        events[2]["auraUninterrupted"] = False
        scenario = {"tacticalOpportunities": events}
        baseline = power.evaluate(self.tactical_profile(), self.tactical_catalog(), 4, scenario)
        statuses = [e["status"] for e in baseline["tacticalEventTrace"]]
        self.assertEqual(["activated", "action-already-spent-this-round",
                          "aura-interruption-unresolved", "activated",
                          "resource-exhausted"], statuses)
        self.assertAlmostEqual(.91, baseline["metrics"][
            "tactical_expected_self_attack_hits_prevented"])
        self.assertAlmostEqual(33, baseline["metrics"][
            "tactical_expected_enemy_nominal_speed_reduction_feet"])
        self.assertAlmostEqual(30, baseline["metrics"][
            "tactical_expected_nonattack_damage_prevented"])
        profile = self.tactical_profile()
        profile["traits"]["trait.custom.lightning-resistance"] = {
            "id": "resistance", "damageResistance": {"percent": 50,
                                                      "damageType": "lightning"}}
        overlap = power.evaluate(profile, self.tactical_catalog(), 4, scenario)
        self.assertAlmostEqual(10, overlap["metrics"][
            "tactical_expected_nonattack_damage_prevented"])

    def test_mixed_tactical_traits_share_bonus_action_not_uses(self):
        first = self.tactical_event("stone-throw", 1)
        second = self.tactical_event("maelstrom-aura", 1)
        events = [first, second]
        result = power.evaluate(self.tactical_profile(), self.tactical_catalog(), 4,
                                {"tacticalOpportunities": events})
        self.assertEqual(["activated", "action-already-spent-this-round"],
                         [e["status"] for e in result["tacticalEventTrace"]])
        self.assertEqual(1, result["metrics"]["tactical_bonus_actions_used"])
        self.assertNotIn("tactical_expected_self_attack_hits_prevented", result["metrics"])

    def test_tactical_definition_changes_fail_closed(self):
        profile = self.tactical_profile()
        profile["traits"]["trait.goliath.stone.stone-throw"]["attack"]["rangeFeet"] = 80
        r = power.evaluate(profile, self.tactical_catalog(), 4, {
            "tacticalOpportunities": [self.tactical_event("stone-throw")]})
        self.assertEqual("unrecognized-authored-mechanics", r["tacticalEventTrace"][0]["status"])
        self.assertTrue(any(x.get("reason") == "tactical-definition-does-not-match-fixture"
                            for x in r["unmodeled"]))

    def test_tactical_event_validation_rejects_bad_inputs(self):
        event = self.tactical_event("stone-throw")
        cases = ({"hitChance": 1.1}, {"rockAvailable": "yes"},
                 {"distanceFeet": -1}, {"normalAttackRollsBeforeRecovery": -1},
                 {"rockAvaiable": False}, {"kind": "breathe"}, {"round": 0})
        for bad in cases:
            row = {**event, **bad}
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                power.validate_scenario({"tacticalOpportunities": [row]})

    @staticmethod
    def joint_strike(round_number, name):
        return {"type": "strike", "opportunity": {
            "id": name, "traitId": "trait.goliath.stone.stone-strike",
            "turn": round_number, "delivery": "melee-weapon",
            "mode": "base", "targetSize": "medium", "hit": True,
            "dealtDamage": True, "targetIsCreature": True,
            "failureProbability": .55}}

    @staticmethod
    def joint_reaction(round_number, name):
        return {"type": "reaction",
                "traitId": "trait.goliath.stone.stones-endurance",
                "reactionAlternativeExpectedDamage": 0,
                "opportunity": {"id": name, "round": round_number,
                                "amount": 20, "damageType": "force",
                                "isAttackHit": True, "attackerVisible": True,
                                "reactionAvailable": True}}

    def test_joint_martial_offense_and_defense_use_one_stone_pool(self):
        profile = self.synthetic_goliath("stone")
        events = [self.joint_strike(1, "hit-1"),
                  self.joint_reaction(1, "incoming-1"),
                  self.joint_strike(2, "hit-2")]
        scenario = {"abilityModifiers": {"constitution": 3},
                    "jointCombatPlans": [{"id": "martial-stone", "archetype": "weapon-martial",
                                          "events": events}]}
        result = power.evaluate(profile, self.catalog, 4, scenario)
        joint = result["jointCombatPlanResults"][0]
        self.assertEqual(["activated", "activated", "shared-resource-exhausted"],
                         [x["status"] for x in joint["events"]])
        self.assertEqual(2, joint["resourcesSpent"]["trait.goliath.stone.stones-endurance"])
        self.assertAlmostEqual(3.5, joint["metrics"]["joint_expected_net_damage_delta"])
        self.assertAlmostEqual(9.5, joint["metrics"]["joint_reaction_damage_prevented"])
        self.assertNotIn("joint_expected_net_damage_delta", result["metrics"])

    def test_joint_skirmisher_bonus_action_competition_across_traits(self):
        profile = self.tactical_profile()
        first = self.tactical_event("stone-throw", 1)
        second = self.tactical_event("maelstrom-aura", 1)
        third = self.tactical_event("maelstrom-aura", 2)
        for n, event in enumerate((first, second, third)):
            event["id"] = f"joint-tactical-{n}"
        plans = [{"type": "tactical", "opportunity": event}
                 for event in (first, second, third)]
        output = power.evaluate(profile, self.tactical_catalog(), 4,
                                {"jointCombatPlans": [{"id": "skirmisher",
                                                       "archetype": "bonus-action-skirmisher",
                                                       "events": plans}]})
        joint = output["jointCombatPlanResults"][0]
        self.assertEqual(["activated", "action-already-spent-this-round", "activated"],
                         [row["status"] for row in joint["events"]])
        self.assertEqual(1, joint["resourcesSpent"]["trait.goliath.stone.stone-throw"])
        self.assertEqual(1, joint["resourcesSpent"]["trait.goliath.storm.maelstrom-aura"])
        self.assertAlmostEqual(.65*5.5-6, joint["metrics"]["joint_expected_net_damage_delta"])
        self.assertAlmostEqual(.455, joint["metrics"][
            "joint_tactical_expected_self_attack_hits_prevented"])

    def test_joint_caster_spell_opportunity_cost_reverses_searing_damage(self):
        event = self.tactical_event("searing-ignition", 1)
        event["attackReplacementExpectedDamage"] = 12.0
        result = power.evaluate(self.tactical_profile(), self.tactical_catalog(), 4,
                                {"jointCombatPlans": [{"id": "prepared-caster",
                                                       "archetype": "prepared-caster",
                                                       "events": [{"type": "tactical",
                                                                   "opportunity": event}]}]})
        metrics = result["jointCombatPlanResults"][0]["metrics"]
        self.assertAlmostEqual(2*(4.5+2)*(.5+.5*.55)-12,
                               metrics["joint_expected_net_damage_delta"])
        self.assertLess(metrics["joint_expected_net_damage_delta"], 0)
        event["attackReplacementExpectedDamage"] = 5.2
        martial = power.evaluate(self.tactical_profile(), self.tactical_catalog(), 4,
                                 {"jointCombatPlans": [{"id": "martial",
                                                        "archetype": "weapon-martial",
                                                        "events": [{"type": "tactical",
                                                                    "opportunity": event}]}]})
        self.assertGreater(martial["jointCombatPlanResults"][0]["metrics"][
            "joint_expected_net_damage_delta"], 0)

    def test_joint_plan_reaction_slot_and_shared_owner_collision_fail_closed(self):
        profile = self.synthetic_goliath("stone")
        action = self.joint_reaction(1, "a")
        later = self.joint_reaction(1, "b")
        plan = {"id": "reaction-conflict", "archetype": "weapon-martial",
                "events": [action, later]}
        result = power.evaluate(profile, self.catalog, 4, {"jointCombatPlans": [plan]})
        statuses = [e["status"] for e in result["jointCombatPlanResults"][0]["events"]]
        self.assertEqual(["activated", "action-already-spent-this-round"], statuses)
        profile["traits"]["trait.test.collision"] = {
            "id": "stones-endurance", "uses": {"value": 2, "recharge": "long-rest"}}
        strike = self.joint_strike(1, "first")
        plan["events"] = [strike]
        collision = power.evaluate(profile, self.catalog, 4, {"jointCombatPlans": [plan]})
        self.assertEqual("unresolved-shared-resource-owner",
                         collision["jointCombatPlanResults"][0]["events"][0]["status"])

    def test_joint_reaction_has_explicit_competing_attack_cost(self):
        profile = self.synthetic_goliath("stone")
        reaction = self.joint_reaction(1, "costly-reaction")
        reaction["reactionAlternativeExpectedDamage"] = 5
        plan = {"id": "reaction-tradeoff", "archetype": "weapon-martial",
                "events": [reaction]}
        outcome = power.evaluate(profile, self.catalog, 4,
                                 {"abilityModifiers": {"constitution": 3},
                                  "jointCombatPlans": [plan]})["jointCombatPlanResults"][0]
        self.assertEqual("activated", outcome["events"][0]["status"])
        self.assertAlmostEqual(9.5, outcome["metrics"]["joint_reaction_damage_prevented"])
        self.assertAlmostEqual(-5, outcome["metrics"]["joint_expected_net_damage_delta"])
        # Damage prevented and damage forgone must remain separate domains.
        self.assertNotIn("joint_utility_score", outcome["metrics"])

    def test_joint_independent_stone_throw_pool_can_share_round_with_strike(self):
        profile = self.synthetic_goliath("stone")
        profile["traits"].update(self.tactical_profile()["traits"])
        catalog = self.tactical_catalog()
        throw = self.tactical_event("stone-throw", 1)
        plan = {"id": "three-slots", "archetype": "weapon-martial", "events": [
            self.joint_strike(1, "hit-one"),
            {"type": "tactical", "opportunity": throw},
            self.joint_reaction(1, "incoming-one")]}
        r = power.evaluate(profile, catalog, 4,
                           {"abilityModifiers": {"constitution": 3},
                            "jointCombatPlans": [plan]})["jointCombatPlanResults"][0]
        self.assertEqual(["activated", "activated", "activated"],
                         [e["status"] for e in r["events"]])
        self.assertEqual({"trait.goliath.stone.stone-throw": 1,
                          "trait.goliath.stone.stones-endurance": 2},
                         r["resourcesSpent"])
        self.assertEqual({"trait.goliath.stone.stone-throw": 2,
                          "trait.goliath.stone.stones-endurance": 2},
                         r["resourcePoolCapacities"])
        self.assertAlmostEqual(3.5 + (.65 * 5.5 - 3),
                               r["metrics"]["joint_expected_net_damage_delta"])
        self.assertAlmostEqual(9.5, r["metrics"]["joint_reaction_damage_prevented"])

    def test_joint_attack_replacement_blocks_same_attack_slot_rider(self):
        profile = self.synthetic_goliath("stone")
        profile["traits"].update(self.tactical_profile()["traits"])
        catalog = self.tactical_catalog()
        ignition = self.tactical_event("searing-ignition", 1)
        r = power.evaluate(profile, catalog, 4, {"jointCombatPlans": [{
            "id": "one-attack", "archetype": "weapon-martial", "events": [
                {"type": "tactical", "opportunity": ignition},
                self.joint_strike(1, "same-attack")]}]})
        trace = r["jointCombatPlanResults"][0]["events"]
        self.assertEqual(["activated", "action-already-spent-this-round"],
                         [x["status"] for x in trace])

    def test_joint_plan_strict_validation(self):
        strike = self.joint_strike(1, "a")
        reaction = self.joint_reaction(2, "b")
        good = {"id": "valid", "archetype": "weapon-martial",
                "events": [strike, reaction]}
        power.validate_scenario({"jointCombatPlans": [good]})
        invalid = [
            {**good, "archetype": "telepathic-caster"},
            {**good, "events": [reaction, strike]},
            {**good, "events": [strike, strike]},
            {**good, "events": [strike, {**reaction, "reactionAlternativeExpectedDamage": -1}]},
            {**good, "events": [strike, {**reaction, "opportunity": {
                **reaction["opportunity"], "roudn": 2}}]},
            {**good, "events": [strike, {"type": "tactical", "opportunity":
                self.tactical_event("stone-throw", 2) |
                {"bonusActionAlternativeExpectedDamage": 3} | {
                    "actionAvailable": True}}]},
        ]
        invalid[-1]["events"][1]["opportunity"].pop("bonusActionAlternativeExpectedDamage")
        for case in invalid:
            with self.subTest(case=case), self.assertRaises(ValueError):
                power.validate_scenario({"jointCombatPlans": [case]})

    def test_joint_scenario_corpus_has_three_class_builds(self):
        suite = self.scenario_suite()
        self.assertEqual(40, len(suite["contexts"]))
        plans = [p for context in suite["contexts"]
                 for p in context["scenario"].get("jointCombatPlans", [])]
        self.assertEqual({"weapon-martial", "bonus-action-skirmisher", "prepared-caster"},
                         {p["archetype"] for p in plans})
        power.validate_scenario_suite(suite)

    @staticmethod
    def class_plan(class_id, procedure="standard", events=None):
        return {"id": "class-plan", "classId": class_id,
                "attackProcedure": procedure, "baseHitChance": 0.65,
                "weaponDamageOnHit": 8,
                "events": events or [{"id": "attack-one", "round": 1, "kind": "attack"}]}

    def test_standard_extra_attack_not_added_to_iterative_bab(self):
        b = self.class_plan("class.barbarian")
        profile = self.profile("ancestry.dragonborn.red")
        for level, standard, iterative in ((1, 1, 1), (5, 2, 1), (11, 2, 3), (17, 2, 4)):
            with self.subTest(level=level):
                b["attackProcedure"] = "standard"
                standard_result = power.evaluate(profile, self.catalog, level,
                                                 {"nativeClassActionPlans": [b]})
                b["attackProcedure"] = "iterative"
                iterative_result = power.evaluate(profile, self.catalog, level,
                                                  {"nativeClassActionPlans": [b]})
                a = standard_result["nativeClassActionPlanResults"][0]["classProgression"]
                other = iterative_result["nativeClassActionPlanResults"][0]["classProgression"]
                self.assertEqual(standard, a["attackCount"])
                self.assertEqual(iterative, other["attackCount"])
                self.assertAlmostEqual(standard * 5.2, a["attackActionExpectedDamage"])
        result = power.evaluate(profile, self.catalog, 11,
                                {"nativeClassActionPlans": [b]})
        attacks = result["nativeClassActionPlanResults"][0]["classProgression"]
        self.assertEqual([11, 6, 1], attacks["evidence"]["baseAttackBonuses"])
        self.assertAlmostEqual(8 * (.65 + .40 + .15), attacks["attackActionExpectedDamage"])

    def test_attack_replacement_keeps_remaining_extra_attack(self):
        b = self.class_plan("class.barbarian", events=[
            {"id": "breath-on-second-attack", "round": 1, "kind": "breath",
             "traitId": "trait.dragonborn.breath", "targets": 2,
             "saveFailure": .55, "replacedAttackIndex": 1}])
        row = power.evaluate(self.profile("ancestry.dragonborn.red"),
                             self.catalog, 5, {"nativeClassActionPlans": [b]})
        events = row["nativeClassActionPlanResults"][0]["events"]
        self.assertEqual("used", events[0]["status"])
        self.assertAlmostEqual(17.05, events[0]["grossExpectedDamage"])
        self.assertAlmostEqual(5.2 + 17.05, events[0]["expectedDamage"])
        self.assertAlmostEqual(11.85, events[0]["marginalVersusWeaponAttack"])
        b["events"][0]["replacedAttackIndex"] = 2
        row = power.evaluate(self.profile("ancestry.dragonborn.red"), self.catalog, 5,
                             {"nativeClassActionPlans": [b]})
        self.assertEqual("attack-index-not-available",
                         row["nativeClassActionPlanResults"][0]["events"][0]["status"])

    def test_searing_ignition_replaces_one_class_attack(self):
        b = self.class_plan("class.barbarian", events=[
            {"id": "searing-one", "round": 1, "kind": "searing-ignition",
             "traitId": "trait.goliath.fire.searing-ignition", "targets": 2,
             "saveFailure": .55, "replacedAttackIndex": 0}])
        for level, expected in ((4, 10.075), (5, 16.825)):
            with self.subTest(level=level):
                result = power.evaluate(self.tactical_profile(),
                                        self.tactical_catalog(), level,
                                        {"nativeClassActionPlans": [b]})
                event = result["nativeClassActionPlanResults"][0]["events"][0]
                self.assertEqual("used", event["status"])
                self.assertAlmostEqual(expected, event["expectedDamage"])

    def test_wizard_spell_slots_and_casting_cost_are_separate(self):
        events = [{"id": f"cast-{i}", "round": i, "kind": "spell",
                   "slotLevel": 3, "spellId": "hypothetical-spell",
                   "expectedDamage": 12} for i in range(1, 5)]
        b = self.class_plan("class.wizard", events=events)
        results = power.evaluate(self.profile("ancestry.dragonborn.red"),
                                 self.catalog, 5, {"nativeClassActionPlans": [b]})
        p = results["nativeClassActionPlanResults"][0]
        self.assertEqual(["used-fixture-assumption", "used-fixture-assumption",
                          "spell-slot-exhausted-or-locked", "spell-slot-exhausted-or-locked"],
                         [e["status"] for e in p["events"]])
        self.assertEqual(0, p["resourceExpenditure"]["remainingClassSpellSlotsByLevel"][3])
        self.assertAlmostEqual(6.8, p["events"][0]["marginalVersusWeaponAttack"])
        self.assertTrue(any(x["reason"] == "spell-identity-and-effect-fixture-assumed"
                            for x in results["unmodeled"]))
        early = power.evaluate(self.profile("ancestry.dragonborn.red"),
                               self.catalog, 1, {"nativeClassActionPlans": [b]})
        self.assertTrue(all(e["status"] == "spell-slot-exhausted-or-locked"
                            for e in early["nativeClassActionPlanResults"][0]["events"]))

    def test_spell_alternative_compares_full_action_not_one_weapon_attack(self):
        b = self.class_plan("class.wizard", events=[
            {"id": "breathe", "round": 1, "kind": "breath",
             "traitId": "trait.dragonborn.breath", "targets": 2,
             "saveFailure": .55, "replacedAttackIndex": 0,
             "spellAlternative": {"slotLevel": 3, "expectedDamage": 12}}])
        row = power.evaluate(self.profile("ancestry.dragonborn.red"), self.catalog, 5,
                             {"nativeClassActionPlans": [b]})["nativeClassActionPlanResults"][0]["events"][0]
        self.assertAlmostEqual(17.05, row["expectedDamage"])
        self.assertAlmostEqual(5.05, row["marginalVersusSpellAction"])
        self.assertAlmostEqual(11.85, row["marginalVersusWeaponAttack"])
        # A Barbarian without a class slot cannot assume the hypothetical spell.
        b["classId"] = "class.barbarian"
        blocked = power.evaluate(self.profile("ancestry.dragonborn.red"), self.catalog, 5,
                                 {"nativeClassActionPlans": [b]})
        self.assertEqual("slot-not-available", blocked["nativeClassActionPlanResults"][0]
                         ["events"][0]["spellAlternativeStatus"])

    def test_rogue_unchanged_reaction_rule_requires_native_unlock(self):
        event = {"id": "dodge-one", "round": 1, "kind": "class-reaction",
                 "featureId": "class-feature.rogue.uncanny-dodge-level-5",
                 "damageAmount": 21, "damageType": "force",
                 "attackerVisible": True, "isAttackHit": True}
        b = self.class_plan("class.rogue", events=[event, {**event, "id": "dodge-two"},
                                                  {**event, "id": "dodge-three", "round": 2}])
        at_five = power.evaluate(self.profile("species.dwarf"), self.catalog, 5,
                                 {"nativeClassActionPlans": [b]})
        rows = at_five["nativeClassActionPlanResults"][0]["events"]
        self.assertEqual(["used", "same-round-slot-already-used", "used"],
                         [x["status"] for x in rows])
        self.assertEqual([11, 11], [x["expectedDamagePrevented"]
                                    for x in rows if x["status"] == "used"])
        at_four = power.evaluate(self.profile("species.dwarf"), self.catalog, 4,
                                 {"nativeClassActionPlans": [b]})
        self.assertEqual("class-reaction-not-granted-or-unverified",
                         at_four["nativeClassActionPlanResults"][0]["events"][0]["status"])
        b["events"][0]["attackerVisible"] = False
        untriggered = power.evaluate(self.profile("species.dwarf"), self.catalog, 5,
                                    {"nativeClassActionPlans": [b]})
        self.assertEqual("reaction-trigger-not-met",
                         untriggered["nativeClassActionPlanResults"][0]["events"][0]["status"])

    def test_class_and_origin_reactions_compete_for_one_slot(self):
        profile = self.synthetic_goliath("stone")
        stone = {"id": "stone-prevention", "round": 1, "kind": "origin-reaction",
                 "traitId": "trait.goliath.stone.stones-endurance",
                 "damageAmount": 20, "damageType": "force",
                 "attackerVisible": True, "isAttackHit": True}
        rogue = {"id": "rogue-prevention", "round": 1, "kind": "class-reaction",
                 "featureId": "class-feature.rogue.uncanny-dodge-level-5",
                 "damageAmount": 20, "damageType": "force",
                 "attackerVisible": True, "isAttackHit": True}
        plan = self.class_plan("class.rogue", events=[stone, rogue,
                                    {**rogue, "id": "next-round", "round": 2}])
        result = power.evaluate(profile, self.catalog, 5,
              {"abilityModifiers": {"constitution": 3},
               "nativeClassActionPlans": [plan]})
        outcome = result["nativeClassActionPlanResults"][0]
        self.assertEqual(["used", "same-round-slot-already-used", "used"],
                         [e["status"] for e in outcome["events"]])
        self.assertAlmostEqual(9.5, outcome["events"][0]["expectedDamagePrevented"])
        self.assertEqual(10, outcome["events"][2]["expectedDamagePrevented"])
        self.assertEqual({"trait.goliath.stone.stones-endurance": 1},
                         outcome["resourceExpenditure"]["originTraitUses"])
        # When Constitution is unspecified, do not claim that +0 is its value;
        # the class reaction remains available instead.
        unresolved = power.evaluate(profile, self.catalog, 5,
                                    {"nativeClassActionPlans": [plan]})
        rows = unresolved["nativeClassActionPlanResults"][0]["events"]
        self.assertEqual(["missing-ability-modifier", "used", "used"],
                         [e["status"] for e in rows])

    def test_native_class_action_plans_fail_closed(self):
        b = self.class_plan("class.barbarian")
        bad = [
            {"attackProceduer": "standard"}, {"attackProcedure": "mix"},
            {"baseHitChance": 1.5}, {"weaponDamageOnHit": -1},
            {"events": [{"id": "a", "kind": "attack", "round": 0}]},
            {"events": [{"id": "a", "kind": "attack", "round": 1},
                        {"id": "a", "kind": "attack", "round": 2}]},
            {"events": [{"id": "a", "kind": "spell", "round": 1,
                        "slotLevel": 0, "spellId": "X", "expectedDamage": 2}]},
            {"events": [{"id": "a", "kind": "spell", "round": 1,
                        "slotLevel": 1, "spellId": "X", "expectedDamge": 2}]},
        ]
        for fields in bad:
            with self.subTest(fields=fields), self.assertRaises(ValueError):
                power.validate_scenario({"nativeClassActionPlans": [{**b, **fields}]})
        broken = copy.deepcopy(self.catalog)
        broken["classFeatures"]["class-feature.barbarian.extra-attack-level-5"][
            "definition"]["mechanics"]["babInteraction"] = "additive"
        m, issue = power._native_class_actions(broken, "class.barbarian", 5,
                                              "standard", .65, 8)
        self.assertIsNone(m)
        self.assertEqual("unverified-native-extra-attack-feature", issue)

    @staticmethod
    def precision(index=0, **changes):
        values = {"attackIndex": index, "basis": "unanticipated",
                  "weaponProficient": True, "withinOrdinaryRange": True,
                  "targetImmune": False}
        return {**values, **changes}

    def test_native_rogue_sneak_requires_explicit_eligible_hits(self):
        profile = self.profile("species.dwarf")
        plan = self.class_plan("class.rogue", events=[
            {"id": "qualified", "kind": "attack", "round": 1,
             "precisionHits": [self.precision()]},
            {"id": "baseline", "kind": "attack", "round": 2},
            {"id": "immune", "kind": "attack", "round": 3,
             "precisionHits": [self.precision(targetImmune=True)]},
        ])
        out = power.evaluate(profile, self.catalog, 4, {"nativeClassActionPlans": [plan]})
        events = out["nativeClassActionPlanResults"][0]["events"]
        self.assertAlmostEqual(9.75, events[0]["expectedDamage"])
        self.assertAlmostEqual(4.55, events[0]["sneakAttackExpectedExtraDamage"])
        self.assertAlmostEqual(5.2, events[1]["expectedDamage"])
        self.assertAlmostEqual(0, events[2]["sneakAttackExpectedExtraDamage"])

    def test_rogue_steady_aim_improves_roll_but_not_sneak_eligibility(self):
        profile = self.profile("species.dwarf")
        feature = "class-feature.rogue.steady-aim-level-3"
        plan = self.class_plan("class.rogue", events=[
            {"id": "aim", "kind": "class-bonus-action", "round": 1,
             "featureId": feature, "option": "steady-aim", "movedThisTurn": False},
            {"id": "weapon", "kind": "attack", "round": 1},
            {"id": "aim-moved", "kind": "class-bonus-action", "round": 2,
             "featureId": feature, "option": "steady-aim", "movedThisTurn": True},
            {"id": "weapon-2", "kind": "attack", "round": 2,
             "precisionHits": [self.precision()]},
        ])
        out = power.evaluate(profile, self.catalog, 4, {"nativeClassActionPlans": [plan]})
        events = out["nativeClassActionPlanResults"][0]["events"]
        self.assertEqual("used", events[0]["status"])
        self.assertAlmostEqual(7.02, events[1]["expectedDamage"])
        self.assertNotIn("sneakAttackExpectedExtraDamage", events[1])
        self.assertEqual("steady-aim-movement-restriction", events[2]["status"])
        self.assertAlmostEqual(9.75, events[3]["expectedDamage"])
        plan["events"][1]["precisionHits"] = [self.precision()]
        out2 = power.evaluate(profile, self.catalog, 4, {"nativeClassActionPlans": [plan]})
        self.assertAlmostEqual(7.02 + (.8775 * 7),
                               out2["nativeClassActionPlanResults"][0]["events"][1]["expectedDamage"])

    def test_battle_rage_bonus_attack_count_and_spell_prohibition(self):
        profile = self.profile("species.dwarf")
        feat = "class-feature.barbarian.rage-level-1"
        def rage(round, ended=False, heavy=False):
            return {"id": f"rage-{round}", "kind": "class-bonus-action", "round": round,
                    "featureId": feat, "option": "battle-rage",
                    "wearingHeavyArmor": heavy, "rageEndedBeforeActivation": ended}
        plan = self.class_plan("class.barbarian", events=[
            rage(1), {"id": "weapon", "kind": "attack", "round": 1,
                      "strengthBasedAttacks": True},
            {"id": "spell", "kind": "spell", "round": 1, "slotLevel": 1,
             "spellId": "fixture-spell", "expectedDamage": 7},
            rage(2, True), {"id": "weapon-2", "kind": "attack", "round": 2,
                              "strengthBasedAttacks": True},
            rage(3, True), rage(4, True),
        ])
        out = power.evaluate(profile, self.catalog, 5, {"nativeClassActionPlans": [plan]})
        entries = out["nativeClassActionPlanResults"][0]["events"]
        self.assertEqual("used", entries[0]["status"])
        self.assertAlmostEqual(13.0, entries[1]["expectedDamage"])
        self.assertAlmostEqual(2.6, entries[1]["battleRageExpectedBonusDamage"])
        self.assertEqual("same-round-slot-already-used", entries[2]["status"])
        self.assertEqual("used", entries[3]["status"])
        self.assertEqual("used", entries[5]["status"])
        self.assertEqual("rage-resource-exhausted", entries[6]["status"])
        self.assertEqual(3, out["nativeClassActionPlanResults"][0]
                         ["resourceExpenditure"]["nativeRageUsesSpent"])
        separate = self.class_plan("class.barbarian", events=[
            rage(1), {"id": "cast", "round": 1, "kind": "spell",
                      "slotLevel": 1, "spellId": "fixture", "expectedDamage": 10}])
        out = power.evaluate(profile, self.catalog, 5, {"nativeClassActionPlans": [separate]})
        self.assertEqual("rage-forbids-spellcasting",
                         out["nativeClassActionPlanResults"][0]["events"][1]["status"])

    def test_rogue_cunning_and_origin_throw_compete_for_bonus_action(self):
        profile = self.profile("species.dwarf")
        ident = "trait.goliath.stone.stone-throw"
        profile["traits"][ident] = {
            "id": "stone-throw", "requiredCharacterLevel": 4,
            "activation": {"action": "bonus-action"},
            "requiresItem": "rock", "attack": {"type": "ranged-spell-attack", "rangeFeet": 60},
            "damage": {"dice": "1d10", "damageType": "force"},
            "savingThrow": {"ability": "strength", "onSuccess": "no-prone"},
            "applyConditionOnFailedSave": "condition.prone",
            "uses": {"value": "proficiency-bonus", "recharge": "long-rest"},
        }
        self.catalog["conditions"]["condition.prone"] = {"id": "condition.prone", "effects": []}
        throw = {"id": "throw", "round": 1, "kind": "origin-bonus-action",
                 "traitId": ident, "rockAvailable": True, "distanceFeet": 30,
                 "hitChance": 0.65, "saveFailure": 0.55, "targetIsCreature": True}
        hide = {"id": "hide", "round": 1, "kind": "class-bonus-action",
                "featureId": "class-feature.rogue.cunning-action-level-2", "option": "hide"}
        plan = self.class_plan("class.rogue", events=[hide, throw,
            {**throw, "id": "throw-again", "round": 2}])
        out = power.evaluate(profile, self.catalog, 4, {"nativeClassActionPlans": [plan]})
        rows = out["nativeClassActionPlanResults"][0]["events"]
        self.assertEqual(["used", "same-round-slot-already-used", "used"],
                         [x["status"] for x in rows])
        self.assertAlmostEqual(3.575, rows[2]["expectedGrossDamage"])
        self.assertEqual({ident: 1}, out["nativeClassActionPlanResults"][0]
                         ["resourceExpenditure"]["originTraitUses"])

    def test_battle_rage_replacement_retains_only_unreplaced_strength_bonus(self):
        profile = self.profile("ancestry.dragonborn.red")
        plan = self.class_plan("class.barbarian", events=[
            {"id": "rage", "round": 1, "kind": "class-bonus-action",
             "featureId": "class-feature.barbarian.rage-level-1", "option": "battle-rage",
             "wearingHeavyArmor": False, "rageEndedBeforeActivation": False},
            {"id": "breath", "round": 1, "kind": "breath",
             "traitId": "trait.dragonborn.breath", "targets": 2,
             "saveFailure": 0.55, "replacedAttackIndex": 0,
             "strengthBasedAttacks": True},
        ])
        results = power.evaluate(profile, self.catalog, 5, {"nativeClassActionPlans": [plan]})
        row = results["nativeClassActionPlanResults"][0]["events"][1]
        self.assertEqual("used", row["status"])
        self.assertAlmostEqual(23.55, row["expectedDamage"])
        self.assertAlmostEqual(1.3, row["battleRageExpectedBonusOnRetainedAttacks"])
        plan["events"][1].pop("strengthBasedAttacks")
        unresolved = power.evaluate(profile, self.catalog, 5, {"nativeClassActionPlans": [plan]})
        self.assertEqual("rage-attack-basis-unspecified",
                         unresolved["nativeClassActionPlanResults"][0]["events"][1]["status"])

    def test_native_rogue_multiple_qualifying_hits_get_independent_precision(self):
        profile = self.profile("species.dwarf")
        plan = self.class_plan("class.rogue", "iterative", events=[
            {"id": "two-hits", "round": 1, "kind": "attack",
             "precisionHits": [self.precision(0), self.precision(1)]},
            {"id": "bad-index", "round": 2, "kind": "attack",
             "precisionHits": [self.precision(2)]},
        ])
        out = power.evaluate(profile, self.catalog, 11, {"nativeClassActionPlans": [plan]})
        row = out["nativeClassActionPlanResults"][0]["events"][0]
        self.assertAlmostEqual(22.05, row["sneakAttackExpectedExtraDamage"])
        self.assertAlmostEqual(30.45, row["expectedDamage"])
        self.assertIn("precision-attack-index-unavailable",
                      [x["reason"] for x in out["nativeClassActionPlanResults"][0]["unresolved"]])
        incompatible = copy.deepcopy(self.catalog)
        incompatible["classFeatures"]["class-feature.rogue.sneak-attack-level-1"][
            "definition"]["mechanics"]["advantageAloneInsufficient"] = False
        out2 = power.evaluate(profile, incompatible, 11, {"nativeClassActionPlans": [plan]})
        self.assertIn("native-sneak-attack-unverified-or-not-granted",
                      [x["reason"] for x in out2["nativeClassActionPlanResults"][0]["unresolved"]])

    def test_class_bonus_and_precision_contracts_fail_closed(self):
        plan = self.class_plan("class.rogue")
        wrong = [
            {"kind": "class-bonus-action", "featureId": "x", "option": "hide",
             "movedThisTurn": False},
            {"kind": "class-bonus-action", "featureId": "x", "option": "steady-aim"},
            {"kind": "class-bonus-action", "featureId": "x", "option": "battle-rage",
             "wearingHeavyArmor": False},
            {"kind": "attack", "precisionHits": [{"attackIndex": 0, "basis": "advantage",
                 "weaponProficient": True, "withinOrdinaryRange": True,
                 "targetImmune": False}]},
            {"kind": "attack", "strengthBasedAttacks": 1},
            {"kind": "origin-bonus-action", "traitId": "x", "rockAvailable": True,
             "distanceFeet": 10, "hitChance": .65, "saveFailure": .55,
             "targetIsCreature": "yes"},
        ]
        for i, action in enumerate(wrong):
            broken = copy.deepcopy(plan)
            broken["events"][0] = {"id": "invalid", "round": 1, **action}
            with self.subTest(case=i), self.assertRaises(ValueError):
                power.validate_scenario({"nativeClassActionPlans": [broken]})

@unittest.skipUnless((power.DEFAULT_NATIVE / "species").is_dir(),
                     "Full native catalog is not available in isolated fixture tests")
class NativeCatalogIntegrationTests(unittest.TestCase):
    """Exercise actual canonical content when running inside the repository."""

    def test_all_terminal_paths_at_all_breakpoints(self):
        catalog = power.load_native(power.DEFAULT_NATIVE)
        self.assertGreaterEqual(len(power.terminal_sources(catalog)), 9)
        for source in power.terminal_sources(catalog, include_deferred=True):
            with self.subTest(source=source):
                profile = power.reconstruct_preset(catalog, source)
                for level in power.LEVELS:
                    result = power.evaluate(profile, catalog, level)
                    self.assertEqual(source, result["sourceId"])
                    self.assertIsInstance(result["unmodeled"], list)
                    self.assertIsInstance(result["metrics"], dict)


    def test_full_native_scenario_corpus(self):
        """Every native terminal source must tolerate all approved fixtures."""
        catalog = power.load_native(power.DEFAULT_NATIVE)
        sources = power.terminal_sources(catalog)
        self.assertGreaterEqual(len(sources), 28)
        suite_path = Path(__file__).parent / "fixtures/pillar2-power-scenarios.json"
        suite = json.loads(suite_path.read_text(encoding="utf-8"))
        result = power.evaluate_scenario_suite(catalog, suite, sources)
        self.assertEqual(40, result["scenarioCount"])
        self.assertEqual(len(sources), result["terminalProfileCount"])
        for context in result["contexts"]:
            self.assertEqual(len(sources), len(context["profiles"]))
            for entry in context["profiles"]:
                self.assertEqual(list(power.LEVELS),
                                 [row["level"] for row in entry["levels"]])
                for evaluation in entry["levels"]:
                    self.assertIsInstance(evaluation["unmodeled"], list)


    def test_native_joint_action_and_resource_replay(self):
        catalog = power.load_native(power.DEFAULT_NATIVE)
        profiles = {
            "stone": power.reconstruct_preset(catalog, "ancestry.goliath.stone"),
            "fire": power.reconstruct_preset(catalog, "ancestry.goliath.fire")}
        suite = json.loads((Path(__file__).parent / "fixtures/pillar2-power-scenarios.json")
                           .read_text(encoding="utf-8"))
        scenarios = {c["id"]: c["scenario"] for c in suite["contexts"]}
        stone = power.evaluate(profiles["stone"], catalog, 4,
                               scenarios["joint-weapon-martial-stone"])
        plan = stone["jointCombatPlanResults"][0]
        self.assertEqual(["activated", "activated", "shared-resource-exhausted"],
                         [row["status"] for row in plan["events"]])
        self.assertAlmostEqual(9.5, plan["metrics"]["joint_reaction_damage_prevented"])
        fire = power.evaluate(profiles["fire"], catalog, 4,
                              scenarios["joint-prepared-caster-searing"])
        self.assertLess(fire["jointCombatPlanResults"][0]["metrics"][
            "joint_expected_net_damage_delta"], 0)

    def test_native_directional_signals_are_preserved(self):
        """Check known native contrasts, not merely that the evaluator runs."""
        catalog = power.load_native(power.DEFAULT_NATIVE)
        suite_path = Path(__file__).parent / "fixtures/pillar2-power-scenarios.json"
        suite = json.loads(suite_path.read_text(encoding="utf-8"))
        sources = ["ancestry.dragonborn.red", "ancestry.dragonborn.bronze",
                   "ancestry.elf.drow", "ancestry.dragonborn.amethyst",
                   "ancestry.gnome.rock", "ancestry.dwarf.hill"]
        # The nonselectable Amethyst reference is tested as authored data,
        # never treated as permission for character creation.
        result = power.evaluate_scenario_suite(catalog, suite, sources)
        contexts = {c["id"]: {r["id"]: r["levels"] for r in c["profiles"]}
                    for c in result["contexts"]}
        def metric(context, source, level_index, key):
            return contexts[context][source][level_index]["metrics"].get(key, 0)
        damage = "breath_expected_net_damage_delta"
        self.assertGreater(metric("corridor-area-opportunities", "ancestry.dragonborn.red", 0, damage),
                           metric("corridor-area-opportunities", "ancestry.dragonborn.bronze", 0, damage))
        self.assertGreater(metric("cluster-area-opportunities", "ancestry.dragonborn.bronze", 0, damage),
                           metric("cluster-area-opportunities", "ancestry.dragonborn.red", 0, damage))
        self.assertAlmostEqual(-0.6, metric("sunlight-unprotected", "ancestry.elf.drow", 0,
                                            "condition_expected_success_delta"))
        self.assertEqual(0, metric("sunlight-protected", "ancestry.elf.drow", 0,
                                   "condition_expected_success_delta"))
        self.assertEqual(2, metric("stone-earth-contacts", "ancestry.dwarf.hill", 0,
                                   "tremorsense_contacts_detected"))
        self.assertEqual(2, metric("stone-earth-contacts", "ancestry.gnome.rock", 0,
                                   "tremorsense_contacts_detected"))
        movement = "origin_mobility_gates_satisfied"
        self.assertEqual(0, metric("travel-separated-gates", "ancestry.dragonborn.amethyst", 0,
                                   movement))
        self.assertEqual(1, metric("travel-separated-gates", "ancestry.dragonborn.amethyst", 3,
                                   movement))
        self.assertEqual(3, metric("travel-concentrated-gates", "ancestry.dragonborn.amethyst", 3,
                                   movement))


    def test_native_spell_entitlements_feat_choices_and_metallic_control(self):
        catalog = power.load_native(power.DEFAULT_NATIVE)
        suite = json.loads((Path(__file__).parent / "fixtures/pillar2-power-scenarios.json")
                           .read_text(encoding="utf-8"))
        result = power.evaluate_scenario_suite(catalog, suite,
            ["ancestry.elf.high", "ancestry.tiefling.infernal", "species.human",
             "ancestry.dragonborn.bronze", "ancestry.dragonborn.red"])
        contexts = {c["id"]: {p["id"]: p["levels"] for p in c["profiles"]}
                    for c in result["contexts"]}
        magic = contexts["innate-fixed-spell-demands"]
        high = magic["ancestry.elf.high"]
        self.assertEqual(0, high[0]["metrics"]["innate_fixed_free_cast_capacity"])
        self.assertEqual(1, high[1]["metrics"]["innate_fixed_free_cast_capacity"])
        self.assertEqual(2, high[3]["metrics"]["spell_free_cast_demands_matched"])
        infernal = magic["ancestry.tiefling.infernal"][3]
        self.assertEqual(2, infernal["metrics"]["spell_at_will_demands_matched"])
        self.assertEqual(2, infernal["metrics"]["spell_free_cast_demands_matched"])
        human = magic["species.human"][0]
        self.assertEqual(1, human["metrics"]["origin_feat_choice_entitlements"])
        self.assertTrue(any(x.get("reason") == "unresolved-origin-feat-effects"
                            for x in human["unmodeled"]))
        bronze = contexts["metallic-enervation-with-allies"]["ancestry.dragonborn.bronze"]
        self.assertEqual(0, bronze[2]["metrics"]["control_attack_replacements"])
        self.assertAlmostEqual(1.10, bronze[3]["metrics"][
            "control_expected_enemy_incapacitated_applications"])
        self.assertAlmostEqual(0.55, bronze[3]["metrics"][
            "control_expected_ally_incapacitated_applications"])
        self.assertEqual(0, contexts["metallic-enervation-with-allies"]["ancestry.dragonborn.red"][3]
                         ["metrics"]["control_attack_replacements"])
        repulsion = contexts["metallic-repulsion"] ["ancestry.dragonborn.bronze"][3]
        self.assertAlmostEqual(1.65, repulsion["metrics"]["control_expected_enemy_prone_applications"])
        self.assertAlmostEqual(33.0, repulsion["metrics"]["control_expected_enemy_forced_movement_feet"])

    def test_native_metallic_condition_windows_use_authored_effects(self):
        """Real Incapacitated/Prone conditions and Metallic Breath timing."""
        catalog = power.load_native(power.DEFAULT_NATIVE)
        bronze = power.reconstruct_preset(catalog, "ancestry.dragonborn.bronze")
        fixture_path = Path(__file__).parent / "fixtures/pillar2-power-scenarios.json"
        suite = json.loads(fixture_path.read_text(encoding="utf-8"))
        contexts = {c["id"]:c["scenario"] for c in suite["contexts"]}
        early = power.evaluate(bronze, catalog, 5,
                               contexts["metallic-enervation-with-allies"])
        self.assertAlmostEqual(1.10, early["metrics"]["control_expected_enemy_action_slots_denied"])
        self.assertAlmostEqual(.55, early["metrics"]["control_expected_ally_action_slots_denied"])
        self.assertAlmostEqual(.55, early["metrics"]["control_expected_enemy_concentrations_ended"])
        late = power.evaluate(bronze, catalog, 5,
                              contexts["metallic-control-no-exposed-turns"])
        self.assertEqual(0, late["metrics"]["control_expected_enemy_action_slots_denied"])
        self.assertAlmostEqual(early["metrics"]["control_expected_enemy_incapacitated_applications"],
                               late["metrics"]["control_expected_enemy_incapacitated_applications"])
        prone = power.evaluate(bronze, catalog, 5,
                               contexts["metallic-repulsion"])
        self.assertAlmostEqual(3*.55*.65*.35,
          prone["metrics"]["control_expected_enemy_normal_attack_hits_prevented_by_prone"])
        self.assertNotIn("control_expected_enemy_action_slots_denied", prone["metrics"])
        before_unlock = power.evaluate(bronze, catalog, 4,
                                       contexts["metallic-enervation-with-allies"])
        self.assertEqual(0, before_unlock["metrics"]["control_attack_replacements"])

    def test_native_goliath_hit_control_and_shared_pool_traces(self):
        catalog = power.load_native(power.DEFAULT_NATIVE)
        contexts = {c["id"]: c["scenario"] for c in json.loads(
            (Path(__file__).parent / "fixtures/pillar2-power-scenarios.json")
            .read_text(encoding="utf-8"))["contexts"]}
        expectations = [
            ("ancestry.goliath.hill", "goliath-hill-automatic-prone",
             "strike_expected_enemy_prone_applications", 1),
            ("ancestry.goliath.hill", "goliath-hill-alternative-prone",
             "strike_expected_enemy_prone_applications", .55),
            ("ancestry.goliath.frost", "goliath-frost-speed-modes",
             "strike_expected_enemy_nominal_speed_reduction_feet", 26.5),
            ("ancestry.goliath.stone", "goliath-stone-shared-strikes",
             "strike_expected_enemy_forced_movement_feet", 11),
            ("ancestry.goliath.storm", "goliath-storm-disadvantage-window",
             "strike_expected_enemy_attack_hits_prevented_by_disadvantage", 2*.55*.65*.35),
        ]
        for source, scene, metric, expected in expectations:
            with self.subTest(source=source, scene=scene):
                row = power.evaluate(power.reconstruct_preset(catalog, source), catalog,
                                     1, contexts[scene])
                self.assertAlmostEqual(expected, row["metrics"][metric])
                self.assertTrue(any(t["status"] == "activated" for t in row["strikeEventTrace"]))

    def test_native_goliath_activated_actions_are_authored_and_modeled(self):
        catalog = power.load_native(power.DEFAULT_NATIVE)
        contexts = {c["id"]: c["scenario"] for c in json.loads(
            (Path(__file__).parent / "fixtures/pillar2-power-scenarios.json")
            .read_text(encoding="utf-8"))["contexts"]}
        examples = (
            ("ancestry.goliath.stone", "goliath-stone-throw-bonus-action",
             "tactical_expected_gross_damage", 7.15),
            ("ancestry.goliath.fire", "goliath-fire-selective-ignition",
             "tactical_expected_enemy_blinded_applications", 1.1),
            ("ancestry.goliath.storm", "goliath-storm-defensive-aura",
             "tactical_expected_nonattack_damage_prevented", 15),
        )
        for source, scenario_id, metric, expected in examples:
            with self.subTest(source=source, scenario=scenario_id):
                result = power.evaluate(power.reconstruct_preset(catalog, source),
                                        catalog, 4, contexts[scenario_id])
                self.assertAlmostEqual(expected, result["metrics"][metric])
                self.assertTrue(any(x["status"] == "activated"
                                    for x in result["tacticalEventTrace"]))
                self.assertFalse(any(x.get("reason") ==
                                     "tactical-definition-does-not-match-fixture"
                                     for x in result["unmodeled"]))

    def test_native_save_and_defensive_reaction_contrasts(self):
        """Match scenario outputs to authored Dwarf, Gnome, Stone and Cloud traits."""
        catalog = power.load_native(power.DEFAULT_NATIVE)
        suite = json.loads((Path(__file__).parent / "fixtures/pillar2-power-scenarios.json")
                           .read_text(encoding="utf-8"))
        sources = ["ancestry.dwarf.hill", "ancestry.gnome.forest",
                   "ancestry.goliath.stone", "ancestry.goliath.cloud"]
        outcomes = power.evaluate_scenario_suite(catalog, suite, sources)
        lookup = {c["id"]: {p["id"]: p["levels"] for p in c["profiles"]}
                  for c in outcomes["contexts"]}
        poison = lookup["poison-saves-and-resistance"]["ancestry.dwarf.hill"][0]
        self.assertAlmostEqual(0.25, poison["metrics"]["saving_throw_success_delta"])
        self.assertAlmostEqual(8.75, poison["metrics"]["saving_throw_damage_prevented"])
        magic = lookup["mental-magic-and-control"]["ancestry.gnome.forest"][0]
        self.assertAlmostEqual(0.25, magic["metrics"]["saving_throw_success_delta"])
        reactions = lookup["heavy-hit-reaction-windows"]
        stone = reactions["ancestry.goliath.stone"][0]
        self.assertAlmostEqual(19, stone["metrics"]["reaction_damage_prevented"])
        self.assertEqual(3, len(stone["sharedResourceFrontiers"][0]["frontier"]))
        cloud = reactions["ancestry.goliath.cloud"]
        self.assertEqual(0, cloud[0]["metrics"]["reaction_damage_prevented"])
        self.assertAlmostEqual(20, cloud[2]["metrics"]["reaction_damage_prevented"])
        self.assertTrue(any("teleport" in row.get("fields", [])
                            for row in cloud[2]["unmodeled"]))


    def test_native_fire_goliath_searing_uses_class_extra_attack(self):
        catalog = power.load_native(power.DEFAULT_NATIVE)
        profile = power.reconstruct_preset(catalog, "ancestry.goliath.fire")
        suite = json.loads((Path(__file__).parent / "fixtures/pillar2-power-scenarios.json")
                           .read_text(encoding="utf-8"))
        scenario = next(ctx["scenario"] for ctx in suite["contexts"]
                        if ctx["id"] == "native-barbarian-searing-ignition")
        before = power.evaluate(profile, catalog, 4, scenario)
        after = power.evaluate(profile, catalog, 5, scenario)
        self.assertAlmostEqual(10.075, before["nativeClassActionPlanResults"][0]
                               ["events"][0]["expectedDamage"])
        self.assertAlmostEqual(16.825, after["nativeClassActionPlanResults"][0]
                               ["events"][0]["expectedDamage"])

    def test_native_goliath_rogue_reactions_share_reaction_slot(self):
        catalog = power.load_native(power.DEFAULT_NATIVE)
        profile = power.reconstruct_preset(catalog, "ancestry.goliath.stone")
        scene = {"abilityModifiers": {"constitution": 3},
                 "nativeClassActionPlans": [{
            "id": "rogue-plus-stone", "classId": "class.rogue",
            "attackProcedure": "standard", "baseHitChance": .65,
            "weaponDamageOnHit": 8,
            "events": [
                {"id": "stone", "round": 1, "kind": "origin-reaction",
                 "traitId": "trait.goliath.stone.stones-endurance",
                 "damageAmount": 20, "damageType": "force",
                 "attackerVisible": True, "isAttackHit": True},
                {"id": "rogue", "round": 1, "kind": "class-reaction",
                 "featureId": "class-feature.rogue.uncanny-dodge-level-5",
                 "damageAmount": 20, "damageType": "force",
                 "attackerVisible": True, "isAttackHit": True},
            ]}]} 
        out = power.evaluate(profile, catalog, 5, scene)
        status = [e["status"] for e in out["nativeClassActionPlanResults"][0]["events"]]
        self.assertEqual(["used", "same-round-slot-already-used"], status)

    def test_native_class_progression_is_authoritative(self):
        catalog = power.load_native(power.DEFAULT_NATIVE)
        self.assertIn("class.barbarian", catalog["classes"])
        self.assertIn("class.rogue", catalog["classes"])
        self.assertIn("class.wizard", catalog["classes"])
        for level, standard, iterative in ((1, 1, 1), (5, 2, 1),
                                           (11, 2, 3), (17, 2, 4)):
            for procedure, expected in (("standard", standard), ("iterative", iterative)):
                with self.subTest(level=level, procedure=procedure):
                    model, issue = power._native_class_actions(
                        catalog, "class.barbarian", level, procedure, .65, 8)
                    self.assertIsNone(issue)
                    self.assertEqual(expected, model["attackCount"])
        wizard, issue = power._native_class_actions(
            catalog, "class.wizard", 5, "standard", .65, 8)
        self.assertIsNone(issue)
        self.assertEqual(2, wizard["nativeSpellSlotsByLevel"][3])
        self.assertEqual(1, wizard["attackCount"])
        rogue = power.reconstruct_preset(catalog, "species.orc")
        scene = {"nativeClassActionPlans": [{
            "id": "rogue-react", "classId": "class.rogue", "attackProcedure": "standard",
            "baseHitChance": .65, "weaponDamageOnHit": 8,
            "events": [{"id": "visible-hit", "kind": "class-reaction", "round": 1,
                        "featureId": "class-feature.rogue.uncanny-dodge-level-5",
                        "damageAmount": 21, "damageType": "force",
                        "attackerVisible": True, "isAttackHit": True}]}]}
        before = power.evaluate(rogue, catalog, 4, scene)
        after = power.evaluate(rogue, catalog, 5, scene)
        self.assertEqual("class-reaction-not-granted-or-unverified",
                         before["nativeClassActionPlanResults"][0]["events"][0]["status"])
        self.assertEqual(11, after["nativeClassActionPlanResults"][0]["events"][0]
                         ["expectedDamagePrevented"])



    def test_native_battle_rage_sneak_and_bonus_actions(self):
        """Verify selected native authoring contracts in the real catalog."""
        catalog = power.load_native(power.DEFAULT_NATIVE)
        dwarf = power.reconstruct_preset(catalog, "species.dwarf")
        barbarian = {"nativeClassActionPlans": [{
            "id": "rage-native", "classId": "class.barbarian",
            "attackProcedure": "standard", "baseHitChance": .65,
            "weaponDamageOnHit": 8, "events": [
                {"id": "rage", "round": 1, "kind": "class-bonus-action",
                 "featureId": "class-feature.barbarian.rage-level-1", "option": "battle-rage",
                 "wearingHeavyArmor": False, "rageEndedBeforeActivation": False},
                {"id": "attack", "round": 1, "kind": "attack",
                 "strengthBasedAttacks": True},
            ]}]}
        result = power.evaluate(dwarf, catalog, 5, barbarian)
        events = result["nativeClassActionPlanResults"][0]["events"]
        self.assertEqual("used", events[0]["status"])
        self.assertAlmostEqual(13, events[1]["expectedDamage"])
        self.assertEqual(1, result["nativeClassActionPlanResults"][0]["resourceExpenditure"]
                         ["nativeRageUsesSpent"])
        rogue = {"nativeClassActionPlans": [{
            "id": "sneak-native", "classId": "class.rogue",
            "attackProcedure": "iterative", "baseHitChance": .65,
            "weaponDamageOnHit": 8, "events": [
                {"id": "eligible", "round": 1, "kind": "attack",
                 "precisionHits": [self.precision()]},
                {"id": "aim", "round": 2, "kind": "class-bonus-action",
                 "featureId": "class-feature.rogue.steady-aim-level-3", "option": "steady-aim",
                 "movedThisTurn": False},
                {"id": "aimed", "round": 2, "kind": "attack"},
            ]}]}
        r = power.evaluate(dwarf, catalog, 5, rogue)
        events = r["nativeClassActionPlanResults"][0]["events"]
        self.assertAlmostEqual(.65 * 10.5, events[0]["sneakAttackExpectedExtraDamage"])
        self.assertEqual("used", events[1]["status"])
        self.assertAlmostEqual(.8775 * 8, events[2]["expectedDamage"])
        stone = power.reconstruct_preset(catalog, "ancestry.goliath.stone")
        clash = {"nativeClassActionPlans": [{
            "id": "stone-rogue", "classId": "class.rogue",
            "attackProcedure": "standard", "baseHitChance": .65,
            "weaponDamageOnHit": 8, "events": [
                {"id": "hide", "round": 1, "kind": "class-bonus-action",
                 "featureId": "class-feature.rogue.cunning-action-level-2", "option": "hide"},
                {"id": "stone-throw", "round": 1, "kind": "origin-bonus-action",
                 "traitId": "trait.goliath.stone.stone-throw", "rockAvailable": True,
                 "distanceFeet": 40, "targetIsCreature": True,
                 "hitChance": .65, "saveFailure": .55},
            ]}]}
        r = power.evaluate(stone, catalog, 5, clash)
        rows = r["nativeClassActionPlanResults"][0]["events"]
        self.assertEqual(["used", "same-round-slot-already-used"],
                         [e["status"] for e in rows])

if __name__ == "__main__":
    unittest.main()
