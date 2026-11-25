"""Inventory structured SRD relationships without interpreting them as earned grants.

Preserve field paths, choice expressions, mode, and level context. Resolve only
literal edition-local identities; never flatten a choice into unconditional grants.
"""
import argparse
from collections import Counter
import json
import re
from pathlib import Path

from audit_srd_native_references import Index, normalize
from report_srd_promotion import REPO, PACKAGES, native_rows

ORIGIN_KINDS = {"class", "subclass", "class-feature", "subclass-feature",
                "race", "subrace", "species", "background", "feat", "optional-feature"}
MODES = {"known", "prepared", "innate", "expanded", "spellbook"}
SCHOOL_CODES = frozenset("acdeintv")


def part(key):
    return str(key).replace("~", "~0").replace("/", "~1")


def children(value, path):
    if isinstance(value, dict):
        for key, child in value.items():
            yield key, child, path + "/" + part(key)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield index, child, path + "/" + str(index)


class SpellSelectorInventory:
    """Resolve only reviewed static filter syntax, never grant a spell.

    Class membership comes from the publisher-reconciled native spell lists,
    not from upstream class tags or any other edition. Character state,
    progression timing, replacement rules and eligibility remain unevaluated.
    """
    def __init__(self, rows):
        self.spells = {r["id"]: r for r in rows if r["kind"] == "spell"}
        self.classes = {}
        for row in rows:
            if row["kind"] == "class":
                self.classes.setdefault(normalize(row["name"]), []).append(row)

    def inspect(self, raw, mode):
        # Keep unsupported forms explicit. In particular, do not interpret
        # arbitrary selector syntax as a wildcard or silently ignore a clause.
        def result(status, candidates=(), **details):
            return {"selectorStatus": status, "candidateSpellIds": sorted(candidates),
                    **details}

        keys = set(raw)
        operators = keys & {"choose", "all"}
        if len(operators) != 1 or keys - {"choose", "all", "count"}:
            return result("unsupported-selector-shape")
        operator = next(iter(operators))
        expression = raw[operator]
        count = raw.get("count", 1) if operator == "choose" else None
        if (not isinstance(expression, str) or
                (operator == "choose" and (type(count) is not int or count < 1)) or
                (operator == "all" and ("count" in raw or mode != "expanded"))):
            return result("unsupported-selector-shape")

        clauses = {}
        for clause in expression.split("|"):
            key, separator, value = clause.partition("=")
            key = normalize(key)
            if not separator or key in clauses or not value.strip():
                return result("unsupported-filter-syntax")
            clauses[key] = [normalize(v) for v in value.split(";")]
        if ("level" not in clauses or
                set(clauses) - {"level", "class", "components & miscellaneous", "school"} or
                any(v not in tuple(str(n) for n in range(10)) for v in clauses["level"]) or
                ("school" in clauses and any(v not in SCHOOL_CODES for v in clauses["school"])) or
                (mode == "spellbook" and "0" in clauses["level"]) or
                ("components & miscellaneous" in clauses and
                 clauses["components & miscellaneous"] != ["ritual"])):
            return result("unsupported-filter-syntax")

        levels = {int(v) for v in clauses["level"]}
        candidates = {identity for identity, spell in self.spells.items()
                      if spell.get("level") in levels}
        if "class" in clauses:
            membership = set()
            for name in clauses["class"]:
                classes = self.classes.get(name, [])
                if len(classes) != 1 or not isinstance(classes[0].get("spellList"), list):
                    return result("unresolved-class-list", unresolvedClass=name)
                groups = classes[0]["spellList"]
                for group in groups:
                    if (not isinstance(group, dict) or type(group.get("level")) is not int
                            or not isinstance(group.get("spellIds"), list)):
                        return result("invalid-class-list", unresolvedClass=name)
                    for identity in group["spellIds"]:
                        if (not isinstance(identity, str) or identity not in self.spells or
                                self.spells[identity].get("level") != group["level"]):
                            return result("invalid-class-list", unresolvedClass=name)
                        if group["level"] in levels:
                            membership.add(identity)
            candidates &= membership
        if "school" in clauses:
            schools = set(clauses["school"])
            candidates = {identity for identity in candidates
                          if normalize(self.spells[identity].get("school", "")) in schools}
        if "components & miscellaneous" in clauses:
            candidates = {identity for identity in candidates
                          if self.spells[identity].get("meta", {}).get("ritual") is True}

        details = {"selectorOperator": operator, "choiceCount": count,
                   "selectorFilters": clauses}
        if not candidates or (count is not None and len(candidates) < count):
            return result("insufficient-candidates", candidates, **details)
        return result("static-candidates", candidates, **details)


