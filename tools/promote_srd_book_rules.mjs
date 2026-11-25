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
  ["Fixed Hit Points by Class","/data/2/entries/2/entries/3/entries/1/items/1/entries/1",23,4],
  ["Hirelings","/data/6/entries/9/entries/1",102,3],
  ["Spellcasting Services","/data/6/entries/10/entries/1",102,7],
  ["Spell Preparation by Class","/data/7/entries/0/entries/1/entries/1",104,8],
  ["Schools of Magic","/data/7/entries/1/entries/2/entries/1",105,8],
  ["Sample Fear DCs","/data/10/entries/4/entries/2/entries/1",196,3],
  ["Sample Mental Stress Effects","/data/10/entries/4/entries/3/entries/2",196,3],
  ["Sentient Item's Alignment","/data/11/entries/9/entries/3/entries/5/entries/1",208,9],
  ["Sentient Item's Communication","/data/11/entries/9/entries/3/entries/5/entries/2",208,3],
  ["Sentient Item's Senses","/data/11/entries/9/entries/3/entries/5/entries/3",208,4],
  ["Sentient Item's Special Purpose","/data/11/entries/9/entries/3/entries/5/entries/4",208,10],
  ["Actions","/data/1/entries/4/entries/1",9,12,[9,10]],
  ["Glossary Abbreviations","/data/9/entries/0/entries/2",176,33],
  ["Magic Item Categories","/data/11/entries/2/entries/1",204,9],
  ["Potion Miscibility","/data/11/entries/2/entries/3/entries/2/entries/2",204,8],
  ["Magic Item Tools","/data/11/entries/8/entries/2/entries/1",207,9],
  ["Magic Item Crafting Time and Cost","/data/11/entries/8/entries/4/entries/3/entries/2",207,5]
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
  "/data/41/entries/4/entries/2":"deity catalog: Norse pantheon",
  "/data/6/entries/7/entries/6/entries/3":"18 native skill definitions; ability grouping verified",
  "/data/40/entries/7/entries/1":"condition.exhaustion (identical six-row nested table)"
 }:{
  "/data/6/entries/14/entries/4":"table.spell-scroll-costs",
  "/data/11/entries/3/entries/2/entries/2":"table.magic-item-rarities-and-values",
  "/data/1/entries/3/entries/4/entries/2/entries/1":"18 native skill definitions; abilities and example uses verified"
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

function projectModernEquipmentTables(book,existing,pages,metadata,rows,report){
 const selections=[
 ["Ammunition","/data/6/entries/5/entries/2/entries/2/entries/1",[96],5],
 ["Arcane Focuses","/data/6/entries/5/entries/2/entries/4/entries/1",[96],5,"item-group.arcane-focus"],
 ["Druidic Focuses","/data/6/entries/5/entries/2/entries/30/entries/1",[97],3,"item-group.druidic-focus"],
 ["Holy Symbols","/data/6/entries/5/entries/2/entries/37/entries/2",[97],3,"item-group.holy-symbol"],
 ["Mounts and Other Animals","/data/6/entries/6/entries/4",[100],8],
 ["Tack, Harness, and Drawn Vehicles","/data/6/entries/6/entries/5",[100],11],
 ["Airborne and Waterborne Vehicles","/data/6/entries/6/entries/6/entries/6",[101],7],
 ["Food, Drink, and Lodging","/data/6/entries/8/entries/1",[101,102],20],
 ["Weapons","/data/6/entries/2/entries/2",[91],42],
 ["Armor","/data/6/entries/3/entries/4",[92],17],
 ["Adventuring Gear","/data/6/entries/5/entries/1",[95],82]
 ];
 const slug=s=>s.normalize("NFKC").replace(/[’‘]/g,"'").toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const itemKinds=new Set(["item","baseitem","item-group"]);
 const items=existing.filter(x=>itemKinds.has(x.kind));
 const byId=new Map(items.map(x=>[x.id,x]));
 const nameMap=new Map();
 for(const r of items){const key=slug(r.name);nameMap.set(key,[...(nameMap.get(key)??[]),r]);}
 const resolve=(raw)=>{const [name,source]=raw.split("|");if(source.toLowerCase()!=="srd52")throw Error("Foreign equipment "+raw);const hits=nameMap.get(slug(name))??[];if(hits.length!==1)throw Error("Nonunique equipment "+raw+" "+hits.map(x=>x.id));return hits[0];};
 const equal=(actual,expected,where)=>{if(typeof expected==="number"?typeof actual!=="number"||Math.abs(actual-expected)>1e-8:actual!==expected)throw Error(where+": "+actual+" != "+expected);};
 const number=s=>{s=s.replaceAll(",","").replace(/ \(full\)$/,"").replace(/ (lb\.|mph)$/,"").trim();if(s==="—")return 0;if(s==="1/2")return .5;if(s==="1/4")return .25;const m=s.match(/^(\d+(?:\.\d+)?)?\s*([½¼¾])?$/);if(!m||!m[1]&&!m[2])throw Error("Unparsed numeric cell "+s);return Number(m[1]??0)+({"½":.5,"¼":.25,"¾":.75}[m[2]]??0);};
 const money=s=>{const m=s.match(/^([\d,]+) (CP|SP|GP)$/);if(!m)throw Error("Unparsed price "+s);return Number(m[1].replaceAll(",",""))*({CP:1,SP:10,GP:100}[m[2]]);};
 const display=s=>s.replace(/\{@(?:item|itemProperty|itemMastery|dice|action|variantrule) ([^}|]+)(?:\|[^}|]+)?(?:\|([^}]+))?\}/g,(_,name,label)=>label??name).replace(/\{@i ([^}]+)\}/g,"$1");
 const norm=s=>s.toLowerCase().replace(/[’‘]/g,"'").replaceAll("½","1/2").replaceAll("¼","1/4").replaceAll("¾","3/4").replace(/\s/g,"");
 const ammoUnitIds=["baseitem.arrow","baseitem.bolt","baseitem.firearm-bullet","baseitem.sling-bullet","baseitem.needle"];
 for(const [name,pointer,nums,count,groupId] of selections){
  const node=pointer.split("/").slice(1).reduce((o,k)=>o[k],book), id="table."+slug(name);
  if(node.type!=="table"||node.caption!==name||node.rows.length!==count)throw Error("Equipment table identity "+name);
  const publisher=pages.filter(p=>nums.includes(p.page)).map(p=>p.textLayout);
  if(publisher.length!==nums.length||!publisher.some(p=>norm(p).includes(norm(name))))throw Error("Publisher table heading "+name);
  const refs=new Set(nums.map(p=>"rulebook.page-"+String(p).padStart(3,"0"))), members=new Set();
  const tableRows=JSON.parse(JSON.stringify(node.rows));let itemCount=0,category=null;
  for(let i=0;i<tableRows.length;i++){
   const raw=tableRows[i], row=Array.isArray(raw)?raw:raw.row;
   if(!Array.isArray(row))throw Error("Invalid equipment row "+name);
   if(row.length===1&&row[0]?.type==="cell"){
    const heading=display(row[0].entry);
    if(row[0].width!==node.colLabels.length||!publisher.some(p=>norm(p).includes(norm(heading))))throw Error("Invalid group heading "+name);
    if(name==="Weapons"||name==="Armor")category=heading;
    if(name==="Armor"&&heading.startsWith("Shield"))refs.add("action.utilize");
    continue;
   }
   if(row.length!==node.colLabels.length||row.some(x=>typeof x!=="string"))throw Error("Invalid equipment cells "+name);
   // Match the visible published row, not commercial tags or a guessed identity.
   const visible=row.map(display);
   let published=publisher.some(p=>norm(p).includes(norm(visible.join(" "))));
   // Four wide weapon rows place the final property on a continuation line.
   if(!published&&name==="Weapons"&&visible[2].endsWith(", Two-Handed")){
    const first=[...visible];first[2]=first[2].slice(0,-" Two-Handed".length);
    published=publisher.some(p=>p.split("\n").some((line,j,lines)=>norm(line).includes(norm(first.join(" ")))&&lines[j+1]?.trim()==="Two-Handed"));
   }
   if(!published)throw Error("Publisher row mismatch "+name+" "+JSON.stringify(visible));
   // Sling bullets are sold in twenty-piece packs, not as twenty units of a single catalog record.
   if(name==="Ammunition"&&i===3)row[0]="{@item Sling Bullets (20)|srd52|Bullets, Sling}";
   if(name==="Adventuring Gear"){
    if(row[0]==="Ammunition"){refs.add("table.ammunition");continue;}
    if(/^Spell Scroll \((Cantrip|Level 1)\)$/.test(row[0]))row[0]="{@item "+row[0]+"|srd52}";
   }
   const links=[...row.join(" ").matchAll(/\{@item ([^}]+)\}/g)].map(m=>resolve(m[1]));
   for(const link of links)refs.add(link.id);
   if(!links.length){
    if(name==="Tack, Harness, and Drawn Vehicles"&&row[0]==="Stabling per day"||name==="Food, Drink, and Lodging")continue;
    throw Error("Unexpected unlinked equipment row "+name+" "+row[0]);
   }
   const item=links[0];members.add(item.id);itemCount++;
   if(name==="Weapons"){
    equal(item.weaponCategory,category.startsWith("Simple")?"simple":"martial",id+" category "+item.id);
    equal(item.type,(category.includes("Melee")?"M":"R")+"|SRD52",id+" type "+item.id);
    const m=visible[1].match(/^(\d+(?:d\d+)?) (Bludgeoning|Piercing|Slashing)$/);
    if(!m)throw Error("Weapon damage format "+item.id);
    equal(String(item.dmg1),m[1],item.id+" damage");
    equal(item.dmgType,m[2][0],item.id+" damage type");
    const props=[...row[2].matchAll(/\{@itemProperty ([^}|]+)\|([^}|]+)(?:\|[^}]+)?\}/g)].map(m=>{
     equal(m[2].toUpperCase(),"SRD52",item.id+" property edition");refs.add("item-property."+slug(m[1]));return m[1];
    });
    const actual=(item.property??[]).map(p=>typeof p==="string"?p:p.uid);
    equal(JSON.stringify(actual.map(p=>p.split("|")[0]).sort()),JSON.stringify(props.sort()),item.id+" properties");
    if(actual.some(p=>p.includes("|")&&p.split("|")[1]!=="SRD52"))throw Error("Foreign weapon property "+item.id);
    equal(item.property?.find(p=>typeof p==="object")?.note??"",visible[2].includes("(unless mounted)")?"unless mounted":"",item.id+" property note");
    equal(item.range??"",visible[2].match(/Range (\d+\/\d+)/)?.[1]??"",item.id+" range");
    equal(item.dmg2??"",visible[2].match(/Versatile \((\d+d\d+)\)/)?.[1]??"",item.id+" versatile damage");
    const ammo=visible[2].match(/; (Arrow|Bolt|Bullet|Needle)\)/)?.[1];
    if(ammo){
     const unit=resolve(item.ammoType),expected=ammo==="Bullet"?(item.name==="Sling"?"Sling Bullet":"Firearm Bullet"):ammo;
     equal(unit.name,expected,item.id+" ammunition type");refs.add(unit.id);
    }else if(item.ammoType)throw Error("Unexpected ammunition type "+item.id);
    equal(JSON.stringify(item.mastery),JSON.stringify([visible[3]+"|SRD52"]),item.id+" mastery");
    refs.add("item-mastery."+slug(visible[3]));
   }
   if(name==="Armor"){
    const type=category.startsWith("Light")?"LA":category.startsWith("Medium")?"MA":category.startsWith("Heavy")?"HA":"S";
    equal(item.type,type+"|SRD52",item.id+" armor category");
    const expectedAc=type==="S"?"+"+item.ac:type==="LA"?item.ac+" + Dex modifier":type==="MA"?item.ac+" + Dex modifier (max 2)":String(item.ac);
    equal(visible[1],expectedAc,item.id+" AC");
    equal(visible[2],item.strength?"Str "+item.strength:"—",item.id+" strength");
    equal(visible[3],item.stealth?"Disadvantage":"—",item.id+" stealth");
   }
   for(let c=1;c<row.length;c++){
    const label=node.colLabels[c],v=row[c],context=id+" "+item.id+" "+label;
    if(v==="Varies"&&(label==="Cost"||label==="Weight")){
     if(item.kind!=="item-group")throw Error("Non-group variable equipment "+item.id);
    }else if(label==="Cost")equal(item.value,money(v),context);
    else if(label==="Weight")equal(item.weight??0,number(v),context);
    else if(name==="Weapons"&&["Damage","Properties","Mastery"].includes(label)||name==="Armor"&&["Armor Class (AC)","Strength","Stealth"].includes(label))continue;
    else if(label==="Carrying Capacity")equal(item.carryingCapacity,number(v),context);
    else if(label==="Speed")equal(item.vehSpeed,number(v),context);
    else if(label==="Crew")equal(item.crew,number(v),context);
    else if(label==="Passengers")equal(item.capPassenger??0,number(v),context);
    else if(label==="Cargo (Tons)")equal(item.capCargo??0,number(v),context);
    else if(label==="AC")equal(item.vehAc,number(v),context);
    else if(label==="HP")equal(item.vehHp,number(v),context);
    else if(label==="Damage Threshold")equal(item.vehDmgThresh??0,number(v),context);
    else if(label==="Amount"){
     const unit=byId.get(ammoUnitIds[i]),quantity=number(v),pack=item.packContents;
     if(!unit||pack?.length!==1||pack[0].quantity!==quantity||resolve(pack[0].item).id!==unit.id)throw Error("Ammunition pack identity "+item.id);
     equal(unit.value*quantity,item.value,context+" price");equal(unit.weight*quantity,item.weight,context+" weight");refs.add(unit.id);
    }else if(label==="Storage"){
     const storage=links[1],unit=byId.get(ammoUnitIds[i]),quantity=number(row[1]);
     if(!storage?.containerCapacity?.item?.some(x=>Object.entries(x).some(([key,value])=>resolve(key).id===unit.id&&value===quantity)))throw Error("Ammunition storage capacity "+item.id);
    }else throw Error("Unreviewed equipment field "+label);
   }
  }
  if(groupId){
   const group=byId.get(groupId);if(!group||group.items.length!==members.size||group.items.some(x=>!members.has(resolve(x).id)))throw Error("Focus group membership "+groupId);
   refs.add(groupId);
  }
  rows.push({id,kind:"table",name,colLabels:JSON.parse(JSON.stringify(node.colLabels)),rows:tableRows,catalogReferences:[...refs].sort(),source:metadata(pointer,nums)});
  report.tables.push({id,pointer,page:nums[0],rowCount:count,...(nums.length>1?{pages:nums}:{})});
 }
}

