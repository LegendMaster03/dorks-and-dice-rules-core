import { codeBlock, definitionList, element } from "./ui.js";

const renderers = new Map([
    ["monster", renderMonster]
]);

export function renderResolvedRule(entityType, document) {
    const renderer = renderers.get(String(entityType ?? "").toLowerCase()) ?? renderGeneric;
    return renderer(document);
}

function renderMonster(document) {
    const root = element("div", { className: "rules-core-rule-renderer rules-core-monster" });
    const stats = extractMonsterStats(document);

    const summary = element("div", { className: "card card-body mb-3" });
    summary.append(
        element("div", { className: "d-flex flex-wrap gap-3 mb-3" },
            metric("Armor Class", stats.armorClass),
            metric("Hit Points", stats.hitPoints),
            metric("Hit Dice", stats.hitDice),
            metric("Initiative", stats.initiative),
            metric("Speed", stats.speed),
            metric("Challenge", stats.challengeRating)),
        renderAbilities(stats.abilities));
    root.append(summary);

    const secondary = [
        ["Saving Throws", formatValue(document?.save)],
        ["Skills", formatValue(document?.skill)],
        ["Vulnerabilities", formatValue(document?.vulnerable)],
        ["Resistances", formatValue(document?.resist)],
        ["Damage Immunities", formatValue(document?.immune)],
        ["Condition Immunities", formatValue(document?.conditionImmune)],
        ["Senses", formatValue(document?.senses)],
        ["Languages", formatValue(document?.languages)]
    ].filter(([, value]) => value);
    if (secondary.length) {
        root.append(element("div", { className: "card card-body mb-3" }, definitionList(secondary)));
    }

    for (const [property, title] of [
        ["trait", "Traits"],
        ["spellcasting", "Spellcasting"],
        ["action", "Actions"],
        ["bonus", "Bonus Actions"],
        ["reaction", "Reactions"],
        ["legendary", "Legendary Actions"],
        ["mythic", "Mythic Actions"]
    ]) {
        const entries = document?.[property];
        if (Array.isArray(entries) && entries.length) {
            root.append(renderNamedEntries(title, entries));
        }
    }

    const raw = element("details", { className: "card card-body" });
    raw.append(
        element("summary", { className: "fw-semibold", text: "Normalized rule document" }),
        element("div", { className: "mt-3" }, codeBlock(document)));
    root.append(raw);
    return root;
}

function renderGeneric(document) {
    const raw = element("details", { className: "card card-body", attributes: { open: "" } });
    raw.append(
        element("summary", { className: "fw-semibold", text: "Resolved rule" }),
        element("div", { className: "mt-3" }, codeBlock(document)));
    return raw;
}

function renderAbilities(abilities) {
    const row = element("div", { className: "row g-2" });
    for (const [label, score] of Object.entries(abilities)) {
        const modifier = typeof score === "number" ? Math.floor((score - 10) / 2) : null;
        row.append(element("div", { className: "col-4 col-md-2" },
            element("div", { className: "border rounded text-center p-2 h-100" },
                element("div", { className: "small fw-semibold", text: label }),
                element("div", {
                    text: score === null || score === undefined
                        ? "—"
                        : `${score} (${modifier >= 0 ? "+" : ""}${modifier})`
                }))));
    }
    return row;
}

function renderNamedEntries(title, entries) {
    const card = element("section", { className: "card card-body mb-3" });
    card.append(element("h4", { className: "h5", text: title }));
    for (const entry of entries) {
        const item = element("div", { className: "mb-3" });
        if (entry && typeof entry === "object" && !Array.isArray(entry) && entry.name) {
            item.append(element("div", { className: "fw-semibold", text: entry.name }));
        }
        const text = formatRuleText(entry?.entries ?? entry?.entry ?? entry);
        if (text) item.append(element("div", { className: "small", text }));
        card.append(item);
    }
    return card;
}

