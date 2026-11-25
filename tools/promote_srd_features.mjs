import { srdImportFiles } from "./srd_import_sources.mjs";
// Promote edition-specific feats and optional features, excluding legacy duplicates.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
import {linkSpellRelationships} from "./link_srd_spell_relationships.mjs";
import {characterRelationshipCatalog,linkCharacterRelationships} from "./link_srd_character_relationships.mjs";
function expandedMetamagic(input,manifest,pages){
 const page=pages.find(p=>p.page===67);
 // Page 67 has a fixed gutter at column 60. Read each column separately,
 // joining wrapped words before comparing both complete published bodies.
 const lines=page.textLayout.split("\n");
 const left=lines.filter(l=>/^ {1,10}\S/.test(l)).map(l=>l.slice(0,60).trim()).filter(Boolean);
 const right=lines.map(l=>l.slice(60).trim()).filter(Boolean);
 const norm=s=>s.replace(/[’‘]/g,"'").replace(/\u00ad/g,"").replace(/-\s+/g,"").replace(/\s+/g," ").trim();
 const seeking=left.slice(left.indexOf("Seeking Spell")+2).join(" ");
 const transmuted=right.slice(right.indexOf("Transmuted Spell")+2,right.indexOf("Twinned Spell")).join(" ");
 const sections={"Seeking Spell":seeking,"Transmuted Spell":transmuted};
 if(left[left.indexOf("Seeking Spell")+1]!=="Cost: 1 Sorcery Point"||right[right.indexOf("Transmuted Spell")+1]!=="Cost: 1 Sorcery Point")throw Error("Publisher Metamagic cost mismatch");
 if(input.provider!=="5etools-mirror-3/5etools-src"||input.commit!=="2512db75eb48ef06f6190b086a82430887906dca")throw Error("Unexpected expanded metamagic source");
 if(input.records.length!==2)throw Error("Unexpected expanded metamagic inventory");
 return input.records.map(({path,pointer,record:c})=>{
  if(c.source!=="XPHB"||c.srd52!==true||c.featureType.join()!=="MM"||!Object.hasOwn(sections,c.name)||c.consumes.name!=="Sorcery Point"||(c.consumes.amount??1)!==1)throw Error("Unexpected metamagic source "+c.name);
  const display=c.entries.join(" ").replace(/\{@dice ([^}]+)\}/g,"$1");
  if(norm(sections[c.name])!==norm(display))throw Error("Publisher metamagic body mismatch "+c.name+": "+norm(sections[c.name]));
  return {id:"optional-feature."+c.name.toLowerCase().replace(/ /g,"-"),kind:"optional-feature",name:c.name,featureType:c.featureType,consumes:{name:c.consumes.name,amount:1},entries:c.entries,catalogReferences:["class.sorcerer",page.id],source:{release:manifest.officialPageImport.release,url:page.source.url,pdfSha256:manifest.officialPageImport.pdfSha256,license:manifest.license,attribution:manifest.attribution,pages:[67],structuredStatus:"Expanded upstream SRD candidate; exact published option body and cost verified.",expandedStructuredSource:{provider:input.provider,commit:input.commit,path,pointer}}};
 });
}

