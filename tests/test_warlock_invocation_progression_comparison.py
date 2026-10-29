"""Preserve the owner's provisional 1-20 Warlock Invocation test curve."""
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class WarlockInvocationBand1ProvisionalProgressionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.model = load("data/reference/warlock-invocation-progression-band1-comparison.json")
        cls.sources = load("data/reference/3x/warlock-invocations-research-index.json")
        cls.levels = cls.model["levels"]

    def test_provisional_only_and_no_band_two(self):
        data = self.model
        self.assertEqual("owner-selected-provisional-testing-model-not-final-balance-approval", data["status"])
        self.assertEqual({"levelStart": 1, "levelEnd": 20, "band": "1", "beyondLevel20": "out-of-scope"}, data["scope"])
        self.assertTrue(data["pactAssumptions"]["nativeInvocationAttachesToOneConcretePact"])
        self.assertFalse(data["pactAssumptions"]["nativeFirstPactCostsInvocationSelection"])
        self.assertEqual("balance-counterfactual-only-not-current-testing-model",
                         data["deferredAlternatives"][0]["status"])

    def test_all_published_source_series_are_retained(self):
        self.assertEqual(list(range(1, 21)), [r["warlockLevel"] for r in self.levels])
        source = self.sources
        expected = [
            source["original3xWarlock"]["invocationsKnownPerLevel"],
            source["modernSRDComparativeFacts"]["invocationsKnownByClassLevel"]["srd51"],
            source["modernSRDComparativeFacts"]["invocationsKnownByClassLevel"]["srd52"],
        ]
        actual = [[r[key] for r in self.levels] for key in (
            "edition35Invocations",
            "edition5e2014Invocations",
            "edition55e2024TotalInvocationSelections",
        )]
        self.assertEqual(expected, actual)
        self.assertTrue(self.model["analysisUse"]["keepAllFourComparisonSeriesOnGraph"])

    def test_hybrid_formula_and_milestones(self):
        for r in self.levels:
            adjusted = r["edition55e2024TotalInvocationSelections"] - 1
            self.assertEqual(adjusted, r["edition55eOnePactAdjustedElectiveInvocations"])
            self.assertEqual(max(r["edition35Invocations"], adjusted),
                             r["provisionalHybridElectiveInvocations"])
        counts = [r["provisionalHybridElectiveInvocations"] for r in self.levels]
        self.assertEqual(1, counts[0])
        self.assertEqual(12, counts[-1])
        self.assertEqual(list(range(1, 13)),
                         [n for i, n in enumerate(counts) if n > (counts[i-1] if i else 0)])
        self.assertEqual([1, 2, 4, 5, 7, 9, 11, 13, 15, 16, 18, 20],
                         self.model["model"]["milestoneLevels"])
        self.assertEqual([5, 7, 9], [r["warlockLevel"] for r in self.levels
                                     if r["provisionalHybridElectiveInvocations"] > r["edition35Invocations"]])

    def test_counterfactual_not_substituted(self):
        self.assertEqual(1, self.model["model"]["levelOneElectiveInvocations"])
        self.assertEqual(0, self.model["deferredAlternatives"][0]["levelOneElectiveInvocations"])
        self.assertEqual("independently-granted-first-Pact", self.model["model"]["levelOnePact"])
        native = load("data/rulesets/first-party-001/working/classes/base/warlock.json")
        self.assertEqual("pending-native-reconciliation", native["baselineStatus"])


if __name__ == "__main__":
    unittest.main()