function projectLegacyEquipmentTables(book,existing,pages,metadata,rows,report){
 const selections=[
 ["Armor","/data/4/entries/8/entries/6",[63,64],17],
 ["Weapons","/data/4/entries/9/entries/4",[65,66],41],
 ["Adventuring Gear","/data/4/entries/10/entries/2",[68,69],108],
 ["Tools","/data/4/entries/11/entries/1",[70],41],
 ["Mounts and Other Animals","/data/4/entries/12/entries/4",[71,72],8],
 ["Tack, Harness, and Drawn Vehicles","/data/4/entries/12/entries/5",[71,72],15],
 ["Waterborne Vehicles","/data/4/entries/12/entries/6",[72],6],
 ["Food, Drink, and Lodging","/data/4/entries/14/entries/3/entries/1",[73,74],24]
 ];
 const slug=s=>s.normalize("NFKC").replace(/[’‘]/g,"'").toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const items=existing.filter(x=>["item","baseitem","item-group"].includes(x.kind));
 const nameMap=new Map();
 for(const r of items){const key=slug(r.name);nameMap.set(key,[...(nameMap.get(key)??[]),r]);}
 const resolve=raw=>{
  const [name,source]=raw.split("|");if(source?.toLowerCase()!=="srd51")throw Error("Foreign equipment "+raw);
  let hits=nameMap.get(slug(name))??[];
  // The priced healing potion is the concrete ordinary potion, not its family.
  if(slug(name)==="potion-of-healing")hits=hits.filter(x=>x.id==="item.potion-of-healing");
  if(hits.length!==1)throw Error("Nonunique equipment "+raw+" "+hits.map(x=>x.id));return hits[0];
 };
 const display=s=>s.replace(/\{@item ([^}|]+)\|[^}|]+(?:\|([^}]+))?\}/g,(_,name,label)=>label??name).replace(/\{@i ([^}]+)\}/g,"$1");
 const norm=s=>s.toLowerCase().replace(/[’‘]/g,"'").replaceAll("½","1/2").replaceAll("¼","1/4").replaceAll("¾","3/4").replace(/[\u00ad\u2010-\u2015]/g,"-").replace(/-+/g,"-").replace(/\s/g,"");
 const equal=(actual,expected,where)=>{if(typeof expected==="number"?typeof actual!=="number"||Math.abs(actual-expected)>1e-8:actual!==expected)throw Error(where+": "+actual+" != "+expected);};
 const money=s=>{const m=s.match(/^([\d,]+) (cp|sp|gp)$/i);if(!m)throw Error("Unparsed price "+s);return Number(m[1].replaceAll(",",""))*({cp:1,sp:10,gp:100}[m[2].toLowerCase()]);};
 const number=s=>{s=s.replaceAll(",","").replace(/ \(full\)$/,"").replace(/ (lb\.|mph|ft\.)$/,"").trim();if(s==="—")return 0;if(s==="1/2")return .5;if(s==="1/4")return .25;const m=s.match(/^(\d+(?:\.\d+)?)?\s*([½¼¾])?$/);if(!m||!m[1]&&!m[2])throw Error("Unparsed numeric cell "+s);return Number(m[1]??0)+({"½":.5,"¼":.25,"¾":.75}[m[2]]??0);};
 const propCodes={"ammunition":"A","finesse":"F","heavy":"H","light":"L","loading":"LD","reach":"R","special":"S","thrown":"T","two-handed":"2H","versatile":"V"};
 for(const [name,pointer,nums,count] of selections){
  const node=pointer.split("/").slice(1).reduce((o,k)=>o[k],book),id="table."+slug(name);
  if(node.type!=="table"||node.caption!==name||node.rows.length!==count)throw Error("Legacy equipment table identity "+name);
  const publisher=pages.filter(p=>nums.includes(p.page)).map(p=>p.textLayout);
  if(publisher.length!==nums.length||!publisher.some(p=>norm(p).includes(norm(name))))throw Error("Publisher table heading "+name);
  const refs=new Set(nums.map(p=>"rulebook.page-"+String(p).padStart(3,"0")));
  let tableRows=JSON.parse(JSON.stringify(node.rows)),category=null;
  if(name==="Adventuring Gear"){
   // The structured source places several tool kits in this table and repeats
   // the healer's kit. The publisher lists only the three gear kits here.
   const expected={60:"climber's kit",61:"disguise kit",62:"forgery kit",63:"herbalism kit",64:"healer's kit",65:"mess kit",66:"poisoner's kit"};
   for(const [i,label] of Object.entries(expected))if(!tableRows[i][0].startsWith("{@item "+label+"|"))throw Error("Unexpected kit source ordering");
   tableRows[60][0]="{@item climber's kit|srd51|Climber's kit}";
   tableRows[65][0]="{@item mess kit|srd51|Mess kit}";
   tableRows[57][0]="{@item Ink (1-ounce bottle)|srd51|Ink (1 ounce bottle)}";
   const old=tableRows;
   tableRows=[...old.slice(0,33),old[60],...old.slice(33,60),...old.slice(67,74),old[65],...old.slice(74)];
   report.corrections.push({id,reason:"Restore published p.69 gear ordering and labels; move climber's and mess kits, remove duplicate healer's kit and four tool-only kit rows; retain their canonical definitions and Tools table memberships.",pages:[69]});
  }
  if(name==="Tools"){
   if(!tableRows[22][0].includes("Dragonchess set")||!tableRows[24][0].includes("Three-Dragon Ante set"))throw Error("Unexpected excluded gaming rows");
   tableRows=tableRows.filter((_,i)=>i!==22&&i!==24);
   tableRows[5][1]="15 gp";
   const replaceHeading=(from,to)=>{const r=tableRows.find(x=>x[0]===from);if(!r)throw Error("Missing heading "+from);r[0]=to;};
   replaceHeading("{@i Gaming sets}","{@i Gaming set}");
   replaceHeading("{@i Musical instruments}","{@i Musical instrument}");
   report.corrections.push({id,reason:"Exclude two gaming-set rows absent from the official Tools table; restore singular headings and remove stray punctuation in cartographer's tools price. Retain the publisher's vehicle-proficiency footnote.",pages:[70]});
  }
  if(name==="Tack, Harness, and Drawn Vehicles"){
   if(!tableRows[0][0].includes("Barding")||tableRows[5][0]!=="Animal Feed (per day)")throw Error("Unexpected tack source");
   tableRows[0][0]="Barding";
   tableRows[5][0]="{@item Feed (per day)|srd51}";
   report.corrections.push({id,reason:"Use published Feed (per day) label and link the existing item; Barding is an armor price/weight multiplier, not a standalone equipment identity.",pages:[71,72]});
  }
  for(const raw of tableRows){
   const row=Array.isArray(raw)?raw:raw.row;
   if(!Array.isArray(row)||row.length!==node.colLabels.length||row.some(x=>typeof x!=="string"))throw Error("Invalid legacy equipment row "+name);
   const visible=row.map(display);
   if(!publisher.some(p=>norm(p).includes(norm(visible.join(" ")))))throw Error("Publisher row mismatch "+name+" "+JSON.stringify(visible));
   const links=[...row[0].matchAll(/\{@item ([^}]+)\}/g)].map(m=>resolve(m[1]));
   for(const link of links)refs.add(link.id);
   if(row.slice(1).every(v=>v==="")){
    if(!row[0].startsWith("{@i"))throw Error("Unexpected header "+row[0]);
    if(name==="Armor"||name==="Weapons")category=visible[0];
    continue;
   }
   if(!links.length){
    if(name==="Tools"&&row[0]==="Vehicles (land or water)"){refs.add("table.tack-harness-and-drawn-vehicles");refs.add("table.waterborne-vehicles");continue;}
    if(name==="Tack, Harness, and Drawn Vehicles"&&row[0]==="Barding"){
     equal(row[1],"×4","Barding price");equal(row[2],"×2","Barding weight");refs.add("table.armor");continue;
    }
    if(name==="Tack, Harness, and Drawn Vehicles"&&row[0]==="Stabling (per day)"||name==="Food, Drink, and Lodging")continue;
    throw Error("Unexpected unlinked legacy equipment "+name+" "+row[0]);
   }
   // Shared published rows (donkey/mule, flask/tankard, jug/pitcher) must
   // agree for each of the linked concrete definitions.
   for(const item of links){
    if(name==="Armor"){
     const type=category==="Shield"?"S":category.startsWith("Light")?"LA":category.startsWith("Medium")?"MA":"HA";
     equal(item.type,type,item.id+" category");
     const ac=type==="S"?"+"+item.ac:type==="LA"?item.ac+" + Dex modifier":type==="MA"?item.ac+" + Dex modifier (max 2)":String(item.ac);
     equal(row[2],ac,item.id+" AC");equal(row[3],item.strength?"Str "+item.strength:"—",item.id+" Strength");equal(row[4],item.stealth?"Disadvantage":"—",item.id+" Stealth");
    }
    if(name==="Weapons"){
     equal(item.weaponCategory,category.startsWith("Simple")?"simple":"martial",item.id+" category");
     equal(item.type,category.includes("Melee")?"M":"R",item.id+" type");
     const damage=row[2].match(/^(\d+(?:d\d+)?) (bludgeoning|piercing|slashing)$/);
     if(damage){equal(String(item.dmg1),damage[1],item.id+" damage");equal(item.dmgType,damage[2][0].toUpperCase(),item.id+" damage type");}
     else if(row[2]!=="—"||item.dmg1!==undefined||item.dmgType!==undefined)throw Error("Unexpected weapon damage "+item.id);
     const props=row[4]==="—"?[]:row[4].split(/,\s*/).map(p=>{
      const code=propCodes[p.split(" (")[0].toLowerCase()];if(!code)throw Error("Unknown weapon property "+p);refs.add("item-property."+slug(code));return code;
     });
     equal(JSON.stringify([...(item.property??[])].sort()),JSON.stringify(props.sort()),item.id+" properties");
     equal(item.range??"",row[4].match(/range (\d+\/\d+)/i)?.[1]??"",item.id+" range");
     equal(item.dmg2??"",row[4].match(/versatile \((\d+d\d+)\)/i)?.[1]??"",item.id+" versatile");
    }
    for(let i=1;i<row.length;i++){
     const label=node.colLabels[i],v=row[i],context=item.id+" "+label;
     if(label==="Cost")equal(item.value,money(v),context);
     else if(label==="Weight")equal(item.weight??0,number(v),context);
     else if(label==="Carrying Capacity")equal(item.carryingCapacity,number(v),context);
     else if(label==="Speed")equal(name==="Waterborne Vehicles"?item.vehSpeed:item.speed,number(v),context);
     else if(name==="Armor"&&["Armor Class (AC)","Strength","Stealth"].includes(label)||name==="Weapons"&&["Damage","Properties"].includes(label))continue;
     else throw Error("Unreviewed legacy field "+label);
    }
   }
  }
  const record={id,kind:"table",name,colLabels:JSON.parse(JSON.stringify(node.colLabels)),rows:tableRows};
  if(node.footnotes)record.footnotes=JSON.parse(JSON.stringify(node.footnotes));
  record.catalogReferences=[...refs].sort();record.source=metadata(pointer,nums);
  rows.push(record);report.tables.push({id,pointer,page:nums[0],rowCount:tableRows.length,...(nums.length>1?{pages:nums}:{}),...(tableRows.length!==count?{sourceRowCount:count}:{})});
 }
}

