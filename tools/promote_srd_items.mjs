// Promote selected item arrays from the pinned, separately hash-checked SRD corpus.
// Source membership is not a claim of official body fidelity or publication approval.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
const slug = name => name.normalize("NFKC").replace(/[’‘]/g,"'").toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
const kinds = {item:"item",baseitem:"baseitem",itemGroup:"item-group",magicvariant:"magic-variant",itemProperty:"item-property",itemMastery:"item-mastery"};
const excluded = new Set(["name","source","page","reprintedAs","classFeatures","miscTags","hasRefs","alias","tier","group","seeAlsoDeck","seeAlsoVehicle","additionalEntries"]);
function reconcileApparatus(records,report,vehicles,sourceManifest,tag){
 const row=records.find(x=>x.id==="item.apparatus-of-the-crab");
 const index=vehicles.vehicle.findIndex(v=>v.name===row?.name&&v.source==="SRD"+tag);
 if(!row||index<0)throw Error("Missing edition-local Apparatus");
 const v=vehicles.vehicle[index],page=tag==="51"?208:210;
 const original=JSON.stringify(row.entries);
 if(!/apparatus of kwalish/i.test(original))throw Error("Expected obsolete internal apparatus name");
 row.entries=JSON.parse(original.replace(/apparatus of kwalish/gi,m=>m==="Apparatus of Kwalish"?"Apparatus of the Crab":"apparatus of the Crab"));
 // Store the verified stat profile on the canonical item. Do not duplicate the
 // lever table or create a second independent object identity.
 row.vehicleProfile=Object.fromEntries(["vehicleType","size","capCrew","capPassenger","ac","speed","hp","immune"].map(k=>[k,JSON.parse(JSON.stringify(v[k]))]));
 if(tag==="51")row.vehicleProfile.speed.swim.condition="(or 0 ft. for both if the legs and tail aren't extended)";
 const pointer={provider:sourceManifest.provider,commit:sourceManifest.commit,path:"vehicles.json",pointer:"/vehicle/"+index};
 row.source.additionalStructuredSources=[...(row.source.additionalStructuredSources??[]),pointer];
 row.source.pages=[...new Set([...row.source.pages,page])].sort((a,b)=>a-b);
 const pageId="rulebook.page-"+String(page).padStart(3,"0");
 if(!row.catalogReferences.includes(pageId)){row.catalogReferences=[...row.catalogReferences,pageId].sort();report.resolvedReferenceCount++;}
 report.corrections.push({id:row.id,reason:"Reconcile publisher Apparatus of the Crab name and typed vehicle statistics; preserve one canonical item identity, omit unverified terrain metadata."});
 report.mergedOccurrences=[...(report.mergedOccurrences??[]),{id:row.id,path:pointer.path,pointer:pointer.pointer,reason:"Verified vehicle profile integrated with canonical magic item; duplicate prose not separately imported."}];
}
function reconcileNamedTemplate(row,tag){
 if(row.kind!=="magic-variant")return null;
 const h=JSON.parse(JSON.stringify(row.inherits)),name=row.name,modern=tag==="52";
 let reason=null,page=h.page;
 if(name==="Defender"){
  // The +1 in the publisher example is not a permanent AC bonus.
  if(h.bonusAc!=="+1")throw Error("Unexpected Defender metadata");
  delete h.bonusAc;h.entries=h.entries.map(s=>typeof s==="string"?s.replaceAll("{=bonusAc}","+1"):s);
  reason="Remove unconditional AC bonus: transfer part of the +3 weapon bonus only when the published condition applies.";
 }else if(!modern&&name==="Arrow of Slaying"){
  h.entries=[
   "An arrow of slaying is a magic weapon meant to slay a particular kind of creature. Some are more focused than others; for example, there are both arrows of dragon slaying and arrows of blue dragon slaying. If a creature belonging to the type, race, or group associated with an arrow of slaying takes damage from the arrow, the creature must make a {@dc 17} Constitution saving throw, taking an extra {@damage 6d10} piercing damage on a failed save, or half as much extra damage on a successful one.",
   "Once an arrow of slaying deals its extra damage to a creature, it becomes a nonmagical arrow.",
   "Other types of magic ammunition of this kind exist, such as bolts of slaying meant for a crossbow, though arrows are most common."
  ];reason="Restore legacy slaying ammunition's piercing damage, flexible target grouping and alternate ammunition guidance; remove imported modern random creature-type table.";
 }else if(!modern&&name==="Sword of Life Stealing"){
  h.entries=["When you attack a creature with this magic weapon and roll a 20 on the attack roll, that target takes an extra {@damage 3d6} necrotic damage, provided that the target isn't a construct or an undead. You gain temporary hit points equal to the extra damage dealt."];
  reason="Restore published 3d6 extra damage and matching temporary hit points instead of fixed 10; remove editorial note.";
 }else if(!modern&&name==="Sword of Sharpness"){
  h.entries=h.entries.filter(s=>typeof s!=="string"||!s.startsWith("{@note ")).map(s=>typeof s==="string"?s.replace("extra 14 slashing damage","extra {@damage 4d6} slashing damage"):s);
  reason="Restore published 4d6 slashing damage instead of fixed 14; remove editorial note.";
 }else if(!modern&&name==="Vicious Weapon"){
  h.entries=["When you roll a 20 on your attack roll with this magic weapon, your critical hit deals an extra {@damage 2d6} damage of the weapon's type."];
  reason="Restore published 2d6 damage on a natural 20 instead of fixed 7; remove editorial note.";
 }else if(modern&&name==="Dancing Sword"){
  row.requires=["Greatsword","Longsword","Rapier","Scimitar","Shortsword"].map(name=>({name,source:"SRD52"}));
  reason="Use the five explicitly listed eligible weapons rather than a generic sword tag.";
 }
 if(!reason)return null;
 row.inherits=h;return {page,reason};
}
function generate(files, sourceManifest, packageManifest, tag, officialUrl, externalIds, vehicles, supplement) {
    const records=[], report={edition:packageManifest.officialPageImport.release, counts:{}, excluded:[], corrections:[], publisherOnly:[]};
    const source="SRD"+tag;
    const reference = (page)=>"rulebook.page-"+String(page).padStart(3,"0");
    const addCorrection=(id,reason)=>report.corrections.push({id,reason});
    const metadata=(candidate,file,kind,index,pages=[])=>({
        release:packageManifest.officialPageImport.release,url:officialUrl,
        pdfSha256:packageManifest.officialPageImport.pdfSha256,license:packageManifest.license,attribution:packageManifest.attribution,
        pages,candidatePage:candidate.page??candidate.inherits?.page??null,
        structuredSource:{provider:sourceManifest.provider,commit:sourceManifest.commit,path:file,pointer:"/"+kind+"/"+index},
        structuredStatus:"source-tagged structured item; official body and inventory review pending"
    });
    for(const [file,data] of Object.entries(files)){
        for(const [kind,rows] of Object.entries(data)){
            if(!Array.isArray(rows))continue;
            for(const [index,candidate] of rows.entries()){
                if(candidate.source!==source)continue;
                const pointer={path:file,pointer:"/"+kind+"/"+index};
                if(!kinds[kind]){
                    report.excluded.push({...pointer,reason:"schema metadata or reusable entry template; not a standalone native rule"});
                    continue;
                }
                if(kind==="itemProperty"&&["AF","BF","RLD"].includes(candidate.abbreviation)){
                    report.excluded.push({...pointer,reason:"legacy firearm metadata, not a distinct official SRD weapon property"});
                    continue;
                }
                if(tag==="52"&&kind==="baseitem"&&["Musket","Pistol"].includes(candidate.name)&&candidate.edition==="classic"){
                    report.excluded.push({...pointer,reason:"obsolete legacy duplicate; retain modern weapon mastery definition"});
                    continue;
                }
                const name=candidate.name??candidate.entries?.[0]?.name??candidate.abbreviation;
                const id=kinds[kind]+"."+slug(kind==="itemProperty"?candidate.abbreviation:name);
                const row={id,kind:kinds[kind],name};
                for(const [key,value] of Object.entries(candidate))if(!excluded.has(key))row[key]=value;
                if(candidate.additionalEntries)report.excluded.push({...pointer,field:"additionalEntries",reason:"supplemental tool/vehicle prose not verified as SRD; base fields retained"});
                let pages=[];
                if(kind==="item"&&["Bag of Holding","Portable Hole"].includes(name)){
                    const serialized=JSON.stringify(row);
                    const corrected=serialized.replace(/Heward's Handy Haversack/gi,match=>match[0]==="h"?"handy haversack":"Handy Haversack");
                    if(serialized!==corrected){Object.assign(row,JSON.parse(corrected));addCorrection(id,"Use official Handy Haversack name");}
                }
                if(tag==="51"&&kind==="item"&&name==="Crawler Mucus (Contact)"){
                    const before=row.entries[0];
                    row.entries[0]=before.replace("{@creature carrion crawler}","crawler");
                    if(before===row.entries[0])throw Error("Expected crawler link");
                    pages=[204];addCorrection(id,"Official SRD 5.1 p.204 uses crawler, not Carrion Crawler; retain incapacitated condition and remove the unsupported creature link.");
                }
                if(tag==="51"&&kind==="itemGroup"&&name==="Gaming Set"){
                    row.items=row.items.filter(item=>/^(Dice Set|Playing Card Set)\|SRD51$/.test(item));
                    pages=[70,71];addCorrection(id,"Remove gaming set variants absent from the 5.1 tools table");
                }
                if(tag==="51"&&kind==="item"&&name==="Orb of Dragonkind"){
                    row.entries[0]=row.entries[0].replace("Ages past, on the world of Krynn,","Ages past,").replace("the wizards of the Towers of High Sorcery","powerful wizards").replace("five towers","five wizard towers");
                    row.entries[1]=row.entries[1].replace("Towers of High Sorcery","wizard towers");
                    row.entries[5]=row.entries[5].replace("Takhisis ({@creature Tiamat|RoT|Tiamat's} name on Krynn)","Tiamat");
                    row.entries=row.entries.map(entry=>entry?.name==="Call Dragons"?{...entry,entries:entry.entries.map(s=>s.replace("{@creature Tiamat|RoT}","Tiamat"))}:entry);
                    pages=[252,253];addCorrection(id,"Remove non-SRD setting names and foreign creature link; retain official artifact mechanics");
                }
                if(tag==="52"&&kind==="item"){
                    if(name==="Cube of Force"){
                        const swaps=[["Leomund's Tiny Hut","Tiny Hut"],["Mordenkainen's Private Sanctum","Private Sanctum"],["Otiluke's Resilient Sphere","Resilient Sphere"]];
                        let serialized=JSON.stringify(row);
                        for(const [old,modern] of swaps)serialized=serialized.split(old).join(modern).split(old.toLowerCase()).join(modern.toLowerCase());
                        Object.assign(row,JSON.parse(serialized));pages=[216];addCorrection(id,"S52-016: correct table and attached spell identities");
                    }else if(name==="Dragon Orb"){
                        row.entries=row.entries.slice(3).filter(e=>e?.name!=="Random Properties").map(e=>typeof e==="object"&&e.name==="Destroying an Orb"?{...e,entries:e.entries.map(s=>s.replace("An Orb of Dragonkind","A Dragon Orb"))}:e);
                        pages=[218];addCorrection(id,"S52-020: remove non-SRD setting introduction and legacy random properties");
                    }else if(name==="Mysterious Deck"){
                        row.entries=row.entries.filter(e=>e?.name!=="A Question of Enmity").map(e=>{
                            if(typeof e==="string")return e.replaceAll("Deck of Many Things","Mysterious Deck");
                            if(e.caption==="Deck of Many Things")return {...e,caption:"Mysterious Deck"};
                            if(e.name==="Ruin")return {...e,entries:e.entries.map(s=>s.replace(/If you have a Bastion .*?beyond your control\. /,""))};
                            return e;
                        });
                        pages=[231,232,233];addCorrection(id,"S52-021: remove non-SRD inset and Bastion effect; correct table identity");
                    }else if(name==="Crawler Mucus (Contact)"){
                        row.entries=row.entries.map(s=>s.replace("Carrion Crawler Mucus","Crawler Mucus"));pages=[197];addCorrection(id,"Correct modern poison name");
                    }else if(name==="Spider's Sting"){
                        row.entries=row.entries.map(s=>s.replace("Lolth's Sting","Spider's Sting"));pages=[198];addCorrection(id,"Correct modern poison name");
                    }
                }
                const templateCorrection=reconcileNamedTemplate(row,tag);
                if(templateCorrection){pages=[templateCorrection.page];addCorrection(id,templateCorrection.reason);}
                if(candidate._copy)throw Error("Unexpected unresolved copy: "+id);
                row.catalogReferences=pages.map(reference);
                row.source=metadata(candidate,file,kind,index,pages);
                records.push(row);
                report.counts[kind]=(report.counts[kind]??0)+1;
            }
        }
    }
    if(tag==="52"){
        const id="item.plate-armor-of-etherealness";
        if(records.some(r=>r.id===id))throw Error("Publisher-only item is no longer missing");
        records.push({id,kind:"item",name:"Plate Armor of Etherealness",eligibleArmor:["Half Plate Armor","Plate Armor"],rarity:"legendary",reqAttune:true,
            entries:["While you're wearing this armor, you can take a {@action Magic|SRD52} action and use a command word to gain the effect of the {@spell Etherealness|SRD52} spell. The spell ends immediately if you remove the armor or take a {@action Magic|SRD52} action to repeat the command word. This property of the armor can't be used again until the next dawn."],
            catalogReferences:[reference(234),"spell.etherealness","baseitem.half-plate-armor","baseitem.plate-armor"],
            source:{release:packageManifest.officialPageImport.release,url:officialUrl,pdfSha256:packageManifest.officialPageImport.pdfSha256,license:packageManifest.license,attribution:packageManifest.attribution,pages:[234],structuredStatus:"publisher-only missing definition recovered"}});
        report.publisherOnly.push(id);
        // Recover a unit from the upstream SRD-marked definition, corroborated by
        // the official ammunition table (p.96) and the pinned pack occurrence.
        const packIndex=files["items-base.json"].baseitem.findIndex(b=>b.name==="Firearm Bullets (10)"&&b.source===source);
        const pack=files["items-base.json"].baseitem[packIndex], unit=supplement.definition;
        if(!pack||unit.name!=="Firearm Bullet"||unit.srd52!==true||unit.source!=="XPHB"||unit.type!=="A|XPHB"||
            pack.packContents?.[0]?.quantity!==10||unit.weight*10!==pack.weight||unit.value*10!==pack.value)
            throw Error("Firearm bullet unit does not agree with reviewed pack");
        const bulletId="baseitem.firearm-bullet";
        records.push({id:bulletId,kind:"baseitem",name:unit.name,edition:unit.edition,type:"A|SRD52",
            rarity:unit.rarity,weight:unit.weight,value:unit.value,bulletFirearm:unit.bulletFirearm,
            catalogReferences:[reference(91),reference(96)],
            source:{...metadata(pack,"items-base.json","baseitem",packIndex,[91,96]),
                upstreamStructuredSource:{provider:supplement.provider,commit:supplement.commit,path:supplement.path,pointer:supplement.pointer},
                structuredStatus:"Individual ammunition derived from official pack quantity, weight and cost; upstream structured unit corroborates fields. Commercial-source prose not promoted."}});
        report.counts.baseitem++;
    }
    if(new Set(records.map(r=>r.id)).size!==records.length)throw Error("Duplicate item identity");
    const known = new Set([...records.map(r=>r.id),...externalIds]);
    const itemIds=new Map();
    for(const row of records.filter(r=>["item","baseitem","magic-variant","item-group"].includes(r.kind))){
        const key=slug(row.name);
        const candidates=itemIds.get(key)??[];
        candidates.push(row.id);itemIds.set(key,candidates);
    }
    report.unresolvedReferences=[];
    report.resolvedReferenceCount=0;
    const leaves=value=>typeof value==="string"?[value]:Array.isArray(value)?value.flatMap(leaves):value&&typeof value==="object"?Object.values(value).flatMap(leaves):[];
    for(const row of records){
        const refs=new Set(row.catalogReferences);
        const unresolved=new Set();
        const resolve=(kind,raw)=>{
            if(typeof raw!=="string")return;
            const [name,edition]=raw.split("|"), key=slug(name.split("#")[0]);
            const editionTag=edition?.split("#")[0];
            let targets=kind==="item"?(itemIds.get(key)??[]):[(kind==="creature"?"monster":kind)+"."+key];
            targets=targets.filter(id=>known.has(id));
            // Prefer a concrete equipment record to a group or generic template;
            // do not pick arbitrarily when two concrete records share a name.
            if(kind==="item"&&targets.length>1){
                const concrete=targets.filter(id=>id.startsWith("item.")||id.startsWith("baseitem."));
                if(concrete.length===1)targets=concrete;
            }
            if((!editionTag||editionTag.toUpperCase()===source)&&targets.length===1){
                if(targets[0]!==row.id)refs.add(targets[0]);
            }else{
                const identity=kind+"|"+raw;
                if(!unresolved.has(identity)){
                    unresolved.add(identity);
                    report.unresolvedReferences.push({id:row.id,kind,raw,reason:editionTag&&editionTag.toUpperCase()!==source?"foreign-source":"missing-or-ambiguous"});
                }
            }
        };
        const {source:provenance,catalogReferences,...payload}=row;
        for(const match of JSON.stringify(payload).matchAll(/\{@(spell|creature|condition|skill|item|feat) ([^}]+)\}/g)){
            const parts=match[2].split("|");
            resolve(match[1],parts.slice(0,2).join("|"));
        }
        for(const node of [row,row.inherits??{}]){
            for(const value of leaves(node.attachedSpells))resolve("spell",value);
            for(const key of ["baseItem","ammoType"])if(node[key])resolve("item",node[key]);
            for(const value of node.items??[])if(typeof value==="string")resolve("item",value);
            for(const value of node.packContents??[])if(typeof value==="string"||value?.item)resolve("item",typeof value==="string"?value:value.item);
            for(const value of node.property??[])resolve("item-property",value);
            for(const value of node.mastery??[])resolve("item-mastery",value);
        }
        row.catalogReferences=[...refs].sort();
        report.resolvedReferenceCount+=row.catalogReferences.length;
    }
    reconcileApparatus(records,report,vehicles,sourceManifest,tag);
    report.nativeCount=records.length;
    return {records,report};
}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),"utf8"));
const json=value=>JSON.stringify(value,null,2)+"\n";
const sourceManifest=read("data/reference/srd/structured-manifest.json");
const inputs=Object.fromEntries(["items.json","items-base.json","magicvariants.json"].map(file=>[file,read("data/reference/srd/structured/"+file)]));
const check=process.argv.includes("--check");
for(const [pack,tag] of [["dnd-5e","51"],["dnd-5-5e","52"]]){
    const base="data/rulesets/"+pack+"/", dir=base+"native/items/";
    const manifest=read(base+"manifest.json");
    const first=fs.readdirSync(path.join(root,base,"native/rulebook")).filter(f=>/^pages-\d+-\d+\.json$/.test(f)).sort()[0];
    const officialUrl=read(base+"native/rulebook/"+first)[0].source.url;
    const native=base+"native/";
    const externalIds=["spells","bestiary"].flatMap(folder=>fs.readdirSync(path.join(root,native,folder)).filter(f=>f.endsWith(".json")).sort().flatMap(f=>read(native+folder+"/"+f).map(row=>row.id)));
    externalIds.push(...read(native+"skills.json").map(row=>row.id),...read(native+"conditions.json").map(row=>row.id));
    const {records,report}=generate(inputs,sourceManifest,manifest,tag,officialUrl,externalIds,read("data/reference/srd/structured/vehicles.json"),read("data/reference/srd/upstream-firearm-bullet-52.json"));
    const outputs=new Map();
    for(let i=0;i<records.length;i+=50)outputs.set(dir+"structured-"+String(i/50+1).padStart(2,"0")+".json",json(records.slice(i,i+50)));
    outputs.set("data/reference/srd/item-promotion-"+tag+".json",json(report));
    for(const [file,body] of outputs){
        const target=path.join(root,file);
        if(check){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale item promotion "+file);}
        else {fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,body);}
    }
    for(const file of fs.readdirSync(path.join(root,dir)).filter(f=>/^structured-\d+\.json$/.test(f)))
        if(!outputs.has(dir+file))throw Error("Stale generated item file "+file);
    console.log(pack+": "+report.nativeCount+" items, groups, templates and weapon rules");
}
