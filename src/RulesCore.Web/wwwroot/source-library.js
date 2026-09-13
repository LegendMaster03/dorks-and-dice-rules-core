import {
    alertNode,
    badge,
    clear,
    definitionList,
    describeError,
    element,
    formatDate,
    paginationControls,
    DEFAULT_PAGE_SIZE
} from "./ui.js";
import { renderRuleDocument, rawDocumentDisclosure } from "./rule-renderers.js";
import { routeParts, withQuery } from "./browser-navigation.js";

const DORKS_MODE = "dorks-and-dice";
const SEGMENT_TYPES = new Map([
    ["monsters", "monster"], ["spells", "spell"], ["classes", "class"], ["subclasses", "subclass"],
    ["class-features", "classFeature"], ["subclass-features", "subclassFeature"], ["prestige-classes", "prestigeClass"],
    ["npc-classes", "npcClass"], ["races", "race"], ["species", "species"], ["feats", "feat"],
    ["skills", "skill"], ["items", "item"], ["powers", "power"], ["domains", "domain"],
    ["divine-abilities", "divineAbility"], ["conditions", "condition"], ["house-rules", "houseRule"],
    ["rules", "rule"], ["documents", "source-fragment"], ["backgrounds", "background"],
    ["optional-features", "optionalfeature"], ["rewards", "reward"], ["objects", "object"],
    ["traps", "trap"], ["vehicles", "vehicle"], ["deities", "deity"], ["languages", "language"],
    ["cults", "cult"], ["boons", "boon"], ["recipes", "recipe"]
]);
const TYPE_SEGMENTS = new Map([...SEGMENT_TYPES].map(([segment, type]) => [type.toLowerCase(), segment]));

export function installSourceLibrary(app) {
    app.canBrowseSourceLibrary = app.hostContext.siteMode === DORKS_MODE;
    app.libraryRoute = { kind: "index", query: "", page: 0 };

    if (app.canBrowseSourceLibrary && app.registerToolRouteHandler) {
        app.registerToolRouteHandler(route => applyLibraryRoute(app, route));
    }

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        if (app.activeView === "library") {
            await renderSourceLibrary(app, container);
            return;
        }
        await renderActiveView(container);
        if (app.activeView === "global" && app.canEditGlobal) prependRulesLawyerWorkflow(app, container);
    };
}

function applyLibraryRoute(app, route) {
    if (!app.canBrowseSourceLibrary) return false;
    const { segments, query } = routeParts(route);
    if (segments.length === 0) {
        app.activeView = "library";
        app.libraryRoute = { kind: "index", query: query.get("q") ?? "", page: pageNumber(query) };
        return true;
    }
    if (segments[0] !== "library") return false;

    if (!segments[1]) {
        app.activeView = "library";
        app.libraryRoute = { kind: "index", query: query.get("q") ?? "", page: pageNumber(query) };
        return true;
    }

    if (segments[1] === "types" && segments[2]) {
        app.activeView = "library";
        app.libraryRoute = { kind: "global-collection", entityType: segments[2], query: query.get("q") ?? "", page: pageNumber(query) };
        return true;
    }
    if (SEGMENT_TYPES.has(segments[1])) {
        app.activeView = "library";
        app.libraryRoute = { kind: "global-collection", entityType: SEGMENT_TYPES.get(segments[1]), query: query.get("q") ?? "", page: pageNumber(query) };
        return true;
    }

    const publicationId = segments[1];
    let entityType = null;
    let entityId = null;
    if (segments[2] === "types") {
        entityType = segments[3] ?? null;
        entityId = segments[4] ?? null;
    } else if (segments[2]) {
        entityType = SEGMENT_TYPES.get(segments[2]) ?? null;
        entityId = segments[3] ?? null;
    }

    app.activeView = "library";
    app.libraryRoute = entityId
        ? { kind: "entity", publicationId, entityType, entityId }
        : entityType
            ? { kind: "collection", publicationId, entityType, query: query.get("q") ?? "", page: pageNumber(query) }
            : { kind: "publication", publicationId, query: query.get("q") ?? "", page: pageNumber(query) };
    return true;
}

