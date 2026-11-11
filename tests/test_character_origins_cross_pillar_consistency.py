"""Content-level consistency checks for Character Origins Pillars 1 and 3.

The rules are authoritative JSON under native/. These checks deliberately do
not require the still-unimplemented character builder or choose a resolution
for independently designed Competency grants.
"""
import json
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


def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)


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
        # Do not freeze a catalog size; source content is continuously edited.
        for source in (
            self.species | self.ancestries | self.upbringings | self.backgrounds
        ).values():
            for node in walk(source):
                for value in node.values():
                    if not isinstance(value, str):
                        continue
                    if value.startswith("skill."):
                        with self.subTest(source=source["id"], ref=value):
                            self.assertIn(value, self.skills)
                    elif value.startswith("competency."):
                        with self.subTest(source=source["id"], ref=value):
                            self.assertIn(value, self.competencies)
                # Lists are handled separately, including selected training.
                for key in ("chooseOne", "chooseFrom"):
                    for value in node.get(key, []) if isinstance(
                        node.get(key), list
                    ) else []:
                        if isinstance(value, str) and value.startswith("skill."):
                            with self.subTest(source=source["id"], ref=value):
                                self.assertIn(value, self.skills)
                        if (isinstance(value, str)
                                and value.startswith("competency.")):
                            with self.subTest(source=source["id"], ref=value):
                                self.assertIn(value, self.competencies)

    def test_retired_product_categories_are_not_native_competencies(self):
        # Manufacturing methods remain; the product category is resolved by
        # its applicable recipe/activity. No existing-character rank migration.
        for retired in ("bowmaking", "shipmaking", "trapmaking",
                        "armorsmithing", "weaponsmithing", "blacksmithing"):
            with self.subTest(retired=retired):
                self.assertNotIn("competency." + retired, self.competencies)


if __name__ == "__main__":
    unittest.main()
