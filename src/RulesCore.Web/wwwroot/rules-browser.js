import {
    alertNode,
    badge,
    clear,
    definitionList,
    describeError,
    element,
    formatDate,
    setButtonBusy,
    DEFAULT_PAGE_SIZE,
    paginationControls
} from "./ui.js";
import { renderResolvedRule } from "./rule-renderers.js";

const DORKS_MODE = "dorks-and-dice";
const ROUTE_FAMILIES = new Map([
    ["monsters", "monster"],
    ["spells", "spell"],
    ["classes", "class"],
    ["feats", "feat"],
    ["races", "race"],
    ["species", "species"],
    ["items", "item"],
    ["conditions", "condition"]
]);

export function installResolvedRulesBrowser(app) {
    app.canBrowseRules = app.hostContext.siteMode === DORKS_MODE;
    app.browserScope = "global";
    app.browserPage = 0;
    app.browserFilters = { entityType: "", query: "" };
    app.browserDeepLink = null;

    const route = parseToolRoute(app.hostContext.toolRoute);
    if (route.entityType) app.browserFilters.entityType = route.entityType;
    if (route.conceptKey) app.browserDeepLink = route.conceptKey;

    if (app.canBrowseRules) {
        app.activeView = "browse";
    }

    const renderNavigation = app.renderNavigation.bind(app);
    app.renderNavigation = () => {
        const nav = renderNavigation();
        if (app.canBrowseRules) {
            nav.prepend(app.navButton("Rules Browser", "browse"));
        }
        return nav;
    };

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        if (app.activeView === "browse") {
            if (app.browserDeepLink) {
                const conceptKey = app.browserDeepLink;
                app.browserDeepLink = null;
                await renderRuleDetailByKey(app, container, conceptKey, "global");
                return;
            }
            await renderRulesBrowser(app, container);
            return;
        }
        await renderActiveView(container);
    };
}

async function renderRulesBrowser(app, container) {
    clear(container);

    const heading = element("div", { className: "card card-body mb-3" });
    heading.append(
        element("h3", { className: "h5 mb-1", text: "Dorks & Dice Rules" }),
        element("p", {
            className: "text-body-secondary mb-0",
            text: "Browse the published resolved ruleset. Campaign scope overlays the campaign publication on its pinned global baseline."
        }));
    container.append(heading);

    const controls = element("div", { className: "card card-body mb-3" });
    const row = element("div", { className: "row g-2 align-items-end" });

    const scopeColumn = element("div", { className: "col-lg-3" });
    scopeColumn.append(element("label", { className: "form-label fw-semibold", text: "Rules scope" }));
    const scope = element("select", { className: "form-select" });
    scope.append(element("option", { value: "global", text: "Global Rules" }));
    for (const campaign of app.campaigns) {
        scope.append(element("option", {
            value: `campaign:${campaign.id}`,
            text: `Campaign: ${campaign.name ?? campaign.id}`
        }));
    }
    scope.value = app.browserScope;
    scopeColumn.append(scope);

    const type = inputGroup("Entity type", "monster, spell, class…", "col-lg-2");
    const query = inputGroup("Search", "Rule, source, package, edition…", "col-lg-5");
    type.input.value = app.browserFilters.entityType;
    query.input.value = app.browserFilters.query;
    const actionColumn = element("div", { className: "col-lg-2 d-grid" });
    const search = element("button", { type: "button", className: "btn btn-primary", text: "Browse" });
    actionColumn.append(search);
    row.append(scopeColumn, type.group, query.group, actionColumn);
    controls.append(row);
    container.append(controls);

    const status = element("div");
    const results = element("div");
    container.append(status, results);

    const load = async (resetPage = false) => {
        if (resetPage) app.browserPage = 0;
        status.replaceChildren();
        results.replaceChildren();
        app.browserScope = scope.value;
        app.browserFilters = { entityType: type.input.value.trim(), query: query.input.value.trim() };
        setButtonBusy(search, true, "Loading…");
        try {
            const filters = {
                entityType: app.browserFilters.entityType || null,
                query: app.browserFilters.query || null,
                limit: DEFAULT_PAGE_SIZE + 1,
                offset: app.browserPage * DEFAULT_PAGE_SIZE
            };
            const requested = scope.value === "global"
                ? await app.api.getGlobalRulesCatalog(filters)
                : await app.api.getCampaignRulesCatalog(scope.value.slice("campaign:".length), filters);
            const hasNext = (requested.rules?.length ?? 0) > DEFAULT_PAGE_SIZE;
            const catalog = { ...requested, rules: (requested.rules ?? []).slice(0, DEFAULT_PAGE_SIZE) };
            if (!catalog.rules.length && app.browserPage > 0) {
                app.browserPage -= 1;
                await load(false);
                return;
            }
            renderCatalog(app, container, results, catalog, scope.value, app.browserPage, hasNext, async nextPage => {
                app.browserPage = nextPage;
                await load(false);
            });
        } catch (error) {
            status.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(search, false);
        }
    };

    scope.addEventListener("change", () => load(true));
    search.addEventListener("click", () => load(true));
    for (const input of [type.input, query.input]) {
        input.addEventListener("keydown", event => {
            if (event.key === "Enter") {
                event.preventDefault();
                load(true);
            }
        });
    }

    await load(false);
}

