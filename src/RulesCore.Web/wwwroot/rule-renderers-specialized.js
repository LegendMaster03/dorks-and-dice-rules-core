import { codeBlock, element } from "./ui.js";
import {
    ABILITIES,
    DAMAGE_TYPE_LABELS,
    ITEM_TYPE_LABELS,
    MONSTER_DETAIL_FIELDS,
    MONSTER_SECTIONS,
    PRESENTATION_METADATA_FIELDS,
    SPELL_SCHOOLS,
    collectMonsterTags,
    firstDefined,
    formatAlignment,
    formatArmorClass,
    formatCreatureType,
    formatDetailValue,
    formatHitPoints,
    formatInitiative,
    formatSignedValue,
    formatSize,
    formatSpeed,
    hasSectionContent,
    hasValue,
    renderAbilityGrid,
    renderAdditionalMechanics,
    renderCompactStatistic,
    renderEntityHeader,
    renderLabeledDetails,
    renderNamedRuleEntry,
    renderRuleContent,
    renderRulesCoreExtensions,
    renderRulesTextSection,
    stripRendererTags,
    titleCase
} from "./rule-renderer-support.js";

export function renderMonster(document = {}, options = {}) {
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

export function renderSpell(document = {}, options = {}) {
    const level = formatSpellLevel(document?.level);
    const school = formatSpellSchool(document?.school);
    const time = formatCastingTime(document?.time);
    const range = formatSpellRange(document?.range);
    const components = formatSpellComponents(document?.components);
    const duration = formatSpellDuration(document?.duration);

    return renderStructuredRule(document, options, {
        summary: [
            ["Level", level],
            ["School", school],
            ["Casting Time", time],
            ["Range", range],
            ["Components", components],
            ["Duration", duration]
        ],
        details: [
            ["Ritual", document?.meta?.ritual === true ? "Yes" : null],
            ["Concentration", hasConcentration(document?.duration) ? "Yes" : null]
        ],
        sections: [
            ["Rules", document?.entries],
            ["At Higher Levels", document?.entriesHigherLevel]
        ],
        consumed: [
            "level", "school", "time", "range", "components", "duration",
            "meta", "entries", "entriesHigherLevel"
        ]
    });
}

export function renderClass(document = {}, options = {}) {
    return renderStructuredRule(document, options, {
        summary: [
            ["Hit Die", formatHitDie(document?.hd)],
            ["Primary Ability", formatAbilityReference(firstDefined(document?.primaryAbility, document?.primaryAbilities))],
            ["Saving Throws", formatAbilityReference(firstDefined(document?.proficiency, document?.savingThrows))],
            ["Spellcasting Ability", formatAbilityReference(firstDefined(document?.spellcastingAbility, document?.casterAbility))]
        ],
        details: [
            ["Subclass", firstDefined(document?.subclassTitle, document?.subclassName)],
            ["Starting Proficiencies", formatStartingProficiencies(document?.startingProficiencies)],
            ["Starting Equipment", formatStartingEquipment(document?.startingEquipment)]
        ],
        sections: [
            ["Class Features", document?.classFeatures],
            ["Rules", document?.entries]
        ],
        consumed: [
            "hd", "primaryAbility", "primaryAbilities", "proficiency", "savingThrows",
            "spellcastingAbility", "casterAbility", "subclassTitle", "subclassName",
            "startingProficiencies", "startingEquipment", "classFeatures", "entries"
        ]
    });
}

export function renderSubclass(document = {}, options = {}) {
    return renderStructuredRule(document, options, {
        summary: [
            ["Class", firstDefined(document?.className, document?.class)],
            ["Subclass", firstDefined(document?.shortName, document?.name)]
        ],
        details: [
            ["Class Source", document?.classSource]
        ],
        sections: [
            ["Subclass Features", document?.subclassFeatures],
            ["Rules", document?.entries]
        ],
        consumed: [
            "className", "class", "shortName", "classSource", "subclassFeatures", "entries"
        ]
    });
}

export function renderPrestigeClass(document = {}, options = {}) {
    return renderStructuredRule(document, options, {
        summary: [
            ["Hit Die", formatHitDie(document?.hd)],
            ["Primary Ability", formatAbilityReference(firstDefined(document?.primaryAbility, document?.primaryAbilities))]
        ],
        details: [
            ["Prerequisites", formatPrerequisite(firstDefined(document?.prerequisite, document?.prerequisites))]
        ],
        sections: [
            ["Class Features", firstDefined(document?.classFeatures, document?.prestigeClassFeatures)],
            ["Rules", document?.entries]
        ],
        consumed: [
            "hd", "primaryAbility", "primaryAbilities", "prerequisite", "prerequisites",
            "classFeatures", "prestigeClassFeatures", "entries"
        ]
    });
}

export function renderSpecies(document = {}, options = {}) {
    return renderStructuredRule(document, options, {
        summary: [
            ["Size", formatSize(document?.size)],
            ["Speed", formatSpeed(document?.speed)],
            ["Ability Scores", formatAbilityBonuses(document?.ability)],
            ["Darkvision", formatDistance(firstDefined(document?.darkvision, document?.darkvisionRange))]
        ],
        details: [
            ["Creature Type", formatCreatureType(firstDefined(document?.creatureTypes, document?.creatureType, document?.type))],
            ["Languages", formatDetailValue(document?.languageProficiencies)]
        ],
        sections: [
            ["Traits", document?.entries]
        ],
        consumed: [
            "size", "speed", "ability", "darkvision", "darkvisionRange",
            "creatureTypes", "creatureType", "type", "languageProficiencies", "entries"
        ]
    });
}

export function renderFeat(document = {}, options = {}) {
    return renderStructuredRule(document, options, {
        summary: [
            ["Category", formatFeatCategory(firstDefined(document?.category, document?.categoryDisplay, document?.featCategory))],
            ["Repeatable", document?.repeatable === true ? "Yes" : document?.repeatable === false ? "No" : null]
        ],
        details: [
            ["Prerequisite", formatPrerequisite(document?.prerequisite)]
        ],
        sections: [
            ["Rules", document?.entries]
        ],
        consumed: ["category", "categoryDisplay", "featCategory", "repeatable", "prerequisite", "entries"]
    });
}

export function renderItem(document = {}, options = {}) {
    return renderStructuredRule(document, options, {
        summary: [
            ["Type", formatItemType(document?.type)],
            ["Rarity", formatRarity(document?.rarity)],
            ["Armor Class", formatArmorClass(firstDefined(document?.ac, document?.armorClass))],
            ["Damage", formatItemDamage(document)],
            ["Weight", formatWeight(document?.weight)],
            ["Value", formatCurrency(document?.value)]
        ],
        details: [
            ["Attunement", formatAttunement(firstDefined(document?.reqAttune, document?.requiresAttunement))],
            ["Weapon Category", document?.weaponCategory],
            ["Properties", formatDetailValue(document?.property)]
        ],
        sections: [
            ["Rules", document?.entries]
        ],
        consumed: [
            "type", "rarity", "ac", "armorClass", "dmg1", "dmgType", "damage",
            "weight", "value", "reqAttune", "requiresAttunement", "weaponCategory",
            "property", "entries"
        ]
    });
}

export function renderCondition(document = {}, options = {}) {
    return renderStructuredRule(document, options, {
        sections: [
            ["Rules", document?.entries]
        ],
        consumed: ["entries"]
    });
}

export function renderSkill(document = {}, options = {}) {
    return renderStructuredRule(document, options, {
        summary: [
            ["Ability", formatAbilityReference(firstDefined(document?.ability, document?.stat))]
        ],
        sections: [
            ["Rules", document?.entries]
        ],
        consumed: ["ability", "stat", "entries"]
    });
}

export function renderGeneralRule(document = {}, options = {}) {
    return renderStructuredRule(document, options, {
        sections: [
            ["Rules", firstDefined(document?.entries, document?.rules, document?.text)]
        ],
        consumed: ["entries", "rules", "text"]
    });
}

function renderStructuredRule(
    document = {},
    options = {},
    {
        summary = [],
        details = [],
        sections = [],
        consumed = []
    } = {})
{
    const root = element("article", {
        className: "rules-core-rule-renderer rules-core-structured-rule"
    });
    const consumedKeys = new Set([
        "name",
        "_rulesCore",
        ...consumed
    ]);

    const visibleSummary = summary.filter(([, value]) => hasValue(value));
    if (visibleSummary.length) {
        root.append(element("section", { className: "rules-core-rule-summary" },
            visibleSummary.map(([label, value]) =>
                renderCompactStatistic(label, value))));
    }

    const visibleDetails = details.filter(([, value]) => hasValue(value));
    if (visibleDetails.length) {
        root.append(renderStructuredDetails(visibleDetails));
    }

    for (const [title, value] of sections) {
        if (!hasSectionContent(value)) continue;
        root.append(renderStructuredTextSection(title, value));
    }

    root.append(...renderRulesCoreExtensions(document?._rulesCore));

    const extras = Object.entries(document ?? {})
        .filter(([key, value]) => !consumedKeys.has(key)
            && !PRESENTATION_METADATA_FIELDS.has(key)
            && !key.startsWith("_")
            && hasValue(value));
    if (extras.length) {
        root.append(renderAdditionalMechanics(extras));
    }

    if (options.showDocument !== false) {
        root.append(renderDocumentDisclosure(
            document,
            options.documentLabel ?? "Normalized rule document"));
    }

    return root;
}

function renderStructuredDetails(items) {
    const section = element("section", {
        className: "rules-core-structured-section rules-core-structured-details"
    });
    const list = element("dl", { className: "rules-core-labeled-details" });
    for (const [label, value] of items) {
        list.append(
            element("dt", { text: label }),
            element("dd", {}, renderRuleContent(value)));
    }
    section.append(list);
    return section;
}

function renderStructuredTextSection(title, entries) {
    const section = element("section", { className: "rules-core-structured-section" });
    section.append(element("h4", {
        className: "rules-core-structured-section-title",
        text: title
    }));
    const body = element("div", { className: "rules-core-rules-text" });
    if (Array.isArray(entries)) {
        for (const entry of entries) body.append(renderNamedRuleEntry(entry));
    } else {
        body.append(renderNamedRuleEntry(entries));
    }
    section.append(body);
    return section;
}

function renderDocumentDisclosure(document, label) {
    const raw = element("details", { className: "rules-core-secondary-details" });
    raw.append(
        element("summary", { text: label }),
        element("div", { className: "rules-core-secondary-details-body" }, codeBlock(document)));
    return raw;
}

function formatSpellLevel(value) {
    const level = Number(value);
    if (!Number.isFinite(level)) return formatDetailValue(value);
    if (level === 0) return "Cantrip";
    const suffix = level % 10 === 1 && level % 100 !== 11
        ? "st"
        : level % 10 === 2 && level % 100 !== 12
            ? "nd"
            : level % 10 === 3 && level % 100 !== 13
                ? "rd"
                : "th";
    return `${level}${suffix} level`;
}

function formatSpellSchool(value) {
    if (!hasValue(value)) return null;
    const key = String(value).toUpperCase();
    return SPELL_SCHOOLS.get(key) ?? titleCase(stripRendererTags(String(value)));
}

function formatCastingTime(value) {
    if (!hasValue(value)) return null;
    const times = Array.isArray(value) ? value : [value];
    return times.map(time => {
        if (typeof time !== "object" || time === null || Array.isArray(time)) {
            return formatDetailValue(time);
        }
        const number = firstDefined(time.number, time.amount);
        const unit = firstDefined(time.unit, time.type);
        const condition = time.condition ? stripRendererTags(String(time.condition)) : null;
        const base = [
            hasValue(number) ? number : null,
            unit ? pluralizeUnit(String(unit), Number(number)) : null
        ].filter(Boolean).join(" ");
        return [base, condition].filter(Boolean).join(" · ");
    }).filter(Boolean).join("; ");
}

function formatSpellRange(value) {
    if (!hasValue(value)) return null;
    if (typeof value !== "object" || Array.isArray(value)) return formatDetailValue(value);

    const type = String(value.type ?? "").toLowerCase();
    if (["self", "touch", "sight", "unlimited", "special"].includes(type)) {
        return titleCase(type);
    }

    const distance = value.distance;
    if (!distance || typeof distance !== "object" || Array.isArray(distance)) {
        return titleCase(type) || formatDetailValue(value);
    }
    const distanceType = String(distance.type ?? "").toLowerCase();
    if (["self", "touch", "sight", "unlimited"].includes(distanceType)) {
        return titleCase(distanceType);
    }
    const amount = firstDefined(distance.amount, distance.number);
    if (!hasValue(amount)) return titleCase(distanceType || type);
    const unit = distanceType === "feet" ? "ft."
        : distanceType === "miles" ? "miles"
            : distanceType || "ft.";
    return `${amount} ${unit}`;
}

function formatSpellComponents(value) {
    if (!hasValue(value)) return null;
    if (typeof value !== "object" || Array.isArray(value)) return formatDetailValue(value);

    const parts = [];
    if (value.v) parts.push("V");
    if (value.s) parts.push("S");
    if (value.m) {
        const material = typeof value.m === "object" && value.m !== null
            ? firstDefined(value.m.text, value.m.entry, value.m.name)
            : value.m === true ? null : value.m;
        parts.push(material ? `M (${stripRendererTags(String(material))})` : "M");
    }
    if (value.r) parts.push("R");
    return parts.join(", ") || formatDetailValue(value);
}

function formatSpellDuration(value) {
    if (!hasValue(value)) return null;
    const durations = Array.isArray(value) ? value : [value];
    return durations.map(duration => {
        if (typeof duration !== "object" || duration === null || Array.isArray(duration)) {
            return formatDetailValue(duration);
        }
        const type = String(duration.type ?? "").toLowerCase();
        if (type === "instant") return "Instantaneous";
        if (type === "permanent") return "Permanent";
        if (type === "special") return "Special";

        const timed = duration.duration;
        let base = titleCase(type);
        if (timed && typeof timed === "object" && !Array.isArray(timed)) {
            const amount = firstDefined(timed.amount, timed.number);
            const unit = firstDefined(timed.type, timed.unit);
            base = [
                duration.upTo ? "Up to" : null,
                amount,
                unit ? pluralizeUnit(String(unit), Number(amount)) : null
            ].filter(Boolean).join(" ");
        }
        return duration.concentration ? `Concentration, ${base}` : base;
    }).filter(Boolean).join("; ");
}

function hasConcentration(value) {
    const durations = Array.isArray(value) ? value : hasValue(value) ? [value] : [];
    return durations.some(duration =>
        duration && typeof duration === "object" && duration.concentration === true);
}

function formatHitDie(value) {
    if (!hasValue(value)) return null;
    if (typeof value !== "object" || Array.isArray(value)) return formatDetailValue(value);
    const number = firstDefined(value.number, 1);
    const faces = firstDefined(value.faces, value.die);
    return hasValue(faces) ? `${number}d${faces}` : formatDetailValue(value);
}

function formatAbilityReference(value) {
    if (!hasValue(value)) return null;
    if (typeof value === "string") return value.toUpperCase();
    if (Array.isArray(value)) {
        return value.map(formatAbilityReference).filter(Boolean).join(", ");
    }
    if (typeof value === "object") {
        const direct = ABILITIES
            .filter(([, key]) => value[key] === true || typeof value[key] === "number")
            .map(([label, key]) => typeof value[key] === "number"
                ? `${label} ${formatSignedValue(value[key])}`
                : label);
        if (direct.length) return direct.join(", ");
        if (value.choose) return formatAbilityChoice(value.choose);
    }
    return formatDetailValue(value);
}

function formatAbilityBonuses(value) {
    if (!hasValue(value)) return null;
    const entries = Array.isArray(value) ? value : [value];
    const formatted = [];
    for (const entry of entries) {
        if (!entry || typeof entry !== "object" || Array.isArray(entry)) {
            formatted.push(formatDetailValue(entry));
            continue;
        }
        for (const [label, key] of ABILITIES) {
            if (typeof entry[key] === "number") {
                formatted.push(`${label} ${formatSignedValue(entry[key])}`);
            }
        }
        if (entry.choose) formatted.push(formatAbilityChoice(entry.choose));
    }
    return formatted.filter(Boolean).join("; ") || formatDetailValue(value);
}

function formatAbilityChoice(value) {
    if (!value || typeof value !== "object" || Array.isArray(value)) return formatDetailValue(value);
    const from = Array.isArray(value.from)
        ? value.from.map(candidate => String(candidate).toUpperCase()).join(", ")
        : "abilities";
    const count = Number(value.count ?? 1);
    const amount = Number(value.amount ?? 1);
    return `Choose ${Number.isFinite(count) ? count : 1} from ${from}: ${formatSignedValue(Number.isFinite(amount) ? amount : 1)}`;
}

function formatStartingProficiencies(value) {
    if (!hasValue(value)) return null;
    if (typeof value !== "object" || Array.isArray(value)) return formatDetailValue(value);
    const parts = [];
    if (hasValue(value.armor)) parts.push(`Armor: ${formatDetailValue(value.armor)}`);
    if (hasValue(value.weapons)) parts.push(`Weapons: ${formatDetailValue(value.weapons)}`);
    if (hasValue(value.tools)) parts.push(`Tools: ${formatDetailValue(value.tools)}`);
    if (hasValue(value.skills)) parts.push(`Skills: ${formatDetailValue(value.skills)}`);
    return parts.join("; ") || formatDetailValue(value);
}

function formatStartingEquipment(value) {
    if (!hasValue(value)) return null;
    if (typeof value === "object" && !Array.isArray(value)) {
        return firstDefined(
            value.additionalFromBackground,
            value.defaultData,
            value.goldAlternative,
            value.default
        ) ?? formatDetailValue(value);
    }
    return formatDetailValue(value);
}

function formatPrerequisite(value) {
    if (!hasValue(value)) return null;
    return formatDetailValue(value);
}

function formatFeatCategory(value) {
    if (!hasValue(value)) return null;
    const text = String(value);
    const categories = new Map([
        ["G", "General"],
        ["FS", "Fighting Style"],
        ["O", "Origin"],
        ["E", "Epic Boon"]
    ]);
    return categories.get(text.toUpperCase()) ?? titleCase(text);
}

function formatItemType(value) {
    if (!hasValue(value)) return null;
    const text = String(value);
    return ITEM_TYPE_LABELS.get(text.toUpperCase()) ?? titleCase(text);
}

function formatRarity(value) {
    if (!hasValue(value)) return null;
    const text = String(value);
    return text.toLowerCase() === "none" ? "—" : titleCase(text);
}

function formatItemDamage(document) {
    const formula = firstDefined(document?.dmg1, document?.damage);
    if (!hasValue(formula)) return null;
    const type = document?.dmgType
        ? DAMAGE_TYPE_LABELS.get(String(document.dmgType).toUpperCase()) ?? document.dmgType
        : null;
    return [stripRendererTags(String(formula)), type].filter(Boolean).join(" ");
}

function formatWeight(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) return formatDetailValue(value);
    return `${number} lb.`;
}

function formatCurrency(value) {
    const copper = Number(value);
    if (!Number.isFinite(copper)) return formatDetailValue(value);
    if (copper === 0) return "0 gp";
    if (copper % 100 === 0) return `${copper / 100} gp`;
    if (copper % 10 === 0) return `${copper / 10} sp`;
    return `${copper} cp`;
}

function formatAttunement(value) {
    if (!hasValue(value)) return null;
    if (value === true) return "Required";
    if (value === false) return "Not required";
    return stripRendererTags(String(value));
}

function formatDistance(value) {
    const number = Number(value);
    if (Number.isFinite(number)) return `${number} ft.`;
    return formatDetailValue(value);
}

function pluralizeUnit(unit, amount) {
    const normalized = unit.toLowerCase();
    const labels = new Map([
        ["action", "action"],
        ["bonus", "bonus action"],
        ["bonus action", "bonus action"],
        ["reaction", "reaction"],
        ["round", "round"],
        ["minute", "minute"],
        ["hour", "hour"],
        ["day", "day"]
    ]);
    const base = labels.get(normalized) ?? normalized;
    return amount === 1 || !Number.isFinite(amount) ? base : `${base}s`;
}
