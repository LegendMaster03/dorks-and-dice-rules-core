import { srdImportFiles } from "./srd_import_sources.mjs";
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
  if(!row.entries?.some(s=>typeof s==="string"&&s.includes("{@link this is incorrect|")))throw Error("Unexpected Vicious Weapon top-level entries");
  row.entries=[...h.entries];
  reason="Restore published 2d6 damage on a natural 20 instead of fixed 7 in both template and inherited entries; remove editorial note.";
 }else if(modern&&name==="Dancing Sword"){
  row.requires=["Greatsword","Longsword","Rapier","Scimitar","Shortsword"].map(name=>({name,source:"SRD52"}));
  reason="Use the five explicitly listed eligible weapons rather than a generic sword tag.";
 }
 if(!reason)return null;
 row.inherits=h;return {page,reason};
}
function reconcileLegacyWeaponProperty(row){
 const corrections={
 "item-property.a":{pages:[64],entries:["You can use a weapon that has the ammunition property to make a ranged attack only if you have ammunition to fire from the weapon. Each time you attack with the weapon, you expend one piece of ammunition. Drawing the ammunition from a quiver, case, or other container is part of the attack (you need a free hand to load a one-handed weapon). At the end of the battle, you can recover half your expended ammunition by taking a minute to search the battlefield.","If you use a weapon that has the ammunition property to make a melee attack, you treat the weapon as an improvised weapon (see \"Improvised Weapons\" later in the section). A sling must be loaded to deal any damage when used in this way."]},
 "item-property.h":{pages:[65],entries:["Small creatures have disadvantage on attack rolls with heavy weapons. A heavy weapon's size and bulk make it too large for a Small creature to use effectively."]},
 "item-property.r":{pages:[65],entries:["This weapon adds 5 feet to your reach when you attack with it, as well as when determining your reach for opportunity attacks with it."]},
 "item-property.2h":{pages:[65],entries:["This weapon requires two hands when you attack with it."]}
 };
 const v=corrections[row.id];if(!v)return null;
 if(row.entries?.length!==1||!row.entries[0].entries)throw Error("Unexpected weapon property "+row.id);
 row.entries=[{...row.entries[0],entries:v.entries}];
 return {pages:v.pages,reason:"Restore original SRD 5.1 weapon property wording; do not import later errata."};
}

