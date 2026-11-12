"""General native Origin choice-schema audit; no grants or auto-selection.

Separates persistent creation choices, source-selection controls, open catalog
entitlements, player allocations, derived bindings, and use-time options.
Preserves complete authored definitions and source occurrence provenance.
No Species-specific dispatch or assumption that a trait is currently granted.

This is read-only schema evidence. It is NOT a trusted character creator, a
complete native choice grammar, or a proof of choice/trait eligibility.
"""
import argparse
import copy
import json

from character_origins_source_inventory import read_catalog, source_lineage


class ChoiceSchemaError(ValueError):
    """Malformed or ambiguous choice source evidence."""


def _nonblank(value):
    return isinstance(value, str) and bool(value.strip())


def _ensure_json(value):
    try:
        json.dumps(value, ensure_ascii=False, sort_keys=True, allow_nan=False)
    except (ValueError, TypeError) as exc:
        raise ChoiceSchemaError("Choice source must contain JSON values") from exc


def _finite_options(values):
    return (
        isinstance(values, list)
        and bool(values)
        and all(_nonblank(x) for x in values)
        and len(values) == len(set(values))
    )


def _note(source_id, authored_id, kind, field, value, timing,
          status, scope, constraints=None):
    return {
        "choiceEvidenceId": f"{authored_id}#choice:{field}",
        "authoredOccurrenceId": authored_id,
        "sourceId": source_id,
        "kind": kind,
        "authoredField": field,
        "authoredValue": copy.deepcopy(value),
        "timing": timing,
        "choiceScope": scope,
        "constraints": copy.deepcopy(constraints or {}),
        "schemaStatus": status,
        "selection": "not-selected",
        "authority": "not-authorized",
    }


def _trait_choices(source_id, trait):
    if not isinstance(trait, dict) or not _nonblank(trait.get("id")):
        raise ChoiceSchemaError("Invalid native trait definition")
    identity = f"{source_id}#trait:{trait['id']}"
    notes = []
    observed = set()

    def add(kind, field, timing, status, constraints=None):
        notes.append(_note(
            source_id, identity, kind, field, trait[field], timing,
            status, "trait-instance", constraints,
        ))
        observed.add(field)

    if "chooseFrom" in trait:
        value = trait["chooseFrom"]
        if not (_nonblank(value) or _finite_options(value)):
            raise ChoiceSchemaError(f"Malformed chooseFrom: {identity}")
        count = trait.get("choiceCount")
        if type(count) is not int or count < 1:
            raise ChoiceSchemaError(
                f"Missing or invalid choiceCount: {identity}"
            )
        add("catalog-entitlement" if isinstance(value, str)
            else "finite-choice", "chooseFrom", trait.get(
                "when", "timing-unspecified"
            ), "requires-catalog-resolution" if isinstance(value, str)
            else "finite-options-representable",
            {"count": count})
        observed.add("choiceCount")

    if "featCategory" in trait:
        if "chooseFrom" in trait:
            raise ChoiceSchemaError(
                "Ambiguous chooseFrom and featCategory in one trait"
            )
        if not _nonblank(trait["featCategory"]) or type(
            trait.get("choiceCount")
        ) is not int or trait["choiceCount"] < 1:
            raise ChoiceSchemaError(
                f"Invalid feat category or choice count: {identity}"
            )
        add("catalog-entitlement", "featCategory", trait.get(
            "when", "timing-unspecified"
        ), "requires-catalog-resolution",
            {"count": trait["choiceCount"], "catalogType": "feat"})
        observed.add("choiceCount")

    if "spellList" in trait and "spell" not in trait:
        if not _nonblank(trait["spellList"]):
            raise ChoiceSchemaError(f"Invalid spellList: {identity}")
        add("spell-from-list", "spellList", "timing-unspecified",
            "requires-catalog-resolution", {
                "count": 1,
                "replacement": copy.deepcopy(trait.get("replacement")),
            })
        if "replacement" in trait:
            observed.add("replacement")

    if "allocation" in trait and trait["allocation"] == "player-choice":
        add("player-allocation", "allocation", "timing-unspecified",
            "requires-allocation-policy", {
                "additionalRanksAtCharacterLevel1": copy.deepcopy(
                    trait.get("additionalRanksAtCharacterLevel1")
                ),
                "additionalRanksPerSubsequentCharacterLevel": copy.deepcopy(
                    trait.get("additionalRanksPerSubsequentCharacterLevel")
                ),
                "obeysNormalSkillRankLimits": copy.deepcopy(
                    trait.get("obeysNormalSkillRankLimits")
                ),
            })

    if "abilityChoice" in trait:
        choice = trait["abilityChoice"]
        if not isinstance(choice, dict) or not _finite_options(
            choice.get("from")
        ):
            raise ChoiceSchemaError(
                f"Invalid abilityChoice options: {identity}"
            )
        add("finite-ability-choice", "abilityChoice", choice.get(
            "when", "timing-unspecified"
        ), "finite-options-representable", {
            "options": copy.deepcopy(choice["from"]),
            "perCharacter": copy.deepcopy(choice.get("perCharacter")),
            "count": 1,
        })

    if "options" in trait:
        values = trait["options"]
        if not isinstance(values, list) or not values or any(
            not isinstance(item, dict) or not _nonblank(item.get("id"))
            for item in values
        ) or len({x["id"] for x in values}) != len(values):
            raise ChoiceSchemaError(
                f"Invalid authored effect options: {identity}"
            )
        timing = trait.get("choiceTiming", "timing-unspecified")
        add("effect-option", "options", timing,
            "use-time-option" if timing == "on-use"
            else "timing-or-selection-policy-unresolved", {
                "optionIds": [x["id"] for x in values],
                "count": 1,
            })
        observed.add("choiceTiming")

    if "spellcastingAbilityFrom" in trait:
        value = trait["spellcastingAbilityFrom"]
        if not _nonblank(value):
            raise ChoiceSchemaError(
                f"Invalid dependent ability reference: {identity}"
            )
        add("derived-parameter-binding", "spellcastingAbilityFrom",
            "derived-from-other-selection", "source-binding-unresolved")

    potential = {
        key for key in trait
        if ("choice" in key.lower() or "choose" in key.lower()
            or key in ("featCategory", "spellList", "allocation",
                       "replacement", "options"))
    }
    if "choiceCount" in trait and "choiceCount" not in observed:
        potential.add("choiceCount")
    for field in sorted(potential - observed):
        notes.append(_note(
            source_id, identity, "unmodeled-choice-field", field,
            trait[field], "unknown", "requires-schema-review",
            "trait-instance",
        ))
    return notes


