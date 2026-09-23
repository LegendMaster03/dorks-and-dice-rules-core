import {
    alertNode,
    badge,
    clear,
    codeBlock,
    definitionList,
    describeError,
    element,
    field as compactField,
    formatDate,
    sectionHeading,
    workspaceSection,
} from "./ui.js";
import { renderResolvedRule } from "./rule-renderers.js";

const DORKS_MODE = "dorks-and-dice";
const BUNDLED_SRDS = [
    {
        includedSourceCodes: ["SRD3"],
        packageKey: "wotc-srd-ogl",
        packageDisplayName: "Wizards of the Coast SRD (OGL)",
        workKey: "srd-3e",
        workDisplayName: "System Reference Document 3e",
        editionKey: "original",
        editionDisplayName: "3e SRD",
        gameEdition: "3e",
        provider: "Wizards of the Coast",
        license: "OGL-1.0a",
        note: "Reviewed 3e SRD snapshot bundled with Rules Core. The archived SRD distribution remains the corpus-membership authority."
    },
    {
        includedSourceCodes: ["SRD35"],
        packageKey: "wotc-srd-ogl",
        packageDisplayName: "Wizards of the Coast SRD (OGL)",
        workKey: "srd-3-5e",
        workDisplayName: "System Reference Document 3.5e",
        editionKey: "original",
        editionDisplayName: "3.5e SRD",
        gameEdition: "3.5e",
        provider: "Wizards of the Coast",
        license: "OGL-1.0a",
        note: "Reviewed 3.5e SRD snapshot bundled with Rules Core from the pinned representation."
    },
    {
        includedSourceCodes: ["SRD51"],
        packageKey: "wotc-srd-cc",
        packageDisplayName: "Wizards of the Coast SRD (Creative Commons)",
        workKey: "srd-5-1",
        workDisplayName: "System Reference Document 5.1",
        editionKey: "5.1",
        editionDisplayName: "SRD 5.1",
        gameEdition: "5e",
        provider: "Wizards of the Coast",
        license: "CC-BY-4.0",
        note: "Reviewed SRD 5.1 snapshot bundled with Rules Core."
    },
    {
        includedSourceCodes: ["SRD52"],
        packageKey: "wotc-srd-cc",
        packageDisplayName: "Wizards of the Coast SRD (Creative Commons)",
        workKey: "srd-5-2-1",
        workDisplayName: "System Reference Document 5.2.1",
        editionKey: "5.2.1",
        editionDisplayName: "SRD 5.2.1",
        gameEdition: "5.5e",
        provider: "Wizards of the Coast",
        license: "CC-BY-4.0",
        note: "Reviewed SRD 5.2.1 snapshot bundled with Rules Core."
    }
];
const SOURCE_LIMIT = 200;
const SOURCE_PAGE_SIZE = 100;
const ENTITY_TYPES = [
    ["", "All types"],
    ["monster", "Monsters"],
    ["spell", "Spells"],
    ["class", "Classes"],
    ["prestigeClass", "Prestige classes"],
    ["npcClass", "NPC classes"],
    ["race", "Races / species"],
    ["skill", "Skills"],
    ["feat", "Feats"],
    ["item", "Items"],
    ["power", "Psionic powers"],
    ["domain", "Domains"],
    ["divineAbility", "Divine abilities"],
    ["houseRule", "House rules"],
    ["rule", "Other rules"]
];

