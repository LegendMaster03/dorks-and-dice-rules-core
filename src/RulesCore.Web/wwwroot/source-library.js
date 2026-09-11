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
const BUILT_IN_PREFIX = "builtin-wotc-srd-";
const SOURCE_LIMIT = 200;
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
    app.libraryFilters = { entityType: "", query: "" };
    app.libraryNotice = null;

    if (app.canBrowseSourceLibrary) {
        app.activeView = "library";
    }

    app.renderHeader = () => renderHeader(app);
    app.renderNavigation = () => renderNavigation(app);

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        if (app.activeView === "library") {
            await renderSourceLibrary(app, container);
            return;
        }

        await renderActiveView(container);
        if (app.activeView === "global" && app.canEditGlobal) {
            prependRulesLawyerWorkflow(app, container);
        }
    };
}

function renderHeader(app) {
    const card = element("div", { className: "card card-body rules-core-header" });
    const row = element("div", {
        className: "d-flex flex-wrap align-items-start justify-content-between gap-3"
    });
    row.append(
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: "DORKS & DICE RULES" }),
            element("h2", { className: "h3 mb-1", text: "Rules Core" }),
            element("p", {
                className: "text-body-secondary mb-0",
                text: "Explore source material, review cross-edition rules, and publish the rules your table actually uses."
            })),
        element("div", { className: "text-end small" },
            element("div", {
                className: "fw-semibold",
                text: app.session.user?.displayName ?? "Guest"
            }),
            element("div", {
                className: "text-body-secondary",
                text: app.session.user?.displayName ? "Signed in" : "Public library access"
            })));
    card.append(row);
    return card;
}

function renderNavigation(app) {
    const shell = element("div", { className: "rules-core-nav-shell" });
    const primary = element("div", { className: "rules-core-nav-primary" });

    if (app.canBrowseSourceLibrary) primary.append(app.navButton("Library", "library"));
    if (app.canBrowseRules) primary.append(app.navButton("Published Rules", "browse"));
    if (app.canEditGlobal) primary.append(app.navButton("Rules Lawyer", "global"));
    if (app.canReviewVersions) primary.append(app.navButton("Cross-version Review", "version-review"));
    if (app.canEditCampaign) primary.append(app.navButton("Campaign Rules", "campaign"));

    shell.append(primary);

    if (app.canManageHostedSources || app.canAdministerSources) {
        const advanced = element("details", { className: "rules-core-advanced-nav" });
        const summary = element("summary", { text: "Advanced" });
        const actions = element("div", { className: "rules-core-advanced-actions" });
        if (app.canManageHostedSources) {
            actions.append(advancedNavButton(app, "Hosted source definitions", "hosted-sources"));
        }
        if (app.canAdministerSources) {
            actions.append(advancedNavButton(app, "Manual source import & access", "source-admin"));
        }
        advanced.append(summary, actions);
        shell.append(advanced);
    }

    return shell;
}

function advancedNavButton(app, label, view) {
    const button = app.navButton(label, view);
    button.classList.add("w-100", "text-start");
    return button;
}

async function renderSourceLibrary(app, container) {
    clear(container);

    if (app.libraryNotice) {
        container.append(alertNode(app.libraryNotice.kind, app.libraryNotice.message));
        app.libraryNotice = null;
    }

    const intro = element("div", { className: "card card-body mb-3 rules-core-library-hero" },
        element("div", { className: "d-flex flex-wrap justify-content-between align-items-start gap-3" },
            element("div", {},
                element("h3", { className: "h4 mb-1", text: "Rules Library" }),
                element("p", {
                    className: "text-body-secondary mb-0",
                    text: "Source material lives here before Rules Lawyer adjudication. Importing an SRD does not automatically make it the table rule; it makes the source available for browsing, normalization, and review."
                })),
            badge("Source Layer", "primary")));
    container.append(intro);

    let builtIns = [];
    if (app.canManageHostedSources) {
        try {
            builtIns = (await app.api.getHostedSources(true))
                .filter(value => value.key?.startsWith(BUILT_IN_PREFIX))
                .sort(compareBuiltIns);
        } catch (error) {
            container.append(alertNode("warning", `Built-in source status could not load: ${describeError(error)}`));
        }
    }

    if (app.canManageHostedSources) {
        await renderBuiltInSources(app, container, builtIns);
    } else {
        container.append(element("div", { className: "card card-body mb-3" },
            element("h4", { className: "h5 mb-1", text: "Public source library" }),
            element("p", {
                className: "text-body-secondary mb-0",
                text: "Browse source material that has already been imported. Rules Lawyer accounts can also import or refresh the built-in SRDs from this page."
            })));
    }

    await renderSourceBrowser(app, container);
}

