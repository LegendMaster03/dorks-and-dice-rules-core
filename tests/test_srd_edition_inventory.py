import unittest
from tools.inventory_srd_editions import inventory


class SrdEditionInventoryTests(unittest.TestCase):
    def test_reference_inventory_distinguishes_editions(self):
        result = inventory()
        self.assertGreater(result["fileCount"], 0)
        self.assertGreater(result["candidateCounts"]["srd-5.1"], 0)
        self.assertGreater(result["candidateCounts"]["srd-5.2.1"], 0)
        self.assertIn("reference-only", result["status"])
