"""Read the import evidence used by the offline SRD audits."""
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]


def read_index(package):
    return json.loads((REPO / "data/reference/srd" / f"{package}-import-sources.json")
                      .read_text(encoding="utf-8"))


def with_import_source(row, evidence):
    metadata = evidence.get(row["id"])
    if metadata is None:
        return row
    source = {**row["source"], **metadata}
    if "spellList" in row["source"] and "spellList" in metadata:
        source["spellList"] = {**row["source"]["spellList"], **metadata["spellList"]}
    return {**row, "source": source}


def read_native_shards(native, pattern):
    index = read_index(native.parent.name)
    return [with_import_source(row, index.get(path.relative_to(native).as_posix(), {}))
            for path in sorted(native.glob(pattern))
            for row in json.loads(path.read_text(encoding="utf-8"))]
