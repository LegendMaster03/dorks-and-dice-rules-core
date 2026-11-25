// Bulk promote edition-specific monster stat blocks from the pinned SRD JSON.
// Preserve the existing independently reviewed records and publisher extracts.
// A publisher name match establishes a locator, not full stat-block fidelity.
import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";

const fields = ("size type alignment ac hp speed initiative str dex con int wis cha save skill senses passive " +
    "languages cr trait action bonus reaction legendary legendaryActions legendaryActionsLair spellcasting " +
    "immune resist vulnerable conditionImmune gear attachedItems summonedBySpell summonedBySpellLevel pbNote").split(" ");
const slug = value => value.normalize("NFKC").replace(/[’‘]/g,"'").toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
const comparable = value => value.normalize("NFKC").toLowerCase().replace(/[\u00ad\u200b\u2010-\u2015\u2018\u2019\u2212-]/g,"").replace(/[^a-z0-9]/g,"");
const aliases = {
    "Deep Gnome (Svirfneblin)": ["Gnome, Deep (Svirfneblin)"],
    "Drow": ["Elf, Drow"],
    "Incubus": ["Succubus/Incubus", "Succubus or Incubus"],
    "Succubus": ["Succubus/Incubus", "Succubus or Incubus"],
    "Swarm of Beetles": ["Swarm of Insects"],
    "Swarm of Centipedes": ["Swarm of Insects"],
    "Swarm of Spiders": ["Swarm of Insects"],
    "Swarm of Wasps": ["Swarm of Insects"]
};
const fixes = {
    "51": {"Lich": ["Melf's acid arrow", "acid arrow"]},
    "52": {"Adult Black Dragon": ["Melf's Acid Arrow", "Acid Arrow"],
           "Ancient Black Dragon": ["Melf's Acid Arrow", "Acid Arrow"]}
};