export function installSourceLibrary(app) {
    app.canBrowseSourceLibrary = app.hostContext.siteMode === DORKS_MODE;
    app.libraryFilters = { entityType: "", query: "", page: 0 };
    app.libraryNotice = null;

    const initialSourceRoute = parseSourceEntityRoute(app.hostContext.toolRoute);
    app.libraryDeepLink = initialSourceRoute.entityId ?? null;
    app.libraryRouteActive = Boolean(app.libraryDeepLink)
        || isSourceLibraryRootRoute(app.hostContext.toolRoute);

    if (app.canBrowseSourceLibrary && app.libraryRouteActive) {
        app.activeView = "sources";
    }

    app.viewNavigation ??= {};
    app.viewNavigation.sources = async () => {
        app.libraryDeepLink = null;
        app.libraryRouteActive = true;
        app.activeView = "sources";
        pushSourceToolRoute(app, "/sources");
        await app.render();
    };

    app.renderHeader = () => renderHeader(app);
    app.renderNavigation = () => renderNavigation(app);

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        if (app.activeView === "sources") {
            if (app.libraryDeepLink) {
                await renderSourceEntityRoute(app, container, app.libraryDeepLink);
                return;
            }
            await renderSourceLibrary(app, container);
            return;
        }

        await renderActiveView(container);
        if (app.activeView === "global" && app.canEditGlobal) {
            prependRulesLawyerWorkflow(app, container);
        }
    };

    window.addEventListener("popstate", async event => {
        if (!app.canBrowseSourceLibrary) return;
        const toolRoute = currentToolRoute(app);
        const next = parseSourceEntityRoute(toolRoute);
        if (next.entityId) {
            event.stopImmediatePropagation();
            app.libraryDeepLink = next.entityId;
            app.libraryRouteActive = true;
            app.activeView = "sources";
            await app.render();
            return;
        }

        if (isSourceLibraryRootRoute(toolRoute)) {
            event.stopImmediatePropagation();
            app.libraryDeepLink = null;
            app.libraryRouteActive = true;
            app.activeView = "sources";
            await app.render();
            return;
        }

        if (app.libraryRouteActive) {
            app.libraryDeepLink = null;
            app.libraryRouteActive = false;
        }
    }, { capture: true });
}

function renderHeader(app) {
    const card = element("div", { className: "card card-body rules-core-header" });
    card.append(element("div", {
        className: "d-flex flex-wrap align-items-start justify-content-between gap-3"
    },
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: "DORKS & DICE RULES" }),
            element("h2", { className: "h3 mb-1", text: "Rules Core" }),
            element("p", {
                className: "text-body-secondary mb-0",
                text: "Explore source material, review cross-edition rules, and publish the rules your table actually uses."
            })),
        element("div", { className: "text-end small" },
            element("div", { className: "fw-semibold", text: app.session.user?.displayName ?? "Guest" }),
            element("div", {
                className: "text-body-secondary",
                text: app.session.user?.displayName ? "Signed in" : "Public library access"
            }))));
    return card;
}

function renderNavigation(app) {
    const shell = element("div", { className: "rules-core-nav-shell" });
    const primary = element("div", { className: "rules-core-nav-primary" });
    if (app.canBrowseRules) primary.append(sourceAwareNavButton(app, "Library", "library"));
    if (app.canBrowseSourceLibrary) primary.append(sourceAwareNavButton(app, "Sources", "sources"));
    if (app.canEditGlobal) primary.append(sourceAwareNavButton(app, "Rules Lawyer", "global"));
    if (app.canReviewVersions) primary.append(sourceAwareNavButton(app, "Cross-version Review", "version-review"));
    if (app.canEditCampaign) primary.append(sourceAwareNavButton(app, "Campaign Rules", "campaign"));
    shell.append(primary);

    if (app.canManageHostedSources || app.canAdministerSources) {
        const advanced = element("details", { className: "rules-core-advanced-nav" });
        const actions = element("div", { className: "rules-core-advanced-actions" });
        if (app.canManageHostedSources) actions.append(advancedNavButton(app, "Hosted source definitions", "hosted-sources"));
        if (app.canAdministerSources) actions.append(advancedNavButton(app, "Manual source import & access", "source-admin"));
        advanced.append(element("summary", { text: "Advanced" }), actions);
        shell.append(advanced);
    }
    return shell;
}

function sourceAwareNavButton(app, label, view) {
    const button = app.navButton(label, view);
    button.addEventListener("click", () => {
        if (view === "sources") {
            app.libraryDeepLink = null;
            app.libraryRouteActive = true;
            pushSourceToolRoute(app, "/sources");
        } else if (app.libraryRouteActive) {
            app.libraryDeepLink = null;
            app.libraryRouteActive = false;
            pushSourceToolRoute(app, "/");
        }
    }, { capture: true });
    return button;
}

function advancedNavButton(app, label, view) {
    const button = sourceAwareNavButton(app, label, view);
    button.classList.add("w-100", "text-start");
    return button;
}

async function renderSourceLibrary(app, container) {
    clear(container);
    if (app.libraryNotice) {
        container.append(alertNode(app.libraryNotice.kind, app.libraryNotice.message));
        app.libraryNotice = null;
    }

    const bundledLoad = renderBuiltInSources(app, container, BUNDLED_SRDS);
    await renderSourceBrowser(app, container);
    await bundledLoad;
}

