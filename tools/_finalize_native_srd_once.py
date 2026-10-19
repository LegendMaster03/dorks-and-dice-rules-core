#!/usr/bin/env python3
"""One-time native baseline cleanup and coverage verification; remove after CI."""
import collections
import json
from pathlib import Path
import re

ROOT=Path(__file__).resolve().parents[1]
NATIVE=ROOT/"data/native"
REF=ROOT/"data/reference/srd"
COVER=REF/"native-coverage-manifest.json"

def load(p):
    return json.loads(p.read_text(encoding="utf-8"))
def put(p,d):
    p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
def slug(s):
    return re.sub(r"[^a-z0-9]+","-",str(s).lower()).strip("-")

manifest=load(COVER)
old_to_new={}
changed=0
all_ids=set()
# Remove publication licensing sections from native gameplay rules;
# they remain in both complete reference SRD documents and license metadata.
legal=NATIVE/"rules/legal-information.json"
assert legal.exists()
legal.unlink()
manifest["nativeDefinitionCount"]-=1
manifest["individualEntityFiles"]-=1
manifest["referenceOnlyBookSections"]=[{
  "name":"Legal Information",
  "location":"data/reference/srd/structured/book/book-srd51.json and book-srd52.json",
  "reason":"Legal and licensing metadata is not a native game mechanic"
}]
manifest["coverage"]=[v for v in manifest["coverage"] if v["nativeId"]!="rule.legal-information"]

for p in sorted(NATIVE.rglob("*.json")):
    if p.parent==NATIVE:continue
    d=load(p)
    if not isinstance(d,dict) or not d.get("baselineStatus"):
        continue
    rel=p.relative_to(NATIVE)
    segments=rel.parts
    stem=p.stem
    kind=d["kind"]
    if segments[:2]==("classes","subclasses"):
        new_id=f"subclass.{segments[2]}.{stem}"
    elif segments[:2]==("species","subspecies"):
        new_id=f"subspecies.{segments[2]}.{stem}"
    elif segments[:2]==("features","classes"):
        new_id=f"class-feature.{segments[2]}.{stem}"
    elif segments[:2]==("features","subclasses"):
        new_id=f"subclass-feature.{segments[2]}.{segments[3]}.{stem}"
    elif kind=="ruleChapter":
        new_id="rule."+stem
    else:
        new_id=slug(kind)+"."+slug(d["name"])
    assert re.fullmatch(r"[a-z0-9-]+(?:\.[a-z0-9-]+)+",new_id),new_id
    assert new_id not in all_ids,(str(p),new_id)
    all_ids.add(new_id)
    if d["id"]!=new_id:
        old_to_new[d["id"]]=new_id
        d["id"]=new_id
        changed+=1
    if kind=="subclassFeature":
        # The parent must be an explicit native subclass ID, not a bare short-name label.
        assert len(segments)>3
        d["parentSubclass"]="subclass."+segments[2]+"."+segments[3]
    put(p,d)
# Also check compact catalogs and previous approved data without rewriting them.
for p in sorted(NATIVE.glob("*.json")):
    data=load(p)
    for k,v in data.items():
        if isinstance(v,list):
            for record in v:
                if not isinstance(record,dict) or not record.get("id"):continue
                rid=record["id"]
                assert rid not in all_ids,("Global native ID conflict",str(p),rid)
                all_ids.add(rid)
for entry in manifest["coverage"]:
    old=entry["nativeId"]
    if old in old_to_new:entry["nativeId"]=old_to_new[old]
manifest["canonicalIdentifiersNormalized"]=changed
manifest["status"]="native-coverage-staged; all source-marked entities present; decisions pending"
put(COVER,manifest)

# Correct an older structural assertion that assumed the reconciled Conditions
# catalog could never grow beyond the first six approved entries.
testfile=ROOT/"tests/test_shared_conditions.py"
old=testfile.read_text()
before='''        self.assertEqual(selected, set(self.native))
        self.assertEqual(selected, set(self.decisions))'''
after='''        self.assertTrue(selected.issubset(self.native))
        self.assertEqual(selected, set(self.decisions))
        pending = {key for key, value in self.native.items()
                   if value.get("baselineStatus") == "pending-native-reconciliation"}
        self.assertEqual(9, len(pending))
        self.assertTrue(set(self.native).isdisjoint(set()) is True)'''
# Use substantive assertions, no decorative no-op.
after='''        self.assertTrue(selected.issubset(self.native))
        self.assertEqual(selected, set(self.decisions))
        pending = {key for key, value in self.native.items()
                   if value.get("baselineStatus") == "pending-native-reconciliation"}
        self.assertEqual(9, len(pending))
        self.assertTrue(pending.isdisjoint(selected))'''
assert before in old
old=old.replace(before,after)
old=old.replace('''for name in ("Blinded", "Charmed", "Incapacitated",
                     "Invisible", "Paralyzed", "Petrified", "Prone", "Stunned", "Unconscious"):
            self.assertNotIn("condition." + name.lower(), self.native)''',
'''for name in ("Blinded", "Charmed", "Incapacitated",
                     "Invisible", "Paralyzed", "Petrified", "Prone", "Stunned", "Unconscious"):
            pending_condition = self.native["condition." + name.lower()]
            self.assertEqual("pending-native-reconciliation",
                             pending_condition["baselineStatus"])
            self.assertEqual(2, len(pending_condition["candidateDefinitions"]))''')
assert "self.assertNotIn(\"condition.\" + name.lower(), self.native)" not in old
testfile.write_text(old)
print("Normalized IDs:",changed,"Native entries:",len(all_ids),
      "Coverage:",manifest["nativeDefinitionCount"],"Individual:",manifest["individualEntityFiles"])
