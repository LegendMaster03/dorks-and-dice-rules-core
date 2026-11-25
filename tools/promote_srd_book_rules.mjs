// Project individually selected book tables and rules, never whole commercial chapters.
// The pinned book's top-level occurrence participates in the existing coverage audit;
// entryPointer records the exact nested node used by this projection.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";

const selections={
 "51":[
  ["Character Advancement","/data/3/entries/5",56,20],
  ["Multiclassing Prerequisites","/data/3/entries/6/entries/3/entries/1",56,12],
  ["Multiclassing Proficiencies","/data/3/entries/6/entries/7/entries/1",57,12],
  ["Multiclass Spellcaster: Spell Slots per Spell Level","/data/3/entries/6/entries/8/entries/4/entries/4",58,20],
  ["Standard Exchange Rates","/data/4/entries/6",62,5],
  ["Ability Scores and Modifiers","/data/6/entries/4/entries/5",76,8],
  ["Typical Difficulty Classes","/data/6/entries/7/entries/2",77,6],
  ["Travel Pace","/data/7/entries/1/entries/2/entries/2/entries/4",84,3],
  ["Size Categories","/data/8/entries/1/entries/9/entries/2",92,6]
 ],
 "52":[
  ["Ability Modifiers","/data/1/entries/1/entries/3/entries/1",6,16],
  ["Typical Difficulty Classes","/data/1/entries/2/entries/2/entries/3/entries/1",6,6],
  ["Proficiency Bonus","/data/1/entries/3/entries/2",8,8],
  ["Travel Pace","/data/1/entries/6/entries/6/entries/3/entries/1",12,3],
  ["Ability Score Point Costs","/data/2/entries/1/entries/4/entries/1/entries/3/entries/1",21,8],
  ["Standard Array by Class","/data/2/entries/1/entries/4/entries/2/entries/2",21,12],
  ["Ability Scores and Modifiers","/data/2/entries/1/entries/4/entries/4/entries/1",21,10],
  ["Character Advancement","/data/2/entries/2/entries/2",23,20],
  ["Starting Equipment at Higher Levels","/data/2/entries/3/entries/2/entries/2",24,4],
  ["Multiclass Spellcaster: Spell Slots per Spell Level","/data/2/entries/4/entries/6/entries/4/entries/1",26,20],
  ["Coin Values","/data/6/entries/1/entries/2",89,5],
  ["XP Budget per Character","/data/10/entries/7/entries/2/entries/2/entries/1",202,20],
  ["Hit Dice by Size","/data/13/entries/1/entries/8",255,6],
  ["Experience Points by Challenge Rating","/data/13/entries/1/entries/20/entries/2",256,34],
  ["Proficiency Bonus by Challenge Rating","/data/13/entries/1/entries/20/entries/3/entries/1",256,8]
 ]
};

