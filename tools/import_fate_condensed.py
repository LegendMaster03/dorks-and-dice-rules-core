"""Reproduce the native Fate catalog from the preserved, pinned CC BY source."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "data/reference/fate-condensed/Fate-Condensed-SRD-CC-BY.md"
TARGET = ROOT / "data/rulesets/fate-condensed/native/sections.json"
COMMIT = "93ed848e723f31b4994b466b7241011ba24be885"
BLOB = "cc313c53ee75939e1f5c53f0f2ee59181e78ee00"
PATH = "docs/markdown/Fate-Condensed-SRD-CC-BY.md"
ATTRIBUTION = "This work is based on Fate Condensed (found at https://fate-srd.com/fate-condensed), a product of Evil Hat Productions, LLC, developed, authored, and edited by PK Sullivan, Lara Turner, Fred Hicks, Richard Bellingham, Robert Hanz, and Sophie Lagacé, and licensed for our use under the Creative Commons Attribution 3.0 Unported license (https://creativecommons.org/licenses/by/3.0/)."


def convert():
    raw = SOURCE.read_bytes()
    digest = hashlib.sha1(b"blob " + str(len(raw)).encode() + b"\0" + raw).hexdigest()
    if digest != BLOB:
        raise ValueError("Fate source differs from pinned Git blob")
    source = raw.decode("utf-8")
    matches = list(re.finditer(r"^(#{1,6}) ([^\r\n]+)", source, re.M))
    stack, records = [], []
    for i, match in enumerate(matches):
        level = len(match[1])
        name = re.sub(r"<a\b[^>]*></a>", "", match[2]).strip()
        slug = re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-")
        while stack and stack[-1][0] >= level:
            stack.pop()
        entity_id = "section." + ".".join([entry[1] for entry in stack] + [slug])
        end = matches[i + 1].start() if i + 1 < len(matches) else len(source)
        records.append(dict(id=entity_id, name=name, kind="chapter" if level == 1 else "rule",
                            headingLevel=level, parentId=stack[-1][2] if stack else None,
                            markdown=source[match.start():end],
                            source=dict(commit=COMMIT, path=PATH,
                                        line=source[:match.start()].count("\n") + 1,
                                        license="CC-BY-3.0", attribution=ATTRIBUTION,
                                        url=f"https://github.com/fate-srd/fate-srd-content/blob/{COMMIT}/{PATH}")))
        stack.append((level, slug, entity_id))
    if len({r["id"] for r in records}) != len(records):
        raise ValueError("Duplicate section IDs")
    if "".join(r["markdown"] for r in records) != source:
        raise ValueError("Source text lost during segmentation")
    return records


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    records = convert()
    if args.check:
        if json.loads(TARGET.read_text(encoding="utf-8")) != records:
            raise SystemExit("Generated Fate catalog is stale")
    else:
        TARGET.write_text(json.dumps(records, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Fate catalog: {len(records)} sections")
