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
    if(tag==="51") {
        const corrections={
  "spell.call-lightning": [
    [
      0,
      "within range directly above you",
      "100 feet directly above you"
    ],
    [
      1,
      "you can see under the cloud",
      "you can see within range"
    ]
  ],
  "spell.clone": [
    [
      0,
      "inside the vessel used in the spell's casting",
      "inside a sealed vessel"
    ]
  ],
  "spell.color-spray": [
    [
      1,
      "until the end of your next turn",
      "until the spell ends"
    ]
  ],
  "spell.contagion": [
    [
      0,
      "On a hit, the target is {@condition poisoned}.",
      "On a hit, you afflict the creature with a disease of your choice from any of the ones described below."
    ],
    [
      1,
      "At the end of each of the {@condition poisoned} target's turns, the target must make a Constitution saving throw. If the target succeeds on three of these saves, it is no longer {@condition poisoned}, and the spell ends. If the target fails three of these saves, the target is no longer {@condition poisoned}, but choose one of the diseases below. The target is subjected to the chosen disease for the spell's duration.",
      "At the end of each of the target's turns, it must make a Constitution saving throw. After failing three of these saving throws, the disease's effects last for the duration, and the creature stops making these saves. After succeeding on three of these saving throws, the creature recovers from the disease, and the spell ends."
    ]
  ],
  "spell.disintegrate": [
    [
      1,
      "The target is disintegrated if this damage leaves it with 0 hit points.",
      "If this damage reduces the target to 0 hit points, it is disintegrated."
    ]
  ],
  "spell.glyph-of-warding": [
    [
      0,
      "When you cast this spell, you inscribe a glyph that later unleashes a magical effect. You inscribe it either on a surface (such as a table or a section of floor or wall) or within an object that can be closed (such as a book, a scroll, or a treasure chest) to conceal the glyph. The glyph can cover an area no larger than 10 feet in diameter. If the surface or object is moved more than 10 feet from where you cast this spell, the glyph is broken, and the spell ends without being triggered.",
      "When you cast this spell, you inscribe a glyph that harms other creatures, either upon a surface (such as a table or a section of floor or wall) or within an object that can be closed (such as a book, a scroll, or a treasure chest) to conceal the glyph. If you choose a surface, the glyph can cover an area of the surface no larger than 10 feet in diameter. If you choose an object, that object must remain in its place; if the object is moved more than 10 feet from where you cast this spell, the glyph is broken, and the spell ends without being triggered."
    ]
  ],
  "spell.heroes-feast": [
    [
      0,
      "Up to twelve creatures",
      "Up to twelve other creatures"
    ]
  ]
};
        const evidenceById={
  "spell.call-lightning": [
    "100 feet directly above you",
    "see within range"
  ],
  "spell.clone": [
    "inside a sealed vessel"
  ],
  "spell.color-spray": [
    "spell is blinded until the spell ends"
  ],
  "spell.contagion": [
    "afflict the creature with a disease of your choice",
    "After failing three of these saving throws"
  ],
  "spell.disintegrate": [
    "If this damage reduces the target to 0 hit points, it is disintegrated."
  ],
  "spell.glyph-of-warding": [
    "harms other creatures, either upon a surface",
    "If you choose an object, that object must remain in its place"
  ],
  "spell.heroes-feast": [
    "Up to twelve other creatures"
  ]
};
        if(Object.hasOwn(corrections,row.id)){
            const official=row.officialTextLayout.replace(/[’‘]/g,"'").replace(/\s+/g," ");
            if(evidenceById[row.id].some(phrase=>!official.includes(phrase)))throw Error("Publisher evidence changed: "+row.id);
            result.entries=[...result.entries];
            for(const [index,from,to] of corrections[row.id]){
                if(typeof result.entries[index]!=="string"||result.entries[index].split(from).length!==2)throw Error("Legacy spell source changed: "+row.id);
                result.entries[index]=result.entries[index].replace(from,to);
            }
            if(row.id==="spell.contagion"){
                if(!result.conditionInflict.includes("poisoned"))throw Error("Unexpected Contagion condition metadata");
                result.conditionInflict=result.conditionInflict.filter(c=>c!=="poisoned");
            }
        }
    }
    // Explicit publisher-confirmed corrections, including nested entries.
    const bodyCorrections=[
  [
    "51",
    "spell.levitate",
    [
      0
    ],
    "One creature or loose object",
    "One creature or object",
    "One creature or object of your choice"
  ],
  [
    "51",
    "spell.magnificent-mansion",
    [
      2
    ],
    "any creatures or objects left inside",
    "any creatures inside",
    "any creatures inside the extradimensional space"
  ],
  [
    "51",
    "spell.moonbeam",
    [
      3
    ],
    "move the beam up to 60 feet",
    "move the beam 60 feet",
    "move the beam 60 feet"
  ],
  [
    "51",
    "spell.sanctuary",
    [
      1
    ],
    "If the warded creature makes an attack, casts a spell that affects an enemy, or deals damage to another creature, this spell ends.",
    "If the warded creature makes an attack or casts a spell that affects an enemy creature, this spell ends.",
    "If the warded creature makes an attack or casts a spell that affects an enemy creature, this spell ends."
  ],
  [
    "51",
    "spell.simulacrum",
    [
      0
    ],
    "it duplicates, except that it is a construct.",
    "it duplicates.",
    "illusion uses all the statistics of the creature it duplicates."
  ],
  [
    "51",
    "spell.true-polymorph",
    [
      0
    ],
    "the creature into a nonmagical object",
    "the creature into an object",
    "the creature into an object"
  ],
  [
    "51",
    "spell.true-polymorph",
    [
      6,
      "entries",
      0
    ],
    "The DM has the creature's statistics",
    "The GM has the creature's statistics",
    "The GM has the creature's statistics"
  ],
  [
    "51",
    "spell.true-polymorph",
    [
      8,
      "entries",
      0
    ],
    "into that form, as long as the object's size is no larger than the creature's size.",
    "into that form.",
    "wearing and carrying into that form."
  ],
  [
    "51",
    "spell.true-resurrection",
    [
      1
    ],
    " If the creature was undead, it is restored to its non-undead form.",
    "",
    "replaces damaged or missing organs and limbs. The spell can even provide"
  ],
  [
    "51",
    "spell.unseen-servant",
    [
      0
    ],
    "mindless, shapeless, Medium force",
    "mindless, shapeless force",
    "mindless, shapeless force"
  ],
  [
    "52",
    "spell.wish",
    [
      3,
      "items",
      6,
      "entries",
      0
    ],
    "If your wish would undo the multiverse itself, threaten the City of Sigil, or affect the Lady of Pain in any way, you see an image of her in your mind for a moment; she shakes her head, and your wish fails.",
    "If your wish would undo the multiverse itself, your wish fails.",
    "If your wish would undo the multiverse itself, your wish fails."
  ]
];
    const applicable=bodyCorrections.filter(([edition,id])=>edition===tag&&id===row.id);
    if(applicable.length){
        result.entries=JSON.parse(JSON.stringify(result.entries));
        const official=row.officialTextLayout.replace(/[’‘]/g,"'").replace(/\s+/g," ");
        for(const [,id,path,from,to,evidence] of applicable){
            if(!official.includes(evidence))throw Error("Publisher evidence changed: "+id);
            let target=result.entries;
            for(const key of path.slice(0,-1))target=target[key];
            const key=path.at(-1);
            if(typeof target[key]!=="string"||target[key].split(from).length!==2)throw Error("Source entry changed: "+id);
            target[key]=target[key].replace(from,to);
        }
    }
    if(tag==="52"){
        const corrections=[
  [
    "spell.awaken",
    [
      "entries",
      0
    ],
    "The DM chooses statistics",
    "The GM chooses statistics",
    "GM chooses statistics"
  ],
  [
    "spell.awaken",
    [
      "entries",
      0
    ],
    "in the {@book Monster Manual|SRD52}.",
    "in “Monsters.”",
    "Awakened Tree in “Monsters.”"
  ],
  [
    "spell.create-undead",
    [
      "entries",
      0
    ],
    "see the {@book Monster Manual|SRD52} for its stat block",
    "see “Monsters” for its stat block",
    "see “Monsters” for its stat block"
  ],
  [
    "spell.create-undead",
    [
      "entriesHigherLevel",
      0,
      "entries",
      0
    ],
    "See the {@book Monster Manual|SRD52} for these stat blocks.",
    "See “Monsters” for these stat blocks.",
    "See “Monsters” for these stat blocks."
  ],
  [
    "spell.find-familiar",
    [
      "entries",
      0
    ],
    "statistics of the chosen form, though",
    "statistics of the chosen form (see “Monsters”), though",
    "statistics of the chosen form (see “Monsters”), though"
  ],
  [
    "spell.finger-of-death",
    [
      "entries",
      1
    ],
    "{@creature Zombie|SRD52} that follows",
    "{@creature Zombie|SRD52} (see “Monsters”) that follows",
    "Zombie (see “Monsters”) that follows"
  ],
  [
    "spell.phantom-steed",
    [
      "entries",
      1
    ],
    "stat block, except",
    "stat block (see “Monsters”), except",
    "stat block (see “Monsters”), except"
  ],
  [
    "spell.plane-shift",
    [
      "entries",
      0
    ],
    "the City of Brass on the Elemental Plane of Fire or the palace of Dispater on the second level of the Nine Hells",
    "a specific city on the Elemental Plane of Fire or palace on the second level of the Nine Hells",
    "a specific city on the Elemental Plane of Fire or palace on the second level of the Nine Hells"
  ],
  [
    "spell.plane-shift",
    [
      "entries",
      0
    ],
    "determined by the DM.",
    "determined by the GM.",
    "determined by the GM."
  ],
  [
    "spell.polymorph",
    [
      "entries",
      0
    ],
    "or shape-shift into Beast form",
    "or shape-shift into a Beast form",
    "or shape-shift into a Beast form"
  ],
  [
    "spell.polymorph",
    [
      "entries",
      0
    ],
    "{@variantrule Hit Point Dice|SRD52}.",
    "{@variantrule Hit Point Dice|SRD52}. See the “Animals” section of “Monsters” for a sample of Beast stat blocks.",
    "See the “Animals” section of “Monsters” for a sample of Beast stat blocks."
  ],
  [
    "spell.telekinesis",
    [
      "entries",
      2,
      "entries",
      2
    ],
    "manipulating a simple tool, opening a door or a container, stowing or retrieving an item from an open container, or pouring the contents from a vial.",
    "manipulating a simple tool,",
    "telekinetic grip, such as manipulating a simple tool,"
  ]
];
        const applicable=corrections.filter(([id])=>id===row.id);
        if(applicable.length){
            // Clone only the edited bodies, never mutate the pinned candidates.
            result.entries=JSON.parse(JSON.stringify(result.entries));
            if(result.entriesHigherLevel)result.entriesHigherLevel=JSON.parse(JSON.stringify(result.entriesHigherLevel));
            const official=row.officialTextLayout.replace(/[’‘]/g,"'").replace(/([a-zA-Z])-\s*\n\s*(?=[a-zA-Z])/g,"$1").replace(/\s+/g," ");
            for(const [id,path,from,to,evidence] of applicable){
                if(!official.includes(evidence))throw Error("Publisher evidence changed: "+id);
                let target=result;
                for(const key of path.slice(0,-1))target=target[key];
                const key=path.at(-1);
                if(typeof target[key]!=="string"||target[key].split(from).length!==2)throw Error("Source entry changed: "+id);
                target[key]=target[key].replace(from,to);
            }
        }
        if(row.id==="spell.telekinesis"&&!row.officialTextLayout.endsWith("telekinetic grip, such as manipulating a simple tool,"))throw Error("Publisher Telekinesis boundary changed");
    }
    result.source = {...row.source, structuredSource: pointer(manifest, tag, "spell", index),
        structuredStatus: tag==="52"&&row.id==="spell.telekinesis"
            ? "publisher-name-level-school-verified; publisher Telekinesis text visibly ends mid-sentence on p.168; unprinted continuation excluded; source gap remains"
            : "publisher-name-level-school-verified; structured body review pending"};
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