function generateBookRules(book,manifest,src,pages,tag,known){
 const rows=[],report={edition:manifest.officialPageImport.release,tables:[],rules:[],corrections:[],nativeCount:0};
 const slug=s=>s.normalize("NFKC").replace(/[’‘]/g,"'").toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const get=p=>p.split("/").slice(1).reduce((o,k)=>o[k],book);
 const clone=x=>JSON.parse(JSON.stringify(x));
 const pageMap=new Map(pages.map(p=>[p.page,p]));
 const pageId=n=>"rulebook.page-"+String(n).padStart(3,"0");
 const norm=s=>s.toLowerCase().replace(/[^a-z0-9]+/g,"");
 const metadata=(pointer,nums)=>({
  release:manifest.officialPageImport.release,url:pageMap.get(nums[0]).source.url,
  pdfSha256:manifest.officialPageImport.pdfSha256,license:manifest.license,attribution:manifest.attribution,pages:nums,
  structuredSource:{provider:src.provider,commit:src.commit,path:"book/book-srd"+tag+".json",
   pointer:pointer.split("/").slice(0,3).join("/"),entryPointer:pointer},
  structuredStatus:"Selected nested book definition; publisher page locator and targeted fidelity review; not whole-chapter certification."
 });
 const classRefs=new Set();
 for(const [name,pointer,page,count] of selections[tag]){
  const t=get(pointer);
  if(t.type!=="table"||t.caption!==name||t.rows.length!==count)throw Error("Unexpected table "+pointer);
  const pageText=norm(pageMap.get(page).textLayout);
  // Split headings in the PDF may have interleaved second-column text.
  const heading=name.split(":")[0];
  if(!pageText.includes(norm(heading)))throw Error("Missing publisher heading "+name+" on "+page);
  const cols=t.colLabels??t.colLabelRows?.at(-1);
  if(!cols||t.rows.some(r=>r.length!==cols.length))throw Error("Nonrectangular table "+name);
  const id="table."+slug(name);
  const r={id,kind:"table",name,colLabels:clone(cols),rows:clone(t.rows)};
  if(t.colLabelRows)r.colLabelRows=clone(t.colLabelRows);
  if(t.footnotes)r.footnotes=clone(t.footnotes);
  const refs=new Set([pageId(page)]);
  for(const match of JSON.stringify(r.rows).matchAll(/\{@class ([^}|]+)(?:\|[^}]+)?\}/g)){
   const cid="class."+slug(match[1]);refs.add(cid);classRefs.add(cid);
  }
  if(name==="Multiclassing Proficiencies")refs.add("item.thieves-tools");
  if(tag==="51"&&name==="Travel Pace")refs.add("skill.perception");
  r.catalogReferences=[...refs].sort();
  r.source=metadata(pointer,[page]);
  rows.push(r);report.tables.push({id,pointer,page,rowCount:count});
 }
 // Keep source prose and its nested rule structure, but point at the canonical
 // table identities rather than duplicating data or inventing a renderer tag.
 const multiPointer=tag==="51"?"/data/3/entries/6":"/data/2/entries/4";
 const multi=get(multiPointer),nums=tag==="51"?[56,57,58]:[24,25,26];
 if(multi.name!=="Multiclassing"||nums.some(n=>!pageMap.has(n)))throw Error("Missing multiclassing source");
 const linked=new Set([pageId(nums[0]),...nums.slice(1).map(pageId),"table.character-advancement"]);
 const clean=x=>{
  if(typeof x==="string"){
   // Book navigation is display text, not an executable reference.
   return x.replace(/\{@book ([^}|]+)(?:\|[^}]+)?\}/g,(_,name)=>name);
  }
  if(Array.isArray(x))return x.map(clean);
  if(x&&typeof x==="object"){
   if(x.type==="table"){
    const id="table."+slug(x.caption);
    if(!rows.some(r=>r.id===id))throw Error("Unprojected multiclassing table "+id);
    linked.add(id);return "See the "+x.caption+" table.";
   }
   const result={};
   for(const [k,v] of Object.entries(x))if(!["id","page","data","srd","srd52","basicRules","basicRules2024"].includes(k))result[k]=clean(v);
   return result;
  }
  return x;
 };
 const entries=clean(multi.entries);
 const find=(arr,name)=>{
  for(const n of arr){if(n&&typeof n==="object"){if(n.name===name)return n;if(n.entries){const found=find(n.entries,name);if(found)return found;}}}
 };
 const slots=find(entries,"Spell Slots");
 if(!slots)throw Error("Missing spell slot rule");
 if(tag==="51"){
  const original=slots.entries[0];
  slots.entries[0]=original.replace(", and a third of your fighter or rogue levels (rounded down) if you have the Eldritch Knight or the Arcane Trickster feature","").replace("wizard classes, half your levels","wizard classes, and half your levels");
  if(original===slots.entries[0]||!slots.entries[0].includes("rounded down")||/Eldritch Knight|Arcane Trickster/.test(slots.entries[0]))throw Error("Legacy slot correction failed");
  const prerequisite=find(entries,"Prerequisites");
  prerequisite.entries[0]=prerequisite.entries[0].replace("new class. having","new class, having");
  find(entries,"Experience Points").entries[0]=find(entries,"Experience Points").entries[0].replace(" in chapter I","");
  find(entries,"Proficiency Bonus").entries[0]=find(entries,"Proficiency Bonus").entries[0].replace(" in chapter 1","");
 }else{
  const list=slots.entries.find(v=>v?.type==="list");
  if(list?.items.length!==3||!list.items[2].includes("Eldritch Knight or Arcane Trickster"))throw Error("Modern slot correction failed");
  list.items.pop();
  if(!list.items[1].includes("round up"))throw Error("Modern half-caster rounding changed");
  find(entries,"Experience Points").entries[0]=find(entries,"Experience Points").entries[0].replace("Character Advancement table in this chapter","Character Advancement table in Character Creation");
  linked.add("glossary.armor-class");
 }
 linked.add("spell.burning-hands");
 for(const c of ["barbarian","bard","cleric","druid","fighter","monk","paladin","ranger","rogue","sorcerer","warlock","wizard"])linked.add("class."+c);
 const rule={id:"rule.multiclassing",kind:"rule",name:"Multiclassing",entries,
  catalogReferences:[...linked].sort(),source:metadata(multiPointer,nums)};
 rows.push(rule);
 report.rules.push({id:rule.id,pointer:multiPointer,pages:nums});
 report.corrections.push({id:rule.id,reason:"Remove non-SRD Eldritch Knight/Arcane Trickster spell-slot contributions; retain legacy half-caster rounding down and modern rounding up as printed. Normalize book navigation; canonicalize nested tables."});
 const ids=new Set(rows.map(r=>r.id));
 if(ids.size!==rows.length||rows.some(r=>known.has(r.id)))throw Error("Book projection identity collision");
 for(const r of rows)for(const id of r.catalogReferences)if(!known.has(id)&&!ids.has(id))throw Error("Missing book reference "+r.id+" -> "+id);
 report.nativeCount=rows.length;
 return {rows,report};
}

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),"utf8"));
const src=read("data/reference/srd/structured-manifest.json");
const readNative=(dir)=>fs.readdirSync(path.join(root,dir),{withFileTypes:true}).flatMap(e=>{
 const p=dir+"/"+e.name;
 if(e.isDirectory())return readNative(p);
 if(!e.name.endsWith(".json")||e.name==="book-rules-and-tables.json")return [];
 const value=read(p);
 return Array.isArray(value)?value:value.id?[value]:Object.entries(value).filter(([k,v])=>!k.startsWith("_")&&Array.isArray(v)).flatMap(([,v])=>v);
});
for(const [pack,tag] of [["dnd-5e","51"],["dnd-5-5e","52"]]){
 const dir="data/rulesets/"+pack+"/",native=dir+"native";
 const existing=readNative(native);
 const pages=existing.filter(r=>r.kind==="rulebook-page"||r.id?.startsWith("rulebook.page-"));
 const known=new Set(existing.map(r=>r.id));
 const {rows,report}=generateBookRules(read("data/reference/srd/structured/book/book-srd"+tag+".json"),read(dir+"manifest.json"),src,pages,tag,known);
 for(const [file,value] of [[native+"/book-rules-and-tables.json",rows],["data/reference/srd/book-rule-promotion-"+tag+".json",report]]){
  const target=path.join(root,file),body=JSON.stringify(value,null,2)+"\n";
  if(process.argv.includes("--check")){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale book projection "+file);}
  else fs.writeFileSync(target,body);
 }
 console.log(pack+": "+report.tables.length+" book tables, "+report.rules.length+" book rules");
}
