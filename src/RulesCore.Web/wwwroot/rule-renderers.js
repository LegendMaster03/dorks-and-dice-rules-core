import { codeBlock, definitionList, element } from "./ui.js";

const renderers = new Map();
for (const type of ["monster"]) renderers.set(type, renderMonster);
for (const type of ["spell", "power"]) renderers.set(type, renderSpellLike);
for (const type of ["item"]) renderers.set(type, renderItem);
for (const type of ["feat", "divineAbility"]) renderers.set(type, renderFeatLike);
for (const type of ["class", "subclass", "classFeature", "subclassFeature", "prestigeClass", "npcClass"]) renderers.set(type, renderClassLike);
for (const type of ["race", "species"]) renderers.set(type, renderSpeciesLike);
for (const type of ["condition"]) renderers.set(type, renderCondition);
for (const type of ["skill", "domain", "houseRule", "rule", "source-fragment"]) renderers.set(type, renderRulesDocument);

export function renderRuleDocument(entityType, document, options = {}) {
    const type = String(entityType ?? "").trim();
    const renderer = renderers.get(type) ?? renderers.get(type.toLowerCase()) ?? renderRulesDocument;
    return renderer(document ?? {}, options);
}

export const renderResolvedRule = renderRuleDocument;

export function projectMonster(document) {
    const legacy = legacyFieldMap(document?.body);
    const hitDice = firstValue(document?.hp?.formula, legacy.get("Hit Dice"));
    const legacyArmorClass = legacy.get("Armor Class") ?? legacy.get("AC");
    const baseAttackGrapple = splitSlashPair(legacy.get("Base Attack/Grapple"));
    const sizeAndType = legacy.get("Size and Type") ?? legacy.get("SizeAndType");
    const abilities = {};

    for (const [key, legacyLabel] of [["STR", "Str"], ["DEX", "Dex"], ["CON", "Con"], ["INT", "Int"], ["WIS", "Wis"], ["CHA", "Cha"]]) {
        abilities[key] = numberValue(document?.[key.toLowerCase()]) ?? numberValue(legacy.get(legacyLabel));
    }

    return {
        size: firstValue(formatValue(document?.size), firstToken(sizeAndType)),
        creatureType: firstValue(formatCreatureType(document?.type), typeFromLegacy(sizeAndType)),
        alignment: firstValue(formatValue(document?.alignment), legacy.get("Alignment")),
        armorClass: firstValue(formatArmorClass(document?.ac), legacyArmorClass),
        touchArmorClass: firstValue(formatValue(document?.touchAc ?? document?.touchAC), legacy.get("Touch AC"), inlineArmorClass(legacyArmorClass, "touch")),
        flatFootedArmorClass: firstValue(formatValue(document?.flatFootedAc ?? document?.flatFootedAC), legacy.get("Flat-Footed AC"), inlineArmorClass(legacyArmorClass, "flat-footed")),
        hitPoints: firstValue(formatHitPoints(document?.hp), legacy.get("Hit Points"), hitPointsFromHitDice(hitDice)),
        hitDice,
        initiative: firstValue(formatValue(document?.initiative), formatValue(document?.init), legacy.get("Initiative"), legacy.get("Init"), abilityModifier(abilities.DEX)),
        speed: firstValue(formatSpeed(document?.speed), legacy.get("Speed")),
        challengeRating: firstValue(formatChallenge(document?.cr), legacy.get("Challenge Rating"), legacy.get("CR")),
        experience: firstValue(formatValue(document?.xp), legacy.get("XP")),
        proficiencyBonus: firstValue(formatSigned(document?.pb), formatSigned(document?.proficiencyBonus)),
        baseAttack: firstValue(formatValue(document?.baseAttack ?? document?.bab), legacy.get("Base Attack"), legacy.get("Base Atk"), baseAttackGrapple[0]),
        grapple: firstValue(formatValue(document?.grapple), legacy.get("Grapple"), baseAttackGrapple[1]),
        spaceReach: firstValue(formatValue(document?.spaceReach), legacy.get("Space/Reach")),
        fortitude: firstValue(formatValue(document?.fort), legacy.get("Fort"), legacy.get("Fortitude")),
        reflex: firstValue(formatValue(document?.ref), legacy.get("Ref"), legacy.get("Reflex")),
        will: firstValue(formatValue(document?.will), legacy.get("Will")),
        specialAttacks: firstValue(formatValue(document?.specialAttacks), legacy.get("Special Attacks")),
        specialQualities: firstValue(formatValue(document?.specialQualities), legacy.get("Special Qualities")),
        environment: firstValue(formatValue(document?.environment), legacy.get("Environment")),
        organization: firstValue(formatValue(document?.organization), legacy.get("Organization")),
        treasure: firstValue(formatValue(document?.treasure), legacy.get("Treasure")),
        advancement: firstValue(formatValue(document?.advancement), legacy.get("Advancement")),
        levelAdjustment: firstValue(formatValue(document?.levelAdjustment), legacy.get("Level Adjustment")),
        abilities
    };
}

