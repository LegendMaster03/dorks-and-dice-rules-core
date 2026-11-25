// Recover publisher-only definitions where the pinned structured corpus lacks a standalone record.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
function recoverPublisherRules(manifest,pages,tag,knownIds){
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
for(const [pack,tag,files] of [["dnd-5e","51",["053-077"]],["dnd-5-5e","52",["080-104"]]]){
 const base="data/rulesets/"+pack+"/",native=base+"native/";
 const pages=files.flatMap(f=>read(native+"rulebook/pages-"+f+".json"));
 const knownIds=["items","classes"].flatMap(dir=>fs.readdirSync(path.join(root,native,dir)).filter(f=>f.endsWith(".json")).flatMap(f=>read(native+dir+"/"+f).map(x=>x.id)));
 const rows=recoverPublisherRules(read(base+"manifest.json"),pages,tag,knownIds),body=JSON.stringify(rows,null,2)+"\n",target=path.join(root,native+"publisher-recovered-rules.json");
 if(process.argv.includes("--check")){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale publisher recovery "+pack);}
 else fs.writeFileSync(target,body);
 console.log(pack+": "+rows.length+" publisher-only rule/group recoveries");
}
