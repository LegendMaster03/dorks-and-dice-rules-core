import { codeBlock, element } from "./ui.js";

const ABILITIES = [
    ["STR", "str"],
    ["DEX", "dex"],
    ["CON", "con"],
    ["INT", "int"],
    ["WIS", "wis"],
    ["CHA", "cha"]
];

const SIZE_LABELS = new Map([
    ["T", "Tiny"],
    ["S", "Small"],
    ["M", "Medium"],
    ["L", "Large"],
    ["H", "Huge"],
    ["G", "Gargantuan"]
]);

const ALIGNMENT_LABELS = new Map([
    ["L", "Lawful"],
    ["N", "Neutral"],
    ["C", "Chaotic"],
    ["G", "Good"],
    ["E", "Evil"],
    ["U", "Unaligned"],
    ["A", "Any alignment"]
]);

const MONSTER_DETAIL_FIELDS = [
    ["Skills", "skill"],
    ["Senses", "senses"],
    ["Languages", "languages"],
    ["Challenge Rating", "cr"],
    ["XP", "xp"],
    ["Proficiency Bonus", "pb"],
    ["Proficiency Bonus", "proficiencyBonus"],
    ["Damage Vulnerabilities", "vulnerable"],
    ["Damage Resistances", "resist"],
    ["Damage Immunities", "immune"],
    ["Condition Immunities", "conditionImmune"],
    ["Gear", "gear"]
];

const MONSTER_SECTIONS = [
    ["entries", "Description"],
    ["trait", "Traits"],
    ["spellcasting", "Spellcasting"],
    ["action", "Actions"],
    ["bonus", "Bonus Actions"],
    ["reaction", "Reactions"],
    ["legendary", "Legendary Actions"],
    ["mythic", "Mythic Actions"],
    ["lairActions", "Lair Actions"],
    ["lair", "Lair Actions"],
    ["regionalEffects", "Regional Effects"],
    ["regional", "Regional Effects"]
];

const PRESENTATION_METADATA_FIELDS = new Set([
    "name",
    "source",
    "page",
    "otherSources",
    "additionalSources",
    "reprintedAs",
    "srd",
    "basicRules",
    "hasToken",
    "hasFluff",
    "hasFluffImages",
    "tokenUrl",
    "legendaryGroup",
    "_copy",
    "_versions"
]);

const renderers = new Map([
    ["monster", renderMonster]
]);

export function renderResolvedRule(entityType, document, options = {}) {
    const renderer = renderers.get(String(entityType ?? "").toLowerCase()) ?? renderGeneric;
    return renderer(document, options);
}

export function renderEntityHeader({ name, subtitle = null, tags = [] } = {}) {
    const header = element("header", { className: "rules-core-entity-header" });
    header.append(element("h3", {
        className: "rules-core-entity-title",
        text: name || "Unnamed rule"
    }));
    if (subtitle) {
        header.append(element("div", {
            className: "rules-core-entity-subtitle",
            text: subtitle
        }));
    }
    const visibleTags = tags.filter(value => value !== null && value !== undefined && String(value).trim());
    if (visibleTags.length) {
        header.append(element("div", { className: "rules-core-tag-row" },
            visibleTags.map(value => element("span", {
                className: "rules-core-rule-tag",
                text: String(value)
            }))));
    }
    return header;
}

export function renderCompactStatistic(label, value) {
    if (!hasValue(value)) return null;
    return element("div", { className: "rules-core-compact-stat" },
        element("div", { className: "rules-core-compact-stat-label", text: label }),
        element("div", { className: "rules-core-compact-stat-value", text: String(value) }));
}