function pageNumber(query) {
    const value = Number(query.get("page") ?? "1");
    return Number.isFinite(value) && value > 0 ? Math.floor(value) - 1 : 0;
}

async function renderSourceLibrary(app, container) {
    clear(container);
    const route = app.libraryRoute ?? { kind: "index" };
    try {
        if (route.kind === "index") await renderLibraryIndex(app, container, route);
        else if (route.kind === "global-collection") await renderGlobalCollection(app, container, route);
        else if (route.kind === "publication") await renderPublication(app, container, route);
        else if (route.kind === "collection") await renderCollection(app, container, route);
        else await renderEntity(app, container, route);
    } catch (error) {
        container.append(alertNode("danger", describeError(error)));
    }
}

async function renderLibraryIndex(app, container, route) {
    container.append(element("section", { className: "rules-core-library-hero rules-core-page-intro" },
        element("div", { className: "rules-core-eyebrow", text: "Source Layer" }),
        element("h2", { text: "Rules Library" }),
        element("p", {
            text: "Browse immutable source publications and their rules content. Source material is evidence and provenance; it does not become a Dorks & Dice table rule until it is explicitly adjudicated and published."
        })));

    const publications = await app.api.getSourceLibraryPublications();
    const search = element("input", {
        type: "search",
        className: "form-control",
        placeholder: "Search all accessible source entries",
        value: route.query ?? ""
    });
    const form = element("form", { className: "rules-core-library-search mb-3" }, search,
        element("button", { type: "submit", className: "btn btn-primary", text: "Search library" }));
    form.addEventListener("submit", async event => {
        event.preventDefault();
        await app.navigateToolRoute(withQuery("/library", { q: search.value.trim() }));
    });
    container.append(form);

    if (route.query) {
        container.append(element("div", { className: "d-flex justify-content-between align-items-center gap-2 mb-3" },
            element("h3", { className: "h5 mb-0", text: `Search results for “${route.query}”` }),
            element("a", {
                className: "btn btn-sm btn-outline-secondary",
                text: "Clear search",
                attributes: { href: app.toolHref("/library") },
                onClick: async event => {
                    event.preventDefault();
                    await app.navigateToolRoute("/library");
                }
            })));
        await renderEntityList(app, container, {
            publicationId: null,
            entityType: null,
            query: route.query,
            page: route.page,
            title: null,
            basePath: "/library"
        });
        return;
    }

    const categoryTotals = aggregateCategories(publications);
    if (categoryTotals.length) {
        const browse = element("section", { className: "mb-4" }, element("h3", { className: "h5 mb-3", text: "Browse across publications" }));
        const categoryGrid = element("div", { className: "rules-core-category-grid" });
        for (const category of categoryTotals.slice(0, 16)) {
            const path = globalCollectionPath(category.entityType);
            categoryGrid.append(element("a", {
                className: "rules-core-category-card",
                attributes: { href: app.toolHref(path) },
                onClick: async event => {
                    event.preventDefault();
                    await app.navigateToolRoute(path);
                }
            },
                element("span", { className: "rules-core-category-name", text: category.displayName }),
                element("span", { className: "rules-core-category-count", text: String(category.count) })));
        }
        browse.append(categoryGrid);
        container.append(browse);
    }

    if (!publications.length) {
        container.append(alertNode("secondary", "No accessible source publications are currently available."));
        return;
    }

    container.append(element("h3", { className: "h5 mb-3", text: "Publications" }));
    const grid = element("div", { className: "rules-core-publication-grid" });
    for (const publication of publications) grid.append(publicationCard(app, publication));
    container.append(grid);
}