async function renderBuiltInSources(app, container, definitions) {
    const section = workspaceSection({ className: "rules-core-bundled-sources" });
    section.append(sectionHeading({
        level: 4,
        title: "Bundled SRDs",
        description: "These reviewed snapshots ship with Rules Core and are hydrated into the public immutable Source Layer during baseline bootstrap. No account, import action, or remote host is required to use them."
    }));

    const stateHolder = element("div", { className: "rules-core-source-grid" });
    section.append(stateHolder);
    container.append(section);

    const states = await Promise.all(definitions.map(definition => loadDefinitionState(app, definition)));
    const readyCount = states.filter(value => value.ready).length;
    const detectedMonsterCount = states.reduce((sum, value) => sum + value.monsterCount, 0);
    const monsterCountCapped = states.some(value => value.monsterCapped);
    section.insertBefore(element("div", { className: "rules-core-library-summary" },
        element("div", { className: "rules-core-metrics" },
            metric("Bundled SRDs", String(definitions.length)),
            metric("Available locally", `${readyCount}/${definitions.length}`),
            metric("Detected monsters", `${detectedMonsterCount}${monsterCountCapped ? "+" : ""}`)),
        element("span", {
            className: "small text-body-secondary",
            text: "Upstream comparison and maintenance controls remain under Advanced; normal library use is entirely local."
        })), stateHolder);

    for (const state of states) stateHolder.append(renderSourceCard(app, container, state));
}

async function loadDefinitionState(app, definition) {
    const sourceCode = definition.includedSourceCodes?.[0] ?? definition.workDisplayName;
    try {
        const [entities, monsters] = await Promise.all([
            app.api.searchSourceEntities({ query: sourceCode, limit: SOURCE_LIMIT }),
            app.api.searchSourceEntities({ entityType: "monster", query: sourceCode, limit: SOURCE_LIMIT })
        ]);
        const exactEntities = entities.filter(value => matchesDefinition(value, definition));
        const exactMonsters = monsters.filter(value => matchesDefinition(value, definition));
        const latestImportedAt = exactEntities.map(value => value.latestImportedAt).filter(Boolean).sort().at(-1) ?? null;
        return {
            definition,
            sourceCode,
            entityCount: exactEntities.length,
            entityCapped: exactEntities.length === SOURCE_LIMIT,
            monsterCount: exactMonsters.length,
            monsterCapped: exactMonsters.length === SOURCE_LIMIT,
            latestImportedAt,
            ready: exactEntities.length > 0,
            error: null
        };
    } catch (error) {
        return { definition, sourceCode, entityCount: 0, entityCapped: false, monsterCount: 0, monsterCapped: false, latestImportedAt: null, ready: false, error };
    }
}

function renderSourceCard(app, container, state) {
    const definition = state.definition;
    const card = element("article", { className: "card card-body rules-core-source-card" });
    card.append(element("div", { className: "d-flex justify-content-between align-items-start gap-2 mb-3" },
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: definition.gameEdition ?? "D&D" }),
            element("h5", { className: "h5 mb-1", text: definition.editionDisplayName }),
            element("div", { className: "small text-body-secondary", text: definition.workDisplayName })),
        state.error ? badge("Status error", "danger") : state.ready ? badge("Bundled", "success") : badge("Bundle unavailable", "danger")));
    card.append(element("div", { className: "rules-core-source-card-stats" },
        metric("Entities", countLabel(state.entityCount, state.entityCapped)),
        metric("Monsters", countLabel(state.monsterCount, state.monsterCapped)),
        metric("Loaded", state.latestImportedAt ? formatDate(state.latestImportedAt) : "Unavailable")));

    if (state.error) card.append(alertNode("warning", describeError(state.error)));
    else if (!state.ready) card.append(alertNode("danger", "This bundled SRD is missing from the local Source Layer. Baseline bootstrap should hydrate it automatically; there is no user import action."));

    const browse = element("button", { type: "button", className: "btn btn-sm btn-outline-primary", text: "Browse source", disabled: !state.ready });
    browse.addEventListener("click", async () => {
        app.libraryFilters = { entityType: "", query: state.sourceCode, page: 0 };
        await renderSourceLibrary(app, container);
        document.getElementById("rules-core-source-browser")?.scrollIntoView({ behavior: "smooth", block: "start" });
    });
    const monsters = element("button", { type: "button", className: "btn btn-sm btn-outline-primary", text: "Monsters", disabled: !state.monsterCount });
    monsters.addEventListener("click", async () => {
        app.libraryFilters = { entityType: "monster", query: state.sourceCode, page: 0 };
        await renderSourceLibrary(app, container);
        document.getElementById("rules-core-source-browser")?.scrollIntoView({ behavior: "smooth", block: "start" });
    });
    card.append(element("div", { className: "d-flex flex-wrap gap-2 mt-3" }, browse, monsters));

    const details = element("details", { className: "mt-3 small" });
    details.append(element("summary", { className: "text-body-secondary", text: "Source and provenance details" }),
        element("div", { className: "pt-2" },
            definitionList([
                ["Source code", state.sourceCode],
                ["Package", definition.packageDisplayName],
                ["Provider", definition.provider],
                ["License", definition.license ?? "—"],
                ["Availability", "Bundled with Rules Core"]
            ]),
            element("p", { className: "text-body-secondary mb-0", text: definition.note ?? "" })));
    card.append(details);
    return card;
}

