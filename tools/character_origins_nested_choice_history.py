"""Version-bound study of past choice records for mixed-profile contributors.

Current draft Origin profiles do not persist independently selected skill,
feat, or spell choice history. A proposed sidecar can document such choices
without fabricating parents or repeating character-creation decisions.

The fingerprint here detects mismatches with the current draft profile; it
is not a signature or actual proof of DM/player authorization. A past
choice is not automatically a new descendant's choice or a transmissible
trait, even when its recorded value is structurally verifiable.
"""
import copy
import hashlib
import json

from character_origins_choice_instances import (
    ChoiceInstanceError, _catalog_reference, _distinct_options,
    _expected_count, _finite_values, _key, _valid_name,
    validate_study_catalogs,
)


class NestedChoiceHistoryError(ValueError):
    """Missing, stale, invented, or invalid nested choice-history evidence."""


def draft_profile_fingerprint(profile):
    if (not isinstance(profile, dict)
            or profile.get("status") != "draft-origin-profile"
            or profile.get("authority") != "not-authorized"
            or not _valid_name(profile.get("profileId"))
            or not isinstance(profile.get("manifestedCharacteristics"), list)):
        raise NestedChoiceHistoryError("Expected a non-authoritative draft Origin profile")
    return hashlib.sha256(_key(profile).encode("utf-8")).hexdigest()


