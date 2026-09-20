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

const STATE_LABELS = {
    Pending: "Pending",
    AgentReview: "Agent review",
    WaitingForHuman: "Waiting for human",
    ManualResolutionRequired: "Manual resolution",
    Completed: "Completed",
    Deferred: "Deferred"
};

let actionSequence = 0;

const KIND_LABELS = {
    "normalization-review": "Normalization",
    "global-rule-adjudication": "Rule adjudication",
    "source-update-review": "Source update"
};

export function installAdjudicationQueue(app) {
    const renderGlobalOverview = app.renderGlobalOverview.bind(app);
    app.renderGlobalOverview = async container => {
        await renderGlobalOverview(container);
        if (app.activeView !== "global") return;

        const card = createQueueCard(app, container);
        const insertionPoint = container.children[1] ?? null;
        container.insertBefore(card, insertionPoint);
        await card.refresh();
    };
}

function createQueueCard(app, container) {
    const card = element("section", {
        className: "card card-body mb-3",
        attributes: { "aria-labelledby": "rules-core-adjudication-queue-heading" }
    });
    const heading = element("div", {
        className: "d-flex flex-wrap justify-content-between align-items-start gap-3 mb-3"
    });
    const title = element("div", {},
        element("h3", {
            id: "rules-core-adjudication-queue-heading",
            className: "h5 mb-1",
            text: "Adjudication Queue"
        }),
        element("p", {
            className: "text-body-secondary mb-0",
            text: "Rules Core performs deterministic processing first. Remaining items can be reviewed by a Rules Lawyer agent or human, while publication remains explicit."
        }));
    const refresh = element("button", {
        type: "button",
        className: "btn btn-outline-primary",
        text: "Refresh queue",
        ariaLabel: "Refresh adjudication queue"
    });
    heading.append(title, refresh);
    card.append(heading);

    const filters = element("div", { className: "row g-2 mb-3" });
    const kind = selectField("Work kind", "Adjudication work kind", [
        ["", "All work kinds"],
        ...Object.entries(KIND_LABELS).map(([value, label]) => [value, label])
    ]);
    const state = selectField("State", "Adjudication work state", [
        ["", "All outstanding states"],
        ...Object.entries(STATE_LABELS).map(([value, label]) => [value, label])
    ]);
    kind.group.classList.add("col-md-6");
    state.group.classList.add("col-md-6");
    filters.append(kind.group, state.group);
    card.append(filters);

    const status = element("div", { className: "small text-body-secondary mb-2" });
    const results = element("div");
    card.append(status, results);

    const load = async () => {
        setButtonBusy(refresh, true, "Refreshing…");
        status.textContent = "Running deterministic discovery and loading adjudication work…";
        results.replaceChildren();
        try {
            const discovery = await app.api.discoverAdjudicationWork();
            const items = await app.api.getAdjudicationWork({
                kind: kind.select.value || null,
                state: state.select.value || null
            });
            status.textContent = `${items.length} visible work item${items.length === 1 ? "" : "s"} · ${discovery.deterministicDecisionsApplied} deterministic decision${discovery.deterministicDecisionsApplied === 1 ? "" : "s"} applied during this discovery pass.`;
            renderQueue(app, container, results, items);
        } catch (error) {
            status.textContent = "";
            results.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(refresh, false);
        }
    };

    refresh.addEventListener("click", load);
    kind.select.addEventListener("change", load);
    state.select.addEventListener("change", load);
    card.refresh = load;
    return card;
}

