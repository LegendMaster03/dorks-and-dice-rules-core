// Recover publisher-only definitions where the pinned structured corpus lacks a standalone record.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
function legacyPublisherTables(pageMap,metadata,items){
 const rows=[],slug=s=>s.toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const norm=s=>s.toLowerCase().replace(/[’‘]/g,"'").replace(/[–—]/g,"-").replace(/\s/g,"");
 const page=n=>{const p=pageMap.get(n);if(!p)throw Error("Missing publisher page "+n);return p.textLayout;};
 const add=(name,n,colLabels,tableRows,refs=[],evidence=page(n))=>{
  for(const row of tableRows)if(!norm(evidence).includes(norm(row.join(" "))))throw Error("Publisher table row mismatch "+name+" "+JSON.stringify(row));
  rows.push({id:"table."+slug(name),kind:"table",name,colLabels,rows:tableRows,catalogReferences:[...new Set([pageMap.get(n).id,...refs])].sort(),source:metadata([n])});
 };
 add("Trap Save DCs and Attack Bonuses",196,["Trap Danger","Save DC","Attack Bonus"],[["Setback","10–11","+3 to +5"],["Dangerous","12–15","+6 to +8"],["Deadly","16–20","+9 to +12"]]);
 add("Damage Severity by Level",196,["Character Level","Setback","Dangerous","Deadly"],[["1st–4th","1d10","2d10","4d10"],["5th–10th","2d10","4d10","10d10"],["11th–16th","4d10","10d10","18d10"],["17th–20th","10d10","18d10","24d10"]]);
 add("Hit Dice by Size",256,["Monster Size","Hit Die","Average HP per Die"],[["Tiny","d4","2½"],["Small","d6","3½"],["Medium","d8","4½"],["Large","d10","5½"],["Huge","d12","6½"],["Gargantuan","d20","10½"]]);
 // Read both parallel columns rather than copying modern grouped PB rows.
 const crs=["0","1/8","1/4","1/2",...Array.from({length:30},(_,i)=>String(i+1))];
 for(const [name,n,valueLabel,valuePattern] of [
  ["Proficiency Bonus by Challenge Rating",256,"Proficiency Bonus",String.raw`\+\d+`],
  ["Experience Points by Challenge Rating",258,"XP",String.raw`(?:0 or 10|[\d,]+)`]
 ]){
  const pattern=new RegExp(String.raw`(?:^|\s{2,})(\d+(?:/\d+)?)\s+(`+valuePattern+String.raw`)\s{2,}(\d+(?:/\d+)?)\s+(`+valuePattern+String.raw`)(?:\s{2,}.*)?\s*// Recover publisher-only definitions where the pinned structured corpus lacks a standalone record.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
);
  const matches=page(n).split("\n").map(l=>l.match(pattern)).filter(Boolean);
  if(matches.length!==17)throw Error("Unexpected challenge table length "+name+" "+matches.length);
  const rr=[...matches.map(m=>[m[1],m[2]]),...matches.map(m=>[m[3],m[4]])];
  if(JSON.stringify(rr.map(r=>r[0]))!==JSON.stringify(crs))throw Error("Challenge rating order "+name);
  add(name,n,["Challenge",valueLabel],rr);
 }
 const poisons=[["Assassin’s blood","Ingested","150 gp"],["Burnt othur fumes","Inhaled","500 gp"],["Crawler mucus","Contact","200 gp"],["Drow poison","Injury","200 gp"],["Essence of ether","Inhaled","300 gp"],["Malice","Inhaled","250 gp"],["Midnight tears","Ingested","1,500 gp"],["Oil of taggit","Contact","400 gp"],["Pale tincture","Ingested","250 gp"],["Purple worm poison","Injury","2,000 gp"],["Serpent venom","Injury","200 gp"],["Torpor","Ingested","600 gp"],["Truth serum","Ingested","150 gp"],["Wyvern poison","Injury","1,200 gp"]];
 const poisonRefs=poisons.map(([name,type,price])=>{
  const id="item."+slug(name)+(name==="Crawler mucus"?"-contact":""),item=items.find(x=>x.id===id);
  if(!item?.poison||JSON.stringify(item.poisonTypes)!==JSON.stringify([type.toLowerCase()])||item.value!==Number(price.replaceAll(",","").replace(" gp",""))*100)throw Error("Poison catalog mismatch "+id);
  return id;
 });
 add("Poisons",204,["Item","Type","Price per Dose"],poisons,poisonRefs);
 add("Sentient Item's Alignment",251,["d100","Alignment"],[["01–15","Lawful good"],["16–35","Neutral good"],["36–50","Chaotic good"],["51–63","Lawful neutral"],["64–73","Neutral"],["74–85","Chaotic neutral"],["86–89","Lawful evil"],["90–96","Neutral evil"],["97–00","Chaotic evil"]]);
 add("Sentient Item's Senses",251,["d4","Senses"],[["1","Hearing and normal vision out to 30 feet."],["2","Hearing and normal vision out to 60 feet"],["3","Hearing and normal vision out to 120 feet."],["4","Hearing and darkvision out to 120 feet."]]);
 const right=page(251).split("\n").map(l=>l.match(/ {10,}(\S.*)$/)?.[1]??"").join(" ");
 add("Sentient Item's Communication",251,["d100","Communication"],[["01–60","The item communicates by transmitting emotion to the creature carrying or wielding it."],["61–90","The item can speak, read, and understand one or more languages."],["91–00","The item can speak, read, and understand one or more languages. In addition, the item can communicate telepathically with any character that carries or wields it."]],[],right);
 // The publisher's narrow gap between columns is four spaces on p.252.
 const left=page(252).split("\n").filter(l=>!/^\s{20,}/.test(l)).map(l=>l.trim().split(/ {4,}/)[0]).join(" ");
 add("Sentient Item's Special Purpose",252,["d10","Purpose"],[["1","Aligned: The item seeks to defeat or destroy those of a diametrically opposed alignment. (Such an item is never neutral.)"],["2","Bane: The item seeks to defeat or destroy creatures of a particular kind, such as fiends, shapechangers, trolls, or wizards."],["3","Protector: The item seeks to defend a particular race or kind of creature, such as elves or druids."],["4","Crusader: The item seeks to defeat, weaken, or destroy the servants of a particular deity."],["5","Templar: The item seeks to defend the servants and interests of a particular deity."],["6","Destroyer: The item craves destruction and goads its user to fight arbitrarily."],["7","Glory Seeker: The item seeks renown as the greatest magic item in the world, by establishing its user as a famous or notorious figure."],["8","Lore Seeker: The item craves knowledge or is determined to solve a mystery, learn a secret, or unravel a cryptic prophecy."],["9","Destiny Seeker: The item is convinced that it and its wielder have key roles to play in future events."],["10","Creator Seeker: The item seeks its creator and wants to understand why it was created."]],[],left);
 return rows;
}

function recoverPublisherRules(manifest,pages,tag,knownIds,items){
 const slug=s=>s.toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const pageMap=new Map(pages.map(p=>[p.page,p]));
 const metadata=nums=>({release:manifest.officialPageImport.release,url:pageMap.get(nums[0]).source.url,pdfSha256:manifest.officialPageImport.pdfSha256,license:manifest.license,attribution:manifest.attribution,pages:nums,structuredStatus:"Publisher-only definition recovered; no corresponding standalone structured source record."});
 const rows=[];
 if(tag==="51"){
  const definitions=[
   {name:"Arcane Focus",scfType:"arcane",classes:["Sorcerer","Warlock","Wizard"],forms:[["baseitem","Crystal"],["baseitem","Orb"],["baseitem","Rod"],["baseitem","Staff"],["baseitem","Wand"]],pages:[66,67,69],entries:["An arcane focus is a special item—an orb, a crystal, a rod, a specially constructed staff, a wand-like length of wood, or some similar item—designed to channel the power of arcane spells. A sorcerer, warlock, or wizard can use such an item as a spellcasting focus."]},
   {name:"Druidic Focus",scfType:"druid",classes:["Druid"],forms:[["item","Sprig of mistletoe"],["item","Totem"],["baseitem","Wooden staff"],["item","Yew wand"]],pages:[67,69],entries:["A druidic focus might be a sprig of mistletoe or holly, a wand or scepter made of yew or another special wood, a staff drawn whole out of a living tree, or a totem object incorporating feathers, fur, bones, and teeth from sacred animals. A druid can use such an object as a spellcasting focus."]},
   {name:"Holy Symbol",scfType:"holy",classes:["Cleric","Paladin"],forms:[["item","Amulet"],["item","Emblem"],["item","Reliquary"]],pages:[67,69],entries:["A holy symbol is a representation of a god or pantheon. It might be an amulet depicting a symbol representing a deity, the same symbol carefully engraved or inlaid as an emblem on a shield, or a tiny box holding a fragment of a sacred relic. Appendix PH-B, Fantasy-Historical Pantheons, lists symbols commonly associated with many gods in the multiverse. A cleric or paladin can use a holy symbol as a spellcasting focus. To use the symbol in this way, the caster must hold it in hand, wear it visibly, or bear it on a shield."]}
  ];
  for(const d of definitions){
   if(!d.pages.some(n=>pageMap.get(n).textLayout.toLowerCase().includes(d.name.toLowerCase())))throw Error("Missing focus locator "+d.name);
   const refs=d.forms.map(([kind,name])=>kind+"."+slug(name));
   if(d.forms.some(([,name])=>!pageMap.get(69).textLayout.toLowerCase().includes(name.toLowerCase())))throw Error("Missing focus form in publisher table "+d.name);
   rows.push({id:"item-group."+slug(d.name),kind:"item-group",name:d.name,scfType:d.scfType,rarity:"none",focus:d.classes,items:d.forms.map(([,name])=>name+"|SRD51"),entries:d.entries,catalogReferences:[...refs,...d.classes.map(n=>"class."+slug(n)),...d.pages.map(n=>pageMap.get(n).id)].sort(),source:metadata(d.pages)});
  }
  rows.push(...legacyPublisherTables(pageMap,metadata,items));
 }else{
  const page=pageMap.get(90),entry="Each weapon has a mastery property, which is usable only by a character who has a feature, such as Weapon Mastery, that unlocks the property for the character. The properties are defined below.";
  const names=["Cleave","Graze","Nick","Push","Sap","Slow","Topple","Vex"];
  if(!page.textLayout.includes("Mastery Properties")||names.some(n=>!page.textLayout.includes(n)))throw Error("Missing mastery section");
  rows.push({id:"rule.weapon-mastery-properties",kind:"rule",name:"Mastery Properties",aliases:["Weapon Mastery Properties"],entries:[entry],properties:names.map(n=>"item-mastery."+slug(n)),catalogReferences:[page.id,...names.map(n=>"item-mastery."+slug(n))].sort(),source:metadata([90])});
 }
 const known=new Set([...knownIds,...pages.map(p=>p.id)]);
 for(const r of rows)for(const id of r.catalogReferences)if(!known.has(id))throw Error("Missing publisher recovery reference "+id);
 return rows;
}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),"utf8"));
for(const [pack,tag,files] of [["dnd-5e","51",["053-077","178-202","203-227","228-252","253-277"]],["dnd-5-5e","52",["080-104"]]]){
 const base="data/rulesets/"+pack+"/",native=base+"native/";
 const pages=files.flatMap(f=>read(native+"rulebook/pages-"+f+".json"));
 const load=dir=>fs.readdirSync(path.join(root,native,dir)).filter(f=>f.endsWith(".json")).flatMap(f=>read(native+dir+"/"+f));
 const items=load("items"),knownIds=[...items,...load("classes")].map(x=>x.id);
 const rows=recoverPublisherRules(read(base+"manifest.json"),pages,tag,knownIds,items),body=JSON.stringify(rows,null,2)+"\n",target=path.join(root,native+"publisher-recovered-rules.json");
 if(process.argv.includes("--check")){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale publisher recovery "+pack);}
 else fs.writeFileSync(target,body);
 console.log(pack+": "+rows.length+" publisher-only rule/group recoveries");
}
