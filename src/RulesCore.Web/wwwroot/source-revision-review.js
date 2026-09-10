import {
    alertNode,
    badge,
    clear,
    codeBlock,
    describeError,
    element,
    formatDate,
    formatJson,
    setButtonBusy
} from "./ui.js";

export function installSourceRevisionReview(app) {
    const renderGlobalOverview = app.renderGlobalOverview.bind(app);
    app.renderGlobalOverview = async container => {
        await renderGlobalOverview(container);
        if (app.activeView !== "global") {
            return;
        }

        try {
            const updates = await app.api.getSourceRevisionUpdates();
            if (!updates.length) {
                return;
            }

            const card = renderUpdatesCard(app, container, updates);
            const insertionPoint = container.children[1] ?? null;
            container.insertBefore(card, insertionPoint);
        } catch (error) {
            const warning = alertNode(
                "warning",
                `Source update review is unavailable: ${describeError(error)}`);
            const insertionPoint = container.children[1] ?? null;
            container.insertBefore(warning, insertionPoint);
        }
    };
}

function renderUpdatesCard(app, container, updates) {
    const card = element("div", { className: "card mb-3" });
    const header = element("div", {
        className: "card-header d-flex flex-wrap justify-content-between align-items-center gap-2"
    });
    header.append(
        element("div", {},
            element("div", { className: "fw-semibold", text: "Source updates to review" }),
            element("div", {
                className: "small text-body-secondary",
                text: "These current rule decisions pin source revisions that now have newer immutable revisions. Nothing migrates automatically."
            })),
        badge(`${updates.length} pending`, "warning"));
    card.append(header);

    const table = element("table", { className: "table table-hover align-middle mb-0" });
    const head = element("thead");
    const headRow = element("tr");
    for (const label of ["Rule", "Source", "Selected", "Latest", "Decision", ""]) {
        headRow.append(element("th", { text: label }));
    }
    head.append(headRow);

    const body = element("tbody");
    for (const update of updates) {
        const row = element("tr");
        const rule = element("td");
        rule.append(
            element("div", { className: "fw-semibold", text: update.displayName }),
            element("div", {
                className: "small text-body-secondary font-monospace",
                text: update.conceptKey
            }));
        row.append(rule);

        const source = element("td");
        source.append(
            element("div", { text: update.sourceEntityName }),
            element("div", {
                className: "small text-body-secondary",
                text: `${update.packageDisplayName} · ${update.editionDisplayName}`
            }));
        row.append(source);

        row.append(element("td", {
            text: `#${update.selectedRevisionNumber} · ${formatDate(update.selectedImportedAt)}`
        }));

        const latest = element("td");
        latest.append(
            element("div", { text: `#${update.latestRevisionNumber}` }),
            element("div", {
                className: "small text-body-secondary",
                text: `${update.newerRevisionCount} newer revision${update.newerRevisionCount === 1 ? "" : "s"}`
            }));
        row.append(latest);

        row.append(element("td", {
            text: `#${update.globalDecisionNumber} · ${update.decisionKind}`
        }));

        const action = element("td", { className: "text-end" });
        const preview = element("button", {
            type: "button",
            className: "btn btn-sm btn-outline-primary",
            text: "Preview update"
        });
        preview.addEventListener("click", async () => {
            setButtonBusy(preview, true, "Loading…");
            await renderPreview(app, container, update.ruleConceptId);
        });
        action.append(preview);
        row.append(action);
        body.append(row);
    }

    table.append(head, body);
    card.append(element("div", { className: "table-responsive" }, table));
    return card;
}

async function renderPreview(app, container, conceptId) {
    clear(container);
    const toolbar = element("div", { className: "d-flex flex-wrap gap-2 mb-3" });
    toolbar.append(
        element("button", {
            type: "button",
            className: "btn btn-sm btn-outline-secondary",
            text: "Back to Global Rules",
            onClick: async () => app.renderGlobalOverview(container)
        }),
        element("button", {
            type: "button",
            className: "btn btn-sm btn-outline-primary",
            text: "Open rule editor",
            onClick: async () => app.renderGlobalConcept(container, conceptId)
        }));
    container.append(toolbar);

    const loading = element("div", {
        className: "card card-body text-body-secondary",
        text: "Comparing source revisions…"
    });
    container.append(loading);

    try {
        const preview = await app.api.previewSourceRevisionUpdate(conceptId);
        loading.remove();
        toolbar.append(createRejectButton(app, container, preview));
        container.append(renderPreviewSummary(preview));

        if (!preview.patchCompatible) {
            container.append(alertNode(
                "warning",
                preview.compatibilityMessage
                    ?? "The current decision can not be replayed against the latest source revision without manual editing."));
        } else {
            toolbar.append(createAdoptButton(app, container, preview));
            container.append(alertNode(
                "info",
                "Previewing does not change the Rules Layer. Adopting the latest source creates a new pending global decision with the same decision semantics; publication remains a separate explicit action."));
        }

        container.append(alertNode(
            "secondary",
            "Rejecting records only that this exact source revision was reviewed for this exact global decision. A newer source revision or a newer global decision will surface the update again."));
        container.append(renderResolvedDocuments(preview));
        if (preview.patchCompatible) {
            container.append(renderChanges(preview.changes));
        }
    } catch (error) {
        loading.remove();
        container.append(alertNode("danger", describeError(error)));
    }
}

