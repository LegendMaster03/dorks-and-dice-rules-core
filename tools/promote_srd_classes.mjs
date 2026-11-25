// Preserve pinned class structures and resolve contextual feature UIDs within each edition.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
import {linkSpellRelationships} from "./link_srd_spell_relationships.mjs";
import {characterRelationshipCatalog,linkCharacterRelationships} from "./link_srd_character_relationships.mjs";
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
    if(tag==="51"&&(id==="class.bard"||id==="subclass.bard.lore")){
     const evidence=pages.find(p=>p.page===13).textLayout;
     if(!evidence.includes("Choose two spells from any class, including this one.")||
        !evidence.includes("14th level and again at 18th level.")||
        !evidence.includes("At 6th level, you learn two spells of your choice from"))throw Error("Magical Secrets publisher evidence changed");
     const bard=inputs.bard.class.find(x=>x.source===edition);
     const slots=bard.classTableGroups.find(x=>x.rowsSpellProgression).rowsSpellProgression;
     const known=record.additionalSpells[0].known;
     const acquisition=id==="class.bard"?[10,14,18]:[6];
     if(JSON.stringify(Object.keys(known))!==JSON.stringify(acquisition.map(String)))throw Error("Magical Secrets acquisition levels changed");
     for(const level of acquisition){
      const max=slots[level-1].filter(n=>n>0).length;
      const expected={6:3,10:5,14:7,18:9}[level];
      const publisherRow=pages.find(p=>p.page===11).textLayout.split("\n").find(line=>line.trim().startsWith(level+"th "));
      // The printed table and the pinned slot progression agree on these maximum spell levels.
      if(max!==expected||!publisherRow)throw Error("Magical Secrets slot progression changed");
      const original=level===18?"":"level="+Array.from({length:max+1},(_,i)=>i).join(";");
      if(known[level].length!==2||known[level].some(x=>JSON.stringify(x)!==JSON.stringify({choose:original})))throw Error("Magical Secrets source selector changed");
      known[level]=known[level].map(()=>({choose:"level="+Array.from({length:max+1},(_,i)=>i).join(";")+"|class=Bard;Cleric;Druid;Paladin;Ranger;Sorcerer;Warlock;Wizard"}));
     }
     report.corrections.push({id,reason:"Official SRD 5.1 pp.11,13: Magical Secrets chooses two cantrips or spells from any class at each acquisition level, bounded by the Bard spell-slot progression. Use the eight published class lists; restore the missing explicit level-18 filter.",pages:[11,13]});
     for(const p of [11,13])if(!selected.includes(p))selected.push(p);
     selected.sort((a,b)=>a-b);
    }
    if(tag==="51"&&id.startsWith("class-feature.bard.magical-secrets.")&&!selected.includes(13))selected.push(13);
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
function readModernSpellLists(publisherPages,add){
 // Modern book JSON links to classes but omits the printed spell tables.
 // Match whole table cells, not prose mentions or suffixes.
 const ranges={Bard:[33,35],Cleric:[38,40],Druid:[44,46],Paladin:[55,56],Ranger:[60,61],Sorcerer:[67,69],Warlock:[74,76],Wizard:[79,82]};
 const school=/^(Abjuration|Conjuration|Divination|Enchantment|Evocation|Illusion|Necromancy|Transmutation)$/;
 const special=/^(?:[CMR](?:, [CMR])*|[—–-])$/;
 for(const [cls,[min,max]] of Object.entries(ranges)){
  for(const p of publisherPages.filter(p=>p.page>=min&&p.page<=max)){
   for(const line of p.textLayout.split("\n")){
    const cells=line.trim().split(/ {2,}/);
    for(let i=0;i+2<cells.length;i++){
     if(!school.test(cells[i+1])||!special.test(cells[i+2]))continue;
     add(cls,cells[i],p.page);
     i+=2;
    }
   }
  }
 }
}
function reconcileMapping(rows,mapping,byName,normal,edition,tag){
 // Source mappings are candidates, not authority for feature access or commercial classes.
 const excluded=[],foreign=[];
 const expected=tag==="51"?["Monk|Astral Projection"]:["Bard|Phantasmal Force","Sorcerer|Mind Spike","Sorcerer|Phantasmal Force","Wizard|Phantasmal Force"];
 for(const [name,access] of Object.entries(mapping)){
  if(!byName.has(normal(name))||!Array.isArray(access.class))throw Error("Invalid spell source mapping "+name);
  for(const c of access.class){
   if(c.source!==edition){
    if(tag!=="52"||c.name!=="Artificer"||c.source!=="EFA")throw Error("Unexpected foreign class mapping "+name);
    foreign.push({className:c.name,spell:name,source:c.source,reason:"Class not published in SRD 5.2.1"});
    continue;
   }
   if(!rows[c.name]?.has(name))excluded.push(c.name+"|"+name);
  }
 }
 if(JSON.stringify(excluded.sort())!==JSON.stringify(expected.sort())||foreign.length!==(tag==="51"?0:73))throw Error("Unexpected structured/publisher mapping discrepancy "+JSON.stringify(excluded));
 for(const [cls,entries] of Object.entries(rows))for(const name of entries.keys())if(!mapping[name]?.class.some(c=>c.name===cls&&c.source===edition))throw Error("Publisher-only membership "+cls+" "+name);
 return {excluded,foreign};
}
function reconcileSpellLists(groups,report,tag,read,publisherPages,src){
 const names=["Bard","Cleric","Druid","Paladin","Ranger","Sorcerer","Warlock","Wizard"];
 const root="data/reference/srd/structured/",edition="SRD"+tag;
 const mapping=read(root+"spells/sources.json")[edition];
 const spells=read(root+"spells/spells-srd"+tag+".json").spell;
 const normal=s=>s.replace(/[’‘]/g,"'").toLowerCase().trim();
 const slug=name=>name.normalize("NFKC").replace(/[’‘]/g,"'").toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const byName=new Map(spells.map(s=>[normal(s.name),s]));
 if(byName.size!==spells.length||spells.some(s=>s.source!==edition)||Object.keys(mapping).length!==spells.length)throw Error("Spell inventory mismatch "+edition);
 const rows=Object.fromEntries(names.map(n=>[n,new Map()]));
 const add=(cls,name,page,level)=>{const spell=byName.get(normal(name));if(!spell||!rows[cls]||level!==undefined&&spell.level!==level||rows[cls].has(spell.name))throw Error("Invalid/duplicate publisher class spell "+cls+" "+name+" p."+page);rows[cls].set(spell.name,page);};
 if(tag==="51"){
 // Read the pinned publisher's two columns in order, carrying class and level across page boundaries.
 let cls,level;
 for(const p of publisherPages.filter(p=>p.page>=105&&p.page<=113)){
  const cols=[[],[]];
  for(const line of p.textLayout.split("\n")){
   if(!line.trim()||line.includes("System Reference Document"))continue;
   const parts=line.trim().split(/ {2,}/);
   if(parts.length===2){cols[0].push(parts[0]);cols[1].push(parts[1]);}
   else if(parts.length===1)cols[line.startsWith(" ")?1:0].push(parts[0]);
   else throw Error("Unexpected legacy spell-list layout p."+p.page);
  }
  for(const line of cols.flat()){
   if(line==="Spell Lists")continue;
   const c=line.match(/^(\w+) Spells$/),l=line.match(/^(\d)(?:st|nd|rd|th) Level$/);
   if(c){cls=c[1];continue;}
   if(l||line==="Cantrips (0 Level)"){level=l?Number(l[1]):0;continue;}
   add(cls,line,p.page,level);
  }
 }
 // Independently reconcile the structured book lists; only exact publisher aliases are allowed.
 const aliases={"Tasha's Hideous Laughter":"Hideous Laughter","Otto's Irresistible Dance":"Irresistible Dance","Mordenkainen's Magnificent Mansion":"Magnificent Mansion","Mordenkainen's Sword":"Arcane Sword","Tenser's Floating Disk":"Floating Disk","Nystul's Magic Aura":"Arcanist's Magic Aura","Mordenkainen's Faithful Hound":"Faithful Hound","Mordenkainen's Private Sanctum":"Private Sanctum","Otiluke's Resilient Sphere":"Resilient Sphere","Rary's Telepathic Bond":"Telepathic Bond","Otiluke's Freezing Sphere":"Freezing Sphere"};
 const book=read(root+"book/book-srd51.json").data.find(x=>x.name==="Spell Lists");
 for(const c of book.entries){
  const cls=c.name.split(" ")[0],seen=new Set();
  for(const l of c.entries){
   const level=l.name.startsWith("Cantrips")?0:Number(l.name[0]);
   for(const raw of l.items){
    const m=raw.match(/^\{@spell ([^|}]+)\|srd51\}$/i);
    if(!m)throw Error("Unexpected legacy book spell "+raw);
    const s=byName.get(normal(aliases[m[1]]??m[1]));
    if(!s||s.level!==level||!rows[cls].has(s.name)||seen.has(s.name))throw Error("Legacy book mismatch "+cls+" "+raw);
    seen.add(s.name);
   }
  }
  if(seen.size!==rows[cls].size)throw Error("Incomplete legacy book list "+cls);
 }
 }else readModernSpellLists(publisherPages,add);
 const {excluded,foreign}=reconcileMapping(rows,mapping,byName,normal,edition,tag);
 const counts={};
 for(const cls of names){
  const r=groups[cls.toLowerCase()].find(x=>x.id==="class."+cls.toLowerCase());
  if(!r||!r.spellcastingAbility)throw Error("Missing spellcasting class "+cls);
  const entries=rows[cls],levels=new Map();
  for(const s of spells.filter(s=>entries.has(s.name))){
   if(!levels.has(s.level))levels.set(s.level,[]);
   levels.get(s.level).push("spell."+slug(s.name));
  }
  r.spellList=[...levels].sort(([a],[b])=>a-b).map(([level,spellIds])=>({level,spellIds:spellIds.sort()}));
  const pages=[...new Set(entries.values())].sort((a,b)=>a-b);
  const ids=r.spellList.flatMap(x=>x.spellIds);
  if(new Set(ids).size!==entries.size)throw Error("Spell identity collision "+cls);
  r.catalogReferences=[...new Set([...r.catalogReferences,...ids,...pages.map(p=>"rulebook.page-"+String(p).padStart(3,"0"))])].sort();
  r.source.spellList={pages,provider:src.provider,commit:src.commit,path:"spells/sources.json",pointer:"/"+edition,verification:"publisher class-list membership verified; not a feature grant or executable spellcasting policy"};
  counts[cls]=ids.length;
 }
 report.classSpellLists={counts,total:Object.values(counts).reduce((a,b)=>a+b,0),excludedSourceMemberships:excluded.map(x=>{const [className,spell]=x.split("|");return {className,spell,reason:tag==="51"?"Feature-specific access, not a published base class list":"Absent from the published class spell table"};}),status:"Publisher class-list membership reconciled; feature grants, preparation and spell execution are separate."};
 if(foreign.length)report.classSpellLists.excludedForeignMemberships=foreign;
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
 const spellPages=tag==="51"?read(base+"native/rulebook/pages-103-127.json"):pages;
 reconcileSpellLists(groups,report,tag,read,spellPages,src);
 if(tag==="52"){
  // Resolve the finite Metamagic choice against the edition-local option catalog,
  // including separately recovered publisher-backed options.
  const option=groups.sorcerer.find(r=>r.id==="class-feature.sorcerer.metamagic-options.2");
  const available=new Set(read(base+"native/feats-and-optional-features.json").filter(r=>r.featureType?.includes("MM")).map(r=>r.id));
  const ids=option.entries[1].entries.map(x=>{
   const [name,source]=x.optionalfeature.split("|"),id="optional-feature."+name.toLowerCase().replace(/ /g,"-");
   if(source!=="SRD52"||!available.has(id))throw Error("Unresolved modern Metamagic option "+x.optionalfeature);
   return id;
  });
  if(new Set(ids).size!==10)throw Error("Incomplete modern Metamagic options");
  option.catalogReferences=[...new Set([...option.catalogReferences,...ids])].sort();
 }
 const spells=fs.readdirSync(path.join(root,base+"native/spells")).filter(f=>f.endsWith(".json")).sort().flatMap(f=>read(base+"native/spells/"+f));
 report.literalSpellRelationships=linkSpellRelationships(Object.values(groups).flat(),spells,"SRD"+tag);
 report.literalCharacterRelationships=linkCharacterRelationships(Object.values(groups).flat(),characterRelationshipCatalog(root,base,read),"SRD"+tag);
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
