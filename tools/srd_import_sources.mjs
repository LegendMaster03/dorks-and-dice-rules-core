import fs from "node:fs";
import path from "node:path";
import { isDeepStrictEqual } from "node:util";

// Keep authoring diagnostics in data/reference, outside the staged runtime catalog.
const citationKeys = new Set(["release", "publisher", "url", "pageUrl", "license", "attribution", "pages", "section"]);
const nativePath = /^data\/rulesets\/(dnd-5e|dnd-5-5e)\/native\/(.+\.json)$/;
const json = value => JSON.stringify(value, null, 2) + "\n";

function splitSource(source) {
    const citation = {};
    const evidence = {};
    for (const [key, value] of Object.entries(source)) {
        if (citationKeys.has(key)) citation[key] = value;
        else if (key === "spellList" && value.pages) {
            const { pages, ...details } = value;
            citation.spellList = { pages };
            evidence.spellList = details;
        } else evidence[key] = value;
    }
    return [citation, evidence];
}

export function srdImportFiles(root) {
    const indexes = new Map();
    function readJson(file) {
        return JSON.parse(fs.readFileSync(path.join(root, file), "utf8"));
    }

    function indexFile(packageId) {
        return "data/reference/srd/" + packageId + "-import-sources.json";
    }

    function readIndex(packageId) {
        if (!indexes.has(packageId)) {
            const file = indexFile(packageId);
            indexes.set(packageId, fs.existsSync(path.join(root, file)) ? readJson(file) : {});
        }
        return indexes.get(packageId);
    }

    function read(file) {
        const rows = readJson(file);
        const match = nativePath.exec(file);
        if (!match || !Array.isArray(rows)) return rows;
        const evidence = readIndex(match[1])[match[2]] ?? {};
        return rows.map(row => {
            const metadata = evidence[row.id];
            if (!metadata) return row;
            const source = { ...row.source, ...metadata };
            if (row.source.spellList && metadata.spellList)
                source.spellList = { ...row.source.spellList, ...metadata.spellList };
            return { ...row, source };
        });
    }

    function write(file, body, check) {
        const target = path.join(root, file);
        const match = nativePath.exec(file);
        if (!match) {
            if (check) {
                if (fs.readFileSync(target, "utf8") !== body)
                    throw new Error("Stale generated file " + file);
            } else {
                fs.mkdirSync(path.dirname(target), { recursive: true });
                fs.writeFileSync(target, body);
            }
            return;
        }

        const evidence = {};
        const rows = JSON.parse(body).map(row => {
            if (!row.source || typeof row.source !== "object") return row;
            const [citation, metadata] = splitSource(row.source);
            if (Object.keys(metadata).length) evidence[row.id] = metadata;
            return { ...row, source: citation };
        });
        const index = readIndex(match[1]);
        if (check) {
            if (!isDeepStrictEqual(readJson(file), rows) || !isDeepStrictEqual(index[match[2]] ?? {}, evidence))
                throw new Error("Stale native rules or import evidence in " + file);
            return;
        }

        if (Object.keys(evidence).length) index[match[2]] = evidence;
        else delete index[match[2]];
        fs.mkdirSync(path.dirname(target), { recursive: true });
        fs.writeFileSync(target, json(rows));
        fs.writeFileSync(path.join(root, indexFile(match[1])), json(Object.fromEntries(
            Object.entries(index).sort(([left], [right]) => left.localeCompare(right))
        )));
    }

    return { read, write };
}
