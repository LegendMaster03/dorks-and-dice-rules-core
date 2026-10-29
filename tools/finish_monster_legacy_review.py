#!/usr/bin/env python3
"""Second monster pass: independent older actions and missing spell access.

Preserve chosen 5.5e stats, attacks, spellcasting ability and resource timing.
Mark old attack/feature successors instead of copying obsolete procedures.
"""
from collections import Counter
import copy
import json
import re

from reconcile_native_baseline import ROOT, load, write
from reconcile_monster_families import normalized_name

REF = ROOT / "data/reference/srd"
LEDGER = REF / "monster-legacy-variant-review.json"
STATS = ROOT / "docs/rewrite/native-reconciliation-statistics.json"
RESULTS = ROOT / "docs/rewrite/monster-legacy-feature-results.json"
SPELL = re.compile(r"\{@spell\s+([^|}]+)", re.I)
RESTORE_ACTIONS = {
    "Change Shape", "Frightful Presence",
    "Healing Touch (3/Day)", "Healing Touch (4/Day)",
    "Haste {@recharge 5}", "Charm", "Etherealness",
    "Teleport (1/Day)",
}
RESTORE_TRAITS = {"False Appearance", "Variable Illumination", "Ambusher"}
OBSOLETE_LEGENDARY_ATTACKS = {
    "Tail Attack", "Tail Swipe", "Tentacle Attack or Fling",
    "Lightning Storm (Costs 2 Actions)", "Chomp (Costs 2 Actions)",
    "Unarmed Strike", "Attack",
}
# Effects that have an identifiable 5.5e successor. We retain the newer
# procedure, not both different default procedures on the same creature.
SUCCESSORS = {
    "Enslave (3/Day)": "Dominate Mind (2/Day)",
    "Tentacles": "Paralyzing Tentacles",
    "Death Glare": "Death Glare",
    "Invisibility": "Vanish",
    "Illusory Appearance": "Disguise Self",
    "Invisible Passage": "Invisibility",
    "Frightful Presence": "Thunderous Bellow",
    "Etherealness": "Etherealness",
    "Petrifying Gaze": "Petrifying Gaze",
    "Trampling Charge": "Trample",
}
ATTACK_STYLE_FIELDS = ("action", "bonus", "trait", "legendary", "spellcasting")

def spell_names(content):
    return {match.lower().strip() for match in SPELL.findall(
        json.dumps(content, ensure_ascii=False))}

def source_spell_grants(source_record):
    records = []
    for feature in source_record.get("spellcasting", []):
        for sp in feature.get("will", []):
            records.extend(("will", None, sp, m.lower().strip())
                           for m in SPELL.findall(sp))
        daily = feature.get("daily", {})
        if isinstance(daily, dict):
            for freq, spells in daily.items():
                for sp in spells:
                    records.extend(("daily", freq, sp, m.lower().strip())
                                   for m in SPELL.findall(sp))
        elif isinstance(daily, list):
            for sp in daily:
                records.extend(("daily", "1e", sp, m.lower().strip())
                               for m in SPELL.findall(sp))
    return records

def find_successor(old_name, definition):
    now = {
        normalized_name(part["name"])
        for kind in ATTACK_STYLE_FIELDS
        for part in definition.get(kind, [])
        if isinstance(part, dict) and part.get("name")
    }
    target = SUCCESSORS.get(old_name)
    if target:
        n = normalized_name(target)
        if any(val == n or val.startswith(n + "-") for val in now):
            return target
        if target.lower() in json.dumps(
            {k: definition.get(k, []) for k in ATTACK_STYLE_FIELDS}
        ).lower():
            return target
    return None

