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
import { renderResolvedRule, rawDocumentDisclosure } from "./rule-renderers.js";
import { routeParts, withQuery } from "./browser-navigation.js";

const DORKS_MODE = "dorks-and-dice";
const ROUTE_FAMILIES = new Map([
    ["monsters", "monster"], ["spells", "spell"], ["classes", "class"], ["feats", "feat"],
    ["races", "race"], ["species", "species"], ["items", "item"], ["conditions", "condition"]
]);

export function installResolvedRulesBrowser(app) {
    app.canBrowseRules = app.hostContext.siteMode === DORKS_MODE;
    app.browserRoute = { kind: "catalog", entityType: "", query: "", page: 0, scope: "global" };

    if (app.canBrowseRules && app.registerToolRouteHandler) {
        app.registerToolRouteHandler(route => applyPublishedRoute(app, route));
    }

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        if (app.activeView === "browse") {
            if (app.browserRoute.kind === "detail") await renderRuleDetail(app, container, app.browserRoute);
            else await renderRulesBrowser(app, container, app.browserRoute);
            return;
        }
        await renderActiveView(container);
    };
}

function applyPublishedRoute(app, route) {
    if (!app.canBrowseRules) return false;
    const { segments, query } = routeParts(route);
    const first = segments[0];
    let entityType = "";
    let conceptKey = null;

    if (first === "published") {
        entityType = query.get("type") ?? "";
    } else if (segments.length === 1 && ROUTE_FAMILIES.has(first)) {
        entityType = ROUTE_FAMILIES.get(first);
    } else if (segments.length === 2 && ROUTE_FAMILIES.has(first)) {
        entityType = ROUTE_FAMILIES.get(first);
        conceptKey = `${entityType}.${segments[1]}`;
    } else if (segments.length === 2 && first === "rules") {
        conceptKey = segments[1];
    } else {
        return false;
    }

    app.activeView = "browse";
    app.browserRoute = conceptKey
        ? { kind: "detail", conceptKey, entityType, scope: query.get("scope") ?? "global" }
        : {
            kind: "catalog",
            entityType,
            query: query.get("q") ?? "",
            page: pageNumber(query),
            scope: query.get("scope") ?? "global"
        };
    return true;
}

function pageNumber(query) {
    const value = Number(query.get("page") ?? "1");
    return Number.isFinite(value) && value > 0 ? Math.floor(value) - 1 : 0;
}

