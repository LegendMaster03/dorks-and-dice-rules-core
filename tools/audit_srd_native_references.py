"""Inventory edition-local inline references in promoted native SRD definitions.

This is a diagnostic, not an executable resolver or a source-fidelity claim.
A unique literal match is a candidate link, not proof of equivalent mechanics.
Unsupported tags, external sources, ambiguous targets and absent targets remain
visible, and no native data is rewritten.
"""
import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path
import re
import unicodedata

from report_srd_promotion import REPO, PACKAGES, native_rows

TAG = re.compile(r"\{@([A-Za-z]+) ([^{}]+)\}")
KINDS = {
    "spell": ("spell",),
    "creature": ("monster",),
    "condition": ("condition", "leveled-condition"),
    "skill": ("skill",),
    "item": ("item", "baseitem", "item-group", "magic-variant"),
    "feat": ("feat",),
    "action": ("action", "attack-option", "combat-option", "equipment-option", "reaction-option", "activity"),
    "sense": ("sense",),
    "language": ("language",),
    "class": ("class",),
    "race": ("race", "subrace", "species"),
    "optfeature": ("optional-feature",),
    "status": ("status",),
    "table": ("table",),
    "variantrule": ("glossary", "rule"),
    "vehicle": ("vehicle",),
    "deity": ("deity",),
    "object": ("object",),
    "trap": ("trap",),
    "hazard": ("hazard", "environmental-effect"),
    "itemproperty": ("item-property",),
    "itemmastery": ("item-mastery",),
}
CONTEXT = {"classfeature", "subclassfeature"}
# These tags format prose or denote values; they do not name catalog identities.
NON_ENTITY = {"b", "bold", "i", "italic", "u", "s", "strike", "dice", "damage", "hit",
              "dc", "h", "atk", "atkr", "actsave", "actsavefail", "actsavesuccess",
              "recharge", "chance", "note", "color", "highlight", "sup", "sub",
              "unit", "scaledice", "scaledamage", "d20", "ability", "skillcheck"}

def normalize(value):
    return " ".join(unicodedata.normalize("NFKC", str(value)).replace("’", "'").casefold().split())


def leaves(value, pointer=""):
    """Yield string leaves and exact JSON pointers, excluding provenance."""
    if isinstance(value, str):
        yield pointer, value
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from leaves(child, pointer + "/" + str(index))
    elif isinstance(value, dict):
        for key, child in sorted(value.items()):
            escaped = key.replace("~", "~0").replace("/", "~1")
            yield from leaves(child, pointer + "/" + escaped)


class Index:
    def __init__(self, rows, tag):
        self.tag = tag
        self.names = defaultdict(set)
        self.features = defaultdict(set)
        self.variants = defaultdict(set)
        self.pantheons = defaultdict(set)
        for row in rows:
            kind, name, identity = row["kind"], row["name"], row["id"]
            self.names[kind, normalize(name)].add(identity)
            if kind == "deity":
                self.pantheons[normalize(row["pantheon"]), normalize(name)].add(identity)
            if kind == "class-feature":
                key = ("classfeature", normalize(name), normalize(row["className"]), str(row["level"]))
                self.features[key].add(identity)
            elif kind == "subclass-feature":
                key = ("subclassfeature", normalize(name), normalize(row["className"]),
                       normalize(row["subclassShortName"]), str(row["level"]))
                self.features[key].add(identity)
            # Only explicit, named variants are candidates. Abstract templates
            # and their variables are not treated as materialized records.
            for variant in row.get("_versions", []):
                if isinstance(variant, dict) and variant.get("name") and variant.get("source", tag).upper() == tag:
                    self.variants[kind, normalize(variant["name"])].add(identity)

    def resolve(self, kind, raw):
        parts = raw.split("|")
        name = parts[0]
        if kind in CONTEXT:
            minimum = 4 if kind == "classfeature" else 6
            if len(parts) < minimum:
                return "malformed-context", [], None
            if kind == "classfeature":
                sources = [parts[2], parts[4] if len(parts) > 4 else ""]
                key = (kind, normalize(name), normalize(parts[1]), parts[3])
            else:
                sources = [parts[2], parts[4], parts[6] if len(parts) > 6 else ""]
                key = (kind, normalize(name), normalize(parts[1]), normalize(parts[3]), parts[5])
            if any(s and s.upper() != self.tag for s in sources):
                return "foreign-source", [], None
            targets = sorted(self.features[key])
            return self.result(targets)
        if kind not in KINDS:
            return "unsupported-tag", [], None
        source = parts[1] if len(parts) > 1 else ""
        # Deity markup uses pantheon, not publication, in its second field.
        if kind == "deity" and source and source.upper() not in ("SRD51", "SRD52"):
            return self.result(sorted(self.pantheons[normalize(source), normalize(name)]))
        if source and source.upper() != self.tag:
            return "foreign-source", [], None
        candidates = KINDS[kind]
        key = normalize(name)
        # The glossary's Ability Check entry and executable resolution seed
        # are separate representations. A glossary tag names the glossary.
        if kind == "variantrule" and self.names["glossary", key]:
            candidates = ("glossary",)
        targets = sorted({i for category in candidates for i in self.names[category, key]})
        if targets:
            return self.result(targets)
        variants = sorted({i for category in candidates for i in self.variants[category, key]})
        if variants:
            return ("named-variant-candidate" if len(variants) == 1 else "ambiguous",
                    variants, name)
        return "missing", [], None

    @staticmethod
    def result(targets):
        return ("unique-literal-candidate" if len(targets) == 1 else
                "ambiguous" if targets else "missing", targets, None)


def audit(rows, tag):
    index = Index(rows, tag)
    occurrences = []
    counts = Counter()
    for row in sorted(rows, key=lambda r: r["id"]):
        payload = {k: v for k, v in row.items()
                   if k not in {"source", "catalogReferences", "officialTextLayout", "textLayout"}}
        explicit = set(row.get("catalogReferences", []))
        for pointer, value in leaves(payload):
            for match in TAG.finditer(value):
                kind, raw = match[1].casefold(), match[2]
                if kind in NON_ENTITY:
                    continue
                status, targets, variant = index.resolve(kind, raw)
                counts[status] += 1
                occurrences.append({
                    "entity": row["id"], "pointer": pointer, "offset": match.start(),
                    "tag": kind, "raw": raw, "status": status, "targets": targets,
                    "variant": variant,
                    "alreadyExplicit": len(targets) == 1 and targets[0] in explicit,
                })
    return {"status": "diagnostic-only", "editionTag": tag,
            "scope": "Inline markup in promoted definitions; not structured-field or full body reconciliation.",
            "nativeEntities": len(rows), "occurrenceCount": len(occurrences),
            "counts": dict(sorted(counts.items())), "occurrences": occurrences}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    results = {}
    for edition, package in PACKAGES.items():
        rows = list(native_rows(REPO / "data" / "rulesets" / package / "native"))
        results[edition] = audit(rows, "SRD51" if edition == "srd-5.1" else "SRD52")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(results, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for edition, result in results.items():
        print(edition, result["occurrenceCount"], result["counts"])


if __name__ == "__main__":
    main()
