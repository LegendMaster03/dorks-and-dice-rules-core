import {
    alertNode,
    badge,
    clear,
    definitionList,
    describeError,
    element,
    formatDate,
    setButtonBusy,
    DEFAULT_PAGE_SIZE
} from "./ui.js";
import { renderResolvedRule } from "./rule-renderers.js";
import { renderSemanticComparison } from "./semantic-comparison.js";

const DORKS_MODE = "dorks-and-dice";
const PAGE_SIZE = DEFAULT_PAGE_SIZE;
const ROUTE_FAMILIES = new Map([
    ["monsters", "monster"],
    ["spells", "spell"],
    ["classes", "class"],
    ["subclasses", "subclass"],
    ["prestige-classes", "prestigeClass"],
    ["feats", "feat"],
    ["races", "race"],
    ["species", "species"],
    ["items", "item"],
    ["conditions", "condition"],
    ["skills", "skill"]
]);
const ENTITY_TYPES = [
    ["", "All"],
    ["monster", "Monsters"],
    ["spell", "Spells"],
    ["class", "Classes"],
    ["subclass", "Subclasses"],
    ["prestigeClass", "Prestige classes"],
    ["feat", "Feats"],
    ["race", "Races"],
    ["species", "Species"],
    ["item", "Items"],
    ["condition", "Conditions"],
    ["skill", "Skills"],
    ["houseRule", "House rules"],
    ["rule", "Other rules"]
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

export function installResolvedRulesBrowser(app) {
    app.canBrowseRules = app.hostContext.siteMode === DORKS_MODE;
    app.browserScope = "global";
    app.browserPage = 0;
    app.browserFilters = { entityType: "", query: "" };
    app.browserDeepLink = null;
    app.browserSelectedConceptKey = null;

    const route = parseToolRoute(app.hostContext.toolRoute);
    app.browserRouteRequested = Boolean(route.entityType || route.conceptKey);
    if (route.entityType) app.browserFilters.entityType = route.entityType;
    if (route.conceptKey) {
        app.browserDeepLink = route.conceptKey;
        app.browserSelectedConceptKey = route.conceptKey;
    }

    if (app.canBrowseRules) app.activeView = "library";

    app.browserKeyboard ??= { focusSearch: null, selectRelative: null };
    if (!app.browserKeyboardBound) {
        app.browserKeyboardBound = true;
        window.addEventListener("keydown", event => {
            if (app.activeView !== "library" || event.altKey || event.ctrlKey || event.metaKey) return;
            if (isEditableTarget(event.target)) return;

            const key = String(event.key ?? "").toLowerCase();
            if (key === "j") {
                event.preventDefault();
                app.browserKeyboard.selectRelative?.(1);
                return;
            }
            if (key === "k") {
                event.preventDefault();
                app.browserKeyboard.selectRelative?.(-1);
                return;
            }
            if (key === "f" || key === "/") {
                event.preventDefault();
                app.browserKeyboard.focusSearch?.();
            }
        });
    }

    app.viewNavigation ??= {};
    app.viewNavigation.library = async () => {
        app.browserDeepLink = null;
        app.browserSelectedConceptKey = null;
        app.libraryDeepLink = null;
        app.libraryRouteActive = false;
        app.activeView = "library";
        pushToolRoute(app, catalogRouteForEntity(app.browserFilters.entityType));
        await app.render();
    };

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        if (app.activeView === "library") {
            await renderRulesBrowser(app, container);
            return;
        }
        await renderActiveView(container);
    };

    window.addEventListener("popstate", async () => {
        if (!app.canBrowseRules) return;

        const toolRoute = currentToolRoute(app);
        if (toolRoute === "/sources" || toolRoute.startsWith("/sources/")) return;

        const next = parseToolRoute(toolRoute);
        app.browserFilters.entityType = next.entityType ?? "";
        app.browserDeepLink = next.conceptKey ?? null;
        app.browserSelectedConceptKey = next.conceptKey ?? null;
        app.activeView = "library";
        await app.render();
    });
}

