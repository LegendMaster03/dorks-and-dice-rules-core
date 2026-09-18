import { element, enhanceRenderedFragment } from "./ui.js";
import { RULE_FAMILY_TABS } from "./rules-browser.js";

const WORKSPACE_NAV_ITEMS = [
    { label: "Sources", view: "sources", capability: "canBrowseSourceLibrary" },
    { label: "Rules Lawyer", view: "global", capability: "canEditGlobal" },
    { label: "Cross-version", view: "version-review", capability: "canReviewVersions" },
    { label: "Campaign Rules", view: "campaign", capability: "canEditCampaign" },
    { label: "Hosted Sources", view: "hosted-sources", capability: "canManageHostedSources", maintenance: true },
    { label: "Source Admin", view: "source-admin", capability: "canAdministerSources", maintenance: true }
];

const VIEW_LABELS = new Map(
    WORKSPACE_NAV_ITEMS.map(item => [item.view, item.label]));

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
    if (!render) {
        return;
    }

    app[methodName] = async (...args) => {
        const result = await render(...args);
        const container = args[0];
        if (container && app.uxActiveRenderDepth === 0) {
            app.presentRenderedView(container);
        }
        return result;
    };
}

function installPresentationOrdering(app) {
    const getGlobalOverview = app.api.getGlobalAuthoringOverview.bind(app.api);
    app.api.getGlobalAuthoringOverview = async () => {
        const overview = await getGlobalOverview();
        return {
            ...overview,
            concepts: [...(overview.concepts ?? [])].sort(compareGlobalConcepts)
        };
    };

    const getCampaignOverview = app.api.getCampaignAuthoringOverview.bind(app.api);
    app.api.getCampaignAuthoringOverview = async campaignId => {
        const overview = await getCampaignOverview(campaignId);
        return {
            ...overview,
            concepts: [...(overview.concepts ?? [])].sort(compareCampaignConcepts)
        };
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
    const brandContext = app.hostContext.siteMode === "dorks-and-dice"
        ? "DORKS & DICE"
        : "RULES WORKSPACE";
    brand.append(
        element("div", { className: "rules-core-brand-mark", text: "R" }),
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: brandContext }),
            element("div", { className: "rules-core-brand-title", text: "Rules Core" })));

    const context = element("div", { className: "rules-core-context" });
    const viewLabel = app.activeView === "library"
        ? ruleFamilyLabel(app.browserFilters?.entityType)
        : VIEW_LABELS.get(app.activeView) ?? "Rules Core";
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
    const nav = element("nav", {
        className: "rules-core-nav-shell",
        ariaLabel: "Rules Core"
    });

    const primary = element("div", {
        className: "rules-core-primary-nav",
        role: "navigation",
        ariaLabel: "Rules content"
    });
    if (app.canBrowseRules) {
        for (const [entityType, label] of RULE_FAMILY_TABS) {
            primary.append(ruleFamilyNavItem(app, entityType, label));
        }
    }

    const utilities = element("div", {
        className: "rules-core-utility-nav",
        role: "navigation",
        ariaLabel: "Rules Core tools"
    });
    for (const item of WORKSPACE_NAV_ITEMS) {
        if (!app[item.capability]) continue;
        utilities.append(workspaceNavItem(app, item));
    }

    nav.append(primary, utilities);
    return nav;
}

function ruleFamilyNavItem(app, entityType, label) {
    const active = app.activeView === "library"
        && (app.browserFilters?.entityType ?? "") === entityType;
    return element("button", {
        type: "button",
        className: `rules-core-primary-tab${active ? " is-active" : ""}`,
        text: label,
        attributes: active ? { "aria-current": "page" } : {},
        onClick: async () => {
            if (active) return;
            await app.navigateRuleFamily?.(entityType);
        }
    });
}

function workspaceNavItem(app, item) {
    const active = app.activeView === item.view;
    return element("button", {
        type: "button",
        className: `rules-core-utility-item${item.maintenance ? " is-maintenance" : ""}${active ? " is-active" : ""}`,
        text: item.label,
        attributes: active ? { "aria-current": "page" } : {},
        onClick: async () => {
            if (active) return;
            const navigate = app.viewNavigation?.[item.view];
            if (navigate) {
                await navigate();
                return;
            }
            app.activeView = item.view;
            await app.render();
        }
    });
}

function ruleFamilyLabel(entityType) {
    const normalized = entityType ?? "";
    const known = RULE_FAMILY_TABS.find(([value]) => value === normalized);
    if (known) return known[1];
    if (!normalized) return "Rules";
    return normalized
        .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
        .replace(/[-_]+/g, " ")
        .replace(/\b\w/g, value => value.toUpperCase());
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
    if (app.activeView === "campaign") {
        addCampaignSiteLink(app, container);
    }

    container.querySelectorAll(":scope > .card").forEach(card => {
        card.classList.add("rules-core-panel");
    });

    const firstCard = container.querySelector(":scope > .card");
    if (firstCard && ["sources", "version-review"].includes(app.activeView)) {
        firstCard.classList.add("rules-core-page-lead");
    }

    enhanceRenderedFragment(container);
    return container;
}

function pageLead(meta) {
    return element("section", { className: "rules-core-generated-page-lead" },
        element("div", { className: "rules-core-eyebrow", text: meta.eyebrow }),
        element("h2", { text: meta.title }),
        element("p", { className: "text-body-secondary", text: meta.description }));
}

function addCampaignSiteLink(app, container) {
    if (app.hostContext?.siteMode !== "dorks-and-dice") return;
    const lead = container.querySelector(":scope > .rules-core-generated-page-lead");
    if (!lead || lead.querySelector(".rules-core-campaign-site-link")) return;

    const campaignId = app.activeCampaignId;
    const href = campaignId
        ? `/campaigns/${encodeURIComponent(campaignId)}`
        : "/campaigns";
    lead.append(element("a", {
        className: "btn btn-sm btn-outline-primary rules-core-campaign-site-link",
        text: campaignId ? "Open campaign" : "Campaigns",
        attributes: { href, target: "_top" }
    }));
}

function updateRulesLawyerWorkflowCopy(container) {
    const workflow = container.querySelector(".rules-core-workflow");
    if (!workflow) return;
    const description = workflow.querySelector(".text-body-secondary.small");
    const copy = "Source material stays separate until you deliberately bind it. Unchanged editions may resolve automatically; publication remains explicit.";
    if (description && description.textContent !== copy) {
        description.textContent = copy;
    }
}

function wrapSecondaryRulesLawyerTools(container) {
    for (const [heading, metadata] of SECONDARY_RULES_LAWYER_TOOLS) {
        const card = findDirectCardByHeading(container, heading);
        if (!card || card.parentElement?.classList.contains("rules-core-tool-disclosure")) continue;

        const disclosure = element("details", { className: "rules-core-tool-disclosure" });
        const summary = element("summary", {},
            element("span", {},
                element("strong", { text: metadata.title }),
                element("small", { text: metadata.description })),
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
