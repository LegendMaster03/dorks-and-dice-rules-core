import {
    alertNode,
    badge,
    clear,
    codeBlock,
    definitionList,
    describeError,
    element,
    formatDate,
    setButtonBusy
} from "./ui.js";

const DORKS_MODE = "dorks-and-dice";

export function installResolvedRulesBrowser(app) {
    app.canBrowseRules = app.hostContext.siteMode === DORKS_MODE;
    app.browserScope = "global";

    if (app.canBrowseRules && app.activeView === "none") {
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
        element("h3", { className: "h5 mb-1", text: "Rules browser" }),
        element("p", {
            className: "text-body-secondary mb-0",
            text: "Browse published rules that this account may access. Campaign views show the last published campaign ruleset, not unpublished DM work."
        }));
    container.append(heading);

    const controls = element("div", { className: "card card-body mb-3" });
    const row = element("div", { className: "row g-2 align-items-end" });

    const scopeColumn = element("div", { className: "col-lg-3" });
    scopeColumn.append(element("label", {
        className: "form-label fw-semibold",
        text: "Ruleset"
    }));
    const scope = element("select", { className: "form-select" });
    scope.append(element("option", { value: "global", text: "Global Dorks & Dice rules" }));
    for (const campaign of app.campaigns) {
        scope.append(element("option", {
            value: `campaign:${campaign.id}`,
            text: campaign.name ?? `Campaign ${campaign.id}`
        }));
    }
    scope.value = app.browserScope;
    scopeColumn.append(scope);

    const type = inputGroup("Entity type", "spell, skill, class…", "col-lg-2");
    const query = inputGroup("Search", "Rule, source, package, edition…", "col-lg-5");
    const actionColumn = element("div", { className: "col-lg-2 d-grid" });
    const search = element("button", {
        type: "button",
        className: "btn btn-primary",
        text: "Browse"
    });
    actionColumn.append(search);
    row.append(scopeColumn, type.group, query.group, actionColumn);
    controls.append(row);
    container.append(controls);

    const status = element("div");
    const results = element("div");
    container.append(status, results);

    const load = async () => {
        status.replaceChildren();
        results.replaceChildren();
        app.browserScope = scope.value;
        setButtonBusy(search, true, "Loading…");
        try {
            const filters = {
                entityType: type.input.value.trim() || null,
                query: query.input.value.trim() || null,
                limit: 200
            };
            const catalog = scope.value === "global"
                ? await app.api.getGlobalRulesCatalog(filters)
                : await app.api.getCampaignRulesCatalog(
                    scope.value.slice("campaign:".length),
                    filters);
            renderCatalog(app, container, results, catalog, scope.value);
        } catch (error) {
            status.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(search, false);
        }
    };

    scope.addEventListener("change", load);
    search.addEventListener("click", load);
    for (const input of [type.input, query.input]) {
        input.addEventListener("keydown", event => {
            if (event.key === "Enter") {
                event.preventDefault();
                load();
            }
        });
    }

    await load();
}