function renderMonster(document, options) {
    const root = element("div", { className: "rules-core-rule-renderer rules-core-monster-sheet" });
    const stats = projectMonster(document);
    const kaiju = projectKaiju(document);
    const identity = [stats.size, stats.creatureType, stats.alignment].filter(Boolean).join(" · ");
    if (identity) root.append(element("div", { className: "rules-core-monster-identity", text: identity }));

    const primary = [
        ["Armor Class", stats.armorClass],
        kaiju ? ["Chaos Threshold", kaiju.chaosThreshold] : ["Hit Points", stats.hitPoints],
        ["Hit Dice", stats.hitDice],
        ["Initiative", stats.initiative],
        ["Speed", stats.speed],
        ["Challenge", stats.challengeRating]
    ];
    root.append(element("section", { className: "rules-core-dnd-block rules-core-monster-summary" },
        element("div", { className: "rules-core-stat-line" }, primary.map(([label, value]) => stat(label, value))),
        abilityRow(stats.abilities)));

    const defenses = compactPairs([
        ["Saving Throws", formatValue(document?.save)],
        ["Skills", formatValue(document?.skill)],
        ["Damage Vulnerabilities", formatValue(document?.vulnerable)],
        ["Damage Resistances", formatValue(document?.resist)],
        ["Damage Immunities", formatValue(document?.immune)],
        ["Condition Immunities", formatValue(document?.conditionImmune)],
        ["Senses", formatValue(document?.senses)],
        ["Languages", formatValue(document?.languages)],
        ["Experience", stats.experience],
        ["Proficiency Bonus", stats.proficiencyBonus]
    ]);
    if (defenses.length) root.append(infoSection("Defenses & Senses", defenses));

    const editionSpecific = compactPairs([
        ["Touch AC", stats.touchArmorClass],
        ["Flat-Footed AC", stats.flatFootedArmorClass],
        ["Base Attack", stats.baseAttack],
        ["Grapple", stats.grapple],
        ["Space / Reach", stats.spaceReach],
        ["Fortitude", stats.fortitude],
        ["Reflex", stats.reflex],
        ["Will", stats.will],
        ["Special Attacks", stats.specialAttacks],
        ["Special Qualities", stats.specialQualities],
        ["Environment", stats.environment],
        ["Organization", stats.organization],
        ["Treasure", stats.treasure],
        ["Advancement", stats.advancement],
        ["Level Adjustment", stats.levelAdjustment]
    ]);
    if (editionSpecific.length) root.append(infoSection("Edition-specific statistics", editionSpecific));
    if (kaiju) root.append(renderKaiju(kaiju));

    appendNamedSections(root, document, [
        ["trait", "Traits"],
        ["spellcasting", "Spellcasting"],
        ["action", "Actions"],
        ["bonus", "Bonus Actions"],
        ["reaction", "Reactions"],
        ["legendary", "Legendary Actions"],
        ["mythic", "Mythic Actions"],
        ["special", "Special Abilities"]
    ]);
    appendLegacyBody(root, document, options);
    return root;
}

function renderSpellLike(document, options) {
    const root = readingRoot();
    const details = compactPairs([
        ["Level", formatValue(document?.level)],
        ["School", formatValue(document?.school)],
        ["Casting / Manifesting Time", formatValue(document?.time ?? document?.castingTime)],
        ["Range", formatValue(document?.range)],
        ["Components", formatValue(document?.components)],
        ["Duration", formatValue(document?.duration)],
        ["Classes / Lists", formatValue(document?.classes ?? document?.classList)],
        ["Saving Throw", formatValue(document?.savingThrow)],
        ["Spell Attack", formatValue(document?.spellAttack)],
        ["Power Points", formatValue(document?.powerPoints)]
    ]);
    if (details.length) root.append(infoSection(document?.powerPoints !== undefined ? "Power details" : "Spell details", details));
    appendEntryBody(root, document);
    if (document?.entriesHigherLevel) {
        root.append(element("section", { className: "rules-core-dnd-section" },
            element("h4", { text: "At Higher Levels" }),
            richParagraphs(formatRuleText(document.entriesHigherLevel))));
    }
    appendLegacyBody(root, document, options);
    return root;
}