function generateFeatures(feats,optional,manifest,src,pages,tag){
 const edition="SRD"+tag,modern=tag==="52",slug=s=>s.toLowerCase().normalize("NFKC").replace(/[’']/g,"").replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const report={edition:manifest.officialPageImport.release,sourceCounts:{},nativeCounts:{},excluded:[],corrections:[],publisherNameLocatorMissing:[],pendingReferences:[],resolvedStructuredPrerequisites:0};
 const omit=new Set(["name","source","page","reprintedAs","basicRules2024","hasFluff","hasFluffImages","repeatableHidden"]);
 const rows=[],normal=s=>s.toLowerCase().replace(/[’‘]/g,"'").replace(/[\u00ad\u2010-\u2014]/g,"-").replace(/\s+/g," ");
 const classByType={EI:"warlock",PB:"warlock",MM:"sorcerer","FS:F":"fighter","FS:P":"paladin","FS:R":"ranger"};
 const classIds=new Set(["barbarian","bard","cleric","druid","fighter","monk","paladin","ranger","rogue","sorcerer","warlock","wizard"].map(s=>"class."+s));
 const pageIds=new Set(pages.map(p=>p.id));
 for(const [kind,list,file,array] of [["feat",feats.feat,"feats.json","feat"],["optional-feature",optional.optionalfeature,"optionalfeatures.json","optionalfeature"]]){
  for(const [index,c] of list.entries()){
   if(c.source!==edition)continue;report.sourceCounts[kind]=(report.sourceCounts[kind]??0)+1;
   if(modern&&kind==="feat"&&c.reprintedAs&&!c.category){
    report.excluded.push({path:file,pointer:"/"+array+"/"+index,name:c.name,reason:"legacy mechanics duplicate tagged SRD52; select the modern categorized feat"});continue;
   }
   const r={id:kind+"."+slug(c.name),kind,name:c.name};
   for(const [k,v] of Object.entries(c))if(!omit.has(k))r[k]=JSON.parse(JSON.stringify(v));
   if(!modern&&r.featureType?.includes("FS:B")){
    r.featureType=r.featureType.filter(x=>x!=="FS:B");
    report.corrections.push({id:r.id,reason:"Exclude Bard fighting-style association outside SRD 5.1 College of Lore."});
   }
   if(!modern&&c.name==="Twinned Spell"){
    const original=JSON.stringify(r.entries),corrected=original.replace("{@spell ray of frost} and {@spell chromatic orb} are","{@spell ray of frost} is");
    if(original===corrected)throw Error("Missing legacy Twinned Spell discrepancy");
    r.entries=JSON.parse(corrected);
    report.corrections.push({id:r.id,pages:[44],reason:"Official SRD 5.1 example uses ray of frost only; chromatic orb is not included."});
   }
   if(modern&&kind==="optional-feature"&&["Agonizing Blast","Eldritch Spear","Repelling Blast"].includes(c.name)){
    const [page,attack,minimumRangeFeet]={"Agonizing Blast":[72,false,null],"Eldritch Spear":[73,false,10],"Repelling Blast":[74,true,null]}[c.name];
    const evidence=pages.find(p=>p.page===page).textLayout;
    if(!evidence.includes(c.name)||!evidence.includes("Prerequisite: Level 2+ Warlock, a Warlock Cantrip")||
       !evidence.includes(attack?"That Deals Damage via an Attack Roll":"That Deals Damage")||
       !evidence.includes("Choose one of your known Warlock cantrips")||
       !evidence.includes("Repeatable. You can gain this invocation more")||
       minimumRangeFeet!==null&&!evidence.includes("has a range of 10+ feet."))throw Error("Warlock invocation publisher evidence changed: "+c.name);
    const raw=r.prerequisite[0].spell[0],expected="level=0|class=Warlock"+(attack?"|spell attack=m;r;o":"");
    if(raw.choose!==expected||raw.entry!=="a Warlock Cantrip That Deals Damage"+(attack?" via an Attack Roll":""))throw Error("Warlock invocation source filter changed: "+c.name);
    raw.choose="level=0|class=Warlock";
    raw.predicate={dealsDamage:true,requiresAttackRoll:attack};
    raw.selection={known:true,differentOnRepeat:true,minimumRangeFeet};
    // The upstream display filter cannot express all of the publisher's
    // predicates. Retain the printed prose, not the misleading link filter.
    const original=r.entries[0];
    r.entries[0]=original.replace(/\{@filter Warlock cantrips\|spells\|[^{}]+\}/,"Warlock cantrips");
    if(original===r.entries[0])throw Error("Warlock invocation display filter changed");
    report.corrections.push({id:r.id,pages:[page],reason:"Normalize the published damaging Warlock cantrip prerequisite and distinct known-cantrip selection requirements, including weapon attack rolls where applicable; preserve repeatability and minimum range separately from the prerequisite."});
   }
   if(modern&&c.name==="Pact of the Chain"){
    const original=JSON.stringify(r.entries),corrected=original.replace("{@creature Slaad Tadpole|SRD52}, ","").replace("see appendix B for the familiar's stat block","see “Monsters” for the familiar's stat block");
    if(corrected===original||corrected.includes("Slaad Tadpole")||corrected.includes("appendix B"))throw Error("S52-017 correction failed");
    r.entries=JSON.parse(corrected);
    const grant=r.additionalSpells[0];
    if(!grant.innate._.ritual.includes("find familiar|srd52"))throw Error("Unexpected Pact Chain spell schema");
    grant.innate._=["find familiar|srd52"];
    report.corrections.push({id:r.id,pages:[74],reason:"S52-017: remove non-SRD Slaad Tadpole, correct Monsters cross-reference, and remove ritual-only encoding; publisher grants spell as a Magic action without a spell slot. Timing retained in entries."});
   }
   let candidates=pages.filter(p=>{
    if(kind==="feat")return modern?p.page>=87&&p.page<=88:p.page===75;
    if(c.featureType.includes("EI")||c.featureType.includes("PB"))return modern?p.page>=70&&p.page<=76:p.page>=46&&p.page<=51;
    if(c.featureType.includes("MM"))return modern?p.page>=64&&p.page<=69:p.page>=42&&p.page<=45;
    return !modern&&p.page>=24&&p.page<=37;
   }).filter(p=>normal(p.textLayout).includes(normal(c.name)));
   const locators=candidates.some(p=>p.page===c.page)?[c.page]:candidates.length?[candidates[0].page]:[];
   if(!locators.length)report.publisherNameLocatorMissing.push({id:r.id,candidatePage:c.page??null});
   if(modern&&c.name==="Pact of the Chain"&&!locators.includes(74))locators.push(74);
   r.catalogReferences=locators.map(p=>"rulebook.page-"+String(p).padStart(3,"0"));
   r.source={release:manifest.officialPageImport.release,url:pages[0].source.url,pdfSha256:manifest.officialPageImport.pdfSha256,license:manifest.license,attribution:manifest.attribution,pages:locators,candidatePage:c.page??null,structuredSource:{provider:src.provider,commit:src.commit,path:file,pointer:"/"+array+"/"+index},structuredStatus:"pinned edition-specific structured feat or optional feature; publisher name locator where available; full body and semantic review pending"};
   rows.push(r);report.nativeCounts[kind]=(report.nativeCounts[kind]??0)+1;
  }
 }
 const ids=new Set(rows.map(x=>x.id));if(ids.size!==rows.length)throw Error("Duplicate feature identity");
 for(const r of rows){
  const refs=new Set(r.catalogReferences);
  for(const type of r.featureType??[]){const id="class."+classByType[type];if(!classIds.has(id))throw Error("Unexpected optional feature type "+type);refs.add(id);}
  function linkOptional(raw){
   const [name,source]=raw.split("|"),id="optional-feature."+slug(name);
   if(source&&source.toUpperCase()!==edition||!ids.has(id))throw Error("Unresolved optional feature prerequisite "+raw);
   refs.add(id);report.resolvedStructuredPrerequisites++;
  }
  for(const p of r.prerequisite??[]){
   if(p.level?.class){
    const c=p.level.class,id="class."+slug(c.name);
    if(c.source&&c.source!==edition||!classIds.has(id))throw Error("Unresolved class prerequisite");refs.add(id);report.resolvedStructuredPrerequisites++;
   }
   for(const raw of p.optionalfeature??[])linkOptional(raw);
   if(p.pact)linkOptional("Pact of the "+p.pact);
  }
  const {source,catalogReferences,...body}=r,pending=new Set();
  for(const m of JSON.stringify(body).matchAll(/\{@(spell|creature|condition|skill|item|feat|action|sense|variantrule|language|class|race|optfeature|classFeature|subclassFeature) ([^}]+)\}/g))pending.add(m[1]+"|"+m[2]);
  for(const reference of pending)report.pendingReferences.push({id:r.id,reference,reason:"cross-category or inline semantic reconciliation pending"});
  refs.delete(r.id);r.catalogReferences=[...refs].sort();
  for(const id of r.catalogReferences)if(!ids.has(id)&&!classIds.has(id)&&!pageIds.has(id))throw Error("Dangling "+id);
 }
 report.nativeCount=rows.length;return {records:rows,report};
}
function reconcileOptionalFeatureMetadata(records,report,pages,tag){
 const modern=tag==="52",meta=records.filter(r=>r.kind==="optional-feature"&&r.featureType?.includes("MM"));
 const expected={"Careful Spell":1,"Distant Spell":1,"Empowered Spell":1,"Extended Spell":1,"Heightened Spell":modern?2:3,"Quickened Spell":2,"Subtle Spell":1,"Twinned Spell":modern?1:null,...(modern?{"Seeking Spell":1,"Transmuted Spell":1}:{})};
 if(meta.length!==Object.keys(expected).length)throw Error("Metamagic inventory changed");
 for(const row of meta){
  if(!Object.hasOwn(expected,row.name)||row.consumes?.name!=="Sorcery Point")throw Error("Unexpected Metamagic cost schema "+row.id);
  const amount=expected[row.name],combine=row.name==="Empowered Spell"||modern&&row.name==="Seeking Spell",page=modern?(row.name==="Careful Spell"?66:67):44;
  const evidence=pages.find(p=>p.page===page).textLayout;
  if(!evidence.includes(row.name))throw Error("Missing Metamagic publisher heading "+row.name);
  if(modern){
   // The published cost is on the next line in the same column as the heading.
   const lines=evidence.split("\n"),i=lines.findIndex(l=>l.includes(row.name)&&!l.includes("can use"));
   const column=lines[i].indexOf(row.name)>=60?60:0;
   const cost=lines[i+1].slice(column,column?undefined:60).trim();
   if(cost!=="Cost: "+amount+" Sorcery Point"+(amount===1?"":"s"))throw Error("Publisher Metamagic cost mismatch "+row.name+": "+cost);
  }
  if(amount===null){
   if(row.consumes.amountMin!==1||row.consumes.amountMax!==9||!evidence.includes("number of sorcery points equal to the spell’s level")||!evidence.includes("1 sorcery point if the spell is a cantrip"))throw Error("Legacy Twinned Spell cost mismatch");
   row.consumes.costBasis={type:"spell-level",minimum:1,maximum:9};
  }else{
   if((row.consumes.amount??1)!==amount)throw Error("Metamagic cost mismatch "+row.name);
   row.consumes.amount=amount;
  }
  const text=JSON.stringify(row.entries);
  if(combine!==text.includes("even if you")||combine&&!evidence.includes("You can use "+row.name+" even if"))throw Error("Metamagic combination evidence mismatch "+row.name);
  row.metamagicUse={canCombineWithAnotherOption:combine};
 }
 report.metamagicMetadata={options:meta.length,fixedCosts:meta.length-(modern?0:1),variableCosts:modern?0:1,combinationExceptions:meta.filter(r=>r.metamagicUse.canCombineWithAnotherOption).map(r=>r.id)};
 if(modern){
  const repeatable={"Agonizing Blast":72,"Eldritch Spear":73,"Lessons of the First Ones":73,"Repelling Blast":74};
  const invocations=records.filter(r=>r.kind==="optional-feature"&&r.featureType?.includes("EI"));
  if(invocations.length!==28)throw Error("Modern invocation inventory changed");
  for(const row of invocations){
   const canRepeat=Object.hasOwn(repeatable,row.name);
   if(JSON.stringify(row.entries).includes('"name":"Repeatable"')!==canRepeat)throw Error("Invocation repeatability mismatch "+row.name);
   if(canRepeat){
    const evidence=pages.find(p=>p.page===repeatable[row.name]).textLayout;
    if(!evidence.includes(row.name)||!evidence.includes("Repeatable. You can gain this invocation more")||!evidence.includes("choose a different"))throw Error("Publisher repeatability mismatch "+row.name);
    row.repeatability={allowed:true,distinctSelection:row.name==="Lessons of the First Ones"?"origin-feat":"cantrip"};
    if(row.name==="Lessons of the First Ones"){
     if(!evidence.includes("feat of your choice (see “Feats”).")||!evidence.includes("Origin feat."))throw Error("Origin feat choice evidence changed");
     row.feats=[{anyFromCategory:{category:["O"],count:1}}];
    }
   }else row.repeatability={allowed:false};
  }
  report.invocationRepeatability={options:invocations.length,repeatable:invocations.filter(r=>r.repeatability.allowed).map(r=>r.id)};
 }
}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const { read, write } = srdImportFiles(root);
const json=x=>JSON.stringify(x,null,2)+"\n";
const feats=read("data/reference/srd/structured/feats.json"),optional=read("data/reference/srd/structured/optionalfeatures.json"),src=read("data/reference/srd/structured-manifest.json");
const check=process.argv.includes("--check");
for(const [pack,tag] of [["dnd-5e","51"],["dnd-5-5e","52"]]){
 const base="data/rulesets/"+pack+"/",manifest=read(base+"manifest.json"),files=tag==="51"?["003-027","028-052","053-077"]:["005-029","030-054","055-079","080-104"];
 const pages=files.flatMap(x=>read(base+"native/rulebook/pages-"+x+".json")).sort((a,b)=>a.page-b.page);
 const {records,report}=generateFeatures(feats,optional,manifest,src,pages,tag);
 if(tag==="52"){
  const additions=expandedMetamagic(read("data/reference/srd/upstream-metamagic-52.json"),manifest,pages);
  if(additions.some(r=>records.some(x=>x.id===r.id)))throw Error("Duplicate expanded feature");
  records.push(...additions);report.nativeCounts["optional-feature"]+=additions.length;report.nativeCount+=additions.length;report.expandedSourceCounts={"optional-feature":additions.length};
 }
 const spells=fs.readdirSync(path.join(root,base+"native/spells")).filter(f=>f.endsWith(".json")).sort().flatMap(f=>read(base+"native/spells/"+f));
 reconcileOptionalFeatureMetadata(records,report,pages,tag);
 report.literalSpellRelationships=linkSpellRelationships(records,spells,"SRD"+tag);
 report.literalCharacterRelationships=linkCharacterRelationships(records,characterRelationshipCatalog(root,base,read,records),"SRD"+tag);
 for(const [file,body] of [[base+"native/feats-and-optional-features.json",json(records)],["data/reference/srd/feature-promotion-"+tag+".json",json(report)]]){

  write(file, body, check);
 }
 console.log(pack+": "+report.nativeCount+" feat and optional feature records");
}