function legacyMagicItemEntries(name,entries){
 const replace=(s,a,b)=>{if(!s.includes(a))throw Error("Legacy magic item source changed: "+a);return s.replace(a,b);};
 const child=n=>{const result=entries.find(x=>x?.name===n);if(!result)throw Error("Missing magic item subsection "+n);return result.entries;};
 if(name==="Attunement"){
  entries[0]=replace(entries[0],"If the prerequisite is to be a spellcaster, a creature qualifies if it can cast at least one spell using its traits or features, not using a magic item or the like. (If the class is a spellcasting class, a monster qualifies if that creature has spell slots and uses that class's spell list.)","If the prerequisite is a class, a creature must be a member of that class to attune to the item. (If the class is a spellcasting class, a monster qualifies if it has spell slots and uses that class's spell list.) If the prerequisite is to be a spellcaster, a creature qualifies if it can cast at least one spell using its traits or features, not using a magic item or the like.");
  entries[3]=replace(entries[3],"end its attunement to an item first","end its attunement to an item first");
  entries[4]=replace(entries[4],"A creature can also voluntarily","A creature can also voluntarily");
 }
 if(name==="Wearing and Wielding Items"){
  entries[0]=replace(entries[0],"A weapon must be held in hand.","A weapon must be held.");
  entries[2]=replace(entries[2],"armor made by the drow might fit elves only. Dwarves might make items usable only by dwarf-sized and dwarf-shaped characters.","drow-made armor might fit elves only. Dwarves might make items usable only by dwarf-sized and dwarf-shaped folk.");
  entries[3]=replace(entries[3],"non-humanoid","nonhumanoid");
  entries[3]=replace(entries[3],"has no way to wear magic boots.","can't wear boots.");
  const multi=child("Multiple Items of the Same Kind");
  multi[0]=replace(multi[0],"or be able to layer two cloaks","or to layer two cloaks");
  const paired=child("Paired Items");
  paired[0]=replace(paired[0],"pairs-such as boots, bracers, gauntlets, and gloves-impart","pairs—such as boots, bracers, gauntlets, and gloves—impart");
  paired[0]=replace(paired[0],"boot of elven kind","boot of elvenkind");
  paired[0]=replace(paired[0],"from either item.","from either.");
 }
 if(name==="Activating an Item"){
  entries[0]=replace(entries[0],"something in particular, such as holding the item and uttering a command word, reading the item if it is a scroll, or drinking it if it is a potion.","something special, such as holding the item and uttering a command word.");
  entries[1]=replace(entries[1],"{@action Use an Item|srd51}","Use an Item");
  const command=child("Command Word");
  command[0]=replace(command[0],"must be spoken audibly for the item to operate. A magic item that requires the user to speak a command word can't be activated in the area of any effect that prevents sound, such as the area created by the silence spell.","must be spoken for an item to work. A magic item that requires a command word can't be activated in an area where sound is prevented, as in the area of the silence spell.");
  const consumables=child("Consumables");
  consumables[0]=replace(consumables[0],"loses its magic and no longer functions.","loses its magic.");
  const spells=child("Spells");
  spells[0]=replace(spells[0],"from the item, often by expending charges from it. The spell is cast at the lowest possible spell and caster level,","from the item. The spell is cast at the lowest possible spell level,");
  spells[0]=replace(spells[0],"requires no components unless","requires no components, unless");
  spells[0]=replace(spells[0]," Certain items make exceptions to these rules, changing the casting time, duration, or other parts of a spell."," Many items, such as potions, bypass the casting of a spell and confer the spell's effects, with their usual duration. Certain items make exceptions to these rules, changing the casting time, duration, or other parts of a spell.");
  spells.splice(1,1);
  spells[1]=replace(spells[1],"and your proficiency bonus does apply.","and your proficiency bonus does apply.");
  const charges=child("Charges");
  charges[0]=replace(charges[0],"have charges that you expend","have charges that must be expended");
  charges[0]=replace(charges[0],"or when a creature attunes to the item","as well as when a creature attunes to it");
 }
 return entries;
}