const DECK_REVIEW=[
  {
    "tag": "51",
    "id": "item.deck-of-illusions",
    "pages": [
      216
    ],
    "count": 33,
    "reason": "Reconcile all 33 legacy illusion table rows with SRD 5.1 p.216; preserve the published Beholder example and restore Succubus or incubus display."
  },
  {
    "tag": "51",
    "id": "item.deck-of-many-things",
    "pages": [
      216,
      217,
      218
    ],
    "count": 22,
    "reason": "Verify all 22 playing-card mappings and named effect sections against SRD 5.1 pp.216–218; remove non-SRD A Question of Enmity inset."
  },
  {
    "tag": "52",
    "id": "item.deck-of-illusions",
    "pages": [
      217,
      218
    ],
    "count": 33,
    "reason": "Verify all 33 illusion table rows against SRD 5.2.1 pp.217–218; restore Basilisk, Veteran Warrior display and published stat-block footnote."
  },
  {
    "tag": "52",
    "id": "item.mysterious-deck",
    "pages": [
      231,
      232,
      233
    ],
    "count": 22,
    "reason": "Verify all 22 published card table mappings and named effect sections; retain existing SRD-specific corrections."
  }
];
function reviewDeckTable(row,tag,publisherPages){
    const spec=DECK_REVIEW.find(s=>s.tag===tag&&s.id===row.id);
    if(!spec)return null;
    const table=row.entries.find(e=>e?.type==="table");
    if(table?.rows.length!==spec.count)throw Error("Unexpected deck table size "+row.id);
    if(row.id==="item.deck-of-illusions"){
        if(tag==="51"){
            if(table.rows[2][1]!=="{@creature Succubus||Succubus/Incubus}")throw Error("Unexpected legacy illusion alias");
            table.rows[2][1]="{@creature Succubus||Succubus or incubus}";
        }else{
            if(table.rows[4][0]!=="13-15"||table.rows[4][1]!=="Beholder"||table.rows[30][1]!=="{@creature Warrior Veteran|SRD52}")throw Error("Unexpected modern illusion table");
            table.rows[4][1]="{@creature Basilisk|SRD52}";
            table.rows[30][1]="{@creature Warrior Veteran|SRD52|Veteran Warrior}";
            table.colLabels[1]="Illusion*";
            const note="Stat blocks for these creatures (except the card drawer) appear in “Monsters.”";
            if(!publisherPages.find(p=>p.page===218).textLayout.includes("*Stat blocks for these creatures (except the card drawer) appear in")||!publisherPages.find(p=>p.page===218).textLayout.includes("“Monsters.”"))throw Error("Missing illusion footnote");
            row.entries.push(note);
        }
    }else if(row.id==="item.deck-of-many-things"){
        if(row.entries.filter(e=>e?.name==="A Question of Enmity").length!==1)throw Error("Unexpected legacy deck inset");
        row.entries=row.entries.filter(e=>e?.name!=="A Question of Enmity");
    }
    const normalize=s=>String(s).toLowerCase().replace(/[’‘]/g,"'").replace(/[–—−]/g,"-").replace(/\s/g,"");
    const display=x=>String(typeof x==="object"?x.entry:x).replace(/\{@creature ([^}|]+)(?:\|[^}|]*)?(?:\|([^}]+))?\}/g,(_,name,label)=>label??name);
    const published=publisherPages.filter(p=>spec.pages.includes(p.page));
    if(published.length!==spec.pages.length)throw Error("Missing deck publisher pages");
    const evidence=published.flatMap(p=>p.textLayout.split("\n")).map(normalize);
    for(const cells of table.rows){
        if(!evidence.some(l=>l.includes(normalize(cells.map(display).join(" ")))))throw Error("Publisher deck table row mismatch "+row.id+" "+JSON.stringify(cells));
    }
    if(row.id!=="item.deck-of-illusions"){
        const heads=row.entries.filter(e=>e?.type==="entries").map(e=>e.name);
        const cardNames=table.rows.map(c=>c.at(-1).replace(/\*$/,""));
        if(heads.length!==22||new Set(heads).size!==22||cardNames.some(n=>!heads.includes(n)))throw Error("Missing named card effects "+row.id);
        for(const name of heads)if(!published.some(p=>p.textLayout.includes(name+".")))throw Error("Missing published card heading "+name);
    }
    return spec;
}

