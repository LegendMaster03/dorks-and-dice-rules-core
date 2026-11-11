"""Content-level consistency checks for Character Origins Pillars 1 and 3.

The rules are authoritative JSON under native/. These checks deliberately do
not require the still-unimplemented character builder or choose a resolution
for independently designed Competency grants.
"""
import json
import re
import unittest
from pathlib import Path


NATIVE = (Path(__file__).resolve().parents[1]
          / "data/rulesets/first-party-001/native")


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def records(folder):
    result = {}
    for path in (NATIVE / folder).rglob("*.json"):
        record = load(path)
        identifier = record["id"]
        if identifier in result:
            raise AssertionError(f"Duplicate ID {identifier}: {path}")
        result[identifier] = record
    return result


def canonical_reference_strings(value):
    """Visit canonical reference strings in arbitrarily nested JSON data.

    Restrict matching to complete identifiers. A prose description that
    mentions a Skill or Competency is not a catalog reference by itself.
    """
    if isinstance(value, str):
        if re.fullmatch(r"(?:skill|competency)\.[a-z0-9][a-z0-9._-]*", value):
            yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from canonical_reference_strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from canonical_reference_strings(child)


def parent_chain(record_id, definitions):
    """Return selected record plus unique ancestors; fail on missing/cycle."""
    seen = set()
    chain = []
    current = record_id
    while current is not None:
        if current in seen:
            raise AssertionError(f"Origin parent cycle at {current}")
        if current not in definitions:
            raise AssertionError(f"Missing parent {current} from {record_id}")
        seen.add(current)
        chain.append(current)
        current = definitions[current].get("parent")
    return chain


class CharacterOriginsCrossPillarConsistencyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.species = records("species/base")
        cls.ancestries = records("species/ancestry")
        cls.upbringings = records("upbringings")
        cls.backgrounds = records("backgrounds")
        cls.skills = {r["id"] for r in load(NATIVE / "skills.json")["skills"]}
        cls.competencies = {
            r["id"] for r in load(NATIVE / "competencies.json")["competencies"]
        }
        cls.selection = load(
            NATIVE / "rules/species-subspecies-upbringing.json"
        )

    def test_one_effective_upbringing_and_default_suggestion(self):
        policy = self.selection["upbringings"]
        self.assertEqual(1, policy["selectedCount"])
        self.assertEqual("defaultUpbringing", policy["defaultField"])
        self.assertFalse(policy["speciesRestrictionsByDefault"])
        self.assertFalse(
            self.selection["originInheritance"]["defaultIsAdditionalSelection"])
        all_species = self.species | self.ancestries
        for identifier, record in all_species.items():
            if "defaultUpbringing" not in record:
                continue
            with self.subTest(origin=identifier):
                target = record["defaultUpbringing"]
                self.assertIn(target, self.upbringings)
                # A suggestion must be an actual player-selectable package.
                # Making an inherited parent nonselectable requires updating
                # its Species default, not silently awarding the parent.
                self.assertTrue(self.upbringings[target]["selectable"])

    def test_nonselectable_parents_cannot_be_selected_directly(self):
        """Only child Upbringings select the inherited Elf/Gnome package."""
        children = {
            "upbringing.elf.general": ("upbringing.elf.drow",
                                       "upbringing.elf.high",
                                       "upbringing.elf.wood"),
            "upbringing.gnome.general": ("upbringing.gnome.forest",
                                         "upbringing.gnome.rock"),
        }
        for parent_id, child_ids in children.items():
            with self.subTest(parent=parent_id):
                self.assertFalse(self.upbringings[parent_id]["selectable"])
                for child_id in child_ids:
                    child = self.upbringings[child_id]
                    self.assertTrue(child["selectable"])
                    self.assertEqual(parent_id, child["parent"])
                    chain = parent_chain(child_id, self.upbringings)
                    self.assertEqual([child_id, parent_id], chain)

        for species_id in ("species.elf", "species.gnome"):
            self.assertNotIn("defaultUpbringing", self.species[species_id])
        for ancestry_id in ("ancestry.elf.drow", "ancestry.elf.high",
                            "ancestry.elf.wood", "ancestry.gnome.forest",
                            "ancestry.gnome.rock"):
            with self.subTest(ancestry=ancestry_id):
                ancestry = self.ancestries[ancestry_id]
                self.assertTrue(
                    self.upbringings[ancestry["defaultUpbringing"]]
                    ["selectable"])

    def test_parent_upbringings_default_to_inheritance_only(self):
        policy = self.selection["upbringings"]["parentSelectability"]
        self.assertEqual("inheritance-only", policy["default"])
        self.assertEqual("allowParentSelection",
                         policy["directSelectionExceptionField"])
        parent_ids = {
            source["parent"] for source in self.upbringings.values()
            if source.get("parent") is not None
        }
        self.assertTrue(parent_ids)
        for parent_id in parent_ids:
            with self.subTest(parent=parent_id):
                self.assertIn(parent_id, self.upbringings)
                parent = self.upbringings[parent_id]
                if parent.get("selectable", False):
                    self.assertIs(parent.get("allowParentSelection"), True)
                else:
                    self.assertNotEqual(parent.get("allowParentSelection"), True)
        for identifier, record in self.upbringings.items():
            if identifier not in parent_ids:
                with self.subTest(leaf=identifier):
                    self.assertFalse(record.get("allowParentSelection", False))

    def test_duplicate_proficiency_policies_are_explicit_and_distinct(self):
        training = self.selection["duplicateTraining"]
        for category, kind in (("skillProficiency", "skill"),
                               ("toolProficiency", "tool")):
            policy = training[category]
            with self.subTest(kind=kind):
                self.assertEqual("choose-another-same-kind",
                                 policy["duplicateResolution"])
                self.assertEqual(kind, policy["replacementKind"])
                self.assertTrue(policy["authorizedChoiceRequired"])
        self.assertEqual("single-effective-boolean",
                         training["weaponAndOtherEquipment"]["duplicateResolution"])
        self.assertEqual("retain-without-automatic-replacement",
                         training["language"]["duplicateResolution"])
        self.assertEqual("rule.competency-duplicate-proficiency",
                         training["competencyProficiency"]["ruleId"])
        policy = load(NATIVE / "rules/competency-duplicate-proficiency.json")
        self.assertEqual(training["competencyProficiency"]["ruleId"],
                         policy["id"])
        self.assertEqual("add-rank", policy["defaultResolution"])
        self.assertEqual(["add-rank", "alternate-proficiency"],
                         policy["supportedResolutions"])
        self.assertEqual(set(policy["supportedResolutions"]),
                         set(policy["resolutions"]))
        self.assertEqual("all-authorized-character-training-sources",
                         policy["scope"])
        optional = policy["optionalRule"]
        self.assertEqual("optional-rule.competency-duplicate-proficiency",
                         optional["id"])
        self.assertEqual("experimental-playtest", optional["status"])
        self.assertEqual("competencyDuplicateProficiencyResolution",
                         optional["settingKey"])
        self.assertEqual("campaign-or-playtest-session",
                         optional["selectionScope"])
        self.assertEqual(policy["defaultResolution"],
                         optional["defaultValue"])
        self.assertEqual(policy["supportedResolutions"],
                         [entry["value"] for entry in optional["options"]])
        for option in optional["options"]:
            with self.subTest(option=option["value"]):
                self.assertTrue(option["label"].strip())
                self.assertTrue(option["description"].strip())
        feedback = policy["playtestFeedback"]
        self.assertEqual(optional["settingKey"],
                         feedback["selectedRuleField"])
        self.assertIn("selectedResolution", feedback["minimumFields"])
        self.assertIn("competencyAndSourceOccurrences",
                      feedback["minimumFields"])
        self.assertIn("resolvedRankOrAlternativeProficiency",
                      feedback["minimumFields"])

    def test_parent_graphs_resolve_without_cycles(self):
        all_species = self.species | self.ancestries
        for identifier, record in self.ancestries.items():
            with self.subTest(ancestry=identifier):
                chain = parent_chain(identifier, all_species)
                self.assertIn(chain[-1], self.species)
                self.assertEqual("ancestry", record["kind"])
        for identifier, record in self.upbringings.items():
            with self.subTest(upbringing=identifier):
                chain = parent_chain(identifier, self.upbringings)
                self.assertEqual(len(chain), len(set(chain)))
                self.assertEqual("upbringing", record["kind"])
        for identifier, record in self.backgrounds.items():
            with self.subTest(background=identifier):
                self.assertEqual("background", record["kind"])
                self.assertNotIn("parent", record)

    def test_upbringing_parent_benefits_are_reused_not_new_selections(self):
        # The inheritance chain is identity-based, not a requirement to
        # select or mechanically award two independent Upbringings.
        for child_id, record in self.upbringings.items():
            if "parent" not in record:
                continue
            with self.subTest(child=child_id):
                chain = parent_chain(child_id, self.upbringings)
                self.assertEqual(record["parent"], chain[1])
                self.assertEqual(
                    len(chain),
                    len(set(chain)),
                    "Every inherited source contributes at most once",
                )

    def test_all_skill_and_competency_references_resolve(self):
        # Check every nested string, not only the fixed fields or named
        # chooseOne/chooseFrom arrays. Source content may introduce new
        # option or prerequisite structures without changing this test.
        for source in (
            self.species | self.ancestries | self.upbringings | self.backgrounds
        ).values():
            for value in canonical_reference_strings(source):
                if value.startswith("skill."):
                    with self.subTest(source=source["id"], ref=value):
                        self.assertIn(value, self.skills)
                elif value.startswith("competency."):
                    with self.subTest(source=source["id"], ref=value):
                        self.assertIn(value, self.competencies)

    def test_retired_product_categories_are_not_native_competencies(self):
        # Manufacturing methods remain; the product category is resolved by
        # its applicable recipe/activity. No existing-character rank migration.
        for retired in ("bowmaking", "shipmaking", "trapmaking",
                        "armorsmithing", "weaponsmithing", "blacksmithing"):
            with self.subTest(retired=retired):
                self.assertNotIn("competency." + retired, self.competencies)


    def test_selected_upbringing_background_fixed_skill_overlap_review_gate(self):
        """Flag newly introduced unavoidable fixed-Skill overlaps for review.

        Optional Skill choices are excluded: an overlap selected by a player
        is not equivalent to a duplicate imposed by two fixed source grants.
        The approved rule replaces a duplicate fixed Skill proficiency with
        another Skill chosen by the character. This source-level test only
        identifies the overlap and does not itself authorize a choice.
        """
        def fixed_skills(record):
            return {
                benefit["skill"]
                for benefit in record.get("benefits", [])
                if benefit.get("grantType") == "skill-training"
                and isinstance(benefit.get("skill"), str)
            }

        overlaps = set()
        for upbringing_id, upbringing in self.upbringings.items():
            if not upbringing.get("selectable", True):
                continue
            inherited = set()
            for ancestor_id in parent_chain(upbringing_id, self.upbringings):
                inherited.update(fixed_skills(self.upbringings[ancestor_id]))
            for background_id, background in self.backgrounds.items():
                if not background.get("selectable", True):
                    continue
                for skill_id in inherited & fixed_skills(background):
                    overlaps.add((upbringing_id, background_id, skill_id))

        self.assertEqual(
            {("upbringing.giant-foundling", "background.soldier",
              "skill.intimidation")},
            overlaps,
            "New unavoidable Origin Skill overlap requires explicit "
            "application of the approved same-kind duplicate rule.",
        )

    def test_mixed_benefit_choices_preserve_one_total_outcome(self):
        """The five heterogeneous choices have one winner per entitlement.

        These are content-level checks. Actual entitlement and nested
        equipment source materialization remain implementation work.
        """
        choices = (
            ("background.entertainer", "entertainer-performance-method"),
            ("background.guild-merchant", "guild-merchant-professional-choice"),
            ("background.spy", "spy-tradecraft-competency"),
            ("upbringing.aristocratic", "aristocratic-formative-competency"),
            ("upbringing.street-raised",
             "street-raised-community-competency"),
        )
        sources = self.backgrounds | self.upbringings
        for source_id, benefit_id in choices:
            with self.subTest(source=source_id, benefit=benefit_id):
                source = sources[source_id]
                benefit = next(
                    b for b in source["benefits"] if b["id"] == benefit_id)
                self.assertEqual("benefit-choice", benefit["grantType"])
                self.assertEqual(1, benefit["choiceCount"])
                options = benefit["options"]
                option_ids = [o["id"] for o in options]
                self.assertGreater(len(options), 1)
                self.assertEqual(len(option_ids), len(set(option_ids)))
                for option in options:
                    self.assertTrue(option.get("benefits"))
                    grants = option["benefits"]
                    competency = any(
                        b.get("grantType") == "competency-training"
                        for b in grants)
                    qualification = any(
                        b.get("grantType") == "qualification-training"
                        for b in grants)
                    self.assertFalse(competency and qualification)
                    if competency:
                        self.assertNotIn("equipmentProficiencyGrants", option)
                    if qualification:
                        self.assertIn("equipmentProficiencyGrants", option)
                # The current five choices have no unconditional equipment
                # entitlement at source level. Child options are conditional.
                self.assertNotIn("equipmentProficiencyGrants", source)

    def test_all_direct_origin_combinations_have_a_valid_selection_path(self):
        """Check the complete selectable Origin product without executing it.

        Defaults suggest one selectable Upbringing; they are never another
        selected cultural package. A chosen Upbringing may be cross-cultural
        and inherits its parent benefits once.
        """
        self.assertEqual(1, self.selection["upbringings"]["selectedCount"])
        self.assertFalse(
            self.selection["upbringings"]["speciesRestrictionsByDefault"])
        self.assertFalse(
            self.selection["originInheritance"]["defaultIsAdditionalSelection"])
        lineage_records = self.species | self.ancestries
        playable_lineages = [
            r for r in lineage_records.values() if r.get("selectable", True)
        ]
        selectable_upbringings = [
            r for r in self.upbringings.values() if r.get("selectable", True)
        ]
        backgrounds = [
            r for r in self.backgrounds.values() if r.get("selectable", True)
        ]
        self.assertTrue(playable_lineages)
        self.assertTrue(selectable_upbringings)
        self.assertTrue(backgrounds)
        for lineage in playable_lineages:
            with self.subTest(lineage=lineage["id"]):
                lineage_chain = parent_chain(lineage["id"], lineage_records)
                self.assertIn(lineage_chain[-1], self.species)
                for upbringing in selectable_upbringings:
                    chain = parent_chain(upbringing["id"], self.upbringings)
                    self.assertEqual(len(chain), len(set(chain)))
                    # This is an explicitly selected single Upbringing, even
                    # when its inherited parent is not player-selectable.
                    self.assertTrue(self.upbringings[chain[0]]["selectable"])
                    for background in backgrounds:
                        self.assertEqual("background", background["kind"])
                        self.assertNotIn(background["id"], chain)

if __name__ == "__main__":
    unittest.main()
