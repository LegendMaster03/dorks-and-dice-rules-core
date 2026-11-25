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
  ["Size Categories","/data/8/entries/1/entries/9/entries/2",92,6],
  ["Standard Languages","/data/3/entries/8/entries/3",59,8],
  ["Exotic Languages","/data/3/entries/8/entries/4",59,8],
  ["Container Capacity","/data/4/entries/10/entries/3",69,13,[69,70]],
  ["Trade Goods","/data/4/entries/13/entries/2",72,13],
  ["Lifestyle Expenses","/data/4/entries/14/entries/1/entries/3",72,7],
  ["Services","/data/4/entries/14/entries/4/entries/3",74,9]
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
  ["Travel Terrain","/data/10/entries/0/entries/5/entries/1",192,11],
  ["XP Budget per Character","/data/10/entries/7/entries/2/entries/2/entries/1",202,20],
  ["Hit Dice by Size","/data/13/entries/1/entries/8",255,6],
  ["Experience Points by Challenge Rating","/data/13/entries/1/entries/20/entries/2",256,34],
  ["Proficiency Bonus by Challenge Rating","/data/13/entries/1/entries/20/entries/3/entries/1",256,8],
  ["Ability Descriptions","/data/1/entries/1/entries/1",5,6],
  ["Ability Scores","/data/1/entries/1/entries/2/entries/1",5,7],
  ["Ability Check Examples","/data/1/entries/2/entries/2/entries/1/entries/1",6,6],
  ["Saving Throw Examples","/data/1/entries/2/entries/3/entries/2/entries/1",7,6],
  ["Attack Roll Abilities","/data/1/entries/2/entries/4/entries/1/entries/1",7,3],
  ["Creature Size and Space","/data/1/entries/7/entries/2/entries/7/entries/2",14,6],
  ["Cover","/data/1/entries/7/entries/4/entries/2",15,3],
  ["Class Overview","/data/2/entries/1/entries/2/entries/1",19,12,[19,20]],
  ["Ability Scores and Backgrounds","/data/2/entries/1/entries/3/entries/2/entries/2",19,6,[19,20]],
  ["Standard Languages","/data/2/entries/1/entries/3/entries/6/entries/2",20,10],
  ["Rare Languages","/data/2/entries/1/entries/3/entries/6/entries/4",20,5],
  ["Level 1 Hit Points by Class","/data/2/entries/1/entries/6/entries/2/entries/4/entries/1",22,4],
  ["Hirelings","/data/6/entries/9/entries/1",102,3],
  ["Spellcasting Services","/data/6/entries/10/entries/1",102,7],
  ["Spell Preparation by Class","/data/7/entries/0/entries/1/entries/1",104,8],
  ["Schools of Magic","/data/7/entries/1/entries/2/entries/1",105,8],
  ["Sample Fear DCs","/data/10/entries/4/entries/2/entries/1",196,3],
  ["Sample Mental Stress Effects","/data/10/entries/4/entries/3/entries/2",196,3],
  ["Sentient Item's Alignment","/data/11/entries/9/entries/3/entries/5/entries/1",208,9],
  ["Sentient Item's Communication","/data/11/entries/9/entries/3/entries/5/entries/2",208,3],
  ["Sentient Item's Senses","/data/11/entries/9/entries/3/entries/5/entries/3",208,4],
  ["Sentient Item's Special Purpose","/data/11/entries/9/entries/3/entries/5/entries/4",208,10]
 ]
};


