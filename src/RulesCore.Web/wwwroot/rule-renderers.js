import { codeBlock, definitionList, element } from "./ui.js";

const rendererRegistry = new Map();

for (const type of ["monster"]) rendererRegistry.set(type, renderMonster);
for (const type of ["spell"]) rendererRegistry.set(type, renderSpell);
for (const type of ["item"]) rendererRegistry.set(type, renderItem);
for (const type of ["feat"]) rendererRegistry.set(type, renderFeat);
for (const type of ["class", "subclass", "classFeature", "subclassFeature", "prestigeClass", "npcClass"]) rendererRegistry.set(type, renderClassLike);
for (const type of ["race", "species"]) rendererRegistry.set(type, renderSpeciesLike);
for (const type of ["condition"]) rendererRegistry.set(type, renderCondition);
for (const type of ["skill", "domain", "power", "divineAbility", "houseRule", "rule", "source-fragment"]) rendererRegistry.set(type, renderRulesDocument);

export function renderRuleDocument(entityType, document, options = {}) {
    const normalizedType = String(entityType ?? "").trim();
    const renderer = rendererRegistry.get(normalizedType) ?? rendererRegistry.get(normalizedType.toLowerCase()) ?? renderRulesDocument;
    return renderer(document ?? {}, options);
}

export const renderResolvedRule = renderRuleDocument;

export function projectMonster(document) {
    const legacy = legacyFieldMap(document?.body);
    const abilities = {};
    for (const [key, legacyLabel] of [["STR", "Str"], ["DEX", "Dex"], ["CON", "Con"], ["INT", "Int"], ["WIS", "Wis"], ["CHA", "Cha"]]) {
        abilities[key] = numberValue(document?.[key.toLowerCase()]) ?? numberValue(legacy.get(legacyLabel));
    }

    return {
        armorClass: firstValue(formatArmorClass(document?.ac), legacy.get("Armor Class"), legacy.get("AC")),
        hitPoints: firstValue(formatHitPoints(document?.hp), legacy.get("Hit Points"), legacy.get("HP")),
        hitDice: firstValue(document?.hp?.formula, legacy.get("Hit Dice")),
        initiative: firstValue(formatValue(document?.initiative), formatValue(document?.init), legacy.get("Initiative"), legacy.get("Init"), abilityModifier(abilities.DEX)),
        speed: firstValue(formatSpeed(document?.speed), legacy.get("Speed")),
        challengeRating: firstValue(formatChallenge(document?.cr), legacy.get("Challenge Rating"), legacy.get("CR")),
        proficiencyBonus: firstValue(formatSigned(document?.pb), formatSigned(document?.proficiencyBonus)),
        abilities
    };
}

function renderMonster(document, options) {
    const root = element("div", { className: "rules-core-rule-renderer rules-core-monster-sheet" });
    const stats = projectMonster(document);
    const kaiju = projectKaiju(document);

    const header = element("section", { className: "rules-core-dnd-block rules-core-monster-summary" });
    header.append(
        element("div", { className: "rules-core-stat-line" },
            stat("Armor Class", stats.armorClass),
            stat("Hit Points", stats.hitPoints),
            stat("Hit Dice", stats.hitDice),
            stat("Initiative", stats.initiative),
            stat("Speed", stats.speed),
            stat("Challenge", stats.challengeRating)),
        abilityRow(stats.abilities));
    root.append(header);

    const defenses = compactPairs([
        ["Saving Throws", formatValue(document?.save)],
        ["Skills", formatValue(document?.skill)],
        ["Damage Vulnerabilities", formatValue(document?.vulnerable)],
        ["Damage Resistances", formatValue(document?.resist)],
        ["Damage Immunities", formatValue(document?.immune)],
        ["Condition Immunities", formatValue(document?.conditionImmune)],
        ["Senses", formatValue(document?.senses)],
        ["Languages", formatValue(document?.languages)],
        ["Proficiency Bonus", stats.proficiencyBonus]
    ]);
    if (defenses.length) root.append(infoSection("Defenses & Senses", defenses));

    if (kaiju) root.append(renderKaiju(kaiju));

    appendNamedSections(root, document, [
        ["trait", "Traits"],
        ["spellcasting", "Spellcasting"],
        ["action", "Actions"],
        ["bonus", "Bonus Actions"],
        ["reaction", "Reactions"],
        ["legendary", "Legendary Actions"],
        ["mythic", "Mythic Actions"]
    ]);
    appendLegacyBody(root, document, options);
    return root;
}