async function renderRulesBrowser(app, container) {
    clear(container);

    const shell = element("section", { className: "rules-core-library-shell" });
    const headingTitle = element("h2", {
        className: "rules-core-library-title",
        text: libraryTitle(app.browserFilters.entityType)
    });
    const headingSubtitle = element("p", {
        className: "rules-core-library-subtitle",
        text: librarySubtitle(app.browserFilters.entityType)
    });
    const heading = element("div", { className: "rules-core-library-heading" },
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: "RULES LIBRARY" }),
            headingTitle,
            headingSubtitle),
        element("div", {
            className: "rules-core-library-revision",
            text: "Loading published rules…"
        }));

    const controls = element("div", { className: "rules-core-library-controls" });
    const scope = element("select", {
        className: "form-select form-select-sm rules-core-library-scope",
        ariaLabel: "Rules scope"
    });
    scope.append(element("option", { value: "global", text: "Dorks & Dice" }));
    for (const campaign of app.campaigns) {
        scope.append(element("option", {
            value: `campaign:${campaign.id}`,
            text: campaign.name ?? `Campaign ${campaign.id}`
        }));
    }
    scope.value = app.browserScope;

    const type = element("select", {
        className: "form-select form-select-sm rules-core-library-type",
        ariaLabel: "Rule type"
    });
    for (const [value, label] of ENTITY_TYPES) {
        type.append(element("option", { value, text: label }));
    }
    type.value = app.browserFilters.entityType;

    controls.append(scope, type);

    const search = element("input", {
        className: "form-control form-control-sm rules-core-library-search",
        type: "search",
        value: app.browserFilters.query,
        placeholder: "Search rules…",
        ariaLabel: "Search rules"
    });
    const reset = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary rules-core-library-reset",
        text: "Reset"
    });
    const indexStatus = element("span", {
        className: "rules-core-library-search-status",
        text: "Loading…"
    });
    const searchGroup = element("div", { className: "rules-core-library-search-group" },
        element("div", { className: "rules-core-library-search-wrap" },
            search,
            element("span", {
                className: "rules-core-library-search-hint",
                text: "F"
            })),
        indexStatus,
        reset);

    const workspace = element("div", { className: "rules-core-library-workspace" });
    const index = element("section", {
        className: "rules-core-library-index",
        ariaLabel: "Rules index"
    });
    const indexHeader = element("div", { className: "rules-core-library-index-header" });
    renderIndexHeader(indexHeader, app.browserFilters.entityType);
    const list = element("div", {
        className: "rules-core-library-index-list",
        role: "listbox",
        ariaLabel: "Published rules"
    });
    const indexFooter = element("div", { className: "rules-core-library-index-footer" });
    index.append(searchGroup, indexHeader, list, indexFooter);

    const detail = element("section", {
        className: "rules-core-library-detail",
        ariaLabel: "Selected rule"
    });
    detail.append(renderEmptyDetail("Select a rule from the list."));

    workspace.append(index, detail);
    shell.append(heading, controls, workspace);
    container.append(shell);

    let loadSerial = 0;
    let detailSerial = 0;
    let rowByConceptKey = new Map();
    let currentRules = [];
    let searchTimer = null;
    let preserveDeepLink = Boolean(app.browserDeepLink);

    app.browserKeyboard.focusSearch = () => {
        search.focus();
        search.select();
    };
    app.browserKeyboard.selectRelative = direction => {
        if (!currentRules.length) return;

        const currentIndex = currentRules.findIndex(rule =>
            rule.conceptKey === app.browserSelectedConceptKey);
        const startIndex = currentIndex >= 0
            ? currentIndex
            : direction > 0 ? -1 : 0;
        const nextIndex = (startIndex + direction + currentRules.length) % currentRules.length;
        const rule = currentRules[nextIndex];
        rowByConceptKey.get(rule.conceptKey)?.scrollIntoView?.({ block: "nearest" });
        pushToolRoute(app, rule.browserLink?.toolRelativePath);
        void renderSelection(rule.conceptKey);
    };

    const renderSelection = async conceptKey => {
        const serial = ++detailSerial;
        app.browserSelectedConceptKey = conceptKey;
        for (const [key, row] of rowByConceptKey) {
            const selected = key === conceptKey;
            row.classList.toggle("is-selected", selected);
            row.setAttribute("aria-selected", selected ? "true" : "false");
        }
        await renderRuleDetailPane(
            app,
            detail,
            conceptKey,
            app.browserScope,
            serial,
            () => detailSerial);
    };

    const load = async ({ resetPage = false, keepSelection = false } = {}) => {
        const serial = ++loadSerial;
        if (resetPage) app.browserPage = 0;

        app.browserScope = scope.value;
        app.browserFilters = {
            entityType: type.value,
            query: search.value.trim()
        };

        list.replaceChildren(element("div", {
            className: "rules-core-library-loading",
            text: "Loading rules…"
        }));
        indexFooter.replaceChildren();

        try {
            const filters = {
                entityType: app.browserFilters.entityType || null,
                query: app.browserFilters.query || null,
                limit: PAGE_SIZE + 1,
                offset: app.browserPage * PAGE_SIZE
            };
            const requested = app.browserScope === "global"
                ? await app.api.getGlobalRulesCatalog(filters)
                : await app.api.getCampaignRulesCatalog(
                    app.browserScope.slice("campaign:".length),
                    filters);
            if (serial !== loadSerial) return;

            const hasNext = (requested.rules?.length ?? 0) > PAGE_SIZE;
            const rules = (requested.rules ?? []).slice(0, PAGE_SIZE);
            currentRules = rules;
            if (!rules.length && app.browserPage > 0) {
                app.browserPage -= 1;
                await load({ keepSelection });
                return;
            }

            headingTitle.textContent = libraryTitle(app.browserFilters.entityType);
            headingSubtitle.textContent = librarySubtitle(app.browserFilters.entityType);
            heading.querySelector(".rules-core-library-revision").textContent = requested.revisionNumber
                ? `${scopeLabel(app, app.browserScope)} · published #${requested.revisionNumber} · ${formatDate(requested.publishedAt)}`
                : `${scopeLabel(app, app.browserScope)} · no published ruleset`;
            const totalCount = requested.totalCount ?? rules.length;
            const firstVisible = rules.length ? app.browserPage * PAGE_SIZE + 1 : 0;
            const lastVisible = rules.length ? firstVisible + rules.length - 1 : 0;
            indexStatus.textContent = requested.revisionNumber
                ? `${firstVisible}–${lastVisible} / ${totalCount}`
                : "No published rules";
            renderIndexHeader(indexHeader, app.browserFilters.entityType);

            renderRuleRows(list, rules, app.browserFilters.entityType, async rule => {
                preserveDeepLink = false;
                pushToolRoute(app, rule.browserLink?.toolRelativePath);
                await renderSelection(rule.conceptKey);
            });
            rowByConceptKey = new Map(
                Array.from(list.querySelectorAll("[data-concept-key]"))
                    .map(row => [row.dataset.conceptKey, row]));

            renderIndexFooter(indexFooter, app.browserPage, hasNext, async nextPage => {
                preserveDeepLink = false;
                app.browserPage = nextPage;
                app.browserSelectedConceptKey = null;
                await load();
            });

            if (!requested.revisionNumber) {
                detail.replaceChildren(renderEmptyDetail(
                    app.browserScope === "global"
                        ? "No global ruleset has been published yet."
                        : "This campaign has no published ruleset yet."));
                return;
            }
            if (!rules.length) {
                detail.replaceChildren(renderEmptyDetail("No rules match the current filters."));
                return;
            }

            let conceptKey = keepSelection ? app.browserSelectedConceptKey : null;
            if (preserveDeepLink && app.browserDeepLink) {
                conceptKey = app.browserDeepLink;
                app.browserDeepLink = null;
            } else if (!conceptKey || !rules.some(rule => rule.conceptKey === conceptKey)) {
                conceptKey = rules[0].conceptKey;
            }
            await renderSelection(conceptKey);
        } catch (error) {
            if (serial !== loadSerial) return;
            list.replaceChildren(alertNode("danger", describeError(error)));
            detail.replaceChildren(renderEmptyDetail("The rule list could not be loaded."));
        }
    };

    scope.addEventListener("change", async () => {
        await load({ resetPage: true, keepSelection: true });
    });
    type.addEventListener("change", async () => {
        preserveDeepLink = false;
        app.browserSelectedConceptKey = null;
        pushToolRoute(app, catalogRouteForEntity(type.value));
        await load({ resetPage: true });
    });
    reset.addEventListener("click", async () => {
        preserveDeepLink = false;
        if (searchTimer) clearTimeout(searchTimer);
        search.value = "";
        type.value = "";
        app.browserSelectedConceptKey = null;
        pushToolRoute(app, "/");
        await load({ resetPage: true });
        search.focus();
    });
    search.addEventListener("input", () => {
        preserveDeepLink = false;
        if (searchTimer) clearTimeout(searchTimer);
        searchTimer = setTimeout(async () => {
            app.browserSelectedConceptKey = null;
            await load({ resetPage: true });
        }, 220);
    });
    search.addEventListener("keydown", async event => {
        if (event.key !== "Enter") return;
        event.preventDefault();
        if (searchTimer) clearTimeout(searchTimer);
        preserveDeepLink = false;
        app.browserSelectedConceptKey = null;
        await load({ resetPage: true });
    });

    await load({ keepSelection: true });
}