export function renderNamedRuleEntry(entry) {
    const item = element("div", { className: "rules-core-rule-entry" });
    if (entry && typeof entry === "object" && !Array.isArray(entry) && entry.name) {
        item.append(element("div", {
            className: "rules-core-rule-entry-name",
            text: stripRendererTags(entry.name)
        }));
    }
    if (entry && typeof entry === "object" && !Array.isArray(entry)) {
        const bodies = [];
        for (const candidate of [entry.entries, entry.entry, entry.items, entry.attackEntries, entry.hitEntries, entry.headerEntries, entry.footerEntries]) {
            if (candidate !== null && candidate !== undefined) bodies.push(candidate);
        }
        if (bodies.length) {
            for (const body of bodies) item.append(renderRuleContent(body));
        } else {
            const fallback = Object.fromEntries(Object.entries(entry).filter(([key]) => key !== "name"));
            if (Object.keys(fallback).length) item.append(renderRuleContent(fallback));
        }
    } else if (entry !== null && entry !== undefined) {
        item.append(renderRuleContent(entry));
    }
    return item;
}

function renderMonster(document = {}, options = {}) {
    const root = element("article", { className: "rules-core-rule-renderer rules-core-monster-stat-block" });
    const consumed = new Set();
    const displayName = options.displayName || document?.name || "Monster";

    consumed.add("name");
    const size = formatSize(document?.size);
    const creatureType = formatCreatureType(document?.type);
    const alignment = formatAlignment(document?.alignment);
    for (const key of ["size", "type", "alignment"]) consumed.add(key);

    const subtitle = [size, creatureType, alignment].filter(Boolean).join(" · ");
    const tags = collectMonsterTags(document);
    consumed.add("tags");
    root.append(renderEntityHeader({ name: displayName, subtitle, tags }));

    const combatStats = [
        renderCompactStatistic("Armor Class", formatArmorClass(document?.ac)),
        renderCompactStatistic("Hit Points", formatHitPoints(document?.hp)),
        renderCompactStatistic("Speed", formatSpeed(document?.speed)),
        renderCompactStatistic("Initiative", formatInitiative(firstDefined(document?.initiative, document?.init)))
    ].filter(Boolean);
    for (const key of ["ac", "hp", "speed", "initiative", "init"]) consumed.add(key);
    if (combatStats.length) {
        root.append(element("section", { className: "rules-core-monster-combat" }, combatStats));
    }

    root.append(renderAbilityGrid(document));
    for (const [, key] of ABILITIES) consumed.add(key);
    consumed.add("save");

    const details = [];
    const seenDetailLabels = new Set();
    for (const [label, key] of MONSTER_DETAIL_FIELDS) {
        consumed.add(key);
        const value = key === "cr" ? formatChallenge(document?.[key]) : formatDetailValue(document?.[key]);
        if (!hasValue(value) || seenDetailLabels.has(label)) continue;
        seenDetailLabels.add(label);
        details.push([label, value]);
    }
    if (details.length) {
        root.append(renderLabeledDetails(details));
    }

    const sectionsByTitle = new Map();
    for (const [property, title] of MONSTER_SECTIONS) {
        consumed.add(property);
        const entries = document?.[property];
        if (!hasSectionContent(entries)) continue;
        const current = sectionsByTitle.get(title) ?? [];
        if (Array.isArray(entries)) current.push(...entries);
        else current.push(entries);
        sectionsByTitle.set(title, current);
    }
    for (const [title, entries] of sectionsByTitle.entries()) {
        root.append(renderRulesTextSection(title, entries));
    }

    const extensionSections = renderRulesCoreExtensions(document?._rulesCore);
    consumed.add("_rulesCore");
    root.append(...extensionSections);

    const extras = Object.entries(document ?? {})
        .filter(([key, value]) => !consumed.has(key)
            && !PRESENTATION_METADATA_FIELDS.has(key)
            && !key.startsWith("_")
            && hasValue(value));
    if (extras.length) {
        root.append(renderAdditionalMechanics(extras));
    }

    if (options.showDocument !== false) {
        const raw = element("details", { className: "rules-core-secondary-details" });
        raw.append(
            element("summary", { text: options.documentLabel ?? "Normalized rule document" }),
            element("div", { className: "rules-core-secondary-details-body" }, codeBlock(document)));
        root.append(raw);
    }
    return root;
}

function renderGeneric(document, options = {}) {
    const raw = element("details", {
        className: "card card-body rules-core-secondary-details",
        attributes: { open: "" }
    });
    raw.append(
        element("summary", { className: "fw-semibold", text: options.documentLabel ?? "Resolved rule" }),
        element("div", { className: "mt-3" }, codeBlock(document)));
    return raw;
}