function renderQueue(app, container, target, items) {
    target.replaceChildren();
    if (!items.length) {
        target.append(alertNode("secondary", "No adjudication work matches the current filters."));
        return;
    }

    const table = element("table", { className: "table table-hover align-middle mb-0" });
    const head = element("thead", {}, element("tr", {},
        element("th", { text: "Work" }),
        element("th", { text: "Kind" }),
        element("th", { text: "State" }),
        element("th", { text: "Deterministic result" }),
        element("th", { text: "Publication" }),
        element("th", { text: "" })));
    const body = element("tbody");

    for (const item of items) {
        const work = element("td", {},
            element("div", { className: "fw-semibold", text: item.displayName ?? item.conceptKey ?? item.id }),
            item.conceptKey ? element("div", { className: "small font-monospace text-body-secondary", text: item.conceptKey }) : null,
            item.entityType ? element("div", { className: "small text-body-secondary", text: item.entityType }) : null);
        const stateCell = element("td");
        stateCell.append(stateBadge(item.state));
        if (item.hasOutstandingClarification) {
            stateCell.append(element("div", { className: "small text-body-secondary mt-1", text: "Human answer required" }));
        }

        const publication = element("td");
        if (item.completedButUnpublished) publication.append(badge("Ready to publish", "warning"));
        else if (item.published) publication.append(badge("Published", "success"));
        else publication.append(element("span", { className: "text-body-secondary", text: "—" }));

        const open = element("button", {
            type: "button",
            className: "btn btn-sm btn-outline-primary",
            text: "Open work item",
            ariaLabel: `Open adjudication work item ${item.displayName ?? item.id}`,
            onClick: async () => renderWorkItem(app, container, item.id)
        });

        body.append(element("tr", {},
            work,
            element("td", { text: KIND_LABELS[item.workKind] ?? item.workKind }),
            stateCell,
            element("td", { text: item.deterministicReason ?? "Not evaluated" }),
            publication,
            element("td", { className: "text-end" }, open)));
    }
    table.append(head, body);
    target.append(element("div", { className: "table-responsive" }, table));
}

async function renderWorkItem(app, container, workItemId) {
    clear(container);
    const toolbar = element("div", { className: "d-flex flex-wrap gap-2 mb-3" });
    toolbar.append(element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary",
        text: "← Back to Adjudication Queue",
        onClick: async () => app.renderGlobalOverview(container)
    }));
    const detail = element("div", {}, element("div", {
        className: "card card-body text-body-secondary",
        text: "Loading adjudication work…"
    }));
    container.append(toolbar, detail);

    try {
        const item = await app.api.getAdjudicationWorkItem(workItemId);
        renderWorkDetail(app, container, detail, item);
    } catch (error) {
        detail.replaceChildren(alertNode("danger", describeError(error)));
    }
}

function renderWorkDetail(app, container, target, detail) {
    target.replaceChildren();
    const work = detail.workItem;
    const summary = element("section", { className: "card card-body mb-3" },
        element("div", { className: "d-flex flex-wrap justify-content-between gap-3" },
            element("div", {},
                element("h3", { className: "h5 mb-1", text: work.displayName ?? work.conceptKey ?? "Adjudication work" }),
                element("div", { className: "small text-body-secondary", text: KIND_LABELS[work.workKind] ?? work.workKind })),
            stateBadge(work.state)),
        definitionList([
            ["Work item", work.id],
            ["Version", String(work.version)],
            ["Concept", work.conceptKey ?? "Not bound yet"],
            ["State", STATE_LABELS[work.state] ?? work.state],
            ["Deterministic result", work.deterministicReason ?? "No deterministic result recorded"],
            ["Publication", work.completedButUnpublished ? "Completed decision is not in the current published ruleset" : (work.published ? "Exact current decision is published" : "Not publication-ready")]
        ]));
    target.append(summary);

    if (detail.clarification) target.append(renderClarificationHistory(detail.clarification));
    target.append(renderWorkflowActions(app, container, detail));

    if (detail.normalizationCandidate) {
        target.append(renderNormalizationEvidence(app, container, detail));
    }
    if (detail.rule) {
        target.append(renderRuleEvidence(app, container, detail));
    }
    if (detail.sourceUpdate) {
        target.append(renderSourceUpdateEvidence(app, container, detail));
    }
    target.append(renderAuditHistory(detail.history));
}