def apply():
    ledger = load(LEDGER)
    report = load(STATS)
    rows = {x["nativeId"]: x for x in load(
        REF / "native-coverage-manifest.json")["coverage"]}
    old = {m["name"]: m for m in load(
        REF / "structured/bestiary/bestiary-srd51.json")["monster"]}
    source_name = {
        re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-"): name
        for name in old
    }
    docs = {}
    delta = Counter()
    spells_restored = {}
    for record in ledger["entries"]:
        original = old[source_name[record["nativeId"].removeprefix("monster.")]]
        target = record.get("correspondingDefault", record["nativeId"])
        path = ROOT / rows[target]["path"]
        if path not in docs:
            docs[path] = load(path)
        definition = docs[path]["definition"]
        for kind, features in record["featureDisposition"].items():
            for f in features:
                if f["status"] not in ("review-needed", "possible-redesign"):
                    continue
                name = f["name"]
                current = {
                    normalized_name(x["name"])
                    for field in ATTACK_STYLE_FIELDS
                    for x in definition.get(field, [])
                    if isinstance(x, dict) and x.get("name")
                }
                if normalized_name(name) in current:
                    f["status"] = "already-present"
                    f["note"] = "Already available in the selected creature"
                    continue
                if kind == "spellcasting":
                    # Do not duplicate an entirely different old spellcasting
                    # trait. Transfer only absent individual spell grants to
                    # the newer Spellcasting ability and its casting ability.
                    existing = spell_names({
                        k: definition.get(k, []) for k in ATTACK_STYLE_FIELDS
                    })
                    missing = [grant for grant in source_spell_grants(original)
                               if grant[3] not in existing]
                    primary = next((
                        ability for ability in definition.get("spellcasting", [])
                        if ability.get("name") == "Spellcasting"
                    ), None)
                    if not missing:
                        f["status"] = "newer-successor"
                        f["note"] = "Every older listed spell is represented in newer abilities"
                        continue
                    if primary is not None:
                        unique = {}
                        for typ, freq, text, spell in missing:
                            unique.setdefault(spell, (typ, freq, text, spell))
                        for typ, freq, text, spell in unique.values():
                            if typ == "will":
                                primary.setdefault("will", []).append(text)
                            else:
                                primary.setdefault("daily", {}).setdefault(freq, []).append(text)
                            delta["spellsRestored"] += 1
                        spells_restored[record["nativeId"]] = sorted(unique)
                        f["status"] = "spell-access-extended"
                        f["note"] = "Added missing spells using newer Spellcasting ability and older frequency: " + ", ".join(sorted(unique))
                        continue
                    f["note"] = "Missing spell access without a compatible newer general Spellcasting feature: " + ", ".join(sorted(set(x[3] for x in missing)))
                    continue

                successor = find_successor(name, definition)
                if successor:
                    f["status"] = "newer-successor"
                    f["note"] = successor + " is the selected newer procedure"
                    continue
                if kind == "legendary" and name in OBSOLETE_LEGENDARY_ATTACKS:
                    f["status"] = "revised-attack-suite"
                    f["note"] = "Refers to removed physical attack or replaces the same attack role; selected 5.5e suite governs"
                    continue
                if kind == "action" and name == "Multiattack":
                    f["status"] = "revised-attack-suite"
                    f["note"] = "5.5e attack sequencing is authoritative"
                    continue
                if kind == "trait" and name in RESTORE_TRAITS:
                    original_feature = next(x for x in original["trait"]
                                            if x["name"] == name)
                    definition.setdefault("trait", []).append(copy.deepcopy(original_feature))
                    f["status"] = "restored"
                    f["note"] = "Reinstated as an independent older capability"
                    delta["traitRestored"] += 1
                    continue
                if kind == "action" and name in RESTORE_ACTIONS:
                    # A distinct 5.5e fright effect already covers Tarrasque.
                    if name == "Frightful Presence" and not original["name"].endswith("Dragon"):
                        continue
                    # Spell equivalents take priority over an additional
                    # older action, e.g. Shapechange on Couatl/Deva.
                    if name == "Change Shape" and (
                        "shapechange" in spell_names(definition.get("spellcasting", []))
                        or "polymorph" in spell_names(definition.get("spellcasting", []))
                    ):
                        f["status"] = "newer-successor"
                        f["note"] = "Newer Shapechange/Polymorph spell access"
                        continue
                    # Avoid copying spells the creature already casts, such
                    # as Etherealness on Incubus.
                    if (name == "Etherealness"
                            and "etherealness" in spell_names(definition.get("spellcasting", []))):
                        f["status"] = "newer-successor"
                        f["note"] = "Etherealness already granted by newer Spellcasting"
                        continue
                    source_action = next(x for x in original.get("action", [])
                                         if x["name"] == name)
                    definition.setdefault("action", []).append(copy.deepcopy(source_action))
                    f["status"] = "restored"
                    f["note"] = "Independent older action retained alongside newer attacks"
                    delta["actionRestored"] += 1
                    continue
                # A changed 5.5e damage/recharge procedure with an identifiable
                # equivalent is not a missing additional action.
                if kind == "trait" and name in ("Charge", "Pounce",
                                                "Trampling Charge"):
                    joined = json.dumps({
                        k: definition.get(k, []) for k in ATTACK_STYLE_FIELDS
                    }).lower()
                    if ("moved" in joined or "charge" in joined or "prone" in joined):
                        f["status"] = "newer-successor"
                        f["note"] = "Movement/contact rider is represented in the newer combat action"
                        continue
        record["remainingFeatureQuestions"] = sum(
            f["status"] in ("review-needed", "possible-redesign")
            for group in record["featureDisposition"].values()
            for f in group
        )

    for path, doc in docs.items():
        write(path, doc)
    counts = Counter()
    for record in ledger["entries"]:
        if record["remainingFeatureQuestions"]:
            counts["monstersWithOpenFeatureQuestions"] += 1
        for group in record["featureDisposition"].values():
            for f in group:
                counts["feature:" + f["status"]] += 1
    ledger["summary"] = dict(sorted(counts.items()))
    write(LEDGER, ledger)
    report["monsterAdditionalOlderFeaturesRestored"] += (
        delta["traitRestored"] + delta["actionRestored"]
    )
    report["monsterAdditionalSpellsRestored"] = delta["spellsRestored"]
    report["monsterRemainingFeatureQuestions"] = sum(
        record["remainingFeatureQuestions"] for record in ledger["entries"]
    )
    report["monsterReviewEntriesWithOpenQuestions"] = counts["monstersWithOpenFeatureQuestions"]
    report["monsterFeatureDispositions"] = {
        k.removeprefix("feature:"): v for k, v in counts.items()
        if k.startswith("feature:")
    }
    write(STATS, report)
    write(RESULTS, {
        "summary": dict(sorted(counts.items())),
        "featuresRestoredThisPass": delta["actionRestored"] + delta["traitRestored"],
        "totalAdditionalOlderFeaturesRestored": report["monsterAdditionalOlderFeaturesRestored"],
        "additionalSpellsRestored": delta["spellsRestored"],
        "spellsAddedByMonster": spells_restored,
        "unresolvedFeatureQuestions": report["monsterRemainingFeatureQuestions"],
        "reviewEntriesWithOpenQuestions": counts["monstersWithOpenFeatureQuestions"],
        "note": "5.5e stats, attack suites and spellcasting ability remain authoritative.",
    })
    print(json.dumps({
        "changes": dict(delta),
        "unresolved": report["monsterRemainingFeatureQuestions"],
        "entriesWithQuestions": report["monsterReviewEntriesWithOpenQuestions"]
    }, indent=2))

if __name__ == "__main__":
    apply()
