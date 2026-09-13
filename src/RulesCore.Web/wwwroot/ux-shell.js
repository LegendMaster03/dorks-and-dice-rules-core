import { element, enhanceRenderedFragment } from "./ui.js";

const NAV_GROUPS = [
    {
        label: "Explore",
        items: [
            { label: "Rules Library", view: "library", route: "/library", capability: "canBrowseSourceLibrary", description: "Source publications" },
            { label: "Published Rules", view: "browse", route: "/published", capability: "canBrowseRules", description: "Table-ready rules" }
        ]
    },
    {
        label: "Curate",
        items: [
            { label: "Rules Lawyer", view: "global", capability: "canEditGlobal", description: "Adjudicate rules" },
            { label: "Cross-version", view: "version-review", capability: "canReviewVersions", description: "Compare editions" }
        ]
    },
    {
        label: "Campaign",
        items: [
            { label: "Campaign Rules", view: "campaign", capability: "canEditCampaign", description: "Overrides & baselines" }
        ]
    },
    {
        label: "Maintenance",
        collapsible: true,
        items: [
            { label: "Hosted Sources", view: "hosted-sources", capability: "canManageHostedSources", description: "Upstream definitions" },
            { label: "Source Admin", view: "source-admin", capability: "canAdministerSources", description: "Import & access" }
        ]
    }
];

const VIEW_LABELS = new Map(
    NAV_GROUPS.flatMap(group => group.items.map(item => [item.view, item.label])));

const VIEW_META = {
    global: {
        eyebrow: "Rules Layer",
        title: "Rules Lawyer",
        description: "Turn reviewed source material into explicit table rules. Decisions remain draft until you publish a global revision."
    },
    campaign: {
        eyebrow: "Campaign Layer",
        title: "Campaign Rules",
        description: "Choose the campaign baseline, review inherited rules, and publish only the overrides this campaign needs."
    },
    "hosted-sources": {
        eyebrow: "Maintenance",
        title: "Hosted Sources",
        description: "Maintain upstream source definitions and compare them with the locally available Source Layer."
    },
    "source-admin": {
        eyebrow: "Maintenance",
        title: "Source Administration",
        description: "Manage manual imports, source access, and acquisition records without changing published rules."
    }
};

const SECONDARY_RULES_LAWYER_TOOLS = new Map([
    ["Normalize imported sources", {
        title: "Normalize imported sources",
        description: "Review unbound source entities and create or reuse stable rule concepts."
    }],
    ["Create rule concept", {
        title: "Create a rule concept manually",
        description: "Use this when no imported source can establish the concept identity you need."
    }]
]);

const IN_PLACE_VIEW_METHODS = [
    "renderGlobalOverview",
    "renderGlobalConcept",
    "renderCampaignOverview",
    "renderCampaignConcept"
];

export function installRulesCoreUx(app) {
    installPresentationOrdering(app);
    app.renderHeader = () => renderWorkspaceHeader(app);
    app.renderNavigation = () => renderWorkspaceNavigation(app);
    app.presentRenderedFragment = container => enhanceRenderedFragment(container);
    app.presentRenderedView = container => enhanceRenderedView(app, container);
    installExplicitRenderLifecycle(app);
}

function installExplicitRenderLifecycle(app) {
    app.uxActiveRenderDepth = 0;

    for (const methodName of IN_PLACE_VIEW_METHODS) {
        wrapInPlaceViewRender(app, methodName);
    }

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        container.classList.add("rules-core-main");
        container.dataset.rulesView = app.activeView ?? "unknown";

        app.uxActiveRenderDepth += 1;
        let result;
        try {
            result = await renderActiveView(container);
        } finally {
            app.uxActiveRenderDepth -= 1;
        }

        if (app.uxActiveRenderDepth === 0) {
            app.presentRenderedView(container);
        }
        return result;
    };
}

function wrapInPlaceViewRender(app, methodName) {
    const render = app[methodName]?.bind(app);
    if (!render) return;

    app[methodName] = async (...args) => {
        const result = await render(...args);
        const container = args[0];
        if (container && app.uxActiveRenderDepth === 0) app.presentRenderedView(container);
        return result;
    };
}

function installPresentationOrdering(app) {
    const getGlobalOverview = app.api.getGlobalAuthoringOverview.bind(app.api);
    app.api.getGlobalAuthoringOverview = async () => {
        const overview = await getGlobalOverview();
        return { ...overview, concepts: [...(overview.concepts ?? [])].sort(compareGlobalConcepts) };
    };

    const getCampaignOverview = app.api.getCampaignAuthoringOverview.bind(app.api);
    app.api.getCampaignAuthoringOverview = async campaignId => {
        const overview = await getCampaignOverview(campaignId);
        return { ...overview, concepts: [...(overview.concepts ?? [])].sort(compareCampaignConcepts) };
    };
}

function compareGlobalConcepts(left, right) {
    const priority = concept => {
        if (!concept.latestDecisionId) return 0;
        if (concept.hasUnpublishedChanges) return 1;
        return 2;
    };
    return priority(left) - priority(right)
        || String(left.entityType ?? "").localeCompare(String(right.entityType ?? ""))
        || String(left.displayName ?? "").localeCompare(String(right.displayName ?? ""));
}

