import { element } from "./ui.js";

const NAV_GROUPS = [
    {
        label: "Explore",
        items: [
            { label: "Library", view: "library", capability: "canBrowseSourceLibrary", description: "Source material" },
            { label: "Published Rules", view: "browse", capability: "canBrowseRules", description: "Table-ready rules" }
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

export function installRulesCoreUx(app) {
    app.renderHeader = () => renderWorkspaceHeader(app);
    app.renderNavigation = () => renderWorkspaceNavigation(app);

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        container.classList.add("rules-core-main");
        container.dataset.rulesView = app.activeView ?? "unknown";
        await renderActiveView(container);
        enhanceRenderedView(app, container);
    };
}

function renderWorkspaceHeader(app) {
    const header = element("header", { className: "rules-core-topbar" });
    const brand = element("div", { className: "rules-core-brand" });
    brand.append(
        element("div", { className: "rules-core-brand-mark", text: "R" }),
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: "DORKS & DICE" }),
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
    const nav = element("nav", {
        className: "rules-core-nav-shell",
        ariaLabel: "Rules Core workspace"
    });
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
            if (app.activeView === item.view) return;
            app.activeView = item.view;
            await app.render();
        }
    },
        element("span", { className: "rules-core-nav-item-label", text: item.label }),
        element("span", { className: "rules-core-nav-item-description", text: item.description }));
}

function enhanceRenderedView(app, container) {
    container.querySelectorAll(":scope > .card").forEach(card => {
        card.classList.add("rules-core-panel");
    });

    const firstCard = container.querySelector(":scope > .card");
    if (firstCard && ["library", "browse", "version-review"].includes(app.activeView)) {
        firstCard.classList.add("rules-core-page-lead");
    }

    container.querySelectorAll(".table-responsive").forEach(tableWrap => {
        tableWrap.classList.add("rules-core-table-wrap");
    });
    container.querySelectorAll("table").forEach(table => {
        table.classList.add("rules-core-table");
    });
    container.querySelectorAll(".list-group").forEach(list => {
        list.classList.add("rules-core-list");
    });
    container.querySelectorAll("form").forEach(form => {
        form.classList.add("rules-core-form");
    });
    container.querySelectorAll(".alert").forEach(alert => {
        alert.classList.add("rules-core-alert");
    });
}
