// Literal references only; preserve grant modes, levels, cast qualifiers and conditional selectors.
export function linkSpellRelationships(rows,spells,edition){
 const normalized=s=>s.normalize("NFKC").replace(/[’‘]/g,"'").toLowerCase().trim().replace(/\s+/g," ");
 const byName=new Map();
 for(const s of spells){if(s.kind!=="spell"||s.source?.release!==(edition==="SRD51"?"srd-5.1":"srd-5.2.1"))throw Error("Foreign or non-spell catalog input");const key=normalized(s.name);if(byName.has(key))throw Error("Ambiguous spell identity "+key);byName.set(key,s);}
 let grants=0,prerequisites=0,links=0,selectors=0;
 for(const row of rows){
  const refs=new Set(row.catalogReferences);
  function link(raw,role){
   if(typeof raw!=="string")throw Error("Unexpected literal spell in "+row.id);
   const [uid,qualifier,...extra]=raw.split("#"),[name,source,...rest]=uid.split("|"),spell=byName.get(normalized(name));
   if(extra.length||rest.length||source&&source.toUpperCase()!==edition||!spell)throw Error("Unresolved edition-local spell "+raw+" in "+row.id);
   if(qualifier==="c"&&spell.level!==0||qualifier&&qualifier!=="c"&&!/^[1-9]$/.test(qualifier))throw Error("Unexpected cast qualifier "+raw);
   if(role==="grant")grants++;else prerequisites++;
   if(!refs.has(spell.id)){refs.add(spell.id);links++;}
  }
  function tree(value){
   if(typeof value==="string")link(value,"grant");
   else if(Array.isArray(value))value.forEach(tree);
   else if(value&&typeof value==="object"){
    if(Object.hasOwn(value,"choose")||Object.hasOwn(value,"all")){selectors++;return;}
    Object.values(value).forEach(tree);
   }
  }
  function visit(value){
   if(Array.isArray(value)){value.forEach(visit);return;}
   if(!value||typeof value!=="object")return;
   for(const [key,child] of Object.entries(value)){
    if(key==="source"||key==="catalogReferences")continue;
    if(key==="additionalSpells"&&Array.isArray(child)){
     for(const grant of child)for(const mode of ["known","prepared","innate","expanded"])if(Object.hasOwn(grant,mode))tree(grant[mode]);
    }else if(key==="prerequisite"&&Array.isArray(child)){
     for(const p of child)for(const target of p.spell??[]){if(typeof target==="string")link(target,"prerequisite");else selectors++;}
    }else visit(child);
   }
  }
  visit(row);row.catalogReferences=[...refs].sort();
 }
 return {grantOccurrences:grants,prerequisiteOccurrences:prerequisites,addedCatalogLinks:links,unresolvedSelectors:selectors};
}
