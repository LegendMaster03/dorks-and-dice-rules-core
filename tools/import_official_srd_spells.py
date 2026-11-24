"""Extract named spell records directly from pinned publisher PDF columns.

The structured reference pack supplies identity comparison only, not spell
bodies. The PDF is authoritative for content and table layout. Unmatched
headings are reported; the extractor must not silently substitute pack text.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import unicodedata

ROOT = Path(__file__).resolve().parents[1]
SCHOOLS = {"abjuration": "A", "conjuration": "C", "divination": "D",
           "enchantment": "E", "evocation": "V", "illusion": "I",
           "necromancy": "N", "transmutation": "T"}
SPECS = {
    "srd-5.1": ("dnd-5e", 114, 194, "51"),
    "srd-5.2.1": ("dnd-5-5e", 107, 175, "52"),
}
HEADER = re.compile(r"(?i)^(?:Level\s+([1-9])\s+([a-z]+)|([1-9])(?:st|nd|rd|th)[\s-]*level\s+([a-z]+)|([a-z]+)\s+cantrip)\b")


def normalized(value):
    value = unicodedata.normalize("NFKC", value)
    value = value.replace("’", "'").replace("‘", "'").replace("–", "-").replace("—", "-")
    value = value.replace("\u00ad", "").replace("\u2010", "-").replace("\u2011", "-")
    return re.sub(r"\s+", " ", value).strip()


def clean_line(line):
    return (line.replace("\t\r \u00a0", " ").replace("\t\r", " ")
            .replace("\u00a0", " ").replace("\t", "    ").replace("\r", "")
            .replace("\u00ad", "").replace("\u2010", "-").replace("\u2011", "-")
            .replace("\u2028", " ").replace("\u2029", " ").rstrip())


def slug(name):
    return re.sub(r"[^a-z0-9]+", "-", normalized(name).lower()).strip("-")


def header(line):
    value = normalized(line)
    match = HEADER.match(value)
    if not match:
        return None
    a, b, c, d, e = match.groups()
    school = (b or d or e).lower()
    if school not in SCHOOLS:
        return None
    return int(a or c or 0), SCHOOLS[school]


def columns(pdf, first, last):
    info = subprocess.check_output(["pdfinfo", "-f", str(first), "-l", str(last), str(pdf)], text=True)
    sizes = re.findall(r"Page\s+\d+\s+size:\s+([\d.]+)\s+x\s+([\d.]+)\s+pts", info)
    if not sizes:
        sizes = re.findall(r"Page size:\s+([\d.]+)\s+x\s+([\d.]+)\s+pts", info)
    if not sizes or len(set(sizes)) != 1:
        raise ValueError("Unexpected variable spell-page dimensions")
    width, height = map(float, sizes[0])
    midpoint = round(width / 2)
    # pdftotext crops by word bounding box; use the central gutter and omit
    # the page footer. The page reference remains available for visual review.
    for number in range(first, last + 1):
        for side, x in enumerate((0, midpoint)):
            raw = subprocess.check_output(["pdftotext", "-f", str(number), "-l", str(number),
                "-r", "72", "-x", str(x), "-y", "0", "-W", str(midpoint),
                "-H", str(round(height - 28)), "-layout", "-enc", "UTF-8", str(pdf), "-"]).decode("utf-8")
            for line in raw.replace("\f", "").split("\n"):
                line = clean_line(line)
                if re.search(r"System Reference Document 5\.", line):
                    continue
                yield number, side, line


def parse(lines):
    # Blank lines can separate a 2014 title and school line. The preceding
    # nonblank line is the title; every casting-time header must have a spell.
    lines = list(lines)
    starts = []
    for i, (_, _, line) in enumerate(lines):
        parsed = header(line)
        if parsed is None:
            continue
        j = i - 1
        while j >= 0 and not lines[j][2].strip():
            j -= 1
        if j < 0:
            continue
        # A heading must be followed shortly by Casting Time. This avoids
        # interpreting prose about another spell as a new definition.
        following = [x[2].strip() for x in lines[i+1:i+7] if x[2].strip()]
        if not any(x.startswith("Casting Time:") for x in following):
            continue
        name = lines[j][2].strip()
        if len(name) > 100 or not name:
            raise ValueError(f"Invalid spell heading {name!r}")
        starts.append((j, i, parsed))
    records, malformed = [], []
    for k, (j, i, (level, school)) in enumerate(starts):
        end = starts[k+1][0] if k+1 < len(starts) else len(lines)
        block = lines[j:end]
        name = normalized(block[0][2])
        text = "\n".join(x[2] for x in block).strip()
        metadata = {}
        for field in ("Casting Time", "Range", "Components", "Duration"):
            aliases = (field, "Component") if field == "Components" else (field,)
            hits = [x[2].strip().split(":", 1)[1].strip() for x in block
                    if any(x[2].strip().startswith(alias + ":") for alias in aliases)]
            if len(hits) != 1:
                malformed.append({"name": name, "field": field, "matches": len(hits)})
            else:
                metadata[field] = hits[0]
        records.append({"name": name, "level": level, "school": school,
                        "officialTextLayout": text,
                        "pages": sorted({x[0] for x in block if x[2].strip()}),
                        "header": metadata})
    if len({slug(x["name"]) for x in records}) != len(records):
        raise ValueError("Duplicate extracted spell identities")
    return records, malformed


def build(edition, pdf):
    package, first, last, tag = SPECS[edition]
    manifest = json.loads((ROOT / "data/rulesets" / package / "manifest.json").read_text())
    sha = hashlib.sha256(pdf.read_bytes()).hexdigest()
    if sha != manifest["officialPageImport"]["pdfSha256"]:
        raise ValueError("Publisher PDF differs from pinned source")
    candidates = json.loads((ROOT / f"data/reference/srd/structured/spells/spells-srd{tag}.json").read_text())["spell"]
    expected = {slug(x["name"]): x for x in candidates}
    parsed, malformed = parse(columns(pdf, first, last))
    actual = {slug(x["name"]): x for x in parsed}
    missing = sorted(set(expected) - set(actual))
    additional = sorted(set(actual) - set(expected))
    mismatches = sorted(key for key in set(expected) & set(actual)
                        if (expected[key]["level"], expected[key]["school"]) !=
                        (actual[key]["level"], actual[key]["school"]))
    report = {"edition": edition, "extracted": len(parsed), "candidateCount": len(expected),
              "missingCandidateNames": missing, "additionalOfficialNames": additional,
              "levelOrSchoolDifferences": mismatches, "malformedHeaders": malformed}
    result = []
    source = next(x for x in json.loads((ROOT / "data/reference/srd/manifest.json").read_text())["srds"]
                  if x["id"] == edition)
    for item in parsed:
        pages = item.pop("pages")
        result.append({"id": "spell." + slug(item["name"]), "kind": "spell", **item,
                       "catalogReferences": [f"rulebook.page-{n:03}" for n in pages],
                       "source": {"release": edition, "url": source["officialSource"],
                                  "pdfSha256": sha, "pages": pages, "license": "CC-BY-4.0",
                                  "attribution": manifest["attribution"],
                                  "adaptation": "direct publisher column extraction; layout text, not executable mechanics"}})
    return result, report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("edition", choices=SPECS)
    parser.add_argument("pdf", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--emit-transfer", action="store_true")
    args = parser.parse_args()
    rows, report = build(args.edition, args.pdf)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "report.json").write_text(json.dumps(report, indent=2) + "\n")
    (args.output / "spells.json").write_text(json.dumps(rows, ensure_ascii=False, indent=2) + "\n")
    print("SPELL_IMPORT_REPORT " + json.dumps(report))
    if args.emit_transfer:
        for row in rows:
            print("SPELL_IMPORT_RECORD " + json.dumps(row, ensure_ascii=False, separators=(",", ":")))
