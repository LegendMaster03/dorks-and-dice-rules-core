#!/usr/bin/env python3
"""Apply the owner-selected 5.5e monster-roster model to native staging.

Newer shared-name monster stat blocks become the default native representation.
Known renamed roles become aliases, while source-only creatures with genuinely
different tactical identity are preserved pending role design.
Legacy mechanical distinctions are kept in a non-runtime review ledger.
No legacy ability is appended blindly to a revised attack suite.
"""
from collections import Counter
from pathlib import Path
import copy
import json

from reconcile_native_baseline import ROOT, MANIFEST, REPORT, load, write, retained_definition

REF = ROOT / "data/reference/srd"
REVIEW = REF / "monster-legacy-variant-review.json"
FAMILIES = ROOT / "data/native/monster-families.json"
ALIAS_PAIRS = {
    "Acolyte": "Priest Acolyte",
    "Azer": "Azer Sentinel",
    "Centaur": "Centaur Trooper",
    "Cult Fanatic": "Cultist Fanatic",
    "Flying Sword": "Animated Flying Sword",
    "Giant Poisonous Snake": "Giant Venomous Snake",
    "Giant Sea Horse": "Giant Seahorse",
    "Gnoll": "Gnoll Warrior",
    "Goblin": "Goblin Warrior",
    "Kobold": "Kobold Warrior",
    "Merfolk": "Merfolk Skirmisher",
    "Poisonous Snake": "Venomous Snake",
    "Quipper": "Piranha",
    "Rug of Smothering": "Animated Rug of Smothering",
    "Sahuagin": "Sahuagin Warrior",
    "Sea Horse": "Seahorse",
    "Shrieker": "Shrieker Fungus",
    "Swarm of Poisonous Snakes": "Swarm of Venomous Snakes",
    "Swarm of Quippers": "Swarm of Piranhas",
    "Thug": "Tough",
    "Veteran": "Warrior Veteran",
}
# Existing 5.1 monster tactics have no confirmed same-role mechanical successor.
# Retain them as potential role variants rather than merging away their identity.
DISTINCT_ROLE_REVIEW = {
    "Hobgoblin": "Martial Advantage rather than Pack Tactics",
    "Bugbear": "Brute and Surprise Attack rather than grappling-oriented combat",
}

# These feature names express independent perceptions, terrain or disguise
# capabilities, and have no numeric attack or action-economy replacement.
# Even here, source-specific restrictions remain intact.
INDEPENDENT_TRAIT_NAMES = {
    "Keen Smell", "Keen Sight", "Keen Hearing",
    "Keen Hearing and Smell", "Keen Hearing and Sight",
    "Keen Sight and Smell", "False Appearance",
    "False Appearance (Object Form Only)",
    "Sure-Footed", "Stone Camouflage", "Snow Camouflage",
    "Underwater Camouflage", "Standing Leap", "Hold Breath",
    "Web Sense", "Echolocation", "Wakeful",
}
# An older feature name may refer to a capability represented under an
# updated name or procedure in 5.5. Do not append it automatically.
NOT_SEPARATE = {"Legendary Resistance (3/Day)"}

def slug(name):
    import re
    return re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-")

def normalized_name(name):
    return slug(name.split(" (")[0])

def named_features(content, kind):
    value = content.get(kind)
    return value if isinstance(value, list) else []

def all_names(content):
    return {
        normalized_name(f["name"]) for key in (
            "trait", "action", "bonus", "reaction", "legendary",
            "spellcasting"
        ) for f in named_features(content, key)
        if isinstance(f, dict) and f.get("name")
    }

def legacy_differences(old, newer):
    newer_names = all_names(newer)
    out = {}
    for kind in ("trait", "action", "bonus", "reaction", "legendary", "spellcasting"):
        distinct = [
            f["name"] for f in named_features(old, kind)
            if f.get("name") and normalized_name(f["name"]) not in newer_names
            and f["name"] not in NOT_SEPARATE
        ]
        if distinct:
            out[kind] = distinct
    return out

def add_independent_traits(old, newer):
    existing = all_names(newer)
    # Choose only listed features with no same-named counterpart anywhere in
    # the newer monster's actions; preserve their original trigger and limits.
    additions = []
    for trait in named_features(old, "trait"):
        name = trait["name"]
        if name not in INDEPENDENT_TRAIT_NAMES:
            continue
        if normalized_name(name) in existing:
            continue
        additions.append(copy.deepcopy(trait))
    if additions:
        newer.setdefault("trait", []).extend(additions)
    return [t["name"] for t in additions]

def family_for(name):
    low = name.lower()
    if low.startswith("hobgoblin"):
        return "monster-family.hobgoblin"
    if low.startswith("goblin"):
        return "monster-family.goblin"
    if low.startswith("bugbear"):
        return "monster-family.bugbear"
    return None

