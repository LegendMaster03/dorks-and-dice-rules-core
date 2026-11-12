"""Experimental choice-instance proposals for source-neutral Origin construction.

This tool is not an authoritative choice engine, does not grant traits, and
does not permit arbitrary catalog items. An instance is bound to an existing
tentatively retained candidate and the exact native choice evidence ID.
Finite values can be checked against authored options. Open catalog options
require an explicit *synthetic* catalog snapshot, which is NOT an actual
Rules Core qualification or authorization decision.

Previously mixed contributor profiles do not yet have verified choice
histories. Such gaps remain open and no historical choices are fabricated.
"""
import copy
import hashlib
import json

from character_origins_reconciliation_decisions import proposal_fingerprint


class ChoiceInstanceError(ValueError):
    """A choice proposal cannot be matched or validated as provisional evidence."""


def _key(value):
    try:
        return json.dumps(
            value, sort_keys=True, separators=(",", ":"),
            allow_nan=False, ensure_ascii=False,
        )
    except (TypeError, ValueError) as exc:
        raise ChoiceInstanceError("Choice evidence must be JSON-serializable") from exc


def _valid_name(value):
    return isinstance(value, str) and bool(value.strip())


def _distinct_options(options):
    return (
        isinstance(options, list) and bool(options)
        and all(_valid_name(option) for option in options)
        and len(options) == len(set(options))
    )


def validate_study_catalogs(catalogs):
    """Conservative, explicit test catalog references, NEVER trusted permissions."""
    if (not isinstance(catalogs, dict)
            or set(catalogs) != {"status", "catalogs"}
            or catalogs["status"] != "synthetic-catalog-evidence-only"
            or not isinstance(catalogs["catalogs"], list)):
        raise ChoiceInstanceError("Invalid synthetic catalog evidence")
    indexed = {}
    for row in catalogs["catalogs"]:
        if (not isinstance(row, dict) or set(row) != {
            "reference", "optionIds", "evidenceRef"
        } or not _valid_name(row["reference"])
                or not _valid_name(row["evidenceRef"])
                or not _distinct_options(row["optionIds"])
                or row["reference"] in indexed):
            raise ChoiceInstanceError("Malformed or duplicate catalog evidence")
        indexed[row["reference"]] = copy.deepcopy(row)
    _key(indexed)
    return indexed


def _catalog_reference(choice):
    field = choice["authoredField"]
    if field == "featCategory":
        return "feat:" + choice["authoredValue"]
    if field == "spellList":
        return "spell-list:" + choice["authoredValue"]
    if field == "chooseFrom" and isinstance(
        choice["authoredValue"], str
    ):
        return choice["authoredValue"]
    return None


def _finite_values(choice):
    field = choice["authoredField"]
    if field == "sizeChoice":
        return choice["constraints"]["options"]
    if field == "abilityChoice":
        return choice["constraints"]["options"]
    if field == "chooseFrom" and isinstance(
        choice["authoredValue"], list
    ):
        return choice["authoredValue"]
    if field == "spellcastingAbilityChoice":
        return choice["constraints"]["options"]
    return None


def _expected_count(choice):
    return choice.get("constraints", {}).get("count", 1)


