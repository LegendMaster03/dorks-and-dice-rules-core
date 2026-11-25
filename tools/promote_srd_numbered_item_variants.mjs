// Materialize numbered magic equipment only from the reviewed publisher eligibility categories.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
function generateEnhancements(base,templates,src,manifest,tag,officialUrl,known,unit){
 const clone=x=>JSON.parse(JSON.stringify(x));
 const slug=s=>s.normalize("NFKC").replace(/[’‘]/g,"'").toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 const edition="SRD"+tag,records=[],report={edition:manifest.officialPageImport.release,templateCount:0,counts:{},mergedTemplates:[],deferred:[],excludedBases:[],nativeCount:0};
 const fields=["type","weight","weaponCategory","property","mastery","dmg1","dmg2","dmgType","range","ac","strength","stealth","armor","weapon","ammoType","sword","axe","arrow","bolt","bulletFirearm","firearm"];
 const categories=["Ammunition","Armor","Shield","Weapon"];
 const pages=tag==="51"?[207,208,242,250]:[209,210,243,253];
 const selected=base.baseitem.map((b,index)=>({b,index})).filter(({b})=>b.source===edition&&!(tag==="52"&&b.edition==="classic"&&["Musket","Pistol"].includes(b.name)));
 const eligible=(b,category)=>{
  const type=b.type?.split("|")[0];
  if(category==="Ammunition")return ["A","AF"].includes(type)&&!b.packContents?.length&&!/\(\d+\)$/.test(b.name);
  if(category==="Armor")return ["LA","MA","HA"].includes(type)&&b.armor===true;
  if(category==="Shield")return type==="S";
  return ["M","R"].includes(type)&&b.weapon===true&&["simple","martial"].includes(b.weaponCategory)&&!b.net;
 };
 if(tag==="52"){
  const index=base.baseitem.findIndex(b=>b.name==="Firearm Bullets (10)"&&b.source===edition);
  if(index<0||unit?.id!=="baseitem.firearm-bullet")throw Error("Missing reviewed firearm bullet unit");
  selected.push({b:{name:unit.name,source:edition,type:unit.type,weight:unit.weight,bulletFirearm:unit.bulletFirearm},index,supplemental:true});
 }
 const lookup=new Map(templates.magicvariant.map((t,index)=>[t.source+"|"+t.name,{t,index}]));
 const ids=new Set();
 for(const category of categories){
  const page=pages[categories.indexOf(category)];
  for(const bonus of [1,2,3]){
   const found=lookup.get(edition+"|+"+bonus+" "+category);if(!found)throw Error("Missing generic enhancement template");
   const {t,index}=found,templateId="magic-variant."+slug(t.name);
   if(t.inherits.page!==page||t.inherits.source!==edition||t.inherits.namePrefix!==`+${bonus} `)throw Error("Unexpected template source "+t.name);
   const bonusField=["Armor","Shield"].includes(category)?"bonusAc":"bonusWeapon";
   if(t.inherits[bonusField]!==`+${bonus}`)throw Error("Unexpected enhancement bonus");
   const rarity=(category==="Armor"?["rare","very rare","legendary"]:["uncommon","rare","very rare"])[bonus-1];
   if(t.inherits.rarity!==rarity)throw Error("Unexpected enhancement rarity");
   report.templateCount++;
   for(const {b,index:baseIndex,supplemental} of selected.filter(({b})=>eligible(b,category))){
    const name=`+${bonus} ${b.name}`,id="item."+slug(name),baseId="baseitem."+slug(b.name);
    if(ids.has(id)||known?.has(id))throw Error("Concrete enhancement collision "+id);
    ids.add(id);
    const refs=new Set([templateId,baseId,"rulebook.page-"+String(page).padStart(3,"0")]);
    const row={id,kind:"item",name,baseItem:b.name+"|"+edition,baseItemId:baseId,variantOf:templateId};
    for(const field of fields)if(field in b)row[field]=clone(b[field]);
    row.rarity=rarity;row[bonusField]=t.inherits[bonusField];
    row.entries=t.inherits.entries.map(s=>s.replace(/\{=([A-Za-z]+)\}/g,(_,field)=>{
     if(field!==bonusField)throw Error("Unsupported enhancement substitution "+field);
     return t.inherits[field];
    }));
    if(tag==="52"&&category==="Ammunition")row.entries.push("This ammunition is typically found or sold in quantities of ten or twenty pieces. Ten pieces of this ammunition are equivalent in value to a potion of the same rarity.");
    if(tag==="52"&&category==="Shield")refs.add("glossary.armor-class");
    row.catalogReferences=[...refs].sort();
    const templatePointer={provider:src.provider,commit:src.commit,path:"magicvariants.json",pointer:"/magicvariant/"+index};
    row.source={release:manifest.officialPageImport.release,url:officialUrl,pdfSha256:manifest.officialPageImport.pdfSha256,license:manifest.license,attribution:manifest.attribution,pages:[page],structuredSource:templatePointer,additionalStructuredSources:[{provider:src.provider,commit:src.commit,path:"items-base.json",pointer:"/baseitem/"+baseIndex}],structuredStatus:"Derived numbered enhancement from the official generic item entry and edition-local base equipment; not an independent publisher item."};
    if(supplemental)row.source.upstreamStructuredSource=clone(unit.source.upstreamStructuredSource);
    if(tag==="52"&&name==="+1 Longsword"){
     const duplicate=lookup.get(edition+"|+1 Longsword");
     if(!duplicate||duplicate.t.inherits.bonusWeapon!=="+1")throw Error("Expected duplicate modern template");
     row.source.additionalStructuredSources.push({provider:src.provider,commit:src.commit,path:"magicvariants.json",pointer:"/magicvariant/"+duplicate.index});
     report.mergedTemplates.push({id,templateId:"magic-variant.1-longsword",reason:"Duplicate generic +1 weapon template occurrence; one concrete Longsword identity."});
    }
    if(known)for(const ref of row.catalogReferences)if(!known.has(ref))throw Error("Missing enhancement dependency "+ref);
    records.push(row);
    report.counts[category]=(report.counts[category]??0)+1;
   }
  }
 }
 for(const {b} of selected)if(["A","AF"].includes(b.type?.split("|")[0])&&!eligible(b,"Ammunition"))report.excludedBases.push({name:b.name,reason:"Ammunition pack; numbered magic item is one piece, not a whole pack."});
 if(tag==="51")report.excludedBases.push({name:"Net",reason:"Legacy net has no damage roll and is excluded by the source enhancement template."});
 // Keep existing generated chunks stable; append recovered individual bullets.
 if(tag==="52")records.sort((a,b)=>Number(a.baseItemId==="baseitem.firearm-bullet")-Number(b.baseItemId==="baseitem.firearm-bullet"));
 report.nativeCount=records.length;return {records,report};
}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),"utf8"));
const json=x=>JSON.stringify(x,null,2)+"\n";
const base=read("data/reference/srd/structured/items-base.json"),templates=read("data/reference/srd/structured/magicvariants.json"),src=read("data/reference/srd/structured-manifest.json");
for(const [pack,tag] of [["dnd-5e","51"],["dnd-5-5e","52"]]){
 const dir="data/rulesets/"+pack+"/native/",manifest=read("data/rulesets/"+pack+"/manifest.json");
 const itemFiles=fs.readdirSync(path.join(root,dir,"items")).filter(f=>/^structured-\d+\.json$/.test(f));
 const pages=fs.readdirSync(path.join(root,dir,"rulebook")).filter(f=>/^pages-\d+-\d+\.json$/.test(f)).flatMap(f=>read(dir+"rulebook/"+f));
 const items=itemFiles.flatMap(f=>read(dir+"items/"+f));
 const known=new Set([...items.map(r=>r.id),...pages.map(r=>r.id),...read(dir+"supplementary-rules.json").map(r=>r.id)]);
 const {records,report}=generateEnhancements(base,templates,src,manifest,tag,pages[0].source.url,known,items.find(r=>r.id==="baseitem.firearm-bullet"));
 const outputs=new Map();
 for(let i=0;i<records.length;i+=40)outputs.set(dir+"items/numbered-variants-"+String(i/40+1).padStart(2,"0")+".json",json(records.slice(i,i+40)));
 outputs.set("data/reference/srd/numbered-item-variant-promotion-"+tag+".json",json(report));
 for(const [file,body] of outputs){
  const target=path.join(root,file);
  if(process.argv.includes("--check")){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale numbered item variants "+file);}
  else fs.writeFileSync(target,body);
 }
 for(const f of fs.readdirSync(path.join(root,dir,"items")).filter(f=>/^numbered-variants-\d+\.json$/.test(f)))if(!outputs.has(dir+"items/"+f))throw Error("Stale numbered variant file "+f);
 console.log(pack+": "+report.nativeCount+" derived numbered item variants");
}
