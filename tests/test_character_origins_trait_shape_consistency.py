"""Behavior-preserving data-shape checks for native Species and Ancestries.

Pillar 2 will determine universal characteristic identities and inheritance.
These tests deliberately do not equate same-named traits from different
Species or require a currently unapproved canonical trait schema.
"""
import json
import unittest
from pathlib import Path


NATIVE = (Path(__file__).resolve().parents[1]
          / "data/rulesets/first-party-001/native")
SPECIES = NATIVE / "species"


def definitions():
    """Yield source files and native records, including inheritance-only nodes."""
    for directory in ("base", "ancestry"):
        for path in sorted((SPECIES / directory).rglob("*.json")):
            yield path, json.loads(path.read_text(encoding="utf-8"))


def resistance_grants(value, location="root"):
    """Find all damage resistance objects, including nested/temporary grants."""
    if isinstance(value, dict):
        for key, child in value.items():
            child_location = f"{location}.{key}"
            if key in ("damageResistance", "damageResistanceOverride"):
                grants = child if isinstance(child, list) else [child]
                for index, grant in enumerate(grants):
                    yield f"{child_location}[{index}]", grant
            yield from resistance_grants(child, child_location)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from resistance_grants(child, f"{location}[{index}]")


class CharacterOriginsTraitShapeConsistencyTests(unittest.TestCase):
    def test_resistance_uses_explicit_percent_and_type_or_scope(self):
        # A resistance to a chosen Dragonborn damage type uses damageTypeFrom;
        # a short-lived resistance to a triggering attack can use scope.
        # Distinct contexts and modifier stacking remain authoritative.
        for path, source in definitions():
            for location, grant in resistance_grants(source):
                with self.subTest(source=path.name, record=source["id"],
                                  location=location):
                    self.assertIsInstance(grant, dict)
                    self.assertIsInstance(grant.get("percent"), int)
                    self.assertGreaterEqual(grant["percent"], 0)
                    self.assertLessEqual(grant["percent"], 100)
                    self.assertTrue(
                        any(key in grant for key in
                            ("damageType", "damageTypeFrom", "scope")),
                        "Resistance needs a named damage type, bound type, "
                        "or a scoped triggering attack",
                    )

    def test_redundant_movement_speed_fields_do_not_disagree(self):
        # Some Dragonborn adaptations currently carry an ancestry-level
        # speed as well as a trait-level speed. Check equivalence without
        # treating these as two additive movement grants.
        fields = {
            "climbingSpeedFeet": "climb",
            "swimmingSpeedFeet": "swim",
            "burrowingSpeedFeet": "burrow",
        }
        for path, source in definitions():
            traits = source.get("traits", [])
            for field, mode in fields.items():
                if field not in source:
                    continue
                matching = [
                    trait for trait in traits
                    if trait.get("movementMode") == mode
                    and "speedFeet" in trait
                ]
                for trait in matching:
                    with self.subTest(record=source["id"], speed=field,
                                      trait=trait["id"]):
                        self.assertEqual(
                            source[field], trait["speedFeet"],
                            "Mirrored movement definitions must agree",
                        )

    def test_trait_ids_are_unique_within_each_record(self):
        # Identity is source-scoped at present. A shared ID between Species
        # does NOT yet imply equal mechanics or permission to stack effects.
        for path, source in definitions():
            ids = [trait["id"] for trait in source.get("traits", [])]
            with self.subTest(record=source["id"]):
                self.assertEqual(len(ids), len(set(ids)))


if __name__ == "__main__":
    unittest.main()
