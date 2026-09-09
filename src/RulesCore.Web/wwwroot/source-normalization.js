import {
    alertNode,
    badge,
    describeError,
    element,
    formatDate,
    setButtonBusy
} from "./ui.js";

export function installSourceNormalization(app) {
    const renderGlobalOverview = app.renderGlobalOverview.bind(app);
    app.renderGlobalOverview = async container => {
        await renderGlobalOverview(container);
        if (app.activeView !== "global") {
            return;
        }

        for (const alert of container.querySelectorAll(".alert")) {
            if (alert.textContent?.includes("Concept creation and source binding are not exposed")) {
                alert.textContent = "No rule concepts exist yet. Create one manually or accept a reviewed source normalization suggestion below.";
            }
        }

        const card = createNormalizationCard(app, container);
        const insertionPoint = container.children[2] ?? null;
        container.insertBefore(card, insertionPoint);
        await card.loadCandidates();
    };
}

function createNormalizationCard(app, container) {
    const card = element("div", { className: "card card-body mb-3" });
    const heading = element("div", {
        className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3"
    });
    heading.append(
        element("div", {},
            element("h3", { className: "h5 mb-1", text: "Normalize imported sources" }),
            element("p", {
                className: "text-body-secondary mb-0",
                text: "Review deterministic concept suggestions for accessible source entities that are not bound yet. Suggestions never apply automatically."
            })),
        badge("Rules Lawyer review", "primary"));
    card.append(heading);

    const filters = element("div", { className: "row g-2 align-items-end mb-3" });
    const type = inputGroup("Entity type", "spell, skill, class…", "col-lg-3");
    const query = inputGroup("Search", "Name, source, work, edition…", "col-lg-6");
    const searchColumn = element("div", { className: "col-lg-3 d-grid" });
    const searchButton = element("button", {
        type: "button",
        className: "btn btn-outline-primary",
        text: "Review suggestions"
    });
    searchColumn.append(searchButton);
    filters.append(type.group, query.group, searchColumn);
    card.append(filters);

    card.append(element("div", {
        className: "small text-body-secondary mb-3",
        text: "The suggested key is derived only from entity type and name. Accepting a suggestion creates or reuses that stable concept and binds this source entity; it does not create a rule decision or publish anything."
    }));

    const status = element("div");
    const results = element("div");
    card.append(status, results);

    const loadCandidates = async () => {
        status.replaceChildren();
        results.replaceChildren();
        setButtonBusy(searchButton, true, "Loading…");
        try {
            const candidates = await app.api.getSourceNormalizationCandidates({
                entityType: type.input.value.trim() || null,
                query: query.input.value.trim() || null,
                limit: 100
            });
            renderCandidates(app, container, results, candidates);
        } catch (error) {
            status.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(searchButton, false);
        }
    };

    searchButton.addEventListener("click", loadCandidates);
    query.input.addEventListener("keydown", event => {
        if (event.key === "Enter") {
            event.preventDefault();
            loadCandidates();
        }
    });
    type.input.addEventListener("keydown", event => {
        if (event.key === "Enter") {
            event.preventDefault();
            loadCandidates();
        }
    });

    card.loadCandidates = loadCandidates;
    return card;
}

function renderCandidates(app, container, results, candidates) {
    if (!candidates.length) {
        results.append(alertNode(
            "secondary",
            "No accessible unbound source entities match these filters."));
        return;
    }

    const table = element("table", { className: "table table-hover align-middle mb-0" });
    const head = element("thead");
    const headRow = element("tr");
    for (const label of ["Source entity", "Package / edition", "Suggested concept", "Suggestion", ""]) {
        headRow.append(element("th", { text: label }));
    }
    head.append(headRow);

    const body = element("tbody");
    for (const candidate of candidates) {
        const row = element("tr");

        const source = element("td");
        source.append(
            element("div", { className: "fw-semibold", text: candidate.name }),
            element("div", {
                className: "small text-body-secondary",
                text: `${candidate.entityType} · ${candidate.sourceCode} · revision #${candidate.latestRevisionNumber}`
            }),
            element("div", {
                className: "small text-body-secondary",
                text: `Imported ${formatDate(candidate.latestImportedAt)}`
            }));
        row.append(source);

        row.append(element("td", {
            text: `${candidate.packageDisplayName} · ${candidate.editionDisplayName}`
        }));

        const suggestion = element("td");
        suggestion.append(element("div", {
            className: "font-monospace small",
            text: candidate.suggestedConceptKey
        }));
        if (candidate.suggestedConceptDisplayName) {
            suggestion.append(element("div", {
                className: "small text-body-secondary",
                text: `Existing: ${candidate.suggestedConceptDisplayName}`
            }));
        }
        row.append(suggestion);

        const kind = element("td");
        kind.append(suggestionBadge(candidate.suggestionKind));
        row.append(kind);

        const action = element("td", { className: "text-end" });
        const conflict = candidate.suggestionKind === "conflict";
        const accept = element("button", {
            type: "button",
            className: conflict ? "btn btn-sm btn-outline-secondary" : "btn btn-sm btn-outline-primary",
            text: conflict
                ? "Review manually"
                : candidate.suggestionKind === "existing-concept"
                    ? "Bind to concept"
                    : "Create + bind",
            disabled: conflict
        });

        if (!conflict) {
            accept.addEventListener("click", async () => {
                setButtonBusy(accept, true, "Accepting…");
                try {
                    const accepted = await app.api.acceptSourceNormalization(candidate.sourceEntityId);
                    await app.renderGlobalConcept(container, accepted.concept.id);
                } catch (error) {
                    window.alert(describeError(error));
                    setButtonBusy(accept, false);
                }
            });
        }
        action.append(accept);
        row.append(action);
        body.append(row);
    }

    table.append(head, body);
    results.append(element("div", { className: "table-responsive" }, table));
}

function suggestionBadge(kind) {
    switch (kind) {
        case "new-concept":
            return badge("New concept", "info");
        case "existing-concept":
            return badge("Existing concept", "success");
        case "conflict":
            return badge("Key conflict", "danger");
        default:
            return badge(kind ?? "Unknown", "secondary");
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