function createAdoptButton(app, container, preview) {
    const update = preview.update;
    const button = element("button", {
        type: "button",
        className: "btn btn-sm btn-primary",
        text: "Adopt latest source revision"
    });

    button.addEventListener("click", async () => {
        const confirmed = window.confirm(
            `Create a new global decision for ${update.displayName} using source revision #${update.latestRevisionNumber}? This preserves the current decision kind and patch, but does not publish the rule.`);
        if (!confirmed) {
            return;
        }

        setButtonBusy(button, true, "Creating decision…");
        try {
            const result = await app.api.adoptLatestSourceRevision(
                update.ruleConceptId,
                {
                    expectedGlobalRuleDecisionId: update.globalRuleDecisionId,
                    expectedLatestSourceEntityRevisionId: update.latestSourceEntityRevisionId,
                    expectedLatestFingerprint: update.latestFingerprint
                });
            window.alert(
                `Created pending global decision #${result.globalDecisionNumber} using source revision #${result.sourceRevisionNumber}. It has not been published.`);
            await app.renderGlobalConcept(container, update.ruleConceptId);
        } catch (error) {
            window.alert(describeError(error));
            setButtonBusy(button, false);
        }
    });

    return button;
}

function createRejectButton(app, container, preview) {
    const update = preview.update;
    const button = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-danger",
        text: "Reject latest source revision"
    });

    button.addEventListener("click", async () => {
        const reason = window.prompt(
            `Why should source revision #${update.latestRevisionNumber} not be adopted for global decision #${update.globalDecisionNumber}? This dismisses only this exact review target.`);
        if (reason === null) {
            return;
        }
        if (!reason.trim()) {
            window.alert("A rejection reason is required.");
            return;
        }

        setButtonBusy(button, true, "Recording rejection…");
        try {
            const result = await app.api.rejectLatestSourceRevision(
                update.ruleConceptId,
                {
                    expectedGlobalRuleDecisionId: update.globalRuleDecisionId,
                    expectedLatestSourceEntityRevisionId: update.latestSourceEntityRevisionId,
                    expectedLatestFingerprint: update.latestFingerprint,
                    reason: reason.trim()
                });
            window.alert(
                `Recorded rejection of source revision #${result.sourceRevisionNumber} for the current global decision. No rule decision or publication was changed.`);
            await app.renderGlobalOverview(container);
        } catch (error) {
            window.alert(describeError(error));
            setButtonBusy(button, false);
        }
    });

    return button;
}

function renderPreviewSummary(preview) {
    const update = preview.update;
    const card = element("div", { className: "card card-body mb-3" });
    card.append(
        element("h3", { className: "h5 mb-2", text: update.displayName }),
        element("div", {
            className: "small text-body-secondary font-monospace mb-3",
            text: update.conceptKey
        }));

    const grid = element("div", { className: "row g-3" });
    grid.append(
        summaryColumn("Current source", [
            `Revision #${update.selectedRevisionNumber}`,
            update.sourceEntityName,
            `${update.packageDisplayName} · ${update.editionDisplayName}`,
            `Imported ${formatDate(update.selectedImportedAt)}`
        ]),
        summaryColumn("Latest source", [
            `Revision #${update.latestRevisionNumber}`,
            `${update.newerRevisionCount} newer revision${update.newerRevisionCount === 1 ? "" : "s"}`,
            `Imported ${formatDate(update.latestImportedAt)}`,
            `Decision: ${update.decisionKind}`
        ]));
    card.append(grid);
    return card;
}

function summaryColumn(title, lines) {
    const column = element("div", { className: "col-lg-6" });
    column.append(element("div", { className: "fw-semibold mb-1", text: title }));
    for (const line of lines) {
        column.append(element("div", { className: "small text-body-secondary", text: line }));
    }
    return column;
}

function renderResolvedDocuments(preview) {
    const row = element("div", { className: "row g-3 mb-3" });
    const current = element("div", { className: "col-xl-6" });
    current.append(
        element("h4", { className: "h6", text: "Current resolved rule" }),
        codeBlock(preview.currentResolvedDocument));

    const candidate = element("div", { className: "col-xl-6" });
    candidate.append(element("h4", { className: "h6", text: "Candidate resolved rule" }));
    if (preview.candidateResolvedDocument) {
        candidate.append(codeBlock(preview.candidateResolvedDocument));
    } else {
        candidate.append(alertNode(
            "secondary",
            "No candidate resolved document is available because the current patch is incompatible with the newer source revision."));
    }

    row.append(current, candidate);
    return row;
}

function renderChanges(changes) {
    const card = element("div", { className: "card" });
    const header = element("div", { className: "card-header fw-semibold" });
    header.textContent = `Resolved-rule changes (${changes.length})`;
    card.append(header);

    if (!changes.length) {
        card.append(element("div", {
            className: "card-body text-body-secondary",
            text: "The newer source revision produces the same resolved rule with the current decision."
        }));
        return card;
    }

    const table = element("table", { className: "table align-middle mb-0" });
    const head = element("thead");
    const row = element("tr");
    for (const label of ["Path", "Change", "Before", "After"]) {
        row.append(element("th", { text: label }));
    }
    head.append(row);
    const body = element("tbody");
    for (const change of changes) {
        const changeRow = element("tr");
        changeRow.append(
            element("td", { className: "font-monospace small", text: change.path || "/" }),
            element("td", {}, badge(change.changeKind, changeKindBadge(change.changeKind))),
            element("td", { className: "small font-monospace", text: compactJson(change.before) }),
            element("td", { className: "small font-monospace", text: compactJson(change.after) }));
        body.append(changeRow);
    }
    table.append(head, body);
    card.append(element("div", { className: "table-responsive" }, table));
    return card;
}

function changeKindBadge(kind) {
    switch (kind) {
        case "add": return "success";
        case "remove": return "danger";
        case "replace": return "warning";
        default: return "secondary";
    }
}

function compactJson(value) {
    if (value === null || value === undefined) {
        return "—";
    }
    const formatted = formatJson(value);
    return formatted.length > 180 ? `${formatted.slice(0, 177)}…` : formatted;
}