function modernMagicItemEntries(name,entries){
 if(name==="Magic Item Rarity"){
  const values=entries.find(x=>x?.name==="Magic Item Values by Rarity").entries;
  const old="such as the City of Brass or Sigil";
  if(!values[0].includes(old))throw Error("Unexpected rarity location examples");
  values[0]=values[0].replace(old,"such as a city on another plane of existence");
  values[1]=values[1].replace("{@item +1 Plate Armor|srd52|+1 Armor (Plate Armor)}","+1 Armor (Plate Armor)");
 }
 return entries;
}

function legacyGameplayEntries(name,entries){
 const find=(arr,name)=>{for(const n of arr){if(n&&typeof n==="object"){if(n.name===name)return n;if(n.entries){const v=find(n.entries,name);if(v)return v;}}}};
 const replace=(s,a,b)=>{if(!s.includes(a))throw Error("Changed source: "+a);return s.replace(a,b);};
 if(name==="Movement"){
  entries[0]=replace(entries[0],"mountain slope all sorts","mountain slope—all sorts");
  const travel=find(entries,"Travel Pace");
  travel.entries[0]=replace(travel.entries[0],' (see the "Activity While Traveling" section later in this chapter for more information)',"");
  travel.entries[2]=replace(travel.entries[2],"(see the appendix)","(see appendix PH-A)");
  const mounts=find(entries,"Mounts and Vehicles");
  mounts.entries[2]=replace(mounts.entries[2]," The GM's Guide contains more information on special methods of travel.","");
  const jump=find(entries,"High Jump");
  jump.entries[0]=replace(jump.entries[0]," (minimum of 0 feet)","");
 }
 if(name==="Between Adventures"){
  const lifestyle=find(entries,"Lifestyle Expenses");lifestyle.entries[0]=replace(lifestyle.entries[0],"lifestyle,.","lifestyle.");
  const training=find(entries,"Training");training.entries[0]=replace(training.entries[0],"Your DM","Your GM");
 }
 if(name==="What Is a Spell?"){
  entries[1]=replace(entries[1],"(see appendix A)","(see appendix PH-A)");
  const level=find(entries,"Spell Level");level.entries[0]=replace(level.entries[0],"incredible time stop","earth-shaking wish");
  const prepared=find(entries,"Known and Prepared Spells");prepared.entries[0]=replace(prepared.entries[0],"Members of a few classes have","Members of a few classes, including bards and sorcerers, have");prepared.entries[0]=replace(prepared.entries[0],"magic using monsters","magic-using monsters");
  const slots=find(entries,"Spell Slots");slots.entries[0]=replace(slots.entries[0],"each spellcasting class's description includes","each spellcasting class's description (except that of the warlock) includes");
 }
 return entries;
}

function legacySkillList(table,existing,local){
 if(table.caption||table.colLabels.join("|")!=="Ability|Skills"||table.rows.length!==5)throw Error("Unexpected legacy skill grouping");
 const expected=new Map([["Strength","str"],["Dexterity","dex"],["Intelligence","int"],["Wisdom","wis"],["Charisma","cha"]]);
 const seen=new Set(),items=[];
 for(const [ability,labels] of table.rows){
  const abilityId=expected.get(ability);
  if(!abilityId)throw Error("Unexpected skill ability "+ability);
  const names=[...labels.matchAll(/\{@skill ([^}]+)\}/g)].map(m=>m[1]);
  if(!names.length)throw Error("Empty skill group");
  for(const name of names){
   const skill=existing.find(x=>x.kind==="skill"&&x.name===name);
   if(!skill||skill.ability!==abilityId||seen.has(skill.id))throw Error("Unreconciled skill "+name);
   seen.add(skill.id);local.add(skill.id);
  }
  items.push({type:"item",name:ability,entry:labels});
 }
 if(seen.size!==18||existing.filter(x=>x.kind==="skill").some(x=>!seen.has(x.id)))throw Error("Incomplete skill grouping");
 return {type:"list",style:"list-hang-notitle",items};
}

