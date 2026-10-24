"""Guard the complete Warlock SRD-only vertical-slice corpus, without approving native mechanics."""
import json
import unittest
from pathlib import Path

R = Path(__file__).resolve().parents[1]

def load(path):
    return json.loads((R / path).read_text(encoding="utf-8"))

class WarlockSRDSliceScopeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.inventory = load("data/reference/srd/warlock-srd-slice-inventory.json")
        cls.source = load("data/reference/srd/structured/optionalfeatures.json")["optionalfeature"]
        cls.warlock = load("data/reference/srd/structured/class/class-warlock.json")

    def test_imported_invocation_and_pact_corpus_is_exhaustive(self):
        entries = [x for x in self.source if set(x.get("featureType", [])) & {"EI", "PB"}]
        expected = sorted({x["name"] for x in entries})
        actual = sorted(x["name"] for x in self.inventory["options"])
        self.assertEqual(expected, actual)
        self.assertEqual(42, len(actual))
        self.assertEqual(42, self.inventory["uniqueSelectableNames"])
        for item in self.inventory["options"]:
            actual_sources = sorted(
                (x["source"], tuple(sorted(set(x["featureType"]) & {"EI", "PB"})))
                for x in entries if x["name"] == item["name"])
            recorded = sorted(
                (x["source"], tuple(sorted(x["sourceCategory"]))) for x in item["editions"])
            self.assertEqual(actual_sources, recorded, item["name"])

    def test_source_budget_not_falsely_conflated(self):
        by_source = {"SRD51": [], "SRD52": []}
        for x in self.source:
            if set(x.get("featureType", [])) & {"EI", "PB"}:
                by_source[x["source"]].append(x)
        self.assertEqual(35, len(by_source["SRD51"]))
        self.assertEqual(32, sum("EI" in x["featureType"] for x in by_source["SRD51"]))
        self.assertEqual(3, sum("PB" in x["featureType"] for x in by_source["SRD51"]))
        self.assertEqual(28, len(by_source["SRD52"]))
        self.assertEqual(28, sum("EI" in x["featureType"] for x in by_source["SRD52"]))
        self.assertEqual(3, sum(x["name"] in {"Pact of the Blade", "Pact of the Chain", "Pact of the Tome"} for x in by_source["SRD52"]))

    def test_real_patron_scope_is_fiend_only(self):
        patrons = {(x["source"], x["name"]) for x in self.warlock["subclass"]}
        self.assertEqual({("SRD51","The Fiend"), ("SRD52","Fiend Patron")}, patrons)
        self.assertEqual(["Fiend"], self.inventory["scope"]["subclasses"])
        self.assertNotIn("Pact of the Talisman", {x["name"] for x in self.inventory["options"]})

    def test_inventory_only_never_confused_with_native_approval(self):
        self.assertEqual("source-inventory-and-acceptance-scope-not-native-reconciliation", self.inventory["status"])
        self.assertEqual([1, 20], self.inventory["scope"]["band"])
        self.assertEqual(["SRD51", "SRD52"], self.inventory["scope"]["editions"])
        native = load("data/native/classes/base/warlock.json")
        self.assertEqual("pending-native-reconciliation", native["baselineStatus"])

if __name__ == "__main__":
    unittest.main()