def review_choice_instances(applicable, preview, choices, catalogs):
    """Review explicitly proposed current-character choice instances.

    A source control is not a selected characteristic. Use-time options are
    NOT persistent choices. Records for unselected/deferred/unknown traits
    do not acquire native entitlement simply by naming a source ID.
    """
    if (not isinstance(applicable, dict)
            or applicable.get("status") != "applicable-choice-evidence-only"
            or applicable.get("grantsIssued") is not False
            or applicable.get("authoritativeChoiceClosure") != "not-certified"
            or not isinstance(preview, dict)
            or applicable.get("proposalFingerprint")
            != proposal_fingerprint(preview)
            or not isinstance(applicable.get("applicableNativeChoiceEvidence"), list)
            or not isinstance(applicable.get("nestedChoiceHistoryGaps"), list)
            or not isinstance(applicable.get("customChoiceAuthoringGaps"), list)
            or not isinstance(applicable.get("sourceConfigurationControlsNotApplied"), list)):
        raise ChoiceInstanceError(
            "Expected matching non-authoritative applicable choice evidence"
        )
    catalogs_by_ref = validate_study_catalogs(catalogs)
    if not isinstance(choices, list):
        raise ChoiceInstanceError("Proposed choices must be a list")
    selected = {
        item["candidateId"]: item
        for item in preview.get("selectedCandidateEvidence", [])
    }
    if len(selected) != len(preview.get("selectedCandidateEvidence", [])):
        raise ChoiceInstanceError("Repeated candidate evidence IDs")

    available = {}
    use_time = []
    unresolved_bindings = []
    for item in applicable["applicableNativeChoiceEvidence"]:
        if not isinstance(item, dict) or set(item) != {
            "candidateId", "choiceEvidence", "applicability", "grantAuthorized"
        } or item["grantAuthorized"] is not False:
            raise ChoiceInstanceError("Malformed applicable choice entry")
        candidate = selected.get(item["candidateId"])
        evidence = item["choiceEvidence"]
        if (not candidate or not isinstance(evidence, dict)
                or not _valid_name(evidence.get("choiceEvidenceId"))
                or candidate["contributorProvenance"].get("kind") != "native-source"
                or evidence.get("authoredOccurrenceId") != candidate[
                    "originalOffer"].get("sourceOccurrenceId")):
            raise ChoiceInstanceError("Choice evidence differs from current native offer")
        key = (item["candidateId"], evidence["choiceEvidenceId"])
        if key in available:
            raise ChoiceInstanceError("Repeated applicable choice occurrence")
        if item["applicability"] == "use-time-option-not-creation":
            if evidence["timing"] != "on-use":
                raise ChoiceInstanceError("Inconsistent use-time choice authoring")
            use_time.append({
                "candidateId": key[0],
                "choiceEvidenceId": key[1],
                "status": "retained-as-use-time-option",
            })
        elif item["applicability"] == "intrinsic-value-already-proposed":
            if evidence["kind"] != "intrinsic-choice":
                raise ChoiceInstanceError("Non-intrinsic choice in intrinsic slot")
        elif item["applicability"] != "active-trait-choice-needs-authority":
            raise ChoiceInstanceError("Unsupported choice applicability")
        available[key] = item

    seen = set()
    instances = []
    for choice in choices:
        if (not isinstance(choice, dict)
                or set(choice) != {
                    "candidateId", "choiceEvidenceId", "selectedOptionIds",
                    "proposedAuthorityRef",
                } or not _valid_name(choice.get("candidateId"))
                or not _valid_name(choice.get("choiceEvidenceId"))
                or not _valid_name(choice.get("proposedAuthorityRef"))
                or not _distinct_options(choice.get("selectedOptionIds"))):
            raise ChoiceInstanceError("Malformed proposed choice instance")
        key = (choice["candidateId"], choice["choiceEvidenceId"])
        if key in seen:
            raise ChoiceInstanceError("Duplicate proposed choice instance")
        seen.add(key)
        item = available.get(key)
        if item is None:
            raise ChoiceInstanceError("Choice is not applicable to a retained native offer")
        evidence = item["choiceEvidence"]
        if item["applicability"] == "use-time-option-not-creation":
            raise ChoiceInstanceError("Use-time options cannot be persisted as Origin choices")
        if evidence["kind"] in (
            "player-allocation", "derived-parameter-binding",
            "unmodeled-choice-field",
        ):
            raise ChoiceInstanceError(
                "Unmodeled choice or derived binding cannot be independently selected"
            )
        permitted = _finite_values(evidence)
        catalog_ref = _catalog_reference(evidence)
        evidence_ref = None
        if catalog_ref is not None:
            catalog = catalogs_by_ref.get(catalog_ref)
            if catalog is None:
                raise ChoiceInstanceError(
                    "Catalog-backed choice lacks explicit catalog evidence"
                )
            permitted = catalog["optionIds"]
            evidence_ref = catalog["evidenceRef"]
        elif permitted is None:
            raise ChoiceInstanceError("No recognized option vocabulary for this choice")
        if not _distinct_options(permitted) or any(
            option not in permitted for option in choice["selectedOptionIds"]
        ):
            raise ChoiceInstanceError("Proposed option outside authored/catalog evidence")
        count = _expected_count(evidence)
        if type(count) is not int or len(choice["selectedOptionIds"]) != count:
            raise ChoiceInstanceError("Proposed option count does not match declaration")
        if item["applicability"] == "intrinsic-value-already-proposed":
            candidate_value = selected[choice["candidateId"]].get("value")
            if choice["selectedOptionIds"] != [candidate_value]:
                raise ChoiceInstanceError(
                    "Recorded intrinsic choice differs from proposed characteristic"
                )
        instance = {
            "candidateId": choice["candidateId"],
            "choiceEvidenceId": choice["choiceEvidenceId"],
            "selectedOptionIds": copy.deepcopy(choice["selectedOptionIds"]),
            "choiceSourceOccurrenceId": evidence["authoredOccurrenceId"],
            "contributorProvenance": copy.deepcopy(
                selected[choice["candidateId"]]["contributorProvenance"]
            ),
            "syntheticCatalogEvidenceRef": evidence_ref,
            "proposedAuthorityRef": choice["proposedAuthorityRef"],
            "timing": evidence["timing"],
            "status": (
                "timing-unresolved" if evidence["timing"] in (
                    "timing-unspecified", "unknown"
                ) else "option-evidence-reviewed-not-authorized"
            ),
            "grantInstanceId": None,
        }
        instances.append(instance)

    missing = []
    for key, item in available.items():
        if key in seen:
            continue
        evidence = item["choiceEvidence"]
        if item["applicability"] == "use-time-option-not-creation":
            continue
        if evidence["kind"] == "derived-parameter-binding":
            unresolved_bindings.append({
                "candidateId": key[0],
                "choiceEvidenceId": key[1],
                "status": "requires-dependency-binding",
            })
            continue
        missing.append({
            "candidateId": key[0],
            "choiceEvidenceId": key[1],
            "reason": (
                "needs-allocation-or-schema-policy" if evidence["kind"] in (
                    "player-allocation", "unmodeled-choice-field"
                ) else "needs-persistent-choice"
            ),
        })

    pending = bool(
        missing or unresolved_bindings
        or applicable["nestedChoiceHistoryGaps"]
        or applicable["customChoiceAuthoringGaps"]
        or any(x["status"] == "timing-unresolved" for x in instances)
    )
    return {
        "proposalFingerprint": applicable["proposalFingerprint"],
        "proposedChoiceInstances": instances,
        "unresolvedChoiceRequirements": missing,
        "unresolvedDerivedBindings": unresolved_bindings,
        "useTimeOptionsNotPersisted": use_time,
        "nestedChoiceHistoryGaps": copy.deepcopy(applicable["nestedChoiceHistoryGaps"]),
        "customChoiceAuthoringGaps": copy.deepcopy(applicable["customChoiceAuthoringGaps"]),
        "sourceConfigurationControlsNotApplied": copy.deepcopy(
            applicable["sourceConfigurationControlsNotApplied"]
        ),
        "choiceReviewStatus": (
            "incomplete-or-unverified-choice-evidence" if pending
            else "proposed-choice-evidence-complete-not-authorized"
        ),
        "catalogEligibility": "not-certified",
        "traitSelectionEligibility": "not-certified",
        "grantsIssued": False,
        "authority": "not-authorized",
        "status": "choice-instance-proposals-only",
    }


def choice_instance_snapshot_fingerprint(preview, instances):
    """Fingerprint *proposed* recorded choices for eventual profile-binding.

    This digest only detects changes. It is not proof the originator or DM
    approved the values, or that they can be inherited.
    """
    payload = {
        "proposalFingerprint": proposal_fingerprint(preview),
        "choiceInstances": instances,
    }
    return hashlib.sha256(_key(payload).encode("utf-8")).hexdigest()
