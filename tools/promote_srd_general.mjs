import { srdImportFiles } from "./srd_import_sources.mjs";
// Promote core action, language and sense definitions independently by edition.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
function generate(input,manifest,src,pages,tag){
 const modern=tag==="52",edition="SRD"+tag,slug=s=>s.toLowerCase().normalize("NFKC").replace(/[’']/g,"").replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const report={edition:manifest.officialPageImport.release,sourceCounts:{},nativeCounts:{},deferred:[],corrections:[],pendingReferences:[]},records=[];
 const actionPages=modern?{"Attack":177,"Dash":180,"Disengage":181,"Dodge":181,"Help":182,"Hide":183,"Influence":184,"Magic":185,"Ready":186,"Search":187,"Study":189,"Utilize":191}:{"Attack":93,"Cast a Spell":93,"Dash":93,"Disengage":93,"Dodge":93,"Help":93,"Hide":93,"Ready":93,"Search":94,"Use an Item":94};
 const normalize=s=>s.toLowerCase().replace(/[’‘]/g,"'").replace(/\s+/g," ");
 for(const [kind,file,array] of [["action","actions","action"],["language","languages","language"],["sense","senses","sense"]]){
 for(const [i,c] of input[file][array].entries()){
 if(c.source!==edition)continue;
 report.sourceCounts[kind]=(report.sourceCounts[kind]??0)+1;
 if(kind==="action"&&!Object.hasOwn(actionPages,c.name)){report.deferred.push({name:c.name,path:file+".json",pointer:"/"+array+"/"+i,reason:"supplementary action, reaction, or attack option; distinct representation requires review"});continue;}
 const name=c.name==="Use an Item"?"Use an Object":c.name;
 const r={id:kind+"."+slug(name),kind,name};
 for(const [k,v] of Object.entries(c))if(!["name","source","page","hasFluff","hasFluffImages","basicRules2024","reprintedAs"].includes(k))r[k]=JSON.parse(JSON.stringify(v));
 if(name!==c.name)report.corrections.push({id:r.id,reason:"Use the publisher action title Use an Object (p.94), rather than the source label Use an Item."});
 if(kind==="language"&&!modern&&r.type==="secret"){delete r.typicalSpeakers;report.corrections.push({id:r.id,reason:"Remove source-added typical-speaker list for secret language; publisher p.59 does not supply it."});}
 const candidates=pages.filter(p=>kind==="language"?p.page===(modern?20:59):kind==="sense"?(modern?p.page>=176&&p.page<=191:p.page===86||p.page===257):(modern?p.page>=177&&p.page<=191:p.page===93||p.page===94));
 const needle=kind==="language"&&!modern?(name==="Druidic"?"tongue of druids":name==="Thieves' Cant"?"secret language, such as thieves":name):name;
 const locators=candidates.filter(p=>normalize(p.textLayout).includes(normalize(needle)));
 if(!locators.length)throw Error("Missing publisher locator: "+r.id);
 const loc=locators.find(p=>kind==="action"&&p.page===actionPages[c.name])??locators.find(p=>p.page===c.page)??locators.find(p=>kind==="action"&&modern&&normalize(p.textLayout).includes(normalize(name+" [Action]")))??locators[0];
 r.catalogReferences=[loc.id];r.source={release:manifest.officialPageImport.release,url:loc.source.url,pdfSha256:manifest.officialPageImport.pdfSha256,license:manifest.license,attribution:manifest.attribution,pages:[loc.page],candidatePage:c.page??null,structuredSource:{provider:src.provider,commit:src.commit,path:file+".json",pointer:"/"+array+"/"+i},structuredStatus:"pinned edition-specific structured record; publisher name or equivalent locator verified; body and semantic reconciliation pending"};
 const {source,catalogReferences,...body}=r;for(const reference of new Set([...JSON.stringify(body).matchAll(/\{@(spell|creature|condition|skill|item|feat|action|sense|variantrule|language|class|race|optfeature|classFeature|subclassFeature) ([^}]+)\}/g)].map(m=>m[1]+"|"+m[2])))report.pendingReferences.push({id:r.id,reference,reason:"inline semantic reconciliation pending"});
 records.push(r);report.nativeCounts[kind]=(report.nativeCounts[kind]??0)+1;
 }}
 if(new Set(records.map(r=>r.id)).size!==records.length)throw Error("duplicate");
 report.nativeCount=records.length;return {records,report};
}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const { read, write } = srdImportFiles(root);
const json=x=>JSON.stringify(x,null,2)+"\n";
const input=Object.fromEntries(["actions","languages","senses"].map(n=>[n,read("data/reference/srd/structured/"+n+".json")]));
const src=read("data/reference/srd/structured-manifest.json"),check=process.argv.includes("--check");
for(const [pack,tag,files] of [["dnd-5e","51",["053-077","078-102","253-277"]],["dnd-5-5e","52",["005-029","155-179","180-204"]]]){
 const base="data/rulesets/"+pack+"/",manifest=read(base+"manifest.json"),pages=files.flatMap(f=>read(base+"native/rulebook/pages-"+f+".json")).sort((a,b)=>a.page-b.page);
 const {records,report}=generate(input,manifest,src,pages,tag);
 for(const [file,body] of [[base+"native/general-rules.json",json(records)],["data/reference/srd/general-promotion-"+tag+".json",json(report)]]){

 write(file, body, check);
 }
 console.log(pack+": "+report.nativeCount+" core actions, languages and senses");
}
