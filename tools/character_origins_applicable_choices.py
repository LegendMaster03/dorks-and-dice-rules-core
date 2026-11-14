"""Project native choice authoring onto tentative Origin characteristics.

Only choices belonging to provisionally retained characteristic evidence are
considered *applicable*. This is not permission to select a trait or a
completed dependency check. Non-native mixed parents and custom proposals
must not acquire new first-party grants or have their prior choices invented.
"""
import copy

from character_origins_reconciliation_decisions import proposal_fingerprint
from character_origins_source_inventory import source_lineage


class ApplicableChoiceError(ValueError):
    """Choice authoring or characteristic evidence failed conservation."""


def project_applicable_choice_evidence(
        choice_report, preview, whole_profile, catalog):
    """Connect authored choice occurrences to tentative retained candidates.

    The tool does not infer whether a character-creation choice is mandatory,
    reuse a nested contributor's historical choice values, or treat a
    use-time ability option as a permanent Origin selection.
    """
    if (not isinstance(choice_report, dict)
            or choice_report.get("status")
            != "origin-native-choice-schema-evidence-only"
            or choice_report.get("authorization") != "not-authorized"
            or not isinstance(choice_report.get("choiceObservations"), list)
            or not isinstance(preview, dict)
            or not isinstance(whole_profile, dict)
            or whole_profile.get("status") != "synthetic-whole-profile-review-only"
            or whole_profile.get("authorization") != "not-authorized"
            or whole_profile.get("grantsIssued") is not False
            or whole_profile.get("proposalFingerprint")
            != proposal_fingerprint(preview)):
        raise ApplicableChoiceError(
            "Expected matching non-authoritative choice and Origin studies"
        )
    chosen = whole_profile.get("tentativelyRetainedCandidateIds")
    if not isinstance(chosen, list) or len(set(chosen)) != len(chosen):
        raise ApplicableChoiceError("Invalid retained candidate IDs")
    proposals = {
        row["candidateId"]: row
        for row in preview["selectedCandidateEvidence"]
    }
    if (len(proposals) != len(preview["selectedCandidateEvidence"])
            or any(cid not in proposals for cid in chosen)):
        raise ApplicableChoiceError("Retained candidates lack source evidence")

    # Source-level controls are observed only for source lineages actually
    # participating in this proposal. Do not expose unrelated Species
    # controls from an audit of the full native catalog.
    relevant_source_ids = set()
    for candidate in preview["selectedCandidateEvidence"]:
        prov = candidate["contributorProvenance"]
        if prov["kind"] == "native-source":
            for node in source_lineage(prov["selectedSourceId"], catalog):
                relevant_source_ids.add(node["id"])

    by_occurrence = {}
    source_controls = []
    for choice in choice_report["choiceObservations"]:
        if not isinstance(choice, dict) or not isinstance(
            choice.get("authoredOccurrenceId"), str
        ):
            raise ApplicableChoiceError("Malformed choice evidence")
        # A source-control or source-wide ability choice is not
        # automatically tied to the grant of a particular mixed trait.
        if choice["choiceScope"] == "source-configuration" and (
            choice["authoredField"] != "sizeChoice"
        ):
            if choice["sourceId"] in relevant_source_ids:
                source_controls.append(copy.deepcopy(choice))
            continue
        by_occurrence.setdefault(choice["authoredOccurrenceId"], []).append(
            choice
        )

    projected, nested, custom = [], [], []
    for cid in chosen:
        candidate = proposals[cid]
        offer = candidate["originalOffer"]
        provenance = candidate["contributorProvenance"]
        kind = provenance["kind"]
        if kind == "native-source":
            occurrence = offer.get("sourceOccurrenceId")
            if (not isinstance(occurrence, str) or
                    occurrence != provenance.get("sourceOccurrenceId")):
                raise ApplicableChoiceError(
                    "Direct native offer has mismatched authored occurrence"
                )
            source_id = provenance["selectedSourceId"]
            if source_id not in catalog:
                raise ApplicableChoiceError(
                    "Native selected source is not in current catalog"
                )
            source_occurrence = occurrence.split("#", 1)[0]
            if source_occurrence not in {
                x["id"] for x in source_lineage(source_id, catalog)
            }:
                raise ApplicableChoiceError(
                    "Native source occurrence is outside selected lineage"
                )
            # Intrinsic candidates use a source occurrence ID for
            # provenance but identify their mechanic by the shared slot.
            # Traits use their source-qualified trait identity directly.
            identity_matches = (
                candidate["intrinsicSlotId"] == offer["identity"]
                if candidate["kind"] == "intrinsic"
                else candidate["sourceTraitOrFieldId"] == offer["identity"]
            )
            if not identity_matches:
                raise ApplicableChoiceError(
                    "Characteristic identity differs from native offer"
                )
            authored = catalog.get(source_occurrence)
            if not isinstance(authored, dict):
                raise ApplicableChoiceError(
                    "Native authoring occurrence is not in current catalog"
                )
            if "#trait:" in occurrence:
                local_id = occurrence.split("#trait:", 1)[1]
                trait = next(
                    (x for x in authored.get("traits", [])
                     if x["id"] == local_id), None
                )
                if trait is None or offer.get("authoredDefinition") != trait:
                    raise ApplicableChoiceError(
                        "Native trait offer does not match current authoring"
                    )
            elif "#intrinsic:" in occurrence:
                field = occurrence.split("#intrinsic:", 1)[1]
                if field not in authored:
                    raise ApplicableChoiceError(
                        "Native intrinsic occurrence no longer authored"
                    )
                value = offer.get("value")
                authored_value = authored[field]
                allowed = (
                    isinstance(authored_value, dict)
                    and isinstance(authored_value.get("from"), list)
                    and value in authored_value["from"]
                    if field == "sizeChoice"
                    else value == authored_value
                )
                if not allowed:
                    raise ApplicableChoiceError(
                        "Native intrinsic value is not from authored source"
                    )
            else:
                raise ApplicableChoiceError(
                    "Unrecognized native characteristic occurrence"
                )
            for record in by_occurrence.get(occurrence, []):
                projected.append({
                    "candidateId": cid,
                    "choiceEvidence": copy.deepcopy(record),
                    "applicability": (
                        "use-time-option-not-creation"
                        if record["timing"] == "on-use"
                        else "intrinsic-value-already-proposed"
                        if candidate["kind"] == "intrinsic"
                        else "active-trait-choice-needs-authority"
                    ),
                    "grantAuthorized": False,
                })
        elif kind == "manifested-profile":
            # Nested profiles retain actual manifested parameter evidence,
            # but the older draft schema has no authoritative choice history.
            # Neither replay source creation nor reconstruct latent ancestry.
            nested.append({
                "candidateId": cid,
                "profileId": provenance["profileId"],
                "sourceTraitOrFieldId": candidate["sourceTraitOrFieldId"],
                "status": "prior-choice-history-not-verified",
            })
        elif kind == "custom-proposal":
            custom.append({
                "candidateId": cid,
                "proposedAuthorityRef": provenance["proposedAuthorityRef"],
                "status": "custom-choice-schema-not-yet-authorized",
            })
        else:
            raise ApplicableChoiceError(
                "Unsupported contributor offer provenance"
            )
    # No standard source-level controls are automatically applied to a
    # hybrid or fully custom Origin. A future standard-preset contract must
    # explicitly bind source selection to required source-wide choices.
    return {
        "proposalFingerprint": whole_profile["proposalFingerprint"],
        "applicableNativeChoiceEvidence": projected,
        "nestedChoiceHistoryGaps": nested,
        "customChoiceAuthoringGaps": custom,
        "sourceConfigurationControlsNotApplied": source_controls,
        "selectionEligibility": "not-evaluated",
        "authoritativeChoiceClosure": "not-certified",
        "grantsIssued": False,
        "status": "applicable-choice-evidence-only",
    }