function extractMonsterStats(document) {
    const legacyText = normalizeLegacyBody(document?.body);
    return {
        armorClass: firstDefined(formatArmorClass(document?.ac), matchLegacy(legacyText, ["Armor Class", "AC"])),
        hitPoints: firstDefined(formatHitPoints(document?.hp), matchLegacy(legacyText, ["Hit Points", "HP"])),
        hitDice: firstDefined(document?.hp?.formula, matchLegacy(legacyText, ["Hit Dice"])),
        initiative: firstDefined(formatValue(document?.initiative), formatValue(document?.init), matchLegacy(legacyText, ["Initiative", "Init"]), abilityModifier(document?.dex)),
        speed: firstDefined(formatValue(document?.speed), matchLegacy(legacyText, ["Speed"])),
        challengeRating: firstDefined(formatChallenge(document?.cr), matchLegacy(legacyText, ["Challenge Rating", "CR"])),
        abilities: {
            STR: firstDefined(numberOrNull(document?.str), matchLegacyAbility(legacyText, "Str")),
            DEX: firstDefined(numberOrNull(document?.dex), matchLegacyAbility(legacyText, "Dex")),
            CON: firstDefined(numberOrNull(document?.con), matchLegacyAbility(legacyText, "Con")),
            INT: firstDefined(numberOrNull(document?.int), matchLegacyAbility(legacyText, "Int")),
            WIS: firstDefined(numberOrNull(document?.wis), matchLegacyAbility(legacyText, "Wis")),
            CHA: firstDefined(numberOrNull(document?.cha), matchLegacyAbility(legacyText, "Cha"))
        }
    };
}

function normalizeLegacyBody(body) {
    if (!body || typeof body !== "string") return "";
    const withBreaks = body
        .replace(/<\/(?:p|div|tr|td|th|li|h[1-6])>/gi, "\n")
        .replace(/<br\s*\/?\s*>/gi, "\n");
    const wrapper = document.createElement("div");
    wrapper.innerHTML = withBreaks;
    return (wrapper.textContent ?? "")
        .replace(/\r/g, "")
        .replace(/[ \t]+/g, " ")
        .replace(/\n+/g, "\n")
        .trim();
}

function matchLegacy(text, labels) {
    if (!text) return null;
    for (const label of labels) {
        const escaped = label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
        const match = text.match(new RegExp(`(?:^|\\n|\\s)${escaped}\\s*:?\\s*([^\\n]+)`, "i"));
        if (match?.[1]) return match[1].trim();
    }
    return null;
}

function matchLegacyAbility(text, label) {
    if (!text) return null;
    const match = text.match(new RegExp(`\\b${label}\\s*:?\\s*(-?\\d+)`, "i"));
    return match ? Number(match[1]) : null;
}

function formatArmorClass(value) {
    if (Array.isArray(value)) {
        return value.map(item => typeof item === "object" && item !== null ? item.ac ?? formatValue(item) : item).join(", ");
    }
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
    if (value && typeof value === "object" && !Array.isArray(value)) {
        return firstDefined(value.cr, value.lair, formatValue(value));
    }
    return formatValue(value);
}

function formatRuleText(value) {
    if (value === null || value === undefined) return "";
    if (typeof value === "string" || typeof value === "number" || typeof value === "boolean") return String(value);
    if (Array.isArray(value)) return value.map(formatRuleText).filter(Boolean).join(" ");
    if (typeof value === "object") {
        if (value.entries) return formatRuleText(value.entries);
        if (value.entry) return formatRuleText(value.entry);
        if (value.items) return formatRuleText(value.items);
        return Object.entries(value)
            .filter(([key]) => !["name", "type"].includes(key))
            .map(([, candidate]) => formatRuleText(candidate))
            .filter(Boolean)
            .join(" ");
    }
    return String(value);
}

function formatValue(value) {
    if (value === null || value === undefined) return null;
    if (typeof value === "string" || typeof value === "number" || typeof value === "boolean") return String(value);
    if (Array.isArray(value)) return value.map(formatValue).filter(Boolean).join(", ");
    if (typeof value === "object") {
        return Object.entries(value)
            .filter(([, candidate]) => candidate !== false && candidate !== null && candidate !== undefined)
            .map(([key, candidate]) => candidate === true ? key : `${key} ${formatValue(candidate)}`)
            .join(", ");
    }
    return String(value);
}

function abilityModifier(value) {
    const number = numberOrNull(value);
    if (number === null) return null;
    const modifier = Math.floor((number - 10) / 2);
    return `${modifier >= 0 ? "+" : ""}${modifier} (from DEX)`;
}

function numberOrNull(value) {
    return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function firstDefined(...values) {
    return values.find(value => value !== null && value !== undefined && value !== "") ?? null;
}

function metric(label, value) {
    return element("div", { className: "rules-core-metric" },
        element("div", { className: "rules-core-metric-value", text: value ?? "—" }),
        element("div", { className: "rules-core-metric-label", text: label }));
}
