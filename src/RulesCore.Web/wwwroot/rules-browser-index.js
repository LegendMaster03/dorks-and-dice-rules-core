import { element } from "./ui.js";

export const RULE_FAMILY_TABS = [
    ["", "All Content"],
    ["monster", "Bestiary"],
    ["spell", "Spells"],
    ["class", "Classes"],
    ["subclass", "Subclasses"],
    ["prestigeClass", "Prestige Classes"],
    ["feat", "Feats"],
    ["background", "Backgrounds"],
    ["optionalfeature", "Options & Features"],
    ["race", "Races"],
    ["species", "Species"],
    ["item", "Items"],
    ["condition", "Conditions"],
    ["skill", "Skills"],
    ["houseRule", "House Rules"],
    ["rule", "Other Rules"]
];

const BROWSER_COLUMNS = new Map([
    ["", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "entityType", label: "Type", width: "minmax(5rem, .8fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["monster", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "type", label: "Type", width: "minmax(5rem, .9fr)" },
        { key: "cr", label: "CR", width: "minmax(2.5rem, .4fr)", align: "center" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["spell", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "level", label: "Level", width: "minmax(4.5rem, .6fr)" },
        { key: "school", label: "School", width: "minmax(6rem, 1fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["class", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "hitDie", label: "Hit Die", width: "minmax(4rem, .55fr)", align: "center" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["subclass", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "parentClass", label: "Class", width: "minmax(6rem, 1fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["prestigeClass", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["feat", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "category", label: "Category", width: "minmax(6rem, 1fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["background", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["optionalfeature", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["race", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "ability", label: "Ability", width: "minmax(7rem, 1.1fr)" },
        { key: "size", label: "Size", width: "minmax(4rem, .65fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["species", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "ability", label: "Ability", width: "minmax(7rem, 1.1fr)" },
        { key: "size", label: "Size", width: "minmax(4rem, .65fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["item", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "type", label: "Type", width: "minmax(5rem, .8fr)" },
        { key: "rarity", label: "Rarity", width: "minmax(5rem, .8fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["condition", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]],
    ["skill", [
        { key: "name", label: "Name", width: "minmax(9rem, 2fr)" },
        { key: "ability", label: "Ability", width: "minmax(4rem, .65fr)" },
        { key: "source", label: "Base", width: "minmax(3.5rem, .55fr)" }
    ]]
]);

export function libraryTitle(entityType) {
    const normalized = entityType ?? "";
    const known = RULE_FAMILY_TABS.find(([value]) => value === normalized);
    return known?.[1] ?? humanizeEntityType(normalized);
}

function browserColumns(entityType) {
    return BROWSER_COLUMNS.get(entityType)
        ?? BROWSER_COLUMNS.get("")
        ?? [];
}

export function renderIndexHeader(container, entityType) {
    const columns = browserColumns(entityType);
    container.replaceChildren();
    container.style.gridTemplateColumns = columns.map(column => column.width).join(" ");
    for (const column of columns) {
        container.append(element("span", {
            className: `rules-core-library-column-header${column.align === "center" ? " is-center" : ""}`,
            text: column.label
        }));
    }
}

export function renderRuleRows(container, rules, entityType, onSelect, { append = false } = {}) {
    if (!append) container.replaceChildren();
    if (!rules.length) {
        if (!append) {
            container.append(element("div", {
                className: "rules-core-library-empty-list",
                text: "No rules match the current filters."
            }));
        }
        return;
    }

    const columns = browserColumns(entityType);
    const template = columns.map(column => column.width).join(" ");
    for (const rule of rules) {
        const row = element("button", {
            type: "button",
            className: "rules-core-library-row",
            dataset: { conceptKey: rule.conceptKey },
            attributes: {
                role: "option",
                "aria-selected": "false"
            }
        });
        row.style.gridTemplateColumns = template;
        for (const column of columns) {
            row.append(renderRuleCell(rule, column));
        }
        row.addEventListener("click", () => onSelect(rule));
        container.append(row);
    }
}

function renderRuleCell(rule, column) {
    const value = browserColumnValue(rule, column.key);
    const classNames = [
        "rules-core-library-cell",
        column.key === "name" ? "rules-core-library-cell--name" : "",
        column.key === "source" ? "rules-core-library-cell--source" : "",
        column.align === "center" ? "is-center" : ""
    ].filter(Boolean).join(" ");

    if (column.key === "name") {
        return element("span", { className: classNames },
            element("span", {
                className: "rules-core-library-row-name",
                text: rule.displayName
            }),
            rule.hasCampaignOverride
                ? element("span", {
                    className: "rules-core-library-row-override",
                    text: "Campaign override"
                })
                : null);
    }

    return element("span", {
        className: classNames,
        text: value || "—",
        title: value || undefined
    });
}

function browserColumnValue(rule, key) {
    if (key === "name") return rule.displayName;
    if (key === "entityType") return humanizeEntityType(rule.entityType);
    if (key === "source") return rule.sourceCode || "D&D";
    if (key === "parentClass") {
        return (rule.relationships ?? [])
            .filter(relationship =>
                relationship.kind === "parent-class"
                && relationship.relatedEntityType === "class")
            .map(relationship => relationship.relatedDisplayName)
            .join(", ");
    }

    return (rule.browserFields ?? [])
        .find(field => field.key === key)?.value
        ?? "";
}

export function renderContinuousIndexFooter(
    container,
    loadedCount,
    totalCount,
    hasMore,
    isLoadingMore,
    loadError,
    onLoadMore)
{
    container.replaceChildren();
    container.hidden = !hasMore && !loadError && !isLoadingMore;
    if (container.hidden) return;

    if (loadError) {
        container.append(element("span", {
            text: `Could not load more: ${loadError}`
        }));
    }

    if (!hasMore) return;

    const button = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary",
        text: isLoadingMore ? "Loading…" : loadError ? "Retry" : "Load more",
        disabled: isLoadingMore
    });
    button.addEventListener("click", onLoadMore);
    container.append(button);
}

function humanizeEntityType(entityType) {
    const value = String(entityType ?? "");
    if (!value) return "Rule";
    return value
        .replace(/([a-z])([A-Z])/g, "$1 $2")
        .replace(/^./, match => match.toUpperCase());
}
