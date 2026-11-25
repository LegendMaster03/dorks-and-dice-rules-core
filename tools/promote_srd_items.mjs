// Promote selected item arrays from the pinned, separately hash-checked SRD corpus.
// Source membership is not a claim of official body fidelity or publication approval.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";
const slug = name => name.normalize("NFKC").replace(/[’‘]/g,"'").toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
const kinds = {item:"item",baseitem:"baseitem",itemGroup:"item-group",magicvariant:"magic-variant",itemProperty:"item-property",itemMastery:"item-mastery"};
const excluded = new Set(["name","source","page","reprintedAs","classFeatures","miscTags","hasRefs","alias","tier","group","seeAlsoDeck","seeAlsoVehicle","additionalEntries"]);
function generate(files, sourceManifest, packageManifest, tag, officialUrl, externalIds) {
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
    const {records,report}=generate(inputs,sourceManifest,manifest,tag,officialUrl,externalIds);
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
