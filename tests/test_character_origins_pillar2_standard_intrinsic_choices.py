"""Pillar 2: explicit intrinsic Origin choice is not automatic override policy."""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))

from character_origins_compound_traits import read_contracts  # noqa: E402
from character_origins_manifestation_inventory import (  # noqa: E402
    draft_manifestation_profile,
)
from character_origins_parameter_inheritance import (  # noqa: E402
    ParameterInheritanceError, read_blueprints,
)
from character_origins_source_inventory import read_catalog  # noqa: E402
from character_origins_standard_preset_readiness import (  # noqa: E402
    preview_explicit_standard_intrinsic_choices, propose_standard_preset,
)


class Pillar2StandardIntrinsicChoiceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = read_catalog()
        cls.contracts = read_contracts()
        cls.blueprints = read_blueprints()

    def base(self, selected, catalog=None):
        return propose_standard_preset(
            selected, self.catalog if catalog is None else catalog,
            self.contracts, self.blueprints,
        )

    def choose(self, selected, choices, catalog=None):
        return preview_explicit_standard_intrinsic_choices(
            selected, choices,
            self.catalog if catalog is None else catalog,
            self.contracts, self.blueprints,
        )

    def test_wood_elf_ancestor_descendant_evidence_both_retained(self):
        base = self.base("ancestry.elf.wood")
        speed = next(x for x in base["unresolvedOriginDecisions"]
                     if x["identity"] == "movement.walk.speed")
        self.assertEqual(
            "ancestor-descendant-authored-values",
            speed["lineageRelationship"],
        )
        self.assertEqual("not-approved", speed["defaultOverrideDecision"])
        self.assertEqual(
            [{"value": 30,
              "sourceOccurrences": [
                  "species.elf#intrinsic:walkingSpeedFeet",
              ]},
             {"value": 35,
              "sourceOccurrences": [
                  "ancestry.elf.wood#intrinsic:walkingSpeedFeet",
              ]}],
            speed["options"],
        )

    def test_explicit_wood_elf_35_choice_keeps_source_provenance(self):
        result = self.choose(
            "ancestry.elf.wood", {"movement.walk.speed": 35}
        )
        speed = [x for x in result["proposedSelections"]
                 if x["identity"] == "movement.walk.speed"]
        self.assertEqual(
            [{"kind": "intrinsic",
              "identity": "movement.walk.speed", "value": 35}],
            speed,
        )
        self.assertEqual(
            ["ancestry.elf.wood#intrinsic:walkingSpeedFeet"],
            result["explicitIntrinsicDecisions"][0][
                "contributedBySourceOccurrences"],
        )
        self.assertEqual(
            "explicit-preview-choice-not-default-policy",
            result["explicitIntrinsicDecisions"][0]["status"],
        )
        self.assertFalse(any(
            x.get("identity") == "movement.walk.speed"
            for x in result["unresolvedOriginDecisions"]
        ))
        self.assertIn("dependent-source-choice-not-selected",
                      {x["kind"] for x in result["unresolvedOriginDecisions"]})
        self.assertFalse(result["templateComplete"])

    def test_explicit_wood_elf_30_is_also_representable_not_new_default(self):
        result = self.choose("ancestry.elf.wood",
                             {"movement.walk.speed": 30})
        self.assertEqual(
            ["species.elf#intrinsic:walkingSpeedFeet"],
            result["explicitIntrinsicDecisions"][0][
                "contributedBySourceOccurrences"],
        )
        self.assertEqual(30, result["explicitIntrinsicDecisions"][0][
            "selectedValue"])
        self.assertEqual(
            "explicit-standard-origin-proposal-not-authorized",
            result["status"],
        )

    def test_human_size_choice_is_explicit_without_a_fake_override(self):
        for size in ("small", "medium"):
            with self.subTest(size=size):
                result = self.choose("species.human",
                                     {"creature.size": size})
                self.assertEqual(
                    ["species.human#intrinsic:sizeChoice"],
                    result["explicitIntrinsicDecisions"][0][
                        "contributedBySourceOccurrences"],
                )
                self.assertEqual(
                    size, next(
                        x["value"] for x in result["proposedSelections"]
                        if x.get("identity") == "creature.size"
                    ),
                )
                self.assertIn(
                    "dependent-trait-choices-not-selected",
                    {x["kind"] for x in result["unresolvedOriginDecisions"]},
                )

    def test_unsupported_value_and_unknown_dimension_are_rejected(self):
        for source, choices in (
            ("ancestry.elf.wood", {"movement.walk.speed": 50}),
            ("species.human", {"creature.size": "large"}),
            ("ancestry.elf.wood", {"creature.size": "medium"}),
            ("species.human", {"movement.walk.speed": 30}),
            ("ancestry.elf.wood", {"movement.walk.speed": True}),
        ):
            with self.subTest(source=source, choices=choices):
                with self.assertRaises(ParameterInheritanceError):
                    self.choose(source, choices)

    def test_no_choice_does_not_silently_resolve_a_source_conflict(self):
        source = self.base("ancestry.elf.wood")
        p = self.choose("ancestry.elf.wood", {})
        self.assertEqual(source["proposedSelections"],
                         p["proposedSelections"])
        self.assertEqual(source["unresolvedOriginDecisions"],
                         p["unresolvedOriginDecisions"])
        self.assertFalse(p["templateComplete"])
        self.assertEqual([], p["explicitIntrinsicDecisions"])

    def test_preview_can_feed_shared_profile_without_creating_grant(self):
        p = self.choose("ancestry.elf.wood",
                        {"movement.walk.speed": 35})
        drafted = draft_manifestation_profile(
            "example", [p["standardSource"]], p["proposedSelections"],
            {}, self.blueprints, self.catalog, self.contracts,
        )
        self.assertEqual("not-authorized", drafted["authority"])
        speed = next(x for x in drafted["manifestedCharacteristics"]
                     if x["identity"] == "movement.walk.speed")
        self.assertEqual(35, speed["value"])
        self.assertEqual(
            "ancestry.elf.wood",
            speed["contributedBy"][0]["selectedSourceId"],
        )
        # The original source pool still shows an unresolved 30/35 conflict;
        # a choice preview does not adopt a universal override policy.
        self.assertTrue(any(
            x.get("identity") == "movement.walk.speed"
            for x in drafted["reviewFlags"]
        ))

    def test_explicit_decisions_preserve_input_immutability(self):
        source = copy.deepcopy(self.catalog)
        contracts = copy.deepcopy(self.contracts)
        blueprints = copy.deepcopy(self.blueprints)
        choices = {"movement.walk.speed": 35}
        before = (copy.deepcopy(source), copy.deepcopy(contracts),
                  copy.deepcopy(blueprints), copy.deepcopy(choices))
        a = preview_explicit_standard_intrinsic_choices(
            "ancestry.elf.wood", choices, source, contracts, blueprints
        )
        b = preview_explicit_standard_intrinsic_choices(
            "ancestry.elf.wood", choices, source, contracts, blueprints
        )
        self.assertEqual(a, b)
        a["explicitIntrinsicDecisions"][0]["selectedValue"] = 999
        self.assertEqual(
            before, (source, contracts, blueprints, choices)
        )


if __name__ == "__main__":
    unittest.main()