async function renderSourceBrowser(app, container) {
    const section = workspaceSection({ id: "rules-core-source-browser", className: "rules-core-source-browser" });
    section.append(sectionHeading({
        level: 4,
        title: "Browse source material",
        description: "This is the immutable Source Layer, not the published ruleset. Source-native records remain available alongside their Rules Core mechanical presentation.",
        actions: [badge("Source Layer", "secondary")]
    }));

    const form = element("form", { className: "rules-core-filter-bar" });
    const type = element("select", { className: "form-select form-select-sm" });
    for (const [value, label] of ENTITY_TYPES) {
        const option = element("option", { value, text: label });
        option.selected = value === app.libraryFilters.entityType;
        type.append(option);
    }
    const query = element("input", { type: "search", className: "form-control form-control-sm", placeholder: "Name, source code, or package", value: app.libraryFilters.query });
    const searchButton = element("button", { type: "submit", className: "btn btn-sm btn-primary", text: "Search" });
    const monstersButton = element("button", { type: "button", className: "btn btn-sm btn-outline-primary", text: "Monsters" });
    form.append(
        compactField("Entity type", type),
        compactField("Search", query),
        element("div", { className: "rules-core-action-bar" }, searchButton, monstersButton));
    section.append(form);

    const results = element("div");
    section.append(results);
    container.append(section);
    form.addEventListener("submit", async event => {
        event.preventDefault();
        app.libraryFilters = { entityType: type.value, query: query.value.trim(), page: 0 };
        await renderBrowserResults(app, results);
    });
    monstersButton.addEventListener("click", async () => {
        type.value = "monster";
        app.libraryFilters = { entityType: "monster", query: query.value.trim(), page: 0 };
        await renderBrowserResults(app, results);
    });
    await renderBrowserResults(app, results);
}