function compareCampaignConcepts(left, right) {
    const priority = concept => concept.hasUnpublishedOverrideChange ? 0 : 1;
    return priority(left) - priority(right)
        || String(left.displayName ?? "").localeCompare(String(right.displayName ?? ""));
}

function renderWorkspaceHeader(app) {
    const header = element("header", { className: "rules-core-topbar" });
    const brand = element("div", { className: "rules-core-brand" });
    const brandContext = app.hostContext.siteMode === "dorks-and-dice" ? "DORKS & DICE" : "RULES WORKSPACE";
    brand.append(
        element("div", { className: "rules-core-brand-mark", text: "R" }),
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: brandContext }),
            element("div", { className: "rules-core-brand-title", text: "Rules Core" })));

    const context = element("div", { className: "rules-core-context" });
    const viewLabel = VIEW_LABELS.get(app.activeView) ?? "Rules Core";
    context.append(
        element("div", { className: "rules-core-context-view", text: viewLabel }),
        element("div", { className: "rules-core-user-chip" },
            element("span", {
                className: `rules-core-user-dot ${app.session.user ? "is-authenticated" : "is-public"}`,
                attributes: { "aria-hidden": "true" }
            }),
            element("span", { text: app.session.user?.displayName ?? "Public access" })));

    header.append(brand, context);
    return header;
}

function renderWorkspaceNavigation(app) {
    const nav = element("nav", { className: "rules-core-nav-shell", ariaLabel: "Rules Core workspace" });
    const groups = element("div", { className: "rules-core-nav-groups" });

    for (const groupDefinition of NAV_GROUPS) {
        const available = groupDefinition.items.filter(item => app[item.capability]);
        if (!available.length) continue;

        const group = element("section", {
            className: `rules-core-nav-section${groupDefinition.collapsible ? " rules-core-nav-section-maintenance" : ""}`
        });
        group.append(element("div", { className: "rules-core-nav-label", text: groupDefinition.label }));
        const items = element("div", { className: "rules-core-nav-items" });
        for (const item of available) items.append(navItem(app, item));
        group.append(items);
        groups.append(group);
    }

    nav.append(groups);
    return nav;
}

function navItem(app, item) {
    const active = app.activeView === item.view;
    return element("button", {
        type: "button",
        className: `rules-core-nav-item${active ? " is-active" : ""}`,
        attributes: active ? { "aria-current": "page" } : {},
        onClick: async () => {
            if (item.route && app.navigateToolRoute) {
                await app.navigateToolRoute(item.route);
                return;
            }
            if (app.activeView === item.view) return;
            app.activeView = item.view;
            await app.render();
        }
    },
        element("span", { className: "rules-core-nav-item-label", text: item.label }),
        element("span", { className: "rules-core-nav-item-description", text: item.description }));
}

export function enhanceRenderedView(app, container) {
    enhanceRenderedFragment(container);

    if (VIEW_META[app.activeView] && !container.querySelector(":scope > .rules-core-generated-page-lead")) {
        container.prepend(pageLead(VIEW_META[app.activeView]));
    }

    if (app.activeView === "global") {
        updateRulesLawyerWorkflowCopy(container);
        wrapSecondaryRulesLawyerTools(container);
    }

    container.querySelectorAll(":scope > .card").forEach(card => card.classList.add("rules-core-panel"));
    enhanceRenderedFragment(container);
    return container;
}

function pageLead(meta) {
    return element("section", { className: "rules-core-generated-page-lead" },
        element("div", { className: "rules-core-eyebrow", text: meta.eyebrow }),
        element("h2", { text: meta.title }),
        element("p", { className: "text-body-secondary", text: meta.description }));
}

function updateRulesLawyerWorkflowCopy(container) {
    const workflow = container.querySelector(".rules-core-workflow");
    if (!workflow) return;
    const description = workflow.querySelector(".text-body-secondary.small");
    const copy = "Source material stays separate until you deliberately bind it. Unchanged editions may resolve automatically; publication remains explicit.";
    if (description && description.textContent !== copy) description.textContent = copy;
}

function wrapSecondaryRulesLawyerTools(container) {
    for (const [heading, metadata] of SECONDARY_RULES_LAWYER_TOOLS) {
        const card = findDirectCardByHeading(container, heading);
        if (!card || card.parentElement?.classList.contains("rules-core-tool-disclosure")) continue;

        const disclosure = element("details", { className: "rules-core-tool-disclosure" });
        const summary = element("summary", {},
            element("span", {}, element("strong", { text: metadata.title }), element("small", { text: metadata.description })),
            element("span", { className: "rules-core-disclosure-cue", text: "Open" }));
        card.classList.remove("mb-3");
        card.parentNode.insertBefore(disclosure, card);
        disclosure.append(summary, card);
        disclosure.addEventListener("toggle", () => {
            const cue = disclosure.querySelector(".rules-core-disclosure-cue");
            if (cue) cue.textContent = disclosure.open ? "Close" : "Open";
        });
    }
}

function findDirectCardByHeading(container, heading) {
    return Array.from(container.children).find(child => {
        if (!child.classList?.contains("card")) return false;
        const title = child.querySelector("h3, h4, h5");
        return title?.textContent?.trim() === heading;
    }) ?? null;
}