class WarlockCantripPrerequisiteInventory:
    """Review the three published damaging-cantrip invocation predicates.

    Validate normalized descriptive predicates against the published evidence.
    Distinguish prerequisite candidates from the more restrictive selected pool.
    """
    FEATURES = {
        "optional-feature.agonizing-blast": ("level=0|class=Warlock",
                                            "a Warlock Cantrip That Deals Damage", False, None),
        "optional-feature.eldritch-spear": ("level=0|class=Warlock",
                                           "a Warlock Cantrip That Deals Damage", False, 10),
        "optional-feature.repelling-blast": ("level=0|class=Warlock",
                                            "a Warlock Cantrip That Deals Damage via an Attack Roll", True, None),
    }
    # Exact reviewed SRD 5.2.1 Warlock cantrip list. The positive damage and
    # attack evidence below is publisher text, not an inference from a name.
    REVIEWED = {
        "spell.chill-touch": ("necrotic damage", "make a melee spell attack", "touch"),
        "spell.eldritch-blast": ("force damage", "make a ranged spell attack", 120),
        "spell.poison-spray": ("poison damage", "make a ranged spell attack", 30),
        "spell.true-strike": ("if the attack deals damage", "make one attack with the weapon", "self"),
        "spell.mage-hand": (None, None, 30),
        "spell.minor-illusion": (None, None, 30),
        "spell.prestidigitation": (None, None, 10),
    }

    def __init__(self, spell_selectors, tag):
        self.spell_selectors = spell_selectors
        self.tag = tag

    @staticmethod
    def report(status, prerequisite=(), selection=(), **context):
        return {"predicateStatus": status,
                "prerequisiteCandidateSpellIds": sorted(prerequisite),
                "selectionCandidateSpellIds": sorted(selection), **context}

    def inspect(self, row, raw):
        definition = self.FEATURES.get(row["id"])
        if self.tag != "SRD52" or definition is None:
            return self.report("unreviewed-prerequisite-selector")
        expression, label, attack, minimum_range = definition
        if (not isinstance(raw, dict) or raw.get("choose") != expression or
                raw.get("entry") != label or set(raw) != {"choose", "entry", "entrySummary", "predicate", "selection"} or
                raw["entrySummary"] != label.removeprefix("a ") or
                raw["predicate"] != {"dealsDamage": True, "requiresAttackRoll": attack} or
                raw["selection"] != {"known": True, "differentOnRepeat": True, "minimumRangeFeet": minimum_range}):
            return self.report("unexpected-prerequisite-shape")
        base = self.spell_selectors.inspect({"choose": "level=0|class=Warlock"}, "known")
        if base.get("selectorStatus") != "static-candidates":
            return self.report("unresolved-warlock-cantrip-list")
        if set(base["candidateSpellIds"]) != self.REVIEWED.keys():
            return self.report("warlock-cantrip-inventory-changed")
        prerequisites, selected = [], []
        for identity in base["candidateSpellIds"]:
            spell = self.spell_selectors.spells[identity]
            damage_text, attack_text, expected_range = self.REVIEWED[identity]
            evidence = spell.get("officialTextLayout")
            distance = spell.get("range", {}).get("distance", {})
            actual_range = distance.get("amount") if distance.get("type") == "feet" else distance.get("type")
            if not isinstance(evidence, str) or actual_range != expected_range:
                return self.report("cantrip-evidence-mismatch", spellId=identity)
            text = normalize(evidence)
            if (bool(spell.get("damageInflict")) != bool(damage_text) or
                    (damage_text and damage_text not in text) or
                    (attack_text and attack_text not in text)):
                return self.report("cantrip-evidence-mismatch", spellId=identity)
            if not damage_text or (attack and not attack_text):
                continue
            prerequisites.append(identity)
            if minimum_range is None or (type(actual_range) is int and actual_range >= minimum_range):
                selected.append(identity)
        return self.report("reviewed-static-predicate", prerequisites, selected,
                           requiresDamage=True, requiresAttackRoll=attack,
                           selectedCantripMustBeKnown=True, differentCantripOnRepeat=True,
                           minimumSelectionRangeFeet=minimum_range,
                           publisherEvidenceReviewed=True,
                           nativePredicateVerified=True)