function renderAbilityGrid(document) {
    const section = element("section", { className: "rules-core-monster-section rules-core-ability-section" });
    section.append(element("h4", { className: "rules-core-monster-section-title", text: "Ability Scores" }));
    const grid = element("div", { className: "rules-core-ability-score-grid" });
    const saves = document?.save && typeof document.save === "object" && !Array.isArray(document.save)
        ? document.save
        : {};

    for (const [label, key] of ABILITIES) {
        const score = finiteNumber(document?.[key]);
        const modifier = score === null ? null : Math.floor((score - 10) / 2);
        const explicitSave = saves?.[key];
        const save = hasValue(explicitSave) ? formatSignedValue(explicitSave) : formatSignedValue(modifier);
        grid.append(element("div", { className: "rules-core-ability-score" },
            element("div", { className: "rules-core-ability-name", text: label }),
            element("div", { className: "rules-core-ability-score-values" },
                abilityDatum("Score", score === null ? "—" : score),
                abilityDatum("Mod", modifier === null ? "—" : formatSignedValue(modifier)),
                abilityDatum("Save", save ?? "—"))));
    }
    section.append(grid);
    return section;
}

function abilityDatum(label, value) {
    return element("div", { className: "rules-core-ability-datum" },
        element("span", { text: label }),
        element("strong", { text: String(value) }));
}

function renderLabeledDetails(items) {
    const section = element("section", { className: "rules-core-monster-details rules-core-monster-section" });
    const list = element("dl", { className: "rules-core-labeled-details" });
    for (const [label, value] of items) {
        list.append(
            element("dt", { text: label }),
            element("dd", { text: String(value) }));
    }
    section.append(list);
    return section;
}

function renderRulesTextSection(title, entries) {
    const section = element("section", { className: "rules-core-monster-section" });
    section.append(element("h4", { className: "rules-core-monster-section-title", text: title }));
    const body = element("div", { className: "rules-core-rules-text" });
    if (Array.isArray(entries)) {
        for (const entry of entries) body.append(renderNamedRuleEntry(entry));
    } else {
        body.append(renderNamedRuleEntry(entries));
    }
    section.append(body);
    return section;
}

function renderAdditionalMechanics(entries) {
    const section = element("section", { className: "rules-core-monster-section rules-core-additional-mechanics" });
    section.append(element("h4", { className: "rules-core-monster-section-title", text: "Additional Mechanics" }));
    const list = element("div", { className: "rules-core-additional-mechanics-list" });
    for (const [key, value] of entries) {
        list.append(element("div", { className: "rules-core-additional-mechanic" },
            element("div", { className: "rules-core-additional-mechanic-label", text: humanizeKey(key) }),
            renderRuleContent(value)));
    }
    section.append(list);
    return section;
}

function renderRulesCoreExtensions(extension) {
    if (!extension || typeof extension !== "object" || Array.isArray(extension)) return [];
    const sections = [];
    const pcgenSegments = extension?.pcgen?.unmappedSegments;
    if (Array.isArray(pcgenSegments) && pcgenSegments.length) {
        const rows = pcgenSegments
            .filter(segment => segment && typeof segment === "object")
            .map(segment => [String(segment.tag ?? "Source field"), segment.value]);
        if (rows.length) {
            const section = element("section", { className: "rules-core-monster-section rules-core-source-mechanics" });
            section.append(element("h4", { className: "rules-core-monster-section-title", text: "Source-Specific Mechanics" }));
            const list = element("dl", { className: "rules-core-labeled-details" });
            for (const [tag, value] of rows) {
                list.append(element("dt", { text: tag }), element("dd", {}, renderRuleContent(value)));
            }
            section.append(list);
            sections.push(section);
        }
    }

    const remaining = Object.entries(extension)
        .filter(([key, value]) => !["context", "pcgen", "competencyConversion", "exactCompetencyIdentity"].includes(key)
            && hasValue(value));
    if (remaining.length) {
        const section = renderAdditionalMechanics(remaining);
        section.querySelector(".rules-core-monster-section-title").textContent = "Dorks & Dice Mechanics";
        sections.push(section);
    }
    return sections;
}