function renderSpell(document, options) {
    const root = element("article", { className: "rules-core-rule-renderer rules-core-reading-view" });
    root.append(infoSection("Spell details", compactPairs([
        ["Level", formatValue(document?.level)],
        ["School", formatValue(document?.school)],
        ["Casting Time", formatValue(document?.time ?? document?.castingTime)],
        ["Range", formatValue(document?.range)],
        ["Components", formatValue(document?.components)],
        ["Duration", formatValue(document?.duration)],
        ["Classes", formatValue(document?.classes)],
        ["Saving Throw", formatValue(document?.savingThrow)],
        ["Attack", formatValue(document?.spellAttack)]
    ])));
    appendEntryBody(root, document);
    appendLegacyBody(root, document, options);
    return root;
}

function renderItem(document, options) {
    const root = element("article", { className: "rules-core-rule-renderer rules-core-reading-view" });
    root.append(infoSection("Item details", compactPairs([
        ["Type", formatValue(document?.type)],
        ["Rarity", formatValue(document?.rarity)],
        ["Attunement", formatAttunement(document?.reqAttune)],
        ["Armor Class", formatValue(document?.ac)],
        ["Damage", formatValue(document?.dmg1 ?? document?.damage)],
        ["Properties", formatValue(document?.property)],
        ["Weight", formatValue(document?.weight)],
        ["Value", formatValue(document?.value ?? document?.price)]
    ])));
    appendEntryBody(root, document);
    appendLegacyBody(root, document, options);
    return root;
}

function renderFeat(document, options) {
    const root = element("article", { className: "rules-core-rule-renderer rules-core-reading-view" });
    const prerequisites = formatValue(document?.prerequisite ?? document?.prerequisites);
    if (prerequisites) root.append(callout("Prerequisites", prerequisites));
    appendEntryBody(root, document);
    appendLegacyBody(root, document, options);
    return root;
}

function renderClassLike(document, options) {
    const root = element("article", { className: "rules-core-rule-renderer rules-core-reading-view" });
    const details = compactPairs([
        ["Hit Die", formatValue(document?.hd ?? document?.hitDie)],
        ["Primary Ability", formatValue(document?.primaryAbility)],
        ["Saving Throws", formatValue(document?.proficiency ?? document?.savingThrows)],
        ["Armor Training", formatValue(document?.armorProficiencies)],
        ["Weapon Proficiencies", formatValue(document?.weaponProficiencies)],
        ["Spellcasting Ability", formatValue(document?.spellcastingAbility)],
        ["Requirements", formatValue(document?.requirements ?? document?.prerequisite)]
    ]);
    if (details.length) root.append(infoSection("Class details", details));
    appendNamedSections(root, document, [
        ["classFeatures", "Class Features"],
        ["subclassFeatures", "Subclass Features"],
        ["features", "Features"]
    ]);
    appendEntryBody(root, document);
    appendLegacyBody(root, document, options);
    return root;
}

function renderSpeciesLike(document, options) {
    const root = element("article", { className: "rules-core-rule-renderer rules-core-reading-view" });
    const details = compactPairs([
        ["Creature Type", formatValue(document?.creatureTypes ?? document?.creatureType)],
        ["Size", formatValue(document?.size)],
        ["Speed", formatSpeed(document?.speed)],
        ["Languages", formatValue(document?.languageProficiencies ?? document?.languages)],
        ["Ability Scores", formatValue(document?.ability)]
    ]);
    if (details.length) root.append(infoSection("Ancestry details", details));
    appendEntryBody(root, document);
    appendLegacyBody(root, document, options);
    return root;
}

function renderCondition(document, options) {
    const root = element("article", { className: "rules-core-rule-renderer rules-core-reading-view" });
    appendEntryBody(root, document);
    appendLegacyBody(root, document, options);
    return root;
}

function renderRulesDocument(document, options) {
    const root = element("article", { className: "rules-core-rule-renderer rules-core-reading-view" });
    appendEntryBody(root, document);
    appendLegacyBody(root, document, options);
    if (!root.children.length) {
        root.append(element("p", { className: "text-body-secondary", text: "This source record has no structured presentation fields." }));
    }
    return root;
}

function projectKaiju(document) {
    const explicit = document?.kaiju === true
        || document?.isKaiju === true
        || String(document?.rulesVariant ?? "").toLowerCase().includes("kaiju")
        || String(document?.statBlockType ?? "").toLowerCase().includes("kaiju");
    const vulnerableAreas = document?.vulnerableAreas ?? document?.vulnerableArea ?? document?.weakPoints;
    const thresholds = document?.thresholds ?? document?.damageThresholds ?? document?.stateThresholds;
    const states = document?.states ?? document?.behaviorStates ?? document?.phases;
    const kaijuActions = document?.kaijuActions ?? document?.colossalActions;
    if (!explicit && !vulnerableAreas && !thresholds && !states && !kaijuActions) return null;
    return { vulnerableAreas, thresholds, states, actions: kaijuActions };
}

