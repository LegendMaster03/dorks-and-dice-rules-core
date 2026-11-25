"""Extract the publisher's CC SRD page text as edition-specific reference rules.

A page record is descriptive content, not a structured spell, class or stat block.
The PDF remains authoritative for typography, column ordering and tables.
Use only the specified official PDFs, never a third-party corpus.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "data/reference/srd/manifest.json"
PACKAGES = {"srd-5.1": ("dnd-5e", 403, 3), "srd-5.2.1": ("dnd-5-5e", 364, 5)}


def extract(edition, pdf):
    package, expected_count, first_gameplay_page = PACKAGES[edition]
    spec = next(x for x in json.loads(SOURCE.read_text(encoding="utf-8"))["srds"] if x["id"] == edition)
    raw = pdf.read_bytes()
    if not raw.startswith(b"%PDF-"):
        raise ValueError("Not a PDF")
    sha = hashlib.sha256(raw).hexdigest()
    output = subprocess.check_output(["pdftotext", "-layout", "-enc", "UTF-8", str(pdf), "-"]).decode("utf-8")
    pages = output.split("\f")
    if not pages[-1].strip():
        pages.pop()
    if len(pages) != expected_count:
        raise ValueError(f"Unexpected page count: {len(pages)}, expected {expected_count}")
    legal = " ".join(pages[0].split())
    if "Creative Commons" not in legal or "4.0" not in legal:
        raise ValueError("Official CC licensing page not found")
    version = edition.removeprefix("srd-")
    # Fail if extraction drifts from the initial publisher snapshot.
    pinned = {
        "srd-5.1": "2504d2a0abb0a4d491a939be4f17910a2dde0312570ab8d208080225ccf0a1f0",
        "srd-5.2.1": "8974902d109d6e63672d7c490bde9ccf052410503d9cfa768237154fbc5e3d87",
    }
    if sha != pinned[edition]:
        raise ValueError("Publisher PDF differs from pinned source")
    records = []
    # The 5.1 text layer uses tab/CR/NBSP sequences between words.
    # Preserve physical newlines, not splitlines(), and normalize only
    # the extractor's nonprinting whitespace. Tabs elsewhere become spaces.
    for number, page in enumerate(pages, 1):
        # Preserve the layout extractor's columns and tables; only trim line ends.
        body = "\n".join(line.replace("\u2028", " ").replace("\u2029", " ")
                         .replace("\u000b", " ").replace("\u0085", " ")
                         .replace("\t\r \u00a0", " ").replace("\t\r", " ")
                         .replace("\u00a0", " ").replace("\t", "    ")
                         .replace("\r", "").rstrip()
                         for line in page.split("\n")).strip("\n")
        # Legal information, front matter and contents remain source material
        # rather than becoming gameplay definitions.
        if number < first_gameplay_page:
            continue
        if not body.strip():
            raise ValueError(f"Empty gameplay page {number}")
        records.append({"id": f"rulebook.page-{number:03}", "kind": "rulebook-page",
                        "name": f"SRD {version} — Page {number:03}", "page": number,
                        "textLayout": body, "representation": "pdf-layout-extract",
                        "source": {"release": edition, "url": spec["officialSource"],
                                   "pageUrl": spec["officialSource"] + f"#page={number}",
                                   "pdfSha256": sha, "license": "CC-BY-4.0",
                                   "attribution": spec["attribution"],
                                   "adaptation": "page text extracted; layout whitespace normalized; no rule changes"}})
    return {"package": package, "edition": edition, "pdfSha256": sha,
            "pdfPages": expected_count, "firstGameplayPage": first_gameplay_page,
            "recordCount": len(records), "records": records}


def write_result(result, output):
    output.mkdir(parents=True, exist_ok=True)
    records = result["records"]
    # Chunk output so native Git files remain modest in size.
    for i in range(0, len(records), 25):
        batch = records[i:i+25]
        path = output / f"pages-{batch[0]['page']:03}-{batch[-1]['page']:03}.json"
        path.write_text(json.dumps(batch, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    metadata = {key: value for key, value in result.items() if key != "records"}
    (output / "source-metadata.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    return metadata


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("edition", choices=PACKAGES)
    parser.add_argument("pdf", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--check", action="store_true",
                        help="Compare generated records with the committed native reference pages")
    args = parser.parse_args()
    result = extract(args.edition, args.pdf)
    metadata = write_result(result, args.output)
    if args.check:
        directory = ROOT / "data" / "rulesets" / result["package"] / "native" / "rulebook"
        committed = []
        for path in sorted(directory.glob("pages-*.json")):
            committed.extend(json.loads(path.read_text(encoding="utf-8")))
        if committed != result["records"]:
            raise ValueError("Committed native page records differ from official extraction")
        metadata["nativePageParity"] = True
    print(json.dumps(metadata, ensure_ascii=False))
