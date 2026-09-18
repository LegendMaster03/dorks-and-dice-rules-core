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
    const familyNav = element("div", {
        className: "rules-core-library-family-nav",
        role: "navigation",
        ariaLabel: "Rule families"
    });
    const familyButtons = new Map();
    for (const [value, label] of ENTITY_TYPES) {
        const button = element("button", {
            type: "button",
            className: "rules-core-library-family",
            text: label,
            dataset: { entityType: value },
            attributes: {
                "aria-current": value === app.browserFilters.entityType ? "page" : "false"
            }
        });
        familyButtons.set(value, button);
        familyNav.append(button);
    }

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

    const syncFamilyNav = () => {
        for (const [value, button] of familyButtons) {
            const selected = value === type.value;
            button.classList.toggle("is-active", selected);
            button.setAttribute("aria-current", selected ? "page" : "false");
        }
    };
    const populateEntityTypeFacets = facets => {
        const counts = new Map(
            (facets ?? []).map(facet => [facet.entityType, facet.count]));

        for (const facet of facets ?? []) {
            if (familyButtons.has(facet.entityType)) continue;
            const label = pluralizeEntityType(facet.entityType);
            const button = element("button", {
                type: "button",
                className: "rules-core-library-family",
                text: label,
                dataset: { entityType: facet.entityType },
                title: `${facet.count} published rules`
            });
            familyButtons.set(facet.entityType, button);
            familyNav.append(button);
            type.append(element("option", {
                value: facet.entityType,
                text: label
            }));
        }

        for (const [value, button] of familyButtons) {
            if (!value) {
                button.hidden = false;
                continue;
            }
            button.hidden = !counts.has(value) && type.value !== value;
            if (counts.has(value)) {
                button.title = `${counts.get(value)} published rules`;
            }
        }
        syncFamilyNav();
    };
    syncFamilyNav();

    controls.append(familyNav, scope, type);

    const search = element("input", {
        className: "form-control form-control-sm rules-core-library-search",
        type: "search",
        value: app.browserFilters.query,
        placeholder: "Search rules…",
        ariaLabel: "Search rules"
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

    const renderSelection = async conceptKey => {
        const serial = ++detailSerial;
        app.browserSelectedConceptKey = conceptKey;
        workspace.classList.add("has-selection");
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
            entityType: type.value,
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

            headingTitle.textContent = libraryTitle(app.browserFilters.entityType);
            headingSubtitle.textContent = librarySubtitle(app.browserFilters.entityType);
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
            const deepLinkedConceptKey = preserveDeepLink && app.browserDeepLink
                ? app.browserDeepLink
                : null;
            if (deepLinkedConceptKey) {
                conceptKey = deepLinkedConceptKey;
                app.browserDeepLink = null;
            } else if (!conceptKey || !currentRules.some(rule => rule.conceptKey === conceptKey)) {
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
        if (type.value === value && app.browserFilters.entityType === value) return;
        preserveDeepLink = false;
        type.value = value;
        syncFamilyNav();
        app.browserSelectedConceptKey = null;
        pushToolRoute(app, catalogRouteForEntity(type.value));
        await load();
    };

    familyNav.addEventListener("click", event => {
        const button = event.target.closest?.("[data-entity-type]");
        if (!button || !familyNav.contains(button)) return;
        void changeEntityType(button.dataset.entityType ?? "");
    });
    type.addEventListener("change", () => void changeEntityType(type.value));
    reset.addEventListener("click", async () => {
        preserveDeepLink = false;
        if (searchTimer) clearTimeout(searchTimer);
        search.value = "";
        type.value = "";
        sourceFilter.value = "";
        overrideFilter.querySelector("input").checked = false;
        filterBar.hidden = true;
        filterToggle.setAttribute("aria-expanded", "false");
        syncFamilyNav();
        syncFilterControls();
        app.browserSelectedConceptKey = null;
        pushToolRoute(app, "/");
        await load();
        search.focus();
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

function renderRuleRows(container, rules, entityType, onSelect, { append = false } = {}) {
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

function renderContinuousIndexFooter(
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

async function renderRuleDetailPane(
    app,
    container,
    conceptKey,
    scopeValue,
    requestSerial,
    getCurrentSerial,
    onBackToList)
{
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
        const backToList = element("button", {
            type: "button",
            className: "btn btn-sm btn-outline-secondary rules-core-library-mobile-back",
            text: "← Back to list",
            onClick: onBackToList
        });
        tabBar.append(backToList);
        const body = element("div", { className: "rules-core-library-detail-body" });
        const effectiveLabel = campaignId
            ? campaignName(app, campaignId)
            : "Dorks & Dice";

        const tabs = [{
            key: "effective",
            label: effectiveLabel,
            title: campaignId ? "Effective campaign rule" : "Dorks & Dice combined rule",
            render: () => renderEffectiveRule(app, body, resolved, baseline, campaignId)
        }];

        for (const version of versions?.versions ?? []) {
            tabs.push({
                key: `source:${version.canonicalEntityId}`,
                label: sourceVersionTabLabel(version, versions?.versions ?? []),
                title: sourceVersionLabel(version),
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

function renderEffectiveRule(app, container, resolved, baseline, campaignId) {
    clear(container);
    const campaignScope = Boolean(campaignId);
    const isMonster = String(resolved.entityType ?? "").toLowerCase() === "monster";

    container.append(renderRulingStatusBar(app, resolved, campaignId));

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
                    version.gameEdition,
                    version.sourceCode,
                    version.packageDisplayName,
                    version.publicationDate,
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
                ["Edition", version.gameEdition],
                ["Release", version.releaseKind],
                ["Publication date", version.publicationDate],
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

function sourceVersionTabLabel(version, allVersions) {
    const edition = String(version.gameEdition ?? "").trim();
    if (!edition) {
        return version.sourceCode || version.formatKey || version.packageDisplayName;
    }

    const sameEditionCount = allVersions.filter(candidate =>
        String(candidate.gameEdition ?? "").trim().toLowerCase() === edition.toLowerCase()).length;
    return sameEditionCount > 1 && version.sourceCode
        ? `${edition} · ${version.sourceCode}`
        : edition;
}

function sourceVersionLabel(version) {
    return [
        version.gameEdition,
        version.sourceCode || version.formatKey,
        version.packageDisplayName,
        version.publicationDate,
        `rev. ${version.sourceRevisionNumber}`
    ].filter(Boolean).join(" · ");
}

function renderRulingStatusBar(app, resolved, campaignId) {
    const campaignScope = Boolean(campaignId);
    const hasCampaignOverride = campaignScope && Boolean(resolved.campaignDecisionNumber);
    const title = campaignScope
        ? hasCampaignOverride
            ? "Campaign override"
            : "Inherited from Dorks & Dice"
        : "Dorks & Dice ruling";
    const detail = campaignScope
        ? hasCampaignOverride
            ? `Decision #${resolved.campaignDecisionNumber} · ${resolved.effectiveDecisionKind}`
            : `Global decision #${resolved.globalDecisionNumber} · ${resolved.globalDecisionKind}`
        : `Decision #${resolved.globalDecisionNumber} · ${resolved.decisionKind}`;

    return element("div", { className: "rules-core-ruling-status" },
        element("div", {},
            element("div", { className: "rules-core-ruling-status-title", text: title }),
            element("div", { className: "rules-core-ruling-status-detail", text: detail })),
        adjudicationButton(app, resolved.ruleConceptId, campaignId));
}

function adjudicationButton(app, ruleConceptId, campaignId) {
    const campaignCanEdit = campaignId
        && app.dmCampaigns?.some(value => String(value.id) === String(campaignId));
    if (!campaignId && !app.canEditGlobal) return null;
    if (campaignId && !campaignCanEdit) return null;

    return element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-primary",
        text: campaignId ? "Edit campaign rule" : "Edit Dorks & Dice rule",
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