async function renderBuiltInSources(app, container, definitions) {
    const section = element("section", { className: "mb-3" });
    const heading = element("div", {
        className: "d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2"
    });
    heading.append(
        element("div", {},
            element("h4", { className: "h5 mb-1", text: "Built-in SRDs" }),
            element("p", {
                className: "text-body-secondary small mb-0",
                text: "These are canonical acquisition definitions. Ready means source entities are present locally; the application never depends on the remote host at runtime."
            })));
    section.append(heading);

    if (!definitions.length) {
        section.append(alertNode(
            "warning",
            "No built-in SRD definitions are registered. On a current deployment this usually indicates baseline bootstrap has not run successfully."));
        container.append(section);
        return;
    }

    const stateHolder = element("div", { className: "rules-core-source-grid" });
    section.append(stateHolder);
    container.append(section);

    const states = await Promise.all(definitions.map(definition => loadDefinitionState(app, definition)));
    const readyCount = states.filter(value => value.ready).length;
    const detectedMonsterCount = states.reduce((sum, value) => sum + value.monsterCount, 0);
    const monsterCountCapped = states.some(value => value.monsterCapped);

    const controls = element("div", { className: "rules-core-library-summary card card-body mb-3" });
    const importAll = element("button", {
        type: "button",
        className: "btn btn-primary",
        text: readyCount ? "Refresh all built-in SRDs" : "Import all built-in SRDs"
    });
    importAll.addEventListener("click", async () => refreshAllBuiltIns(app, container, definitions, importAll));
    controls.append(
        element("div", { className: "rules-core-metrics" },
            metric("Built-in SRDs", String(definitions.length)),
            metric("Ready locally", `${readyCount}/${definitions.length}`),
            metric("Detected monsters", `${detectedMonsterCount}${monsterCountCapped ? "+" : ""}`)),
        element("div", { className: "d-flex flex-wrap gap-2 align-items-center" },
            importAll,
            element("span", {
                className: "small text-body-secondary",
                text: "For a quick Block Initiative beta, importing SRD 5.1 or 5.2.1 first is enough to exercise structured monster records."
            })));
    section.insertBefore(controls, stateHolder);

    clear(stateHolder);
    for (const state of states) {
        stateHolder.append(renderSourceCard(app, container, state));
    }
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
        const latestImportedAt = exactEntities
            .map(value => value.latestImportedAt)
            .filter(Boolean)
            .sort()
            .at(-1) ?? null;
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
        return {
            definition,
            sourceCode,
            entityCount: 0,
            entityCapped: false,
            monsterCount: 0,
            monsterCapped: false,
            latestImportedAt: null,
            ready: false,
            error
        };
    }
}

