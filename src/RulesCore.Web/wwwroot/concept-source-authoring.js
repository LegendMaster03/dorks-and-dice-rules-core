import {
    alertNode,
    badge,
    describeError,
    element,
    formatDate,
    setButtonBusy,
    DEFAULT_PAGE_SIZE,
    paginationControls
} from "./ui.js";

export function installConceptSourceAuthoring(app) {
    const renderGlobalOverview = app.renderGlobalOverview.bind(app);
    app.renderGlobalOverview = async container => {
        await renderGlobalOverview(container);
        if (app.activeView !== "global") {
            return;
        }

        const card = createConceptCard(app, container);
        const summary = container.firstElementChild;
        if (summary?.nextSibling) {
            container.insertBefore(card, summary.nextSibling);
        } else {
            container.append(card);
        }
    };

    const renderGlobalConcept = app.renderGlobalConcept.bind(app);
    app.renderGlobalConcept = async (container, conceptId) => {
        await renderGlobalConcept(container, conceptId);
        try {
            const detail = await app.api.getGlobalAuthoringConcept(conceptId);
            const card = await createSourceBindingCard(app, container, detail);
            const decisionEditor = container.children.length >= 3 ? container.children[2] : null;
            container.insertBefore(card, decisionEditor);
        } catch (error) {
            container.append(alertNode("danger", `Source binding tools could not load: ${describeError(error)}`));
        }
    };
}

function createConceptCard(app, container) {
    const card = element("div", { className: "card card-body mb-3" });
    const heading = element("div", {
        className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3"
    });
    heading.append(
        element("div", {},
            element("h3", { className: "h5 mb-1", text: "Create rule concept" }),
            element("p", {
                className: "text-body-secondary mb-0",
                text: "Create a stable rule identity before binding edition-specific source entities."
            })),
        badge("Rules Layer", "primary"));
    card.append(heading);

    const form = element("form");
    const row = element("div", { className: "row g-3" });
    const displayName = inputGroup("Display name", "Arcana", "col-lg-4");
    const entityType = inputGroup("Entity type", "skill", "col-lg-3");
    const key = inputGroup("Stable key", "skill.arcana", "col-lg-5");
    row.append(displayName.group, entityType.group, key.group);

    const help = element("div", {
        className: "form-text mt-2",
        text: "Keys and entity types are normalized to lowercase. Concept metadata is immutable after creation."
    });
    const result = element("div", { className: "mt-3" });
    const submit = element("button", {
        type: "submit",
        className: "btn btn-primary mt-3",
        text: "Create concept"
    });

    form.append(row, help, submit, result);
    form.addEventListener("submit", async event => {
        event.preventDefault();
        result.replaceChildren();
        setButtonBusy(submit, true, "Creating…");
        try {
            const payload = {
                displayName: displayName.input.value.trim(),
                entityType: entityType.input.value.trim(),
                key: key.input.value.trim()
            };
            if (!payload.displayName || !payload.entityType || !payload.key) {
                throw new Error("Display name, entity type, and stable key are required.");
            }

            const concept = await app.api.createGlobalConcept(payload);
            await app.renderGlobalConcept(container, concept.id);
        } catch (error) {
            result.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(submit, false);
        }
    });

    card.append(form);
    return card;
}

