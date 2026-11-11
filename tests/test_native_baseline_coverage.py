"""Permanent integrity and coverage checks for the staged, non-executable native SRD baseline."""
import collections
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
WORKING = ROOT / "data/rulesets/first-party-001/working"
AUTHORITATIVE = ROOT / "data/rulesets/first-party-001/native"
REF = ROOT / "data/reference/srd"


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


class NativeSrdBaselineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.coverage = load(REF / "native-coverage-manifest.json")
        cls.entries = cls.coverage["coverage"]

    def test_full_pinned_source_pack_coverage(self):
        meta = self.coverage
        self.assertEqual(90, meta["sourceFilesVerified"])
        self.assertEqual(2522, meta["nativeDefinitionCount"])
        self.assertEqual(2225, meta["individualEntityFiles"])
        self.assertEqual(2522, len(self.entries))
        self.assertEqual([], meta["warnings"])
        self.assertEqual([], meta["excludedSourceRecords"])
        self.assertEqual(
            "d06d1dadee357857767b1e4da985df6609509bcf",
            meta["pinnedPackRevision"])
        self.assertEqual(
            ["Legal Information"],
            [s["name"] for s in meta["referenceOnlyBookSections"]])
        self.assertFalse((WORKING / "rules/legal-information.json").exists())
        self.assertTrue((REF / "structured/book/book-srd51.json").exists())
        self.assertTrue((REF / "structured/book/book-srd52.json").exists())

    def test_every_staged_entity_exists_and_has_stable_unique_id(self):
        seen = set()
        paths = set()
        for row in self.entries:
            cid = row["nativeId"]
            # Preserve the pinned SRD source identity even when approved
            # rules use a different canonical native identity (subrace ->
            # Ancestry). Only the explicit mapping may resolve that rename.
            target_id = row.get("reconciledAsNativeId", cid)
            self.assertNotIn(cid, seen, cid)
            seen.add(cid)
            self.assertRegex(cid, r"^[a-z0-9-]+(?:\.[a-z0-9-]+)+$")
            if row["status"] == "existing-native-covered":
                self.assertNotIn("path", row)
                continue
            relative = row["path"]
            self.assertTrue(relative.startswith("data/rulesets/first-party-001/"), relative)
            path = ROOT / relative
            self.assertTrue(path.is_file(), relative)
            paths.add(path)
            if path.parent in (WORKING, AUTHORITATIVE):
                catalog = load(path)
                matches = [x for v in catalog.values() if isinstance(v, list)
                           for x in v if isinstance(x, dict) and x.get("id") == target_id]
                self.assertEqual(1, len(matches), cid)
                native = matches[0]
            else:
                native = load(path)
                self.assertEqual(target_id, native["id"], relative)
            if cid.startswith("condition.") or cid == "rule.conditions":
                self.assertNotIn("baselineStatus", native, cid)
                self.assertNotIn("candidateDefinitions", native, cid)
                if cid.startswith("condition."):
                    approved = {
                        decision["nativeRuleId"] for decision in
                        load(REF / "reconciliation-decisions.json")["decisions"]
                    }
                    self.assertIn(cid, approved, cid)
                else:
                    conditions = load(AUTHORITATIVE / "conditions.json")["conditions"]
                    self.assertEqual({c["id"] for c in conditions},
                                     set(native["conditionIds"]))
                continue
            if row["status"] == "approved-native-reconciled":
                self.assertTrue(path.is_relative_to(AUTHORITATIVE), relative)
                self.assertNotIn("baselineStatus", native, cid)
                self.assertNotIn("candidateDefinitions", native, cid)
                expected_kind = {"race": "species", "subrace": "subspecies"}.get(
                    row["kind"], row["kind"])
                if target_id != cid:
                    # An explicit approved migration maps historical subrace
                    # evidence to the independently selected native Ancestry.
                    self.assertEqual("subrace", row["kind"], cid)
                    self.assertTrue(cid.startswith("subspecies."), cid)
                    self.assertEqual("ancestry." + cid.removeprefix("subspecies."),
                                     target_id)
                    expected_kind = "ancestry"
                self.assertEqual(expected_kind, native["kind"], cid)
                self.assertIn("name", native, cid)
                self.assertEqual("selected-native", native.get("status"), cid)
                continue
            if row["status"] == "matching-candidates":
                self.assertNotIn("candidateDefinitions", native, cid)
                self.assertNotIn("baselineStatus", native, cid)
                self.assertIn("abbreviation", native, cid)
                continue
            if row["status"] == "native-monster-identity-alias":
                self.assertEqual("native-identity-alias", native["baselineStatus"], cid)
                self.assertTrue(native["aliasOf"].startswith("monster."), cid)
                self.assertNotIn("definition", native, cid)
                self.assertNotIn("candidateDefinitions", native, cid)
                continue
            if row["status"] in ("native-single-source-retained",
                                 "native-equivalent-reconciled",
                                 "native-compatible-merged",
                                 "native-monster-role-default"):
                self.assertNotIn("candidateDefinitions", native, cid)
                self.assertEqual("retained-native-definition", native["baselineStatus"])
                self.assertIsInstance(native["definition"], dict)
                continue
            self.assertEqual("pending-native-reconciliation",
                             native["baselineStatus"], cid)
            self.assertNotIn("activeDefinition", native, cid)
            self.assertTrue(native["candidateDefinitions"], cid)
            self.assertEqual(row["sourceRecords"],
                             len(native["candidateDefinitions"]), cid)
            self.assertTrue(all(v["key"].startswith(("candidate-a", "candidate-b"))
                                and isinstance(v["content"], dict)
                                for v in native["candidateDefinitions"]))
            self.assertNotIn("source", native)
        tracked = {p for p in WORKING.rglob("*.json") if p.parent != WORKING}
        source_tracked = {p for p in paths if p.is_relative_to(WORKING) and p.parent != WORKING}
        self.assertEqual(set(), source_tracked - tracked)
        native_only = tracked - source_tracked
        approved_non_srd_invocations = {
            WORKING / "features/optional" / f"{name}.json"
            for name in (
                "improved-pact-weapon", "gift-of-the-ever-living-ones",
                "aspect-of-the-moon", "far-scribe",
                "rebuke-of-the-talisman", "protection-of-the-talisman",
                "bond-of-the-talisman",
            )
        }
        approved_native_only = {
            WORKING / "features/classes/wizard" / f"{name}.json"
            for name in (
                "scribe-scroll-level-1",
                "wizard-specialization-level-5",
                "wizard-specialization-level-10",
                "wizard-specialization-level-15",
                "wizard-specialization-level-20",
            )
        }
        self.assertEqual(approved_native_only | approved_non_srd_invocations, native_only)
        for path in native_only:
            entity = load(path)
            if path in approved_non_srd_invocations:
                contract = entity["nativeInvocationDefinition"]
                self.assertEqual("pending-native-reconciliation", entity["baselineStatus"])
                self.assertFalse(contract["nativeExecutable"])
                self.assertEqual("non-srd-review-pending", contract["sourceRightsReview"])
                self.assertFalse(entity["publishedSourceBoundary"]["fullProtectedTextImported"])
                continue
            self.assertNotIn(entity["id"], seen)
            seen.add(entity["id"])
            self.assertEqual("retained-native-definition", entity["baselineStatus"])
            self.assertEqual("class.wizard", entity["parentClass"])
            self.assertIsInstance(entity["definition"]["mechanics"], dict)

    def test_historical_subrace_aliases_map_only_to_reviewed_ancestries(self):
        """Retain source traceability without reviving retired native IDs."""
        expected = {
            "subspecies.dwarf.hill": "ancestry.dwarf.hill",
            "subspecies.elf.high": "ancestry.elf.high",
            "subspecies.gnome.rock": "ancestry.gnome.rock",
            "subspecies.halfling.lightfoot": "ancestry.halfling.lightfoot",
        }
        mapped = {r["nativeId"]: r["reconciledAsNativeId"]
                  for r in self.entries if "reconciledAsNativeId" in r}
        self.assertEqual(expected, mapped)
        for row in self.entries:
            if row["nativeId"] not in expected:
                self.assertNotIn("reconciledAsNativeId", row)

    def test_native_skill_and_competency_ids_are_unique(self):
        # These catalogs evolve during reconciliation; do not pin provisional
        # record counts or require an intermediate working-catalog mirror.
        for catalog_name, key, id_prefix in (
            ("skills.json", "skills", "skill."),
            ("competencies.json", "competencies", "competency."),
        ):
            entries = load(AUTHORITATIVE / catalog_name)[key]
            ids = [entry["id"] for entry in entries]
            self.assertEqual(len(ids), len(set(ids)), catalog_name)
            self.assertTrue(all(id.startswith(id_prefix) for id in ids),
                            catalog_name)

    def test_approved_condition_data_not_overwritten(self):
        conditions = load(AUTHORITATIVE / "conditions.json")["conditions"]
        self.assertEqual(len(conditions), len({c["id"] for c in conditions}))
        self.assertEqual(0, sum(x.get("baselineStatus") ==
                                "pending-native-reconciliation" for x in conditions))
        ex = next(x for x in conditions if x["id"] == "condition.exhaustion")
        self.assertEqual(6, len(ex["levels"]))
        gr = next(x for x in conditions if x["id"] == "condition.grappled")
        attack_penalty = [e for e in gr["effects"] if e.get("type") == "roll-mode"]
        self.assertEqual("disadvantage", attack_penalty[0]["mode"])
        self.assertEqual({"targetRelationToGrappler": "other-than-grappler"},
                         attack_penalty[0]["when"])

    def test_class_species_parent_structure(self):
        classes = {p.stem for p in (WORKING / "classes/base").glob("*.json")}
        species = {p.stem for p in (WORKING / "species/base").glob("*.json")} | {p.stem for p in (AUTHORITATIVE / "species/base").glob("*.json")}
        self.assertEqual(12, len(classes))
        self.assertGreaterEqual(len(species), 10)
        for p in (WORKING / "classes/subclasses").glob("*/*.json"):
            d = load(p)
            self.assertEqual("class." + p.parent.name, d["parentClass"])
            self.assertIn(p.parent.name, classes)
            self.assertEqual("subclass." + p.parent.name + "." + p.stem, d["id"])
        for p in (WORKING / "species/subspecies").glob("*/*.json"):
            d = load(p)
            self.assertEqual("species." + p.parent.name, d["parentSpecies"])
            self.assertIn(p.parent.name, species)
            self.assertEqual("subspecies." + p.parent.name + "." + p.stem, d["id"])

    def test_selected_species_subspecies_and_upbringings_have_stable_parents(self):
        native = AUTHORITATIVE
        species_defs = {
            r["id"]: r
            for p in (native / "species/base").rglob("*.json")
            for r in [load(p)]
        }
        subspecies_defs = {
            r["id"]: r
            for p in (native / "species/ancestry").rglob("*.json")
            for r in [load(p)]
        }
        upbringings = {
            r["id"]: r
            for p in (native / "upbringings").rglob("*.json")
            for r in [load(p)]
        }
        self.assertGreaterEqual(len(species_defs), 4)
        self.assertGreaterEqual(len(subspecies_defs), 29)
        self.assertGreaterEqual(len(upbringings), 8)
        all_species = species_defs | subspecies_defs
        for item in subspecies_defs.values():
            self.assertIn(item["parent"], all_species)
        for item in upbringings.values():
            self.assertNotIn("defaultFor", item, item["id"])
            if "parent" in item:
                self.assertIn(item["parent"], upbringings)
        for item in all_species.values():
            if "defaultUpbringing" in item:
                reference = item["defaultUpbringing"]
                self.assertIn(reference, upbringings, item["id"])
                self.assertTrue(upbringings[reference]["selectable"], reference)

        expected_defaults = {
            "species.dwarf": "upbringing.dwarf.general",
            "species.goliath": "upbringing.goliath.general",
            "ancestry.elf.high": "upbringing.elf.high",
            "ancestry.elf.wood": "upbringing.elf.wood",
            "ancestry.elf.drow": "upbringing.elf.drow",
            "ancestry.gnome.forest": "upbringing.gnome.forest",
            "ancestry.gnome.rock": "upbringing.gnome.rock",
        }
        self.assertEqual(
            expected_defaults,
            {id: item["defaultUpbringing"] for id, item in all_species.items()
             if "defaultUpbringing" in item}
        )

        # General Elf and Gnome are inherited structural parents, not
        # directly selectable Upbringings or valid Species-level defaults.
        for cultural_parent in ("upbringing.elf.general",
                                "upbringing.gnome.general"):
            self.assertIn(cultural_parent, upbringings)
            self.assertFalse(upbringings[cultural_parent]["selectable"])
        for species_id in ("species.elf", "species.gnome"):
            self.assertNotIn("defaultUpbringing", all_species[species_id])
        for child_id in ("upbringing.elf.high", "upbringing.elf.wood",
                         "upbringing.elf.drow", "upbringing.gnome.forest",
                         "upbringing.gnome.rock"):
            child = upbringings[child_id]
            self.assertTrue(child["selectable"])
            self.assertFalse(upbringings[child["parent"]]["selectable"])

        def nearest_default(id):
            current = id
            while current is not None:
                record = all_species[current]
                if "defaultUpbringing" in record:
                    return record["defaultUpbringing"]
                current = record.get("parent")
            return None

        self.assertEqual("upbringing.dwarf.general", nearest_default("ancestry.dwarf.hill"))
        self.assertEqual("upbringing.elf.high", nearest_default("ancestry.elf.high"))
        self.assertEqual("upbringing.gnome.forest", nearest_default("ancestry.gnome.forest"))
        for giant_type in ("cloud", "fire", "frost", "hill", "stone", "storm"):
            self.assertEqual("upbringing.goliath.general",
                             nearest_default("ancestry.goliath." + giant_type))
        self.assertIsNone(nearest_default("ancestry.halfling.lightfoot"))
        self.assertFalse({"species.half-elf", "species.half-orc"} & set(species_defs))
        policy = load(native / "rules/species-subspecies-upbringing.json")["upbringings"]
        self.assertEqual("defaultUpbringing", policy["defaultField"])
        self.assertEqual(1, policy["selectedCount"])
        self.assertFalse(policy["speciesRestrictionsByDefault"])
        for records in (species_defs | subspecies_defs, upbringings):
            for id in records:
                visited = set()
                cursor = id
                while cursor in records and "parent" in records[cursor]:
                    self.assertNotIn(cursor, visited, cursor)
                    visited.add(cursor)
                    cursor = records[cursor]["parent"]
                self.assertIn(cursor, records)

    def test_approved_dragonborn_shared_baseline_and_source_promotion(self):
        path = AUTHORITATIVE / "species/base/dragonborn.json"
        dragonborn = load(path)
        self.assertEqual("species.dragonborn", dragonborn["id"])
        self.assertFalse(dragonborn["selectable"])
        self.assertEqual("dragon", dragonborn["creatureType"])
        self.assertEqual("medium", dragonborn["size"])
        self.assertEqual(30, dragonborn["walkingSpeedFeet"])
        self.assertNotIn("darkvisionFeet", dragonborn)
        traits = {x["id"]: x for x in dragonborn["traits"]}
        self.assertEqual({"low-light-vision", "draconic-resistance", "breath-weapon"}, set(traits))
        self.assertEqual("rule.low-light-vision", traits["low-light-vision"]["rule"])
        self.assertEqual(50, traits["draconic-resistance"]["damageResistance"]["percent"])
        breath = traits["breath-weapon"]
        self.assertEqual("one-attack", breath["activation"]["replaces"])
        self.assertEqual("proficiency-bonus", breath["uses"]["value"])
        self.assertEqual("long-rest", breath["uses"]["recharge"])
        self.assertEqual("dexterity", breath["savingThrow"]["ability"])
        self.assertEqual("8 + proficiency-bonus + constitution-modifier", breath["savingThrow"]["dc"])
        self.assertEqual("half-damage", breath["savingThrow"]["onSuccess"])
        self.assertEqual([(1, "1d10"), (5, "2d10"), (11, "3d10"), (17, "4d10")],
                         [(x["characterLevel"], x["dice"]) for x in breath["damage"]["progression"]])
        self.assertEqual("selected-dragonborn-family-or-leaf", breath["areaFrom"])
        self.assertNotIn("shape", breath)
        self.assertFalse((WORKING / "species/base/dragonborn.json").exists())
        row = next(r for r in self.entries if r["nativeId"] == "species.dragonborn")
        self.assertEqual("approved-native-reconciled", row["status"])
        self.assertEqual("data/rulesets/first-party-001/native/species/base/dragonborn.json",
                         row["path"])

    def test_approved_chromatic_dragonborn_family_and_five_leaves(self):
        base = AUTHORITATIVE / "species/ancestry/dragonborn"
        chromatic = load(base / "chromatic.json")
        self.assertEqual("ancestry.dragonborn.chromatic", chromatic["id"])
        self.assertEqual("species.dragonborn", chromatic["parent"])
        self.assertFalse(chromatic["selectable"])
        self.assertEqual({"shape": "line", "lengthFeet": 30, "widthFeet": 5},
                         chromatic["breathWeaponArea"])
        self.assertEqual(1, len(chromatic["traits"]))
        warding = chromatic["traits"][0]
        self.assertEqual("chromatic-warding", warding["id"])
        self.assertEqual(5, warding["requiredCharacterLevel"])
        self.assertEqual("action", warding["activation"]["action"])
        self.assertEqual(1, warding["duration"]["minutes"])
        self.assertEqual({"value": 1, "recharge": "long-rest"}, warding["uses"])
        self.assertEqual(100, warding["damageResistanceOverride"]["percent"])
        self.assertEqual("selected-dragonborn-leaf",
                         warding["damageResistanceOverride"]["damageTypeFrom"])
        self.assertEqual("replace-lower-same-type",
                         warding["damageResistanceOverride"]["stacking"])
        self.assertTrue(warding["doesNotGrantConditionImmunity"])

        expected_damage_types = {
            "black": "acid", "blue": "lightning", "green": "poison",
            "red": "fire", "white": "cold"
        }
        actual = {p.stem for p in (base / "chromatic").glob("*.json")}
        self.assertEqual(set(expected_damage_types), actual)
        leaves = {
            slug: load(base / "chromatic" / (slug + ".json"))
            for slug in expected_damage_types
        }
        for slug, leaf in leaves.items():
            self.assertEqual("ancestry.dragonborn." + slug, leaf["id"])
            self.assertEqual("ancestry.dragonborn.chromatic", leaf["parent"])
            self.assertEqual("ancestry", leaf["kind"])
            self.assertTrue(leaf["selectable"])
            self.assertEqual("selected-native", leaf["status"])
            self.assertEqual(expected_damage_types[slug], leaf["damageType"])
            self.assertNotIn("defaultUpbringing", leaf)
            self.assertNotIn("breathWeaponArea", leaf)
            self.assertNotIn("flyingSpeedFeet", leaf)
            if slug != "white":
                self.assertNotIn("swimmingSpeedFeet", leaf)
            if slug != "red":
                self.assertNotIn("climbingSpeedFeet", leaf)

        for slug in ("black", "green"):
            amphibious = leaves[slug]["traits"][0]
            self.assertEqual("amphibious", amphibious["id"])
            self.assertEqual(["air", "water"], amphibious["canBreathe"])
            self.assertFalse(amphibious["grantsSwimSpeed"])

        blue = leaves["blue"]["traits"][0]
        self.assertEqual("displaced-voice", blue["id"])
        self.assertEqual("action", blue["activation"]["action"])
        self.assertTrue(blue["atWill"])
        self.assertEqual(30, blue["rangeFeet"])
        self.assertEqual("own-vocal-sounds", blue["soundSource"])
        self.assertEqual("while-speaking-during-action", blue["duration"])
        self.assertFalse(blue["visualEffect"])
        self.assertFalse(blue["guaranteesDeception"])

        red = leaves["red"]
        self.assertEqual(15, red["climbingSpeedFeet"])
        self.assertEqual("natural-climber", red["traits"][0]["id"])
        self.assertIn("spider-climb", red["traits"][0]["doesNotGrant"])

        white = leaves["white"]
        self.assertEqual(15, white["swimmingSpeedFeet"])
        self.assertEqual({"icewalker", "coldwater-swimmer"},
                         {t["id"] for t in white["traits"]})
        icewalker = next(t for t in white["traits"] if t["id"] == "icewalker")
        self.assertEqual("ordinary-slippery-ice-alone",
                         icewalker["slipperyIceSaveExemption"]["trigger"])
        self.assertEqual("dexterity",
                         icewalker["slipperyIceSaveExemption"]["savingThrow"])
        self.assertEqual("prone", icewalker["slipperyIceSaveExemption"]["effect"])
        self.assertTrue(icewalker["doesNotNegateOtherHazards"])
        swimmer = next(t for t in white["traits"] if t["id"] == "coldwater-swimmer")
        self.assertEqual(15, swimmer["speedFeet"])
        self.assertFalse(swimmer["grantsWaterBreathing"])

        # The staged SRD template includes unresolved Metallic variants and
        # must not be deleted merely because Chromatic leaves are selected.
        historical = WORKING / "species/subspecies/dragonborn/unnamed-1a909417769c0dff.json"
        self.assertTrue(historical.is_file())
        row = next(r for r in self.entries
                   if r["nativeId"] == "subspecies.dragonborn.unnamed-1a909417769c0dff")
        self.assertEqual("single-candidate", row["status"])
        self.assertIn("working/species/subspecies/dragonborn/", row["path"])

    def test_large_entities_have_full_candidate_records_and_merged_lore(self):
        for path, expected in [
            # Wizard is now an approved native definition, not a staged candidate.
            ("classes/subclasses/monk/open-hand.json", 2),
            ("spells/acid-splash.json", 2),
            # Dragonborn base was promoted to native; pinned source preserved in reference.

        ]:
            d = load(WORKING / path)
            self.assertEqual(expected, len(d["candidateDefinitions"]), path)
            self.assertTrue(all(v["content"] for v in d["candidateDefinitions"]))
            self.assertNotIn("fluff", path)
        wizard = load(WORKING / "classes/base/wizard.json")
        self.assertEqual("retained-native-definition", wizard["baselineStatus"])
        self.assertNotIn("candidateDefinitions", wizard)
        self.assertEqual(20, len(wizard["definition"]["progression"]))
        aboleth = load(WORKING / "bestiary/aboleth.json")
        self.assertEqual("retained-native-definition", aboleth["baselineStatus"])
        self.assertIn("lore", aboleth["definition"])
        self.assertFalse(list(WORKING.rglob("fluff-*.json")))

    def test_no_runtime_source_specific_switch_and_no_leftover_bootstrap(self):
        for f in [
            "tools/_stage_native_srd_once.py",
            "tools/_finalize_native_srd_once.py",
            ".github/workflows/stage-native-srd-baseline.yml",
            ".github/workflows/finalize-native-srd-baseline.yml",
        ]:
            self.assertFalse((ROOT / f).exists(), f)
        self.assertEqual("native-coverage-staged; pinned SRD evidence retained; approved reconciliations promoted individually",
                         self.coverage["status"])


if __name__ == "__main__":
    unittest.main()