function aggregateCategories(publications) {
    const totals = new Map();
    for (const publication of publications) {
        for (const category of publication.categories ?? []) {
            const key = String(category.entityType).toLowerCase();
            const current = totals.get(key) ?? { entityType: category.entityType, displayName: category.displayName, count: 0 };
            current.count += category.count;
            totals.set(key, current);
        }
    }
    return [...totals.values()].sort((left, right) => left.displayName.localeCompare(right.displayName));
}

function publicationCard(app, publication) {
    const href = app.toolHref(publication.browserLink.toolRelativePath);
    return element("article", { className: "card card-body rules-core-publication-card" },
        element("div", { className: "rules-core-publication-card-head" },
            element("div", {},
                element("div", { className: "rules-core-eyebrow", text: publication.editionDisplayName }),
                element("h3", { className: "h5", text: publication.displayName }),
                element("div", { className: "small text-body-secondary", text: publication.provider })),
            badge(publication.isPublic ? "Public" : "Granted", publication.isPublic ? "success" : "primary")),
        element("div", { className: "rules-core-publication-meta" },
            metric("Entries", publication.entityCount),
            metric("Categories", publication.categories?.length ?? 0),
            metric("Updated", formatDate(publication.latestImportedAt))),
        categoryPreview(publication.categories),
        element("a", {
            className: "btn btn-outline-primary align-self-start",
            text: "Open publication",
            attributes: { href },
            onClick: async event => {
                event.preventDefault();
                await app.navigateToolRoute(publication.browserLink.toolRelativePath);
            }
        }));
}

function categoryPreview(categories = []) {
    const row = element("div", { className: "rules-core-category-preview" });
    for (const category of categories.slice(0, 6)) row.append(element("span", { className: "rules-core-category-chip", text: `${category.displayName} ${category.count}` }));
    if (categories.length > 6) row.append(element("span", { className: "rules-core-category-chip", text: `+${categories.length - 6} more` }));
    return row;
}

async function renderGlobalCollection(app, container, route) {
    const title = humanizePlural(route.entityType);
    container.append(breadcrumbs(app, [["Rules Library", "/library"], [title, null]]));
    container.append(element("section", { className: "rules-core-page-intro" },
        element("div", { className: "rules-core-eyebrow", text: "Across Publications" }),
        element("h2", { text: title }),
        element("p", { text: `Browse accessible ${title.toLowerCase()} from every publication in your Rules Library.` })));
    await renderEntityList(app, container, {
        publicationId: null,
        entityType: route.entityType,
        query: route.query,
        page: route.page,
        title: null,
        basePath: globalCollectionPath(route.entityType)
    });
}

async function renderPublication(app, container, route) {
    const publication = await app.api.getSourceLibraryPublication(route.publicationId);
    container.append(breadcrumbs(app, [["Rules Library", "/library"], [publication.displayName, null]]));
    container.append(element("section", { className: "rules-core-page-intro" },
        element("div", { className: "rules-core-eyebrow", text: publication.editionDisplayName }),
        element("h2", { text: publication.displayName }),
        element("p", { text: `${publication.provider}${publication.license ? ` · ${publication.license}` : ""}` })));

    const categories = element("section", { className: "mb-4" }, element("h3", { className: "h5 mb-3", text: "Browse by category" }));
    const grid = element("div", { className: "rules-core-category-grid" });
    for (const category of publication.categories ?? []) {
        const href = app.toolHref(category.browserLink.toolRelativePath);
        grid.append(element("a", {
            className: "rules-core-category-card",
            attributes: { href },
            onClick: async event => {
                event.preventDefault();
                await app.navigateToolRoute(category.browserLink.toolRelativePath);
            }
        }, element("span", { className: "rules-core-category-name", text: category.displayName }), element("span", { className: "rules-core-category-count", text: String(category.count) })));
    }
    categories.append(grid);
    container.append(categories);

    await renderEntityList(app, container, {
        publicationId: route.publicationId,
        entityType: null,
        query: route.query,
        page: route.page,
        title: "All entries",
        basePath: publication.browserLink.toolRelativePath
    });
}