function legacyAdditionalEntries(name,entries){
 const find=(arr,name)=>{for(const n of arr){if(n&&typeof n==="object"){if(n.name===name)return n;if(n.entries){const v=find(n.entries,name);if(v)return v;}}}};
 const replace=(s,a,b)=>{if(!s.includes(a))throw Error("Changed source: "+a);return s.replace(a,b);};
 if(name==="Ability Checks"){
  const skill=find(entries,"Skills");skill.entries[2]=replace(skill.entries[2],"later sections of this chapter","later sections of this section");
  const variant=find(entries,"Variant: Skills with Different Abilities");variant.entries[1]=replace(variant.entries[1],"dwarf fighter","half-orc barbarian");
  const passive=find(entries,"Passive Checks");if(passive.entries[2].type!=="abilityGeneric")throw Error("Unexpected passive check formula");passive.entries[2]=passive.entries[2].text;
 }
 if(name==="The Environment"){
  entries[0]=replace(entries[0]," The GM's Guide has rules covering more unusual situations.","");
  const vision=find(entries,"Vision and Light");vision.entries[2]=replace(vision.entries[2],"{@condition blinded} condition","{@condition blinded} condition (see appendix PH-A)");
  const food=find(entries,"Food and Water");food.entries[0]=replace(food.entries[0],"(see the appendix)","(see appendix PH-A)");
  const objects=find(entries,"Interacting with Objects");objects.entries[0]=replace(objects.entries[0],"such a moving","such as moving");objects.entries[0]=replace(objects.entries[0],"if anything happens","if anything, happens");
 }
 if(name==="The Order of Combat"){
  const init=find(entries,"Initiative");init.entries[2]=replace(init.entries[2],"DM-controlled","GM-controlled");
  const turn=find(entries,"Your Turn");turn.entries[1]=replace(turn.entries[1]," section later in this chapter"," section");turn.entries[2]=replace(turn.entries[2]," section later in this chapter"," section");
  const reactions=find(entries,"Reactions");reactions.entries[0]=replace(reactions.entries[0],", described later in this chapter,","");
 }
 return entries;
}

function legacyCombatAndCastingEntries(name,entries){
 const find=(arr,name)=>{for(const n of arr){if(n&&typeof n==="object"){if(n.name===name)return n;if(n.entries){const v=find(n.entries,name);if(v)return v;}}}};
 const replace=(s,a,b)=>{if(!s.includes(a))throw Error("Changed source: "+a);return s.replace(a,b);};
 if(name==="Movement and Position"){
  const prone=find(entries,"Being Prone");prone.entries[0]=replace(prone.entries[0],"described in the appendix","described in appendix PH-A");
  const moving=find(entries,"Moving around Other Creatures");moving.entries[2]=replace(moving.entries[2],", as explained later in the chapter","");
  const space=find(entries,"Space");space.entries[3]=replace(space.entries[3],"four Large","five Large");
 }
 if(name==="Making an Attack"){
  const modifier=find(entries,"Ability Modifier");modifier.entries[1]=replace(modifier.entries[1],', as explained in "Spellcasting"',"");
  const roll=find(entries,"Rolling 1 or 20");roll.entries[1]=replace(roll.entries[1],", which is explained later in this chapter","");
  const options=entries.at(-1);
  const refs={"Opportunity Attacks":"See Opportunity Attack.","Two-Weapon Fighting":"See Two-Weapon Fighting.","Grappling":"See Grapple, including moving a grappled creature, and Escape a Grapple.","Shoving a Creature":"See Shove."};
  options.entries=options.entries.map(x=>Object.hasOwn(refs,x.name)?{type:"entries",name:x.name,entries:[refs[x.name]]}:x);
 }
 if(name==="Damage and Healing"){
  const damage=find(entries,"Damage Rolls");
  damage.entries[0]+=" With a penalty, it is possible to deal 0 damage, but never negative damage.";
  damage.entries[1]=replace(damage.entries[1],"used for the attack roll to the damage","used for the attack roll—to the damage");
  const unconscious=find(entries,"Falling Unconscious");unconscious.entries[0]=replace(unconscious.entries[0],"(see the appendix)","(see appendix PH-A)");
  const stabilize=find(entries,"Stabilizing a Creature");stabilize.entries=[stabilize.entries[0],"See Stabilize a Creature."];
 }
 if(name==="Casting a Spell"){
  const range=find(entries,"Range");range.entries[2]=replace(range.entries[2],' later in the this chapter',"");
  const areas=find(entries,"Areas of Effect");areas.entries[1]=replace(areas.entries[1],', as explained in "Combat"',"");
  const schools=find(entries,"The Schools of Magic");
  schools.entries.unshift("Academies of magic group spells into eight categories called schools of magic. Scholars, particularly wizards, apply these categories to all spells, believing that all magic functions in essentially the same way, whether it derives from rigorous study or is bestowed by a deity.","The schools of magic help describe spells; they have no rules of their own, although some rules refer to the schools.");
  const combine=find(entries,"Combining Magical Effects");combine.entries[0]=replace(combine.entries[0],", or the most recent effect applies if the castings are equally potent and their durations overlap","");
 }
 return entries;
}

function legacyAbilityAndOriginsEntries(name,entries,existing,local){
 const find=(arr,name)=>{for(const n of arr){if(n&&typeof n==="object"){if(n.name===name)return n;if(n.entries){const v=find(n.entries,name);if(v)return v;}}}};
 const replace=(s,a,b)=>{if(!s.includes(a))throw Error("Changed source: "+a);return s.replace(a,b);};
 if(name==="Using Each Ability"){
  const seen=new Set(),norm=x=>JSON.stringify(x).replace(/\{@skill ([^}]+)\}/g,"$1");
  for(const ability of entries.slice(1)){
   const checks=find(ability.entries,ability.name+" Checks");
   const projected=[];
   for(let i=0;i<checks.entries.length;i++){
    const entry=checks.entries[i],skill=existing.find(x=>x.kind==="skill"&&x.name===entry?.name);
    if(!skill){projected.push(entry);continue;}
    const body=entry.name==="Perception"?[...entry.entries,checks.entries[++i]]:entry.entries;
    if(norm(body)!==JSON.stringify(skill.entries)||seen.has(skill.id))throw Error("Unreconciled ability application "+skill.id);
    seen.add(skill.id);local.add(skill.id);
    projected.push({type:"entries",name:entry.name,entries:["See the "+entry.name+" skill."]});
   }
   checks.entries=projected;
  }
  if(seen.size!==18)throw Error("Incomplete ability skill references");
  const vision=find(entries,"What Can you See?");vision.name="What Can You See?";
  vision.entries[0]=replace(vision.entries[0],"{@quickref Vision and Light|srd51|4||heavily obscured}.",'heavily obscured, as explained in "The Environment".');
 }
 if(name==="Alignment"){
  const multiverse=find(entries,"Alignment in the Multiverse");
  multiverse.entries=[
   "For many thinking creatures, alignment is a moral choice. Humans, dwarves, elves, and other humanoid races can choose whether to follow the paths of good or evil, law or chaos. According to myth, the good-aligned gods who created these races gave them free will to choose their moral paths, knowing that good without free will is slavery.",
   "The evil deities who created other races, though, made those races to serve them. Those races have strong inborn tendencies that match the nature of their gods. Most orcs share the violent, savage nature of the orc gods, and are thus inclined toward evil. Even if an orc chooses a good alignment, it struggles against its innate tendencies for its entire life. (Even half-orcs feel the lingering pull of the orc god's influence.)",
   "Alignment is an essential part of the nature of celestials and fiends. "+multiverse.entries[3],
   multiverse.entries[4]
  ];
 }
 if(name==="Languages")entries[1]="Choose your languages from the Standard Languages table, or choose one that is common in your campaign. "+entries[1];
 if(name==="Inspiration"){
  const gain=find(entries,"Gaining Inspiration");gain.entries[0]=gain.entries[0].replaceAll("Your DM","Your GM");
 }
 if(name==="Backgrounds"){
  entries.splice(2,0,"The sample background presented here provides both concrete benefits (features, proficiencies, and languages) and roleplaying suggestions.");
  const prof=find(entries,"Proficiencies");prof.entries[0]='Each background gives a character proficiency in two skills (described in "Using Ability Scores").';prof.entries[1]='In addition, most backgrounds give a character proficiency with one or more tools (detailed in "Equipment").';
  const lang=find(entries,"Languages");lang.entries[0]=replace(lang.entries[0]," earlier in this chapter","");
  const eq=find(entries,"Equipment");eq.entries[0]=replace(eq.entries[0]," from chapter 5","");
  const custom=find(entries,"Customizing a Background");custom.entries[0]=replace(custom.entries[0],"as described in chapter 5","as described in the equipment section");
  if(entries.at(-1).items?.[0]!=="{@background Acolyte|srd51}")throw Error("Unexpected background index");
  entries[entries.length-1]="See the Acolyte background.";
 }
 return entries;
}

function legacyEconomyEntries(name,entries){
 const find=(arr,name)=>{for(const n of arr){if(n&&typeof n==="object"){if(n.name===name)return n;if(n.entries){const v=find(n.entries,name);if(v)return v;}}}};
 const replace=(s,a,b)=>{if(!s.includes(a))throw Error("Changed source: "+a);return s.replace(a,b);};
 if(name==="Mounts and Vehicles"){
  const barding=find(entries,"Barding");barding.entries[0]=replace(barding.entries[0],"Armor table in this chapter","Armor table");
 }
 if(name==="Expenses"){
  const spells=find(entries,"Spellcasting Services");
  spells.entries[1]=replace(spells.entries[1],"{@filter 1st|spells|level=1} or {@filter 2nd|spells|level=2}","1st or 2nd");
 }
 return entries;
}