function recoverArmorResistance(records,report,manifest,tag,publisherPages,input){
 const edition="SRD"+tag,modern=tag==="52",damage=["Acid","Cold","Fire","Force","Lightning","Necrotic","Poison","Psychic","Radiant","Thunder"];
 const pages=modern?[210]:[208,209],pageIds=pages.map(p=>"rulebook.page-"+String(p).padStart(3,"0")),selected=publisherPages.filter(p=>pages.includes(p.page));
 const slug=s=>s.normalize("NFKC").replace(/[’‘]/g,"'").toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
 if(selected.length!==pages.length||input.provider!=="5etools-mirror-3/5etools-src"||input.commit!=="2512db75eb48ef06f6190b086a82430887906dca")throw Error("Unexpected Armor of Resistance source");
 const layout=selected.map(p=>p.textLayout).join("\n"),norm=s=>s.replace(/\s+/g," ").trim();
 const clauses=modern?["You have Resistance to one type of damage while","you wear this armor. The GM chooses the type or","determines it randomly by rolling on the following","table."]:["You have resistance to one type of damage while you","wear this armor. The GM chooses the type or","determines it randomly from the options below."];
 if(!layout.includes("Armor of Resistance")||clauses.some(s=>!norm(layout).includes(s)))throw Error("Publisher Armor of Resistance body mismatch");
 for(let i=0;i<5;i++)if(!new RegExp("\\b"+(i+1)+"\\s+"+damage[i]+"\\s+"+(i+6)+"\\s+"+damage[i+5]+"\\b").test(layout))throw Error("Publisher resistance table mismatch");
 const baseFields=["type","weaponCategory","rarity","weight","value","dmg1","dmg2","dmgType","property","mastery","range","ac","strength","stealth","armor","weapon","ammoType","sword","axe","arrow","bolt","bulletFirearm","firearm"];
 const bases=records.filter(b=>b.kind==="baseitem"&&b.armor===true&&["LA","MA","HA"].includes(b.type?.split("|")[0]));
 if(bases.length!==12||new Set(bases.map(b=>b.id)).size!==12)throw Error("Unexpected resistance armor base inventory");
 const provenance={release:manifest.officialPageImport.release,url:selected[0].source.url,pdfSha256:manifest.officialPageImport.pdfSha256,license:manifest.license,attribution:manifest.attribution,pages};
 const groupBody=modern?"You have {@variantrule Resistance|SRD52} to one type of damage while you wear this armor. The GM chooses the type or determines it randomly by rolling on the following table.":"You have resistance to one type of damage while you wear this armor. The GM chooses the type or determines it randomly from the options below.";
 const table={type:"table",colStyles:["col-2 text-center","col-10"],colLabels:[modern?"1d10":"d10","Damage Type"],rows:damage.map((d,i)=>[String(i+1),d])};
 const localRefs=modern?[...pageIds,"glossary.resistance"].sort():pageIds,variants=[],concrete=[];
 if(input.records[tag].length!==10)throw Error("Unexpected expanded resistance variant count");
 for(let i=0;i<damage.length;i++){
  const {path,pointer,record:c}=input.records[tag][i],d=damage[i],lower=d.toLowerCase(),inh=c.inherits,expectedReq=modern?[{type:"LA|XPHB"},{type:"MA|XPHB"},{type:"HA|XPHB"}]:[{armor:true}];
  if(c.name!=="Armor of "+d+" Resistance"||inh?.source!==(modern?"XDMG":"DMG")||inh[modern?"srd52":"srd"]!==true||inh.rarity!=="rare"||inh.reqAttune!==true||JSON.stringify(inh.resist)!==JSON.stringify([lower])||inh.nameSuffix!==" of "+d+" Resistance"||JSON.stringify(c.requires)!==JSON.stringify(expectedReq))throw Error("Unexpected upstream resistance variant "+d);
  const expandedStructuredSource={provider:input.provider,commit:input.commit,path,pointer};
  const body=modern?`You have {@variantrule Resistance|SRD52} to ${d} damage while you wear this armor.`:`You have resistance to ${lower} damage while you wear this armor.`;
  const t={id:"magic-variant."+slug(c.name),kind:"magic-variant",name:c.name,type:"GV|"+edition,requires:modern?["LA","MA","HA"].map(type=>({type:type+"|"+edition})):[{armor:true}],inherits:{nameSuffix:inh.nameSuffix,source:edition,page:pages[0],rarity:"rare",reqAttune:true,resist:[lower],entries:[body]},catalogReferences:localRefs,source:{...provenance,structuredStatus:"Expanded upstream damage-type template; published resistance group and damage table verified. Conditional effect is descriptive.",expandedStructuredSource}};
  variants.push(t);
  for(const b of bases){
   const name=b.name+inh.nameSuffix,row={id:"item."+slug(name),kind:"item",name,baseItem:b.name+"|"+edition,baseItemId:b.id,variantOf:t.id};
   for(const key of baseFields)if(Object.hasOwn(b,key))row[key]=JSON.parse(JSON.stringify(b[key]));
   row.rarity="rare";row.reqAttune=true;row.resist=[lower];row.entries=[body];
   row.catalogReferences=[...new Set([b.id,t.id,...localRefs,...(b.catalogReferences??[])])].sort();
   row.source={...provenance,additionalStructuredSources:[b.source.structuredSource],structuredStatus:"Derived named armor configuration; eligible base and published damage choice verified. Conditional effect is descriptive, not executed.",expandedStructuredSource};
   concrete.push(row);
  }
 }
 const groupId="item-group.armor-of-resistance";let group=records.find(r=>r.id===groupId),newGroup=[];
 if(modern){
  if(!group||group.items.length!==0)throw Error("Expected incomplete modern resistance group");
  group.entries=[groupBody,table];
  group.source.pages=pages;
  group.source.structuredStatus="Published armor eligibility, body and damage table verified; finite damage templates recovered from expanded source.";
 }else{
  if(group)throw Error("Unexpected legacy resistance group");
  group={id:groupId,kind:"item-group",name:"Armor of Resistance",type:"GV|"+edition,rarity:"rare",reqAttune:true,entries:[groupBody,table],items:[],itemsHidden:true,catalogReferences:[],source:{...provenance,structuredStatus:"Publisher-only armor resistance group recovered from SRD 5.1 pp.208–209."}};
  newGroup=[group];report.publisherOnly.push(groupId);
 }
 group.items=variants.map(t=>t.name+"|"+edition);
 group.catalogReferences=[...new Set([...localRefs,...variants.map(t=>t.id)])].sort();
 const extra=[...newGroup,...variants,...concrete],known=new Set(records.map(r=>r.id));
 for(const row of extra){if(known.has(row.id))throw Error("Resistance identity collision "+row.id);known.add(row.id);}
 report.expandedArmorResistance={groups:newGroup.length,templates:variants.length,concrete:concrete.length,nativeCount:extra.length,groupId,pages};
 report.resolvedReferenceCount+=group.catalogReferences.length+extra.filter(x=>x.id!==groupId).reduce((n,x)=>n+x.catalogReferences.length,0);
 report.nativeCount+=extra.length;
 report.corrections.push({id:groupId,reason:"Recover the published ten damage choices, link the ten generic templates and derive 120 eligible armor configurations; retain conditional resistance and attunement."});
 return extra;
}

