"""Pillar 2 characteristic identity and compatibility evidence tests.

These tests intentionally do NOT authorize automatic trait equivalence,
effect stacking, speed replacement, vision replacement, or earned grants.
"""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_identity_compatibility_audit import (  # noqa: E402
    audit_characteristic_identity,
)
from character_origins_manifestation_inventory import (  # noqa: E402
    draft_manifestation_profile,
)
from character_origins_parameter_inheritance import read_blueprint  # noqa: E402
from character_origins_source_inventory import read_catalog  # noqa: E402


def direct(key, selected):
    return {"contributorId": key, "selectedSourceId": selected}


def find(report, kind):
    return [row for row in report["findings"] if row["kind"] == kind]


class Pillar2IdentityCompatibilityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        blueprint = read_blueprint()
        cls.blueprints = {blueprint["definitionOccurrenceId"]: blueprint}

    def audit(self, *contributors, catalog=None, registry=None,
              blueprints=None):
        return audit_characteristic_identity(
            list(contributors),
            self.catalog if catalog is None else catalog,
            self.contracts,
            self.blueprints if blueprints is None else blueprints,
            registry=registry,
        )

    def test_all_selectable_native_lineages_are_auditable(self):
        for sid, entry in self.catalog.items():
            if entry.get("selectable") is not True:
                continue
            with self.subTest(selected=sid):
                r = self.audit(direct("one", sid))
                self.assertEqual(
                    "characteristic-identity-evidence-only", r["status"]
                )
                self.assertFalse(r["effectsEvaluated"])
                self.assertFalse(r["duplicatesMerged"])
                self.assertIsInstance(r["findings"], list)

    def test_human_size_choice_is_not_mistaken_for_conflicting_sources(self):
        result = self.audit(direct("human", "species.human"))
        alternatives = find(result, "authored-intrinsic-choice-alternatives")
        self.assertEqual(1, len(alternatives))
        self.assertEqual("creature.size",
                         alternatives[0]["characteristicDimension"])
        self.assertEqual(["small", "medium"], alternatives[0]["offeredValues"])
        self.assertFalse(find(result, "competing-intrinsic-values"))

    def test_wood_elf_records_competing_speed_without_a_replacement_rule(self):
        result = self.audit(direct("wood", "ancestry.elf.wood"))
        flags = find(result, "competing-intrinsic-values")
        speed = next(x for x in flags
                     if x["characteristicDimension"] == "movement.walk.speed")
        self.assertEqual([30, 35], speed["offeredValues"])
        self.assertEqual(
            "requires-overlap-replacement-or-selection-review",
            speed["disposition"],
        )
        self.assertFalse(result["duplicatesMerged"])

    def test_red_dragonborn_root_and_trait_climb_are_one_overlap_question(self):
        result = self.audit(direct("red", "ancestry.dragonborn.red"))
        matches = find(result, "trait-and-intrinsic-movement-overlap")
        self.assertEqual(1, len(matches))
        self.assertEqual(
            "movement.climb.speed",
            matches[0]["characteristicDimension"],
        )
        self.assertTrue(matches[0]["matching"])
        self.assertEqual(15, matches[0]["traitSpeed"])
        self.assertEqual(15, matches[0]["intrinsicSpeed"])
        self.assertEqual(
            "same-source-mirror-not-double-grant", matches[0]["disposition"]
        )

    def test_mirrored_movement_mismatch_is_not_silently_corrected(self):
        catalog = copy.deepcopy(self.catalog)
        catalog["ancestry.dragonborn.red"]["climbingSpeedFeet"] = 20
        result = self.audit(
            direct("red", "ancestry.dragonborn.red"), catalog=catalog
        )
        overlap = find(result, "trait-and-intrinsic-movement-overlap")
        self.assertEqual(1, len(overlap))
        self.assertFalse(overlap[0]["matching"])
        self.assertEqual(20, overlap[0]["intrinsicSpeed"])
        self.assertEqual(
            "conflicting-authored-values-require-review",
            overlap[0]["disposition"],
        )

    def test_drow_senses_are_distinct_relationship_not_automatic_upgrade(self):
        result = self.audit(direct("drow", "ancestry.elf.drow"))
        relation = find(result, "low-light-and-darkvision-relationship")
        self.assertEqual(1, len(relation))
        self.assertIn(
            "species.elf#trait:low-light-vision",
            relation[0]["sourceOccurrences"],
        )
        self.assertIn(
            "ancestry.elf.drow#intrinsic:darkvisionFeet",
            relation[0]["sourceOccurrences"],
        )
        self.assertEqual(
            "coexist-replace-or-interact-not-decided",
            relation[0]["disposition"],
        )

    def test_same_rule_reference_is_not_a_whole_trait_merge(self):
        result = self.audit(
            direct("elf", "species.elf"),
            direct("orc", "species.orc"),
        )
        refs = [x for x in find(result, "shared-explicit-rule-reference")
                if x["ruleRef"] == "rule.low-light-vision"]
        self.assertEqual(1, len(refs))
        self.assertEqual(
            {"species.elf#trait:low-light-vision",
             "species.orc#trait:low-light-vision"},
            set(refs[0]["sourceOccurrences"]),
        )
        self.assertEqual(
            "candidate-shared-behavior-not-automatic-merge",
            refs[0]["disposition"],
        )
        self.assertFalse(result["duplicatesMerged"])

    def test_orc_goliath_powerful_build_shares_component_not_whole_trait(self):
        result = self.audit(
            direct("orc", "species.orc"),
            direct("goliath", "ancestry.goliath.cloud"),
        )
        duplicates = [x for x in
                      find(result, "same-local-trait-id-distinct-source-occurrences")
                      if x["localTraitId"] == "powerful-build"]
        self.assertEqual(1, len(duplicates))
        self.assertEqual("different-authored-definitions",
                         duplicates[0]["definitionRelationship"])
        shared = [x for x in find(result, "shared-mechanical-components")
                  if "effectiveSizeCategoriesHigherForCarryingCapacity"
                  in x["sharedFields"]]
        self.assertTrue(any(
            {"species.orc#trait:powerful-build",
             "species.goliath#trait:powerful-build"}
            == set(item["sourceOccurrences"])
            for item in shared
        ))

    def test_same_spell_is_not_same_entitlement(self):
        result = self.audit(
            direct("drow", "ancestry.elf.drow"),
            direct("infernal", "ancestry.tiefling.infernal"),
        )
        darkness = [x for x in
                    find(result, "same-spell-distinct-trait-grants")
                    if x["spellRef"] == "darkness"]
        self.assertEqual(1, len(darkness))
        self.assertEqual(
            "different-authored-definitions",
            darkness[0]["definitionRelationship"],
        )
        self.assertEqual(
            "spell-identity-does-not-equate-entitlements",
            darkness[0]["disposition"],
        )

    def test_two_identical_dragonborn_contributors_do_not_double_grant(self):
        result = self.audit(
            direct("one", "ancestry.dragonborn.red"),
            direct("two", "ancestry.dragonborn.red"),
        )
        breath = [x for x in find(result, "repeated-source-trait-offer")
                  if x["occurrenceId"] ==
                  "species.dragonborn#trait:breath-weapon"]
        self.assertEqual(1, len(breath))
        self.assertEqual("same-parameters",
                         breath[0]["configurationRelationship"])
        self.assertEqual(["one", "two"],
                         breath[0]["contributorOccurrences"])
        self.assertFalse(result["duplicatesMerged"])

    def test_different_breath_parameters_do_not_invent_separate_mechanics(self):
        result = self.audit(
            direct("red", "ancestry.dragonborn.red"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        )
        breath = [x for x in find(result, "repeated-source-trait-offer")
                  if x["occurrenceId"] ==
                  "species.dragonborn#trait:breath-weapon"]
        self.assertEqual(1, len(breath))
        self.assertEqual(
            "different-parameter-configurations",
            breath[0]["configurationRelationship"],
        )
        matching_ids = [x for x in result["traitOccurrences"]
                        if x["identity"] ==
                        "species.dragonborn#trait:breath-weapon"]
        self.assertEqual(2, len(matching_ids))
        self.assertNotEqual(
            matching_ids[0]["parameterConfiguration"],
            matching_ids[1]["parameterConfiguration"],
        )

    def test_same_intrinsic_value_is_not_additive(self):
        result = self.audit(
            direct("one", "species.human"),
            direct("two", "species.orc"),
        )
        same = [x for x in
                find(result, "same-intrinsic-value-from-multiple-occurrences")
                if x["characteristicDimension"] == "movement.walk.speed"]
        self.assertEqual(1, len(same))
        self.assertEqual(30, same[0]["authoredValue"])
        self.assertEqual(
            "same-value-does-not-mean-additive-grants", same[0]["disposition"]
        )

    def test_nested_profile_is_reviewed_without_restoring_grandparents(self):
        original = [
            direct("red", "ancestry.dragonborn.red"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        ]
        profile = draft_manifestation_profile(
            "mixed-parent", original, [
                {"kind": "intrinsic", "identity": "creature.type",
                 "value": "dragon"},
                {"kind": "trait",
                 "identity": "species.dragonborn#trait:breath-weapon",
                 "parameters": {
                     "damageType": "fire",
                     "area": {"shape": "cone", "lengthFeet": 15},
                 }},
            ], {}, self.blueprints, self.catalog, self.contracts,
        )
        child = self.audit(
            {"contributorId": "mixed", "profileId": "mixed-parent"},
            direct("human", "species.human"),
            registry={"mixed-parent": profile},
        )
        breath = next(x for x in child["traitOccurrences"]
                      if x["identity"] ==
                      "species.dragonborn#trait:breath-weapon")
        self.assertEqual("fire",
                         breath["parameterConfiguration"]["damageType"])
        self.assertEqual(
            {"shape": "cone", "lengthFeet": 15},
            breath["parameterConfiguration"]["area"],
        )
        self.assertFalse(any(
            t["parameterConfiguration"].get("damageType") == "lightning"
            for t in child["traitOccurrences"]
        ))

    def test_unknown_mechanical_fields_preserve_explicit_review_evidence(self):
        altered = copy.deepcopy(self.catalog)
        altered["species.human"]["futureIntrinsic"] = {
            "something": "unknown"
        }
        result = self.audit(
            direct("human", "species.human"), catalog=altered
        )
        flagged = [x for x in result["unresolvedSourceEvidence"]
                   if x.get("field") == "futureIntrinsic"]
        self.assertEqual(1, len(flagged))
        self.assertEqual(
            {"something": "unknown"}, flagged[0]["authoredValue"]
        )

    def test_source_catalog_and_blueprints_are_never_mutated(self):
        catalog = copy.deepcopy(self.catalog)
        blueprints = copy.deepcopy(self.blueprints)
        original_catalog, original_blueprints = (
            copy.deepcopy(catalog), copy.deepcopy(blueprints)
        )
        contributors = [
            direct("red", "ancestry.dragonborn.red"),
            direct("bronze", "ancestry.dragonborn.bronze"),
        ]
        a = self.audit(*contributors, catalog=catalog, blueprints=blueprints)
        b = self.audit(*contributors, catalog=catalog, blueprints=blueprints)
        self.assertEqual(a, b)
        a["traitOccurrences"][0]["parameterConfiguration"]["fake"] = 1
        self.assertEqual(original_catalog, catalog)
        self.assertEqual(original_blueprints, blueprints)


if __name__ == "__main__":
    unittest.main()