async function renderBrowserResults(app, container) {
    clear(container);
    container.append(element("div", { className: "text-body-secondary", text: "Loading source entities…" }));
    try {
        const page = Math.max(0, app.libraryFilters.page ?? 0);
        const offset = page * SOURCE_PAGE_SIZE;
        const requested = await app.api.searchSourceEntityPage({
            entityType: app.libraryFilters.entityType || null,
            query: app.libraryFilters.query || null,
            limit: SOURCE_PAGE_SIZE + 1,
            offset
        });
        const hasNext = requested.length > SOURCE_PAGE_SIZE;
        const entities = requested.slice(0, SOURCE_PAGE_SIZE);
        if (!entities.length && page > 0) {
            app.libraryFilters.page = page - 1;
            await renderBrowserResults(app, container);
            return;
        }

        clear(container);
        if (!entities.length) {
            container.append(alertNode("secondary", app.libraryFilters.entityType === "monster" ? "No monster entities match these filters." : "No source entities match these filters."));
            presentFragment(app, container);
            return;
        }

        const table = element("table", { className: "table table-hover align-middle mb-0" });
        const head = element("thead", {}, element("tr", {},
            element("th", { text: "Source entity" }),
            element("th", { text: "Type" }),
            element("th", { text: "Source" }),
            element("th", { text: "Revision" }),
            element("th")));
        const body = element("tbody");
        for (const entity of entities) {
            const href = sourceEntityHref(app, entity.entityId);
            const openEntity = async event => {
                event.preventDefault();
                app.libraryDeepLink = entity.entityId;
                app.libraryRouteActive = true;
                app.activeView = "sources";
                pushSourceToolRoute(app, sourceEntityRoute(entity.entityId));
                await app.render();
            };
            const nameLink = element("a", {
                className: "fw-semibold text-decoration-none",
                text: entity.name,
                attributes: { href },
                onClick: openEntity
            });
            const open = element("a", {
                className: "btn btn-sm btn-outline-primary",
                text: entity.entityType === "monster" ? "Open stat block" : "Open",
                attributes: { href },
                onClick: openEntity
            });
            body.append(element("tr", {},
                element("td", {}, nameLink, element("div", { className: "small text-body-secondary", text: entity.sourceCode })),
                element("td", {}, entity.entityType === "monster" ? badge("monster", "primary") : badge(entity.entityType, "secondary")),
                element("td", {}, element("div", { text: entity.packageDisplayName }), element("div", { className: "small text-body-secondary", text: entity.sourceCode })),
                element("td", { text: `#${entity.latestRevisionNumber}` }),
                element("td", { className: "text-end" }, open)));
        }
        table.append(head, body);

        const previous = element("button", { type: "button", className: "btn btn-sm btn-outline-secondary", text: "← Previous", disabled: page === 0 });
        previous.addEventListener("click", async () => {
            app.libraryFilters.page = Math.max(0, page - 1);
            await renderBrowserResults(app, container);
            document.getElementById("rules-core-source-browser")?.scrollIntoView({ behavior: "smooth", block: "start" });
        });
        const next = element("button", { type: "button", className: "btn btn-sm btn-outline-secondary", text: "Next →", disabled: !hasNext });
        next.addEventListener("click", async () => {
            app.libraryFilters.page = page + 1;
            await renderBrowserResults(app, container);
            document.getElementById("rules-core-source-browser")?.scrollIntoView({ behavior: "smooth", block: "start" });
        });
        const firstResult = offset + 1;
        const lastResult = offset + entities.length;
        container.append(
            element("div", { className: "small text-body-secondary mb-2", text: `Showing ${firstResult}–${lastResult}${hasNext ? "+" : ""} matching result(s)` }),
            element("div", { className: "table-responsive" }, table),
            element("div", { className: "d-flex flex-wrap justify-content-between align-items-center gap-2 mt-3" },
                element("div", { className: "small text-body-secondary", text: `Showing ${firstResult}–${lastResult} · Page ${page + 1}` }),
                element("div", { className: "d-flex gap-2" }, previous, next)));
        presentFragment(app, container);
    } catch (error) {
        clear(container);
        container.append(alertNode("danger", describeError(error)));
        presentFragment(app, container);
    }
}

async function renderSourceEntityRoute(app, container, entityId) {
    clear(container);
    const back = element("a", {
        className: "btn btn-sm btn-outline-secondary mb-3",
        text: "← Back to sources",
        attributes: { href: sourceLibraryHref(app) },
        onClick: async event => {
            event.preventDefault();
            app.libraryDeepLink = null;
            app.libraryRouteActive = true;
            app.activeView = "sources";
            pushSourceToolRoute(app, "/sources");
            await app.render();
        }
    });
    const detail = element("div", { className: "rules-core-source-route-detail" });
    container.append(back, detail);
    await renderSourceEntityDetail(app, detail, entityId);
}

