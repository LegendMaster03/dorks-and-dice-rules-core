"""Illustrative ancestral-history annotations for Pillar 2 reconstruction.

Narrative hypotheses are *not* inheritance evidence. They never modify a
candidate pool, grant a characteristic, infer genetics, or make two variant
mechanics interchangeable. This separation is part of the design study.
"""
import argparse
import copy
import json
from pathlib import Path

from character_origins_compound_traits import read_contracts
from character_origins_parameter_inheritance import (
    ParameterInheritanceError, _value_key, read_blueprint,
)
from character_origins_source_inventory import read_catalog
from character_origins_target_reconstruction import compare_standard_target


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_EVIDENCE = (
    ROOT / "docs/rewrite/character-origins/"
    "pillar2-illustrative-ancestry-relationships-20261111.json"
)


def read_hypotheses(path=DEFAULT_EVIDENCE):
    """Read a deliberately non-canonical illustration, never active rules."""
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    if data.get("status") != "illustrative-hypotheses-not-canon-or-grants":
        raise ParameterInheritanceError(
            "Only explicitly non-canonical narrative evidence is accepted"
        )
    return data


def _trait(catalog, occurrence):
    if not isinstance(occurrence, str) or occurrence.count("#trait:") != 1:
        raise ParameterInheritanceError("Invalid source-qualified trait reference")
    source_id, local = occurrence.split("#trait:", 1)
    source = catalog.get(source_id)
    if not source or not local:
        raise ParameterInheritanceError("Unknown narrative trait source")
    matching = [x for x in source.get("traits", []) if x.get("id") == local]
    if len(matching) != 1:
        raise ParameterInheritanceError("Unknown or ambiguous narrative trait")
    return matching[0]


def overlay_narrative_hypotheses(reconstruction, evidence, catalog):
    """Append independent storytelling evidence to a reconstruction audit.

    Return the original comparison *unchanged in meaning* and keep proposed
    historical relationships separate. Even exact shared mechanical fields
    carry no trait eligibility or manifestation authority.
    """
    if not isinstance(reconstruction, dict) or not isinstance(
        reconstruction.get("comparison"), list
    ) or not isinstance(reconstruction.get("contributors"), list):
        raise ParameterInheritanceError("Expected Origin comparison report")
    if not isinstance(evidence, dict) or (
        evidence.get("status") != "illustrative-hypotheses-not-canon-or-grants"
    ) or not isinstance(evidence.get("hypotheses"), list):
        raise ParameterInheritanceError("Invalid narrative-only evidence")
    contributor_sources = {
        item.get("selectedSourceId")
        for item in reconstruction["contributors"]
        if isinstance(item, dict)
    }
    annotations = []
    for hypothesis in evidence["hypotheses"]:
        if not isinstance(hypothesis, dict) or (
            set(hypothesis) != {
                "hypothesisId", "descendantSourceId",
                "possibleHistoricalSourceIds", "narrative",
                "traitVariantEvidence"
            }
        ):
            raise ParameterInheritanceError("Invalid narrative hypothesis structure")
        target = hypothesis["descendantSourceId"]
        sources = hypothesis["possibleHistoricalSourceIds"]
        if not isinstance(target, str) or target not in catalog or (
            not isinstance(sources, list) or not sources
            or any(not isinstance(s, str) or s not in catalog for s in sources)
        ):
            raise ParameterInheritanceError("Unknown hypothetical source records")
        if not isinstance(hypothesis["hypothesisId"], str) or (
            not hypothesis["hypothesisId"]
            or not isinstance(hypothesis["narrative"], str)
            or not hypothesis["narrative"]
            or not isinstance(hypothesis["traitVariantEvidence"], list)
        ):
            raise ParameterInheritanceError("Incomplete narrative hypothesis")

        observations = []
        for relationship in hypothesis["traitVariantEvidence"]:
            if not isinstance(relationship, dict) or set(relationship) != {
                "descendantOccurrenceId", "relatedSourceOccurrenceId",
                "sharedAuthoredField", "sharedAuthoredValue",
                "proposedRelationship", "inheritanceEffect"
            }:
                raise ParameterInheritanceError("Invalid trait-variant evidence")
            if (relationship["inheritanceEffect"] != "none"
                or relationship["proposedRelationship"]
                != "possibly-divergent-mechanical-variants"):
                raise ParameterInheritanceError(
                    "Narrative hypothesis can not authorize inheritance"
                )
            descendant_id = relationship["descendantOccurrenceId"]
            source_id = relationship["relatedSourceOccurrenceId"]
            if not descendant_id.startswith(target + "#trait:") or (
                not any(source_id.startswith(s + "#trait:") for s in sources)
            ):
                raise ParameterInheritanceError(
                    "Trait evidence is not from its declared source records"
                )
            descendant = _trait(catalog, descendant_id)
            ancestor = _trait(catalog, source_id)
            field = relationship["sharedAuthoredField"]
            if not isinstance(field, str) or field not in descendant or (
                field not in ancestor
                or _value_key(descendant[field])
                != _value_key(relationship["sharedAuthoredValue"])
                or _value_key(ancestor[field])
                != _value_key(relationship["sharedAuthoredValue"])
            ):
                raise ParameterInheritanceError(
                    "Claimed shared trait field is not authored by both variants"
                )
            observations.append(copy.deepcopy(relationship))

        if target == reconstruction["target"] and set(sources).issubset(
            contributor_sources
        ):
            annotations.append({
                "hypothesisId": hypothesis["hypothesisId"],
                "status": "illustrative-historical-hypothesis-not-canon",
                "narrative": hypothesis["narrative"],
                "possibleHistoricalSourceIds": copy.deepcopy(sources),
                "variantEvidence": observations,
                "confersEligibility": False,
            })
    return {
        "reconstruction": copy.deepcopy(reconstruction),
        "narrativeHypotheses": annotations,
        "annotationsAffectEligibility": False,
        "status": "annotated-origin-evidence-only",
    }


def main():
    parser = argparse.ArgumentParser(
        description="Show hypothetical history next to unchanged Origin coverage"
    )
    parser.add_argument("--target", default="ancestry.gnome.rock")
    parser.add_argument("--contributors", nargs="+",
                        default=["species.gnome", "species.dwarf"])
    parser.add_argument("--evidence", type=Path, default=DEFAULT_EVIDENCE)
    args = parser.parse_args()
    catalog = read_catalog()
    blueprint = read_blueprint()
    report = compare_standard_target(
        args.target,
        [
            {"contributorId": f"contributor-{i+1}",
             "selectedSourceId": source}
            for i, source in enumerate(args.contributors)
        ],
        catalog, read_contracts(),
        {blueprint["definitionOccurrenceId"]: blueprint},
    )
    print(json.dumps(overlay_narrative_hypotheses(
        report, read_hypotheses(args.evidence), catalog
    ), indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
