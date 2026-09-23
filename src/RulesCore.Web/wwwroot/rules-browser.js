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
import { renderRuleDetailPane } from "./rules-browser-detail.js";
import {
    RULE_FAMILY_TABS,
    libraryTitle,
    renderContinuousIndexFooter,
    renderIndexHeader,
    renderRuleRows
} from "./rules-browser-index.js";

export { RULE_FAMILY_TABS } from "./rules-browser-index.js";

const DORKS_MODE = "dorks-and-dice";
const PAGE_SIZE = DEFAULT_PAGE_SIZE;
const ROUTE_FAMILIES = new Map([
    ["monsters", "monster"],
    ["spells", "spell"],
    ["classes", "class"],
    ["subclasses", "subclass"],
    ["prestige-classes", "prestigeClass"],
    ["feats", "feat"],
    ["backgrounds", "background"],
    ["optional-features", "optionalfeature"],
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
    ["background", "Backgrounds"],
    ["optionalfeature", "Options & features"],
    ["race", "Races"],
    ["species", "Species"],
    ["item", "Items"],
    ["condition", "Conditions"],
    ["skill", "Skills"],
    ["houseRule", "House rules"],
    ["rule", "Other rules"]
];

export function installResolvedRulesBrowser(app) {
    app.canBrowseRules = app.hostContext.siteMode === DORKS_MODE;
    app.browserScope = "global";
    app.browserPage = 0;
    app.browserFilters = {
        entityType: "",
        query: "",
        sourceCode: "",
        overridesOnly: false
    };
    app.browserDeepLink = null;
    app.browserSelectedConceptKey = null;

    const routeScope = parseBrowserScopeFromLocation(app);
    if (routeScope) app.browserScope = routeScope;

    const route = parseToolRoute(app.hostContext.toolRoute);
    app.browserRouteRequested = Boolean(route.entityType || route.conceptKey);
    if (route.entityType) app.browserFilters.entityType = route.entityType;
    if (route.conceptKey) {
        app.browserDeepLink = route.conceptKey;
        app.browserSelectedConceptKey = route.conceptKey;
    }

    if (app.canBrowseRules) app.activeView = "library";

    app.ruleFamilyTabs = RULE_FAMILY_TABS;
    app.navigateRuleFamily = async entityType => {
        const nextEntityType = entityType ?? "";
        app.browserDeepLink = null;
        app.browserSelectedConceptKey = null;
        app.libraryDeepLink = null;
        app.libraryRouteActive = false;
        app.browserFilters.entityType = nextEntityType;
        app.activeView = "library";
        pushToolRoute(app, catalogRouteForEntity(nextEntityType), app.browserScope);
        await app.render();
    };

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
    app.viewNavigation.library = async () =>
        app.navigateRuleFamily(app.browserFilters.entityType);

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
        app.browserScope = parseBrowserScopeFromLocation(app) ?? "global";
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
    const heading = element("div", { className: "rules-core-library-heading" },
        headingTitle,
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

    const knownEntityTypes = new Set(ENTITY_TYPES.map(([value]) => value));
    const moreTypes = element("select", {
        className: "form-select form-select-sm rules-core-library-more-types",
        ariaLabel: "More rule types",
        attributes: { hidden: "" }
    });

    const populateEntityTypeFacets = facets => {
        const currentEntityType = app.browserFilters.entityType ?? "";
        const dynamicTypes = [];
        for (const facet of facets ?? []) {
            if (!facet.entityType || knownEntityTypes.has(facet.entityType)) continue;
            if (dynamicTypes.some(value => value.entityType === facet.entityType)) continue;
            dynamicTypes.push(facet);
        }

        if (currentEntityType
            && !knownEntityTypes.has(currentEntityType)
            && !dynamicTypes.some(value => value.entityType === currentEntityType)) {
            dynamicTypes.unshift({
                entityType: currentEntityType,
                count: null
            });
        }

        moreTypes.replaceChildren(element("option", {
            value: "",
            text: dynamicTypes.length ? "More rule types…" : "No additional rule types"
        }));
        for (const facet of dynamicTypes) {
            moreTypes.append(element("option", {
                value: facet.entityType,
                text: facet.count === null || facet.count === undefined
                    ? pluralizeEntityType(facet.entityType)
                    : `${pluralizeEntityType(facet.entityType)} (${facet.count})`
            }));
        }

        moreTypes.hidden = dynamicTypes.length === 0;
        moreTypes.value = currentEntityType && !knownEntityTypes.has(currentEntityType)
            ? currentEntityType
            : "";
    };

    controls.append(scope, moreTypes);

    const search = element("input", {
        className: "form-control form-control-sm rules-core-library-search",
        type: "search",
        value: app.browserFilters.query,
        placeholder: "Search rules…",
        ariaLabel: "Search rules",
        title: "Press F or / to focus search. Use J/K to move through results."
    });
    const filterToggle = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary rules-core-library-filter-toggle",
        text: "Filters",
        attributes: {
            "aria-expanded": "false"
        }
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
        filterToggle,
        reset);

    const sourceFilter = element("select", {
        className: "form-select form-select-sm",
        ariaLabel: "Filter by source"
    }, element("option", { value: "", text: "All sources" }));
    const overrideFilter = element("label", {
        className: "rules-core-library-filter-check"
    }, element("input", {
        type: "checkbox",
        className: "form-check-input"
    }), element("span", { text: "Campaign overrides only" }));
    const clearFilters = element("button", {
        type: "button",
        className: "btn btn-sm btn-link rules-core-library-filter-clear",
        text: "Clear filters"
    });
    const filterBar = element("div", {
        className: "rules-core-library-filterbar",
        attributes: { hidden: "" }
    }, element("label", { className: "rules-core-library-filter-field" },
        element("span", { text: "Source" }),
        sourceFilter),
    overrideFilter,
    clearFilters);

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
    index.append(searchGroup, filterBar, indexHeader, list, indexFooter);

    const detail = element("section", {
        className: "rules-core-library-detail",
        ariaLabel: "Selected rule",
        attributes: { tabindex: "-1" }
    });
    detail.append(renderEmptyDetail("Select a rule from the list."));

    workspace.append(index, detail);
    shell.append(heading, controls, workspace);
    container.append(shell);

    let loadSerial = 0;
    let detailSerial = 0;
    let rowByConceptKey = new Map();
    let currentRules = [];
    let totalCount = 0;
    let hasMore = false;
    let hasPublishedRuleset = false;
    let isLoadingMore = false;
    let loadMoreError = null;
    let searchTimer = null;
    let preserveDeepLink = Boolean(app.browserDeepLink);
    let loadMore = async () => [];

    app.browserKeyboard.focusSearch = () => {
        search.focus();
        search.select();
    };
    app.browserKeyboard.selectRelative = async direction => {
        if (!currentRules.length) return;

        const currentIndex = currentRules.findIndex(rule =>
            rule.conceptKey === app.browserSelectedConceptKey);
        if (direction > 0 && currentIndex === currentRules.length - 1 && hasMore) {
            const added = await loadMore();
            if (added.length) {
                const rule = added[0];
                rowByConceptKey.get(rule.conceptKey)?.scrollIntoView?.({ block: "nearest" });
                pushToolRoute(app, rule.browserLink?.toolRelativePath);
                await renderSelection(rule.conceptKey);
            }
            return;
        }

        const startIndex = currentIndex >= 0
            ? currentIndex
            : direction > 0 ? -1 : 0;
        const nextIndex = Math.max(
            0,
            Math.min(currentRules.length - 1, startIndex + direction));
        const rule = currentRules[nextIndex];
        if (!rule || rule.conceptKey === app.browserSelectedConceptKey) return;
        rowByConceptKey.get(rule.conceptKey)?.scrollIntoView?.({ block: "nearest" });
        pushToolRoute(app, rule.browserLink?.toolRelativePath);
        await renderSelection(rule.conceptKey);
    };

    const syncFilterControls = () => {
        const campaignScope = scope.value.startsWith("campaign:");
        overrideFilter.hidden = !campaignScope;
        if (!campaignScope) {
            overrideFilter.querySelector("input").checked = false;
        }

        const activeCount =
            (sourceFilter.value ? 1 : 0)
            + (campaignScope && overrideFilter.querySelector("input").checked ? 1 : 0);
        filterToggle.textContent = activeCount ? `Filters (${activeCount})` : "Filters";
        filterToggle.classList.toggle("is-active", activeCount > 0);
    };

    const populateSourceFacets = facets => {
        const selected = sourceFilter.value;
        sourceFilter.replaceChildren(element("option", {
            value: "",
            text: "All sources"
        }));
        for (const facet of facets ?? []) {
            sourceFilter.append(element("option", {
                value: facet.sourceCode,
                text: `${facet.sourceCode} (${facet.count})`
            }));
        }
        const selectedExists = Array.from(sourceFilter.options)
            .some(option => option.value === selected);
        if (selected && !selectedExists) {
            sourceFilter.append(element("option", {
                value: selected,
                text: `${selected} (0)`
            }));
        }
        sourceFilter.value = selected;
        syncFilterControls();
    };

    const showIndexOnCompactViewport = () => {
        workspace.classList.remove("has-selection");
        pushToolRoute(app, catalogRouteForEntity(app.browserFilters.entityType));
        const selectedRow = rowByConceptKey.get(app.browserSelectedConceptKey);
        selectedRow?.scrollIntoView?.({ block: "nearest" });
        selectedRow?.focus?.({ preventScroll: true });
    };

    const syncSelectedRowState = conceptKey => {
        for (const [key, row] of rowByConceptKey) {
            const selected = key === conceptKey;
            row.classList.toggle("is-selected", selected);
            row.setAttribute("aria-selected", selected ? "true" : "false");
        }
    };

    const renderSelection = async conceptKey => {
        const serial = ++detailSerial;
        app.browserSelectedConceptKey = conceptKey;
        workspace.classList.add("has-selection");
        syncSelectedRowState(conceptKey);
        await renderRuleDetailPane(
            app,
            detail,
            conceptKey,
            app.browserScope,
            serial,
            () => detailSerial,
            showIndexOnCompactViewport);

        if (isCompactLibraryViewport() && serial === detailSerial) {
            detail.focus({ preventScroll: true });
            detail.scrollIntoView({ block: "start" });
        }
    };

    const refreshListState = () => {
        indexStatus.textContent = hasPublishedRuleset
            ? `${currentRules.length} / ${totalCount}`
            : "No published rules";
        renderContinuousIndexFooter(
            indexFooter,
            currentRules.length,
            totalCount,
            hasMore,
            isLoadingMore,
            loadMoreError,
            () => void loadMore());
    };

    loadMore = async () => {
        if (!hasMore || isLoadingMore) return [];
        const serial = loadSerial;
        isLoadingMore = true;
        loadMoreError = null;
        refreshListState();

        try {
            const offset = currentRules.length;
            const filters = {
                entityType: app.browserFilters.entityType || null,
                query: app.browserFilters.query || null,
                sourceCode: app.browserFilters.sourceCode || null,
                overridesOnly: app.browserFilters.overridesOnly,
                limit: PAGE_SIZE,
                offset
            };
            const requested = app.browserScope === "global"
                ? await app.api.getGlobalRulesCatalog(filters)
                : await app.api.getCampaignRulesCatalog(
                    app.browserScope.slice("campaign:".length),
                    filters);
            if (serial !== loadSerial) return [];

            const added = requested.rules ?? [];
            if (!added.length) {
                hasMore = false;
                return [];
            }

            currentRules.push(...added);
            totalCount = requested.totalCount ?? totalCount;
            hasMore = currentRules.length < totalCount;
            renderRuleRows(
                list,
                added,
                app.browserFilters.entityType,
                async rule => {
                    preserveDeepLink = false;
                    pushToolRoute(app, rule.browserLink?.toolRelativePath);
                    await renderSelection(rule.conceptKey);
                },
                { append: true });
            for (const row of list.querySelectorAll("[data-concept-key]")) {
                rowByConceptKey.set(row.dataset.conceptKey, row);
            }
            syncSelectedRowState(app.browserSelectedConceptKey);
            return added;
        } catch (error) {
            if (serial === loadSerial) {
                loadMoreError = describeError(error);
            }
            return [];
        } finally {
            if (serial === loadSerial) {
                isLoadingMore = false;
                refreshListState();
            }
        }
    };

    const load = async ({ keepSelection = false } = {}) => {
        const serial = ++loadSerial;
        app.browserPage = 0;
        isLoadingMore = false;
        currentRules = [];
        totalCount = 0;
        hasMore = false;
        hasPublishedRuleset = false;
        loadMoreError = null;

        app.browserScope = scope.value;
        app.browserFilters = {
            entityType: app.browserFilters.entityType ?? "",
            query: search.value.trim(),
            sourceCode: sourceFilter.value,
            overridesOnly: scope.value.startsWith("campaign:")
                && overrideFilter.querySelector("input").checked
        };
        syncFilterControls();

        list.replaceChildren(element("div", {
            className: "rules-core-library-loading",
            text: "Loading rules…"
        }));
        indexFooter.replaceChildren();

        try {
            const filters = {
                entityType: app.browserFilters.entityType || null,
                query: app.browserFilters.query || null,
                sourceCode: app.browserFilters.sourceCode || null,
                overridesOnly: app.browserFilters.overridesOnly,
                limit: PAGE_SIZE,
                offset: 0
            };
            const requested = app.browserScope === "global"
                ? await app.api.getGlobalRulesCatalog(filters)
                : await app.api.getCampaignRulesCatalog(
                    app.browserScope.slice("campaign:".length),
                    filters);
            if (serial !== loadSerial) return;

            populateEntityTypeFacets(requested.entityTypeFacets);
            populateSourceFacets(requested.sourceFacets);
            const rules = requested.rules ?? [];
            currentRules = [...rules];
            totalCount = requested.totalCount ?? rules.length;
            hasPublishedRuleset = Boolean(requested.revisionNumber);
            hasMore = hasPublishedRuleset && currentRules.length < totalCount;

            heading.querySelector(".rules-core-library-revision").textContent = requested.revisionNumber
                ? `${scopeLabel(app, app.browserScope)} · published #${requested.revisionNumber} · ${formatDate(requested.publishedAt)}`
                : `${scopeLabel(app, app.browserScope)} · no published ruleset`;
            indexStatus.textContent = requested.revisionNumber
                ? `${currentRules.length} / ${totalCount}`
                : "No published rules";
            renderIndexHeader(indexHeader, app.browserFilters.entityType);

            renderRuleRows(
                list,
                rules,
                app.browserFilters.entityType,
                async rule => {
                    preserveDeepLink = false;
                    pushToolRoute(app, rule.browserLink?.toolRelativePath);
                    await renderSelection(rule.conceptKey);
                });
            rowByConceptKey = new Map(
                Array.from(list.querySelectorAll("[data-concept-key]"))
                    .map(row => [row.dataset.conceptKey, row]));
            refreshListState();

            if (!requested.revisionNumber) {
                const message = app.browserScope === "global"
                    ? "No global ruleset has been published yet."
                    : "This campaign has no published ruleset yet.";
                list.replaceChildren(renderEmptyIndexState(message));
                detail.replaceChildren(renderEmptyDetail(message));
                return;
            }
            if (!rules.length) {
                const emptyState = await resolvePublishedEmptyState(app);
                if (serial !== loadSerial) return;
                list.replaceChildren(renderEmptyIndexState(
                    emptyState.message,
                    emptyState.showSourceLibrary ? renderSourceLibraryAction(app) : null));
                detail.replaceChildren(renderEmptyDetail(
                    emptyState.message,
                    emptyState.showSourceLibrary ? renderSourceLibraryAction(app) : null,
                    emptyState.note));
                return;
            }

            let conceptKey = keepSelection ? app.browserSelectedConceptKey : null;
            const deepLinkedConceptKey = preserveDeepLink && app.browserDeepLink
                ? app.browserDeepLink
                : null;
            if (deepLinkedConceptKey) {
                conceptKey = deepLinkedConceptKey;
                app.browserDeepLink = null;
            } else if (conceptKey && keepSelection
                && !currentRules.some(rule => rule.conceptKey === conceptKey)) {
                const remainsAvailable = await isRuleAvailableInScope(
                    app,
                    conceptKey,
                    app.browserScope);
                if (serial !== loadSerial) return;
                if (!remainsAvailable) {
                    conceptKey = isCompactLibraryViewport() ? null : currentRules[0].conceptKey;
                    app.browserSelectedConceptKey = conceptKey;
                    pushToolRoute(
                        app,
                        catalogRouteForEntity(app.browserFilters.entityType),
                        app.browserScope);
                }
            } else if (!conceptKey) {
                conceptKey = isCompactLibraryViewport() ? null : currentRules[0].conceptKey;
            }

            if (conceptKey) {
                await renderSelection(conceptKey);
            } else {
                workspace.classList.remove("has-selection");
                detail.replaceChildren(renderEmptyDetail("Select a rule from the list."));
            }
        } catch (error) {
            if (serial !== loadSerial) return;
            list.replaceChildren(alertNode("danger", describeError(error)));
            indexFooter.replaceChildren();
            detail.replaceChildren(renderEmptyDetail("The rule list could not be loaded."));
        }
    };

    list.addEventListener("scroll", () => {
        if (!hasMore || isLoadingMore) return;
        const remaining = list.scrollHeight - list.scrollTop - list.clientHeight;
        if (remaining <= 180) void loadMore();
    });

    scope.addEventListener("change", async () => {
        if (!scope.value.startsWith("campaign:")) {
            overrideFilter.querySelector("input").checked = false;
        }
        app.browserScope = scope.value;
        pushToolRoute(app, currentToolRoute(app), app.browserScope);
        syncFilterControls();
        await load({ keepSelection: true });
    });
    const changeEntityType = async value => {
        if (app.browserFilters.entityType === value) return;
        preserveDeepLink = false;
        app.browserSelectedConceptKey = null;
        await app.navigateRuleFamily(value);
    };

    moreTypes.addEventListener("change", () => {
        if (!moreTypes.value) return;
        void changeEntityType(moreTypes.value);
    });
    reset.addEventListener("click", async () => {
        preserveDeepLink = false;
        if (searchTimer) clearTimeout(searchTimer);
        search.value = "";
        moreTypes.value = "";
        sourceFilter.value = "";
        overrideFilter.querySelector("input").checked = false;
        filterBar.hidden = true;
        filterToggle.setAttribute("aria-expanded", "false");
        syncFilterControls();
        app.browserSelectedConceptKey = null;
        app.browserFilters.query = "";
        app.browserFilters.sourceCode = "";
        app.browserFilters.overridesOnly = false;
        await app.navigateRuleFamily("");
    });
    filterToggle.addEventListener("click", () => {
        filterBar.hidden = !filterBar.hidden;
        filterToggle.setAttribute("aria-expanded", filterBar.hidden ? "false" : "true");
    });
    sourceFilter.addEventListener("change", async () => {
        syncFilterControls();
        app.browserSelectedConceptKey = null;
        await load();
    });
    overrideFilter.querySelector("input").addEventListener("change", async () => {
        syncFilterControls();
        app.browserSelectedConceptKey = null;
        await load();
    });
    clearFilters.addEventListener("click", async () => {
        sourceFilter.value = "";
        overrideFilter.querySelector("input").checked = false;
        syncFilterControls();
        app.browserSelectedConceptKey = null;
        await load();
    });

    search.addEventListener("input", () => {
        preserveDeepLink = false;
        if (searchTimer) clearTimeout(searchTimer);
        searchTimer = setTimeout(async () => {
            app.browserSelectedConceptKey = null;
            await load();
        }, 220);
    });
    search.addEventListener("keydown", async event => {
        if (event.key !== "Enter") return;
        event.preventDefault();
        if (searchTimer) clearTimeout(searchTimer);
        preserveDeepLink = false;
        app.browserSelectedConceptKey = null;
        await load();
    });

    await load({ keepSelection: true });
}

function isCompactLibraryViewport() {
    return window.matchMedia?.("(max-width: 900px)")?.matches ?? false;
}

function isEditableTarget(target) {
    if (!(target instanceof Element)) return false;
    return Boolean(target.closest("input, textarea, select, button, [contenteditable='true']"));
}

async function resolvePublishedEmptyState(app) {
    const entityType = app.browserFilters.entityType ?? "";
    const family = entityType ? pluralizeEntityType(entityType).toLowerCase() : "rules";
    const hasFilters = Boolean(
        app.browserFilters.query
        || app.browserFilters.sourceCode
        || app.browserFilters.overridesOnly);

    if (hasFilters) {
        return {
            message: `No published ${family} match the current filters.`,
            note: "Clear or change the filters to inspect other published rules in this scope.",
            showSourceLibrary: false
        };
    }

    let sourceAvailability = null;
    try {
        const sourceRecords = await app.api.searchSourceEntityPage({
            entityType: entityType || null,
            limit: 1,
            offset: 0
        });
        sourceAvailability = Array.isArray(sourceRecords) && sourceRecords.length > 0;
    } catch {
        sourceAvailability = null;
    }

    const message = `No ${family} are published in this ruleset for the current account.`;
    if (sourceAvailability === true) {
        return {
            message,
            note: `Accessible ${family} source records exist in the Source Library, but imported/source material is not published until a Rules Layer decision is included in a published ruleset.`,
            showSourceLibrary: true
        };
    }
    if (sourceAvailability === false) {
        return {
            message,
            note: `The Source Library returned no accessible ${entityType ? family : "source"} records. Published catalogs are filtered by source access, so inaccessible material is not counted here.`,
            showSourceLibrary: Boolean(app.canBrowseSourceLibrary)
        };
    }
    return {
        message,
        note: "Source Layer availability could not be checked. Imported/source material remains separate from published content.",
        showSourceLibrary: Boolean(app.canBrowseSourceLibrary)
    };
}

function renderEmptyIndexState(message, action = null) {
    const node = element("div", { className: "rules-core-library-empty-list" },
        element("p", { className: "mb-2", text: message }));
    if (action) node.append(action);
    return node;
}

function renderEmptyDetail(message, action = null, note = null) {
    const node = element("div", { className: "rules-core-library-detail-empty" },
        element("div", { className: "rules-core-library-detail-empty-mark", text: "R" }),
        element("p", { className: "mb-0", text: message }));
    if (note) node.append(element("p", {
        className: "small text-body-secondary mb-0 rules-core-library-empty-note",
        text: note
    }));
    if (action) node.append(action);
    return node;
}

function renderSourceLibraryAction(app) {
    if (!app.canBrowseSourceLibrary || !app.viewNavigation?.sources) return null;
    return element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-primary",
        text: "Open Source Library",
        onClick: async () => app.viewNavigation.sources()
    });
}