function renderItem(document, options) {
    const root = readingRoot();
    const details = compactPairs([
        ["Type", formatValue(document?.type)],
        ["Rarity", formatValue(document?.rarity)],
        ["Attunement", formatAttunement(document?.reqAttune)],
        ["Armor Class", formatValue(document?.ac)],
        ["Damage", formatValue(document?.dmg1 ?? document?.damage)],
        ["Properties", formatValue(document?.property)],
        ["Charges", formatValue(document?.charges)],
        ["Recharge", formatValue(document?.recharge)],
        ["Activation", formatValue(document?.activation)],
        ["Weight", formatValue(document?.weight)],
        ["Value", formatValue(document?.value ?? document?.price)]
    ]);
    if (details.length) root.append(infoSection("Item details", details));
    appendEntryBody(root, document);
    appendLegacyBody(root, document, options);
    return root;
}

function renderFeatLike(document, options) {
    const root = readingRoot();
    const prerequisites = formatValue(document?.prerequisite ?? document?.prerequisites);
    if (prerequisites) root.append(callout("Prerequisites", prerequisites));
    appendEntryBody(root, document);
    if (Array.isArray(document?.features)) root.append(renderNamedEntries("Features", document.features));
    appendLegacyBody(root, document, options);
    return root;
}

function renderClassLike(document, options) {
    const root = readingRoot();
    const details = compactPairs([
        ["Hit Die", formatValue(document?.hd ?? document?.hitDie)],
        ["Primary Ability", formatValue(document?.primaryAbility)],
        ["Saving Throws", formatValue(document?.proficiency ?? document?.savingThrows)],
        ["Armor Training", formatValue(document?.armorProficiencies)],
        ["Weapon Proficiencies", formatValue(document?.weaponProficiencies)],
        ["Skills", formatValue(document?.skillProficiencies ?? document?.skills)],
        ["Spellcasting Ability", formatValue(document?.spellcastingAbility)],
        ["Requirements", formatValue(document?.requirements ?? document?.prerequisite)]
    ]);
    if (details.length) root.append(infoSection("Class details", details));
    const progression = renderProgressionTable(document);
    if (progression) root.append(progression);
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
    const root = readingRoot();
    const details = compactPairs([
        ["Creature Type", formatValue(document?.creatureTypes ?? document?.creatureType)],
        ["Size", formatValue(document?.size)],
        ["Speed", formatSpeed(document?.speed)],
        ["Languages", formatValue(document?.languageProficiencies ?? document?.languages)],
        ["Ability Scores", formatValue(document?.ability)],
        ["Lineage / Subrace", formatValue(document?.lineage ?? document?.subrace)]
    ]);
    if (details.length) root.append(infoSection("Ancestry details", details));
    appendEntryBody(root, document);
    appendLegacyBody(root, document, options);
    return root;
}

function renderCondition(document, options) {
    const root = element("article", { className: "rules-core-rule-renderer rules-core-reading-view rules-core-condition" });
    appendEntryBody(root, document);
    appendLegacyBody(root, document, options);
    return root;
}

function renderRulesDocument(document, options) {
    const root = readingRoot();
    appendEntryBody(root, document);
    appendLegacyBody(root, document, options);
    if (!root.children.length) root.append(element("p", { className: "text-body-secondary", text: "This source record has no structured presentation fields." }));
    return root;
}

function readingRoot() {
    return element("article", { className: "rules-core-rule-renderer rules-core-reading-view" });
}