function legacyEquipmentEntries(name,entries,existing,local){
 const find=(arr,name)=>{for(const n of arr){if(n&&typeof n==="object"){if(n.name===name)return n;if(n.entries){const v=find(n.entries,name);if(v)return v;}}}};
 const lookup=id=>{const row=existing.find(x=>x.id===id);if(!row)throw Error("Missing equipment identity "+id);local.add(id);return row;};
 const link=(entry,id)=>{lookup(id);return {type:"entries",name:entry.name,entries:["See the "+entry.name+" catalog definition."]};};
 if(name==="Armor"){
  const aliases={Padded:"padded-armor",Leather:"leather-armor","Studded Leather":"studded-leather-armor",Hide:"hide-armor","Chain Shirt":"chain-shirt","Scale Mail":"scale-mail",Breastplate:"breastplate","Half Plate":"half-plate-armor","Ring Mail":"ring-mail","Chain Mail":"chain-mail",Splint:"splint-armor",Plate:"plate-armor"};
  let count=0;
  const visit=x=>{if(Array.isArray(x))return x.map(visit);if(x&&typeof x==="object"){if(Object.hasOwn(aliases,x.name)){const row=lookup("baseitem."+aliases[x.name]);if(JSON.stringify(row.entries)!==JSON.stringify(x.entries))throw Error("Unreconciled armor description "+x.name);count++;return link(x,row.id);}if(x.entries)x.entries=visit(x.entries);}return x;};
  entries=visit(entries);if(count!==12)throw Error("Incomplete armor index");
 }
 if(name==="Weapons"){
  const properties=find(entries,"Weapon Properties");
  const ids={Ammunition:"a",Finesse:"f",Heavy:"h",Light:"l",Loading:"ld",Reach:"r",Thrown:"t","Two-Handed":"2h",Versatile:"v"};
  properties.entries[1].entries=properties.entries[1].entries.map(x=>Object.hasOwn(ids,x.name)?link(x,"item-property."+ids[x.name]):x);
  const special=find(entries,"Special Weapons");special.entries=special.entries.map(x=>typeof x==="string"?x:link(x,"baseitem."+x.name.toLowerCase()));
  lookup("item-property.s");
 }
 if(name==="Adventuring Gear"){
  const aliases={"Acid":"acid-vial","Alchemist's Fire":"alchemist-s-fire-flask","Antitoxin":"antitoxin-vial","Ball Bearings":"ball-bearings-bag-of-1-000","Block and Tackle":"block-and-tackle","Book":"book","Caltrops":"caltrops-bag-of-20","Candle":"candle","Case, Crossbow Bolt":"crossbow-bolt-case","Case, Map or Scroll":"map-or-scroll-case","Chain":"chain-10-feet","Climber's Kit":"climber-s-kit","Component Pouch":"component-pouch","Crowbar":"crowbar","Fishing Tackle":"fishing-tackle","Healer's Kit":"healer-s-kit","Holy Water":"holy-water-flask","Hunting Trap":"hunting-trap","Lamp":"lamp","Lantern, Bullseye":"bullseye-lantern","Lantern, Hooded":"hooded-lantern","Lock":"lock","Magnifying Glass":"magnifying-glass","Manacles":"manacles","Mess Kit":"mess-kit","Oil":"oil-flask","Poison, Basic":"basic-poison-vial","Potion of Healing":"potion-of-healing","Pouch":"pouch","Quiver":"quiver","Ram, Portable":"portable-ram","Rations":"rations-1-day","Scale, Merchant's":"merchant-s-scale","Spellbook":"spellbook","Spyglass":"spyglass","Tent":"two-person-tent","Tinderbox":"tinderbox","Torch":"torch"};
  const group={"Arcane Focus":"arcane-focus","Druidic Focus":"druidic-focus","Holy Symbol":"holy-symbol"};
  const descriptions=entries[1].entries[0].entries;
  if(descriptions.length!==42)throw Error("Unexpected gear index size "+descriptions.length);
  entries[1].entries[0].entries=descriptions.map(x=>{
   if(x.name==="Rope"){lookup("item.hempen-rope-50-feet");lookup("item.silk-rope-50-feet");return {type:"entries",name:x.name,entries:["See Hempen Rope (50 feet) and Silk Rope (50 feet)."]};}
   if(Object.hasOwn(group,x.name))return link(x,"item-group."+group[x.name]);
   if(!Object.hasOwn(aliases,x.name))throw Error("Unknown gear entry "+x.name);
   const id="item."+aliases[x.name];
   if(x.name==="Potion of Healing"){lookup(id);return {type:"entries",name:x.name,entries:["See Potion of Healing. Drinking or administering a potion takes an action."]};}
   return link(x,id);
  });
  const packs=find(entries,"Equipment Packs");const packList=packs.entries[1].entries[0].entries;
  if(packList.length!==7)throw Error("Unexpected pack list");
  packs.entries[1].entries[0].entries=packList.map(x=>link(x,"item."+x.name.replace(/ \(\d+ gp\)$/,"").toLowerCase().replace(/[^a-z0-9]+/g,"-")));
 }
 if(name==="Tools"){
  const list=entries[2].entries[0].entries;
  const aliases={"Disguise Kit":"disguise-kit","Forgery Kit":"forgery-kit","Herbalism Kit":"herbalism-kit","Navigator's Tools":"navigator-s-tools","Poisoner's Kit":"poisoner-s-kit","Thieves' Tools":"thieves-tools"};
  const groups={"Artisan's Tools":"artisan-s-tools","Gaming Set":"gaming-set","Musical Instrument":"musical-instrument"};
  entries[2].entries[0].entries=list.map(x=>{
   if(Object.hasOwn(groups,x.name)){lookup("item-group."+groups[x.name]);if(x.name==="Gaming Set")x.entries[0]=x.entries[0].replace(" (for games such as Three-Dragon Ante)","");return x;}
   if(!Object.hasOwn(aliases,x.name))throw Error("Unknown tool "+x.name);
   return link(x,"item."+aliases[x.name]);
  });
 }
 return entries;
}

