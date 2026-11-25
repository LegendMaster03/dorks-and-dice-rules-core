"""Compare promoted native SRD prose to the pinned publisher PDF.

Unlike the historical reference-pack audit, this inventories only native records.
Exact matches are text evidence, not certification of rule semantics or rights.
No generated content, publisher page text or provenance is rewritten.
"""
import argparse
from collections import Counter, defaultdict
from difflib import SequenceMatcher
import hashlib
import json
from pathlib import Path
import re
import subprocess
import unicodedata

from report_srd_promotion import REPO, PACKAGES, native_rows

TOKEN = re.compile(r"\d+d\d+|\d+(?:\.\d+)?|[a-z]+(?:'[a-z]+)?|[+−-](?=\d)", re.I)
TAG = re.compile(r"\{@([A-Za-z]+)(?: ([^{}]*))?\}")
DISPLAY_TAGS = {"spell", "creature", "condition", "skill", "item", "feat", "action",
                "sense", "language", "class", "race", "optfeature", "optionalfeature",
                "status", "table", "variantrule", "itemproperty", "itemmastery",
                "object", "hazard", "vehicle", "deity", "trap"}
TEXT_TAGS = {"b", "bold", "i", "italic", "u", "s", "strike", "sup", "sub"}
BODY_FIELDS = ("entries", "entriesHigherLevel", "trait", "action", "bonus",
               "reaction", "legendary", "legendaryActions", "legendaryActionsLair",
               "spellcasting", "description", "text", "rows", "colLabels",
               "entry", "entriesTemplate", "startingEquipment")
SKIP_NESTED = {"source", "catalogReferences", "type", "data", "style", "colStyles",
               "rowStyles", "width", "roll", "id", "page", "ref", "name"}
EXCLUDE_KINDS = {"rulebook-page", "metadata"}


def render(value):
    """Conservative display expansion; preserve unknown tags for manual review."""
    unresolved = set()
    def substitute(match):
        kind = match[1].lower()
        parts = (match[2] or "").split("|")
        if kind in DISPLAY_TAGS:
            return parts[2] if len(parts) > 2 and parts[2] else parts[0]
        if kind in TEXT_TAGS or kind in {"dice", "damage", "dc", "d20", "chance"}:
            return parts[0]
        if kind in {"scaledice", "scaledamage"} and len(parts) >= 3:
            return parts[2]
        if kind in {"filter", "book", "link"}:
            return parts[0]
        if kind == "hit":
            return ("+" if parts[0].isdigit() else "") + parts[0]
        if kind == "h":
            return "Hit: "
        if kind == "atk" or kind == "atkr":
            mapping = {"m": "Melee Attack Roll", "r": "Ranged Attack Roll",
                       "mw": "Melee Weapon Attack", "rw": "Ranged Weapon Attack",
                       "ms": "Melee Spell Attack", "rs": "Ranged Spell Attack",
                       "m,r": "Melee or Ranged Attack Roll",
                       "mw,rw": "Melee or Ranged Weapon Attack"}
            if parts[0] in mapping:
                return mapping[parts[0]] + ":"
        if kind == "recharge":
            return "Recharge " + (parts[0] + "-6" if parts[0] else "6")
        unresolved.add(kind)
        return match[0]
    for _ in range(5):
        next_value = TAG.sub(substitute, value)
        if next_value == value:
            break
        value = next_value
    if "{@" in value:
        unresolved.add("unrendered-markup")
    if "{=" in value:
        unresolved.add("template-variable")
    return value, sorted(unresolved)


def tokens(value, pdf=False):
    value = unicodedata.normalize("NFKC", str(value)).replace("\u00ad", "")
    value = value.replace("’", "'").replace("‘", "'").replace("−", "-")
    if pdf:
        value = re.sub(r"(?<=[a-zA-Z])-\s*\n\s*(?=[a-zA-Z])", "", value)
    # Dice formula spaces and thousands separators are typographic, not rule changes.
    value = re.sub(r"(?<=\d),(?=\d{3}\b)", "", value)
    value = re.sub(r"(?<=\d)\s*d\s*(?=\d)", "d", value, flags=re.I)
    return [m.group().lower() for m in TOKEN.finditer(value)]


