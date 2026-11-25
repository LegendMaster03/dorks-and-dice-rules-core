// Promote edition-local character creation definitions; no first-party inheritance policy.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
import {linkSpellRelationships} from "./link_srd_spell_relationships.mjs";

function generate(races,backgrounds,manifest,sourceManifest,pages,skills,tag) {
 const slug=s=>s.toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,""), edition="SRD"+tag, modern=tag==="52";
 const records=[],report={edition:manifest.officialPageImport.release,sourceCounts:{},nativeCounts:{},excluded:[],corrections:[],mergedOccurrences:[],pendingReferences:[]};
 const byPage=new Map(pages.map(p=>[p.page,p]));
 const oldPages={Dragonborn:[5,6],Dwarf:[3],Elf:[4],Gnome:[6],"Half-Elf":[6,7],"Half-Orc":[7],Halfling:[4,5],Human:[5],Tiefling:[7]};
 const newPages={Dragonborn:[84],Dwarf:[84],Elf:[84,85],Gnome:[85],Goliath:[85,86],Halfling:[86],Human:[86],Orc:[86],Tiefling:[86,87]};
 const alignment={
 Dragonborn:"Dragonborn tend to extremes, making a conscious choice for one side or the other in the cosmic war between good and evil. Most dragonborn are good, but those who side with evil can be terrible villains.",
 Dwarf:"Most dwarves are lawful, believing firmly in the benefits of a well-ordered society. They tend toward good as well, with a strong sense of fair play and a belief that everyone deserves to share in the benefits of a just order.",
 Elf:"Elves love freedom, variety, and self-expression, so they lean strongly toward the gentler aspects of chaos. They value and protect others’ freedom as well as their own, and they are more often good than not.",
 Gnome:"Gnomes are most often good. Those who tend toward law are sages, engineers, researchers, scholars, investigators, or inventors. Those who tend toward chaos are minstrels, tricksters, wanderers, or fanciful jewelers. Gnomes are good-hearted, and even the tricksters among them are more playful than vicious.",
 "Half-Elf":"Half-elves share the chaotic bent of their elven heritage. They value both personal freedom and creative expression, demonstrating neither love of leaders nor desire for followers. They chafe at rules, resent others’ demands, and sometimes prove unreliable, or at least unpredictable.",
 "Half-Orc":"Half-orcs inherit a tendency toward chaos from their orc parents and are not strongly inclined toward good. Half-orcs raised among orcs and willing to live out their lives among them are usually evil.",
 Halfling:"Most halflings are lawful good. As a rule, they are good-hearted and kind, hate to see others in pain, and have no tolerance for oppression. They are also very orderly and traditional, leaning heavily on the support of their community and the comfort of their old ways.",
 Human:"Humans tend toward no particular alignment. The best and the worst are found among them.",
 Tiefling:"Tieflings might not have an innate tendency toward evil, but many of them end up there. Evil or not, an independent nature inclines many tieflings toward a chaotic alignment."
 };
 const omit=new Set(["name","source","page","reprintedAs","basicRules2024","edition","soundClip","hasFluff","hasFluffImages","traitTags","heightAndWeight","age"]);
 const ptr=(file,array,index)=>({path:file,pointer:`/${array}/${index}`});
 const sourcePtr=(file,array,index)=>({provider:sourceManifest.provider,commit:sourceManifest.commit,...ptr(file,array,index)});
 function payload(candidate,file,array,index){
  const result={};
  for(const [key,value] of Object.entries(candidate)){
   if(!omit.has(key))result[key]=JSON.parse(JSON.stringify(value));
   else if(["heightAndWeight","age","traitTags","soundClip","hasFluff","hasFluffImages"].includes(key))report.excluded.push({path:file,pointer:`/${array}/${index}/${key}`,reason:"unverified metadata or external media; published descriptive entries retained"});
  }return result;
 }
 function record(candidate,file,array,index,kind,id,name,locators){
  if(!locators.every(n=>byPage.has(n)))throw Error("Unknown publisher page "+id);
  if(!locators.some(n=>byPage.get(n).textLayout.toLowerCase().includes(name.toLowerCase())))throw Error("Missing publisher name locator "+id);
  const row={id,kind,name,...payload(candidate,file,array,index),catalogReferences:locators.map(n=>byPage.get(n).id),source:{
   release:manifest.officialPageImport.release,url:byPage.get(locators[0]).source.url,pdfSha256:manifest.officialPageImport.pdfSha256,license:manifest.license,attribution:manifest.attribution,pages:locators,candidatePage:candidate.page??null,structuredSource:sourcePtr(file,array,index),structuredStatus:"publisher name/page locator and pinned structured projection; full descriptive and mechanical field review pending"}};
  records.push(row);report.nativeCounts[kind]=(report.nativeCounts[kind]??0)+1;return row;
 }
 for(const [i,c] of races.race.entries()){
  if(c.source!==edition)continue;
  report.sourceCounts.race=(report.sourceCounts.race??0)+1;
  const kind=modern?"species":"race",row=record(c,"races.json","race",i,kind,kind+"."+slug(c.name),c.name,(modern?newPages:oldPages)[c.name]);
  if(!modern){
   const age=row.entries.findIndex(e=>e.name==="Age");
   if(age<0||!alignment[c.name])throw Error("Missing guidance "+c.name);
   row.entries.splice(age+1,0,{type:"entries",name:"Alignment",entries:[alignment[c.name]]});
   report.corrections.push({id:row.id,reason:"Restore official nonbinding alignment guidance from cited publisher pages."});
  }
 }
 const names={"Dwarf|Hill":"Hill Dwarf","Elf|High":"High Elf","Gnome|Rock":"Rock Gnome","Halfling|Lightfoot":"Lightfoot"};
 const locs={"Dwarf|Hill":[4],"Elf|High":[4],"Gnome|Rock":[6],"Halfling|Lightfoot":[5]};
 for(const [i,c] of races.subrace.entries()){
  if(c.source!==edition)continue;
  report.sourceCounts.subrace=(report.sourceCounts.subrace??0)+1;
  const parent=records.find(r=>r.id==="race."+slug(c.raceName));
  if(!parent||c.raceSource!==edition)throw Error("Missing or foreign parent");
  if(!c.name){
   const extra=payload(c,"races.json","subrace",i);
   delete extra.raceName;delete extra.raceSource;
   for(const [k,v] of Object.entries(extra)){if(k in parent)throw Error("Base extension conflict "+parent.id+" "+k);parent[k]=v;}
   (parent.source.additionalStructuredSources??=[]).push(sourcePtr("races.json","subrace",i));
   report.mergedOccurrences.push({id:parent.id,...ptr("races.json","subrace",i),fields:Object.keys(extra),reason:Object.keys(extra).length?"unnamed base-race extension":"identity-only renderer placeholder"});
  }else{
   const key=c.raceName+"|"+c.name,row=record(c,"races.json","subrace",i,"subrace","subrace."+slug(c.raceName)+"."+slug(c.name),names[key],locs[key]);
   row.subraceName=c.name;row.catalogReferences.push(parent.id);
  }
 }
 for(const [i,c] of backgrounds.background.entries()){
  if(c.source!==edition)continue;report.sourceCounts.background=(report.sourceCounts.background??0)+1;
  record(c,"backgrounds.json","background",i,"background","background."+slug(c.name),c.name,modern?[83]:[60,61]);
 }
 const skillIds=new Set(skills.map(s=>s.id)), ids=new Set(records.map(r=>r.id)), pageIds=new Set(pages.map(p=>p.id));
 if(ids.size!==records.length)throw Error("Duplicate origin identity");
 for(const row of records){
  const {source,catalogReferences,...body}=row,refs=new Set(catalogReferences),pending=new Set();
  for(const m of JSON.stringify(body).matchAll(/\{@(spell|creature|condition|skill|item|feat|action|sense|variantrule|language|class|race|optfeature) ([^}]+)\}/g)){
   const [name,src]=m[2].split("|"),id="skill."+slug(name);
   if(m[1]==="skill"&&(!src||src.toUpperCase()===edition)&&skillIds.has(id))refs.add(id);
   else pending.add(m[1]+"|"+m[2]);
  }
  row.catalogReferences=[...refs].sort();
  for(const reference of pending)report.pendingReferences.push({id:row.id,reference,reason:"semantic cross-category resolution pending; markup preserved"});
  for(const id of row.catalogReferences)if(!ids.has(id)&&!skillIds.has(id)&&!pageIds.has(id))throw Error("Dangling "+id);
 }
 report.nativeCount=records.length;
 report.limitations=["Full body and table fidelity pending; publisher page locators are not certification.","Nested _versions are preserved as source choice templates, not independently materialized records.","Unnamed legacy subrace extensions are folded into their parent with separate source pointers.","Semantic cross-category reference reconciliation and general character-creation rules pending."];
 return {records,report};
}

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),"utf8"));
const json=x=>JSON.stringify(x,null,2)+"\n";
const src=read("data/reference/srd/structured-manifest.json");
const races=read("data/reference/srd/structured/races.json");
const backgrounds=read("data/reference/srd/structured/backgrounds.json");
const check=process.argv.includes("--check");
for(const [pack,tag] of [["dnd-5e","51"],["dnd-5-5e","52"]]){
 const base="data/rulesets/"+pack+"/";
 const manifest=read(base+"manifest.json");
 const files=tag==="51"?["003-027","053-077"]:["080-104"];
 const pages=files.flatMap(x=>read(base+"native/rulebook/pages-"+x+".json"));
 const {records,report}=generate(races,backgrounds,manifest,src,pages,read(base+"native/skills.json"),tag);
 const spells=fs.readdirSync(path.join(root,base+"native/spells")).filter(f=>f.endsWith(".json")).sort().flatMap(f=>read(base+"native/spells/"+f));
 report.literalSpellRelationships=linkSpellRelationships(records,spells,"SRD"+tag);
 for(const [file,body] of [[base+"native/character-origins.json",json(records)],["data/reference/srd/origins-promotion-"+tag+".json",json(report)]]){
  const target=path.join(root,file);
  if(check){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale origins promotion "+file);}
  else fs.writeFileSync(target,body);
 }
 console.log(pack+": "+records.length+" origin records");
}