function renderCatalog(app, container, results, catalog, scopeValue) {
    results.replaceChildren();

    const summary = element("div", { className: "card card-body mb-3" });
    const revisionLabel = catalog.revisionNumber
        ? `#${catalog.revisionNumber}`
        : "None";
    summary.append(definitionList([
        ["Published revision", revisionLabel],
        ["Published", formatDate(catalog.publishedAt)],
        ["Accessible rules", String(catalog.rules?.length ?? 0)]
    ]));
    results.append(summary);

    if (!catalog.revisionNumber) {
        results.append(alertNode(
            "secondary",
            catalog.scope === "campaign"
                ? "This campaign has no published ruleset yet."
                : "No global ruleset has been published yet."));
        return;
    }

    if (!catalog.rules?.length) {
        results.append(alertNode(
            "secondary",
            "No published rules accessible to this account match the current filters."));
        return;
    }

    const table = element("table", { className: "table table-hover align-middle mb-0" });
    const head = element("thead");
    const headRow = element("tr");
    for (const label of ["Rule", "Type", "Source", "Effective decision", ""]) {
        headRow.append(element("th", { text: label }));
    }
    head.append(headRow);

    const body = element("tbody");
    for (const rule of catalog.rules) {
        const row = element("tr");
        const name = element("td");
        name.append(
            element("div", { className: "fw-semibold", text: rule.displayName }),
            element("div", {
                className: "small text-body-secondary font-monospace",
                text: rule.conceptKey
            }));
        row.append(name);
        row.append(element("td", { text: rule.entityType }));

        const source = element("td");
        source.append(
            element("div", { text: rule.sourceEntityName }),
            element("div", {
                className: "small text-body-secondary",
                text: `${rule.packageDisplayName} · ${rule.editionDisplayName} · rev. ${rule.sourceRevisionNumber}`
            }));
        row.append(source);

        const decision = element("td");
        decision.append(badge(
            rule.effectiveDecisionKind,
            rule.hasCampaignOverride ? "warning" : "secondary"));
        if (rule.hasCampaignOverride) {
            decision.append(element("div", {
                className: "small text-body-secondary mt-1",
                text: "Campaign override"
            }));
        }
        row.append(decision);

        const action = element("td", { className: "text-end" });
        action.append(element("button", {
            type: "button",
            className: "btn btn-sm btn-outline-primary",
            text: "Open",
            onClick: async () => renderRuleDetail(app, container, rule, scopeValue)
        }));
        row.append(action);
        body.append(row);
    }

    table.append(head, body);
    results.append(element("div", { className: "card" },
        element("div", { className: "table-responsive" }, table)));
}

async function renderRuleDetail(app, container, summary, scopeValue) {
    clear(container);
    container.append(element("button", {
        type: "button",
        className: "btn btn-outline-secondary mb-3",
        text: "Back to rules",
        onClick: async () => renderRulesBrowser(app, container)
    }));

    const loading = element("div", {
        className: "card card-body text-body-secondary",
        text: "Loading resolved rule…"
    });
    container.append(loading);

    try {
        const resolved = scopeValue === "global"
            ? await app.api.getResolvedGlobalRule(summary.conceptKey)
            : await app.api.getResolvedCampaignRule(
                scopeValue.slice("campaign:".length),
                summary.conceptKey);
        loading.remove();

        const metadata = element("div", { className: "card card-body mb-3" });
        metadata.append(
            element("div", {
                className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3"
            },
            element("div", {},
                element("h3", { className: "h4 mb-1", text: resolved.displayName }),
                element("div", {
                    className: "text-body-secondary font-monospace small",
                    text: resolved.conceptKey
                })),
            badge(resolved.entityType, "primary")),
            definitionList([
                ["Ruleset revision", `#${resolved.campaignRulesetRevisionNumber ?? resolved.rulesetRevisionNumber}`],
                ["Decision", resolved.effectiveDecisionKind ?? resolved.decisionKind],
                ["Source", `${resolved.sourceEntityName} (${resolved.sourceCode})`],
                ["Package", resolved.packageDisplayName],
                ["Edition", resolved.editionDisplayName],
                ["Source revision", `#${resolved.sourceRevisionNumber}`]
            ]));
        container.append(metadata);

        const documentCard = element("div", { className: "card card-body" });
        documentCard.append(
            element("h4", { className: "h5", text: "Resolved rule document" }),
            element("p", {
                className: "small text-body-secondary",
                text: "This is the effective normalized document after published global and campaign rule operations."
            }),
            codeBlock(resolved.document));
        container.append(documentCard);
    } catch (error) {
        loading.remove();
        container.append(alertNode("danger", describeError(error)));
    }
}

function inputGroup(label, placeholder, columnClass) {
    const input = element("input", {
        className: "form-control",
        type: "text",
        placeholder
    });
    const group = element("div", { className: columnClass });
    group.append(
        element("label", { className: "form-label fw-semibold", text: label }),
        input);
    return { group, input };
}