function renderRuleContent(value) {
    const wrapper = element("div", { className: "rules-core-rule-content" });
    appendRuleContent(wrapper, value);
    return wrapper;
}

function appendRuleContent(parent, value) {
    if (value === null || value === undefined) return;
    if (typeof value === "string" || typeof value === "number" || typeof value === "boolean") {
        parent.append(element("span", { text: stripRendererTags(String(value)) }));
        return;
    }
    if (Array.isArray(value)) {
        value.forEach((item, index) => {
            if (index > 0 && isInlineRuleValue(item)) parent.append(document.createTextNode(" "));
            if (isInlineRuleValue(item)) {
                appendRuleContent(parent, item);
            } else {
                parent.append(renderNamedRuleEntry(item));
            }
        });
        return;
    }
    if (typeof value !== "object") {
        parent.append(element("span", { text: String(value) }));
        return;
    }

    if (value.type === "list" && Array.isArray(value.items)) {
        const list = element("ul", { className: "rules-core-rule-list" });
        for (const item of value.items) {
            list.append(element("li", {}, renderRuleContent(item?.entry ?? item?.entries ?? item)));
        }
        parent.append(list);
        return;
    }
    if (value.type === "table" && Array.isArray(value.rows)) {
        parent.append(renderRuleTable(value));
        return;
    }
    const nested = firstDefined(value.entries, value.entry, value.items, value.attackEntries, value.hitEntries);
    if (nested !== null && nested !== undefined) {
        if (value.name) {
            parent.append(element("strong", { className: "rules-core-inline-entry-name", text: stripRendererTags(value.name) }));
            parent.append(document.createTextNode(" "));
        }
        appendRuleContent(parent, nested);
        return;
    }

    const pairs = Object.entries(value)
        .filter(([key, candidate]) => !["name", "type"].includes(key) && hasValue(candidate));
    if (!pairs.length) return;
    const list = element("dl", { className: "rules-core-inline-details" });
    for (const [key, candidate] of pairs) {
        list.append(element("dt", { text: humanizeKey(key) }), element("dd", {}, renderRuleContent(candidate)));
    }
    parent.append(list);
}

function renderRuleTable(table) {
    const wrapper = element("div", { className: "table-responsive rules-core-inline-table" });
    const node = element("table", { className: "table table-sm mb-0" });
    if (table.caption) node.append(element("caption", { text: stripRendererTags(table.caption) }));
    if (Array.isArray(table.colLabels) && table.colLabels.length) {
        node.append(element("thead", {}, element("tr", {},
            table.colLabels.map(label => element("th", { text: stripRendererTags(String(label)) })))));
    }
    const body = element("tbody");
    for (const row of table.rows) {
        const cells = Array.isArray(row) ? row : [row];
        body.append(element("tr", {}, cells.map(cell => element("td", {}, renderRuleContent(cell)))));
    }
    node.append(body);
    wrapper.append(node);
    return wrapper;
}

function collectMonsterTags(document) {
    const tags = [];
    const type = document?.type;
    if (type && typeof type === "object" && !Array.isArray(type) && Array.isArray(type.tags)) {
        tags.push(...type.tags.map(formatDetailValue).filter(Boolean));
    }
    if (Array.isArray(document?.tags)) tags.push(...document.tags.map(formatDetailValue).filter(Boolean));
    return [...new Set(tags)];
}

function formatSize(value) {
    const values = Array.isArray(value) ? value : hasValue(value) ? [value] : [];
    const labels = values.map(candidate => SIZE_LABELS.get(String(candidate).toUpperCase()) ?? formatDetailValue(candidate));
    return labels.filter(Boolean).join("/") || null;
}

function formatCreatureType(value) {
    if (!hasValue(value)) return null;
    if (typeof value === "string") return titleCase(value);
    if (Array.isArray(value)) return value.map(formatCreatureType).filter(Boolean).join(", ");
    if (typeof value === "object") {
        const base = formatCreatureType(value.type) ?? formatCreatureType(value.name);
        const tags = Array.isArray(value.tags) ? value.tags.map(formatDetailValue).filter(Boolean) : [];
        return [base, tags.length ? `(${tags.join(", ")})` : null].filter(Boolean).join(" ") || formatDetailValue(value);
    }
    return String(value);
}

