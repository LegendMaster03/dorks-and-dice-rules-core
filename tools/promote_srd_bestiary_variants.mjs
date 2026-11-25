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
function generateLegacyVariants(candidates,parents,armorRows,pages){
 const clone=x=>JSON.parse(JSON.stringify(x)),slug=s=>s.toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const byId=new Map(parents.map(x=>[x.id,x])),armors=new Map(armorRows.map(x=>[x.id,x]));
 const report={edition:"srd-5.1",sourceGroups:0,sourceVersionOccurrences:0,nativeCount:0,variants:[],excluded:[]},records=[];
 // All seven named configurations from the pinned source are supported by
 // the general barding rule, rather than separate publisher monster blocks.
 const barding={"Chain Mail":"Chain Mail","Leather":"Leather Armor","Plate":"Plate Armor","Ring Mail":"Ring Mail","Scale Mail":"Scale Mail","Splint":"Splint Armor","Studded Leather":"Studded Leather Armor"};
 const locators={"Archmage":[395],"Djinni":[310],"Drider":[304],"Drow":[307],"Efreeti":[310],"Erinyes":[276],"Giant Lizard":[377],"Gray Ooze":[338],"Green Hag":[319],"Mage":[400],"Night Hag":[319],"Sea Hag":[320],"Troll":[351]};
 const pageIds=new Map(pages.map(x=>[x.page,x.id]));
 for(const [i,c] of candidates.entries()){
  if(!c._versions?.length)continue;
  report.sourceGroups++;report.sourceVersionOccurrences+=c._versions.length;
  for(const [j,v] of c._versions.entries()){
   const pointer="/monster/"+i+"/_versions/"+j;
   if(v.source!=="SRD51")throw Error("Foreign legacy variant");
   if(c.name!=="Giant Rat"&&c.name!=="Warhorse"){
    if(!locators[c.name])throw Error("Unreviewed legacy variant group "+c.name);
    report.excluded.push({name:v.name,parentName:c.name,pointer,reviewedPage:locators[c.name][0],reason:"Optional variant mechanics absent from the corresponding publisher stat-block section; do not promote commercial additions based on SRD tag."});continue;
   }
   const parent=byId.get("monster."+slug(c.name));
   if(!parent)throw Error("Missing legacy parent "+c.name);
   const {source,catalogReferences,...data}=clone(parent);
   const r={...data,id:"monster."+slug(v.name),name:v.name};
   r.variantOf=parent.id;
   const refs=new Set([...catalogReferences,parent.id]),provenancePages=[...source.pages];
   if(c.name==="Giant Rat"){
    if(v.name!=="Giant Rat (Diseased)"||!v.action?.length)throw Error("Unreviewed rat variant");
    r.action=clone(v.action);
   }else{
    const label=v.name.match(/^Warhorse \((.+) Barding\)$/)?.[1],armorName=barding[label];
    if(!armorName)throw Error("Unreviewed barding "+v.name);
    const armor=armors.get("baseitem."+slug(armorName));
    if(!armor||!armor.armor||typeof armor.ac!=="number")throw Error("Missing armor definition "+armorName);
    const armorType=armor.type.split("|")[0],dex=Math.floor((parent.dex-10)/2);
    const ac=armor.ac+(armorType==="LA"?dex:armorType==="MA"?Math.min(2,dex):0);
    if(ac!==v.ac[0].ac)throw Error("Barding AC mismatch "+v.name);
    r.ac=[{ac,from:[armor.name+" barding"]}];
    r.barding={baseArmorId:armor.id,costMultiplier:4,weightMultiplier:2};
    refs.add(armor.id);
    for(const p of [64,71,72]){if(!pageIds.has(p))throw Error("Missing armor/barding page "+p);refs.add(pageIds.get(p));provenancePages.push(p);}
   }
   r.catalogReferences=[...refs].sort();
   r.source={...source,pages:[...new Set(provenancePages)].sort((a,b)=>a-b),variantPointer:pointer,
    structuredStatus:c.name==="Giant Rat"?"Publisher p.378 explicitly supplies diseased rat variant; base CR retained.":"Derived equipment configuration from official armor and barding rules; not a separate published monster block.",
    adaptation:"Finite source-backed legacy variant derived from edition-local parent."};
   records.push(r);report.variants.push({id:r.id,parentId:parent.id,pointer,pages:r.source.pages});
  }
 }
 report.nativeCount=records.length;
 if(report.sourceVersionOccurrences!==records.length+report.excluded.length||records.length!==8)throw Error("Legacy variant coverage mismatch");
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

const legacyDir="data/rulesets/dnd-5e/native/";
const legacyParents=fs.readdirSync(path.join(root,legacyDir+"bestiary")).filter(f=>/^structured-\d+\.json$/.test(f)).sort().flatMap(f=>read(legacyDir+"bestiary/"+f));
const armorRows=fs.readdirSync(path.join(root,legacyDir+"items")).filter(f=>/^structured-\d+\.json$/.test(f)).sort().flatMap(f=>read(legacyDir+"items/"+f));
const legacy=generateLegacyVariants(read("data/reference/srd/structured/bestiary/bestiary-srd51.json").monster,legacyParents,armorRows,read(legacyDir+"rulebook/pages-053-077.json"));
for(const [file,value] of [[legacyDir+"bestiary/variants.json",legacy.records],["data/reference/srd/bestiary-variant-promotion-51.json",legacy.report]]){
 const target=path.join(root,file),body=JSON.stringify(value,null,2)+"\n";
 if(process.argv.includes("--check")){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale legacy bestiary variants "+file);}
 else fs.writeFileSync(target,body);
}
console.log("dnd-5e: "+legacy.report.nativeCount+" generated creature variants; "+legacy.report.excluded.length+" excluded source additions");
