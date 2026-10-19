#!/usr/bin/env python3
"""Restore demonstrably compatible removed SRD 5.1 monster abilities.

The chosen SRD 5.2.1 stat block, including its ordinary attacks, numerical
statistics and native creature identity, remains authoritative. Restore
independent older options without manufacturing new monsters or duplicating
changed attacks. Maintain a feature-level, non-runtime review inventory.
"""
from collections import Counter
from pathlib import Path
import copy
import json
import re

from reconcile_native_baseline import ROOT, MANIFEST, REPORT, load, write
from reconcile_monster_families import normalized_name

REFERENCE = ROOT / "data/reference/srd"
LEDGER = REFERENCE / "monster-legacy-variant-review.json"
REPORT_FILE = ROOT / "docs/rewrite/monster-legacy-feature-results.json"

# These older features represent self-contained benefits or limitations with
# no identified replacement in the newer stat blocks. Names are deliberately
# explicit: this list does not authorize any feature having an unfamiliar name.
INDEPENDENT_TRAITS = {
    "Antimagic Susceptibility", "Blood Frenzy", "Brave",
    "Dark Devotion", "Devil's Sight", "Fey Ancestry",
    "Steadfast", "Two Heads", "Two-Headed",
    "Telepathic Bond", "Limited Telepathy", "Blind Senses",
    "Pack Tactics", "Faultless Tracker", "Freedom of Movement",
    "Turn Resistance", "Reckless",
    "Relentless (Recharges after a Short or Long Rest)",
    "Keen Senses", "Web Walker", "Turning Defiance",
    "Martial Advantage", "Surprise Attack",
}
# Independent utility actions add choices; they do not alter Multiattack
# or overwrite a newer action with different damage or saving throw rules.
INDEPENDENT_ACTIONS = {
    "Children of the Night (1/Day)",
    "Leadership (Recharges after a Short or Long Rest)",
}
# All these preserve the original legendary action cost in their names and
# use the selected monster's existing legendary-action economy. Cost tags
# require typed conversion in the later native execution-schema pass.
INDEPENDENT_LEGENDARY = {
    "Wing Attack (Costs 2 Actions)", "Detect",
    "Move", "Ink Cloud (Costs 3 Actions)",
    "Blinding Dust", "Blasphemous Word (Costs 2 Actions)",
    "Searing Burst (Costs 2 Actions)",
    "Heal Self (Costs 3 Actions)", "Teleport",
}
# These mephits lost a stand-alone casting feature and have no replacement
# spellcasting entry or matching spell access in the newer stat block.
INDEPENDENT_SPELLCASTING = {"Magma Mephit"}
# Clearly renamed/repositioned abilities, excluded from automatic copying.
# Not all are numerically equivalent; a replacement should not be doubled.
REPOSITIONED_NAMES = {
    "Mucous Cloud": ("Mucus Cloud",),
    "Aversion of Fire": ("Aversion to Fire",),
    "Petrifying Gaze": ("Petrifying Gaze",),
    "Shapechanger": ("Shape-Shift",),
    "Vampire Weaknesses": ("Vampire Weakness",),
    "Trampling Charge": ("Trample",),
    "Horrific Appearance": ("Vile Appearance",),
    "Rust Metal": ("Destroy Metal", "Antennae"),
    "Corrode Metal": ("Corrosive Form",),
    "Grasping Tendrils": ("Tentacle", "Reel"),
    "Unnerving Mask": ("Unnerving Gaze",),
    "Shield": ("Protection",),
    "Rock Catching": ("Deflect Missile",),
    "Wall of Ice {@recharge}": ("Ice Wall",),
    "Teleport": ("Teleport",),  # normal action -> bonus, but Solar legendary action restored separately
    "Animate Chains (Recharges after a Short or Long Rest)": ("Conjure Infernal Chain",),
    "Horrifying Visage": ("Horrific Visage",),
    "Frightful Presence": ("Frightful Presence",),
    "Draining Kiss": ("Restless Touch", "Draining Kiss"),
    "Etherealness": ("Spellcasting",),
    "Fey Charm": ("Spellcasting",),
    "Damage Transfer": ("Attach", "Smother"),
    "Grappler": ("Bite",),
    "Tail Spike Regrowth": ("Tail Spike",),
}
# These were redesigned; do not assume they were removed. Review the
# mechanics of the new successor, rather than duplicating the old one.
REPLACEMENT_FAMILIES = {
    "Rejuvenation": ("Restoration", "Spirit Jar"),
    "Elemental Demise": ("Restoration",),
    "Hellish Rejuvenation": ("Restoration",),
    "Regeneration": ("Regeneration", "Bite", "Misty Escape"),
    "Limited Magic Immunity": ("Greater Magic Resistance",),
    "Charge": ("Gore", "Ram", "Charge", "Pounce"),
    "Pounce": ("Pounce", "Roar", "Rend", "Scratch"),
    "Sneak Attack (1/Turn)": ("Sneak Attack", "Cunning Action"),
    "Magic Weapons": ("Magic Resistance", "Magic Weapons"),
    "Angelic Weapons": ("Holy", "Radiant"),
    "Hellish Weapons": ("Infernal", "Withering"),
    "Heated Body": ("Fire Aura",),
    "Heated Weapons": ("Burning Hammer", "Fire Aura"),
    "Vampire Weaknesses": ("Vampire Weakness",),
    "Shapechanger": ("Shape-Shift",),
    "Petrifying Gaze": ("Petrifying Gaze",),
    "Mucous Cloud": ("Mucus Cloud",),
}
PHYSICAL_ATTACK = re.compile(
    r"\{@atk\b|\{@atkr\b|\{@hit\s|\{@damage\s|"
    r"\bmakes? (?:one|two|three|\d+) (?:melee |weapon |ranged )?attacks?\b",
    re.I,
)
NON_PHYSICAL_ATTACK_ACTIONS = {
    "Frightful Presence", "Change Shape", "Etherealness",
    "Enslave (3/Day)", "Children of the Night (1/Day)",
}