async function renderSourceEntityDetail(app, container, entityId) {
    clear(container);
    container.append(element("div", { className: "text-body-secondary", text: "Loading source entity…" }));
    try {
        const encodedEntityId = encodeURIComponent(entityId);
        const [entity, nativeDocument] = await Promise.all([
            app.api.backend(`/api/sources/entities/${encodedEntityId}`),
            app.api.backend(`/api/sources/entities/${encodedEntityId}/native`)
        ]);
        clear(container);

        const readable = workspaceSection({
            className: "rules-core-source-readable"
        });
        readable.append(
            element("div", { className: "rules-core-eyebrow", text: "Readable content" }),
            element("p", {
                className: "small text-body-secondary mb-3",
                text: "This presentation uses the Rules Core mechanical translation for readability. It does not replace or rewrite the source-native record."
            }),
            renderResolvedRule(entity.entityType, entity.document, {
                displayName: entity.name,
                showDocument: false
            }));
        container.append(readable);

        const readableNative = workspaceSection({
            tagName: "details",
            className: "rules-core-source-native-readable",
            attributes: { open: "" }
        });
        readableNative.append(
            element("summary", { className: "fw-semibold", text: "Readable source-native content" }),
            element("p", {
                className: "small text-body-secondary mt-3 mb-2",
                text: "Fields below are a human-readable projection of the immutable source-native record. Values are not normalized or promoted into published rules by this view."
            }),
            renderReadableSourceValue(nativeDocument));
        container.append(readableNative);

        const context = element("details", { className: "rules-core-context-disclosure" });
        const contextBody = element("div", { className: "rules-core-context-disclosure-body" });
        contextBody.append(definitionList([
            ["Entity ID", entity.entityId],
            ["Package", `${entity.packageDisplayName} (${entity.packageKey})`],
            ["Source code", entity.sourceCode],
            ["Format", entity.editionDisplayName],
            ["Revision", `#${entity.revisionNumber}`],
            ["Imported", formatDate(entity.importedAt)]
        ]));
        if (entity.entityType === "monster") contextBody.append(renderIntegrationPayloadButton(entity));
        context.append(element("summary", { text: "Source provenance and integration details" }), contextBody);
        container.append(context);

        const native = workspaceSection({ tagName: "details", className: "rules-core-source-raw" });
        native.append(
            element("summary", { className: "fw-semibold", text: "Raw source-native data (advanced)" }),
            element("p", {
                className: "small text-body-secondary mt-3 mb-2",
                text: "Exact immutable source-native structured data used for native revision identity."
            }),
            codeBlock(nativeDocument));
        container.append(native);

        const mechanical = workspaceSection({ tagName: "details", className: "rules-core-source-raw" });
        mechanical.append(
            element("summary", { className: "fw-semibold", text: "Raw Rules Core mechanical data (advanced)" }),
            element("p", {
                className: "small text-body-secondary mt-3 mb-2",
                text: "Exact translated rule-bearing representation used by Rules Core. The source-native record above remains authoritative for source fidelity."
            }),
            codeBlock(entity.document));
        container.append(mechanical);
        presentFragment(app, container);
    } catch (error) {
        clear(container);
        container.append(alertNode("danger", describeError(error)));
        presentFragment(app, container);
    }
}

function renderReadableSourceValue(value, depth = 0) {
    if (value === null || value === undefined) {
        return element("span", { className: "text-body-secondary", text: "—" });
    }
    if (Array.isArray(value)) {
        if (!value.length) return element("span", { className: "text-body-secondary", text: "Empty list" });
        const list = element("ol", { className: "rules-core-source-native-list" });
        for (const item of value) {
            list.append(element("li", {}, renderReadableSourceValue(item, depth + 1)));
        }
        return list;
    }
    if (typeof value === "object") {
        const entries = Object.entries(value);
        if (!entries.length) return element("span", { className: "text-body-secondary", text: "Empty object" });
        const list = element("dl", { className: `rules-core-source-native-fields depth-${Math.min(depth, 3)}` });
        for (const [key, item] of entries) {
            list.append(
                element("dt", { text: readableSourceFieldLabel(key) }),
                element("dd", {}, renderReadableSourceValue(item, depth + 1)));
        }
        return list;
    }
    if (typeof value === "boolean") {
        return element("span", { text: value ? "Yes" : "No" });
    }
    return element("span", { text: String(value) });
}

function readableSourceFieldLabel(key) {
    return String(key ?? "")
        .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
        .replace(/[_-]+/g, " ")
        .replace(/^./, value => value.toUpperCase());
}

