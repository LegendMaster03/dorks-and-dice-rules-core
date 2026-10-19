#!/usr/bin/env python3
"""Triage official SRD PDF prose versus pinned structured source text.

This comparison reports *candidates* for human review. PDF column order, tables,
SRD markup, templated entities, and headings prevent it from certifying parity.
The pinned source material is never modified.
"""
import argparse
import collections
import json
from pathlib import Path
import re
import subprocess
import unicodedata

SOURCE = Path("data/reference/srd/structured")
EDITION_CODES = {"SRD51": "5.1", "SRD52": "5.2.1"}
SKIP_FILES = {
    "books.json", "README.md", "foundry-actions.json", "foundry-feats.json",
    "foundry-items.json", "foundry-optionalfeatures.json", "foundry-races.json",
}
SKIP_KEYS = {
    "page", "source", "otherSources", "reprintedAs", "srd", "edition",
    "id", "uniqueId", "hash", "path", "href", "credit", "title",
    "soundClip", "hasToken", "foundryTokenScale", "tokenUrl",
}
TOKEN_RE = re.compile(r"[a-z0-9]+(?:'[a-z0-9]+)?", re.ASCII)
TAG_RE = re.compile(r"\{@[a-zA-Z][^\s{}]*\s+([^{}]+)\}")

def normalize(text):
    text = str(text).replace("\u00ad", "")
    for _ in range(3):
        text = TAG_RE.sub(lambda m: m.group(1).split("|", 1)[0], text)
    text = re.sub(r"(?<=\w)-\s*\n\s*(?=\w)", "", text)
    text = unicodedata.normalize("NFKC", text).lower()
    text = (text.replace("\u2019", "'").replace("\u2018", "'")
            .replace("\u2014", " ").replace("\u2013", " "))
    return TOKEN_RE.findall(text)

def shingles(tokens, n=5):
    return set(zip(*(tokens[i:] for i in range(n)))) if len(tokens) >= n else set()

def pdf_pages(pdf):
    result = subprocess.run(
        ["pdftotext", "-raw", "-enc", "UTF-8", str(pdf), "-"],
        capture_output=True, text=True, check=True
    )
    pages = [normalize(x) for x in result.stdout.split("\f")]
    return [p for p in pages if p]

def collect_fragments():
    out = {"5.1": [], "5.2.1": []}
    counts = collections.Counter()
    for path in sorted(SOURCE.rglob("*.json")):
        rel = str(path.relative_to(SOURCE))
        if (path.name in SKIP_FILES or path.name.startswith("foundry")
                or "/foundry" in rel or rel.startswith("generated/")
                or path.name in {"index.json", "fluff-index.json", "sources.json"}):
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        forced = "5.1" if "book-srd51" in rel else (
            "5.2.1" if "book-srd52" in rel else None)
        if "spells-srd51" in rel or "bestiary-srd51" in rel:
            forced = "5.1"
        if "spells-srd52" in rel or "bestiary-srd52" in rel:
            forced = "5.2.1"
        def walk(node, edition, pointer):
            if isinstance(node, dict):
                local = EDITION_CODES.get(node.get("source"), edition)
                for key, value in node.items():
                    if key in SKIP_KEYS or key.startswith("_"):
                        continue
                    walk(value, local, pointer + "/" + key)
            elif isinstance(node, list):
                for index, item in enumerate(node):
                    walk(item, edition, pointer + "/" + str(index))
            elif isinstance(node, str) and edition in out:
                tokens = normalize(node)
                if len(tokens) >= 12:
                    out[edition].append({
                        "file": rel, "pointer": pointer, "tokens": tokens,
                        "excerpt": node[:180],
                    })
                    counts[rel] += 1
        walk(data, forced, "")
    return out, counts

def score_fragments(fragments, pdf_set, cutoff=0.35):
    suspicious = []
    source_grams = set()
    scored = 0
    for row in fragments:
        grams = shingles(row["tokens"])
        if not grams:
            continue
        source_grams.update(grams)
        hits = len(grams.intersection(pdf_set))
        ratio = round(hits / len(grams), 4)
        scored += 1
        if len(row["tokens"]) >= 24 and ratio < cutoff:
            suspicious.append({
                "file": row["file"], "pointer": row["pointer"],
                "ratio": ratio, "tokens": len(row["tokens"]),
                "excerpt": row["excerpt"],
            })
    suspicious.sort(key=lambda x: (x["ratio"], -x["tokens"], x["file"]))
    return suspicious, source_grams, scored

def audit(edition, path, rows):
    pages = pdf_pages(path)
    official_grams = set()
    for page in pages:
        official_grams.update(shingles(page))
    suspicious, source_grams, scored = score_fragments(rows, official_grams)
    page_coverage = []
    for index, page in enumerate(pages, 1):
        page_grams = shingles(page)
        if not page_grams:
            continue
        hits = len(page_grams.intersection(source_grams))
        page_coverage.append({
            "pdf_page": index, "tokens": len(page),
            "ratio": round(hits / len(page_grams), 4),
            "matched": hits, "total": len(page_grams)
        })
    overall = sum(x["matched"] for x in page_coverage)
    total = sum(x["total"] for x in page_coverage)
    worst = sorted(page_coverage, key=lambda x: x["ratio"])[:50]
    return {
        "edition": edition,
        "status": "triage-only-not-completeness-certification",
        "pdf_pages_analyzed": len(pages),
        "source_fragments_scored": scored,
        "low_similarity_fragments": len(suspicious),
        "page_5gram_match_ratio": round(overall / total, 4) if total else None,
        "lowest_coverage_pages": worst,
        "low_similarity_examples": suspicious[:250],
        "per_file_low_similarity": dict(collections.Counter(
            x["file"] for x in suspicious).most_common()),
    }

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--srd51", type=Path, required=True)
    parser.add_argument("--srd52", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    rows, counts = collect_fragments()
    report = {
        "method": "5-token shingle overlap of normalized official PDF text and original staged JSON text",
        "limitations": [
            "PDF reading order and split columns can lower apparent overlap.",
            "Mechanical fields represented structurally may not share literal text.",
            "The method can miss one-word substitutions, wrong numbers, missing short tables, and omitted stat blocks.",
            "Similarity is triage evidence, never proof of substantive equality.",
            "Generated metadata, Foundry enrichments, and provenance require separate checks.",
        ],
        "source_files_with_text": len(counts),
        "results": [
            audit("5.1", args.srd51, rows["5.1"]),
            audit("5.2.1", args.srd52, rows["5.2.1"]),
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n",
                           encoding="utf-8")
    for result in report["results"]:
        print(f"SRD {result['edition']}: PDF pages={result['pdf_pages_analyzed']}, "
              f"source fragments={result['source_fragments_scored']}, "
              f"low-similarity fragments={result['low_similarity_fragments']}, "
              f"5-gram overlap={result['page_5gram_match_ratio']:.1%}")
        print("Lowest-similarity PDF pages:", [
            (x["pdf_page"], x["ratio"]) for x in result["lowest_coverage_pages"][:20]])
        print("Largest unmatched-source contributors:",
              list(result["per_file_low_similarity"].items())[:20])

if __name__ == "__main__":
    main()
