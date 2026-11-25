// Preserve pinned class structures and resolve contextual feature UIDs within each edition.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
function generateClasses(inputs,manifest,src,pages,tag){
 const edition="SRD"+tag,slug=s=>s.toLowerCase().normalize("NFKC").replace(/[’']/g,"").replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const kinds={class:"class",subclass:"subclass",classFeature:"class-feature",subclassFeature:"subclass-feature"};
 const omit=new Set(["name","source","page","reprintedAs","basicRules2024","edition","hasFluff","hasFluffImages"]);
 const report={edition:manifest.officialPageImport.release,sourceCounts:{},nativeCounts:{},corrections:[],excluded:[],publisherNameLocatorMissing:[],unresolvedInlineReferences:[],resolvedProgressionReferences:0,referenceStatus:"Explicit feature progression references verified; other semantic references and full body fidelity pending."};
 const rows=[],groups={},normal=s=>s.toLowerCase().replace(/[’‘]/g,"'").replace(/[\u00ad\u2010-\u2014]/g,"-").replace(/\s+/g," ");
 const pageIds=new Set(pages.map(p=>p.id)),sourceKey=(kind,x)=>(kind==="classFeature"?[x.name,x.className,x.classSource,x.level,x.source]:[x.name,x.className,x.classSource,x.subclassShortName,x.subclassSource,x.level,x.source]).join("|").toLowerCase();
 const idFor=(kind,x)=>kind==="class"?"class."+slug(x.name):kind==="subclass"?"subclass."+slug(x.className)+"."+slug(x.shortName):kind==="classFeature"?"class-feature."+slug(x.className)+"."+slug(x.name)+"."+x.level:"subclass-feature."+slug(x.className)+"."+slug(x.subclassShortName)+"."+slug(x.name)+"."+x.level;
 const starts=Object.values(inputs).map(d=>d.class.find(x=>x.source===edition));
 if(starts.some(x=>!x))throw Error("Missing class");
 const startByName=new Map(starts.map((x,i)=>[x.name,[x.page,starts[i+1]?.page??(tag==="51"?55:82)]]));
 for(const [name,data] of Object.entries(inputs)){
  groups[name]=[];
  for(const [kind,list] of Object.entries(data)){
   if(!kinds[kind]){report.excluded.push({path:"class/class-"+name+".json",pointer:"/"+kind,reason:"source metadata, not a class or feature definition"});continue;}
   if(!Array.isArray(list))throw Error("Unexpected class array "+kind);
   for(const [index,c] of list.entries()){
    if(c.source!==edition)continue;
    report.sourceCounts[kind]=(report.sourceCounts[kind]??0)+1;
    if(c.classSource&&c.classSource!==edition||c.subclassSource&&c.subclassSource!==edition)throw Error("Foreign class identity");
    const id=idFor(kind,c),className=kind==="class"?c.name:c.className,[min,max]=startByName.get(className);
    // Candidate feature page numbers can refer to a commercial book.
    // Attach publisher page references only where a literal locator exists.
    const candidates=pages.filter(p=>p.page>=min&&p.page<=max&&normal(p.textLayout).includes(normal(c.name)));
    const selected=candidates.some(p=>p.page===c.page)?[c.page]:candidates.length?[candidates[0].page]:[];
    if(!selected.length)report.publisherNameLocatorMissing.push({id,candidatePage:c.page??null,reason:"No literal name locator in class chapter; may be a progression placeholder."});
    const record={id,kind:kinds[kind],name:c.name};
    for(const [key,val] of Object.entries(c))if(!omit.has(key))record[key]=JSON.parse(JSON.stringify(val));
    if(kind==="classFeature"&&tag==="52"&&c.className==="Warlock"&&c.name==="Pact Magic"&&c.level===1){
     const original=JSON.stringify(record.entries),corrected=original.replace("{@spell Witch Bolt|SRD52}","{@spell Charm Person|SRD52}");
     if(original===corrected)throw Error("Missing expected S52-019 discrepancy");
     record.entries=JSON.parse(corrected);report.corrections.push({id,reason:"S52-019: publisher Pact Magic example uses Charm Person, not Witch Bolt.",pages:[71]});
     if(!selected.includes(71))selected.push(71);
    }
    if(tag==="51"&&id==="subclass-feature.cleric.life.life-domain.1"){
     const names=["Chauntea","Arawai","Lathander","Pelor","Ilmater","Mishakal","Boldrei"];
     let count=0;
     record.entries[0]=record.entries[0].replace(/\{@deity ([^}|]+)(?:\|[^}]+)?\}/g,(whole,name)=>{
      if(!names.includes(name))return whole;count++;return name;
     });
     if(count!==7)throw Error("Expected seven publisher deity examples");
     report.corrections.push({id,reason:"Preserve seven deity examples printed on official SRD 5.1 p.17 as plain text; the SRD does not publish standalone entries for them.",pages:[17]});
    }
    if(tag==="51"&&id==="class-feature.warlock.pact-magic.1"){
     const original=JSON.stringify(record.entries),corrected=original.replace("{@spell witch bolt}","{@spell thunderwave}");
     if(original===corrected)throw Error("Missing legacy Pact Magic discrepancy");
     record.entries=JSON.parse(corrected);report.corrections.push({id,reason:"Official SRD 5.1 Pact Magic example uses thunderwave, not witch bolt.",pages:[47]});
     if(!selected.includes(47))selected.push(47);
    }
    if(tag==="51"&&id==="subclass.druid.land"){
     if(!record.additionalSpells.some(x=>x.name==="Underdark"))throw Error("Expected extra terrain");
     record.additionalSpells=record.additionalSpells.filter(x=>x.name!=="Underdark");
     const swamp=record.additionalSpells.find(x=>x.name==="Swamp");
     swamp.prepared["3"]=swamp.prepared["3"].map(x=>x==="Melf's acid arrow"?"acid arrow":x);
     report.corrections.push({id,reason:"Remove non-SRD Underdark terrain and correct Swamp spell name in structured grants.",pages:[21,22]});
     for(const p of [21,22])if(!selected.includes(p))selected.push(p);
    }
    if(tag==="51"&&id==="subclass-feature.druid.land.circle-spells.2"){
     record.entries=record.entries.filter(x=>x?.caption!=="Underdark");
     record.entries[0]=record.entries[0].replace("mountain, swamp, or Underdark","mountain, or swamp");
     const swamp=record.entries.find(x=>x?.caption==="Swamp");
     swamp.rows[0][1]="{@spell acid arrow}, {@spell darkness}";
     report.corrections.push({id,reason:"Remove Underdark terrain and reconcile Swamp spell table with official SRD 5.1.",pages:[21,22]});
     for(const p of [21,22])if(!selected.includes(p))selected.push(p);
    }
    record.catalogReferences=selected.map(p=>"rulebook.page-"+String(p).padStart(3,"0"));
    record.source={release:manifest.officialPageImport.release,url:pages[0].source.url,pdfSha256:manifest.officialPageImport.pdfSha256,license:manifest.license,attribution:manifest.attribution,pages:selected,candidatePage:c.page??null,structuredSource:{provider:src.provider,commit:src.commit,path:"class/class-"+name+".json",pointer:"/"+kind+"/"+index},structuredStatus:"pinned edition-specific class projection; publisher name locator where available; progression links checked; full body review pending"};
    groups[name].push(record);rows.push({record,kind,c});
    report.nativeCounts[kind]=(report.nativeCounts[kind]??0)+1;
   }
  }
 }
 const ids=new Set(rows.map(x=>x.record.id));
 if(ids.size!==rows.length)throw Error("Class identity collision");
 const featureIds=new Map(rows.filter(x=>x.kind.endsWith("Feature")).map(x=>[sourceKey(x.kind,x.c),x.record.id]));
 function resolve(kind,raw){
  if(typeof raw!=="string")throw Error("Malformed feature UID");
  const p=raw.split("|");p[2]||=edition;
  if(kind==="classFeature"){p[4]||=p[2];if(p[2]!==edition||p[4]!==edition)throw Error("Foreign class feature "+raw);}
  else{p[4]||=p[2];p[6]||=p[4];if(p[2]!==edition||p[4]!==edition||p[6]!==edition)throw Error("Foreign subclass feature "+raw);}
  const key=p.slice(0,kind==="classFeature"?5:7).join("|").toLowerCase(),id=featureIds.get(key);
  if(!id)throw Error("Unresolved explicit feature "+raw);
  report.resolvedProgressionReferences++;return id;
 }
 function walk(v,refs){
  if(Array.isArray(v))v.forEach(x=>walk(x,refs));
  else if(v&&typeof v==="object"){
   if(v.type==="refClassFeature")refs.add(resolve("classFeature",v.classFeature));
   if(v.type==="refSubclassFeature")refs.add(resolve("subclassFeature",v.subclassFeature));
   Object.values(v).forEach(x=>walk(x,refs));
  }
 }
 for(const {record:r,kind,c} of rows){
  const refs=new Set(r.catalogReferences);
  if(kind!=="class")refs.add("class."+slug(c.className));
  if(kind==="subclassFeature")refs.add("subclass."+slug(c.className)+"."+slug(c.subclassShortName));
  if(kind==="class")for(const v of r.classFeatures)refs.add(resolve("classFeature",typeof v==="string"?v:v.classFeature));
  if(kind==="subclass")for(const v of r.subclassFeatures)refs.add(resolve("subclassFeature",typeof v==="string"?v:v.subclassFeature));
  walk(r.entries,refs);
  const {source,catalogReferences,...body}=r,pending=new Set();
  for(const m of JSON.stringify(body).matchAll(/\{@(spell|creature|condition|skill|item|feat|action|sense|variantrule|language|class|race|optfeature|classFeature|subclassFeature) ([^}]+)\}/g))pending.add(m[1]+"|"+m[2]);
  for(const reference of pending)report.unresolvedInlineReferences.push({id:r.id,reference,reason:"cross-category or inline semantic reconciliation pending"});
  refs.delete(r.id);r.catalogReferences=[...refs].sort();
  for(const id of r.catalogReferences)if(!ids.has(id)&&!pageIds.has(id))throw Error("Dangling "+id);
 }
 report.nativeCount=rows.length;return {groups,report};
}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),"utf8"));
const json=x=>JSON.stringify(x,null,2)+"\n";
const src=read("data/reference/srd/structured-manifest.json");
const names=["barbarian","bard","cleric","druid","fighter","monk","paladin","ranger","rogue","sorcerer","warlock","wizard"];
const inputs=Object.fromEntries(names.map(n=>[n,read("data/reference/srd/structured/class/class-"+n+".json")]));
const check=process.argv.includes("--check");
for(const [pack,tag] of [["dnd-5e","51"],["dnd-5-5e","52"]]){
 const base="data/rulesets/"+pack+"/",manifest=read(base+"manifest.json");
 const files=tag==="51"?["003-027","028-052","053-077"]:["030-054","055-079","080-104","005-029"];
 const pages=files.flatMap(x=>read(base+"native/rulebook/pages-"+x+".json")).sort((a,b)=>a.page-b.page);
 const {groups,report}=generateClasses(inputs,manifest,src,pages,tag);
 const outputs=Object.entries(groups).map(([name,records])=>[base+"native/classes/"+name+".json",json(records)]);
 outputs.push(["data/reference/srd/class-promotion-"+tag+".json",json(report)]);
 for(const [file,body] of outputs){
  const target=path.join(root,file);
  if(check){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale class promotion "+file);}
  else {fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,body);}
 }
 for(const file of fs.readdirSync(path.join(root,base,"native/classes")).filter(f=>f.endsWith(".json")))if(!names.includes(file.slice(0,-5)))throw Error("Stale generated class file "+file);
 console.log(pack+": "+report.nativeCount+" class/subclass/feature records; "+report.resolvedProgressionReferences+" explicit progression links");
}