function renderKaiju(kaiju) {
    const section = element("section", { className: "rules-core-dnd-section rules-core-kaiju-variant" });
    section.append(
        element("div", { className: "rules-core-section-heading" },
            element("h4", { text: "Kaiju Fighting" }),
            element("span", { className: "badge text-bg-warning", text: "Variant" })));
    const pairs = compactPairs([
        ["Vulnerable Areas", formatValue(kaiju.vulnerableAreas)],
        ["Thresholds", formatValue(kaiju.thresholds)],
        ["States / Phases", formatValue(kaiju.states)]
    ]);
    if (pairs.length) section.append(definitionList(pairs));
    if (Array.isArray(kaiju.actions) && kaiju.actions.length) section.append(renderNamedEntries("Kaiju Actions", kaiju.actions));
    return section;
}

function appendNamedSections(root, document, mappings) {
    for (const [property, title] of mappings) {
        const entries = document?.[property];
        if (Array.isArray(entries) && entries.length) root.append(renderNamedEntries(title, entries));
    }
}

function appendEntryBody(root, document) {
    const entries = document?.entries ?? document?.entry ?? document?.items;
    if (!entries) return;
    const text = formatRuleText(entries);
    if (text) root.append(element("section", { className: "rules-core-dnd-section" }, richParagraphs(text)));
}

function appendLegacyBody(root, document, options) {
    const body = typeof document?.body === "string" ? document.body.trim() : "";
    if (!body) return;
    const normalized = stripMarkup(body);
    if (!normalized) return;
    root.append(element("section", { className: "rules-core-dnd-section rules-core-source-text" },
        options.legacyBodyTitle === false ? null : element("h4", { text: "Source Text" }),
        richParagraphs(normalized)));
}

function renderNamedEntries(title, entries) {
    const section = element("section", { className: "rules-core-dnd-section" });
    section.append(element("h4", { text: title }));
    for (const entry of entries) {
        const block = element("div", { className: "rules-core-rule-entry" });
        if (entry && typeof entry === "object" && !Array.isArray(entry) && entry.name) {
            block.append(element("h5", { text: entry.name }));
        }
        const text = formatRuleText(entry?.entries ?? entry?.entry ?? entry);
        if (text) block.append(richParagraphs(text));
        section.append(block);
    }
    return section;
}

function infoSection(title, pairs) {
    const section = element("section", { className: "rules-core-dnd-section rules-core-info-section" });
    section.append(element("h4", { text: title }));
    if (pairs.length) section.append(definitionList(pairs));
    return section;
}

function callout(label, value) {
    return element("div", { className: "rules-core-rule-callout" },
        element("strong", { text: `${label}. ` }),
        value);
}

function abilityRow(abilities) {
    const row = element("div", { className: "rules-core-ability-row" });
    for (const key of ["STR", "DEX", "CON", "INT", "WIS", "CHA"]) {
        const score = abilities[key];
        const modifier = typeof score === "number" ? Math.floor((score - 10) / 2) : null;
        row.append(element("div", { className: "rules-core-ability" },
            element("span", { className: "rules-core-ability-name", text: key }),
            element("span", {
                className: "rules-core-ability-value",
                text: score === null || score === undefined ? "—" : `${score} (${signed(modifier)})`
            })));
    }
    return row;
}

function stat(label, value) {
    return element("div", { className: "rules-core-stat" },
        element("span", { className: "rules-core-stat-label", text: label }),
        element("span", { className: "rules-core-stat-value", text: value ?? "—" }));
}

function richParagraphs(text) {
    const fragment = element("div", { className: "rules-core-prose" });
    for (const paragraph of String(text).split(/\n{2,}/).map(value => value.trim()).filter(Boolean)) {
        fragment.append(element("p", { text: paragraph }));
    }
    return fragment;
}

function legacyFieldMap(body) {
    const text = stripMarkup(body);
    const labels = [
        "Armor Class", "Hit Points", "Hit Dice", "Initiative", "Challenge Rating",
        "SizeAndType", "Size and Type", "Speed", "AC", "HP", "Init", "CR",
        "Str", "Dex", "Con", "Int", "Wis", "Cha"
    ];
    const positions = [];
    const lower = text.toLowerCase();
    for (const label of labels) {
        const needle = label.toLowerCase();
        let start = 0;
        while (start < lower.length) {
            const index = lower.indexOf(needle, start);
            if (index < 0) break;
            const before = index === 0 ? " " : lower[index - 1];
            const afterIndex = index + needle.length;
            const after = afterIndex >= lower.length ? " " : lower[afterIndex];
            if (!isWordChar(before) && !isWordChar(after)) positions.push({ index, label, end: afterIndex });
            start = index + needle.length;
        }
    }
    positions.sort((a, b) => a.index - b.index || b.label.length - a.label.length);
    const deduped = positions.filter((value, index, array) => index === 0 || value.index !== array[index - 1].index);
    const result = new Map();
    for (let index = 0; index < deduped.length; index += 1) {
        const current = deduped[index];
        const next = deduped[index + 1];
        let value = text.slice(current.end, next?.index ?? text.length).trim();
        value = value.replace(/^\s*[:=]\s*/, "").trim();
        if (value) result.set(current.label, value);
    }
    return result;
}