def main():
    manifest = load(MANIFEST)
    by_id = {row["nativeId"]: row for row in manifest["coverage"]}
    old = {m["name"]: m for m in load(
        REF / "structured/bestiary/bestiary-srd51.json")["monster"]}
    newer = {m["name"]: m for m in load(
        REF / "structured/bestiary/bestiary-srd52.json")["monster"]}
    paired_ids = {"monster." + slug(name) for name in set(old) & set(newer)}
    alias_ids = {"monster." + slug(name) for name in ALIAS_PAIRS}
    already_paired = {
        identity for identity in paired_ids
        if by_id[identity]["status"] == "native-monster-role-default"
    }
    already_aliased = {
        identity for identity in alias_ids
        if by_id[identity]["status"] == "native-monster-identity-alias"
    }
    if already_paired or already_aliased:
        if (already_paired != paired_ids or already_aliased != alias_ids
                or not REVIEW.exists() or not FAMILIES.exists()):
            raise ValueError("Partially reconciled monster catalog; manual review required")
        print(json.dumps({
            "status": "already-reconciled; no changes",
            "pairedRoleDefaults": len(paired_ids),
            "semanticAliases": len(alias_ids),
            "reviewEntries": len(load(REVIEW)["entries"]),
        }, indent=2))
        return
    docs = {}
    changes = Counter()
    legacy_review = []

    def entity(identity):
        row = by_id[identity]
        path = ROOT / row["path"]
        if path not in docs:
            docs[path] = load(path)
        d = docs[path]
        if d["id"] != identity:
            raise ValueError(f"Wrong monster at {path}: {d['id']}")
        return row, d

    for name in sorted(set(old) & set(newer)):
        identity = "monster." + slug(name)
        row, d = entity(identity)
        if row["status"] == "native-monster-role-default":
            continue
        if row["status"] != "multiple-candidates":
            raise ValueError(f"Unexpected status for {identity}: {row['status']}")
        candidates = d.get("candidateDefinitions", [])
        if len(candidates) != 2:
            raise ValueError(f"Expected two candidates: {identity}")
        selected = candidates[1]["content"]
        old_candidate = candidates[0]["content"]
        if selected.get("name") != name or old_candidate.get("name") != name:
            raise ValueError(f"Source order mismatch {identity}")
        expected = newer[name]
        for key in ("hp", "ac", "action", "cr"):
            if selected.get(key) != expected.get(key):
                raise ValueError(f"Not newer source for {identity}: {key}")
        definition = retained_definition(selected, name)
        # Append compatible, nonattack situational traits only. Preserve all
        # other older candidate abilities in the separate review ledger.
        attached = add_independent_traits(old_candidate, definition)
        distinct = legacy_differences(old_candidate, selected)
        if distinct:
            legacy_review.append({
                "nativeId": identity, "familyId": family_for(name),
                "olderAbilitiesNotIncludedAutomatically": distinct,
                "compatibleTraitsTransferred": attached,
                "reason": "Assess semantic replacements, action budget and distinct tactical variants",
            })
        del d["candidateDefinitions"]
        d["definition"] = definition
        d["baselineStatus"] = "retained-native-definition"
        family = family_for(name)
        if family:
            d["familyId"] = family
        row["status"] = "native-monster-role-default"
        row["nativeShapingRequired"] = True
        row["legacyVariantReviewRequired"] = bool(distinct)
        changes["pairedRoleDefaults"] += 1
        changes["compatibleTraitsTransferred"] += len(attached)

    for old_name, new_name in ALIAS_PAIRS.items():
        if old_name not in old or new_name not in newer:
            raise ValueError(f"Missing canonical crosswalk {old_name}: {new_name}")
        identity = "monster." + slug(old_name)
        target = "monster." + slug(new_name)
        row, doc = entity(identity)
        canonical_row, canonical_doc = entity(target)
        if canonical_row["status"] not in {
            "native-single-source-retained", "native-monster-role-default"
        }:
            raise ValueError(f"Target not retained {target}")
        if row["status"] == "native-monster-identity-alias":
            continue
        if row["status"] != "native-single-source-retained":
            raise ValueError(f"Expected source-only alias {identity}: {row['status']}")
        old_content = old[old_name]
        new_content = newer[new_name]
        distinct = legacy_differences(old_content, new_content)
        if distinct:
            legacy_review.append({
                "nativeId": identity,
                "correspondingDefault": target,
                "olderAbilitiesNotIncludedAutomatically": distinct,
                "reason": "Renamed same-role predecessor; review whether distinct tactics warrant another variant",
            })
        if "definition" not in doc:
            raise ValueError(f"Missing retained legacy definition {identity}")
        del doc["definition"]
        doc["baselineStatus"] = "native-identity-alias"
        doc["aliasOf"] = target
        family = family_for(new_name)
        if family:
            doc["familyId"] = family
            canonical_doc["familyId"] = family
        row["status"] = "native-monster-identity-alias"
        row["aliasOf"] = target
        row["nativeShapingRequired"] = False
        row["legacyVariantReviewRequired"] = bool(distinct)
        changes["sourceNameAliases"] += 1

    families = []
    for family_name, members in [
        ("goblin", ("Goblin Minion", "Goblin Warrior", "Goblin Boss")),
        ("hobgoblin", ("Hobgoblin Warrior", "Hobgoblin Captain")),
        ("bugbear", ("Bugbear Warrior", "Bugbear Stalker")),
    ]:
        ids = ["monster." + slug(name) for name in members]
        if any(i not in by_id for i in ids):
            raise ValueError(f"Family has missing variant {family_name}")
        for name in members:
            identity = "monster." + slug(name)
            row, doc = entity(identity)
            if doc.get("baselineStatus") != "retained-native-definition":
                raise ValueError(f"Unretained family variant {identity}")
            doc["familyId"] = "monster-family." + family_name
            doc["variantRole"] = slug(name).removeprefix(family_name + "-")
        families.append({
            "id": "monster-family." + family_name,
            "name": family_name.capitalize(),
            "memberIds": ids,
            "legacyTacticalReview": [
                "monster." + slug(n) for n in DISTINCT_ROLE_REVIEW
                if slug(n) == family_name
            ],
        })
    # Preserve old Hobgoblin and Bugbear as distinct tactical candidates;
    # their existing source-retained definitions are not auto-merged.
    for name in DISTINCT_ROLE_REVIEW:
        identity = "monster." + slug(name)
        row, doc = entity(identity)
        if row["status"] != "native-single-source-retained":
            raise ValueError(f"Unexpected legacy variant status {identity}")
        doc["familyId"] = family_for(name)
        row["legacyVariantReviewRequired"] = True
        legacy_review.append({
            "nativeId": identity, "familyId": family_for(name),
            "reason": DISTINCT_ROLE_REVIEW[name],
            "olderAbilitiesNotIncludedAutomatically": {},
        })

    # Verify every paired source monster was selected and no source-only
    # identity alias was mistakenly elevated to an independent combat variant.
    paired = set(old) & set(newer)
    if changes["pairedRoleDefaults"] not in (0, len(paired)):
        raise ValueError("Paired reconciliation must be all-or-none on first run")
    if changes["sourceNameAliases"] not in (0, len(ALIAS_PAIRS)):
        raise ValueError("Alias crosswalk must be all-or-none on first run")
    for d in docs.values():
        write(ROOT / by_id[d["id"]]["path"], d)
    write(FAMILIES, {"monsterFamilies": families})
    write(REVIEW, {
        "description": "Non-runtime older abilities and identity matches needing variant/compatibility review; not selectable alternate editions.",
        "roleMappings": [{"old": x, "current": y} for x, y in sorted(ALIAS_PAIRS.items())],
        "distinctLegacyRoleCandidates": DISTINCT_ROLE_REVIEW,
        "entries": sorted(legacy_review, key=lambda r: r["nativeId"]),
    })
    counts = Counter(row["status"] for row in manifest["coverage"])
    manifest["reconciliationCounts"] = dict(sorted(counts.items()))
    manifest["reconciliationStatus"] = "5.5-monster-role-defaults-selected; legacy-variant-review-open"
    write(MANIFEST, manifest)
    report = load(REPORT)
    report["statuses"] = dict(sorted(counts.items()))
    report["reconciledByApprovedGeneralRules"] = {
        x: counts[x] for x in (
            "native-compatible-merged", "native-equivalent-reconciled",
            "native-single-source-retained",
        )
    }
    report["remainingWithMultipleCandidates"] = counts["multiple-candidates"]
    report["remainingWithOneCandidate"] = counts["single-candidate"]
    report["nativeMonsterRoleDefaults"] = counts["native-monster-role-default"]
    report["nativeMonsterIdentityAliases"] = counts["native-monster-identity-alias"]
    report["monsterFamilyDefinitions"] = len(families)
    report["monsterLegacyVariantReviewEntries"] = len(legacy_review)
    report["monsterCompatibleTraitsTransferred"] = changes["compatibleTraitsTransferred"]
    report["monsterOlderOnlySourceEntriesStillRetained"] = sum(
        row["kind"] == "monster" and row["status"] == "native-single-source-retained"
        for row in manifest["coverage"]
    )
    report["retainedDefinitionsNeedingNativeShaping"] = sum(
        counts[status] for status in (
            "native-compatible-merged", "native-equivalent-reconciled",
            "native-single-source-retained", "native-monster-role-default",
        )
    )
    report["candidateRecordsStillUnresolved"] = counts["multiple-candidates"] + counts["single-candidate"]
    report["originalUnresolvedCandidateCount"] = 2479
    report["reducedUnresolvedCandidateCount"] = 2479 - report["candidateRecordsStillUnresolved"]
    report["monsterReviewCaveat"] = (
        "Newer paired stat-block defaults and semantic aliases are selected; "
        "older unique mechanics and some legacy tactical roles still need separate "
        "variant review. Selected definitions still require typed-native conversion."
    )
    write(REPORT, report)
    print(json.dumps({"changes": dict(changes), "counts": dict(counts),
                      "legacyReviewEntries": len(legacy_review),
                      "unresolved": report["candidateRecordsStillUnresolved"]}, indent=2))

if __name__ == "__main__":
    main()