function projectKaiju(document) {
    const explicit = document?.kaiju === true
        || document?.isKaiju === true
        || String(document?.rulesVariant ?? "").toLowerCase().includes("kaiju")
        || String(document?.statBlockType ?? "").toLowerCase().includes("kaiju")
        || formatValue(document?.type?.tags)?.toLowerCase().includes("kaiju");
    const nested = document?.kaiju && typeof document.kaiju === "object" ? document.kaiju : {};
    const chaosThreshold = document?.chaosThreshold ?? document?.chaos ?? nested.chaosThreshold;
    const vulnerableAreas = document?.vulnerableAreas ?? document?.vulnerableArea ?? document?.weakPoints ?? nested.vulnerableAreas;
    const behaviors = document?.behaviors ?? document?.behaviours ?? document?.behaviorStates ?? document?.states ?? document?.phases ?? nested.behaviors;
    const finishingBlow = document?.finishingBlow ?? nested.finishingBlow;
    const deathRattle = document?.deathRattle ?? nested.deathRattle;
    const milestones = document?.xpMilestones ?? document?.milestones ?? nested.xpMilestones;
    const actions = document?.kaijuActions ?? document?.colossalActions ?? nested.actions;
    if (!explicit && chaosThreshold === undefined && !vulnerableAreas && !behaviors && !finishingBlow && !deathRattle && !milestones && !actions) return null;
    return { chaosThreshold: formatValue(chaosThreshold), vulnerableAreas, behaviors, finishingBlow, deathRattle, milestones, actions };
}

function renderKaiju(kaiju) {
    const section = element("section", { className: "rules-core-dnd-section rules-core-kaiju-variant" });
    section.append(element("div", { className: "rules-core-section-heading" },
        element("h4", { text: "Kaiju Battle Structure" }),
        element("span", { className: "badge text-bg-warning", text: "Kaiju" })));

    const summary = compactPairs([
        ["Chaos Threshold", kaiju.chaosThreshold],
        ["Finishing Blow", formatValue(kaiju.finishingBlow)],
        ["Death Rattle", formatValue(kaiju.deathRattle)]
    ]);
    if (summary.length) section.append(definitionList(summary));
    if (kaiju.vulnerableAreas) section.append(renderStructuredTable("Vulnerable Areas", kaiju.vulnerableAreas, ["name", "specialTraits", "cr", "ac", "hp"]));
    if (kaiju.behaviors) section.append(renderStructuredTable("Behaviors", kaiju.behaviors, ["name", "trigger", "effect", "gainedFeatures", "lostFeatures"]));
    if (kaiju.milestones) section.append(renderStructuredTable("XP Milestones", kaiju.milestones, ["criteria", "xp", "value"]));
    if (Array.isArray(kaiju.actions) && kaiju.actions.length) section.append(renderNamedEntries("Kaiju Actions", kaiju.actions));
    return section;
}

function renderStructuredTable(title, value, preferredKeys) {
    const wrap = element("div", { className: "rules-core-structured-table" });
    if (title) wrap.append(element("h5", { text: title }));
    const rows = Array.isArray(value)
        ? value
        : value && typeof value === "object"
            ? Object.entries(value).map(([name, row]) => row && typeof row === "object" ? { name, ...row } : { name, value: row })
            : [{ value }];
    if (!rows.length) return wrap;

    const objects = rows.map(row => row && typeof row === "object" ? row : { value: row });
    const keys = preferredKeys.filter(key => objects.some(row => row[key] !== undefined));
    for (const key of Object.keys(objects[0] ?? {})) {
        if (!keys.includes(key)) keys.push(key);
    }

    const table = element("table", { className: "table table-sm align-middle mb-0" });
    const headRow = element("tr");
    for (const key of keys) headRow.append(element("th", { text: humanize(key) }));
    table.append(element("thead", {}, headRow));
    const body = element("tbody");
    for (const row of objects) {
        const tableRow = element("tr");
        for (const key of keys) tableRow.append(element("td", { text: formatValue(row[key]) ?? "—" }));
        body.append(tableRow);
    }
    table.append(body);
    wrap.append(element("div", { className: "table-responsive" }, table));
    return wrap;
}

function renderProgressionTable(document) {
    const groups = Array.isArray(document?.classTableGroups) ? document.classTableGroups : [];
    if (!groups.length && !Array.isArray(document?.progression)) return null;
    const section = element("section", { className: "rules-core-dnd-section" });
    section.append(element("h4", { text: "Progression" }));

    if (groups.length) {
        for (const group of groups) {
            const labels = group?.colLabels ?? group?.columns ?? [];
            const rows = group?.rows ?? group?.data ?? [];
            if (!Array.isArray(rows) || !rows.length) continue;
            const table = element("table", { className: "table table-sm align-middle mb-3" });
            if (labels.length) {
                const headRow = element("tr");
                for (const label of labels) headRow.append(element("th", { text: formatRuleText(label) }));
                table.append(element("thead", {}, headRow));
            }
            const body = element("tbody");
            for (const row of rows) {
                const values = Array.isArray(row) ? row : Object.values(row ?? {});
                const tableRow = element("tr");
                for (const cell of values) tableRow.append(element("td", { text: formatRuleText(cell) }));
                body.append(tableRow);
            }
            table.append(body);
            section.append(element("div", { className: "table-responsive" }, table));
        }
    } else {
        section.append(renderStructuredTable("", document.progression, []));
    }
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
        if (entry && typeof entry === "object" && !Array.isArray(entry) && entry.name) block.append(element("h5", { text: entry.name }));
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
    return element("div", { className: "rules-core-rule-callout" }, element("strong", { text: `${label}. ` }), value);
}