function generateBookRules(book,manifest,src,pages,tag,known,existing){
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
  if(t.type!=="table"||(name==="Glossary Abbreviations"?t.caption!==undefined:t.caption!==name)||t.rows.length!==count)throw Error("Unexpected table "+pointer);
  const pageText=norm(pageMap.get(page).textLayout);
  // Split headings in the PDF may have interleaved second-column text.
  const heading=name==="Glossary Abbreviations"?"Abbreviations":name.split(":")[0];
  if(!pageText.includes(norm(heading)))throw Error("Missing publisher heading "+name+" on "+page);
  const cols=t.colLabels??t.colLabelRows?.at(-1)??(name==="Glossary Abbreviations"?["Abbreviation","Meaning"]:null);
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
  // Preserve published examples while replacing commercial navigation and
  // display-only aliases with their visible labels; links use known local IDs.
  const displayOnly=x=>x.replace(/\{@(?:book|table|item) ([^}|]+)(?:\|([^}]*))?\}/g,(all,label,rest)=>{
   const bits=(rest??"").split("|");
   return all.startsWith("{@book ")&&bits.length>2?bits.at(-1):all.startsWith("{@table ")&&bits.length>1?bits.at(-1):all.startsWith("{@item ")&&bits.length>1?bits.at(-1):label;
  });
  if(["Magic Item Categories","Potion Miscibility","Magic Item Tools","Magic Item Crafting Time and Cost"].includes(name)){
   r.rows=r.rows.map(row=>row.map(cell=>typeof cell==="string"?displayOnly(cell):cell));
   if(r.footnotes)r.footnotes=r.footnotes.map(displayOnly);
  }
  if(name==="Glossary Abbreviations"){
   // The source uses DM/GM; the published SRD defines GM/Game Master.
   r.rows=r.rows.map(row=>row[0]==="DM"?["GM","Game Master"]:row);
   for(const id of ["status.concentration","glossary.challenge-rating","glossary.difficulty-class","glossary.hit-points","glossary.proficiency","glossary.ritual","glossary.experience-points"])refs.add(id);
  }
  if(name==="Actions"){
   for(const action of ["attack","dash","disengage","dodge","help","hide","influence","magic","ready","search","study","utilize"])refs.add("action."+action);
  }
  if(name==="Magic Item Categories"){
   for(const id of ["item.potion-of-healing","item.ring-of-invisibility","item.immovable-rod","item-group.spell-scroll","item.staff-of-striking","item.wand-of-fireballs","item.bag-of-holding","item.boots-of-elvenkind"])refs.add(id);
   // Numbered example labels are retained without guessing template identities.
  }
  if(name==="Potion Miscibility")for(const id of ["glossary.sphere-area-of-effect","item.potion-of-healing","item.potion-of-invisibility","condition.invisible","spell.dispel-magic"])refs.add(id);
  if(name==="Magic Item Tools"){
   for(const tool of ["leatherworker-s-tools","smith-s-tools","weaver-s-tools","alchemist-s-supplies","jeweler-s-tools","woodcarver-s-tools","calligrapher-s-supplies","tinker-s-tools"])refs.add("baseitem."+tool);
   refs.add("item.herbalism-kit");
  }
  if(name==="Magic Item Crafting Time and Cost"){
   refs.add("item-group.spell-scroll");refs.add("table.spell-scroll-costs");
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
 if(tag==="52")projectModernEquipmentTables(book,existing,pages,metadata,rows,report);
 else projectLegacyEquipmentTables(book,existing,pages,metadata,rows,report);
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
  ["Underwater Combat","/data/8/entries/7",[99],[]],
  ["Attunement","/data/12/entries/1",[206],["rule.resting","item.ring-of-protection"]],
  ["Wearing and Wielding Items","/data/12/entries/2",[206],[]],
  ["Activating an Item","/data/12/entries/3",[206,207],["action.use-an-object","spell.silence","spell.identify","status.concentration","rule.attunement"]],
  ["Ability Scores and Modifiers","/data/6/entries/4",[76],[]],
  ["Time","/data/7/entries/0",[84],[]],
  ["Movement","/data/7/entries/1",[84,85],["condition.exhaustion","condition.prone","skill.perception","skill.athletics","skill.acrobatics","rule.saving-throws"]],
  ["Between Adventures","/data/7/entries/4",[88,89],["table.lifestyle-expenses","skill.performance","skill.investigation","skill.persuasion","baseitem.plate-armor"]],
  ["What Is a Spell?","/data/9/entries/2",[100,101],["spell.magic-missile","spell.wish","spell.cure-wounds","class.bard","class.sorcerer","class.cleric","class.druid","class.wizard","class.warlock","class.monk","monster.pit-fiend","rule.resting"]],
  ["Ability Checks","/data/6/entries/7",[77,78,79],["rule.ability-check","rule.proficiency-bonus","rule.advantage-and-disadvantage","action.help"]],
  ["The Environment","/data/7/entries/2",[86,87],["condition.prone","condition.blinded","condition.invisible","condition.exhaustion","skill.perception","sense.blindsight","sense.darkvision","sense.truesight","rule.breaking-objects","combat-option.stabilize-a-creature"]],
  ["The Order of Combat","/data/8/entries/0",[90,91],["status.surprised","skill.stealth","skill.perception","action.dodge","action.ready","reaction-option.opportunity-attack","activity.other-activity"]],
  ["Actions in Combat","/data/8/entries/2",[93,94],["action.attack","action.cast-a-spell","action.dash","action.disengage","action.dodge","action.help","action.hide","action.ready","action.search","action.use-an-object"]],
  ["Movement and Position","/data/8/entries/1",[91,92],["condition.prone","spell.fly","rule.movement","reaction-option.opportunity-attack","activity.other-activity"]],
  ["Making an Attack","/data/8/entries/3",[94,95,96],["rule.cover","rule.advantage-and-disadvantage","rule.proficiency-bonus","rule.ability-checks","condition.incapacitated","spell.invisibility","reaction-option.opportunity-attack","combat-option.two-weapon-fighting","attack-option.grapple","combat-option.escape-a-grapple","attack-option.shove","action.attack"]],
  ["Damage and Healing","/data/8/entries/5",[96,97,98,99],["table.damage-types","condition.unconscious","combat-option.stabilize-a-creature","skill.medicine","spell.cure-wounds","spell.revivify","rule.resting"]],
  ["Casting a Spell","/data/9/entries/3",[101,102,103,104],["rule.what-is-a-spell","rule.cover","rule.proficiency-bonus","rule.saving-throws","rule.making-an-attack","status.concentration","activity.end-concentration","condition.incapacitated","spell.silence","spell.bless","spell.burning-hands","spell.cone-of-cold","spell.animate-dead"]],
  ["Using Each Ability","/data/6/entries/8",[79,80,81,82,83],["rule.ability-checks","rule.making-an-attack","rule.the-order-of-combat","rule.the-environment","rule.casting-a-spell","table.armor","condition.invisible","class.wizard","class.cleric","class.druid","class.ranger","class.bard","class.paladin","class.sorcerer","class.warlock"]],
  ["Alignment","/data/3/entries/7",[58,59],[]],
  ["Languages","/data/3/entries/8",[59],["language.primordial","language.thieves-cant","language.druidic"]],
  ["Inspiration","/data/3/entries/9",[59,60],["rule.advantage-and-disadvantage","rule.ability-checks","rule.saving-throws","rule.making-an-attack"]],
  ["Backgrounds","/data/3/entries/10",[60],["background.acolyte","rule.languages","rule.ability-checks"]],
  ["Beyond 1st Level","/data/3",[56],["rule.proficiency-bonus","rule.using-each-ability","rule.multiclassing"],[0,6]],
  ["Equipment","/data/4",[62],[],[0,7]],
  ["Selling Treasure","/data/4/entries/7",[62],["table.trade-goods"]],
  ["Mounts and Vehicles","/data/4/entries/12",[71,72],["table.armor","rule.mounted-combat","rule.using-each-ability","rule.movement"]],
  ["Trade Goods","/data/4/entries/13",[72],[]],
  ["Expenses","/data/4/entries/14",[72,73,74],["skill.survival","rule.between-adventures","spell.cure-wounds","spell.identify","spell.secret-chest"]],
  ["Armor","/data/4/entries/8",[62,63,64],["table.donning-and-doffing-armor","equipment-option.don-or-doff-a-shield","skill.stealth"]],
  ["Weapons","/data/4/entries/9",[64,65,66],["rule.making-an-attack","combat-option.two-weapon-fighting","reaction-option.opportunity-attack","condition.restrained"]],
  ["Adventuring Gear","/data/4/entries/10",[66,67,68,69,70],["rule.the-environment","rule.casting-a-spell","item-group.arcane-focus","item-group.druidic-focus","item-group.holy-symbol"]],
  ["Tools","/data/4/entries/11",[70,71],["rule.proficiency-bonus","rule.ability-checks","rule.mounts-and-vehicles","class.bard"]]
 ]:[
  ["Travel Pace","/data/10/entries/0",[192],["table.travel-pace","condition.exhaustion","spell.wind-walk","item-group.carpet-of-flying","glossary.difficult-terrain"]],
  ["Creating a Background","/data/10/entries/1",[192,193],[]],
  ["Combat Encounters","/data/10/entries/7",[202,203],["monster.bugbear-warrior","monster.giant-wasp","monster.giant-rat","monster.druid","monster.stirge","monster.wight","monster.warhorse-skeleton","monster.skeleton","monster.adult-red-dragon","monster.fire-giant","monster.ogre"]],
  ["Crafting Nonmagical Items","/data/6/entries/12",[103],["baseitem.plate-armor","baseitem.heavy-crossbow"]],
  ["Brewing Potions of Healing","/data/6/entries/13",[103],["item.herbalism-kit","item.potion-of-healing"]],
  ["Scribing Spell Scrolls","/data/6/entries/14",[103],["table.spell-scroll-costs","item-group.spell-scroll","skill.arcana","baseitem.calligrapher-s-supplies"]],
  ["Magic Item Rules","/data/11/entries/1",[205],["glossary.attunement"]],
  ["Activating a Magic Item","/data/11/entries/4",[206],["action.magic","spell.silence","spell.identify","item.potion-of-healing","status.concentration","glossary.attunement"]],
  ["\"The Next Dawn\"","/data/11/entries/5",[206],[]],
  ["Cursed Items","/data/11/entries/6",[206],["glossary.attunement","spell.identify","spell.remove-curse"]],
  ["Magic Item Resilience","/data/11/entries/7",[206],["glossary.resistance"]],
  ["Crafting Magic Items","/data/11/entries/8",[206,207],["skill.arcana","rule.brewing-potions-of-healing","rule.scribing-spell-scrolls","rule.crafting-nonmagical-items"]],
  ["Identifying a Magic Item","/data/6/entries/11/entries/1",[102],["spell.identify","glossary.short-rest","item.ring-of-feather-falling","item.ring-of-swimming"]],
  ["Attunement","/data/6/entries/11/entries/2",[102,103],["glossary.attunement","glossary.short-rest","item.ring-of-protection","rule.magic-item-rules","rule.cursed-items"]],
  ["Wearing and Wielding Items","/data/6/entries/11/entries/3",[103],["rule.magic-item-rules","item.boots-of-striding-and-springing","item.boots-of-elvenkind"]],
  ["Magic Item Categories","/data/11/entries/2",[204,205],["glossary.bonus-action","item-group.arcane-focus","item-group.spell-scroll"]],
  ["Magic Item Rarity","/data/11/entries/3",[205,206],["item.potion-of-healing","item.dragon-orb","baseitem.plate-armor","rule.scribing-spell-scrolls"]],
  ["Sentient Magic Items","/data/11/entries/9",[207,208],["condition.charmed","rule.the-next-dawn","rule.attunement"]]
 ];
 const extraCorrections={
  "Armor":"Reuse canonical armor descriptions after exact comparison and retain proficiency, AC, speed, stealth and don/doff rules (pp.62–64).",
  "Weapons":"Reuse canonical weapon properties and special weapon definitions; reconcile original property wording (pp.64–66).",
  "Adventuring Gear":"Use existing item/group/pack identities rather than duplicate their descriptive bodies; retain potion administration action cost (pp.66–70).",
  "Tools":"Reuse canonical concrete tool identities; preserve group proficiency guidance and omit the commercial card-game example (pp.70–71).",
  "Mounts and Vehicles":"Restore publisher barding table navigation (p.71).",
  "Expenses":"Replace source-specific spell filter navigation with published spell-level labels (p.74).",
  "Using Each Ability":"Reuse all 18 canonical skill descriptions after exact normalized body checks; restore publisher environmental navigation (pp.79–83).",
  "Alignment":"Restore publisher-specific fictional multiverse alignment paragraphs and remove later source rewrites (pp.58–59).",
  "Languages":"Restore publisher standard-language selection rule (p.59).",
  "Inspiration":"Restore publisher GM terminology (pp.59–60).",
  "Backgrounds":"Restore publisher introduction and navigation; reference the existing Acolyte definition (p.60).",
  "Movement and Position":"Restore publisher five Large creatures example and appendix/navigation wording (pp.91–92).",
  "Making an Attack":"Use existing canonical attack options, corrected against pp.95–96, instead of duplicating their procedures; restore publisher attack navigation.",
  "Damage and Healing":"Restore the published nonnegative damage floor and appendix label; link canonical stabilization procedure (pp.96–99).",
  "Casting a Spell":"Restore school introduction and publisher navigation; remove later equal-potency recency rule (pp.101–104).",
  "Ability Checks":"Reconcile the grouped skill list with all 18 canonical skill definitions; restore published variant example and navigation; preserve passive formula as text (pp.77–79).",
  "The Environment":"Remove absent commercial navigation, restore appendix labels and correct object-interaction transcription (pp.86–87).",
  "The Order of Combat":"Restore publisher GM terminology and remove later chapter navigation (pp.90–91).",
  "Movement":"Restore published forced-march navigation and high-jump formula without later minimum-0 errata; remove absent commercial navigation (pp.84–85).",
  "Between Adventures":"Correct source punctuation and GM terminology to publisher pp.88–89.",
  "What Is a Spell?":"Restore Wish example, bard/sorcerer examples, Warlock spell-slot exception and appendix label on p.100.",
  "Attunement":"Restore explicit class membership prerequisite from p.206 and the publisher's spellcasting-class monster exception.",
  "Wearing and Wielding Items":"Restore published size/anatomy and paired-item wording on p.206.",
  "Activating an Item":"Restore publisher pp.206–207 activation, consumable, command-word, spell-level and charge wording; retain the published Use an Item label with the local Use an Object action reference.",
  "Advantage and Disadvantage":"Restore the original reroll-only wording and unrestricted Inspiration wording from pp.76–77; remove later source additions.",
  "Resting":"Restore published short-rest healing wording and long-rest sleep/light-activity wording from p.87; do not import later errata into the pinned SRD.",
  "Combat Encounters":"Restore Giant Rat and Druid encounter examples and remove the commercial Monster Manual navigation from pp.202–203."
 };
 for(const [name,pointer,nums,refs,entryRange] of extraRules){
  const node=get(pointer);
  if(node.name!==name||nums.some(n=>!pageMap.has(n)))throw Error("Unreviewed book rule "+name);
  if(name!=="Advantage and Disadvantage"&&!nums.some(n=>norm(pageMap.get(n).textLayout).includes(norm(name))))throw Error("Missing book rule heading "+name);
  const local=new Set([...nums.map(pageId),...refs]);
  const project=x=>{
   if(typeof x==="string")return x.replace(/\{@book ([^}|]+)(?:\|[^}]+)?\}/g,(_,label)=>label);
   if(Array.isArray(x))return x.map(project);
   if(x&&typeof x==="object"){
    if(x.type==="table"){
     if(tag==="51"&&name==="Ability Checks"&&!x.caption)return legacySkillList(x,existing,local);
     const id="table."+slug(x.caption);
     if(!known.has(id)&&!rows.some(r=>r.id===id))throw Error("Unprojected book table "+id);
     local.add(id);return "See the "+x.caption+" table.";
    }
    return Object.fromEntries(Object.entries(x).filter(([k])=>!["id","page","data","srd","srd52","basicRules","basicRules2024"].includes(k)).map(([k,v])=>[k,project(v)]));
   }
   return x;
  };
  if(entryRange&&(entryRange[0]!==0||entryRange[1]>node.entries.length||node.entries.slice(...entryRange).some(x=>typeof x!=="string"&&x.type!=="table")))throw Error("Unexpected chapter introduction "+name);
  let content=project(entryRange?node.entries.slice(...entryRange):node.entries);
  if(tag==="51")content=legacyEquipmentEntries(name,legacyEconomyEntries(name,legacyAbilityAndOriginsEntries(name,legacyCombatAndCastingEntries(name,legacyAdditionalEntries(name,legacyGameplayEntries(name,legacyMagicItemEntries(name,content)))),existing,local)),existing,local);
  else content=modernMagicItemEntries(name,content);
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
  if(name==="Crafting Magic Items"){
   const tools=find(content,"Tools");
   tools.entries[0]=tools.entries[0].replace('For more information on the tools, see the "Equipment".','For more information on the tools, see "Equipment".');
   const raw=find(content,"Raw Materials");
   raw.entries[1]=raw.entries[1].replace("{@item +1 Plate Armor|srd52|+1 Armor (Plate Armor)}","+1 Armor (Plate Armor)");
  }
  const id="rule."+slug(name);
  const source=metadata(pointer,nums);
  if(entryRange)source.structuredSource.entryRange=entryRange;
  rows.push({id,kind:"rule",name,entries:content,catalogReferences:[...local].sort(),source});
  report.rules.push({id,pointer,pages:nums,...(entryRange?{entryRange}:{})});
  if(extraCorrections[name]&&(tag==="51"||!["Attunement","Wearing and Wielding Items","Activating an Item"].includes(name)))report.corrections.push({id,reason:extraCorrections[name]});
  if(tag==="52"&&name==="Magic Item Rarity")report.corrections.push({id,reason:"Restore the published generic planar city example and +1 plate armor display label from pp.205–206."});
 }
 const ids=new Set(rows.map(r=>r.id));
 if(ids.size!==rows.length||rows.some(r=>known.has(r.id)))throw Error("Book projection identity collision");
 for(const r of rows)for(const id of r.catalogReferences)if(!known.has(id)&&!ids.has(id))throw Error("Missing book reference "+r.id+" -> "+id);
 report.nativeCount=rows.length;
 report.tableInventory=inventoryBookTables(book,report,tag);
 if(tag==="52"){
  if(!known.has("item.trinket"))throw Error("Missing existing Trinkets table");
  report.externalTables=[{"name":"Trinkets","bookPointer":"/data/2/entries/5","representation":"item.trinket","structuredPath":"items.json","structuredPointer":"/item/901/entries/1","pages":[26,27],"rows":100,"disposition":"existing-catalog-representation","review":"All 100 publisher roll labels and text matched after punctuation, case and layout normalization."}];
 }
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
 const {rows,report}=generateBookRules(read("data/reference/srd/structured/book/book-srd"+tag+".json"),read(dir+"manifest.json"),src,pages,tag,known,existing);
 for(const [file,value] of [[native+"/book-rules-and-tables.json",rows],["data/reference/srd/book-rule-promotion-"+tag+".json",report]]){
  const target=path.join(root,file),body=JSON.stringify(value,null,2)+"\n";
  if(process.argv.includes("--check")){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale book projection "+file);}
  else fs.writeFileSync(target,body);
 }
 console.log(pack+": "+report.tables.length+" book tables, "+report.rules.length+" book rules");
}
