import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from audit_srd_native_references import audit
from report_srd_promotion import report


class NativeReferenceAuditTests(unittest.TestCase):
    def test_contextual_and_edition_local_candidates(self):
        rows = [
            {"id": "class-feature.fighter.extra-attack.5", "kind": "class-feature",
             "name": "Extra Attack", "className": "Fighter", "level": 5},
            {"id": "class-feature.fighter.extra-attack.11", "kind": "class-feature",
             "name": "Extra Attack", "className": "Fighter", "level": 11},
            {"id": "spell.light", "kind": "spell", "name": "Light"},
            {"id": "feat.magic-initiate", "kind": "feat", "name": "Magic Initiate",
             "_versions": [{"name": "Magic Initiate; Cleric", "source": "SRD52"}]},
            {"id": "rule.test", "kind": "rule", "name": "Test",
             "entries": ["{@classFeature Extra Attack|Fighter|SRD52|11} "
                         "{@spell Light|SRD51} {@spell Light|SRD52} "
                         "{@spell Missing|SRD52} {@feat Magic Initiate; Cleric|SRD52} "
                         "{@book Source|SRD52} {@dice 1d6}"],
             "catalogReferences": ["spell.light"]},
        ]
        before = copy.deepcopy(rows)
        result = audit(rows, "SRD52")
        self.assertEqual(before, rows)
        refs = result["occurrences"]
        self.assertEqual(6, len(refs))
        self.assertEqual(["class-feature.fighter.extra-attack.11"], refs[0]["targets"])
        self.assertEqual("foreign-source", refs[1]["status"])
        self.assertTrue(refs[2]["alreadyExplicit"])
        self.assertEqual("missing", refs[3]["status"])
        self.assertEqual("named-variant-candidate", refs[4]["status"])
        self.assertEqual("unsupported-tag", refs[5]["status"])

    def test_ambiguity_is_not_silently_resolved(self):
        rows = [
            {"id": "item.example", "kind": "item", "name": "Example"},
            {"id": "baseitem.example", "kind": "baseitem", "name": "Example"},
            {"id": "rule.test", "kind": "rule", "name": "Test",
             "entries": ["{@item Example}"]},
        ]
        result = audit(rows, "SRD51")["occurrences"][0]
        self.assertEqual("ambiguous", result["status"])
        self.assertEqual(["baseitem.example", "item.example"], result["targets"])

    def test_coverage_distinguishes_merged_and_excluded_records(self):
        candidates = [
            {"candidateId": str(i), "proposedIdentity": str(i), "sourceKind": "race",
             "source": {"path": "races.json", "pointer": f"/race/{i}"},
             "payload": {"name": str(i)}}
            for i in range(5)
        ]
        rows = [{"id": "race.test", "kind": "race", "source": {
            "release": "srd-5.1", "structuredSource": candidates[0]["source"],
            "additionalStructuredSources": [candidates[1]["source"]]}}]
        decisions = [
            dict(candidates[2]["source"], disposition="not-promoted", reason="duplicate"),
            dict(candidates[3]["source"], disposition="not-promoted", field="age"),
            dict(candidates[4]["source"], disposition="deferred", reason="review"),
            dict(candidates[1]["source"], disposition="deferred", reason="old checkpoint"),
        ]
        statuses = [x["status"] for x in report("srd-5.1", candidates, rows, decisions)["occurrences"]]
        self.assertEqual(["explicit-source-pointer", "explicit-source-pointer", "documented-not-promoted",
                          "unreviewed", "documented-deferred"], statuses)

    def test_deity_pantheon_is_not_an_edition_tag(self):
        rows = [
            {"id": "deity.celtic.oghma", "kind": "deity", "name": "Oghma", "pantheon": "Celtic"},
            {"id": "rule.test", "kind": "rule", "name": "Test",
             "entries": ["{@deity Oghma|Celtic} {@deity Oghma|Forgotten Realms}"]},
        ]
        refs = audit(rows, "SRD51")["occurrences"]
        self.assertEqual(["deity.celtic.oghma"], refs[0]["targets"])
        self.assertEqual("missing", refs[1]["status"])

    def test_reference_aliases_respect_category_and_edition(self):
        rows = [
            {"id": "item-property.2h", "kind": "item-property", "name": "Two-Handed", "abbreviation": "2H"},
            {"id": "rule.weapon-mastery-properties", "kind": "rule", "name": "Mastery Properties",
             "aliases": ["Weapon Mastery Properties"]},
            {"id": "status.concentration", "kind": "status", "name": "Concentration"},
            {"id": "table.spell-scroll-costs", "kind": "table", "name": "Spell Scroll Costs"},
            {"id": "optional-feature.eldritch-mind", "kind": "optional-feature", "name": "Eldritch Mind"},
            {"id": "rule.test", "kind": "rule", "name": "Test",
             "entries": ["{@itemProperty 2H|SRD52|Two-Handed} "
                         "{@variantrule Weapon Mastery Properties|SRD52} "
                         "{@variantrule Concentration|SRD52} "
                         "{@table Scribing Spell Scrolls; Spell Scroll Costs|SRD52} "
                         "{@optionalfeature Eldritch Mind|SRD52} "
                         "{@itemProperty 2H|SRD51}"]},
        ]
        refs = audit(rows, "SRD52")["occurrences"]
        self.assertEqual(["item-property.2h", "rule.weapon-mastery-properties",
                          "status.concentration", "table.spell-scroll-costs",
                          "optional-feature.eldritch-mind"], [r["targets"][0] for r in refs[:5]])
        self.assertEqual("foreign-source", refs[5]["status"])

    def test_concrete_item_preferred_over_duplicate_template(self):
        rows = [
            {"id": "item.1-longsword", "kind": "item", "name": "+1 Longsword"},
            {"id": "magic-variant.1-longsword", "kind": "magic-variant", "name": "+1 Longsword"},
            {"id": "rule.test", "kind": "rule", "name": "Test",
             "entries": ["{@item +1 Longsword|SRD52}", "{@item +1 Longsword|SRD51}"]},
        ]
        refs = audit(rows, "SRD52")["occurrences"]
        self.assertEqual(["item.1-longsword"], refs[0]["targets"])
        self.assertEqual("foreign-source", refs[1]["status"])