class CharacterSelectorInventory:
    """Static, edition-local candidate pools, not character eligibility decisions."""

    GROUPS = {
        "toolArtisan": "item-group.artisan-s-tools",
        "instrumentMusical": "item-group.musical-instrument",
        "setGaming": "item-group.gaming-set",
        "focusSpellcastingArcane": "item-group.arcane-focus",
        "focusSpellcastingDruidic": "item-group.druidic-focus",
        "focusSpellcastingHoly": "item-group.holy-symbol",
    }
    PROFICIENCY_GROUPS = {
        "anyArtisansTool": "toolArtisan",
        "anyMusicalInstrument": "instrumentMusical",
        "anyGamingSet": "setGaming",
    }
    ARMOR = {"light": "LA", "medium": "MA", "heavy": "HA", "shield": "S"}
    WEAPONS = {"weaponSimple": ("simple", {"M", "R"}),
               "weaponMartial": ("martial", {"M", "R"}),
               "weaponSimpleMelee": ("simple", {"M"}),
               "weaponMartialMelee": ("martial", {"M"}),
               "weaponSimpleRanged": ("simple", {"R"}),
               "weaponMartialRanged": ("martial", {"R"})}

    def __init__(self, rows, index):
        self.rows = {r["id"]: r for r in rows}
        self.index = index

    @staticmethod
    def result(status, candidates=(), **details):
        return {"candidateStatus": status, "candidateIds": sorted(set(candidates)), **details}

    def local_code(self, raw):
        if not isinstance(raw, str):
            return None
        parts = raw.split("|")
        if len(parts) > 2 or (len(parts) == 2 and parts[1].upper() != self.index.tag):
            return None
        return parts[0]

    def pool(self, candidates, count=None, **details):
        ids = sorted(set(candidates))
        if count is not None and (type(count) is not int or count < 1):
            return self.result("unsupported-selector-shape", **details)
        if not ids or (count is not None and len(ids) < count):
            return self.result("insufficient-candidates", ids, choiceCount=count, **details)
        return self.result("static-candidates", ids, choiceCount=count, **details)

    def literal_candidates(self, kind, names, count=None):
        if not isinstance(names, list) or not names:
            return self.result("unsupported-selector-shape")
        ids = []
        for name in names:
            if not isinstance(name, str):
                return self.result("unsupported-selector-shape")
            status, targets, _ = self.index.resolve(kind, name)
            if status not in {"unique-literal-candidate", "named-variant-candidate"} or len(targets) != 1:
                return self.result("unresolved-choice-member", unresolvedMember=name,
                                   memberStatus=status)
            ids.extend(targets)
        return self.pool(ids, count)

    def equipment_group(self, category):
        group = self.rows.get(self.GROUPS[category])
        if not group or group["kind"] != "item-group":
            return None
        ids = []
        for name in group.get("items", []):
            status, targets, _ = self.index.resolve("item", name) if isinstance(name, str) else ("invalid", [], None)
            if (status != "unique-literal-candidate" or len(targets) != 1 or
                    self.rows[targets[0]]["kind"] not in {"item", "baseitem"} or
                    targets[0] not in group.get("catalogReferences", [])):
                return None
            ids.extend(targets)
        return ids

    def weapon_pool(self, category, types, properties=None):
        ids = []
        for row in self.rows.values():
            if (row["kind"] != "baseitem" or self.local_code(row.get("type")) not in types or
                    row.get("weaponCategory") != category):
                continue
            codes = {self.local_code(p.get("uid") if isinstance(p, dict) else p)
                     for p in row.get("property", [])}
            if properties is None or codes & properties:
                ids.append(row["id"])
        return ids

    def equipment(self, raw):
        if not isinstance(raw, dict) or set(raw) - {"equipmentType", "equipmentTypes", "quantity"}:
            return self.result("non-category-equipment-or-unreviewed-shape")
        if ("equipmentType" in raw) == ("equipmentTypes" in raw):
            return self.result("unsupported-selector-shape")
        categories = [raw["equipmentType"]] if "equipmentType" in raw else raw["equipmentTypes"]
        quantity = raw.get("quantity", 1)
        if (not isinstance(categories, list) or not categories or
                any(not isinstance(c, str) for c in categories) or
                type(quantity) is not int or quantity < 1):
            return self.result("unsupported-selector-shape")
        ids = []
        for category in categories:
            if category in self.GROUPS:
                group = self.equipment_group(category)
                if not group:
                    return self.result("unresolved-equipment-group", category=category)
                ids.extend(group)
            elif category in self.WEAPONS:
                ids.extend(self.weapon_pool(*self.WEAPONS[category]))
            else:
                return self.result("unsupported-equipment-category", category=category)
        # Quantity is a purchased/granted item quantity, not a requirement
        # to select that many distinct equipment identities.
        return self.pool(ids, quantity=quantity, categories=categories)

    def proficiency(self, raw, field):
        if not isinstance(raw, dict) or len(raw) != 1:
            return self.result("unsupported-selector-shape")
        key, value = next(iter(raw.items()))
        kind = {"skill": "skill", "language": "language",
                "tool": "item", "weapon": "item", "armor": "item"}[field]
        if key == "choose" and isinstance(value, dict) and set(value) <= {"from", "count"}:
            return self.literal_candidates(kind, value.get("from"), value.get("count", 1))
        if field == "skill" and key == "any":
            return self.pool((r["id"] for r in self.rows.values() if r["kind"] == "skill"), value)
        if field == "language" and key == "anyStandard":
            return self.pool((r["id"] for r in self.rows.values()
                              if r["kind"] == "language" and r.get("type") == "standard"), value)
        if field == "tool" and key in self.PROFICIENCY_GROUPS:
            ids = self.equipment_group(self.PROFICIENCY_GROUPS[key])
            if not ids:
                return self.result("unresolved-equipment-group", category=key)
            return self.pool(ids, value)
        if field == "armor" and key in self.ARMOR and value is True:
            return self.pool((r["id"] for r in self.rows.values()
                              if r["kind"] == "baseitem" and self.local_code(r.get("type")) == self.ARMOR[key]),
                             operator="all")
        if field == "weapon" and key in {"simple", "martial"} and value is True:
            return self.pool(self.weapon_pool(key, {"M", "R"}), operator="all")
        if field == "weapon" and key == "all" and isinstance(value, dict) and set(value) == {"fromFilter"}:
            filters = {"type=martial weapon|property=light": {"L"},
                       "type=martial weapon|property=light;finesse": {"L", "F"}}
            expression = value["fromFilter"]
            if isinstance(expression, str) and expression in filters:
                return self.pool(self.weapon_pool("martial", {"M", "R"}, filters[expression]),
                                 operator="all")
        return self.result("unsupported-proficiency-selector")

    def feat(self, raw):
        if (not isinstance(raw, dict) or set(raw) != {"category", "count"} or
                raw["category"] != ["O"]):
            return self.result("unsupported-feat-category")
        return self.pool((r["id"] for r in self.rows.values()
                          if r["kind"] == "feat" and r.get("category") == "O"), raw["count"])

    def class_member(self, raw, field):
        value = raw.get("proficiency") if isinstance(raw, dict) else raw
        if not isinstance(value, str):
            return self.result("unsupported-class-proficiency-shape")
        if field == "armor" and value in self.ARMOR or field == "weapon" and value in {"simple", "martial"}:
            return self.proficiency({value: True}, field)
        match = re.fullmatch(r"\{@item ([^{}]+)\}", value)
        if match:
            return self.literal_candidates("item", [match.group(1)])
        return self.result("unreviewed-class-proficiency-prose")


