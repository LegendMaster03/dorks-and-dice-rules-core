"""Experimental whole-Origin dependency closure, without authorized grants.

Applies proposed reconciliation dispositions to build a *tentative* set of
characteristics, then evaluates explicitly declared synthetic dependencies
against the whole set. This does not authorize inheritance, DM overrides,
resource sharing, compatibility, balancing, or a final Origin profile.

Required authoring choices and unknown effect semantics stay visible. Source
review flags about unselected alternatives remain separate from selected
profile blockers. No Species-specific branching or auto-inferred constraints.
"""
import copy
import json

from character_origins_reconciliation_decisions import (
    examine_reconciliation_decisions,
)
from character_origins_standard_preset_readiness import _choice_observations


class WholeProfileReviewError(ValueError):
    """Untrusted contract, invalid choice evidence, or bad dependency record."""


def _key(value):
    try:
        return json.dumps(value, sort_keys=True, separators=(",", ":"),
                          ensure_ascii=False, allow_nan=False)
    except (ValueError, TypeError) as exc:
        raise WholeProfileReviewError("Values must be valid JSON") from exc


def _name(value):
    return isinstance(value, str) and bool(value.strip())


def _selector(value):
    if not isinstance(value, dict) or value.get("kind") not in (
        "trait-source", "trait-definition", "intrinsic-slot",
    ):
        raise WholeProfileReviewError("Unknown characteristic selector")
    if value["kind"] == "intrinsic-slot":
        allowed = ({"kind", "id"}, {"kind", "id", "equals"})
    else:
        allowed = ({"kind", "id"},)
    if set(value) not in allowed or not _name(value.get("id")):
        raise WholeProfileReviewError("Malformed characteristic selector")
    _key(value)


def _matches(item, selector):
    kind = selector["kind"]
    if kind == "intrinsic-slot":
        if item["kind"] != "intrinsic" or (
            item["intrinsicSlotId"] != selector["id"]
        ):
            return False
        return (
            "equals" not in selector or
            _key(item["value"]) == _key(selector["equals"])
        )
    if item["kind"] != "trait":
        return False
    return (
        item["sourceTraitOrFieldId"] == selector["id"]
        if kind == "trait-source"
        else item.get("mechanicDefinitionId") == selector["id"]
    )


def _selector_list(values):
    if not isinstance(values, list) or not values:
        raise WholeProfileReviewError("Expected nonempty selector list")
    for value in values:
        _selector(value)
    if len({_key(x) for x in values}) != len(values):
        raise WholeProfileReviewError("Repeated characteristic selector")


def validate_dependency_study(study):
    """Validate only a small declarative dependency vocabulary."""
    if not isinstance(study, dict) or set(study) != {"status", "rules"} or (
        study["status"] != "synthetic-dependency-study"
    ) or not isinstance(study["rules"], list):
        raise WholeProfileReviewError("Invalid dependency study envelope")
    seen = set()
    for rule in study["rules"]:
        if not isinstance(rule, dict) or not _name(rule.get("id")) or (
            rule["id"] in seen
        ):
            raise WholeProfileReviewError("Duplicate or missing rule ID")
        seen.add(rule["id"])
        kind = rule.get("kind")
        shape = {
            "requires-any": {"id", "kind", "subject", "oneOf"},
            "excludes": {"id", "kind", "first", "second"},
            "selection-count": {"id", "kind", "from", "min", "max"},
            "requires-choice": {
                "id", "kind", "subject", "choiceId", "options", "min", "max",
            },
            "linked-parameters": {
                "id", "kind", "first", "firstPath", "second", "secondPath",
            },
            "resource-coordination": {
                "id", "kind", "from", "resourceId",
            },
        }.get(kind)
        if shape is None or set(rule) != shape:
            raise WholeProfileReviewError("Unsupported dependency rule shape")
        if kind in ("requires-any", "requires-choice"):
            _selector(rule["subject"])
        if kind in ("requires-any", "selection-count",
                    "resource-coordination"):
            _selector_list(rule["oneOf"] if kind == "requires-any"
                           else rule["from"])
        if kind in ("excludes", "linked-parameters"):
            for side in ("first", "second"):
                _selector(rule[side])
        if kind in ("selection-count", "requires-choice"):
            minimum, maximum = rule["min"], rule["max"]
            if (type(minimum) is not int or type(maximum) is not int
                    or minimum < 0 or maximum < minimum):
                raise WholeProfileReviewError("Invalid choice/cardinality bounds")
        if kind == "requires-choice":
            if not _name(rule["choiceId"]) or not isinstance(
                rule["options"], list
            ) or not rule["options"] or any(
                not _name(x) for x in rule["options"]
            ) or len(set(rule["options"])) != len(rule["options"]) or (
                rule["max"] > len(rule["options"])
            ):
                raise WholeProfileReviewError("Invalid dependent choice options")
        if kind == "linked-parameters":
            for name in ("firstPath", "secondPath"):
                path = rule[name]
                if (not _name(path) or not all(part.isidentifier()
                                             for part in path.split("."))):
                    raise WholeProfileReviewError("Invalid parameter path")
        if kind == "resource-coordination" and not _name(rule["resourceId"]):
            raise WholeProfileReviewError("Invalid resource group identity")