def review_nested_choice_history(applicable, profiles, sidecars,
                                 native_choice_audit, catalogs):
    """Review proposed archived choices against exact current profile content.

    A matching sidecar still does not prove actual historical selection,
    make a catalog choice legal, or entitle a future descendant to copy it.
    """
    if (not isinstance(applicable, dict)
            or applicable.get("status") != "applicable-choice-evidence-only"
            or applicable.get("grantsIssued") is not False
            or not isinstance(applicable.get("nestedChoiceHistoryGaps"), list)
            or not isinstance(profiles, dict)
            or not isinstance(sidecars, list)
            or not isinstance(native_choice_audit, dict)
            or native_choice_audit.get("status")
            != "origin-native-choice-schema-evidence-only"
            or native_choice_audit.get("authorization") != "not-authorized"):
        raise NestedChoiceHistoryError("Expected nested, choice and profile study evidence")
    try:
        catalogs_by_ref = validate_study_catalogs(catalogs)
    except ChoiceInstanceError as exc:
        raise NestedChoiceHistoryError(str(exc)) from exc
    definitions = {}
    for record in native_choice_audit.get("choiceObservations", []):
        if not isinstance(record, dict) or not _valid_name(
            record.get("choiceEvidenceId")
        ):
            raise NestedChoiceHistoryError("Malformed native choice definition evidence")
        if record["choiceEvidenceId"] in definitions:
            raise NestedChoiceHistoryError("Duplicate native choice definition evidence")
        definitions[record["choiceEvidenceId"]] = record

    requested = {}
    for record in applicable["nestedChoiceHistoryGaps"]:
        if not isinstance(record, dict) or not all(
            _valid_name(record.get(field))
            for field in ("candidateId", "profileId", "sourceTraitOrFieldId")
        ):
            raise NestedChoiceHistoryError("Invalid nested contributor reference")
        requested.setdefault(record["profileId"], set()).add(
            record["sourceTraitOrFieldId"]
        )

    archive = {}
    for sidecar in sidecars:
        if (not isinstance(sidecar, dict) or set(sidecar) != {
            "status", "profileId", "profileFingerprint", "records"
        } or sidecar.get("status") != "proposed-archived-choice-history"
                or not _valid_name(sidecar.get("profileId"))
                or not isinstance(sidecar.get("records"), list)):
            raise NestedChoiceHistoryError("Invalid archived choice envelope")
        profile_id = sidecar["profileId"]
        if profile_id in archive or profile_id not in requested:
            raise NestedChoiceHistoryError("Duplicate or unrelated contributor history")
        profile = profiles.get(profile_id)
        if profile is None or (
            sidecar["profileFingerprint"] != draft_profile_fingerprint(profile)
        ):
            raise NestedChoiceHistoryError("Archived choice evidence is stale or lacks a profile")
        archive[profile_id] = sidecar

    matches, missing, unresolved = [], [], []
    for profile_id, traits in requested.items():
        profile = profiles.get(profile_id)
        if profile is None:
            raise NestedChoiceHistoryError("Referenced contributor profile is unavailable")
        fingerprint = draft_profile_fingerprint(profile)
        manifest = {
            item["identity"]: item
            for item in profile["manifestedCharacteristics"]
            if isinstance(item, dict) and _valid_name(item.get("identity"))
        }
        if len(manifest) != len(profile["manifestedCharacteristics"]):
            raise NestedChoiceHistoryError("Ambiguous manifested contributor identities")
        if any(key not in manifest for key in traits):
            raise NestedChoiceHistoryError(
                "History references trait not manifested in contributor profile"
            )
        sidecar = archive.get(profile_id)
        recorded = {}
        if sidecar:
            for record in sidecar["records"]:
                if (not isinstance(record, dict) or set(record) != {
                    "characteristicIdentity", "choiceEvidenceId",
                    "selectedOptionIds", "proposedAuthorityRef"
                } or not all(_valid_name(record.get(k)) for k in (
                    "characteristicIdentity", "choiceEvidenceId",
                    "proposedAuthorityRef",
                )) or not _distinct_options(record.get("selectedOptionIds"))):
                    raise NestedChoiceHistoryError("Invalid archived choice record")
                identity = record["characteristicIdentity"]
                key = (identity, record["choiceEvidenceId"])
                if identity not in traits or key in recorded:
                    raise NestedChoiceHistoryError(
                        "Archived choice not relevant to this manifested trait"
                    )
                evidence = definitions.get(key[1])
                if (not evidence or evidence["authoredOccurrenceId"] != identity
                        or evidence["choiceScope"] != "trait-instance"):
                    raise NestedChoiceHistoryError("Archived choice not supported by native authoring")
                if evidence["timing"] == "on-use" or evidence["kind"] in (
                    "player-allocation", "derived-parameter-binding",
                    "unmodeled-choice-field",
                ):
                    raise NestedChoiceHistoryError(
                        "Unresolved, derived or use-time choice cannot be archived as a persistent selection"
                    )
                values = _finite_values(evidence)
                reference = _catalog_reference(evidence)
                evidence_ref = None
                if reference is not None:
                    catalog = catalogs_by_ref.get(reference)
                    if catalog is None:
                        raise NestedChoiceHistoryError(
                            "Archived catalog choice lacks actual study evidence"
                        )
                    values = catalog["optionIds"]
                    evidence_ref = catalog["evidenceRef"]
                if (not _distinct_options(values)
                        or any(x not in values for x in record["selectedOptionIds"])
                        or len(record["selectedOptionIds"]) != _expected_count(evidence)):
                    raise NestedChoiceHistoryError(
                        "Archived choice values do not match authored evidence"
                    )
                recorded[key] = {
                    "profileId": profile_id,
                    "profileFingerprint": fingerprint,
                    "characteristicIdentity": identity,
                    "choiceEvidenceId": key[1],
                    "selectedOptionIds": copy.deepcopy(record["selectedOptionIds"]),
                    "proposedAuthorityRef": record["proposedAuthorityRef"],
                    "syntheticCatalogEvidenceRef": evidence_ref,
                    "status": "source-shape-matches-proposal-not-authorized",
                }

        for trait in sorted(traits):
            expected = [
                note for note in definitions.values()
                if note["choiceScope"] == "trait-instance"
                and note["authoredOccurrenceId"] == trait
                and note["timing"] != "on-use"
            ]
            for evidence in expected:
                key = (trait, evidence["choiceEvidenceId"])
                if key in recorded:
                    matches.append(recorded[key])
                else:
                    missing.append({
                        "profileId": profile_id,
                        "characteristicIdentity": trait,
                        "choiceEvidenceId": key[1],
                        "status": "historical-choice-record-unavailable",
                    })
            if not expected:
                unresolved.append({
                    "profileId": profile_id,
                    "characteristicIdentity": trait,
                    "status": "no-verified-trait-choice-history-schema",
                })

    return {
        "structurallyMatchedHistory": matches,
        "missingChoiceHistory": missing,
        "unmodeledTraitHistory": unresolved,
        "historicalSelectionAuthorized": False,
        "transmissionEligibility": "not-evaluated",
        "descendantChoiceInstancesCreated": False,
        "status": "nested-choice-history-evidence-only",
    }