def fragments(row):
    """Yield real text leaves and table cells, not identifiers or metadata."""
    def walk(value, path, table=False):
        if isinstance(value, str) or table and type(value) in (int, float):
            yield path, str(value), table
        elif isinstance(value, list):
            for i, child in enumerate(value):
                yield from walk(child, path + "/" + str(i), table)
        elif isinstance(value, dict):
            for key, child in value.items():
                if key in SKIP_NESTED:
                    continue
                # Equipment includes structured UIDs and amounts; only compare
                # display prose here. The relationship audit handles the rest.
                if path.startswith("/startingEquipment") and key not in {"entries", "default"}:
                    continue
                yield from walk(child, path + "/" + key, table or key in {"rows", "colLabels"})
    for key in BODY_FIELDS:
        if key in row:
            yield from walk(row[key], "/" + key, key in {"rows", "colLabels"})
    if isinstance(row.get("inherits"), dict):
        for key in ("entries", "entriesTemplate"):
            if key in row["inherits"]:
                yield from walk(row["inherits"][key], "/inherits/" + key)


def spell_body(row):
    """Compare the complete spell prose sequence, excluding casting headers.

    Duration has no continuation in the reviewed spell extractor's format.
    Higher-level/upgrade headings are emitted from named entry containers.
    """
    layout = row.get("officialTextLayout", "")
    lines = layout.splitlines()
    starts = [i for i, line in enumerate(lines) if line.strip().startswith("Duration:")]
    if len(starts) != 1:
        return {"status": "missing-or-ambiguous-duration-header"}
    publisher = tokens("\n".join(lines[starts[0] + 1:]), pdf=True)
    unresolved = set()
    def flatten(node):
        if isinstance(node, str):
            value, unknown = render(node)
            unresolved.update(unknown)
            yield value
        elif isinstance(node, list):
            for child in node:
                yield from flatten(child)
        elif isinstance(node, dict):
            if isinstance(node.get("name"), str):
                yield from flatten(node["name"])
            for key in ("caption", "colLabels", "entries", "entry", "items", "rows"):
                if key in node:
                    yield from flatten(node[key])
        elif type(node) in (int, float):
            yield str(node)
    structured = tokens(" ".join(flatten([row.get("entries", []), row.get("entriesHigherLevel", [])])))
    matcher = SequenceMatcher(None, publisher, structured, autojunk=False)
    differences = [{"operation": op, "publisherOffset": i, "nativeOffset": k,
                    "publisherTokens": publisher[i:j], "nativeTokens": structured[k:l]}
                   for op, i, j, k, l in matcher.get_opcodes() if op != "equal"]
    return {"status": "markup-review" if unresolved else "exact-normalized-text" if not differences else "text-difference",
            "publisherTokenCount": len(publisher), "nativeTokenCount": len(structured),
            "unrenderedTags": sorted(unresolved), "differences": differences,
            "similarity": round(matcher.ratio(), 4)}