function abilityRow(abilities) {
    const row = element("div", { className: "rules-core-ability-row" });
    for (const key of ["STR", "DEX", "CON", "INT", "WIS", "CHA"]) {
        const score = abilities[key];
        const modifier = typeof score === "number" ? Math.floor((score - 10) / 2) : null;
        row.append(element("div", { className: "rules-core-ability" },
            element("span", { className: "rules-core-ability-name", text: key }),
            element("span", { className: "rules-core-ability-value", text: score === null || score === undefined ? "—" : `${score} (${signed(modifier)})` })));
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
        "Armor Class", "Touch AC", "Flat-Footed AC", "Hit Points", "Hit Dice", "Initiative", "Challenge Rating",
        "Base Attack/Grapple", "Base Attack", "Base Atk", "Grapple", "Space/Reach", "Fortitude", "Fort", "Reflex", "Ref", "Will",
        "Special Attacks", "Special Qualities", "Environment", "Organization", "Treasure", "Advancement", "Level Adjustment",
        "SizeAndType", "Size and Type", "Alignment", "Speed", "AC", "Init", "CR", "XP", "Str", "Dex", "Con", "Int", "Wis", "Cha"
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

function hitPointsFromHitDice(value) {
    const text = String(value ?? "");
    const lower = text.toLowerCase();
    const hpIndex = lower.lastIndexOf(" hp");
    if (hpIndex < 0) return null;
    const open = text.lastIndexOf("(", hpIndex);
    if (open < 0) return null;
    const candidate = text.slice(open + 1, hpIndex).trim();
    return /^\d+$/.test(candidate) ? candidate : null;
}

function inlineArmorClass(value, label) {
    const text = String(value ?? "");
    const lower = text.toLowerCase();
    const index = lower.indexOf(label.toLowerCase());
    if (index < 0) return null;
    const tail = text.slice(index + label.length).trim();
    const match = tail.match(/^\s*[:=]?\s*(-?\d+)/);
    return match?.[1] ?? null;
}

function splitSlashPair(value) {
    const text = String(value ?? "").trim();
    const slash = text.indexOf("/");
    if (slash < 0) return [null, null];
    return [text.slice(0, slash).trim() || null, text.slice(slash + 1).trim() || null];
}

function firstToken(value) {
    const text = String(value ?? "").trim();
    return text ? text.split(/\s+/, 1)[0] : null;
}

function typeFromLegacy(value) {
    const text = String(value ?? "").trim();
    if (!text) return null;
    const words = text.split(/\s+/);
    return words.length > 1 ? words.slice(1).join(" ").split(",", 1)[0].trim() : null;
}

function stripMarkup(value) {
    if (!value || typeof value !== "string") return "";
    if (!value.includes("<")) return value.replace(/\r/g, "").replace(/[ \t]+/g, " ").replace(/\n{3,}/g, "\n\n").trim();
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

function isWordChar(character) { return /[a-z0-9]/i.test(character ?? ""); }
function compactPairs(values) { return values.filter(([, value]) => value !== null && value !== undefined && value !== ""); }

function formatCreatureType(value) {
    if (!value) return null;
    if (typeof value === "string") return value;
    if (typeof value !== "object") return formatValue(value);
    const base = value.type ?? value.name ?? null;
    const tags = formatValue(value.tags);
    return base && tags ? `${base} (${tags})` : base ?? tags;
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
    if (typeof value !== "string") return null;
    const match = value.trim().match(/^-?\d+(?:\.\d+)?/);
    if (!match) return null;
    const parsed = Number(match[0]);
    return Number.isFinite(parsed) ? parsed : null;
}

function abilityModifier(score) {
    if (typeof score !== "number") return null;
    return `${signed(Math.floor((score - 10) / 2))} (DEX)`;
}

function signed(value) { return `${value >= 0 ? "+" : ""}${value}`; }
function firstValue(...values) { return values.find(value => value !== null && value !== undefined && value !== "") ?? null; }

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