function renderWorkflowActions(app, container, detail) {
    const work = detail.workItem;
    const card = element("section", { className: "card card-body mb-3" },
        element("h4", { className: "h6", text: "Workflow actions" }));
    const actions = element("div", { className: "d-flex flex-wrap gap-2 mb-3" });

    if (work.state === "Pending") {
        actions.append(actionButton("Begin review", "btn-outline-primary", async button => {
            await mutateAndReload(app, container, work.id, button, () => app.api.beginAdjudicationWork(work.id, work.version));
        }));
    }
    if (work.state === "ManualResolutionRequired" || work.state === "Deferred") {
        actions.append(actionButton("Reopen", "btn-outline-primary", async button => {
            await mutateAndReload(app, container, work.id, button, () => app.api.reopenAdjudicationWork(work.id, work.version));
        }));
    }
    if (detail.rule?.concept?.id) {
        actions.append(element("button", {
            type: "button",
            className: "btn btn-outline-primary",
            text: "Open normal decision editor",
            ariaLabel: "Open the normal Rules Lawyer decision editor",
            onClick: async () => app.renderGlobalConcept(container, detail.rule.concept.id)
        }));
    }
    if (work.completedButUnpublished) {
        actions.append(actionButton("Publish pending global rules", "btn-success", async button => {
            setButtonBusy(button, true, "Publishing…");
            try {
                await app.api.publishGlobalRules();
                await app.api.discoverAdjudicationWork();
                await renderWorkItem(app, container, work.id);
            } catch (error) {
                window.alert(describeError(error));
                setButtonBusy(button, false);
            }
        }));
    }
    card.append(actions);

    if (work.state === "WaitingForHuman" && detail.clarification && !detail.clarification.answer) {
        card.append(textAction(
            "Human answer",
            "Answer the focused clarification question",
            "Answer clarification",
            async (button, value) => mutateAndReload(app, container, work.id, button, () =>
                app.api.answerAdjudicationClarification(work.id, work.version, value))));
    } else if (work.state !== "Completed" && work.state !== "Deferred" && work.state !== "ManualResolutionRequired") {
        card.append(textAction(
            "Ask a human",
            "Focused question that is required before adjudication can continue",
            "Request clarification",
            async (button, value) => mutateAndReload(app, container, work.id, button, () =>
                app.api.requestAdjudicationClarification(work.id, work.version, value))));
    }

    if (work.state !== "Completed") {
        card.append(textAction(
            "Manual escalation",
            "Concise reason this edge case needs a human Rules Lawyer",
            "Require manual resolution",
            async (button, value) => mutateAndReload(app, container, work.id, button, () =>
                app.api.escalateAdjudicationWork(work.id, work.version, value)),
            "btn-outline-danger"));
        card.append(textAction(
            "Defer",
            "Reason to defer this work without resolving it",
            "Defer work",
            async (button, value) => mutateAndReload(app, container, work.id, button, () =>
                app.api.deferAdjudicationWork(work.id, work.version, value)),
            "btn-outline-secondary"));
    }
    return card;
}

function renderNormalizationEvidence(app, container, detail) {
    const candidate = detail.normalizationCandidate;
    const card = element("section", { className: "card card-body mb-3" },
        element("h4", { className: "h6", text: "Normalization evidence" }),
        definitionList([
            ["Source entity", candidate.name],
            ["Source", `${candidate.packageDisplayName} · ${candidate.editionDisplayName} · ${candidate.sourceCode}`],
            ["Suggested concept key", candidate.suggestedConceptKey],
            ["Suggestion", candidate.suggestionKind]
        ]));
    if (candidate.suggestionKind !== "conflict") {
        card.append(actionButton("Accept reviewed normalization suggestion", "btn-primary mt-3", async button => {
            setButtonBusy(button, true, "Accepting…");
            try {
                await app.api.acceptSourceNormalization(candidate.sourceEntityId);
                await app.api.discoverAdjudicationWork();
                await renderWorkItem(app, container, detail.workItem.id);
            } catch (error) {
                window.alert(describeError(error));
                setButtonBusy(button, false);
            }
        }));
    } else {
        card.append(alertNode("warning", "The deterministic suggestion conflicts with an existing concept. It can not be accepted silently and requires manual normalization review."));
    }
    return card;
}

