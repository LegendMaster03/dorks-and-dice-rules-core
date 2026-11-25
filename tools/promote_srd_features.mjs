// Promote edition-specific feats and optional features, excluding legacy duplicates.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
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
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),"utf8"));
const json=x=>JSON.stringify(x,null,2)+"\n";
const feats=read("data/reference/srd/structured/feats.json"),optional=read("data/reference/srd/structured/optionalfeatures.json"),src=read("data/reference/srd/structured-manifest.json");
const check=process.argv.includes("--check");
for(const [pack,tag] of [["dnd-5e","51"],["dnd-5-5e","52"]]){
 const base="data/rulesets/"+pack+"/",manifest=read(base+"manifest.json"),files=tag==="51"?["003-027","028-052","053-077"]:["005-029","030-054","055-079","080-104"];
 const pages=files.flatMap(x=>read(base+"native/rulebook/pages-"+x+".json")).sort((a,b)=>a.page-b.page);
 const {records,report}=generateFeatures(feats,optional,manifest,src,pages,tag);
 for(const [file,body] of [[base+"native/feats-and-optional-features.json",json(records)],["data/reference/srd/feature-promotion-"+tag+".json",json(report)]]){
  const target=path.join(root,file);
  if(check){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale feat promotion "+file);}
  else fs.writeFileSync(target,body);
 }
 console.log(pack+": "+report.nativeCount+" feat and optional feature records");
}