def feature_names(record):
    return {
        normalized_name(f["name"])
        for kind in ("trait", "action", "bonus", "reaction",
                     "legendary", "spellcasting")
        for f in record.get(kind, [])
        if isinstance(f, dict) and f.get("name")
    }

def named_from_source(record, kind, name):
    return next((entry for entry in record.get(kind, [])
                 if entry.get("name") == name), None)

def logical_successor(old_name, current):
    targets = REPOSITIONED_NAMES.get(old_name, ())
    if (old_name == "Innate Spellcasting (1/Day)"
            and normalized_name("Blurred Form") in feature_names(current)):
        return "Blurred Form"
    now = feature_names(current)
    for target in targets:
        normalized = normalized_name(target)
        if normalized in now or any(x.startswith(normalized + "-") for x in now):
            return target
    return None

def apply():
    manifest = load(MANIFEST)
    rows = {r["nativeId"]: r for r in manifest["coverage"]}
    older = {m["name"]: m for m in load(
        REFERENCE / "structured/bestiary/bestiary-srd51.json")["monster"]}
    newer = {m["name"]: m for m in load(
        REFERENCE / "structured/bestiary/bestiary-srd52.json")["monster"]}
    ledger = load(LEDGER)
    aliases = {r["old"]: r["current"] for r in ledger["roleMappings"]}
    by_slug = {
        re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-"): name
        for name in older
    }
    changed = {}
    counts = Counter()
    detailed = []
    for entry in ledger["entries"]:
        identity = entry["nativeId"]
        old_name = by_slug.get(identity.removeprefix("monster."))
        if old_name is None:
            raise ValueError(f"Missing source monster {identity}")
        old = older[old_name]
        new_name = aliases.get(old_name, old_name)
        if new_name not in newer:
            raise ValueError(f"Missing newer counterpart for {identity}")
        current_id = "monster." + re.sub(
            r"[^a-z0-9]+", "-", new_name.lower()).strip("-")
        path = ROOT / rows[current_id]["path"]
        if path not in changed:
            changed[path] = load(path)
        native = changed[path]
        definition = native.get("definition")
        if not isinstance(definition, dict):
            raise ValueError(f"No selected monster definition: {current_id}")
        status_by_kind = {}
        for kind, names in entry["olderAbilitiesNotIncludedAutomatically"].items():
            status_by_kind[kind] = []
            for name in names:
                feature = named_from_source(old, kind, name)
                if feature is None:
                    raise ValueError(f"Source feature missing {identity} {kind} {name}")
                now = feature_names(definition)
                key = normalized_name(name)
                status = "review-needed"
                note = ""
                if key in now:
                    status, note = "already-present", "Ability name exists in selected creature; no second copy"
                else:
                    successor = logical_successor(name, definition)
                    if successor is not None:
                        status, note = "newer-successor", successor
                    else:
                        allowed = {
                            "trait": INDEPENDENT_TRAITS,
                            "action": INDEPENDENT_ACTIONS,
                            "legendary": INDEPENDENT_LEGENDARY,
                        }.get(kind, set())
                        if name in allowed:
                            # Do not append an action to a creature with no
                            # applicable resource pool, or a new old-style
                            # legendary ability to a nonlegendary creature.
                            if kind != "legendary" or definition.get("legendary"):
                                definition.setdefault(kind, []).append(copy.deepcopy(feature))
                                status, note = "restored", "Preserved source limits and action costs"
                                counts[f"restored:{kind}"] += 1
                        elif (kind == "spellcasting"
                              and old_name in INDEPENDENT_SPELLCASTING
                              and not definition.get("spellcasting")):
                            text = json.dumps({
                                key: definition.get(key, [])
                                for key in ("trait", "action", "bonus", "reaction",
                                            "legendary", "spellcasting")
                            }, ensure_ascii=False).lower()
                            # Guard against an existing action containing the
                            # spell under another representation.
                            spell_name = "heat metal" if old_name == "Magma Mephit" else "blur"
                            if spell_name not in text:
                                definition.setdefault(kind, []).append(copy.deepcopy(feature))
                                status, note = "restored", "Original limited spellcasting retained"
                                counts["restored:spellcasting"] += 1
                        elif kind == "action":
                            content = json.dumps(feature.get("entries", []))
                            if (PHYSICAL_ATTACK.search(content)
                                    and name not in NON_PHYSICAL_ATTACK_ACTIONS):
                                status = "revised-attack-suite"
                                note = "5.5 action statistics and Multiattack remain authoritative; no duplicate legacy attack"
                        elif name in REPLACEMENT_FAMILIES:
                            status = "possible-redesign"
                            note = ", ".join(REPLACEMENT_FAMILIES[name])
                status_by_kind[kind].append({
                    "name": name, "status": status, "note": note,
                })
                counts["feature:" + status] += 1
        entry["featureDisposition"] = status_by_kind
        unresolved = sum(
            f["status"] in ("review-needed", "possible-redesign")
            for group in status_by_kind.values() for f in group
        )
        entry["remainingFeatureQuestions"] = unresolved
        if unresolved:
            counts["monstersWithOpenFeatureQuestions"] += 1
        detailed.append({"id": identity, "target": current_id, "questions": unresolved})

    for path, obj in changed.items():
        write(path, obj)
    ledger["featureReviewPolicy"] = (
        "Retain 5.5e attack values and procedures; restore genuinely independent "
        "5e abilities; do not duplicate renamed or relocated abilities. "
        "Each remaining feature question is evaluated within one creature."
    )
    ledger["summary"] = dict(sorted(counts.items()))
    write(LEDGER, ledger)
    report = load(REPORT)
    report["monsterAdditionalOlderFeaturesRestored"] = sum(
        n for key, n in counts.items() if key.startswith("restored:")
    )
    report["monsterOlderFeaturesPreviouslyTransferred"] = report.get(
        "monsterCompatibleTraitsTransferred", 0)
    report["monsterRemainingFeatureQuestions"] = sum(
        row["questions"] for row in detailed
    )
    report["monsterReviewEntriesWithOpenQuestions"] = counts["monstersWithOpenFeatureQuestions"]
    report["monsterFeatureDispositions"] = {
        k.removeprefix("feature:"): v
        for k, v in counts.items() if k.startswith("feature:")
    }
    report["monsterReviewCaveat"] = (
        "Selected 5.5e stat blocks are retained. Independent missing older "
        "abilities restored with exact original restrictions; revised attack "
        "suites are not duplicated. Remaining differences are tracked per "
        "feature in the reference ledger. Imported markup still needs native typing."
    )
    write(REPORT, report)
    write(REPORT_FILE, {
        "summary": dict(sorted(counts.items())),
        "featuresRestoredThisPass": report["monsterAdditionalOlderFeaturesRestored"],
        "unresolvedFeatureQuestions": report["monsterRemainingFeatureQuestions"],
        "reviewEntriesWithOpenQuestions": report["monsterReviewEntriesWithOpenQuestions"],
        "note": "Changed attack suites and moved abilities do not create new monster identities.",
    })
    print(json.dumps(load(REPORT_FILE), indent=2))

if __name__ == "__main__":
    apply()