async function renderCollection(app, container, route) {
    const publication = await app.api.getSourceLibraryPublication(route.publicationId);
    const category = publication.categories?.find(value => value.entityType.toLowerCase() === String(route.entityType).toLowerCase());
    const title = category?.displayName ?? humanizePlural(route.entityType);
    container.append(breadcrumbs(app, [
        ["Rules Library", "/library"],
        [publication.displayName, publication.browserLink.toolRelativePath],
        [title, null]
    ]));
    container.append(element("section", { className: "rules-core-page-intro" },
        element("div", { className: "rules-core-eyebrow", text: publication.editionDisplayName }),
        element("h2", { text: title }),
        element("p", { text: `Source entries from ${publication.displayName}.` })));
    await renderEntityList(app, container, {
        publicationId: route.publicationId,
        entityType: route.entityType,
        query: route.query,
        page: route.page,
        title: null,
        basePath: category?.browserLink?.toolRelativePath ?? `/library/${route.publicationId}/types/${encodeURIComponent(route.entityType)}`
    });
}

async function renderEntityList(app, container, { publicationId, entityType, query, page, title, basePath }) {
    if (title) container.append(element("h3", { className: "h5 mb-3", text: title }));
    const search = element("input", {
        type: "search",
        className: "form-control",
        placeholder: entityType ? `Search ${humanizePlural(entityType).toLowerCase()}` : "Search entries",
        value: query ?? ""
    });
    const form = element("form", { className: "rules-core-library-search mb-3" }, search,
        element("button", { type: "submit", className: "btn btn-primary", text: "Search" }));
    form.addEventListener("submit", async event => {
        event.preventDefault();
        await app.navigateToolRoute(withQuery(basePath, { q: search.value.trim() }));
    });
    container.append(form);

    const requested = await app.api.getSourceLibraryEntityPage({
        publicationId,
        entityType,
        query: query || null,
        limit: DEFAULT_PAGE_SIZE + 1,
        offset: Math.max(0, page ?? 0) * DEFAULT_PAGE_SIZE
    });
    const hasNext = requested.length > DEFAULT_PAGE_SIZE;
    const entries = requested.slice(0, DEFAULT_PAGE_SIZE);
    if (!entries.length) {
        container.append(alertNode("secondary", "No source entries match the current filters."));
        return;
    }

    const list = element("div", { className: "rules-core-entry-list" });
    for (const entry of entries) {
        const href = app.toolHref(entry.browserLink.toolRelativePath);
        list.append(element("a", {
            className: "rules-core-entry-row",
            attributes: { href },
            onClick: async event => {
                event.preventDefault();
                await app.navigateToolRoute(entry.browserLink.toolRelativePath);
            }
        },
            element("span", { className: "rules-core-entry-main" },
                element("strong", { text: entry.name }),
                element("small", { text: `${entry.publicationDisplayName} · ${entry.sourceCode}` })),
            element("span", { className: "rules-core-entry-type", text: humanize(entry.entityType) }),
            element("span", { className: "rules-core-entry-revision", text: `rev. ${entry.latestRevisionNumber}` })));
    }
    container.append(list);
    container.append(paginationControls({
        page: page ?? 0,
        itemCount: entries.length,
        hasNext,
        onPage: async nextPage => app.navigateToolRoute(withQuery(basePath, { q: query || null, page: nextPage + 1 }))
    }));
}