function renderSourceCard(app, container, state) {
    const definition = state.definition;
    const card = element("article", { className: "card card-body rules-core-source-card" });
    const titleRow = element("div", {
        className: "d-flex justify-content-between align-items-start gap-2 mb-3"
    });
    titleRow.append(
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: definition.gameEdition ?? "D&D" }),
            element("h5", { className: "h5 mb-1", text: definition.editionDisplayName }),
            element("div", { className: "small text-body-secondary", text: definition.workDisplayName })),
        state.error
            ? badge("Status error", "danger")
            : !definition.isEnabled
                ? badge("Disabled", "secondary")
                : state.ready
                    ? badge("Ready", "success")
                    : badge("Not imported", "warning"));
    card.append(titleRow);

    card.append(element("div", { className: "rules-core-source-card-stats" },
        metric("Entities", countLabel(state.entityCount, state.entityCapped)),
        metric("Monsters", countLabel(state.monsterCount, state.monsterCapped)),
        metric("Last import", state.latestImportedAt ? formatDate(state.latestImportedAt) : "Never")));

    if (state.error) {
        card.append(alertNode("warning", describeError(state.error)));
    }

    const actions = element("div", { className: "d-flex flex-wrap gap-2 mt-3" });
    const browse = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-primary",
        text: "Browse source"
    });
    browse.disabled = !state.ready;
    browse.addEventListener("click", async () => {
        app.libraryFilters = { entityType: "", query: state.sourceCode };
        await renderSourceLibrary(app, container);
        document.getElementById("rules-core-source-browser")?.scrollIntoView({ behavior: "smooth", block: "start" });
    });

    const monsters = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-primary",
        text: "Monsters"
    });
    monsters.disabled = !state.monsterCount;
    monsters.addEventListener("click", async () => {
        app.libraryFilters = { entityType: "monster", query: state.sourceCode };
        await renderSourceLibrary(app, container);
        document.getElementById("rules-core-source-browser")?.scrollIntoView({ behavior: "smooth", block: "start" });
    });

    const preview = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary",
        text: "Preview live"
    });
    preview.disabled = !definition.isEnabled;
    preview.addEventListener("click", async () => previewDefinition(app, container, definition, preview));

    const refresh = element("button", {
        type: "button",
        className: "btn btn-sm btn-primary",
        text: state.ready ? "Refresh" : "Import"
    });
    refresh.disabled = !definition.isEnabled;
    refresh.addEventListener("click", async () => refreshDefinition(app, container, definition, refresh));

    actions.append(browse, monsters, preview, refresh);
    card.append(actions);

    const details = element("details", { className: "mt-3 small" });
    details.append(
        element("summary", { className: "text-body-secondary", text: "Source and provenance details" }),
        element("div", { className: "pt-2" },
            definitionList([
                ["Source code", state.sourceCode],
                ["Package", definition.packageDisplayName],
                ["Provider", definition.provider],
                ["License", definition.license ?? "—"],
                ["Acquisition format", definition.formatKind],
                ["Definition revision", `#${definition.revisionNumber}`]
            ]),
            element("p", { className: "text-body-secondary mb-0", text: definition.note ?? "" })));
    card.append(details);
    return card;
}

async function previewDefinition(app, container, definition, button) {
    setButtonBusy(button, true, "Previewing…");
    try {
        const response = await app.api.previewHostedSource(definition.id);
        app.libraryNotice = {
            kind: response.preview.canImport ? "success" : "warning",
            message: `${definition.editionDisplayName}: ${response.documents.length} remote document(s), ${response.preview.entityCount} detected entities, ${response.preview.newEntityCount} new, ${response.preview.newRevisionCount} changed, ${response.preview.unchangedCount} unchanged.`
        };
    } catch (error) {
        app.libraryNotice = { kind: "danger", message: `${definition.editionDisplayName}: ${describeError(error)}` };
    } finally {
        setButtonBusy(button, false);
    }
    await renderSourceLibrary(app, container);
}

async function refreshDefinition(app, container, definition, button) {
    setButtonBusy(button, true, "Importing…");
    try {
        const response = await app.api.refreshHostedSource(definition.id);
        const created = response.import.entities.filter(value => value.createdRevision).length;
        const monsters = response.import.entities.filter(value => value.entityType === "monster").length;
        app.libraryNotice = {
            kind: "success",
            message: `${definition.editionDisplayName}: processed ${response.import.entities.length} entities from ${response.documents.length} document(s); ${created} immutable revision(s) created and ${monsters} monster record(s) detected in this refresh.`
        };
    } catch (error) {
        app.libraryNotice = { kind: "danger", message: `${definition.editionDisplayName}: ${describeError(error)}` };
    } finally {
        setButtonBusy(button, false);
    }
    await renderSourceLibrary(app, container);
}