function isEditableTarget(target) {
    if (!(target instanceof Element)) return false;
    return Boolean(target.closest("input, textarea, select, button, [contenteditable='true']"));
}

function libraryTitle(entityType) {
    if (!entityType) return "Rules Library";
    return ENTITY_TYPES.find(([value]) => value === entityType)?.[1]
        ?? humanizeEntityType(entityType);
}

function librarySubtitle(entityType) {
    const subject = entityType ? libraryTitle(entityType) : "Rules";
    return `One concept per row. Search ${subject.toLowerCase()} on the left and view the selected rule on the right. Press J/K to navigate; F or / focuses search.`;
}

function browserColumns(entityType) {
    return BROWSER_COLUMNS.get(entityType)
        ?? BROWSER_COLUMNS.get("")
        ?? [];
}

function renderIndexHeader(container, entityType) {
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

function renderRuleRows(container, rules, entityType, onSelect) {
    container.replaceChildren();
    if (!rules.length) {
        container.append(element("div", {
            className: "rules-core-library-empty-list",
            text: "No rules match the current filters."
        }));
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

function renderIndexFooter(container, page, hasNext, onPage) {
    const previous = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary",
        text: "Previous",
        disabled: page === 0
    });
    const next = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary",
        text: "Next",
        disabled: !hasNext
    });
    previous.addEventListener("click", () => onPage(Math.max(0, page - 1)));
    next.addEventListener("click", () => onPage(page + 1));
    container.append(
        previous,
        element("span", { text: `Page ${page + 1}` }),
        next);
}

