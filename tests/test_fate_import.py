"""Verify that the native section catalog reproduces the licensed source."""
import json
import re
import unittest
from tools.import_fate_condensed import convert, TARGET


class FateImportTests(unittest.TestCase):
    def test_source_and_catalog_match(self):
        records = convert()
        self.assertEqual(records, json.loads(TARGET.read_text(encoding="utf-8")))
        ids = {r["id"] for r in records}
        self.assertTrue(all(r["parentId"] is None or r["parentId"] in ids for r in records))

    def test_structured_default_skills_actions_and_rating_ladder(self):
        sections = {r["id"]: r for r in convert()}
        structured = json.loads((TARGET.parent / "structured.json").read_text(encoding="utf-8"))
        known = sections.keys() | {r["id"] for r in structured}
        self.assertEqual(26, len(structured))
        self.assertEqual(19, sum(r["kind"] == "skill" for r in structured))
        self.assertEqual(4, sum(r["kind"] == "action" for r in structured))
        for record in structured:
            self.assertTrue(set(record["catalogReferences"]) <= known)
            origin = sections[record["catalogReferences"][0]]
            self.assertEqual(origin["source"], record["source"])
            if record["kind"] == "skill":
                self.assertIn("**" + record["name"] + ":** " + record["description"], origin["markdown"])
            if record["kind"] == "action":
                self.assertEqual(origin["markdown"], record["markdown"])
        # Check the complete maintained projections against the actual pinned source.
        by_id = {r["id"]: r for r in structured}
        skill_source = sections["section.getting-started.create-your-characters.skills.skill-list"]["markdown"]
        skills = [r for r in structured if r["kind"] == "skill"]
        self.assertEqual(re.findall(r"^\* +\*\*([^*]+)\*\*$", skill_source, re.M),
                         [r["name"] for r in skills])
        self.assertTrue(all(r["defaultRating"] == 0 for r in skills))
        actions = [r for r in structured if r["kind"] == "action"]
        self.assertEqual({"action.overcome", "action.create-an-advantage", "action.attack", "action.defend"},
                         {r["id"] for r in actions})
        self.assertEqual(["action.defend"], [r["id"] for r in actions if r["reactive"]])

        def cells(section_id):
            return re.findall(r"<td>\s*(.*?)\s*</td>", sections[section_id]["markdown"], re.S)[2:]

        ladder = cells("section.getting-started.create-your-characters.skills.the-adjective-ladder")
        self.assertEqual([{"rating": int(rating), "name": name} for rating, name in zip(ladder[::2], ladder[1::2])],
                         by_id["rule.adjective-ladder"]["ratings"])
        pyramid = sections["section.getting-started.create-your-characters.skills"]["markdown"]
        counts = {"One": 1, "Two": 2, "Three": 3, "Four": 4}
        allocations = [{"rating": int(rating), "count": counts[count]}
                       for count, rating in re.findall(r"^\* +(\w+) \w+\s*\(\+(\d+)\) skills?$", pyramid, re.M)]
        self.assertEqual(allocations, by_id["rule.starting-skill-pyramid"]["allocations"])
        self.assertEqual(0, by_id["rule.starting-skill-pyramid"]["remainingRating"])
        stress = cells("section.getting-started.create-your-characters.stress-and-consequences")
        thresholds = []
        for rating, boxes in zip(stress[::2], stress[1::2]):
            row = {"minimumRating": int(re.search(r"\(\+(\d+)\)", rating)[1]), "boxes": boxes.count("[1]")}
            if "second mild consequence slot" in boxes:
                row["additionalMildConsequence"] = True
            thresholds.append(row)
        capacity = by_id["rule.stress-capacity"]
        self.assertEqual(thresholds, capacity["thresholds"])
        self.assertEqual(1, capacity["boxValue"])
        self.assertEqual([{"id": "physical", "skillId": "skill.physique"},
                          {"id": "mental", "skillId": "skill.will"}], capacity["tracks"])