async function refreshAllBuiltIns(app, container, definitions, button) {
    setButtonBusy(button, true, "Importing SRDs…");
    const failures = [];
    let processed = 0;
    let monsters = 0;
    try {
        for (const definition of definitions.filter(value => value.isEnabled)) {
            button.textContent = `Importing ${definition.editionDisplayName}…`;
            try {
                const response = await app.api.refreshHostedSource(definition.id);
                processed += response.import.entities.length;
                monsters += response.import.entities.filter(value => value.entityType === "monster").length;
            } catch (error) {
                failures.push(`${definition.editionDisplayName}: ${describeError(error)}`);
            }
        }
        app.libraryNotice = failures.length
            ? {
                kind: "warning",
                message: `Built-in SRD refresh finished with ${failures.length} failure(s). ${processed} entities were processed and ${monsters} monster records were detected. ${failures.join(" ")}`
            }
            : {
                kind: "success",
                message: `Built-in SRD refresh complete: ${processed} entities processed and ${monsters} monster records detected.`
            };
    } finally {
        setButtonBusy(button, false);
    }
    await renderSourceLibrary(app, container);
}

async function renderSourceBrowser(app, container) {
    const section = element("section", {
        id: "rules-core-source-browser",
        className: "card card-body"
    });
    section.append(
        element("div", { className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3" },
            element("div", {},
                element("h4", { className: "h5 mb-1", text: "Browse imported source material" }),
                element("p", {
                    className: "text-body-secondary mb-0",
                    text: "This is the immutable Source Layer, not the published ruleset. Monster records can be inspected here before any cross-edition adjudication."
                })),
            badge("Beta integration surface", "secondary")));

    const form = element("form", { className: "row g-2 align-items-end mb-3" });
    const type = element("select", { className: "form-select" });
    for (const [value, label] of ENTITY_TYPES) {
        const option = element("option", { value, text: label });
        option.selected = value === app.libraryFilters.entityType;
        type.append(option);
    }
    const query = element("input", {
        type: "search",
        className: "form-control",
        placeholder: "Name, source code, publication, or edition",
        value: app.libraryFilters.query
    });
    const searchButton = element("button", { type: "submit", className: "btn btn-primary w-100", text: "Search" });
    const monstersButton = element("button", { type: "button", className: "btn btn-outline-primary w-100", text: "Monster beta" });

    form.append(
        field("Entity type", type, "col-lg-3"),
        field("Search", query, "col-lg-6"),
        element("div", { className: "col-lg-2" }, searchButton),
        element("div", { className: "col-lg-1" }, monstersButton));
    section.append(form);

    const results = element("div");
    section.append(results);
    container.append(section);

    form.addEventListener("submit", async event => {
        event.preventDefault();
        app.libraryFilters = { entityType: type.value, query: query.value.trim() };
        await renderBrowserResults(app, results);
    });
    monstersButton.addEventListener("click", async () => {
        type.value = "monster";
        app.libraryFilters = { entityType: "monster", query: query.value.trim() };
        await renderBrowserResults(app, results);
    });

    await renderBrowserResults(app, results);
}

async function renderBrowserResults(app, container) {
    clear(container);
    container.append(element("div", { className: "text-body-secondary", text: "Loading source entities…" }));
    try {
        const entities = await app.api.searchSourceEntities({
            entityType: app.libraryFilters.entityType || null,
            query: app.libraryFilters.query || null,
            limit: 100
        });
        clear(container);
        if (!entities.length) {
            container.append(alertNode(
                "secondary",
                app.libraryFilters.entityType === "monster"
                    ? "No imported monster entities match these filters. Import an SRD above, then search again."
                    : "No imported source entities match these filters."));
            return;
        }

        const table = element("table", { className: "table table-hover align-middle mb-0" });
        const head = element("thead", {}, element("tr", {},
            element("th", { text: "Source entity" }),
            element("th", { text: "Type" }),
            element("th", { text: "Publication" }),
            element("th", { text: "Revision" }),
            element("th")));
        const body = element("tbody");
        for (const entity of entities) {
            const open = element("button", {
                type: "button",
                className: "btn btn-sm btn-outline-primary",
                text: entity.entityType === "monster" ? "Inspect stats" : "Open"
            });
            open.addEventListener("click", async () => renderSourceEntityDetail(app, container, entity.entityId));
            body.append(element("tr", {},
                element("td", {},
                    element("div", { className: "fw-semibold", text: entity.name }),
                    element("div", { className: "small text-body-secondary", text: entity.sourceCode })),
                element("td", {}, entity.entityType === "monster"
                    ? badge("monster", "primary")
                    : badge(entity.entityType, "secondary")),
                element("td", {},
                    element("div", { text: entity.editionDisplayName }),
                    element("div", { className: "small text-body-secondary", text: entity.workDisplayName })),
                element("td", { text: `#${entity.latestRevisionNumber}` }),
                element("td", { className: "text-end" }, open)));
        }
        table.append(head, body);
        container.append(
            element("div", { className: "small text-body-secondary mb-2", text: `${entities.length}${entities.length === 100 ? "+" : ""} result(s)` }),
            element("div", { className: "table-responsive" }, table));
    } catch (error) {
        clear(container);
        container.append(alertNode("danger", describeError(error)));
    }
}

async function renderSourceEntityDetail(app, container, entityId) {
    clear(container);
    container.append(element("div", { className: "text-body-secondary", text: "Loading source entity…" }));
    try {
        const entity = await app.api.backend(`/api/sources/entities/${encodeURIComponent(entityId)}`);
        clear(container);
        const back = element("button", { type: "button", className: "btn btn-sm btn-outline-secondary mb-3", text: "← Back to source results" });
        back.addEventListener("click", async () => renderBrowserResults(app, container));
        container.append(back);

        const header = element("div", { className: "d-flex flex-wrap justify-content-between gap-3 mb-3" },
            element("div", {},
                element("h5", { className: "h4 mb-1", text: entity.name }),
                element("div", { className: "text-body-secondary", text: `${entity.editionDisplayName} · ${entity.sourceCode}` })),
            entity.entityType === "monster" ? badge("Monster detected", "success") : badge(entity.entityType, "secondary"));
        container.append(header);

        if (entity.entityType === "monster") {
            container.append(renderMonsterPanel(entity));
        }

        const provenance = element("div", { className: "card card-body mb-3" },
            element("h6", { className: "h6 mb-2", text: "Source provenance" }),
            definitionList([
                ["Entity ID", entity.entityId],
                ["Package", `${entity.packageDisplayName} (${entity.packageKey})`],
                ["Work", `${entity.workDisplayName} (${entity.workKey})`],
                ["Release", `${entity.editionDisplayName} (${entity.editionKey})`],
                ["Revision", `#${entity.revisionNumber}`],
                ["Imported", formatDate(entity.importedAt)]
            ]));
        container.append(provenance);

        const raw = element("details", { className: "card card-body" });
        raw.append(
            element("summary", { className: "fw-semibold", text: "Raw immutable source document" }),
            element("div", { className: "pt-3" }, codeBlock(entity.document)));
        container.append(raw);
    } catch (error) {
        clear(container);
        container.append(alertNode("danger", describeError(error)));
    }
}

function renderMonsterPanel(entity) {
    const stats = extractMonsterStats(entity.document);
    const payload = {
        sourceEntityId: entity.entityId,
        name: entity.name,
        sourceCode: entity.sourceCode,
        workKey: entity.workKey,
        editionKey: entity.editionKey,
        stats
    };

    const card = element("div", { className: "card card-body mb-3 rules-core-monster-card" });
    card.append(
        element("div", { className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3" },
            element("div", {},
                element("h6", { className: "h5 mb-1", text: "Detected monster stats" }),
                element("p", {
                    className: "text-body-secondary mb-0",
                    text: "Common fields are projected for Block Initiative testing. The immutable source document remains the authority."
                })),
            badge(stats.detectedFieldCount ? `${stats.detectedFieldCount} fields` : "Needs parser review", stats.detectedFieldCount ? "success" : "warning")));

    const statGrid = element("div", { className: "rules-core-stat-grid mb-3" });
    for (const [label, value] of [
        ["Armor Class", stats.armorClass],
        ["Hit Points", stats.hitPoints],
        ["Hit Dice", stats.hitDice],
        ["Initiative", stats.initiative],
        ["Speed", stats.speed],
        ["Challenge", stats.challengeRating]
    ]) {
        statGrid.append(metric(label, value ?? "—"));
    }
    card.append(statGrid);

    const abilities = ["STR", "DEX", "CON", "INT", "WIS", "CHA"]
        .map(key => [key, stats.abilities[key]])
        .filter(([, value]) => value !== null && value !== undefined);
    if (abilities.length) {
        const abilityGrid = element("div", { className: "rules-core-ability-grid mb-3" });
        for (const [label, value] of abilities) abilityGrid.append(metric(label, String(value)));
        card.append(abilityGrid);
    }

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
    card.append(element("div", { className: "d-flex flex-wrap align-items-center gap-2" },
        copy,
        element("span", {
            className: "small text-body-secondary",
            text: "API discovery: GET /api/sources/entities?entityType=monster&q=<name>; then GET /api/sources/entities/{entityId}."
        })));
    return card;
}

function extractMonsterStats(document) {
    const legacyText = normalizeLegacyBody(document?.body);
    const armorClass = firstDefined(
        formatArmorClass(document?.ac),
        matchLegacy(legacyText, ["Armor Class", "AC"]));
    const hitPoints = firstDefined(
        formatHitPoints(document?.hp),
        matchLegacy(legacyText, ["Hit Points", "HP"]));
    const hitDice = firstDefined(
        document?.hp?.formula,
        matchLegacy(legacyText, ["Hit Dice"]));
    const speed = firstDefined(
        formatValue(document?.speed),
        matchLegacy(legacyText, ["Speed"]));
    const challengeRating = firstDefined(
        formatChallenge(document?.cr),
        matchLegacy(legacyText, ["Challenge Rating", "CR"]));
    const initiative = firstDefined(
        formatValue(document?.initiative),
        formatValue(document?.init),
        matchLegacy(legacyText, ["Initiative", "Init"]),
        abilityModifier(document?.dex));

    const abilities = {
        STR: firstDefined(numberOrNull(document?.str), matchLegacyAbility(legacyText, "Str")),
        DEX: firstDefined(numberOrNull(document?.dex), matchLegacyAbility(legacyText, "Dex")),
        CON: firstDefined(numberOrNull(document?.con), matchLegacyAbility(legacyText, "Con")),
        INT: firstDefined(numberOrNull(document?.int), matchLegacyAbility(legacyText, "Int")),
        WIS: firstDefined(numberOrNull(document?.wis), matchLegacyAbility(legacyText, "Wis")),
        CHA: firstDefined(numberOrNull(document?.cha), matchLegacyAbility(legacyText, "Cha"))
    };

    const detectedFieldCount = [armorClass, hitPoints, hitDice, initiative, speed, challengeRating]
        .filter(value => value !== null && value !== undefined && value !== "").length
        + Object.values(abilities).filter(value => value !== null && value !== undefined).length;

    return {
        armorClass,
        hitPoints,
        hitDice,
        initiative,
        speed,
        challengeRating,
        abilities,
        detectedFieldCount
    };
}

function normalizeLegacyBody(body) {
    if (!body || typeof body !== "string") return "";
    const withBreaks = body
        .replace(/<\/(?:p|div|tr|td|th|li|h[1-6])>/gi, "\n")
        .replace(/<br\s*\/?\s*>/gi, "\n");
    const wrapper = document.createElement("div");
    wrapper.innerHTML = withBreaks;
    return (wrapper.textContent ?? "")
        .replace(/\r/g, "")
        .replace(/[ \t]+/g, " ")
        .replace(/\n+/g, "\n")
        .trim();
}

function matchLegacy(text, labels) {
    if (!text) return null;
    for (const label of labels) {
        const escaped = label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
        const match = text.match(new RegExp(`(?:^|\\n|\\s)${escaped}\\s*:?\\s*([^\\n]+)`, "i"));
        if (match?.[1]) {
            return match[1]
                .replace(/\s+(?:Armor Class|AC|Hit Points|HP|Hit Dice|Initiative|Init|Speed|Challenge Rating|CR)\s*:.*$/i, "")
                .trim();
        }
    }
    return null;
}

function matchLegacyAbility(text, label) {
    if (!text) return null;
    const match = text.match(new RegExp(`\\b${label}\\s*:?\\s*(-?\\d+)`, "i"));
    return match ? Number(match[1]) : null;
}

function formatArmorClass(value) {
    if (Array.isArray(value)) {
        return value.map(item => typeof item === "object" && item !== null ? item.ac ?? formatValue(item) : item).join(", ");
    }
    return formatValue(value);
}

function formatHitPoints(value) {
    if (value === null || value === undefined) return null;
    if (typeof value !== "object") return String(value);
    if (value.average !== undefined && value.formula) return `${value.average} (${value.formula})`;
    if (value.average !== undefined) return String(value.average);
    return formatValue(value);
}

function formatChallenge(value) {
    if (value && typeof value === "object" && !Array.isArray(value)) {
        return firstDefined(value.cr, value.lair, formatValue(value));
    }
    return formatValue(value);
}

function formatValue(value) {
    if (value === null || value === undefined) return null;
    if (typeof value === "string" || typeof value === "number" || typeof value === "boolean") return String(value);
    if (Array.isArray(value)) return value.map(formatValue).filter(Boolean).join(", ");
    if (typeof value === "object") {
        return Object.entries(value)
            .filter(([, candidate]) => candidate !== false && candidate !== null && candidate !== undefined)
            .map(([key, candidate]) => candidate === true ? key : `${key} ${formatValue(candidate)}`)
            .join(", ");
    }
    return String(value);
}

function abilityModifier(value) {
    const number = numberOrNull(value);
    if (number === null) return null;
    const modifier = Math.floor((number - 10) / 2);
    return `${modifier >= 0 ? "+" : ""}${modifier} (from DEX)`;
}

function numberOrNull(value) {
    return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function firstDefined(...values) {
    return values.find(value => value !== null && value !== undefined && value !== "") ?? null;
}

function matchesDefinition(entity, definition) {
    return entity.packageKey === definition.packageKey
        && entity.workKey === definition.workKey
        && entity.editionKey === definition.editionKey;
}

function compareBuiltIns(left, right) {
    const order = new Map([["3e", 30], ["3.5e", 35], ["5e", 50], ["5.5e", 55]]);
    return (order.get(left.gameEdition) ?? 999) - (order.get(right.gameEdition) ?? 999);
}

function countLabel(count, capped) {
    return `${count}${capped ? "+" : ""}`;
}

function metric(label, value) {
    return element("div", { className: "rules-core-metric" },
        element("div", { className: "rules-core-metric-value", text: value }),
        element("div", { className: "rules-core-metric-label", text: label }));
}

function field(label, control, columnClass) {
    return element("div", { className: columnClass },
        element("label", { className: "form-label fw-semibold", text: label }),
        control);
}

function prependRulesLawyerWorkflow(app, container) {
    if (container.querySelector(".rules-core-workflow")) return;
    const card = element("div", { className: "card card-body mb-3 rules-core-workflow" },
        element("div", { className: "d-flex flex-wrap justify-content-between gap-3 align-items-center" },
            element("div", {},
                element("h3", { className: "h5 mb-1", text: "Rules Lawyer workflow" }),
                element("div", { className: "text-body-secondary small", text: "Source material stays separate until you explicitly bind, decide, and publish." })),
            element("div", { className: "rules-core-workflow-steps" },
                workflowStep("1", "Sources", "Import and inspect", async () => { app.activeView = "library"; await app.render(); }),
                workflowStep("2", "Normalize", "Review suggestions"),
                workflowStep("3", "Decide", "Select or consolidate"),
                workflowStep("4", "Publish", "Create global revision"))));
    container.prepend(card);
}

function workflowStep(number, title, description, onClick = null) {
    const options = {
        className: `rules-core-workflow-step${onClick ? " rules-core-workflow-step-action" : ""}`
    };
    const node = element(onClick ? "button" : "div", onClick ? { ...options, type: "button" } : options,
        element("span", { className: "rules-core-workflow-number", text: number }),
        element("span", {},
            element("strong", { text: title }),
            element("small", { text: description })));
    if (onClick) node.addEventListener("click", onClick);
    return node;
}