def audit(rows, tag):
    index = Index(rows, tag)
    character_selectors = CharacterSelectorInventory(rows, index)
    spell_selectors = SpellSelectorInventory(rows)
    warlock_predicates = WarlockCantripPrerequisiteInventory(spell_selectors, tag)
    result = []
    for row in sorted(rows, key=lambda r: r["id"]):
        if row["kind"] not in ORIGIN_KINDS:
            continue
        explicit = set(row.get("catalogReferences", []))

        def emit(path, relation, raw, status, targets=(), **context):
            result.append({"entity": row["id"], "pointer": path, "relation": relation,
                           "raw": raw, "status": status, "targets": sorted(targets),
                           "alreadyExplicit": bool(targets) and set(targets) <= explicit,
                           **context})

        def literal(path, relation, kind, raw, **context):
            if not isinstance(raw, str):
                emit(path, relation, raw, "unreviewed-shape", literal=True, **context)
                return
            # Spell UIDs can have a cast-level or cantrip suffix; preserve it
            # separately rather than confusing cast level with spell identity.
            uid, sep, suffix = raw.partition("#") if kind == "spell" else (raw, "", "")
            if sep:
                context["castQualifier"] = suffix
            status, targets, variant = index.resolve(kind, uid)
            emit(path, relation, raw, status, targets, variant=variant, literal=True, **context)

        def spell_tree(value, path, mode):
            if isinstance(value, str):
                literal(path, "spell-grant", "spell", value, grantMode=mode)
            elif isinstance(value, dict) and ("choose" in value or "all" in value):
                # Filters are authored selectors, not lists of awarded spells.
                emit(path, "spell-selector", value, "selector-review", grantMode=mode,
                     **spell_selectors.inspect(value, mode))
            else:
                for _, child, child_path in children(value, path):
                    spell_tree(child, child_path, mode)

        def equipment(value, path):
            if isinstance(value, str):
                literal(path, "starting-equipment", "item", value)
            elif isinstance(value, dict):
                if "item" in value:
                    literal(path + "/item", "starting-equipment", "item", value["item"])
                elif any(k in value for k in ("equipmentType", "equipmentTypes", "special", "value")):
                    emit(path, "equipment-selector-or-value", value, "selector-review",
                         **character_selectors.equipment(value))
                else:
                    for _, child, child_path in children(value, path):
                        equipment(child, child_path)
            elif isinstance(value, list):
                for _, child, child_path in children(value, path):
                    equipment(child, child_path)

        def proficiency(value, path, kind, field):
            if not isinstance(value, dict):
                emit(path, "proficiency", value, "unreviewed-shape", proficiencyKind=kind)
                return
            for key, child, child_path in children(value, path):
                if key == "choose" and isinstance(child, dict):
                    emit(child_path, "proficiency-selector", child, "selector-review", proficiencyKind=kind,
                         **character_selectors.proficiency({key: child}, field))
                    for i, target in enumerate(child.get("from", [])):
                        literal(child_path + "/from/" + str(i), "proficiency-choice-member", kind, target)
                elif key == "all" and isinstance(child, dict) and isinstance(child.get("fromFilter"), str):
                    emit(child_path, "proficiency-selector", {key: child}, "selector-review", proficiencyKind=kind,
                         **character_selectors.proficiency({key: child}, field))
                elif key.startswith("any") or key in {"simple", "martial", "light", "medium", "heavy", "shield"}:
                    emit(child_path, "proficiency-selector", {key: child}, "selector-review", proficiencyKind=kind,
                         **character_selectors.proficiency({key: child}, field))
                elif child is True:
                    literal(child_path, "proficiency", kind, key)
                else:
                    emit(child_path, "proficiency", {key: child}, "unreviewed-shape", proficiencyKind=kind)

        def prerequisites(value, path):
            for key, child, child_path in children(value, path):
                if key in {"spell", "optionalfeature", "feat"} and isinstance(child, list):
                    kind = {"spell": "spell", "optionalfeature": "optionalfeature", "feat": "feat"}[key]
                    for i, target in enumerate(child):
                        target_path = child_path + "/" + str(i)
                        if isinstance(target, dict):
                            if kind == "spell":
                                emit(target_path, "prerequisite-selector", target, "selector-review",
                                     **warlock_predicates.inspect(row, target))
                            else:
                                emit(target_path, "prerequisite-selector", target, "selector-review")
                        else:
                            literal(target_path, "prerequisite", kind, target)
                elif key == "pact":
                    literal(child_path, "prerequisite", "optionalfeature", "Pact of the " + str(child))
                elif key == "level" and isinstance(child, dict) and isinstance(child.get("class"), dict):
                    cls = child["class"]
                    raw = cls.get("name", "") + ("|" + cls["source"] if cls.get("source") else "")
                    literal(child_path + "/class", "prerequisite", "class", raw,
                            minimumClassLevel=child.get("level"))
                else:
                    emit(child_path, "prerequisite-constraint", {key: child}, "constraint-review")

        def visit(value, path):
            for key, child, child_path in children(value, path):
                if key in {"source", "catalogReferences", "textLayout", "officialTextLayout"}:
                    continue
                if key == "additionalSpells" and isinstance(child, list):
                    for i, grant in enumerate(child):
                        grant_path = child_path + "/" + str(i)
                        if not isinstance(grant, dict):
                            emit(grant_path, "spell-grant", grant, "unreviewed-shape")
                            continue
                        for mode, payload, mode_path in children(grant, grant_path):
                            if mode in MODES:
                                spell_tree(payload, mode_path, mode)
                            elif mode == "replacementOptions" and isinstance(payload, list):
                                for _, option, option_path in children(payload, mode_path):
                                    if (isinstance(option, dict) and set(option) == {"trigger", "target", "different", "selector"}
                                            and option["trigger"] == "long-rest" and option["target"] == "known-lineage-cantrip"
                                            and option["different"] is True and isinstance(option["selector"], dict)):
                                        emit(option_path, "spell-replacement-selector", option, "selector-review",
                                             grantMode="known", **spell_selectors.inspect(option["selector"], "known"))
                                    else:
                                        emit(option_path, "spell-replacement-selector", option, "unreviewed-shape")
                            elif mode not in {"name", "ability"}:
                                emit(mode_path, "spell-grant", payload, "unreviewed-shape")
                elif key == "startingEquipment":
                    # Classes carry both formatted descriptions and structured
                    # choices. Do not count the formatted descriptions twice.
                    if isinstance(child, dict):
                        if "defaultData" in child:
                            equipment(child["defaultData"], child_path + "/defaultData")
                        else:
                            emit(child_path, "starting-equipment", child, "unreviewed-shape")
                    else:
                        equipment(child, child_path)
                elif key == "prerequisite" and isinstance(child, list):
                    for i, requirement in enumerate(child):
                        prerequisites(requirement, child_path + "/" + str(i))
                elif key == "feats" and isinstance(child, list):
                    for i, grant in enumerate(child):
                        for name, flag, grant_path in children(grant, child_path + "/" + str(i)):
                            if name == "anyFromCategory":
                                emit(grant_path, "feat-selector", flag, "selector-review",
                                     **character_selectors.feat(flag))
                            elif flag is True:
                                literal(grant_path, "feat-grant", "feat", name)
                            else:
                                emit(grant_path, "feat-grant", {name: flag}, "unreviewed-shape")
                elif key in {"skillProficiencies", "toolProficiencies", "languageProficiencies",
                             "weaponProficiencies", "armorProficiencies"} and isinstance(child, list):
                    kind = {"skillProficiencies": "skill", "toolProficiencies": "item",
                            "languageProficiencies": "language", "weaponProficiencies": "item",
                            "armorProficiencies": "item"}[key]
                    for i, grant in enumerate(child):
                        proficiency(grant, child_path + "/" + str(i), kind,
                                    key.removesuffix("Proficiencies"))
                elif key in {"startingProficiencies", "proficienciesGained"} and isinstance(child, dict):
                    # Classes use short field names alongside structured long names.
                    # The latter take precedence; the former are not counted twice.
                    for alias, field in (("skills", "skill"), ("armor", "armor"), ("weapons", "weapon")):
                        if field + "Proficiencies" in child or alias not in child:
                            continue
                        values = child[alias]
                        if not isinstance(values, list):
                            emit(child_path + "/" + alias, "class-proficiency-member", values, "unreviewed-shape")
                            continue
                        for i, member in enumerate(values):
                            member_path = child_path + "/" + alias + "/" + str(i)
                            if field == "skill":
                                proficiency(member, member_path, "skill", field)
                            else:
                                match = re.fullmatch(r"\{@item ([^|{}]+)(?:\|([^|{}]*))?(?:\|[^{}]+)?\}", member) if isinstance(member, str) else None
                                if field == "weapon" and match:
                                    raw = match.group(1) + ("|" + match.group(2) if match.group(2) else "")
                                    literal(member_path, "class-proficiency-member", "item", raw)
                                else:
                                    emit(member_path, "class-proficiency-member", member, "selector-review",
                                         proficiencyKind=field, **character_selectors.class_member(member, field))
                    visit(child, child_path)
                elif key == "optionalfeatureProgression":
                    emit(child_path, "optional-feature-progression", child, "selector-review")
                elif key == "type" and child == "refOptionalfeature":
                    literal(path + "/optionalfeature", "optional-feature-choice-member",
                            "optionalfeature", value.get("optionalfeature"))
                else:
                    visit(child, child_path)

        visit(row, "")
    unlinked = [o for o in result if o.get("literal") and (o["status"] not in {"unique-literal-candidate", "named-variant-candidate"} or not o["alreadyExplicit"])]
    counts = Counter(o["status"] for o in result)
    relation_counts = Counter(o["relation"] for o in result)
    selector_counts = Counter(o["selectorStatus"] for o in result if "selectorStatus" in o)
    character_counts = Counter(o["candidateStatus"] for o in result if "candidateStatus" in o)
    predicate_counts = Counter(o["predicateStatus"] for o in result if "predicateStatus" in o)
    return {"status": "diagnostic-only", "editionTag": tag,
            "scope": "Structured grants, prerequisites, equipment and proficiency fields in character options; selectors and constraints remain unexecuted.",
            "counts": dict(sorted(counts.items())),
            "relationCounts": dict(sorted(relation_counts.items())),
            "nativeEntities": len(rows),
            "spellSelectorCounts": dict(sorted(selector_counts.items())),
            "characterSelectorCounts": dict(sorted(character_counts.items())),
            "spellPrerequisiteCounts": dict(sorted(predicate_counts.items())),
            "explicitLiteralLinks": sum(o["alreadyExplicit"] for o in result),
            "unlinkedLiteralOccurrences": len(unlinked),
            "occurrences": result}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--require-literal-links", action="store_true", help="Fail when an inventoried literal dependency does not resolve and link within its edition.")
    args = parser.parse_args()
    report = {}
    for edition, package in PACKAGES.items():
        rows = list(native_rows(REPO / "data" / "rulesets" / package / "native"))
        report[edition] = audit(rows, "SRD51" if edition == "srd-5.1" else "SRD52")
        print(edition, report[edition]["counts"], "explicit", report[edition]["explicitLiteralLinks"],
              "unlinked literal", report[edition]["unlinkedLiteralOccurrences"],
              "spell selectors", report[edition]["spellSelectorCounts"],
              "character selectors", report[edition]["characterSelectorCounts"],
              "spell prerequisites", report[edition]["spellPrerequisiteCounts"],
              "native entities", report[edition]["nativeEntities"])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if args.require_literal_links and any(r["unlinkedLiteralOccurrences"] for r in report.values()):
        for edition, r in report.items():
            for o in r["occurrences"]:
                if o.get("literal") and (o["status"] not in {"unique-literal-candidate", "named-variant-candidate"} or not o["alreadyExplicit"]):
                    print(edition, o["entity"], o["pointer"], o["raw"], o["status"], o["targets"])
        raise SystemExit("Unlinked literal structured relationships")


if __name__ == "__main__":
    main()