async function renderRuleDetailPane(app, container, conceptKey, scopeValue, requestSerial, getCurrentSerial) {
    container.replaceChildren(element("div", {
        className: "rules-core-library-detail-loading",
        text: "Loading rule…"
    }));

    const campaignId = scopeValue.startsWith("campaign:")
        ? scopeValue.slice("campaign:".length)
        : null;
    try {
        const [resolved, versions, baseline] = await Promise.all([
            campaignId
                ? app.api.getCampaignResolvedRule(campaignId, conceptKey)
                : app.api.getGlobalResolvedRule(conceptKey),
            getOptionalRuleVersions(app, conceptKey),
            campaignId
                ? getOptionalCampaignBaseline(app, campaignId, conceptKey)
                : null
        ]);
        if (requestSerial !== getCurrentSerial()) return;

        const tabBar = element("div", { className: "rules-core-version-tabs" });
        const body = element("div", { className: "rules-core-library-detail-body" });
        const effectiveLabel = campaignId
            ? campaignName(app, campaignId)
            : "Dorks & Dice";

        const tabs = [{
            key: "effective",
            label: effectiveLabel,
            title: campaignId ? "Effective campaign rule" : "Dorks & Dice combined rule",
            render: () => renderEffectiveRule(body, resolved, baseline, Boolean(campaignId))
        }];

        for (const version of versions?.versions ?? []) {
            tabs.push({
                key: `source:${version.canonicalEntityId}`,
                label: version.sourceCode || version.formatKey || version.packageDisplayName,
                title: `${version.packageDisplayName} · rev. ${version.sourceRevisionNumber}`,
                render: () => renderSourceVersion(body, versions, version, resolved)
            });
        }

        if ((versions?.versions?.length ?? 0) > 1) {
            tabs.push({
                key: "compare",
                label: "Compare",
                title: "Compare source versions",
                render: () => renderComparisonTab(app, body, resolved, versions, campaignId)
            });
        }

        let activeKey = "effective";
        const buttons = new Map();
        const activate = key => {
            activeKey = key;
            for (const [buttonKey, button] of buttons) {
                const selected = buttonKey === activeKey;
                button.classList.toggle("is-active", selected);
                button.setAttribute("aria-selected", selected ? "true" : "false");
            }
            const tab = tabs.find(value => value.key === activeKey) ?? tabs[0];
            tab.render();
            app.presentRenderedFragment?.(body);
        };

        tabBar.append(element("span", {
            className: "rules-core-version-tabs-label",
            text: "View"
        }));
        for (const tab of tabs) {
            const button = element("button", {
                type: "button",
                className: "rules-core-version-tab",
                text: tab.label,
                title: tab.title,
                attributes: {
                    role: "tab",
                    "aria-selected": tab.key === activeKey ? "true" : "false"
                }
            });
            button.addEventListener("click", () => activate(tab.key));
            buttons.set(tab.key, button);
            tabBar.append(button);
        }

        const context = element("div", { className: "rules-core-version-tabs-context" },
            badge(humanizeEntityType(resolved.entityType), "secondary"));
        if ((versions?.versions?.length ?? 0) > 1) {
            context.append(element("span", {
                className: "rules-core-version-count",
                text: `${versions.versions.length} source versions`
            }));
        }
        tabBar.append(context);

        container.replaceChildren(tabBar, body);
        activate("effective");
        app.presentRenderedFragment?.(container);
    } catch (error) {
        if (requestSerial !== getCurrentSerial()) return;
        container.replaceChildren(alertNode("danger", describeError(error)));
        app.presentRenderedFragment?.(container);
    }
}