async function renderEntity(app, container, route) {
    const entity = await app.api.getSourceLibraryEntity(route.publicationId, route.entityId);
    const publication = await app.api.getSourceLibraryPublication(route.publicationId);
    const category = publication.categories?.find(value => value.entityType.toLowerCase() === String(entity.entityType).toLowerCase());
    container.append(breadcrumbs(app, [
        ["Rules Library", "/library"],
        [publication.displayName, publication.browserLink.toolRelativePath],
        [category?.displayName ?? humanizePlural(entity.entityType), category?.browserLink?.toolRelativePath ?? null],
        [entity.name, null]
    ]));

    container.append(element("header", { className: "rules-core-entry-header" },
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: `${entity.editionDisplayName} · ${humanize(entity.entityType)}` }),
            element("h2", { text: entity.name }),
            element("p", { className: "text-body-secondary", text: entity.sourceCode })),
        badge(`rev. ${entity.revisionNumber}`, "secondary")));

    container.append(renderRuleDocument(entity.entityType, entity.document));

    const provenance = element("details", { className: "card card-body rules-core-provenance mt-4" });
    provenance.append(
        element("summary", { className: "fw-semibold", text: "Source provenance" }),
        element("div", { className: "pt-3" }, definitionList([
            ["Publication", entity.publicationDisplayName],
            ["Edition", entity.editionDisplayName],
            ["Provider", entity.provider],
            ["License", entity.license],
            ["Source code", entity.sourceCode],
            ["Revision", `#${entity.revisionNumber}`],
            ["Imported", formatDate(entity.importedAt)],
            ["Source entity ID", entity.entityId]
        ])));
    container.append(provenance, rawDocumentDisclosure(entity.document));
}

function breadcrumbs(app, items) {
    const nav = element("nav", { className: "rules-core-breadcrumbs", ariaLabel: "Breadcrumb" });
    items.forEach(([label, route], index) => {
        if (index > 0) nav.append(element("span", { className: "rules-core-breadcrumb-separator", text: "/" }));
        if (!route) nav.append(element("span", { text: label, attributes: { "aria-current": "page" } }));
        else nav.append(element("a", {
            text: label,
            attributes: { href: app.toolHref(route) },
            onClick: async event => {
                event.preventDefault();
                await app.navigateToolRoute(route);
            }
        }));
    });
    return nav;
}

function globalCollectionPath(entityType) {
    const segment = TYPE_SEGMENTS.get(String(entityType).toLowerCase());
    return segment ? `/library/${segment}` : `/library/types/${encodeURIComponent(entityType)}`;
}

function metric(label, value) {
    return element("div", { className: "rules-core-metric" },
        element("div", { className: "rules-core-metric-value", text: value ?? "—" }),
        element("div", { className: "rules-core-metric-label", text: label }));
}

function humanize(value) {
    return String(value ?? "")
        .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
        .replace(/[-_]+/g, " ")
        .replace(/^./, character => character.toUpperCase());
}

function humanizePlural(value) {
    const name = humanize(value);
    if (!name) return "Entries";
    if (name.endsWith("s")) return name;
    if (name.endsWith("y")) return `${name.slice(0, -1)}ies`;
    return `${name}s`;
}

function prependRulesLawyerWorkflow(app, container) {
    if (container.querySelector(".rules-core-workflow")) return;
    const card = element("div", { className: "card card-body mb-3 rules-core-workflow" },
        element("div", { className: "d-flex flex-wrap justify-content-between gap-3 align-items-center" },
            element("div", {},
                element("h3", { className: "h5 mb-1", text: "Rules Lawyer workflow" }),
                element("div", { className: "text-body-secondary small", text: "Source material stays separate until you deliberately bind, decide, and publish." })),
            element("div", { className: "rules-core-workflow-steps" },
                workflowStep("1", "Sources", "Browse and inspect", async () => app.navigateToolRoute("/library")),
                workflowStep("2", "Normalize", "Review suggestions"),
                workflowStep("3", "Decide", "Select or consolidate"),
                workflowStep("4", "Publish", "Create global revision"))));
    container.prepend(card);
}

function workflowStep(number, title, description, onClick = null) {
    const node = element(onClick ? "button" : "div", onClick
        ? { className: "rules-core-workflow-step rules-core-workflow-step-action", type: "button" }
        : { className: "rules-core-workflow-step" },
        element("span", { className: "rules-core-workflow-number", text: number }),
        element("span", {}, element("strong", { text: title }), element("small", { text: description })));
    if (onClick) node.addEventListener("click", onClick);
    return node;
}
