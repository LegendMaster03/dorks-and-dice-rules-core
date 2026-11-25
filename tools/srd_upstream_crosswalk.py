"""Crosswalk expanded-source occurrences to existing edition-local representations.

A title, nested heading, or row identity is not body-fidelity certification.
Unmatched and scope-different occurrences remain explicit review work.
"""
from collections import defaultdict
from copy import deepcopy
import re

LEGACY = "srd-5.1"
MODERN = "srd-5.2.1"

TABLE_ALIASES = {
    LEGACY: {
        "sentient magic item communication": "table.sentient-item-s-communication",
        "sentient magic item senses": "table.sentient-item-s-senses",
        "sentient magic item alignment": "table.sentient-item-s-alignment",
        "sentient special purpose": "table.sentient-item-s-special-purpose",
    },
    MODERN: {"typical dcs": "table.typical-difficulty-classes"},
}

# Only explicit, reviewed representations. In particular, a narrower SRD
# vehicle table is not evidence that all commercial-source rows are eligible.
TABLE_RELATED = {
    (LEGACY, "airborne and waterborne vehicles"): ("table.waterborne-vehicles",),
    (LEGACY, "ability score point cost"): (),
    (LEGACY, "precipitation"): (),
    (MODERN, "abilities, ability checks, and saving throws"): (),
}

NESTED_TABLES = {
    (LEGACY, "object armor class"): "rule.breaking-objects",
    (LEGACY, "object hit points"): "rule.breaking-objects",
    (LEGACY, "short-term madness"): "rule.madness",
    (LEGACY, "long-term madness"): "rule.madness",
    (LEGACY, "indefinite madness"): "rule.madness",
    (MODERN, "wand of wonder effects"): "item.wand-of-wonder",
}

OTHER_ALIASES = {
    (LEGACY, "action", "use an object"): ("action.use-an-object",),
    (MODERN, "monster", "avatar of death"): ("monster.avatar-of-death",),
    (MODERN, "monster", "giant fly"): ("monster.giant-fly",),
    (MODERN, "baseitem", "firearm bullet"): ("baseitem.firearm-bullet",),
    (MODERN, "item", "crawler mucus"): ("item.crawler-mucus-contact",),
    (MODERN, "variantrule", "multiclassing"): ("rule.multiclassing",),
    (MODERN, "variantrule", "weapon mastery properties"): ("rule.weapon-mastery-properties",),
    (MODERN, "variantrule", "mixing potions"): ("rule.magic-item-categories", "table.potion-miscibility"),
    (LEGACY, "magicvariant", "arrow of slaying (*)"): ("magic-variant.arrow-of-slaying",),
    (LEGACY, "magicvariant", "barding"): ("rule.mounts-and-vehicles",),
    (MODERN, "magicvariant", "plate armor of etherealness (*)"): ("item.plate-armor-of-etherealness",),
}


def normalize(value):
    return " ".join(str(value or "").replace("’", "'").casefold().split())


def display(value):
    if isinstance(value, dict):
        value = value.get("entry", "")
    return re.sub(r"\{@\w+ ([^}|]+)(?:\|[^}|]*)?(?:\|([^}]+))?\}",
                  lambda m: m[2] or m[1], str(value))


def descendants(value, pointer=""):
    if isinstance(value, dict):
        yield pointer, value
        for key, child in value.items():
            if key not in {"source", "catalogReferences"}:
                yield from descendants(child, pointer + "/" + key.replace("~", "~0").replace("/", "~1"))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from descendants(child, pointer + "/" + str(index))


def deck_parent(edition, name):
    name = normalize(name)
    if name == "deck of illusions":
        return "item.deck-of-illusions"
    if name in {"deck of many things", "mysterious deck"}:
        return "item.deck-of-many-things" if edition == LEGACY else "item.mysterious-deck"
    return None