function formatAlignment(value) {
    if (!hasValue(value)) return null;
    if (typeof value === "string") return ALIGNMENT_LABELS.get(value.toUpperCase()) ?? value;
    if (Array.isArray(value)) {
        const labels = value.map(formatAlignment).filter(Boolean);
        if (labels.length === 2 && ["Lawful", "Neutral", "Chaotic"].includes(labels[0])
            && ["Good", "Neutral", "Evil"].includes(labels[1])) {
            return `${labels[0]} ${labels[1]}`;
        }
        return labels.join(" ");
    }
    if (typeof value === "object") {
        const alignment = formatAlignment(value.alignment ?? value.value);
        const chance = value.chance !== undefined ? `${value.chance}%` : null;
        return [alignment, chance].filter(Boolean).join(" · ") || formatDetailValue(value);
    }
    return String(value);
}

function formatArmorClass(value) {
    if (!hasValue(value)) return null;
    if (!Array.isArray(value)) return formatArmorClassEntry(value);
    return value.map(formatArmorClassEntry).filter(Boolean).join(", ");
}

function formatArmorClassEntry(value) {
    if (value === null || value === undefined) return null;
    if (typeof value !== "object" || Array.isArray(value)) return String(value);
    const armorClass = firstDefined(value.ac, value.value);
    const from = Array.isArray(value.from) ? value.from.map(stripRendererTags).join(", ") : formatDetailValue(value.from);
    const condition = value.condition ? stripRendererTags(String(value.condition)) : null;
    if (hasValue(armorClass)) {
        const note = [from, condition].filter(Boolean).join("; ");
        return note ? `${armorClass} (${note})` : String(armorClass);
    }
    return formatDetailValue(value);
}

function formatHitPoints(value) {
    if (!hasValue(value)) return null;
    if (typeof value !== "object" || Array.isArray(value)) return String(value);
    if (value.average !== undefined && value.formula) return `${value.average} (${stripRendererTags(String(value.formula))})`;
    if (value.average !== undefined) return String(value.average);
    if (value.formula) return stripRendererTags(String(value.formula));
    return formatDetailValue(value);
}

function formatSpeed(value) {
    if (!hasValue(value)) return null;
    if (typeof value === "number") return `${value} ft.`;
    if (typeof value === "string") return stripRendererTags(value);
    if (Array.isArray(value)) return value.map(formatSpeed).filter(Boolean).join(", ");
    if (typeof value !== "object") return String(value);
    const parts = [];
    for (const [mode, candidate] of Object.entries(value)) {
        if (!hasValue(candidate)) continue;
        const label = mode === "walk" ? null : titleCase(mode);
        const formatted = formatMovementDistance(candidate);
        parts.push([label, formatted].filter(Boolean).join(" "));
    }
    return parts.join(", ") || null;
}

function formatMovementDistance(value) {
    if (typeof value === "number") return `${value} ft.`;
    if (typeof value === "string") return stripRendererTags(value);
    if (value && typeof value === "object" && !Array.isArray(value)) {
        const distance = firstDefined(value.number, value.amount, value.value);
        const condition = value.condition ? stripRendererTags(String(value.condition)) : null;
        if (hasValue(distance)) {
            return `${distance} ft.${condition ? ` ${condition}` : ""}`;
        }
    }
    return formatDetailValue(value);
}

function formatInitiative(value) {
    if (!hasValue(value)) return null;
    if (typeof value === "number") return formatSignedValue(value);
    if (typeof value === "string") return stripRendererTags(value);
    if (typeof value === "object" && !Array.isArray(value)) {
        const modifier = firstDefined(value.mod, value.bonus, value.value, value.initiative);
        const score = firstDefined(value.score, value.passive);
        if (hasValue(modifier)) {
            return score !== null && score !== undefined
                ? `${formatSignedValue(modifier)} (${score})`
                : formatSignedValue(modifier);
        }
    }
    return formatDetailValue(value);
}