async function renderRulesBrowser(app, container, route) {
    clear(container);
    container.append(element("section", { className: "rules-core-page-intro" },
        element("div", { className: "rules-core-eyebrow", text: "Rules Layer" }),
        element("h2", { text: route.entityType ? humanizePlural(route.entityType) : "Published Rules" }),
        element("p", {
            text: "Browse the resolved rules your table has deliberately published. Source publications remain available separately in the Rules Library."
        })));

    const controls = element("form", { className: "rules-core-published-controls mb-4" });
    const scope = element("select", { className: "form-select" });
    scope.append(element("option", { value: "global", text: "Global Rules" }));
    for (const campaign of app.campaigns) {
        scope.append(element("option", { value: `campaign:${campaign.id}`, text: `Campaign: ${campaign.name ?? campaign.id}` }));
    }
    scope.value = validScope(app, route.scope);

    const type = element("input", {
        className: "form-control",
        type: "text",
        placeholder: "monster, spell, class…",
        value: route.entityType ?? ""
    });
    const query = element("input", {
        className: "form-control",
        type: "search",
        placeholder: "Search published rules",
        value: route.query ?? ""
    });
    controls.append(
        labeled("Scope", scope),
        labeled("Type", type),
        labeled("Search", query, "rules-core-search-grow"),
        element("button", { type: "submit", className: "btn btn-primary", text: "Browse" }));
    controls.addEventListener("submit", async event => {
        event.preventDefault();
        await app.navigateToolRoute(withQuery("/published", {
            type: type.value.trim(),
            q: query.value.trim(),
            scope: scope.value === "global" ? null : scope.value
        }));
    });
    scope.addEventListener("change", async () => {
        await app.navigateToolRoute(withQuery(catalogPath(route.entityType), {
            q: query.value.trim(),
            scope: scope.value === "global" ? null : scope.value
        }));
    });
    container.append(controls);

    const filters = {
        entityType: route.entityType || null,
        query: route.query || null,
        limit: DEFAULT_PAGE_SIZE + 1,
        offset: route.page * DEFAULT_PAGE_SIZE
    };
    const requested = scope.value === "global"
        ? await app.api.getGlobalRulesCatalog(filters)
        : await app.api.getCampaignRulesCatalog(scope.value.slice("campaign:".length), filters);
    const hasNext = (requested.rules?.length ?? 0) > DEFAULT_PAGE_SIZE;
    const rules = (requested.rules ?? []).slice(0, DEFAULT_PAGE_SIZE);

    if (!requested.revisionNumber) {
        container.append(alertNode("secondary", requested.scope === "campaign"
            ? "This campaign has no published ruleset yet."
            : "No global ruleset has been published yet."));
        return;
    }

    container.append(element("div", { className: "rules-core-published-meta" },
        element("span", {}, element("strong", { text: `Revision #${requested.revisionNumber}` })),
        element("span", { text: formatDate(requested.publishedAt) }),
        element("span", { text: `${rules.length} rule${rules.length === 1 ? "" : "s"} on this page` })));

    if (!rules.length) {
        container.append(alertNode("secondary", "No published rules available to you match the current filters."));
        return;
    }

    const list = element("div", { className: "rules-core-published-list" });
    for (const rule of rules) list.append(publishedRuleRow(app, rule, scope.value));
    container.append(list, paginationControls({
        page: route.page,
        itemCount: rules.length,
        hasNext,
        onPage: async nextPage => app.navigateToolRoute(withQuery(catalogPath(route.entityType), {
            q: route.query || null,
            page: nextPage + 1,
            scope: scope.value === "global" ? null : scope.value
        }))
    }));
}

function publishedRuleRow(app, rule, scopeValue) {
    const route = withQuery(rule.browserLink?.toolRelativePath ?? `/rules/${encodeURIComponent(rule.conceptKey)}`, {
        scope: scopeValue === "global" ? null : scopeValue
    });
    const href = app.toolHref(route);
    return element("a", {
        className: "rules-core-published-row",
        attributes: { href },
        onClick: async event => {
            event.preventDefault();
            await app.navigateToolRoute(route);
        }
    },
        element("span", { className: "rules-core-published-rule" },
            element("strong", { text: rule.displayName }),
            element("small", { text: `${rule.sourceEntityName} · ${rule.editionDisplayName} · rev. ${rule.sourceRevisionNumber}` })),
        element("span", { className: "rules-core-published-type", text: humanize(rule.entityType) }),
        element("span", { className: "rules-core-published-decision" },
            badge(rule.effectiveDecisionKind, rule.hasCampaignOverride ? "warning" : "secondary")));
}