function renderRuleEvidence(app, container, detail) {
    const rule = detail.rule;
    const card = element("section", { className: "card card-body mb-3" },
        element("div", { className: "d-flex flex-wrap justify-content-between gap-2" },
            element("h4", { className: "h6", text: "Rule adjudication evidence" }),
            badge(`${rule.restrictedBindingCount} restricted binding${rule.restrictedBindingCount === 1 ? "" : "s"}`, rule.restrictedBindingCount ? "warning" : "secondary")),
        element("p", {
            className: "small text-body-secondary",
            text: "Only exact source revisions accessible to this authenticated Rules Lawyer are included below. Restricted binding counts are shown without restricted source contents."
        }));

    if (detail.deterministicResolution) {
        card.append(alertNode(
            detail.deterministicResolution.applied ? "success" : "info",
            detail.deterministicResolution.reason));
    }

    const sources = element("div", { className: "mb-3" },
        element("h5", { className: "h6", text: "Accessible exact source revisions" }));
    if (!detail.accessibleSourceRevisions.length) {
        sources.append(alertNode("warning", "This Rules Lawyer identity can not inspect an active source implementation for this work item."));
    }
    for (const source of detail.accessibleSourceRevisions) {
        const disclosure = element("details", { className: "border rounded p-2 mb-2" },
            element("summary", {
                text: `${source.sourceEntityName} · ${source.packageDisplayName} · revision #${source.revisionNumber}`
            }),
            element("div", { className: "small text-body-secondary my-2", text: `${source.sourceCode} · ${source.editionDisplayName} · ${source.fingerprint}` }),
            codeBlock(source.document));
        sources.append(disclosure);
    }
    card.append(sources);

    const comparisons = element("div", {}, element("h5", { className: "h6", text: "Semantic comparisons" }));
    if (!detail.semanticComparisons.length) {
        comparisons.append(element("div", { className: "text-body-secondary", text: "No cross-source comparison is available for this item." }));
    }
    for (const comparison of detail.semanticComparisons) {
        const disclosure = element("details", { className: "border rounded p-2 mb-2" },
            element("summary", {
                text: `${comparison.contradictionCount} contradiction${comparison.contradictionCount === 1 ? "" : "s"} · ${comparison.compatibleDifferenceCount} compatible difference${comparison.compatibleDifferenceCount === 1 ? "" : "s"}`
            }),
            element("p", { className: "small text-body-secondary mt-2 mb-2", text: comparison.explanation }));
        if (comparison.differences.length) {
            const list = element("ul", { className: "mb-0" });
            for (const difference of comparison.differences) {
                list.append(element("li", {},
                    element("code", { text: difference.path }),
                    ` · ${difference.kind} · ${difference.explanation}`));
            }
            disclosure.append(list);
        }
        comparisons.append(disclosure);
    }
    card.append(comparisons);
    return card;
}

function renderSourceUpdateEvidence(app, container, detail) {
    const preview = detail.sourceUpdate;
    const update = preview.update;
    const card = element("section", { className: "card card-body mb-3" },
        element("h4", { className: "h6", text: "Source update review" }),
        definitionList([
            ["Source", `${update.sourceEntityName} · ${update.packageDisplayName}`],
            ["Current decision", `#${update.globalDecisionNumber} · source revision #${update.selectedRevisionNumber}`],
            ["Latest source revision", `#${update.latestRevisionNumber}`],
            ["Patch compatibility", preview.patchCompatible ? "Compatible" : (preview.compatibilityMessage ?? "Manual review required")]
        ]));
    if (preview.patchCompatible) {
        card.append(actionButton("Adopt latest source revision", "btn-primary mt-3", async button => {
            setButtonBusy(button, true, "Adopting…");
            try {
                await app.api.adoptLatestSourceRevision(update.ruleConceptId, {
                    expectedGlobalRuleDecisionId: update.globalRuleDecisionId,
                    expectedLatestSourceEntityRevisionId: update.latestSourceEntityRevisionId,
                    expectedLatestFingerprint: update.latestFingerprint
                });
                await app.api.discoverAdjudicationWork();
                await renderWorkItem(app, container, detail.workItem.id);
            } catch (error) {
                window.alert(describeError(error));
                setButtonBusy(button, false);
            }
        }));
    } else {
        card.append(alertNode("warning", preview.compatibilityMessage ?? "The source update needs manual decision editing."));
    }
    return card;
}