function formatChallenge(value) {
    if (!hasValue(value)) return null;
    if (typeof value !== "object" || Array.isArray(value)) return String(value);
    const base = firstDefined(value.cr, value.value);
    const lair = value.lair;
    if (hasValue(base) && hasValue(lair)) return `${base} (lair ${lair})`;
    return hasValue(base) ? String(base) : formatDetailValue(value);
}

function formatDetailValue(value) {
    if (!hasValue(value)) return null;
    if (typeof value === "string") return stripRendererTags(value);
    if (typeof value === "number" || typeof value === "boolean") return String(value);
    if (Array.isArray(value)) return value.map(formatDetailValue).filter(Boolean).join(", ");
    if (typeof value === "object") {
        if (value.name && Object.keys(value).length <= 2) return stripRendererTags(String(value.name));
        return Object.entries(value)
            .filter(([, candidate]) => hasValue(candidate))
            .map(([key, candidate]) => candidate === true
                ? humanizeKey(key)
                : `${humanizeKey(key)} ${formatDetailValue(candidate)}`)
            .join(", ");
    }
    return String(value);
}

function stripRendererTags(value) {
    let result = String(value ?? "");
    for (let pass = 0; pass < 4 && result.includes("{@"); pass += 1) {
        result = result.replace(/\{@([a-zA-Z][\w]*)\s+([^{}]+)\}/g, (_match, tag, payload) =>
            formatRendererTag(tag, payload));
    }
    return result.replace(/\s+/g, " ").trim();
}

function formatRendererTag(tag, payload) {
    const normalizedTag = String(tag).toLowerCase();
    const parts = String(payload).split("|");
    const primary = parts[0]?.trim() ?? "";
    const display = parts.length >= 3 && parts[2]?.trim() ? parts[2].trim() : primary;
    switch (normalizedTag) {
        case "atk":
            return formatAttackTag(primary);
        case "hit": {
            const number = Number(primary);
            return Number.isFinite(number) ? formatSignedValue(number) : primary;
        }
        case "dc":
            return `DC ${primary}`;
        case "recharge":
            return primary ? `Recharge ${primary}–6` : "Recharge 6";
        case "damage":
        case "dice":
        case "d20":
        case "chance":
            return primary;
        case "spell":
        case "item":
        case "condition":
        case "skill":
        case "creature":
        case "class":
        case "feat":
        case "race":
        case "background":
        case "action":
        case "sense":
            return display;
        default:
            return display || primary;
    }
}

function formatAttackTag(value) {
    const codes = String(value).split(",").map(candidate => candidate.trim().toLowerCase());
    const labels = new Map([
        ["mw", "Melee Weapon Attack"],
        ["rw", "Ranged Weapon Attack"],
        ["ms", "Melee Spell Attack"],
        ["rs", "Ranged Spell Attack"]
    ]);
    return codes.map(code => labels.get(code) ?? code).join(" or ");
}

function formatSignedValue(value) {
    if (value === null || value === undefined || value === "") return null;
    if (typeof value === "number") return `${value >= 0 ? "+" : ""}${value}`;
    const text = stripRendererTags(String(value));
    const number = Number(text);
    if (text !== "" && Number.isFinite(number)) return `${number >= 0 ? "+" : ""}${number}`;
    return text;
}

function humanizeKey(value) {
    return String(value)
        .replace(/[_-]+/g, " ")
        .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
        .replace(/\b\w/g, character => character.toUpperCase());
}

function titleCase(value) {
    return String(value).replace(/\b\w/g, character => character.toUpperCase());
}

function hasSectionContent(value) {
    return Array.isArray(value) ? value.length > 0 : hasValue(value);
}

function hasValue(value) {
    if (value === null || value === undefined || value === "") return false;
    if (Array.isArray(value)) return value.length > 0;
    if (typeof value === "object") return Object.keys(value).length > 0;
    return true;
}

function isInlineRuleValue(value) {
    return value === null || value === undefined
        || typeof value === "string"
        || typeof value === "number"
        || typeof value === "boolean";
}

function finiteNumber(value) {
    return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function firstDefined(...values) {
    return values.find(value => value !== null && value !== undefined && value !== "") ?? null;
}
