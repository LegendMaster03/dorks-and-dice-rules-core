// Link literal character-option dependencies without flattening choice branches.
import fs from "node:fs";
import path from "node:path";

const norm=s=>String(s).normalize("NFKC").replace(/[’‘]/g,"'").toLowerCase().trim().replace(/\s+/g," ");
const categories={item:["item","baseitem","item-group","magic-variant"],skill:["skill"],language:["language"],feat:["feat"],optionalfeature:["optional-feature"]};
const proficiencyFields={skillProficiencies:"skill",toolProficiencies:"item",languageProficiencies:"language",weaponProficiencies:"item",armorProficiencies:"item"};

export function characterRelationshipCatalog(root,base,read,featureRows){
 const native=base+"native/";
 const items=fs.readdirSync(path.join(root,native,"items")).filter(f=>f.endsWith(".json")).sort().flatMap(f=>read(native+"items/"+f));
 const general=["skills.json","general-rules.json","supplementary-rules.json","publisher-recovered-rules.json","book-rules-and-tables.json"].flatMap(f=>read(native+f));
 return [...items,...general,...(featureRows??read(native+"feats-and-optional-features.json"))]
  .filter(r=>Object.values(categories).some(kinds=>kinds.includes(r.kind)));
}

export function linkCharacterRelationships(rows,catalog,edition){
 const release=edition==="SRD51"?"srd-5.1":"srd-5.2.1";
 const names=new Map(),variants=new Map(),seen=new Set();
 function put(index,key,id){if(!index.has(key))index.set(key,new Set());index.get(key).add(id);}
 for(const r of catalog){
  if(r.source?.release!==release||seen.has(r.id))throw Error("Foreign or duplicate relationship target "+r.id);
  seen.add(r.id);
  for(const name of [r.name,...(r.aliases??[])])if(typeof name==="string")put(names,r.kind+"|"+norm(name),r.id);
  for(const v of r._versions??[])if(v.name&&(!v.source||v.source.toUpperCase()===edition))put(variants,r.kind+"|"+norm(v.name),r.id);
 }
 let literalOccurrences=0,namedVariantOccurrences=0,addedCatalogLinks=0;
 function resolve(kind,raw){
  if(typeof raw!=="string")throw Error("Nonliteral relationship");
  const [name,source,...rest]=raw.split("|");
  if(rest.length||source&&source.toUpperCase()!==edition)throw Error("Foreign or malformed relationship "+raw);
  const candidates=index=>[...new Set(categories[kind].flatMap(k=>[...(index.get(k+"|"+norm(name))??[])]))];
  let ids=candidates(names),variant=false;
  if(kind==="item"&&ids.length>1){
   const concrete=ids.filter(id=>id.startsWith("item.")||id.startsWith("baseitem."));
   if(concrete.length===1)ids=concrete;
  }
  if(!ids.length){ids=candidates(variants);variant=true;}
  if(ids.length!==1)throw Error("Missing or ambiguous "+kind+" relationship "+raw+" -> "+ids.join(","));
  return {id:ids[0],variant};
 }
 for(const row of rows){
  const refs=new Set(row.catalogReferences??[]);
  function link(kind,raw){
   const {id,variant}=resolve(kind,raw);
   literalOccurrences++;
   if(variant)namedVariantOccurrences++;
   if(!refs.has(id)&&id!==row.id){refs.add(id);addedCatalogLinks++;}
  }
  function equipment(v){
   if(typeof v==="string")link("item",v);
   else if(Array.isArray(v))v.forEach(equipment);
   else if(v&&typeof v==="object"){
    if(Object.hasOwn(v,"item"))link("item",v.item);
    else if(!["equipmentType","equipmentTypes","special","value"].some(k=>Object.hasOwn(v,k)))Object.values(v).forEach(equipment);
   }
  }
  function proficiency(v,kind){
   if(!v||typeof v!=="object"||Array.isArray(v))return;
   for(const [key,value] of Object.entries(v)){
    if(key==="choose"&&value&&typeof value==="object"){for(const member of value.from??[])link(kind,member);}
    else if(key==="all"||key.startsWith("any")||["simple","martial","light","medium","heavy","shield"].includes(key))continue;
    else if(value===true)link(kind,key);
   }
  }
  function visit(v){
   if(Array.isArray(v)){v.forEach(visit);return;}
   if(!v||typeof v!=="object")return;
   for(const [key,value] of Object.entries(v)){
    if(["source","catalogReferences","textLayout","officialTextLayout"].includes(key))continue;
    if(key==="startingEquipment"){
     if(Array.isArray(value))equipment(value);
     else if(value&&Object.hasOwn(value,"defaultData"))equipment(value.defaultData);
    }else if(key==="feats"&&Array.isArray(value)){
     for(const grant of value)for(const [name,flag] of Object.entries(grant))if(name!=="anyFromCategory"&&flag===true)link("feat",name);
    }else if(Object.hasOwn(proficiencyFields,key)&&Array.isArray(value)){
     for(const grant of value)proficiency(grant,proficiencyFields[key]);
    }else if(["startingProficiencies","proficienciesGained"].includes(key)&&value&&typeof value==="object"){
     // The class short fields are authoritative only when no structured
     // long-form equivalent exists. Literal choice members are dependencies,
     // not unconditional proficiency grants.
     if(!Object.hasOwn(value,"skillProficiencies"))for(const grant of value.skills??[])proficiency(grant,"skill");
     if(!Object.hasOwn(value,"weaponProficiencies"))for(const member of value.weapons??[]){
      if(typeof member!=="string")continue;
      const match=member.match(/^\{@item ([^|{}]+)(?:\|([^|{}]*))?(?:\|[^{}]+)?\}$/);
      if(match)link("item",match[1]+(match[2]?"|"+match[2]:""));
     }
     visit(value);
    }else if(key==="type"&&value==="refOptionalfeature")link("optionalfeature",v.optionalfeature);
    else visit(value);
   }
  }
  visit(row);row.catalogReferences=[...refs].sort();
 }
 return {literalOccurrences,namedVariantOccurrences,addedCatalogLinks};
}
