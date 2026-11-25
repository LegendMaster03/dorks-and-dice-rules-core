// Materialize only publisher-backed spell-summon variants, without resolving dynamic spell levels.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
function generateVariants(candidates,parents){
 const clone=x=>JSON.parse(JSON.stringify(x)),slug=s=>s.toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const records=[],report={edition:"srd-5.2.1",sourceGroups:0,sourceVersionOccurrences:0,nativeCount:0,variants:[],corrections:[]};
 const fields=new Set(["size","type","ac","hp","speed","str","dex","con","int","wis","cha","save","skill","senses","passive","languages","alignment","cr","trait","action","bonus","reaction","legendary","spellcasting","immune","resist","vulnerable","conditionImmune","summonedBySpell","summonedBySpellLevel","pbNote"]);
 const byId=new Map(parents.map(x=>[x.id,x]));
 for(const [i,c] of candidates.entries()){
  if(!c._versions?.length)continue;
  const parent=byId.get("monster."+slug(c.name));
  if(!parent||c.source!=="SRD52")throw Error("Missing modern variant parent "+c.name);
  report.sourceGroups++;report.sourceVersionOccurrences+=c._versions.length;
  const variants=c._versions.flatMap((v,j)=>{
   const pointer="/monster/"+i+"/_versions/"+j;
   if(v._abstract){
    if(c.name!=="Draconic Spirit"||v._implementations?.length!==5)throw Error("Unsupported abstract variant "+c.name);
    return v._implementations.map((impl,k)=>{
     const value=impl._variables.resist;
     if(!parent.resist.includes(value.toLowerCase()))throw Error("Unknown resistance choice");
     // The shared-resistance choice is fixed, but the publisher does not explicitly
     // require the separate Breath Weapon damage choice to be identical.
     const resolved=clone(v._abstract);resolved.name=resolved.name.replace("{{resist}}",value);
     resolved._mod.trait[0].items.entries=resolved._mod.trait[0].items.entries.map(s=>s.replace("{{resist}}",value));
     delete resolved._mod.action;
     return {v:resolved,pointer:pointer+"/_implementations/"+k,selection:{sharedResistance:value.toLowerCase()}};
    });
   }
   return [{v,pointer}];
  });
  for(const {v,pointer,selection} of variants){
   if(v.source!=="SRD52")throw Error("Foreign variant");
   const r={id:"monster."+slug(v.name),kind:"monster",name:v.name};
   for(const [k,value] of Object.entries(parent))if(fields.has(k))r[k]=clone(value);
   for(const [k,value] of Object.entries(v))if(fields.has(k)){
    if(value===null)delete r[k];else r[k]=clone(value);
   }
   for(const [field,ops] of Object.entries(v._mod??{})){
    if(!fields.has(field)||!Array.isArray(r[field]))throw Error("Unsupported variant field "+field);
    for(const op of Array.isArray(ops)?ops:[ops]){
     if(op.mode==="removeArr"){
      const names=Array.isArray(op.names)?op.names:[op.names];
      for(const name of names)if(!r[field].some(x=>x.name===name))throw Error("Missing removal "+name);
      r[field]=r[field].filter(x=>!names.includes(x.name));
     }else if(op.mode==="renameArr"){
      const entry=r[field].find(x=>x.name===op.renames.rename);
      if(!entry)throw Error("Missing rename");entry.name=op.renames.with;
     }else if(op.mode==="replaceArr"){
      const index=r[field].findIndex(x=>x.name===op.replace);
      if(index<0)throw Error("Missing replacement");
      r[field].splice(index,1,...(Array.isArray(op.items)?clone(op.items):[clone(op.items)]));
     }else throw Error("Unreviewed variant modification "+op.mode);
    }
   }
   if(c.name==="Animated Object"){
    const label=v.name.slice(v.name.lastIndexOf("(")+1,-1);
    const sizes={"Huge":["H"],"Large":["L"],"Medium or Smaller":["T","S","M"]};
    r.size=sizes[label];if(!r.size)throw Error("Unknown animated object size");
    r.hp={special:v.hp.special};
    if(label==="Medium or Smaller"){
     const entries=r.action[0].entries;
     if(!entries[0].includes(" + your spellcasting ability modifier."))throw Error("Expected unsupported modifier");
     entries[0]=entries[0].replace(" + your spellcasting ability modifier.",".");
     report.corrections.push({id:r.id,reason:"Official p.109 does not add the caster ability modifier to Medium or smaller Slam damage."});
    }
   }
   if(c.name==="Draconic Spirit"){
    r.variantSelection=selection;
    report.corrections.push({id:r.id,reason:"Preserve all five creature resistances and the independent published Breath Weapon damage choice; specialize only the caster's shared resistance."});
   }
   r.variantOf=parent.id;
   const conditionTags=new Set([...JSON.stringify(r).matchAll(/\{@condition ([^}|]+)/gi)].map(m=>m[1].toLowerCase()));
   const refs=parent.catalogReferences.filter(id=>!id.startsWith("condition.")||r.conditionImmune?.includes(id.slice(10))||conditionTags.has(id.slice(10)));
   r.catalogReferences=[...new Set([...refs,parent.id])].sort();
   r.source={...clone(parent.source),variantPointer:pointer,structuredStatus:"Publisher-backed finite variant derived from the edition-local parent and pinned variant occurrence; spell-level formulas retained.",adaptation:"Generated finite spell-summon variant; no external media, renderer metadata or first-party mechanics."};
   if(new Set(records.map(x=>x.id)).has(r.id)||byId.has(r.id))throw Error("Variant collision "+r.id);
   records.push(r);report.variants.push({id:r.id,parentId:parent.id,pointer,page:parent.source.pages[0]});
  }
 }
 report.nativeCount=records.length;
 if(report.sourceGroups!==4||records.length!==14)throw Error("Unexpected modern variant coverage");
 return {records,report};
}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),"utf8"));
const dir="data/rulesets/dnd-5-5e/native/bestiary/";
const parents=fs.readdirSync(path.join(root,dir)).filter(f=>/^structured-\d+\.json$/.test(f)).sort().flatMap(f=>read(dir+f));
const {records,report}=generateVariants(read("data/reference/srd/structured/bestiary/bestiary-srd52.json").monster,parents);
for(const [file,value] of [[dir+"variants.json",records],["data/reference/srd/bestiary-variant-promotion-52.json",report]]){
 const target=path.join(root,file),body=JSON.stringify(value,null,2)+"\n";
 if(process.argv.includes("--check")){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale bestiary variants "+file);}
 else fs.writeFileSync(target,body);
}
console.log("dnd-5-5e: "+report.nativeCount+" generated spell-summon variants");
