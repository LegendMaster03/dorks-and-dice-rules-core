#!/usr/bin/env python3
"""ONE-TIME staging of the existing pinned SRD-only structured pack into native JSON.

Not a runtime importer, not a rules engine, and not a reconciliation policy.
Removes itself from the branch after successful generation and verification.
Native records not yet decided use pending candidateDefinitions (no active definition).
"""
import collections
import copy
import hashlib
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE = ROOT / "data/reference/srd/structured"
NATIVE = ROOT / "data/native"
REFERENCE = ROOT / "data/reference/srd"
VERSIONS = {"SRD51": "candidate-a", "SRD52": "candidate-b"}
SOURCE_VERSION = {"SRD51": "5.1", "SRD52": "5.2.1"}

# Small, self-contained catalogs remain consolidated; all other entities
# are independently maintained files. No category folder per SRD version.
COMPACT = {
    "action": "actions.json",
    "condition": "conditions.json",
    "disease": "diseases.json",
    "status": "statuses.json",
    "sense": "senses.json",
    "language": "languages.json",
    "skill": "skills.json",
    "itemProperty": "item-properties.json",
    "itemType": "item-types.json",
    "itemMastery": "weapon-masteries.json",
    "itemGroup": "item-groups.json",
    "table": "tables.json",
    "variantrule": "general-rules.json",
}
PATH_BY_KIND = {
    "monster": "bestiary",
    "spell": "spells",
    "class": "classes/base",
    "subclass": "classes/subclasses",
    "race": "species/base",
    "subrace": "species/subspecies",
    "item": "items/magic",
    "baseitem": "items/equipment",
    "itemEntry": "items/entries",
    "magicvariant": "items/variants",
    "feat": "feats",
    "background": "backgrounds",
    "optionalfeature": "features/optional",
    "classFeature": "features/classes",
    "subclassFeature": "features/subclasses",
    "deity": "deities",
    "hazard": "hazards",
    "trap": "traps",
    "vehicle": "vehicles",
    "object": "objects",
}
FLUFF_KEYS = {
    "monsterFluff": "monster", "spellFluff": "spell",
    "classFluff": "class", "subclassFluff": "subclass",
    "raceFluff": "race", "subraceFluff": "subrace",
    "backgroundFluff": "background", "featFluff": "feat",
    "itemFluff": "item", "optionalfeatureFluff": "optionalfeature",
    "conditionFluff": "condition", "diseaseFluff": "disease",
    "languageFluff": "language", "objectFluff": "object",
    "trapFluff": "trap", "hazardFluff": "hazard",
    "vehicleFluff": "vehicle",
}
# These keys contain publication markup, not native mechanics.
ROOT_META_KEYS = {
    "source", "page", "reprintedAs", "basicRules", "basicRules2024",
    "srd", "srd52", "edition", "otherSources", "hasFluff",
    "hasFluffImages",
}
def slug(value):
    x = re.sub(r"[^a-z0-9]+", "-", str(value).casefold()).strip("-")
    return x or "unnamed"

def classify(kind, record):
    class_slug = slug(record.get("className") or record.get("class") or "unassigned")
    sub_slug = slug(record.get("subclassShortName") or record.get("subclassName") or "unassigned")
    species_slug = slug(record.get("raceName") or record.get("race") or "unassigned")
    name = record.get("name") or record.get("id") or record.get("caption")
    # A missing name needs a stable fingerprint for a reproducible record ID.
    if not name:
        name = "unnamed-" + hashlib.sha256(json.dumps(
            record, sort_keys=True, ensure_ascii=False).encode()).hexdigest()[:16]
    token = slug(name)
    directory = PATH_BY_KIND.get(kind)
    if kind == "subclass":
        directory = f"classes/subclasses/{class_slug}"
    elif kind == "subrace":
        directory = f"species/subspecies/{species_slug}"
    elif kind == "classFeature":
        directory = f"features/classes/{class_slug}"
    elif kind == "subclassFeature":
        directory = f"features/subclasses/{class_slug}/{sub_slug}"
    # Include feature level in path to avoid grouping distinct advancement grants.
    if kind in ("classFeature", "subclassFeature") and record.get("level") is not None:
        token = f"{token}-level-{record['level']}"
    if kind == "subclass":
        token = slug(record.get("shortName") or name)
    key = (kind, class_slug if kind in ("subclass","classFeature","subclassFeature") else "",
           sub_slug if kind == "subclassFeature" else "",
           species_slug if kind == "subrace" else "",
           (slug(record.get("shortName") or name) if kind=="subclass" else slug(name)),
           str(record.get("level","")) if kind in ("classFeature","subclassFeature") else "",
           str(record.get("shortName","")) if kind == "subclass" else "")
    return key, directory, token, str(name)

