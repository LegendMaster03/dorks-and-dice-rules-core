"""Regression fixtures for a read-only characteristic power evaluation prototype."""
import json
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
                       "fields": ["savingThrows", "spell"]}, result["unmodeled"])

    def test_all_fixture_presets_run_all_six_breakpoints(self):
        sources = power.terminal_sources(self.catalog)
        for source in sources:
            for level in power.LEVELS:
                result = power.evaluate(self.profile(source), self.catalog, level)
                self.assertEqual(level, result["level"])
                self.assertIn("unmodeled", result)


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


if __name__ == "__main__":
    unittest.main()