async function renderRuleDetail(app, container, route) {
    clear(container);
    const scopeValue = validScope(app, route.scope);
    const campaignId = scopeValue.startsWith("campaign:") ? scopeValue.slice("campaign:".length) : null;
    const resolved = campaignId
        ? await app.api.getCampaignResolvedRule(campaignId, route.conceptKey)
        : await app.api.getGlobalResolvedRule(route.conceptKey);
    const baseline = campaignId ? await getOptionalCampaignBaseline(app, campaignId, route.conceptKey) : null;

    const backRoute = withQuery(catalogPath(resolved.entityType), {
        scope: scopeValue === "global" ? null : scopeValue
    });
    container.append(breadcrumbs(app, [
        ["Published Rules", "/published"],
        [humanizePlural(resolved.entityType), backRoute],
        [resolved.displayName, null]
    ]));
    container.append(element("header", { className: "rules-core-entry-header" },
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: campaignId ? "Effective Campaign Rule" : "Published Global Rule" }),
            element("h2", { text: resolved.displayName }),
            element("p", { className: "text-body-secondary", text: `${resolved.sourceEntityName} · ${resolved.editionDisplayName}` })),
        badge(resolved.effectiveDecisionKind ?? resolved.decisionKind, campaignId ? "warning" : "primary")));

    container.append(renderResolvedRule(resolved.entityType, resolved.document));

    if (campaignId) {
        const overlay = element("details", { className: "card card-body rules-core-provenance mt-4" });
        overlay.append(
            element("summary", { className: "fw-semibold", text: "Campaign overlay" }),
            element("div", { className: "pt-3" }, definitionList([
                ["Pinned global baseline", `#${resolved.baselineRulesetRevisionNumber}`],
                ["Global decision", `#${resolved.globalDecisionNumber} · ${resolved.globalDecisionKind}`],
                ["Campaign decision", resolved.campaignDecisionNumber ? `#${resolved.campaignDecisionNumber} · ${resolved.effectiveDecisionKind}` : "Inherited without campaign override"],
                ["Campaign note", resolved.campaignDecisionNote]
            ])));
        container.append(overlay);
        if (baseline) {
            const details = element("details", { className: "card card-body rules-core-provenance" });
            details.append(
                element("summary", { className: "fw-semibold", text: `Published global baseline #${baseline.baselineRulesetRevisionNumber}` }),
                element("div", { className: "pt-3" }, renderResolvedRule(baseline.entityType, baseline.document)));
            container.append(details);
        } else {
            container.append(alertNode("secondary", "The pinned global baseline is not available to this account under the independent source-access rules."));
        }
    }

    container.append(renderProvenance(resolved), rawDocumentDisclosure(resolved.document, "Resolved rule document"));
}

function renderProvenance(resolved) {
    const details = element("details", { className: "card card-body rules-core-provenance mt-4" });
    const contributions = resolved.contributions ?? resolved.globalContributions ?? [];
    const body = element("div", { className: "pt-3" }, definitionList([
        ["Selected source", `${resolved.sourceEntityName} · ${resolved.editionDisplayName} · rev. ${resolved.sourceRevisionNumber}`],
        ["Package", resolved.packageDisplayName],
        ["Decision note", resolved.decisionNote ?? resolved.campaignDecisionNote],
        ["Additional contributing sources", String(contributions.length)]
    ]));
    if (contributions.length) {
        const list = element("ul", { className: "mt-3 mb-0" });
        for (const contribution of contributions) {
            list.append(element("li", { text: `${contribution.sourceEntityName} · ${contribution.editionDisplayName} — ${contribution.contributionKind}${contribution.note ? `: ${contribution.note}` : ""}` }));
        }
        body.append(list);
    }
    details.append(element("summary", { className: "fw-semibold", text: "Rule provenance" }), body);
    return details;
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

function labeled(label, control, className = "") {
    return element("label", { className: `rules-core-control ${className}`.trim() },
        element("span", { text: label }), control);
}

function validScope(app, scope) {
    if (!scope || scope === "global") return "global";
    if (!scope.startsWith("campaign:")) return "global";
    const id = scope.slice("campaign:".length);
    return app.campaigns.some(value => String(value.id) === id) ? scope : "global";
}

function catalogPath(entityType) {
    for (const [segment, type] of ROUTE_FAMILIES.entries()) if (type === entityType) return `/${segment}`;
    return "/published";
}

function humanize(value) {
    return String(value ?? "")
        .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
        .replace(/[-_]+/g, " ")
        .replace(/^./, character => character.toUpperCase());
}

function humanizePlural(value) {
    const name = humanize(value);
    if (!name) return "Published Rules";
    if (name.endsWith("s")) return name;
    if (name.endsWith("y")) return `${name.slice(0, -1)}ies`;
    return `${name}s`;
}

async function getOptionalCampaignBaseline(app, campaignId, conceptKey) {
    try {
        return await app.api.backend(`/api/campaigns/${encodeURIComponent(campaignId)}/rules/${encodeURIComponent(conceptKey)}/global-baseline`);
    } catch (error) {
        if (error?.status === 404) return null;
        throw error;
    }
}
