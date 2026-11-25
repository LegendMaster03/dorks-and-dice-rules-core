// Promote existing 5e.tools-shaped SRD data, retaining publisher evidence.
// The official PDF workflow verifies the publisher projection independently.
// Do not interpret source tags or successful generation as publication approval.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const spellFields = "time range components duration entries entriesHigherLevel scalingLevelDice damageInflict savingThrow miscTags areaTags meta conditionInflict affectsCreatureType spellAttack abilityCheck damageResist conditionImmune damageVulnerable damageImmune".split(" ");
const monsterEntryFields = ["trait", "action", "bonus", "reaction", "spellcasting"];
const slug = name => name.normalize("NFKC").replace(/[’‘]/g, "'").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
const pointer = (manifest, tag, kind, index) => ({
    provider: manifest.provider, commit: manifest.commit,
    path: (kind === "spell" ? "spells/spells" : "bestiary/bestiary") + "-srd" + tag + ".json",
    pointer: "/" + kind + "/" + index
});

function enrichSpell(row, candidate, index, manifest, tag) {
    if (candidate.source !== "SRD" + tag || row.id !== "spell." + slug(candidate.name)
        || row.level !== candidate.level || row.school !== candidate.school)
        throw Error("Publisher/source identity mismatch: " + row.id);
    for (const field of ["time", "range", "components", "duration", "entries"])
        if (!(field in candidate)) throw Error("Missing spell field " + field + ": " + row.id);
    const result = {...row};
    for (const field of spellFields) {
        delete result[field];
        if (field in candidate) result[field] = candidate[field];
    }
    // Keep publisher-confirmed differences in the native projection, not the pinned corpus.
    if(tag==="51"&&row.id==="spell.acid-splash"){
        const evidence=row.officialTextLayout.replace(/\s+/g," ");
        if(!evidence.includes("Choose one creature within range, or choose two creatures within range")||
           result.entries[0].split("you can see").length!==3||!result.miscTags.includes("SGT"))
            throw Error("Unexpected legacy Acid Splash targeting evidence");
        result.entries=[result.entries[0].replaceAll(" you can see",""),...result.entries.slice(1)];
        result.miscTags=result.miscTags.filter(t=>t!=="SGT");
    }
    if(tag==="52"&&row.id==="spell.animate-dead"){
        const evidence=row.officialTextLayout.replace(/\s+/g," ");
        if(!evidence.includes('corpse (see “Monsters” for the stat blocks).')||!result.entries[0].endsWith("if you chose a corpse."))
            throw Error("Unexpected modern Animate Dead cross-reference evidence");
        result.entries=[result.entries[0].replace(/corpse\.$/,'corpse (see “Monsters” for the stat blocks).'),...result.entries.slice(1)];
    }
    result.source = {...row.source, structuredSource: pointer(manifest, tag, "spell", index),
        structuredStatus: "publisher-name-level-school-verified; structured body review pending"};
    return result;
}

function enrichAnimal(row, candidate, index, manifest) {
    if (candidate.source !== "SRD52" || row.id !== "monster." + slug(candidate.name)
        || row.source.structuredSource.pointer !== "/monster/" + index)
        throw Error("Publisher/source animal identity mismatch: " + row.id);
    const result = {...row};
    // Retain independently verified numerical values and source layout. Restore
    // original entries, including attack/damage/recharge tags and spell structure.
    for (const field of monsterEntryFields) {
        delete result[field];
        if (field in candidate) result[field] = candidate[field];
    }
    result.source = {...row.source, structuredStatus:
        "publisher-stat-block-headers-and-core-fields-verified; structured entry body review pending"};
    return result;
}

function promote(rows, candidates, manifest, tag, kind) {
    const lookup = new Map(candidates.map((row, index) => [slug(row.name), {row, index}]));
    if (lookup.size !== candidates.length) throw Error("Duplicate source identity");
    return rows.map(row => {
        const key = row.id.slice(row.id.indexOf(".") + 1);
        const hit = lookup.get(key);
        if (!hit) throw Error("Missing structured source for " + row.id);
        return kind === "spell" ? enrichSpell(row, hit.row, hit.index, manifest, tag)
            : enrichAnimal(row, hit.row, hit.index, manifest);
    });
}
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const read = p => JSON.parse(fs.readFileSync(path.join(root, p), "utf8"));
const json = value => JSON.stringify(value, null, 2) + "\n";
const manifest = read("data/reference/srd/structured-manifest.json");
const check = process.argv.includes("--check");
for (const [pack, tag] of [["dnd-5e", "51"], ["dnd-5-5e", "52"]]) {
    const kinds = tag === "52" ? ["spell", "monster"] : ["spell"];
    for (const kind of kinds) {
        const source = kind === "spell" ? "spells/spells" : "bestiary/bestiary";
        const candidates = read("data/reference/srd/structured/" + source + "-srd" + tag + ".json")[kind];
        const directory = "data/rulesets/" + pack + "/native/" + (kind === "spell" ? "spells" : "bestiary");
        const pattern = kind === "spell" ? /^spells-\d+-\d+\.json$/ : /^animals-\d+\.json$/;
        const files = fs.readdirSync(path.join(root, directory)).filter(f => pattern.test(f)).sort();
        let count = 0;
        for (const file of files) {
            const p = directory + "/" + file;
            const current = read(p);
            const result = promote(current, candidates, manifest, tag, kind);
            if (check) {
                // Compare data, not property order or serializer whitespace.
                const canonical = value => JSON.stringify(value, function (key, item) {
                    return item && typeof item === "object" && !Array.isArray(item)
                        ? Object.fromEntries(Object.entries(item).sort(([a], [b]) => a.localeCompare(b))) : item;
                });
                if (canonical(current) !== canonical(result)) throw Error("Stale structured promotion " + p);
            } else fs.writeFileSync(path.join(root, p), json(result));
            count += result.length;
        }
        if (count !== (kind === "spell" ? candidates.length : 95))
            throw Error("Unexpected promoted count " + pack + " " + kind + ": " + count);
        console.log(pack + " " + kind + ": " + count + " structured records");
    }
}
