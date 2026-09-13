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
        await card.loadIgnoredPackages();
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
                text: "Review deterministic concept suggestions for accessible source entities that are not bound yet. Binding remains deliberate; mechanically identical cross-edition rules may then resolve automatically, while publication remains explicit. Large or irrelevant source packages can be ignored for global rules work without deleting them or changing the uploader's access."
            })),
        badge("Rules Lawyer review", "primary"));
    card.append(heading);

    const ignored = element("div", { className: "mb-3" });
    card.append(ignored);

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
        text: "The suggested key is derived only from entity type and name. Accepting creates or reuses that stable concept and binds this source. Mechanically identical cross-edition rules may then resolve automatically, and publication remains explicit. Ignoring applies to the entire source package for global review; it does not revoke the user's source grant or remove Source Layer data."
    }));

    const status = element("div");
    const results = element("div");
    results.append(element("div", {
        className: "rules-core-idle-state",
        text: "Search by rule name, source, edition, or entity type when you are ready to review normalization suggestions."
    }));
    card.append(status, results);

    let page = 0;
    const loadIgnoredPackages = async () => {
        ignored.replaceChildren();
        try {
            const packages = await app.api.backend("/api/global/rules/normalization/ignored-packages");
            if (!packages.length) return;

            const details = element("details", { className: "border rounded p-2" });
            details.append(element("summary", {
                className: "fw-semibold",
                text: `Ignored for global rules (${packages.length})`
            }));
            const list = element("div", { className: "list-group list-group-flush mt-2" });
            for (const source of packages) {
                const restore = element("button", {
                    type: "button",
                    className: "btn btn-sm btn-outline-secondary",
                    text: "Restore"
                });
                restore.addEventListener("click", async () => {
                    setButtonBusy(restore, true, "Restoring…");
                    try {
                        await setPackageIgnored(app, source.sourcePackageId, false);
                        await loadIgnoredPackages();
                        await loadCandidates(true);
                    } catch (error) {
                        window.alert(describeError(error));
                    } finally {
                        setButtonBusy(restore, false);
                    }
                });
                list.append(element("div", {
                    className: "list-group-item px-0 d-flex flex-wrap justify-content-between gap-2"
                },
                element("div", {},
                    element("div", { className: "fw-semibold", text: source.packageDisplayName }),
                    element("div", {
                        className: "small text-body-secondary",
                        text: [source.provider, source.reason || null].filter(Boolean).join(" · ")
                    })),
                restore));
            }
            details.append(list);
            ignored.append(details);
        } catch (error) {
            ignored.replaceChildren(alertNode("warning", `Ignored sources could not be loaded: ${describeError(error)}`));
        }
    };

    const loadCandidates = async (resetPage = false) => {
        if (resetPage) page = 0;
        status.replaceChildren();
        results.replaceChildren();
        setButtonBusy(searchButton, true, "Loading…");
        try {
            const requested = await app.api.getSourceNormalizationCandidates({
                entityType: type.input.value.trim() || null,
                query: query.input.value.trim() || null,
                limit: DEFAULT_PAGE_SIZE + 1,
                offset: page * DEFAULT_PAGE_SIZE
            });
            const hasNext = requested.length > DEFAULT_PAGE_SIZE;
            const candidates = requested.slice(0, DEFAULT_PAGE_SIZE);
            if (!candidates.length && page > 0) {
                page -= 1;
                await loadCandidates(false);
                return;
            }
            renderCandidates(app, container, results, candidates, page, hasNext, async nextPage => {
                page = nextPage;
                await loadCandidates(false);
            }, async () => {
                await loadIgnoredPackages();
                await loadCandidates(true);
            });
        } catch (error) {
            status.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(searchButton, false);
        }
    };

    searchButton.addEventListener("click", () => loadCandidates(true));
    query.input.addEventListener("keydown", event => {
        if (event.key === "Enter") {
            event.preventDefault();
            loadCandidates(true);
        }
    });
    type.input.addEventListener("keydown", event => {
        if (event.key === "Enter") {
            event.preventDefault();
            loadCandidates(true);
        }
    });

    card.loadCandidates = () => loadCandidates(true);
    card.loadIgnoredPackages = loadIgnoredPackages;
    return card;
}

function renderCandidates(app, container, results, candidates, page, hasNext, onPage, onIgnored) {
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
        const buttons = element("div", { className: "d-inline-flex flex-wrap gap-1 justify-content-end" });
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

        const ignore = element("button", {
            type: "button",
            className: "btn btn-sm btn-outline-danger",
            text: "Ignore package"
        });
        ignore.addEventListener("click", async () => {
            if (!window.confirm(`Ignore '${candidate.packageDisplayName}' for global rules review? The source remains available to users who have access to it.`)) {
                return;
            }
            setButtonBusy(ignore, true, "Ignoring…");
            try {
                await setPackageIgnored(app, candidate.sourcePackageId, true);
                await onIgnored();
            } catch (error) {
                window.alert(describeError(error));
                setButtonBusy(ignore, false);
            }
        });

        buttons.append(accept, ignore);
        action.append(buttons);
        row.append(action);
        body.append(row);
    }

    table.append(head, body);
    results.append(
        element("div", { className: "table-responsive" }, table),
        paginationControls({ page, itemCount: candidates.length, hasNext, onPage }));
}

function setPackageIgnored(app, sourcePackageId, ignored) {
    return app.api.backend(`/api/global/rules/normalization/packages/${encodeURIComponent(sourcePackageId)}/ignored`, {
        method: "PUT",
        body: { ignored }
    });
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