async function createSourceBindingCard(app, container, detail) {
    const card = element("div", { className: "card card-body mb-3" });
    const heading = element("div", {
        className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3"
    });
    heading.append(
        element("div", {},
            element("h3", { className: "h5 mb-1", text: "Source bindings" }),
            element("p", {
                className: "text-body-secondary mb-0",
                text: "Attach immutable source entities that may implement this concept."
            })),
        badge(`${detail.bindings.length} bound`, "secondary"));
    card.append(heading);

    const filters = element("div", { className: "row g-2 align-items-end mb-3" });
    const type = inputGroup("Entity type", detail.concept.entityType, "col-lg-3");
    const query = inputGroup("Search", "Name, source, work, or edition", "col-lg-6");
    const searchColumn = element("div", { className: "col-lg-3 d-grid" });
    const searchButton = element("button", {
        type: "button",
        className: "btn btn-outline-primary",
        text: "Find sources"
    });
    searchColumn.append(searchButton);
    filters.append(type.group, query.group, searchColumn);
    card.append(filters);

    const status = element("div");
    const results = element("div");
    card.append(status, results);

    let page = 0;
    const runSearch = async (resetPage = false) => {
        if (resetPage) page = 0;
        status.replaceChildren();
        results.replaceChildren();
        setButtonBusy(searchButton, true, "Searching…");
        try {
            const requested = await app.api.searchSourceEntityPage({
                entityType: type.input.value.trim() || null,
                query: query.input.value.trim() || null,
                limit: DEFAULT_PAGE_SIZE + 1,
                offset: page * DEFAULT_PAGE_SIZE
            });
            const hasNext = requested.length > DEFAULT_PAGE_SIZE;
            const sources = requested.slice(0, DEFAULT_PAGE_SIZE);
            if (!sources.length && page > 0) {
                page -= 1;
                await runSearch(false);
                return;
            }
            renderSourceResults(app, container, detail, results, sources, page, hasNext, async nextPage => {
                page = nextPage;
                await runSearch(false);
            });
        } catch (error) {
            status.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(searchButton, false);
        }
    };

    searchButton.addEventListener("click", () => runSearch(true));
    query.input.addEventListener("keydown", event => {
        if (event.key === "Enter") {
            event.preventDefault();
            runSearch(true);
        }
    });

    await runSearch(true);
    return card;
}

function renderSourceResults(app, container, detail, results, sources, page, hasNext, onPage) {
    const boundEntityIds = new Set(detail.bindings.map(binding => binding.sourceEntityId));
    if (!sources.length) {
        results.append(alertNode(
            "secondary",
            "No accessible source entities match these filters. Source ingestion and source-access grants are managed separately from Rules Lawyer authoring."));
        return;
    }

    const table = element("table", { className: "table table-hover align-middle mb-0" });
    const head = element("thead");
    const headRow = element("tr");
    for (const label of ["Source entity", "Type", "Package / edition", "Latest revision", ""]) {
        headRow.append(element("th", { text: label }));
    }
    head.append(headRow);

    const body = element("tbody");
    for (const source of sources) {
        const row = element("tr");
        const name = element("td");
        name.append(
            element("div", { className: "fw-semibold", text: source.name }),
            element("div", {
                className: "small text-body-secondary font-monospace",
                text: source.sourceCode
            }));
        row.append(name);
        row.append(element("td", { text: source.entityType }));
        row.append(element("td", {
            text: `${source.packageDisplayName} · ${source.editionDisplayName}`
        }));
        row.append(element("td", {
            text: `#${source.latestRevisionNumber} · ${formatDate(source.latestImportedAt)}`
        }));

        const action = element("td", { className: "text-end" });
        if (boundEntityIds.has(source.entityId)) {
            action.append(element("button", {
                type: "button",
                className: "btn btn-sm btn-outline-secondary",
                text: "Bound",
                disabled: true
            }));
        } else {
            const bindButton = element("button", {
                type: "button",
                className: "btn btn-sm btn-outline-primary",
                text: "Bind"
            });
            bindButton.addEventListener("click", async () => {
                setButtonBusy(bindButton, true, "Binding…");
                try {
                    await app.api.bindGlobalConceptSource(detail.concept.id, source.entityId);
                    await app.renderGlobalConcept(container, detail.concept.id);
                } catch (error) {
                    window.alert(describeError(error));
                    setButtonBusy(bindButton, false);
                }
            });
            action.append(bindButton);
        }
        row.append(action);
        body.append(row);
    }

    table.append(head, body);
    results.append(
        element("div", { className: "table-responsive" }, table),
        paginationControls({ page, itemCount: sources.length, hasNext, onPage }));
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