def _source_choices(source):
    sid = source["id"]
    records = []

    if "sizeChoice" in source:
        choice = source["sizeChoice"]
        if not isinstance(choice, dict) or not _finite_options(
            choice.get("from")
        ):
            raise ChoiceSchemaError(f"Invalid native size choice: {sid}")
        records.append(_note(
            sid, f"{sid}#intrinsic:sizeChoice", "intrinsic-choice",
            "sizeChoice", choice, choice.get("when", "timing-unspecified"),
            "finite-options-representable", "source-configuration",
            {"slotId": "creature.size",
             "options": copy.deepcopy(choice["from"]), "count": 1},
        ))

    if "spellcastingAbilityChoice" in source:
        values = source["spellcastingAbilityChoice"]
        if not _finite_options(values):
            raise ChoiceSchemaError(
                f"Invalid native spellcasting ability choices: {sid}"
            )
        records.append(_note(
            sid, f"{sid}#intrinsic:spellcastingAbilityChoice",
            "finite-ability-choice", "spellcastingAbilityChoice", values,
            "timing-unspecified", "finite-options-representable",
            "source-configuration", {"options": copy.deepcopy(values),
                                     "count": 1},
        ))

    if "ancestrySelection" in source:
        selection = source["ancestrySelection"]
        if not isinstance(selection, dict) or not _finite_options(
            selection.get("selectOneFrom")
        ) or type(selection.get("required")) is not bool:
            raise ChoiceSchemaError(
                f"Invalid native ancestry selection: {sid}"
            )
        records.append(_note(
            sid, f"{sid}#control:ancestrySelection",
            "source-selection-control", "ancestrySelection",
            selection, "source-selection",
            "separate-from-inherent-characteristics",
            "source-configuration", {
                "count": 1 if selection["required"] else 0,
                "options": copy.deepcopy(selection["selectOneFrom"]),
            },
        ))
    return records


def audit_native_choice_schema(catalog, source_ids=None):
    """Inventory native choice evidence, optionally by direct-source lineage.

    The audit is about authored content, not which candidate traits a user
    selects or whether a mixed contributor can pass a particular benefit.
    """
    if not isinstance(catalog, dict):
        raise ChoiceSchemaError("Catalog must be an object")
    if source_ids is None:
        selected = [catalog[k] for k in sorted(catalog)]
    else:
        if not isinstance(source_ids, list) or not source_ids:
            raise ChoiceSchemaError("Expected nonempty direct source selection")
        selected = []
        seen = set()
        for source_id in source_ids:
            for source in source_lineage(source_id, catalog):
                if source["id"] not in seen:
                    seen.add(source["id"])
                    selected.append(source)
    records = []
    counts = {}
    for source in selected:
        sid = source["id"]
        if not _nonblank(sid) or catalog.get(sid) != source:
            raise ChoiceSchemaError("Invalid authored source record")
        for record in _source_choices(source):
            records.append(record)
        for trait in source.get("traits", []):
            records.extend(_trait_choices(sid, trait))
    for note in records:
        counts[note["kind"]] = counts.get(note["kind"], 0) + 1
    if len({n["choiceEvidenceId"] for n in records}) != len(records):
        raise ChoiceSchemaError("Duplicate choice occurrence identity")
    _ensure_json(records)
    return {
        "choiceObservations": records,
        "countsByKind": counts,
        "sourceIds": [s["id"] for s in selected],
        "observationCount": len(records),
        "unmodeledEvidenceIds": [
            n["choiceEvidenceId"] for n in records
            if n["schemaStatus"] in (
                "requires-schema-review",
                "source-binding-unresolved",
                "timing-or-selection-policy-unresolved",
            )
        ],
        "unresolvedTimingEvidenceIds": [
            n["choiceEvidenceId"] for n in records
            if n["timing"] in ("timing-unspecified", "unknown")
        ],
        "status": "origin-native-choice-schema-evidence-only",
        "authorization": "not-authorized",
        "completeness": "not-certified",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Inspect native Species and Ancestry choice authoring"
    )
    parser.add_argument(
        "--sources", nargs="+",
        help="Restrict to selected standard Species/Ancestry source lineages",
    )
    parser.add_argument("--summary", action="store_true")
    args = parser.parse_args()
    report = audit_native_choice_schema(read_catalog(), args.sources)
    if args.summary:
        report.pop("choiceObservations")
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