function renderEffectiveRule(container, resolved, baseline, campaignScope) {
    clear(container);
    const isMonster = String(resolved.entityType ?? "").toLowerCase() === "monster";

    if (isMonster) {
        container.append(element("section", { className: "rules-core-effective-rule" },
            renderResolvedRule(resolved.entityType, resolved.document, {
                displayName: resolved.displayName,
                showDocument: false
            })));
        container.append(renderRuleContextDisclosure(resolved, campaignScope));
    } else {
        container.append(renderMetadata(resolved, campaignScope));
        container.append(element("section", { className: "rules-core-effective-rule" },
            renderResolvedRule(resolved.entityType, resolved.document, {
                displayName: resolved.displayName
            })));
    }

    if (campaignScope) {
        const campaign = element("div", {
            className: "card card-body mb-3 rules-core-detail-context-card"
        });
        campaign.append(
            element("h4", { className: "h5", text: "Campaign overlay" }),
            definitionList([
                ["Pinned global baseline", `#${resolved.baselineRulesetRevisionNumber}`],
                ["Global decision", `#${resolved.globalDecisionNumber} · ${resolved.globalDecisionKind}`],
                ["Campaign decision", resolved.campaignDecisionNumber
                    ? `#${resolved.campaignDecisionNumber} · ${resolved.effectiveDecisionKind}`
                    : "Inherited without campaign override"],
                ["Campaign note", resolved.campaignDecisionNote]
            ]));
        container.append(campaign);

        if (baseline) {
            const details = element("details", {
                className: "card card-body mb-3 rules-core-detail-context-card"
            });
            details.append(
                element("summary", {
                    className: "fw-semibold",
                    text: `Published global baseline #${baseline.baselineRulesetRevisionNumber}`
                }),
                element("div", { className: "mt-3" },
                    renderResolvedRule(baseline.entityType, baseline.document, {
                        displayName: baseline.displayName,
                        showDocument: !isMonster
                    })));
            container.append(details);
        } else {
            container.append(alertNode(
                "secondary",
                "The pinned global baseline is not available to this account under the independent source-access rules."));
        }
    }

    if (!isMonster) container.append(renderProvenance(resolved));
}