async function isRuleAvailableInScope(app, conceptKey, scopeValue) {
    try {
        if (scopeValue.startsWith("campaign:")) {
            await app.api.getCampaignResolvedRule(
                scopeValue.slice("campaign:".length),
                conceptKey);
        } else {
            await app.api.getGlobalResolvedRule(conceptKey);
        }
        return true;
    } catch (error) {
        if (error?.status === 404) return false;
        throw error;
    }
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

function pluralizeEntityType(entityType) {
    const known = ENTITY_TYPES.find(([value]) => value === entityType)?.[1];
    if (known) return known;

    const label = humanizeEntityType(entityType);
    if (/s$/i.test(label)) return label;
    if (/y$/i.test(label)) return `${label.slice(0, -1)}ies`;
    return `${label}s`;
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
    if (segments.length === 2 && segments[0] === "types") {
        return { entityType: decodeURIComponent(segments[1]) };
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

function parseBrowserScopeFromLocation(app) {
    const requested = new URLSearchParams(window.location.search).get("scope");
    if (!requested || requested === "global") return "global";
    if (!requested.startsWith("campaign:")) return null;

    const campaignId = requested.slice("campaign:".length);
    return app.campaigns.some(value => String(value.id) === campaignId)
        ? requested
        : null;
}

function browserHref(app, toolRelativePath, scopeValue = app.browserScope) {
    const base = app.hostContext.toolBasePath ?? "/tools/rules-core";
    const path = `${base.replace(/\/$/, "")}${toolRelativePath || "/"}`;
    const parameters = new URLSearchParams(window.location.search);
    parameters.delete("scope");
    if (scopeValue?.startsWith("campaign:")) {
        parameters.set("scope", scopeValue);
    }
    const query = parameters.toString();
    return query ? `${path}?${query}` : path;
}

function pushToolRoute(app, toolRelativePath, scopeValue = app.browserScope) {
    const href = browserHref(app, toolRelativePath || "/", scopeValue);
    const current = `${window.location.pathname}${window.location.search}`;
    if (current !== href) window.history.pushState({}, "", href);
}

function catalogRouteForEntity(entityType) {
    if (!entityType) return "/";
    for (const [segment, mappedType] of ROUTE_FAMILIES.entries()) {
        if (mappedType === entityType) return `/${segment}`;
    }
    return `/types/${encodeURIComponent(entityType)}`;
}