def _path(value, path):
    cursor = value
    for key in path.split("."):
        if not isinstance(cursor, dict) or key not in cursor:
            return None, False
        cursor = cursor[key]
    return cursor, True


def review_whole_origin_profile(preview, decision_contract,
                                dependency_study, choice_records=None):
    """Review dependency closure of provisional retained candidate evidence.

    The output does not assert that the complete Origin or source eligibility
    was checked, even if all *declared* synthetic rules appear satisfied.
    """
    validate_dependency_study(dependency_study)
    decision = examine_reconciliation_decisions(preview, decision_contract)
    choice_records = [] if choice_records is None else choice_records
    if not isinstance(choice_records, list):
        raise WholeProfileReviewError("Choice records must be a list")

    selected = {
        row["candidateId"]: row
        for row in preview["selectedCandidateEvidence"]
    }
    if (len(selected) != len(preview["selectedCandidateEvidence"])
            or set(selected) != set(preview["proposedCandidateIds"])):
        raise WholeProfileReviewError(
            "Proposed selection does not match its candidate evidence"
        )

    kept, suppressed, pending = set(), set(), set()
    for record in decision["reviewedDecisions"]:
        first = record["firstCandidateId"]
        second = record["secondCandidateId"]
        action = record["proposedDisposition"]
        if action == "preserve-both":
            kept.update((first, second))
        elif action in ("choose-first", "consolidate-evidence"):
            kept.add(first)
            suppressed.add(second)
        elif action == "choose-second":
            kept.add(second)
            suppressed.add(first)
        else:
            pending.update((first, second))
    for record in decision["unreviewedRelationships"]:
        pending.update((
            record["firstCandidateId"], record["secondCandidateId"]
        ))
    contradictions = {x["candidateId"] for x in decision[
        "globalContradictions"
    ]}
    pending.update(contradictions)
    pending.update(kept & suppressed)
    active = set(selected) - suppressed - pending
    uncertain = pending
    # A proposed disposition remains hypothetical, including every active
    # characteristic. No actual grant instances are created.
    def find(selector, ids):
        return [sid for sid in preview["proposedCandidateIds"]
                if sid in ids and _matches(selected[sid], selector)]

    recorded = {}
    for record in choice_records:
        if not isinstance(record, dict) or set(record) != {
            "candidateId", "choiceId", "values"
        } or record.get("candidateId") not in selected or not _name(
            record.get("choiceId")
        ) or not isinstance(record.get("values"), list) or any(
            not _name(v) for v in record["values"]
        ) or len(set(record["values"])) != len(record["values"]):
            raise WholeProfileReviewError("Malformed or unknown choice record")
        key = (record["candidateId"], record["choiceId"])
        if key in recorded:
            raise WholeProfileReviewError("Duplicate choice record")
        recorded[key] = copy.deepcopy(record["values"])

    outcomes = []
    used_choice_records = set()
    for rule in dependency_study["rules"]:
        kind, rid = rule["kind"], rule["id"]
        observations = []
        status = "not-applicable"
        if kind in ("requires-any", "requires-choice"):
            subjects = find(rule["subject"], active)
            waiting_subjects = find(rule["subject"], uncertain)
            if waiting_subjects:
                status = "pending-reconciliation"
            elif subjects and kind == "requires-any":
                matches = [x for selector in rule["oneOf"]
                           for x in find(selector, active)]
                waiting = [x for selector in rule["oneOf"]
                           for x in find(selector, uncertain)]
                status = (
                    "satisfied-evidence" if matches else
                    "pending-reconciliation" if waiting else
                    "missing-prerequisite"
                )
                observations = list(dict.fromkeys(matches or waiting))
            elif subjects and kind == "requires-choice":
                statuses = []
                for cid in subjects:
                    key = (cid, rule["choiceId"])
                    used_choice_records.add(key)
                    values = recorded.get(key)
                    if values is None:
                        statuses.append("missing-choice")
                    elif (
                        not rule["min"] <= len(values) <= rule["max"] or
                        any(v not in rule["options"] for v in values)
                    ):
                        statuses.append("invalid-choice")
                    else:
                        statuses.append("satisfied-evidence")
                    observations.append({"candidateId": cid,
                                         "submittedValues": values})
                status = (
                    "invalid-choice" if "invalid-choice" in statuses else
                    "missing-choice" if "missing-choice" in statuses else
                    "satisfied-evidence"
                )
        elif kind == "excludes":
            first = find(rule["first"], active)
            second = find(rule["second"], active)
            observations = [*first, *second]
            if first and second:
                status = "excluded-combination"
            elif (first and find(rule["second"], uncertain)) or (
                second and find(rule["first"], uncertain)
            ):
                status = "pending-reconciliation"
            else:
                status = "satisfied-evidence"
        elif kind == "selection-count":
            matched = set()
            waiting = set()
            for selector in rule["from"]:
                matched.update(find(selector, active))
                waiting.update(find(selector, uncertain))
            observations = sorted(matched)
            if len(matched) > rule["max"]:
                status = "too-many-selected"
            elif len(matched) + len(waiting) < rule["min"]:
                status = "too-few-selected"
            elif waiting and (
                len(matched) < rule["min"] or
                len(matched) + len(waiting) > rule["max"]
            ):
                status = "pending-reconciliation"
            else:
                status = "satisfied-evidence"
        elif kind == "linked-parameters":
            first = find(rule["first"], active)
            second = find(rule["second"], active)
            observations = [*first, *second]
            if not first and not second:
                status = "not-applicable"
            elif (len(first) != 1 or len(second) != 1):
                status = "pending-reconciliation" if (
                    find(rule["first"], uncertain) or
                    find(rule["second"], uncertain)
                ) else "missing-linked-characteristic"
            else:
                a, found_a = _path(selected[first[0]].get("parameters"),
                                   rule["firstPath"])
                b, found_b = _path(selected[second[0]].get("parameters"),
                                   rule["secondPath"])
                status = (
                    "unresolved-parameter" if not found_a or not found_b
                    else "satisfied-evidence" if _key(a) == _key(b)
                    else "linked-parameter-conflict"
                )
        else:  # resource-coordination
            matched = set()
            waiting = set()
            for selector in rule["from"]:
                matched.update(find(selector, active))
                waiting.update(find(selector, uncertain))
            observations = sorted(matched)
            status = (
                "requires-resource-policy" if len(matched) > 1
                else "pending-reconciliation" if waiting
                else "satisfied-evidence"
            )
        outcomes.append({
            "ruleId": rid, "kind": kind, "status": status,
            "involvedCandidateIds": copy.deepcopy(observations),
        })

    unused = set(recorded) - used_choice_records
    if unused:
        raise WholeProfileReviewError(
            "Choice record lacks a matching active declared requirement"
        )
    authoring = []
    for cid in preview["proposedCandidateIds"]:
        if cid not in active:
            continue
        row = selected[cid]
        if row["kind"] != "trait":
            continue
        original = row["originalOffer"]
        definition = original.get("authoredDefinition")
        if isinstance(definition, dict):
            for hint in _choice_observations(definition):
                authoring.append({
                    "candidateId": cid,
                    "observedChoice": copy.deepcopy(hint),
                    "status": "authored-choice-not-covered-by-trusted-schema",
                })

    failures = {
        "missing-prerequisite", "missing-choice", "invalid-choice",
        "excluded-combination", "too-many-selected", "too-few-selected",
        "missing-linked-characteristic", "linked-parameter-conflict",
    }
    ambiguous = {
        "pending-reconciliation", "unresolved-parameter",
        "requires-resource-policy",
    }
    result_status = (
        "blocked-by-reconciliation" if decision["reviewStatus"] != (
            "complete-proposal-not-authorized"
        ) else "declared-dependency-conflicts"
        if any(x["status"] in failures for x in outcomes)
        else "declared-dependencies-require-review"
        if any(x["status"] in ambiguous for x in outcomes) or authoring
        else "no-declared-blockers-not-certified"
    )
    return {
        "proposalFingerprint": decision["proposalFingerprint"],
        "tentativelyRetainedCandidateIds": [
            x for x in preview["proposedCandidateIds"] if x in active
        ],
        "tentativelySuppressedCandidateIds": [
            x for x in preview["proposedCandidateIds"] if x in suppressed
        ],
        "unresolvedCandidateIds": [
            x for x in preview["proposedCandidateIds"] if x in uncertain
        ],
        "reconciliationReviewStatus": decision["reviewStatus"],
        "dependencyRuleOutcomes": outcomes,
        "authoredChoiceObservations": authoring,
        "candidatePoolEvidenceStatus": decision["candidatePoolEvidenceStatus"],
        "deferredSourceFields": copy.deepcopy(
            decision["deferredSourceFields"]
        ),
        "sourceReviewFlags": copy.deepcopy(decision["sourceReviewFlags"]),
        "wholeProfileReviewStatus": result_status,
        "fullDependencyClosure": "not-certified",
        "selectionEligibility": "not-evaluated",
        "grantsIssued": False,
        "authorization": "not-authorized",
        "balance": "not-evaluated",
        "status": "synthetic-whole-profile-review-only",
    }