function renderClarificationHistory(clarification) {
    const card = element("section", { className: "card card-body mb-3" },
        element("h4", { className: "h6", text: "Human clarification" }),
        definitionList([
            ["Question", clarification.question],
            ["Requested by", clarification.requestedByActorId],
            ["Requested", formatDate(clarification.requestedAt)],
            ["Request context version", String(clarification.requestedAtVersion)],
            ["Answer", clarification.answer ?? "Awaiting answer"],
            ["Answered by", clarification.answeredByActorId ?? "—"],
            ["Answered", clarification.answeredAt ? formatDate(clarification.answeredAt) : "—"]
        ]));
    return card;
}

function renderAuditHistory(history) {
    const card = element("section", { className: "card card-body mb-3" },
        element("h4", { className: "h6", text: "Workflow history" }));
    if (!history.length) {
        card.append(element("div", { className: "text-body-secondary", text: "No workflow events are recorded." }));
        return card;
    }
    const list = element("div", { className: "list-group list-group-flush" });
    for (const event of history) {
        list.append(element("div", { className: "list-group-item px-0" },
            element("div", { className: "d-flex flex-wrap justify-content-between gap-2" },
                element("strong", { text: event.eventKind }),
                element("span", { className: "small text-body-secondary", text: formatDate(event.occurredAt) })),
            event.message ? element("div", { text: event.message }) : null,
            element("div", {
                className: "small text-body-secondary",
                text: `Work version ${event.workVersion}${event.actorUserId ? ` · actor ${event.actorUserId}` : ""}`
            })));
    }
    card.append(list);
    return card;
}

function textAction(label, placeholder, buttonLabel, action, buttonClass = "btn-outline-primary") {
    const id = `adjudication-${label.toLowerCase().replace(/[^a-z0-9]+/g, "-")}-${++actionSequence}`;
    const input = element("textarea", {
        id,
        className: "form-control",
        rows: 2,
        placeholder,
        ariaLabel: label
    });
    const button = element("button", {
        type: "button",
        className: `btn ${buttonClass}`,
        text: buttonLabel,
        onClick: async () => {
            const value = input.value.trim();
            if (!value) {
                input.focus();
                return;
            }
            await action(button, value);
        }
    });
    return element("div", { className: "border rounded p-3 mb-3" },
        element("label", { className: "form-label fw-semibold", text: label, attributes: { for: id } }),
        input,
        element("div", { className: "mt-2" }, button));
}

function actionButton(label, className, action) {
    return element("button", {
        type: "button",
        className: `btn ${className}`,
        text: label,
        onClick: async event => action(event.currentTarget)
    });
}

async function mutateAndReload(app, container, workItemId, button, mutation) {
    setButtonBusy(button, true, "Saving…");
    try {
        await mutation();
        await renderWorkItem(app, container, workItemId);
    } catch (error) {
        window.alert(describeError(error));
        setButtonBusy(button, false);
    }
}

function stateBadge(state) {
    const kind = state === "Completed"
        ? "success"
        : state === "WaitingForHuman"
            ? "info"
            : state === "ManualResolutionRequired"
                ? "danger"
                : state === "Deferred"
                    ? "secondary"
                    : state === "AgentReview"
                        ? "primary"
                        : "warning";
    return badge(STATE_LABELS[state] ?? state, kind);
}

function selectField(label, ariaLabel, options) {
    const select = element("select", { className: "form-select", ariaLabel });
    for (const [value, text] of options) select.append(element("option", { value, text }));
    const group = element("div", {}, element("label", { className: "form-label fw-semibold", text: label }), select);
    return { group, select };
}