function renderSourceVersion(container, versions, version, resolved) {
    clear(container);
    container.append(element("div", { className: "rules-core-source-version-heading" },
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: "SOURCE VERSION" }),
            element("h3", { className: "h4 mb-1", text: versions.displayName }),
            element("div", {
                className: "text-body-secondary",
                text: [
                    version.sourceCode,
                    version.packageDisplayName,
                    `rev. ${version.sourceRevisionNumber}`
                ].filter(Boolean).join(" · ")
            })),
        version.sourceEntityRevisionId === resolved.sourceEntityRevisionId
            ? badge("Selected source", "primary")
            : null));

    container.append(element("section", { className: "rules-core-effective-rule" },
        renderResolvedRule(versions.entityType, version.document, {
            displayName: versions.displayName,
            showDocument: String(versions.entityType).toLowerCase() !== "monster"
        })));

    const details = element("details", { className: "rules-core-context-disclosure" });
    details.append(
        element("summary", { text: "Source provenance" }),
        element("div", { className: "rules-core-context-disclosure-body" },
            definitionList([
                ["Source", version.sourceEntityName],
                ["Source code", version.sourceCode],
                ["Package", version.packageDisplayName],
                ["Format", version.formatKey],
                ["Source revision", `#${version.sourceRevisionNumber}`],
                ["Imported", formatDate(version.importedAt)],
                ["Equivalent representations", String(version.equivalentRepresentationCount)]
            ])));
    container.append(details);
}

function renderComparisonTab(app, container, resolved, versions, campaignId) {
    clear(container);

    const available = versions.versions ?? [];
    const heading = element("div", { className: "rules-core-comparison-heading" },
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: "VERSION DIFFERENCES" }),
            element("h3", { className: "h4 mb-1", text: `Compare ${versions.displayName}` }),
            element("p", {
                className: "small text-body-secondary mb-0",
                text: "Compare rule-bearing content without collapsing the source versions into one representation."
            })));
    container.append(heading);

    if (available.length < 2) {
        container.append(alertNode("secondary", "At least two accessible source versions are required."));
        return;
    }

    const left = element("select", {
        className: "form-select form-select-sm",
        ariaLabel: "Left source version"
    });
    const right = element("select", {
        className: "form-select form-select-sm",
        ariaLabel: "Right source version"
    });
    for (const version of available) {
        const label = sourceVersionLabel(version);
        left.append(element("option", { value: version.sourceEntityRevisionId, text: label }));
        right.append(element("option", { value: version.sourceEntityRevisionId, text: label }));
    }
    left.value = available[0].sourceEntityRevisionId;
    right.value = available[1].sourceEntityRevisionId;

    const compare = element("button", {
        type: "button",
        className: "btn btn-sm btn-primary",
        text: "Compare"
    });
    const result = element("div", { className: "rules-core-comparison-result" });
    const controls = element("div", { className: "rules-core-comparison-controls" },
        comparisonField("Left", left),
        comparisonField("Right", right),
        element("div", { className: "rules-core-comparison-action" }, compare));
    container.append(controls, result);

    compare.addEventListener("click", async () => {
        result.replaceChildren();
        if (left.value === right.value) {
            result.append(alertNode("secondary", "Choose two different source versions."));
            return;
        }

        setButtonBusy(compare, true, "Comparing…");
        try {
            const comparison = await app.api.compareRuleVersions({
                ruleConceptId: resolved.ruleConceptId,
                leftSourceEntityRevisionId: left.value,
                rightSourceEntityRevisionId: right.value
            });
            renderSemanticComparison(result, comparison);
            const adjudication = adjudicationButton(app, resolved.ruleConceptId, campaignId);
            if (adjudication) {
                result.append(element("div", { className: "rules-core-comparison-adjudication" },
                    element("div", {},
                        element("strong", { text: "Need a ruling?" }),
                        element("div", {
                            className: "small text-body-secondary",
                            text: campaignId
                                ? "Open this concept in the campaign rule editor."
                                : "Open this concept in the global Rules Lawyer editor."
                        })),
                    adjudication));
            }
        } catch (error) {
            result.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(compare, false);
        }
    });

    compare.click();
}