function generate(candidates, spells, conditions, skills, pages, existing, sourceManifest, packageManifest, tag, officialUrl) {
    const idFor = (kind, name) => (kind === "creature" ? "monster" : kind) + "." + slug(name);
    const expectedTag = "SRD" + tag;
    const known = new Set([
        ...candidates.map(row=>idFor("creature",row.name)),
        ...existing.map(row=>row.id),
        ...spells.map(row=>idFor("spell",row.name)),
        ...conditions.map(row=>row.id), ...skills.map(row=>row.id)
    ]);
    const identities = new Set(candidates.map(row=>idFor("creature",row.name)));
    if (identities.size !== candidates.length) throw Error("Duplicate monster identity");
    const existingById = new Map(existing.map(row=>[row.id,row]));
    const pageMap = new Map(pages.map(row=>[row.page,row]));
    const result = [], report = {edition:packageManifest.officialPageImport.release, sourceCount:candidates.length,
        retainedExisting:[], imported:[], sourceVariantsPending:[], unresolvedTags:[], corrections:[], unmatchedPublisherLocators:[]};
    for (const [index, candidate] of candidates.entries()) {
        if (candidate.source !== expectedTag || candidate._copy) throw Error("Unsupported monster source " + candidate.name);
        const id = idFor("creature",candidate.name);
        if (candidate._versions?.length) report.sourceVariantsPending.push({id, count:candidate._versions.length,
            pointer:"/monster/"+index+"/_versions"});
        if (existingById.has(id)) {
            report.retainedExisting.push(id);
            continue;
        }
        const row = {id,kind:"monster",name:candidate.name};
        for (const key of fields) if (key in candidate) row[key] = candidate[key];
        const fix=fixes[tag]?.[candidate.name];
        if (fix) {
            const serialized=JSON.stringify(row);
            if (!serialized.includes(fix[0])) throw Error("Expected correction absent: "+id);
            Object.assign(row,JSON.parse(serialized.split(fix[0]).join(fix[1])));
            report.corrections.push({id,from:fix[0],to:fix[1]});
        }
        for (const key of ["size","type","ac","hp","speed","str","dex","con","int","wis","cha"])
            if (!(key in row)) throw Error("Missing stat-block field "+key+": "+id);
        // Candidate page numbers often match the SRD PDF. Verify that a name
        // occurs on that page or an adjacent page before asserting a locator.
        // Shared headings for swarm variants and succubus/incubus are explicit.
        const names=[candidate.name,...(aliases[candidate.name]??[])].map(comparable);
        const publisherPage=[candidate.page,candidate.page-1,candidate.page+1].map(n=>pageMap.get(n))
            .find(p=>p && names.some(name=>comparable(p.textLayout).includes(name)));
        if (!publisherPage) report.unmatchedPublisherLocators.push({id,candidatePage:candidate.page});
        const refs=new Set(publisherPage?[publisherPage.id]:[]);
        for (const match of JSON.stringify(row).matchAll(/\{@(spell|creature|condition|skill|item|feat) ([^}]+)\}/g)) {
            const [name,edition] = match[2].split("|");
            const target=idFor(match[1],name);
            if ((!edition || edition.toUpperCase()===expectedTag) && known.has(target)) refs.add(target);
            else report.unresolvedTags.push({id,kind:match[1],name,source:edition??null});
        }
        for (const condition of row.conditionImmune??[]) {
            if (typeof condition==="string" && known.has(idFor("condition",condition))) refs.add(idFor("condition",condition));
        }
        if (row.summonedBySpell) {
            const [name,edition]=row.summonedBySpell.split("|");
            if (edition===expectedTag && known.has(idFor("spell",name))) refs.add(idFor("spell",name));
            else report.unresolvedTags.push({id,kind:"summonedBySpell",name,source:edition??null});
        }
        row.catalogReferences=[...refs].sort();
        row.source={release:packageManifest.officialPageImport.release,url:officialUrl,
            pdfSha256:packageManifest.officialPageImport.pdfSha256,license:packageManifest.license,
            attribution:packageManifest.attribution,
            pages:publisherPage?[publisherPage.page]:[],
            structuredSource:{provider:sourceManifest.provider,commit:sourceManifest.commit,
                path:"bestiary/bestiary-srd"+tag+".json",pointer:"/monster/"+index},
            candidatePage:candidate.page,
            structuredStatus:publisherPage?"publisher-name-locator-matched; stat-block body review pending":"source-tagged; publisher locator and body review pending",
            adaptation:"Pinned edition-specific stat block; external media, fluff, derived metadata and generated variants excluded."};
        result.push(row);report.imported.push(id);
    }
    report.publisherOnly=existing.filter(row=>!identities.has(row.id)).map(row=>row.id);
    if (report.imported.length+report.retainedExisting.length!==candidates.length) throw Error("Source coverage mismatch");
    return {rows:result,report};
}

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),"utf8"));
const json=value=>JSON.stringify(value,null,2)+"\n";
const check=process.argv.includes("--check");
const sourceManifest=read("data/reference/srd/structured-manifest.json");
for(const [pack,tag] of [["dnd-5e","51"],["dnd-5-5e","52"]]){
    const base="data/rulesets/"+pack+"/", native=base+"native/";
    const candidates=read("data/reference/srd/structured/bestiary/bestiary-srd"+tag+".json").monster;
    const spells=read("data/reference/srd/structured/spells/spells-srd"+tag+".json").spell;
    const pages=fs.readdirSync(path.join(root,native,"rulebook")).filter(f=>/^pages-\d+-\d+\.json$/.test(f)).sort().flatMap(f=>read(native+"rulebook/"+f));
    const directory=native+"bestiary/";
    const existing=fs.readdirSync(path.join(root,directory)).filter(f=>f==="reviewed-initial.json"||/^animals-\d+\.json$/.test(f)).sort().flatMap(f=>read(directory+f));
    const {rows,report}=generate(candidates,spells,read(native+"conditions.json"),read(native+"skills.json"),pages,existing,sourceManifest,read(base+"manifest.json"),pages[0].source.url);
    const outputs=new Map();
    for(let i=0;i<rows.length;i+=25)outputs.set(directory+"structured-"+String(i/25+1).padStart(2,"0")+".json",json(rows.slice(i,i+25)));
    outputs.set("data/reference/srd/bestiary-promotion-"+tag+".json",json(report));
    for(const [file,body] of outputs){
        const target=path.join(root,file);
        if(check){if(fs.readFileSync(target,"utf8")!==body)throw Error("Stale bestiary promotion "+file);}
        else fs.writeFileSync(target,body);
    }
    for(const file of fs.readdirSync(path.join(root,directory)).filter(f=>/^structured-\d+\.json$/.test(f)))
        if(!outputs.has(directory+file))throw Error("Stale generated bestiary file "+file);
    console.log(pack+": "+rows.length+" new; "+report.retainedExisting.length+" retained source matches; "+report.publisherOnly.length+" publisher-only; "+report.unmatchedPublisherLocators.length+" pending locators; "+report.unresolvedTags.length+" unresolved markup references");
}