function verifyLegacyScroll(row,publisherPages){
 if(row.kind!=="item"||!row.id.startsWith("item.spell-scroll-"))return false;
 const levels=["Cantrip","1st","2nd","3rd","4th","5th","6th","7th","8th","9th"],level=row.spellScrollLevel;
 const page=publisherPages.find(p=>p.page===242),expectedRarity=["common","common","uncommon","uncommon","rare","rare","very rare","very rare","very rare","legendary"],dc=[13,13,13,15,15,17,17,18,18,19],bonus=[5,5,5,7,7,9,9,10,10,11];
 if(!Number.isInteger(level)||level<0||level>9||!page||row.rarity!==expectedRarity[level])throw Error("Unexpected legacy scroll identity "+row.id);
 const normalize=s=>s.replace(/\s+/g," ").toLowerCase();
 const expected=normalize(`${levels[level]} ${expectedRarity[level]} ${dc[level]} +${bonus[level]}`);
 if(!page.textLayout.split("\n").some(l=>normalize(l).includes(expected)))throw Error("Missing publisher scroll table row "+row.id);
 if(!row.entries.includes(`A spell cast from this scroll has a save DC of ${dc[level]} and an attack bonus of {@hit ${bonus[level]}}.`))throw Error("Scroll prose mismatch "+row.id);
 row.spellSaveDc=dc[level];row.spellAttackBonus=bonus[level];
 return true;
}