function comparisonField(label, control) {
    return element("label", { className: "rules-core-comparison-field" },
        element("span", { text: label }),
        control);
}

function sourceVersionLabel(version) {
    return [
        version.sourceCode || version.formatKey,
        version.packageDisplayName,
        `rev. ${version.sourceRevisionNumber}`
    ].filter(Boolean).join(" · ");
}

function adjudicationButton(app, ruleConceptId, campaignId) {
    const campaignCanEdit = campaignId
        && app.dmCampaigns?.some(value => String(value.id) === String(campaignId));
    if (!campaignId && !app.canEditGlobal) return null;
    if (campaignId && !campaignCanEdit) return null;

    return element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-primary",
        text: campaignId ? "Open campaign ruling" : "Open global ruling",
        onClick: async () => openAdjudication(app, ruleConceptId, campaignId)
    });
}

async function openAdjudication(app, ruleConceptId, campaignId) {
    if (campaignId) {
        app.activeView = "campaign";
        app.activeCampaignId = campaignId;
    } else {
        app.activeView = "global";
    }

    await app.render();
    const body = app.root.querySelector(".rules-core-main");
    if (!body) return;

    if (campaignId) {
        await app.renderCampaignConcept(body, ruleConceptId);
    } else {
        await app.renderGlobalConcept(body, ruleConceptId);
    }
}

function renderMetadata(resolved, campaignScope) {
    return element("div", { className: "rules-core-detail-heading" },
        element("div", {},
            element("div", {
                className: "rules-core-eyebrow",
                text: campaignScope ? "CAMPAIGN RULE" : "DORKS & DICE RULE"
            }),
            element("h3", { className: "h3 mb-1", text: resolved.displayName }),
            element("div", {
                className: "text-body-secondary font-monospace small",
                text: resolved.conceptKey
            })),
        element("div", { className: "rules-core-detail-heading-meta" },
            badge(humanizeEntityType(resolved.entityType), "primary"),
            element("span", {
                className: "small text-body-secondary",
                text: `Published #${resolved.campaignRulesetRevisionNumber ?? resolved.rulesetRevisionNumber}`
            })));
}

function renderRuleContextDisclosure(resolved, campaignScope) {
    const details = element("details", { className: "rules-core-context-disclosure" });
    const body = element("div", { className: "rules-core-context-disclosure-body" });
    body.append(definitionList([
        ["Concept", resolved.conceptKey],
        ["Scope", campaignScope ? "Campaign effective rule" : "Published Dorks & Dice rule"],
        ["Published revision", `#${resolved.campaignRulesetRevisionNumber ?? resolved.rulesetRevisionNumber}`],
        ["Decision", resolved.effectiveDecisionKind ?? resolved.decisionKind],
        ["Selected source", `${resolved.sourceEntityName} · ${resolved.sourceCode} · rev. ${resolved.sourceRevisionNumber}`],
        ["Package", resolved.packageDisplayName],
        ["Decision note", resolved.decisionNote ?? resolved.campaignDecisionNote]
    ]));
    appendContributions(body, resolved);
    details.append(element("summary", { text: "Rule context and provenance" }), body);
    return details;
}