function stripMarkup(value) {
    if (!value || typeof value !== "string") return "";
    const wrapper = document.createElement("div");
    wrapper.innerHTML = value
        .replace(/<\/(?:p|div|tr|td|th|li|h[1-6])>/gi, "\n")
        .replace(/<br\s*\/?\s*>/gi, "\n");
    return (wrapper.textContent ?? "")
        .replace(/\r/g, "")
        .replace(/[ \t]+/g, " ")
        .replace(/\n[ \t]+/g, "\n")
        .replace(/\n{3,}/g, "\n\n")
        .trim();
}

function isWordChar(character) {
    return /[a-z0-9]/i.test(character ?? "");
}

function compactPairs(values) {
    return values.filter(([, value]) => value !== null && value !== undefined && value !== "");
}

function formatArmorClass(value) {
    if (Array.isArray(value)) return value.map(item => item && typeof item === "object" ? item.ac ?? formatValue(item) : item).join(", ");
    return formatValue(value);
}

function formatHitPoints(value) {
    if (value === null || value === undefined) return null;
    if (typeof value !== "object") return String(value);
    if (value.average !== undefined && value.formula) return `${value.average} (${value.formula})`;
    if (value.average !== undefined) return String(value.average);
    return formatValue(value);
}

function formatChallenge(value) {
    if (value && typeof value === "object" && !Array.isArray(value)) return firstValue(value.cr, value.lair, formatValue(value));
    return formatValue(value);
}

function formatSpeed(value) {
    if (value === null || value === undefined) return null;
    if (typeof value === "number" || typeof value === "string") return String(value);
    if (Array.isArray(value)) return value.map(formatSpeed).filter(Boolean).join(", ");
    return Object.entries(value)
        .filter(([, candidate]) => candidate !== false && candidate !== null && candidate !== undefined)
        .map(([key, candidate]) => `${humanize(key)} ${typeof candidate === "number" ? `${candidate} ft.` : formatValue(candidate)}`)
        .join(", ");
}

function formatAttunement(value) {
    if (value === true) return "Required";
    if (value === false || value === null || value === undefined) return null;
    return String(value);
}

function formatSigned(value) {
    const number = numberValue(value);
    return number === null ? formatValue(value) : signed(number);
}

function formatRuleText(value) {
    if (value === null || value === undefined) return "";
    if (["string", "number", "boolean"].includes(typeof value)) return String(value);
    if (Array.isArray(value)) return value.map(formatRuleText).filter(Boolean).join("\n\n");
    if (typeof value === "object") {
        if (value.entries) return formatRuleText(value.entries);
        if (value.entry) return formatRuleText(value.entry);
        if (value.items) return formatRuleText(value.items);
        return Object.entries(value)
            .filter(([key]) => !["name", "type"].includes(key))
            .map(([, candidate]) => formatRuleText(candidate))
            .filter(Boolean)
            .join("\n\n");
    }
    return String(value);
}

function formatValue(value) {
    if (value === null || value === undefined) return null;
    if (["string", "number", "boolean"].includes(typeof value)) return String(value);
    if (Array.isArray(value)) return value.map(formatValue).filter(Boolean).join(", ");
    return Object.entries(value)
        .filter(([, candidate]) => candidate !== false && candidate !== null && candidate !== undefined)
        .map(([key, candidate]) => candidate === true ? humanize(key) : `${humanize(key)} ${formatValue(candidate)}`)
        .join(", ");
}

function numberValue(value) {
    if (typeof value === "number" && Number.isFinite(value)) return value;
    if (typeof value === "string") {
        const parsed = Number(value.trim());
        return Number.isFinite(parsed) ? parsed : null;
    }
    return null;
}

function abilityModifier(score) {
    if (typeof score !== "number") return null;
    return `${signed(Math.floor((score - 10) / 2))} (DEX)`;
}

function signed(value) {
    return `${value >= 0 ? "+" : ""}${value}`;
}

function firstValue(...values) {
    return values.find(value => value !== null && value !== undefined && value !== "") ?? null;
}

function humanize(value) {
    return String(value ?? "")
        .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
        .replace(/[-_]+/g, " ")
        .replace(/^./, character => character.toUpperCase());
}

export function rawDocumentDisclosure(document, title = "Raw immutable source document") {
    const raw = element("details", { className: "card card-body rules-core-raw-source" });
    raw.append(
        element("summary", { className: "fw-semibold", text: title }),
        element("div", { className: "pt-3" }, codeBlock(document)));
    return raw;
}
