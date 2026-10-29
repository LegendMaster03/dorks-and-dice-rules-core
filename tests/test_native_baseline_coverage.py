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
            if path.parent == WORKING:
                catalog = load(path)
                matches = [x for v in catalog.values() if isinstance(v, list)
                           for x in v if isinstance(x, dict) and x.get("id") == cid]
                self.assertEqual(1, len(matches), cid)
                native = matches[0]
            else:
                native = load(path)
                self.assertEqual(cid, native["id"], relative)
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
                    self.assertEqual(30, len(native["conditionIds"]))
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

    def test_approved_skill_competency_and_condition_data_not_overwritten(self):
        skills = load(WORKING / "skills.json")["skills"]
        competencies = load(WORKING / "competencies.json")["competencies"]
        conditions = load(AUTHORITATIVE / "conditions.json")["conditions"]
        self.assertEqual(88, len(skills))
        self.assertEqual(33, len(competencies))
        self.assertEqual(30, len(conditions))
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
        species = {p.stem for p in (WORKING / "species/base").glob("*.json")}
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

    def test_large_entities_have_full_candidate_records_and_merged_lore(self):
        for path, expected in [
            # Wizard is now an approved native definition, not a staged candidate.
            ("classes/subclasses/monk/open-hand.json", 2),
            ("spells/acid-splash.json", 2),
            ("species/base/dragonborn.json", 2),

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
        self.assertEqual("native-coverage-staged; all source-marked entities present; decisions pending",
                         self.coverage["status"])


if __name__ == "__main__":
    unittest.main()