function renderCatalog(app, container, results, catalog, scopeValue, page, hasNext, onPage) {
    results.replaceChildren();
    const summary = element("div", { className: "card card-body mb-3" });
    summary.append(definitionList([
        ["Published revision", catalog.revisionNumber ? `#${catalog.revisionNumber}` : "None"],
        ["Published", formatDate(catalog.publishedAt)],
        ["Rules on this page", String(catalog.rules?.length ?? 0)]
    ]));
    results.append(summary);

    if (!catalog.revisionNumber) {
        results.append(alertNode("secondary", catalog.scope === "campaign"
            ? "This campaign has no published ruleset yet."
            : "No global ruleset has been published yet."));
        return;
    }
    if (!catalog.rules?.length) {
        results.append(alertNode("secondary", "No published rules available to you match the current filters."));
        return;
    }

    const table = element("table", { className: "table table-hover align-middle mb-0" });
    const head = element("thead");
    const headRow = element("tr");
    for (const label of ["Rule", "Type", "Source", "Effective decision", ""]) headRow.append(element("th", { text: label }));
    head.append(headRow);
    const body = element("tbody");

    for (const rule of catalog.rules) {
        const row = element("tr");
        const name = element("td");
        const href = browserHref(app, rule.browserLink?.toolRelativePath);
        const link = element("a", {
            className: "fw-semibold text-decoration-none",
            text: rule.displayName,
            attributes: { href },
            onClick: async event => {
                event.preventDefault();
                pushToolRoute(app, rule.browserLink?.toolRelativePath);
                await renderRuleDetail(app, container, rule.conceptKey, scopeValue);
            }
        });
        name.append(link, element("div", { className: "small text-body-secondary font-monospace", text: rule.conceptKey }));
        row.append(name, element("td", { text: rule.entityType }));
        row.append(element("td", {},
            element("div", { text: rule.sourceEntityName }),
            element("div", { className: "small text-body-secondary", text: `${rule.packageDisplayName} · ${rule.editionDisplayName} · rev. ${rule.sourceRevisionNumber}` })));
        const decision = element("td");
        decision.append(badge(rule.effectiveDecisionKind, rule.hasCampaignOverride ? "warning" : "secondary"));
        if (rule.hasCampaignOverride) decision.append(element("div", { className: "small text-body-secondary mt-1", text: "Campaign override" }));
        row.append(decision);
        row.append(element("td", { className: "text-end" }, element("a", {
            className: "btn btn-sm btn-outline-primary",
            text: "Open",
            attributes: { href },
            onClick: async event => {
                event.preventDefault();
                pushToolRoute(app, rule.browserLink?.toolRelativePath);
                await renderRuleDetail(app, container, rule.conceptKey, scopeValue);
            }
        })));
        body.append(row);
    }

    table.append(head, body);
    results.append(
        element("div", { className: "card" }, element("div", { className: "table-responsive" }, table)),
        paginationControls({ page, itemCount: catalog.rules.length, hasNext, onPage }));
}

async function renderRuleDetail(app, container, conceptKey, scopeValue) {
    const campaignId = scopeValue.startsWith("campaign:") ? scopeValue.slice("campaign:".length) : null;
    clear(container);
    container.append(element("button", {
        type: "button",
        className: "btn btn-outline-secondary mb-3",
        text: "Back to rules",
        onClick: async () => {
            pushToolRoute(app, catalogRouteForEntity(app.browserFilters.entityType));
            await renderRulesBrowser(app, container);
        }
    }));

    const loading = element("div", { className: "card card-body text-body-secondary", text: "Loading resolved rule…" });
    container.append(loading);
    try {
        const resolved = campaignId
            ? await app.api.getCampaignResolvedRule(campaignId, conceptKey)
            : await app.api.getGlobalResolvedRule(conceptKey);
        const baseline = campaignId ? await getOptionalCampaignBaseline(app, campaignId, conceptKey) : null;
        loading.remove();
        renderResolvedDetail(app, container, resolved, baseline, campaignId !== null);
    } catch (error) {
        loading.remove();
        container.append(alertNode("danger", describeError(error)));
    }
}

