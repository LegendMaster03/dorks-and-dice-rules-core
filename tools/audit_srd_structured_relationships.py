"""Inventory structured SRD relationships without interpreting them as earned grants.

Preserve field paths, choice expressions, mode, and level context. Resolve only
literal edition-local identities; never flatten a choice into unconditional grants.
"""
import argparse
from collections import Counter
import json
from pathlib import Path

from audit_srd_native_references import Index
from report_srd_promotion import REPO, PACKAGES, native_rows

ORIGIN_KINDS = {"class", "subclass", "class-feature", "subclass-feature",
                "race", "subrace", "species", "background", "feat", "optional-feature"}
MODES = {"known", "prepared", "innate", "expanded"}


def part(key):
    return str(key).replace("~", "~0").replace("/", "~1")


def children(value, path):
    if isinstance(value, dict):
        for key, child in value.items():
            yield key, child, path + "/" + part(key)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield index, child, path + "/" + str(index)


def audit(rows, tag):
    index = Index(rows, tag)
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
                emit(path, relation, raw, "unreviewed-shape", **context)
                return
            # Spell UIDs can have a cast-level or cantrip suffix; preserve it
            # separately rather than confusing cast level with spell identity.
            uid, sep, suffix = raw.partition("#") if kind == "spell" else (raw, "", "")
            if sep:
                context["castQualifier"] = suffix
            status, targets, variant = index.resolve(kind, uid)
            emit(path, relation, raw, status, targets, variant=variant, **context)

        def spell_tree(value, path, mode):
            if isinstance(value, str):
                literal(path, "spell-grant", "spell", value, grantMode=mode)
            elif isinstance(value, dict) and ("choose" in value or "all" in value):
                # Filters are authored selectors, not lists of awarded spells.
                emit(path, "spell-selector", value, "selector-review", grantMode=mode)
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
                    emit(path, "equipment-selector-or-value", value, "selector-review")
                else:
                    for _, child, child_path in children(value, path):
                        equipment(child, child_path)
            elif isinstance(value, list):
                for _, child, child_path in children(value, path):
                    equipment(child, child_path)

        def proficiency(value, path, kind):
            if not isinstance(value, dict):
                emit(path, "proficiency", value, "unreviewed-shape", proficiencyKind=kind)
                return
            for key, child, child_path in children(value, path):
                if key == "choose" and isinstance(child, dict):
                    emit(child_path, "proficiency-selector", child, "selector-review", proficiencyKind=kind)
                    for i, target in enumerate(child.get("from", [])):
                        literal(child_path + "/from/" + str(i), "proficiency-choice-member", kind, target)
                elif key.startswith("any") or key in {"simple", "martial", "light", "medium", "heavy", "shield"}:
                    emit(child_path, "proficiency-selector", {key: child}, "selector-review", proficiencyKind=kind)
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
                            emit(target_path, "prerequisite-selector", target, "selector-review")
                        else:
                            literal(target_path, "prerequisite", kind, target)
                elif key == "pact":
                    literal(child_path, "prerequisite", "optionalfeature", "Pact of the " + str(child))
                elif key == "level" and isinstance(child, dict) and isinstance(child.get("class"), dict):
                    cls = child["class"]
                    raw = cls.get("name", "") + ("|" + cls["source"] if cls.get("source") else "")
                    literal(child_path + "/class", "prerequisite", "class", raw)
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
                                emit(grant_path, "feat-selector", flag, "selector-review")
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
                        proficiency(grant, child_path + "/" + str(i), kind)
                elif key == "optionalfeatureProgression":
                    emit(child_path, "optional-feature-progression", child, "selector-review")
                elif key == "type" and child == "refOptionalfeature":
                    literal(path + "/optionalfeature", "optional-feature-choice-member",
                            "optionalfeature", value.get("optionalfeature"))
                else:
                    visit(child, child_path)

        visit(row, "")
    counts = Counter(o["status"] for o in result)
    relation_counts = Counter(o["relation"] for o in result)
    return {"status": "diagnostic-only", "editionTag": tag,
            "scope": "Structured grants, prerequisites, equipment and proficiency fields in character options; selectors and constraints remain unexecuted.",
            "counts": dict(sorted(counts.items())),
            "relationCounts": dict(sorted(relation_counts.items())),
            "explicitLiteralLinks": sum(o["alreadyExplicit"] for o in result),
            "occurrences": result}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = {}
    for edition, package in PACKAGES.items():
        rows = list(native_rows(REPO / "data" / "rulesets" / package / "native"))
        report[edition] = audit(rows, "SRD51" if edition == "srd-5.1" else "SRD52")
        print(edition, report[edition]["counts"], "explicit", report[edition]["explicitLiteralLinks"])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