// Account for every table occurrence in the pinned book, not just its parent
// chapter. A covered catalog is not proof that every source table cell agrees.
function inventoryBookTables(book,report,tag){
 const selected=new Map(report.tables.map(t=>[t.pointer,t.id]));
 const existing=tag==="51"?{
  "/data/4/entries/8/entries/7/entries/2":"table.donning-and-doffing-armor",
  "/data/41/entries/1/entries/1":"deity catalog: Celtic pantheon",
  "/data/41/entries/2/entries/1":"deity catalog: Greek pantheon",
  "/data/41/entries/3/entries/3":"deity catalog: Egyptian pantheon",
  "/data/41/entries/4/entries/2":"deity catalog: Norse pantheon"
 }:{
  "/data/6/entries/14/entries/4":"table.spell-scroll-costs",
  "/data/11/entries/3/entries/2/entries/2":"table.magic-item-rarities-and-values"
 };
 const result=[];
 const escape=k=>k.replaceAll("~","~0").replaceAll("/","~1");
 function visit(node,pointer,context){
  if(!node||typeof node!=="object")return;
  if(node.type==="table"){
   const id=selected.get(pointer),representation=existing[pointer];
   result.push({pointer,caption:node.caption??null,context,rows:node.rows?.length??null,
    disposition:id?"projected":representation?"existing-catalog-representation":"pending-review",
    ...(id?{nativeId:id}:representation?{representation}:{})});
   // Continue walking in case a table contains further nested tables.
  }
  if(Array.isArray(node))node.forEach((child,i)=>visit(child,pointer+"/"+i,context));
  else for(const [key,child] of Object.entries(node))
   visit(child,pointer+"/"+escape(key),node.name??node.caption??context);
 }
 visit(book.data,"/data",null);
 for(const pointer of [...selected.keys(),...Object.keys(existing)])
  if(!result.some(t=>t.pointer===pointer))throw Error("Unaccounted book table pointer "+pointer);
 const counts={projected:0,"existing-catalog-representation":0,"pending-review":0};
 for(const t of result)counts[t.disposition]++;
 return {total:result.length,counts,occurrences:result};
}

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
 for(const [name,pointer,page,count,sourcePages=[page]] of selections[tag]){
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
  const refs=new Set(sourcePages.map(pageId));
  if(sourcePages.some(n=>!pageMap.has(n)))throw Error("Missing publisher table page "+name);
  // Normalize navigation and presentation aliases without inventing item identities.
  if(name==="Container Capacity"||name==="Services"||name==="Attack Roll Abilities"||name==="Spellcasting Services"){
   r.rows=r.rows.map(row=>row.map(cell=>typeof cell==="string"?cell
    .replace(/\{@book ([^}|]+)(?:\|[^}]+)?\}/g,(_,label)=>label==="chapter 7"?"Spells":label)
    .replace(/\{@filter ([^}|]+)(?:\|[^}]+)?\}/g,(_,label)=>label)
    .replace(/\{@i ([^}]+)\}/g,(_,label)=>label)
    .replace(/\{@item ([^}|]+)\|[^}|]+(?:\|([^}]+))?\}/g,(_,label,display)=>display??label):cell));
  }
  if(name==="Services")r.rows[2][0]="Within a city";
  if(name==="Attack Roll Abilities"){
   r.rows[0][1]+=" (see Rules Glossary)";
   refs.add("glossary.unarmed-strike");
  }
  if(name==="Cover")refs.add("glossary.cover");
  if(name==="Sentient Item's Senses")refs.add("sense.darkvision");
  for(const match of JSON.stringify(r.rows).matchAll(/\{@(language|background) ([^}|]+)(?:\|[^}]+)?\}/g)){
   refs.add(match[1]+"."+slug(match[2]));
  }
  if(tag==="51"&&(name==="Standard Languages"||name==="Exotic Languages")){
   for(const row of r.rows)refs.add("language."+slug(row[0]));
  }
  for(const match of JSON.stringify(r.rows).matchAll(/\{@class ([^}|]+)(?:\|[^}]+)?\}/g)){
   const cid="class."+slug(match[1]);refs.add(cid);
  }
  if(name==="Multiclassing Proficiencies")refs.add("item.thieves-tools");
  if(tag==="51"&&name==="Travel Pace")refs.add("skill.perception");
  r.catalogReferences=[...refs].sort();
  r.source=metadata(pointer,sourcePages);
  rows.push(r);report.tables.push({id,pointer,page,rowCount:count,...(sourcePages.length>1?{pages:sourcePages}:{})});
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
 // Individually reviewed extended rules. Keep the modern glossary and existing
 // native item definitions authoritative; book navigation remains plain text.
 const extraRules=tag==="51"?[
  ["Advantage and Disadvantage","/data/6/entries/5",[76,77],[]],
  ["Proficiency Bonus","/data/6/entries/6",[77],["skill.history","table.character-advancement"]],
  ["Saving Throws","/data/6/entries/9",[83],["rule.proficiency-bonus","rule.advantage-and-disadvantage"]],
  ["Resting","/data/7/entries/3",[87],[]],
  ["Cover","/data/8/entries/4",[96],[]],
  ["Mounted Combat","/data/8/entries/6",[99],["condition.prone","action.dash","action.disengage","action.dodge","reaction-option.opportunity-attack"]],
  ["Underwater Combat","/data/8/entries/7",[99],[]]
 ]:[
  ["Travel Pace","/data/10/entries/0",[192],["table.travel-pace","condition.exhaustion","spell.wind-walk","item-group.carpet-of-flying","glossary.difficult-terrain"]],
  ["Creating a Background","/data/10/entries/1",[192,193],[]],
  ["Combat Encounters","/data/10/entries/7",[202,203],["monster.bugbear-warrior","monster.giant-wasp","monster.giant-rat","monster.druid","monster.stirge","monster.wight","monster.warhorse-skeleton","monster.skeleton","monster.adult-red-dragon","monster.fire-giant","monster.ogre"]],
  ["Crafting Nonmagical Items","/data/6/entries/12",[103],["baseitem.plate-armor","baseitem.heavy-crossbow"]],
  ["Brewing Potions of Healing","/data/6/entries/13",[103],["item.herbalism-kit","item.potion-of-healing"]],
  ["Scribing Spell Scrolls","/data/6/entries/14",[103],["table.spell-scroll-costs","item-group.spell-scroll","skill.arcana","baseitem.calligrapher-s-supplies"]]
 ];
 const extraCorrections={
  "Advantage and Disadvantage":"Restore the original reroll-only wording and unrestricted Inspiration wording from pp.76–77; remove later source additions.",
  "Resting":"Restore published short-rest healing wording and long-rest sleep/light-activity wording from p.87; do not import later errata into the pinned SRD.",
  "Combat Encounters":"Restore Giant Rat and Druid encounter examples and remove the commercial Monster Manual navigation from pp.202–203."
 };
 for(const [name,pointer,nums,refs] of extraRules){
  const node=get(pointer);
  if(node.name!==name||nums.some(n=>!pageMap.has(n)))throw Error("Unreviewed book rule "+name);
  if(name!=="Advantage and Disadvantage"&&!nums.some(n=>norm(pageMap.get(n).textLayout).includes(norm(name))))throw Error("Missing book rule heading "+name);
  const local=new Set([...nums.map(pageId),...refs]);
  const project=x=>{
   if(typeof x==="string")return x.replace(/\{@book ([^}|]+)(?:\|[^}]+)?\}/g,(_,label)=>label);
   if(Array.isArray(x))return x.map(project);
   if(x&&typeof x==="object"){
    if(x.type==="table"){
     const id="table."+slug(x.caption);
     if(!known.has(id)&&!rows.some(r=>r.id===id))throw Error("Unprojected book table "+id);
     local.add(id);return "See the "+x.caption+" table.";
    }
    return Object.fromEntries(Object.entries(x).filter(([k])=>!["id","page","data","srd","srd52","basicRules","basicRules2024"].includes(k)).map(([k,v])=>[k,project(v)]));
   }
   return x;
  };
  let content=project(node.entries);
  if(name==="Advantage and Disadvantage"){
   content[3]=content[3].replaceAll("reroll or replace","reroll");
   content[4]=content[4].replace("advantage on checks related to the character's personality, ideals, or bonds","advantage");
  }
  if(name==="Resting"){
   content[0]=content[0].replace("rest-time","rest—time");
   content[1]="Adventurers can take short rests in the midst of an adventuring day and a long rest to end the day.";
   find(content,"Short Rest").entries[1]=find(content,"Short Rest").entries[1].replace(" (minimum of 0)","");
   const lr=find(content,"Long Rest");
   lr.entries[0]=lr.entries[0].replace("sleeps for at least 6 hours and performs no more than 2 hours of light activity, such as reading, talking, eating, or standing watch","sleeps or performs light activity: reading, talking, eating, or standing watch for no more than 2 hours");
   lr.entries[1]=lr.entries[1].replace("number of them. You regain at least 1 Hit Die when you finish a long rest.","number of them (minimum of one die).");
  }
  if(name==="Combat Encounters"){
   const before=JSON.stringify(content);
   content=JSON.parse(before.replace("{@creature Twig Blight|srd52|Twig Blights}","{@creature Giant Rat|srd52|Giant Rats}").replace("{@creature Nothic|srd52|Nothics}","{@creature Druid|srd52|Druids}").replace("use swarms from the Monster Manual instead","use swarms instead"));
   if(JSON.stringify(content).includes("Twig Blight")||JSON.stringify(content).includes("Nothic")||JSON.stringify(content).includes("Monster Manual"))throw Error("Unreconciled encounter example");
  }
  const id="rule."+slug(name);
  rows.push({id,kind:"rule",name,entries:content,catalogReferences:[...local].sort(),source:metadata(pointer,nums)});
  report.rules.push({id,pointer,pages:nums});
  if(extraCorrections[name])report.corrections.push({id,reason:extraCorrections[name]});
 }
 const ids=new Set(rows.map(r=>r.id));
 if(ids.size!==rows.length||rows.some(r=>known.has(r.id)))throw Error("Book projection identity collision");
 for(const r of rows)for(const id of r.catalogReferences)if(!known.has(id)&&!ids.has(id))throw Error("Missing book reference "+r.id+" -> "+id);
 report.nativeCount=rows.length;
 report.tableInventory=inventoryBookTables(book,report,tag);
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