class PageIndex:
    def __init__(self, pages):
        self.pages = {n: tokens(text, pdf=True) for n, text in pages.items()}
        self.text = {n: " " + " ".join(t) + " " for n, t in self.pages.items()}
        self.grams = defaultdict(set)
        for n, sequence in self.pages.items():
            for gram in self.shingles(sequence):
                self.grams[gram].add(n)

    @staticmethod
    def shingles(sequence):
        return set(zip(sequence, sequence[1:], sequence[2:], sequence[3:]))

    def compare(self, sequence, locators):
        if not sequence:
            return {"status": "empty-display"}
        preferred = {n + delta for n in locators for delta in (-1, 0, 1)} & self.pages.keys()
        needle = " " + " ".join(sequence) + " "
        grams = self.shingles(sequence)
        scores = Counter(n for gram in grams for n in self.grams.get(gram, ()))
        # Short cells cannot use shingles. Match within named source pages only;
        # a common digit somewhere in the book is not useful provenance.
        candidates = preferred if len(sequence) < 4 else preferred | set(scores)
        exact = [n for n in sorted(candidates) if needle in self.text[n]]
        if exact:
            local = sorted(set(exact) & preferred)
            return {"status": "exact-located-text" if local else "exact-elsewhere",
                    "pages": local or exact[:10], "matchingPageCount": len(exact)}
        if not grams:
            return {"status": "short-fragment-review", "pages": sorted(preferred)}
        best = sorted(scores, key=lambda n: (-scores[n], n not in preferred, n))[:3]
        ratio = round(scores[best[0]] / len(grams), 4) if best else 0
        return {"status": "partial-text-match" if ratio >= 0.5 else "low-overlap-review",
                "pages": best, "shingleOverlap": ratio}


def audit(rows, pages):
    index = PageIndex(pages)
    summaries = defaultdict(Counter)
    spell_counts = Counter()
    records = []
    for row in rows:
        if row["kind"] in EXCLUDE_KINDS:
            continue
        locators = row.get("source", {}).get("pages", [])
        record = {"id": row["id"], "kind": row["kind"], "sourcePages": locators,
                  "fragments": []}
        if row["kind"] == "spell":
            body = spell_body(row)
            record["spellBody"] = body
            spell_counts[body["status"]] += 1
        for pointer, text, table in fragments(row):
            display, unknown = render(text)
            result = index.compare(tokens(display), locators)
            if unknown:
                result["unrenderedTags"] = unknown
                result["matchStatus"] = result["status"]
                result["status"] = "markup-review"
            summaries[row["kind"]][result["status"]] += 1
            record["fragments"].append({"pointer": pointer, "tableCellOrLabel": table,
                                        "text": text, **result})
        if not record["fragments"]:
            summaries[row["kind"]]["no-inventoried-prose"] += 1
        records.append(record)
    return {"status": "diagnostic-only-not-semantic-certification",
            "nativeRecords": len(records),
            "spellBodyCounts": dict(sorted(spell_counts.items())),
            "fragmentCountsByKind": {kind: dict(sorted(counts.items())) for kind, counts in sorted(summaries.items())},
            "records": records}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("edition", choices=PACKAGES)
    parser.add_argument("pdf", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    base = REPO / "data" / "rulesets" / PACKAGES[args.edition]
    manifest = json.loads((base / "manifest.json").read_text(encoding="utf-8"))
    if hashlib.sha256(args.pdf.read_bytes()).hexdigest() != manifest["officialPageImport"]["pdfSha256"]:
        raise SystemExit("Publisher PDF differs from pinned source")
    raw = subprocess.check_output(["pdftotext", "-raw", "-enc", "UTF-8", str(args.pdf), "-"]).decode("utf-8")
    pages = {i: p for i, p in enumerate(raw.split("\f"), 1) if p.strip()}
    result = audit(list(native_rows(base / "native")), pages)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(args.edition, "native records", result["nativeRecords"], "spell bodies", result["spellBodyCounts"])
    print("Native fragment comparison by kind:", json.dumps(result["fragmentCountsByKind"], sort_keys=True))
    # Compact actionable output: show all spell diffs with a bounded preview;
    # the complete token diff and exact source pointers remain in the artifact.
    for row in result["records"]:
        body = row.get("spellBody")
        if body and body["status"] != "exact-normalized-text":
            preview = [{"op": d["operation"], "publisher": " ".join(d["publisherTokens"][:12]),
                        "native": " ".join(d["nativeTokens"][:12])} for d in body.get("differences", [])[:3]]
            print("SPELL_REVIEW", row["id"], body["status"], body.get("similarity"), json.dumps(preview, ensure_ascii=False))


if __name__ == "__main__":
    main()