def native_payload(record):
    # Original source version/provenance must be kept in the reference pack,
    # not inside the future runtime definition. Preserve gameplay properties
    # and body exactly as supplied, including unresolved 5etools markup.
    return {k: copy.deepcopy(v) for k,v in record.items() if k not in ROOT_META_KEYS}

def build():
    manifest = json.loads((REFERENCE/"structured-manifest.json").read_text())
    # Reject drift from the already vetted pinned reference inputs.
    recorded={x["path"] for x in manifest["files"]}
    actual={p.relative_to(SOURCE).as_posix() for p in SOURCE.rglob("*.json")}
    assert recorded == actual, "Reference pack contents changed"
    for e in manifest["files"]:
        raw=(SOURCE/e["path"]).read_bytes()
        assert len(raw)==e["bytes"]
        assert hashlib.sha256(raw).hexdigest()==e["sha256"]

    groups=collections.defaultdict(lambda:collections.defaultdict(list))
    narrative=collections.defaultdict(lambda:collections.defaultdict(list))
    metadata_rows=collections.defaultdict(list)
    input_summary=collections.Counter()
    unsupported=[]
    for filepath in sorted(SOURCE.rglob("*.json")):
        rel=filepath.relative_to(SOURCE).as_posix()
        # These are precomputed UI indexes / Foundry conversion enhancements,
        # not primary mechanical or rulebook entities. They are already included
        # in the complete reference and counted in the coverage manifest.
        if (rel.startswith("generated/") or "foundry" in rel or
            rel.endswith("index.json") or rel in ("spells/sources.json","books.json")):
            metadata_rows["reference-only"].append(rel)
            continue
        if rel.startswith("book/"):
            document=json.loads(filepath.read_text())
            source="SRD51" if rel.endswith("srd51.json") else "SRD52" if rel.endswith("srd52.json") else None
            if source is None: unsupported.append({"path":rel,"reason":"unknown-book-version"});continue
            for ix,section in enumerate(document.get("data", [])):
                if not isinstance(section,dict):
                    unsupported.append({"path":rel,"reason":"non-object-section","index":ix})
                    continue
                name=section.get("name") or f"section-{ix+1}"
                key=("ruleChapter",slug(name))
                groups[key][source].append({"record":section,"file":rel,"index":ix})
                input_summary["ruleChapter:"+source]+=1
            continue
        document=json.loads(filepath.read_text())
        if not isinstance(document,dict):
            metadata_rows["non-object-json"].append(rel)
            continue
        for kind,records in document.items():
            if not isinstance(records,list) or kind.startswith("_"):
                continue
            for idx,record in enumerate(records):
                if not isinstance(record,dict):
                    unsupported.append({"path":rel,"kind":kind,"index":idx,"reason":"non-object-record"})
                    continue
                source=record.get("source")
                if source not in VERSIONS:
                    unsupported.append({"path":rel,"kind":kind,"index":idx,"source":source,"reason":"not-marked-SRD51-or-SRD52"})
                    continue
                target=FLUFF_KEYS.get(kind)
                if target:
                    key,*_=classify(target,record)
                    narrative[key][source].append({"record":record,"file":rel,"index":idx})
                else:
                    key,*_=classify(kind,record)
                    groups[key][source].append({"record":record,"file":rel,"index":idx})
                input_summary[kind+":"+source]+=1

    # Existing native skill/competency decisions and six condition rulings
    # are authoritative; do NOT clobber them with source variants.
    existing={}
    for fname,prop in [("skills.json","skills"),("competencies.json","competencies"),("conditions.json","conditions")]:
        obj=json.loads((NATIVE/fname).read_text())
        existing[fname]=(obj,prop,{x["name"].casefold():x for x in obj[prop]})
    condition_data,_,condition_by_name=existing["conditions.json"]
    skill_by_name=existing["skills.json"][2]
    coverage=[]
    warnings=[]
    output=collections.defaultdict(dict)
    output_ids=set()
    natives=collections.Counter()
    duplicates=collections.Counter()

    def add_rel(path, data, key):
        if path in output and output[path] != data:
            raise RuntimeError("Native filename collision: "+path)
        if key in output_ids:
            raise RuntimeError("Native ID collision: "+str(key))
        output[path]=data
        output_ids.add(key)
        natives[path.rsplit("/",1)[0]]+=1

    for key,versions in sorted(groups.items(),key=lambda e:str(e[0])):
        kind=key[0]
        first=versions.get("SRD51",[])
        second=versions.get("SRD52",[])
        # Group by genuine record identity: duplicates with same name and level
        # within a single source are *not* silently overwritten or chosen.
        if len(first)>1 or len(second)>1:
            duplicates[kind]+=1
        rep=(first or second)[0]["record"]
        display=rep.get("name") or rep.get("id") or rep.get("caption") or ("Section "+str((first or second)[0]["index"]+1))
        id_name=slug(display)
        if kind=="ruleChapter":
            directory="rules";token=slug(display)
            cid="rule."+token
        else:
            _,directory,token,_=classify(kind,rep)
            cid=(("species" if kind=="race" else "subspecies" if kind=="subrace" else kind)+
                "."+slug(display))
            # Parent/level disambiguates IDs for subclass and features.
            if kind in ("subclass","subrace","classFeature","subclassFeature"):
                cid+= "."+".".join(str(p) for p in key[1:] if p)
        if kind=="skill" and display.casefold() in skill_by_name:
            coverage.append({"nativeId":skill_by_name[display.casefold()]["id"],"kind":kind,"status":"existing-native-covered",
             "sourceRecords":len(first)+len(second)})
            continue
        if kind=="condition" and display.casefold() in condition_by_name:
            coverage.append({"nativeId":condition_by_name[display.casefold()]["id"],"kind":kind,"status":"existing-native-covered",
             "sourceRecords":len(first)+len(second)})
            continue
        if kind=="skill":
            # Do not add any SRD-specific skill taxonomy to a second file.
            warnings.append({"kind":kind,"name":display,"reason":"source-skill-not-in-approved-native-catalog"})
            continue
        if kind=="condition":
            directory=None
        if not directory and kind not in COMPACT:
            warnings.append({"kind":kind,"name":display,"reason":"unmapped-kind"})
            continue

        enriched={}
        for src,rows in versions.items():
            bodies=[native_payload(r["record"]) for r in rows]
            if kind=="ruleChapter":
                bodies=[copy.deepcopy(r["record"]) for r in rows]
            fluff=narrative.get(key,{}).get(src,[])
            if fluff:
                # Keep narrative with the matching entity, not a companion
                # fluff file. If there is no corresponding mechanical entity
                # at all, it will be staged as an independent lore entry below.
                for body in bodies:
                    body["lore"]=[native_payload(r["record"]) for r in fluff]
            enriched[src]=bodies

        # Same meaning cannot be asserted from matching names or partial
        # cosmetic normalization; every unreviewed item stays non-executable.
        resolved=None
        if len(enriched)==1 and all(len(x)==1 for x in enriched.values()):
            resolved="single-candidate"
        elif len(enriched)==2 and all(len(x)==1 for x in enriched.values()) and enriched["SRD51"][0]==enriched["SRD52"][0]:
            resolved="matching-candidates"
        else:
            resolved="multiple-candidates"
        candidates=[]
        for source in ("SRD51","SRD52"):
            for ix,body in enumerate(enriched.get(source,[])):
                cid_key=VERSIONS[source]+(f"-{ix+1}" if len(enriched[source])>1 else "")
                candidates.append({"key":cid_key,"content":body})
        doc={
          "id":cid,"name":str(display),"kind":("species" if kind=="race" else "subspecies" if kind=="subrace" else kind),
          "baselineStatus":"pending-native-reconciliation",
          "candidateDefinitions":candidates
        }
        # These relations are meaningful native concepts, not edition aliases.
        if kind in ("subclass","classFeature","subclassFeature"):
            doc["parentClass"]="class."+key[1]
        if kind=="subclassFeature":
            doc["parentSubclass"]=key[2]
        if kind=="subrace":
            doc["parentSpecies"]="species."+key[3]
        if kind in ("classFeature","subclassFeature"):
            doc["classLevel"]=rep.get("level")
        if kind=="ruleChapter":
            p=f"rules/{token}.json"
        elif kind in COMPACT:
            p=COMPACT[kind]
        else:
            p=f"{directory}/{token}.json"
        if p in COMPACT.values():
            # Consolidated catalogs remain single files, without duplicating
            # existing approved native Skills or Conditions.
            if cid in output[p]:
                raise RuntimeError("Native compact catalog ID collision: "+p+" / "+cid)
            output[p][cid]=doc
        else:
            add_rel(p,doc,cid)
        coverage.append({"nativeId":cid,"kind":kind,"status":resolved,
          "sourceRecords":len(first)+len(second),"path":"data/native/"+p,
          "candidates":[{"key":x["key"],"source":SOURCE_VERSION["SRD51" if x["key"].startswith("candidate-a") else "SRD52"]} for x in candidates]})
    # Some fluff records have no matched entity. Don't lose that content.
    for key,versioned in sorted(narrative.items(),key=lambda e:str(e[0])):
        if key in groups:continue
        display=key[-1] if key else "unnamed"
        path="lore/"+slug("-".join(str(x) for x in key))+".json"
        candidates=[]
        for source in ("SRD51","SRD52"):
            for ix,r in enumerate(versioned.get(source,[])):
                candidates.append({"key":VERSIONS[source]+(f"-{ix+1}" if len(versioned[source])>1 else ""),
                                   "content":native_payload(r["record"])})
        cid="lore."+". ".join(key)
        add_rel(path,{"id":cid,"name":display,"kind":"lore","baselineStatus":"pending-native-reconciliation",
                      "candidateDefinitions":candidates},cid)
        coverage.append({"nativeId":cid,"kind":"lore","status":"narrative-without-matched-record",
                         "sourceRecords":len(candidates),"path":"data/native/"+path})
    # Careful: preserve existing native accepted conditions and append
    # unresolved condition candidates under the same small catalog file.
    if "conditions.json" in output:
        condition_data["conditions"].extend(output.pop("conditions.json").values())
        condition_data["conditions"].sort(key=lambda x:x["id"])
        (NATIVE/"conditions.json").write_text(json.dumps(condition_data,indent=2,ensure_ascii=False)+"\n")
    compact_counts={}
    for name in sorted(set(COMPACT.values())):
        if name=="skills.json" or name=="conditions.json":continue
        if name not in output:continue
        values=sorted(output.pop(name).values(),key=lambda d:d["id"])
        plural="records"
        data={"_meta":{"status":"SRD-derived-native-baseline-pending-reconciliation",
                       "note":"Candidate definitions are imported content, not adopted executable rules."},
              plural:values}
        (NATIVE/name).write_text(json.dumps(data,indent=2,ensure_ascii=False)+"\n")
        compact_counts[name]=len(values)
    # Write individual entity documents only; reject existing native collisions.
    for rel,datum in sorted(output.items()):
        p=NATIVE/rel
        if p.exists():raise RuntimeError("Would overwrite existing authored native file: "+str(p))
        p.parent.mkdir(parents=True,exist_ok=True)
        p.write_text(json.dumps(datum,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")

    summary={
      "status":"native-coverage-staged; NOT fully reconciled or executable",
      "pinnedPackRevision":manifest["commit"],
      "sourceFilesVerified":len(manifest["files"]),
      "nativeDefinitionCount":len(coverage),
      "individualEntityFiles":len(output),
      "compactRecords":compact_counts,
      "recordCountsByInputKind":dict(sorted(input_summary.items())),
      "unresolvedMultipleSourceRecordKeys":dict(duplicates),
      "excludedSourceRecords":unsupported,
      "referenceOnlyFiles":metadata_rows,
      "warnings":warnings,
      "coverage":coverage,
      "reconciliationPolicy":"Existing approved native rules left untouched; unreviewed variants carried as inert candidateDefinitions; edition version names mapped outside native JSON; no source variant chosen as runtime rule.",
    }
    manifestPath=REFERENCE/"native-coverage-manifest.json"
    manifestPath.write_text(json.dumps(summary,indent=2,ensure_ascii=False)+"\n")
    if warnings:raise RuntimeError("Unmapped content remains: "+str(warnings[:12]))
    print("Native baseline staged:",len(coverage),"logical entities,",len(output),"individual files")
    print("Compact catalogs:",compact_counts)
    print("Unresolved multiple-occurrence identities:",dict(duplicates))
    print("Excluded rows lacking SRD source code:",len(unsupported))
    print("Source records covered by category:",dict(sorted(input_summary.items())))

if __name__=="__main__":
    build()