function renderIntegrationPayloadButton(entity) {
    const payload = {
        sourceEntityId: entity.entityId,
        name: entity.name,
        sourceCode: entity.sourceCode,
        revisionNumber: entity.revisionNumber,
        document: entity.document
    };
    const row = element("div", { className: "d-flex flex-wrap align-items-center gap-2 mt-3" });
    const copy = element("button", { type: "button", className: "btn btn-sm btn-outline-primary", text: "Copy beta payload" });
    copy.addEventListener("click", async () => {
        try {
            await navigator.clipboard.writeText(JSON.stringify(payload, null, 2));
            copy.textContent = "Copied";
            setTimeout(() => { copy.textContent = "Copy beta payload"; }, 1200);
        } catch {
            copy.textContent = "Clipboard unavailable";
        }
    });
    row.append(copy, element("span", {
        className: "small text-body-secondary",
        text: "API discovery: GET /api/sources/entities?entityType=monster&q=<name>; then GET /api/sources/entities/{entityId}."
    }));
    return row;
}

function parseSourceEntityRoute(toolRoute) {
    if (!toolRoute) return {};
    const path = String(toolRoute).split(/[?#]/, 1)[0];
    const segments = path.replace(/^\/+|\/+$/g, "").split("/").filter(Boolean);
    if (segments.length !== 2 || segments[0] !== "sources") return {};
    let entityId;
    try {
        entityId = decodeURIComponent(segments[1]);
    } catch {
        return {};
    }
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(entityId)
        ? { entityId }
        : {};
}

function sourceEntityRoute(entityId) {
    return `/sources/${encodeURIComponent(entityId)}`;
}

function sourceEntityHref(app, entityId) {
    return sourceToolHref(app, sourceEntityRoute(entityId));
}

function sourceLibraryHref(app) {
    return sourceToolHref(app, "/sources");
}

function sourceToolHref(app, toolRelativePath) {
    const base = app.hostContext.toolBasePath ?? "/tools/rules-core";
    return `${base.replace(/\/$/, "")}${toolRelativePath || "/"}`;
}

function pushSourceToolRoute(app, toolRelativePath) {
    const href = sourceToolHref(app, toolRelativePath);
    if (window.location.pathname !== href) window.history.pushState({}, "", href);
}

function currentToolRoute(app) {
    const base = (app.hostContext.toolBasePath ?? "/tools/rules-core").replace(/\/$/, "");
    const path = window.location.pathname;
    return path.startsWith(base) ? path.slice(base.length) || "/" : null;
}

function isSourceLibraryRootRoute(toolRoute) {
    if (toolRoute === null || toolRoute === undefined) return false;
    const path = String(toolRoute).split(/[?#]/, 1)[0];
    return path.replace(/^\/+|\/+$/g, "") === "sources";
}

function matchesDefinition(entity, definition) {
    return entity.packageKey === definition.packageKey
        && definition.includedSourceCodes.includes(entity.sourceCode);
}

function countLabel(count, capped) {
    return `${count}${capped ? "+" : ""}`;
}

function metric(label, value) {
    return element("div", { className: "rules-core-metric" },
        element("div", { className: "rules-core-metric-value", text: value }),
        element("div", { className: "rules-core-metric-label", text: label }));
}

function prependRulesLawyerWorkflow(app, container) {
    if (container.querySelector(".rules-core-workflow")) return;
    const card = element("div", { className: "card card-body mb-3 rules-core-workflow" },
        element("div", { className: "d-flex flex-wrap justify-content-between gap-3 align-items-center" },
            element("div", {},
                element("h3", { className: "h5 mb-1", text: "Rules Lawyer workflow" }),
                element("div", { className: "text-body-secondary small", text: "Source material stays separate until you explicitly bind, decide, and publish." })),
            element("div", { className: "rules-core-workflow-steps" },
                workflowStep("1", "Sources", "Browse and inspect", async () => {
                    await app.viewNavigation?.sources?.();
                }),
                workflowStep("2", "Normalize", "Review suggestions"),
                workflowStep("3", "Decide", "Select or consolidate"),
                workflowStep("4", "Publish", "Create global revision"))));
    container.prepend(card);
}

function workflowStep(number, title, description, onClick = null) {
    const options = { className: `rules-core-workflow-step${onClick ? " rules-core-workflow-step-action" : ""}` };
    const node = element(onClick ? "button" : "div", onClick ? { ...options, type: "button" } : options,
        element("span", { className: "rules-core-workflow-number", text: number }),
        element("span", {}, element("strong", { text: title }), element("small", { text: description })));
    if (onClick) node.addEventListener("click", onClick);
    return node;
}

function presentFragment(app, container) {
    app.presentRenderedFragment?.(container);
}