async function renderRuleDetailByKey(app, container, conceptKey, scopeValue) {
    await renderRuleDetail(app, container, conceptKey, scopeValue);
}

function renderResolvedDetail(app, container, resolved, baseline, campaignScope) {
    const metadata = element("div", { className: "card card-body mb-3" });
    metadata.append(
        element("div", { className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3" },
            element("div", {},
                element("h3", { className: "h4 mb-1", text: resolved.displayName }),
                element("div", { className: "text-body-secondary font-monospace small", text: resolved.conceptKey })),
            badge(resolved.entityType, "primary")),
        definitionList([
            ["Scope", campaignScope ? "Campaign effective rule" : "Published global rule"],
            ["Published revision", `#${resolved.campaignRulesetRevisionNumber ?? resolved.rulesetRevisionNumber}`],
            ["Decision", resolved.effectiveDecisionKind ?? resolved.decisionKind],
            ["Source", `${resolved.sourceEntityName} (${resolved.sourceCode})`],
            ["Edition", resolved.editionDisplayName]
        ]));
    container.append(metadata);

    container.append(element("section", { className: "mb-3" },
        element("h4", { className: "h5", text: campaignScope ? "Effective campaign rule" : "Dorks & Dice rule" }),
        renderResolvedRule(resolved.entityType, resolved.document)));

    if (campaignScope) {
        const campaign = element("div", { className: "card card-body mb-3" });
        campaign.append(
            element("h4", { className: "h5", text: "Campaign overlay" }),
            definitionList([
                ["Pinned global baseline", `#${resolved.baselineRulesetRevisionNumber}`],
                ["Global decision", `#${resolved.globalDecisionNumber} · ${resolved.globalDecisionKind}`],
                ["Campaign decision", resolved.campaignDecisionNumber ? `#${resolved.campaignDecisionNumber} · ${resolved.effectiveDecisionKind}` : "Inherited without campaign override"],
                ["Campaign note", resolved.campaignDecisionNote]
            ]));
        container.append(campaign);

        if (baseline) {
            const details = element("details", { className: "card card-body mb-3" });
            details.append(
                element("summary", { className: "fw-semibold", text: `Published global baseline #${baseline.baselineRulesetRevisionNumber}` }),
                element("div", { className: "mt-3" }, renderResolvedRule(baseline.entityType, baseline.document)));
            container.append(details);
        } else {
            container.append(alertNode("secondary", "The pinned global baseline is not available to this account under the independent source-access rules."));
        }
    }

    container.append(renderProvenance(resolved));
}

function renderProvenance(resolved) {
    const card = element("div", { className: "card card-body mb-3" });
    card.append(element("h4", { className: "h5", text: "Provenance" }));
    const contributions = resolved.contributions ?? resolved.globalContributions ?? [];
    card.append(definitionList([
        ["Selected source", `${resolved.sourceEntityName} · ${resolved.editionDisplayName} · rev. ${resolved.sourceRevisionNumber}`],
        ["Package", resolved.packageDisplayName],
        ["Decision note", resolved.decisionNote ?? resolved.campaignDecisionNote],
        ["Additional contributing sources", String(contributions.length)]
    ]));
    if (contributions.length) {
        const list = element("ul", { className: "mb-0 mt-3" });
        for (const contribution of contributions) {
            list.append(element("li", {},
                element("span", { className: "fw-semibold", text: `${contribution.sourceEntityName} · ${contribution.editionDisplayName}` }),
                ` — ${contribution.contributionKind}${contribution.note ? `: ${contribution.note}` : ""}`));
        }
        card.append(list);
    }
    return card;
}

async function getOptionalCampaignBaseline(app, campaignId, conceptKey) {
    try {
        return await app.api.backend(`/api/campaigns/${encodeURIComponent(campaignId)}/rules/${encodeURIComponent(conceptKey)}/global-baseline`);
    } catch (error) {
        if (error?.status === 404) return null;
        throw error;
    }
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
        return { entityType, conceptKey: `${entityType}.${decodeURIComponent(segments[1])}` };
    }
    if (segments.length === 2 && segments[0] === "rules") {
        return { conceptKey: decodeURIComponent(segments[1]) };
    }
    return {};
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

function inputGroup(label, placeholder, columnClass) {
    const input = element("input", { className: "form-control", type: "text", placeholder });
    const group = element("div", { className: columnClass });
    group.append(element("label", { className: "form-label fw-semibold", text: label }), input);
    return { group, input };
}