function renderProvenance(resolved) {
    const card = element("details", { className: "rules-core-context-disclosure" });
    const body = element("div", { className: "rules-core-context-disclosure-body" });
    body.append(definitionList([
        ["Selected source", `${resolved.sourceEntityName} · ${resolved.sourceCode} · rev. ${resolved.sourceRevisionNumber}`],
        ["Package", resolved.packageDisplayName],
        ["Decision note", resolved.decisionNote ?? resolved.campaignDecisionNote],
        ["Additional contributing sources", String((resolved.contributions ?? resolved.globalContributions ?? []).length)]
    ]));
    appendContributions(body, resolved);
    card.append(element("summary", { text: "Rule context and provenance" }), body);
    return card;
}

function appendContributions(container, resolved) {
    const contributions = resolved.contributions ?? resolved.globalContributions ?? [];
    if (!contributions.length) return;
    const list = element("ul", { className: "mb-0 mt-3" });
    for (const contribution of contributions) {
        list.append(element("li", {},
            element("span", {
                className: "fw-semibold",
                text: `${contribution.sourceEntityName} · ${contribution.sourceCode || contribution.editionDisplayName}`
            }),
            ` — ${contribution.contributionKind}${contribution.note ? `: ${contribution.note}` : ""}`));
    }
    container.append(list);
}

function renderEmptyDetail(message) {
    return element("div", { className: "rules-core-library-detail-empty" },
        element("div", { className: "rules-core-library-detail-empty-mark", text: "R" }),
        element("p", { text: message }));
}

async function getOptionalRuleVersions(app, conceptKey) {
    try {
        return await app.api.getRuleVersions(conceptKey);
    } catch (error) {
        if (error?.status === 404) return null;
        throw error;
    }
}

async function getOptionalCampaignBaseline(app, campaignId, conceptKey) {
    try {
        return await app.api.backend(
            `/api/campaigns/${encodeURIComponent(campaignId)}/rules/${encodeURIComponent(conceptKey)}/global-baseline`);
    } catch (error) {
        if (error?.status === 404) return null;
        throw error;
    }
}

function scopeLabel(app, scopeValue) {
    if (scopeValue === "global") return "Dorks & Dice";
    return campaignName(app, scopeValue.slice("campaign:".length));
}

function campaignName(app, campaignId) {
    return app.campaigns.find(value => String(value.id) === String(campaignId))?.name
        ?? "Campaign";
}

function humanizeEntityType(entityType) {
    const value = String(entityType ?? "");
    if (!value) return "Rule";
    return value
        .replace(/([a-z])([A-Z])/g, "$1 $2")
        .replace(/^./, match => match.toUpperCase());
}

function parseToolRoute(toolRoute) {
    if (!toolRoute || toolRoute === "/") return {};
    const path = String(toolRoute).split(/[?#]/, 1)[0];
    const segments = path.replace(/^\/+|\/+$/g, "").split("/").filter(Boolean);
    if (segments.length === 1 && ROUTE_FAMILIES.has(segments[0])) {
        return { entityType: ROUTE_FAMILIES.get(segments[0]) };
    }
    if (segments.length === 2 && ROUTE_FAMILIES.has(segments[0])) {
        const entityType = ROUTE_FAMILIES.get(segments[0]);
        return {
            entityType,
            conceptKey: `${entityType}.${decodeURIComponent(segments[1])}`
        };
    }
    if (segments.length === 2 && segments[0] === "rules") {
        return { conceptKey: decodeURIComponent(segments[1]) };
    }
    return {};
}

function currentToolRoute(app) {
    const base = (app.hostContext.toolBasePath ?? "/tools/rules-core").replace(/\/$/, "");
    const path = window.location.pathname;
    return path.startsWith(base) ? path.slice(base.length) || "/" : "/";
}

function browserHref(app, toolRelativePath) {
    const base = app.hostContext.toolBasePath ?? "/tools/rules-core";
    return `${base.replace(/\/$/, "")}${toolRelativePath || "/"}`;
}

function pushToolRoute(app, toolRelativePath) {
    const href = browserHref(app, toolRelativePath || "/");
    if (window.location.pathname !== href) window.history.pushState({}, "", href);
}

function catalogRouteForEntity(entityType) {
    for (const [segment, mappedType] of ROUTE_FAMILIES.entries()) {
        if (mappedType === entityType) return `/${segment}`;
    }
    return "/";
}