class Crosswalk:
    def __init__(self, edition, rows):
        self.edition = edition
        self.rows = {r["id"]: r for r in rows}
        self.tables = defaultdict(list)
        for row in rows:
            if row["kind"] == "table":
                self.tables[normalize(row["name"])].append(row["id"])

    def result(self, status, ids=(), **details):
        ids = sorted(set(ids))
        missing = [i for i in ids if i not in self.rows]
        if missing:
            raise ValueError("Missing native crosswalk target: " + ", ".join(missing))
        return {"status": status, "nativeIds": ids, **details}

    def nested_table(self, parent, caption):
        if parent not in self.rows:
            raise ValueError("Missing nested table parent: " + parent)
        return [(p, v) for p, v in descendants(self.rows[parent])
                if v.get("type") == "table" and normalize(v.get("caption")) == normalize(caption)]

    def adjudicate_table_scope(self, caption, record, related, details):
        # Adjudicate the published table scope, not the SRD flag on a
        # commercial book's generated convenience table.
        pages = [r for r in self.rows.values() if r["kind"] == "rulebook-page"]
        if not pages:  # Small synthetic unit fixtures need not supply a book.
            return self.result("publisher-scope-review", related, **details)
        text = [normalize(p["textLayout"]) for p in pages]
        if any(caption in page for page in text):
            raise ValueError("Previously absent publisher table title appeared: " + caption)
        if self.edition == LEGACY and caption == "airborne and waterborne vehicles":
            native = self.rows["table.waterborne-vehicles"]
            page = self.rows["rulebook.page-072"]["textLayout"]
            if "Waterborne Vehicles" not in page or len(native["rows"]) != 6:
                raise ValueError("Unexpected legacy vehicle table scope")
            def compact(value):
                return re.sub(r"\s+", "", normalize(display(value)))
            evidence = [compact(line) for line in page.splitlines()]
            for cells in native["rows"]:
                if not any(compact(" ".join(display(cell) for cell in cells)) in line for line in evidence):
                    raise ValueError("Legacy vehicle table row no longer matches publisher")
            return self.result("publisher-narrower-table", related, publisherPages=[72],
                               nativeRowCount=6, **details)
        if self.edition == MODERN:
            ids = ["table.ability-descriptions", "table.ability-check-examples", "table.saving-throw-examples"]
            for identity in ids:
                table = self.rows[identity]
                if len(table["rows"]) != 6:
                    raise ValueError("Unexpected published ability table inventory")
                if not any(normalize(table["name"]) in page for page in text):
                    raise ValueError("Missing published ability table heading")
            return self.result("publisher-different-table-structure", ids, publisherPages=[5, 6, 7], **details)
        # SRD 5.1 does not include the PHB point-buy variant or the DMG
        # random precipitation table. This does not exclude other weather
        # rules or forbid independently authored optional rules.
        if caption == "ability score point cost" and any("customizing ability scores" in page for page in text):
            raise ValueError("Legacy point-buy scope requires renewed review")
        return self.result("not-published-as-table", **details)

    def table(self, name, record):
        caption = normalize(name.split("; ")[-1])
        count = len(record["rows"]) if isinstance(record.get("rows"), list) else None
        details = {"upstreamRowCount": count}
        related = TABLE_RELATED.get((self.edition, caption))
        if related is not None:
            return self.adjudicate_table_scope(caption, record, related, details)
        alias = TABLE_ALIASES[self.edition].get(caption)
        ids = [alias] if alias else self.tables.get(caption, [])
        if len(ids) == 1:
            native = self.rows[ids[0]]
            return self.result("native-table-title-match", ids,
                               nativeRowCount=len(native["rows"]) if isinstance(native.get("rows"), list) else None,
                               **details)
        if len(ids) > 1:
            return self.result("ambiguous-native-table-title", ids, **details)
        parent = NESTED_TABLES.get((self.edition, caption))
        if parent:
            matches = self.nested_table(parent, caption)
            if len(matches) != 1:
                raise ValueError("Expected unique nested table: " + parent + " / " + caption)
            pointer, table = matches[0]
            return self.result("nested-table-title-match", [parent], nativePointer=pointer,
                               nativeRowCount=len(table["rows"]), **details)
        if self.edition == LEGACY and caption.endswith(" deities"):
            pantheon = caption.removesuffix(" deities")
            ids = [r["id"] for r in self.rows.values()
                   if r["kind"] == "deity" and normalize(r.get("pantheon")) == pantheon]
            if not ids:
                raise ValueError("Missing pantheon representation: " + pantheon)
            return self.result("normalized-collection", ids, nativeRecordCount=len(ids), **details)
        if self.edition == MODERN and caption == "skills":
            ids = [r["id"] for r in self.rows.values() if r["kind"] == "skill"]
            if len(ids) != 18:
                raise ValueError("Unexpected skill inventory")
            return self.result("normalized-collection", ids, nativeRecordCount=len(ids), **details)
        if self.edition == LEGACY and caption == "spell scroll":
            ids = [r["id"] for r in self.rows.values() if r["id"].startswith("item.spell-scroll-")]
            if len(ids) != 10:
                raise ValueError("Unexpected legacy spell scroll inventory")
            rarity = ["common", "common", "uncommon", "uncommon", "rare", "rare", "very rare", "very rare", "very rare", "legendary"]
            dc = [13, 13, 13, 15, 15, 17, 17, 18, 18, 19]
            bonus = [5, 5, 5, 7, 7, 9, 9, 10, 10, 11]
            levels = set()
            for identity in ids:
                row = self.rows[identity]
                level = row.get("spellScrollLevel")
                if (type(level) is not int or not 0 <= level <= 9 or level in levels
                        or row.get("rarity") != rarity[level]
                        or row.get("spellSaveDc") != dc[level]
                        or row.get("spellAttackBonus") != bonus[level]
                        or 242 not in row.get("source", {}).get("pages", [])):
                    raise ValueError("Unverified legacy spell scroll row " + identity)
                levels.add(level)
            return self.result("publisher-verified-normalized-collection", ids,
                               nativeRecordCount=len(ids), publisherPages=[242], **details)
        return self.result("unresolved-table", **details)

    def card(self, record):
        parent = deck_parent(self.edition, record.get("set"))
        if not parent:
            return self.result("unresolved-card-parent")
        row = self.rows[parent]
        name = normalize(record.get("name"))
        if parent == "item.deck-of-illusions":
            aliases = {"succubus/incubus": "succubus or incubus", "warrior veteran": "veteran warrior"}
            name = aliases.get(name, name)
            matches = []
            for pointer, table in descendants(row):
                if table.get("type") != "table":
                    continue
                for i, cells in enumerate(table.get("rows", [])):
                    if normalize(display(cells[-1])) == name:
                        matches.append(pointer + "/rows/" + str(i))
            if matches:
                return self.result("nested-card-table-row", [parent], nativePointers=matches)
        else:
            matches = [pointer for pointer, node in descendants(row)
                       if node.get("type") == "entries" and normalize(node.get("name")) == name]
            if len(matches) == 1:
                return self.result("nested-card-effect-heading", [parent], nativePointers=matches)
        return self.result("card-body-review", [parent])

    def unnamed_subrace(self, record):
        # Upstream uses unnamed default subrace records for these legacy base
        # races. Confirm merged content instead of creating a blank-name race.
        allowed = {"source", "raceName", "raceSource", "page", "srd", "basicRules",
                   "reprintedAs", "hasFluff", "hasFluffImages", "ability", "_versions"}
        parents = {"Dragonborn": "race.dragonborn", "Half-Elf": "race.half-elf",
                   "Half-Orc": "race.half-orc", "Human": "race.human", "Tiefling": "race.tiefling"}
        parent = parents.get(record.get("raceName"))
        if (self.edition != LEGACY or not parent or record.get("source") != "PHB"
                or record.get("raceSource") != "PHB" or record.keys() - allowed):
            return self.result("unresolved-identity")
        native = self.rows[parent]
        if "ability" in record and record["ability"] != native.get("ability"):
            return self.result("unnamed-subrace-field-review", [parent])
        if "_versions" in record:
            versions = deepcopy(record["_versions"])
            for version in versions:
                if version.get("_abstract", {}).get("source") != "PHB":
                    return self.result("unnamed-subrace-field-review", [parent])
                version["_abstract"]["source"] = "SRD51"
            if versions != native.get("_versions"):
                return self.result("unnamed-subrace-field-review", [parent])
        return self.result("merged-unnamed-subrace", [parent])

    def classify(self, occurrence, record):
        if occurrence["expandedSourceNativeIds"]:
            return self.result("expanded-source-pointer", occurrence["expandedSourceNativeIds"])
        kind, name = occurrence["kind"], occurrence["name"]
        if not name:
            if kind == "subrace":
                return self.unnamed_subrace(record)
            return self.result("unresolved-identity")
        if kind == "table":
            return self.table(name, record)
        if kind == "card":
            return self.card(record)
        if kind == "deck":
            parent = deck_parent(self.edition, name)
            if parent:
                return self.result("canonical-deck-parent", [parent])
        ids = OTHER_ALIASES.get((self.edition, kind, normalize(name)))
        if ids:
            return self.result("reviewed-related-native-identity", ids)
        if kind == "magicvariant" and re.fullmatch(r"\+[123] Shield \(\*\)", name):
            bonus = name[1]
            return self.result("reviewed-related-native-identity",
                               ["magic-variant." + bonus + "-shield", "item." + bonus + "-shield"])
        return self.result("unresolved-occurrence")
