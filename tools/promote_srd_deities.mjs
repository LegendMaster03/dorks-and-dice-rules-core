import { srdImportFiles } from "./srd_import_sources.mjs";
// Reconcile the legacy fantasy-historical pantheon tables, not commercial setting deities.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
function generatePantheons(source,manifest,src,pages){
 const groups=new Set(["Celtic","Greek","Egyptian","Norse"]);
 const slug=s=>s.toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const clean=s=>s.replace(/[’ʼ]/g,"'").replace(/\u00ad/g,"").replace(/[\u2010-\u2014]/g,"-").replace(/-+/g,"-").replace(/\s+/g," ").trim();
 const norm=s=>clean(s).toLowerCase();
 const table=new Map(),report={edition:"srd-5.1",nativeCount:0,excluded:[],corrections:[],publisherRows:0};
 let pantheon="Celtic";
 for(const page of pages.filter(p=>p.page>=360&&p.page<=362)){
  for(const line of page.textLayout.split("\n")){
   const group=line.trim().match(/^(Celtic|Greek|Egyptian|Norse) Deities$/);
   if(group){pantheon=group[1];continue;}
   const cells=line.trim().split(/\s{2,}/);
   if(cells.length!==4||!/^(LG|LN|LE|NG|N|NE|CG|CN|CE)$/.test(cells[1])||!cells[0].includes(", "))continue;
   const [name,...title]=clean(cells[0]).split(", ");
   const key=pantheon+"|"+norm(name);
   if(table.has(key))throw Error("Duplicate publisher deity "+key);
   table.set(key,{page,name,title:title.join(", "),alignment:cells[1].split(""),domains:clean(cells[2]).split(", "),symbol:clean(cells[3])});
  }
 }
 report.publisherRows=table.size;
 const records=[];
 for(const [i,c] of source.deity.entries()){
  if(c.source!=="SRD51")continue;
  const pointer={path:"deities.json",pointer:"/deity/"+i};
  if(!groups.has(c.pantheon)){report.excluded.push({...pointer,name:c.name,reason:"Pantheon not in official SRD 5.1 deity tables; no separate entity is authorized by a prose mention."});continue;}
  const key=c.pantheon+"|"+norm(c.name),row=table.get(key);
  if(!row)throw Error("Missing publisher deity row: "+key);
  const id="deity."+slug(c.pantheon)+"."+slug(c.name),changes=[];
  for(const field of ["title","symbol"])if(norm(c[field])!==norm(row[field]))changes.push({field,from:c[field],to:row[field]});
  for(const field of ["domains","alignment"])if(JSON.stringify(c[field])!==JSON.stringify(row[field]))changes.push({field,from:c[field],to:row[field]});
  if(changes.length)report.corrections.push({id,page:row.page.page,changes});
  records.push({id,kind:"deity",name:c.name,pantheon:c.pantheon,title:norm(c.title)===norm(row.title)?c.title:row.title,alignment:row.alignment,domains:row.domains,symbol:norm(c.symbol)===norm(row.symbol)?c.symbol:row.symbol,catalogReferences:[row.page.id],source:{release:manifest.officialPageImport.release,url:row.page.source.url,pdfSha256:manifest.officialPageImport.pdfSha256,license:manifest.license,attribution:manifest.attribution,pages:[row.page.page],structuredSource:{provider:src.provider,commit:src.commit,...pointer},structuredStatus:"Publisher deity table row verified; domains, alignment, title and symbol reconciled."}});
  table.delete(key);
 }
 if(table.size)throw Error("Missing structured candidates "+[...table.keys()]);
 report.nativeCount=records.length;return {records,report};
}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const { read, write } = srdImportFiles(root);
const base="data/rulesets/dnd-5e/",src=read("data/reference/srd/structured-manifest.json");
const {records,report}=generatePantheons(read("data/reference/srd/structured/deities.json"),read(base+"manifest.json"),src,read(base+"native/rulebook/pages-353-377.json"));
for(const [file,value] of [[base+"native/deities.json",records],["data/reference/srd/deity-promotion-51.json",report]]){
 const body=JSON.stringify(value,null,2)+"\n";
 write(file, body, process.argv.includes("--check"));
}
console.log("dnd-5e: "+report.nativeCount+" verified deity table rows");