function generate(files, sourceManifest, packageManifest, tag, officialUrl, externalIds, vehicles, supplement, publisherPages) {
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
                if(tag==="51"&&kind==="item"&&name==="Mace of Smiting"){
                    const published=publisherPages.find(p=>p.page===229)?.textLayout??"";
                    if(!published.includes("target takes an extra 2d6")||!published.includes("bludgeoning damage, or 4d6 bludgeoning damage if")||
                       row.entries.length!==3||!row.entries[1].includes("extra 7 bludgeoning damage")||!row.entries[2].includes("{@link this is incorrect|"))throw Error("Unexpected Mace of Smiting source");
                    row.entries=[row.entries[0],"When you roll a 20 on an attack roll made with this weapon, the target takes an extra {@damage 2d6} bludgeoning damage, or {@damage 4d6} bludgeoning damage if it's a construct. If a construct has 25 hit points or fewer after taking this damage, it is destroyed."];
                    pages=[229];addCorrection(id,"Restore the published 2d6/4d6 critical-hit damage on SRD 5.1 p.229; remove the non-publisher editorial note.");
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
                    if(name==="Burglar's Pack"){
                        if(row.weight!==47.5)throw Error("Unexpected source Burglar's Pack weight");
                        row.weight=42;pages=[95,96];addCorrection(id,"Correct Burglar's Pack weight to published 42 lb.; price and contents agree with SRD 5.2.1 pp.95–96.");
                    }else if(name==="Trinket"){
                        // The published random table is already part of this canonical item.
                        const table=row.entries.find(entry=>entry?.type==="table"&&entry.caption==="Trinkets");
                        if(table?.rows.length!==100||table.rows.some((r,i)=>r[0]!==String((i+1)%100).padStart(2,"0")))throw Error("Unexpected Trinkets roll coverage");
                        row.entries[0]=row.entries[0].replace("The DM might also","The GM might also");
                        pages=[26,27];addCorrection(id,"Verify all 100 Trinkets rows against publisher pp.26–27; restore GM wording and page references.");
                    }else if(name==="Cube of Force"){
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
                if(tag==="52"&&kind==="itemGroup"&&name==="Druidic Focus"){
                    if(JSON.stringify(row.focus)!=='["Druid"]')throw Error("Unexpected source Druidic Focus eligibility");
                    row.focus=["Druid","Ranger"];pages=[97];addCorrection(id,"Restore Ranger as an eligible user of a Druidic Focus, as printed on SRD 5.2.1 p.97.");
                }
                if(tag==="51"&&kind==="itemProperty"){
                    const correction=reconcileLegacyWeaponProperty(row);
                    if(correction){pages=correction.pages;addCorrection(id,correction.reason);}
                }
                if(tag==="51"&&verifyLegacyScroll(row,publisherPages)){pages=[242];addCorrection(id,"Verify published spell level, rarity, save DC and attack bonus on SRD 5.1 p.242; expose typed scroll statistics.");}
                const deckReview=reviewDeckTable(row,tag,publisherPages);
                if(deckReview){pages=deckReview.pages;addCorrection(id,deckReview.reason);}
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
const { read, write } = srdImportFiles(root);
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
    const publisherPages=(tag==="51"?["203-227","228-252"]:["205-229","230-254"]).flatMap(n=>read(native+"rulebook/pages-"+n+".json"));
    const {records,report}=generate(inputs,sourceManifest,manifest,tag,officialUrl,externalIds,read("data/reference/srd/structured/vehicles.json"),read("data/reference/srd/upstream-firearm-bullet-52.json"),publisherPages);
    const resistance=recoverArmorResistance(records,report,manifest,tag,publisherPages,read("data/reference/srd/upstream-armor-resistance.json"));
    const outputs=new Map();
    outputs.set(dir+"armor-resistance.json",json(resistance));
    for(let i=0;i<records.length;i+=50)outputs.set(dir+"structured-"+String(i/50+1).padStart(2,"0")+".json",json(records.slice(i,i+50)));
    outputs.set("data/reference/srd/item-promotion-"+tag+".json",json(report));
    for(const [file,body] of outputs){

        write(file, body, check);
    }
    for(const file of fs.readdirSync(path.join(root,dir)).filter(f=>/^structured-\d+\.json$/.test(f)))
        if(!outputs.has(dir+file))throw Error("Stale generated item file "+file);
    console.log(pack+": "+report.nativeCount+" items, groups, templates and weapon rules");
}
