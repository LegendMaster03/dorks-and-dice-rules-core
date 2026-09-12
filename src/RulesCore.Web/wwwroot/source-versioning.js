import {
    alertNode,
    badge,
    clear,
    describeError,
    element,
    formatJson,
    setButtonBusy
} from "./ui.js";

const RULES_LAWYER_ROLE = "Rules Lawyer";
const DORKS_MODE = "dorks-and-dice";
const LINEAGE_KINDS = [
    "predecessor-of",
    "playtest-of",
    "revised-as",
    "renamed-as",
    "split-into",
    "combined-into",
    "related-version"
];

export function installSourceVersioning(app) {
    app.canReviewVersions = app.hostContext.siteMode === DORKS_MODE
        && (app.session.globalRoles ?? []).includes(RULES_LAWYER_ROLE);

    const renderNavigation = app.renderNavigation.bind(app);
    app.renderNavigation = () => {
        const nav = renderNavigation();
        if (app.canReviewVersions) {
            nav.append(app.navButton("Version Review", "version-review"));
        }
        return nav;
    };

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        if (app.activeView === "version-review") {
            await renderVersionReview(app, container);
            return;
        }
        await renderActiveView(container);
    };

    const renderGlobalConcept = app.renderGlobalConcept.bind(app);
    app.renderGlobalConcept = async (container, conceptId) => {
        await renderGlobalConcept(container, conceptId);
        try {
            const consolidation = await app.api.getRuleConsolidation(conceptId);
            container.append(renderConsolidationCard(app, consolidation, container, conceptId));
        } catch (error) {
            container.append(alertNode("warning", `Version consolidation unavailable: ${describeError(error)}`));
        }
    };
}

async function renderVersionReview(app, container) {
    clear(container);
    const intro = element("div", { className: "card card-body mb-3" });
    intro.append(
        element("h3", { className: "h5 mb-1", text: "Cross-version review" }),
        element("p", {
            className: "text-body-secondary mb-0",
            text: "Find likely versions of the same conceptual rule across publications, playtests, D&D editions, and source histories. Suggestions never bind, merge, or publish automatically."
        }));
    container.append(intro);

    const searchCard = element("div", { className: "card card-body mb-3" });
    const form = element("form", { className: "row g-2 align-items-end" });
    const query = element("input", { type: "search", className: "form-control", placeholder: "Rule name, source, work, or release" });
    const type = element("input", { type: "text", className: "form-control", placeholder: "Optional entity type" });
    const results = element("div", { className: "mt-3" });
    form.append(
        field("Search", query, "col-md-7"),
        field("Entity type", type, "col-md-3"),
        element("div", { className: "col-md-2" }, element("button", { type: "submit", className: "btn btn-primary w-100", text: "Search" })));
    searchCard.append(form, results);
    container.append(searchCard);

    form.addEventListener("submit", async event => {
        event.preventDefault();
        results.replaceChildren(element("div", { className: "text-body-secondary", text: "Searching…" }));
        try {
            const sources = await app.api.searchSourceEntities({
                entityType: type.value.trim() || null,
                query: query.value.trim() || null,
                limit: 100
            });
            renderSourceSearchResults(app, results, sources);
        } catch (error) {
            results.replaceChildren(alertNode("danger", describeError(error)));
        }
    });
}

function renderSourceSearchResults(app, container, sources) {
    clear(container);
    if (!sources.length) {
        container.append(alertNode("secondary", "No accessible source entities matched."));
        return;
    }

    const list = element("div", { className: "list-group" });
    for (const source of sources) {
        const row = element("div", { className: "list-group-item d-flex flex-wrap justify-content-between gap-2 align-items-center" });
        row.append(
            element("div", {},
                element("div", { className: "fw-semibold", text: source.name }),
                element("div", { className: "small text-body-secondary", text: `${source.entityType} · ${source.sourceCode} · ${source.workDisplayName} · ${source.editionDisplayName}` })),
            element("button", {
                type: "button",
                className: "btn btn-sm btn-outline-primary",
                text: "Review versions",
                onClick: async () => renderDetection(app, container, source.entityId)
            }));
        list.append(row);
    }
    container.append(list);
}

async function renderDetection(app, container, sourceEntityId) {
    clear(container);
    container.append(element("div", { className: "text-body-secondary", text: "Detecting candidate versions…" }));
    try {
        const detection = await app.api.detectSourceVersions(sourceEntityId);
        clear(container);
        container.append(sourceIdentityCard("Source under review", detection.source));
        if (!detection.candidates.length) {
            container.append(alertNode("secondary", "No likely accessible versions met the current detection threshold. This does not prove that no related version exists."));
            return;
        }

        for (const candidate of detection.candidates) {
            const card = element("div", { className: "card card-body mt-2" });
            const top = element("div", { className: "d-flex flex-wrap justify-content-between gap-2" });
            top.append(
                element("div", {},
                    element("h4", { className: "h6 mb-1", text: candidate.candidate.name }),
                    element("div", { className: "small text-body-secondary", text: describeSource(candidate.candidate) })),
                badge(`${candidate.confidence}% candidate`, candidate.confidence >= 75 ? "success" : (candidate.confidence >= 50 ? "warning" : "secondary")));
            card.append(top);

            const reasons = element("ul", { className: "small mt-2 mb-2" });
            for (const reason of candidate.reasons) reasons.append(element("li", { text: reason }));
            card.append(reasons);

            const actions = element("div", { className: "d-flex flex-wrap gap-2 align-items-end" });
            appendBindingActions(app, actions, detection.source, candidate.candidate, sourceEntityId, container);
            actions.append(lineageControls(app, detection.source, candidate.candidate, async () => renderDetection(app, container, sourceEntityId)));
            card.append(actions);
            container.append(card);
        }
    } catch (error) {
        container.replaceChildren(alertNode("danger", describeError(error)));
    }
}

function appendBindingActions(app, actions, source, candidate, sourceEntityId, container) {
    const sourceConcepts = source.boundConcepts ?? [];
    const candidateConcepts = candidate.boundConcepts ?? [];

    for (const concept of candidateConcepts) {
        if (sourceConcepts.some(value => value.id === concept.id)) continue;
        actions.append(actionButton(`Bind source to ${concept.displayName}`, async button => {
            await app.api.bindDetectedSourceVersion(source.sourceEntityId, concept.id);
            await renderDetection(app, container, sourceEntityId);
        }));
    }
    for (const concept of sourceConcepts) {
        if (candidateConcepts.some(value => value.id === concept.id)) continue;
        actions.append(actionButton(`Bind candidate to ${concept.displayName}`, async button => {
            await app.api.bindDetectedSourceVersion(candidate.sourceEntityId, concept.id);
            await renderDetection(app, container, sourceEntityId);
        }));
    }

    if (!sourceConcepts.length && !candidateConcepts.length) {
        actions.append(actionButton("Create/reuse concept from source", async button => {
            await app.api.acceptSourceNormalization(source.sourceEntityId);
            await renderDetection(app, container, sourceEntityId);
        }));
    }
}

function lineageControls(app, source, candidate, refresh) {
    const wrap = element("div", { className: "d-flex flex-wrap gap-2 align-items-end" });
    const direction = element("select", { className: "form-select form-select-sm" });
    direction.append(
        option("source-to-candidate", "Source → candidate"),
        option("candidate-to-source", "Candidate → source"));
    const kind = element("select", { className: "form-select form-select-sm" });
    for (const value of LINEAGE_KINDS) kind.append(option(value, value));
    const note = element("input", { type: "text", className: "form-control form-control-sm", placeholder: "Optional lineage note" });
    const button = actionButton("Record lineage", async () => {
        const reverse = direction.value === "candidate-to-source";
        await app.api.createSourceLineage({
            fromSourceEntityId: reverse ? candidate.sourceEntityId : source.sourceEntityId,
            toSourceEntityId: reverse ? source.sourceEntityId : candidate.sourceEntityId,
            relationshipKind: kind.value,
            note: note.value.trim() || null
        });
        await refresh();
    });
    wrap.append(direction, kind, note, button);
    return wrap;
}

function renderConsolidationCard(app, model, container, conceptId) {
    const card = element("div", { className: "card card-body mt-3" });
    card.append(
        element("h3", { className: "h5 mb-1", text: "Version comparison and consolidation" }),
        element("p", { className: "text-body-secondary", text: "Compare every accessible implementation bound to this concept. Choose one exact revision as the base, then record any other revisions deliberately incorporated or reviewed. Saving creates an append-only decision; publication remains separate." }));

    if (model.latestDecision?.note?.startsWith("Auto-resolved:")) {
        card.append(alertNode(
            "success",
            "Automatically resolved: the bound editions have the same rule-bearing content. Publication remains a separate action."));
    }
    if (model.latestDecision?.note?.startsWith("Auto-resolved:")) {
        card.append(alertNode(
            "success",
            "Automatically resolved: the bound editions have the same rule-bearing content. Publication remains a separate action."));
    }
    if (model.restrictedBindingCount > 0) {
        card.append(alertNode("warning", `${model.restrictedBindingCount} bound source implementation(s) are hidden because this account lacks source access.`));
    }
    if (!model.sources.length) {
        card.append(alertNode("secondary", "No accessible source implementations are bound to this concept."));
        return card;
    }

    const revisions = flattenRevisions(model.sources);
    card.append(renderLineageSection(app, model, conceptId, container));
    card.append(renderComparisonSection(revisions));
    card.append(renderDecisionSection(app, model, revisions, conceptId, container));
    return card;
}

function renderLineageSection(app, model, conceptId, container) {
    const section = element("div", { className: "border-top pt-3 mt-3" });
    section.append(element("h4", { className: "h6", text: "Confirmed source lineage" }));
    const sourceById = new Map(model.sources.map(value => [value.sourceEntityId, value]));
    if (!model.lineage.length) {
        section.append(element("div", { className: "small text-body-secondary mb-2", text: "No lineage is recorded among these accessible implementations." }));
    } else {
        const list = element("div", { className: "list-group list-group-flush mb-2" });
        for (const lineage of model.lineage) {
            const from = sourceById.get(lineage.fromSourceEntityId);
            const to = sourceById.get(lineage.toSourceEntityId);
            if (!from || !to) continue;
            const row = element("div", { className: "list-group-item px-0 d-flex justify-content-between gap-2" });
            row.append(
                element("div", { className: "small", text: `${from.name} → ${to.name} · ${lineage.relationshipKind}${lineage.note ? ` · ${lineage.note}` : ""}` }),
                actionButton("Void", async () => {
                    const reason = window.prompt("Reason for voiding this lineage record (optional):", "");
                    if (reason === null) return;
                    await app.api.voidSourceLineage(lineage.id, reason.trim() || null);
                    await app.renderGlobalConcept(container, conceptId);
                }, "btn-outline-danger"));
            list.append(row);
        }
        section.append(list);
    }

    if (model.sources.length >= 2) {
        const row = element("div", { className: "row g-2 align-items-end" });
        const from = sourceSelect(model.sources);
        const to = sourceSelect(model.sources);
        if (to.options.length > 1) to.selectedIndex = 1;
        const kind = element("select", { className: "form-select" });
        for (const value of LINEAGE_KINDS) kind.append(option(value, value));
        const note = element("input", { type: "text", className: "form-control", placeholder: "Optional note" });
        row.append(field("From", from, "col-lg-3"), field("To", to, "col-lg-3"), field("Relationship", kind, "col-lg-2"), field("Note", note, "col-lg-3"),
            element("div", { className: "col-lg-1" }, actionButton("Add", async () => {
                if (from.value === to.value) throw new Error("Lineage endpoints must be different source entities.");
                await app.api.createSourceLineage({
                    fromSourceEntityId: from.value,
                    toSourceEntityId: to.value,
                    relationshipKind: kind.value,
                    note: note.value.trim() || null
                });
                await app.renderGlobalConcept(container, conceptId);
            })));
        section.append(row);
    }
    return section;
}

function renderComparisonSection(revisions) {
    const section = element("div", { className: "border-top pt-3 mt-3" });
    section.append(element("h4", { className: "h6", text: "Side-by-side source comparison" }));
    const selectors = element("div", { className: "row g-2 mb-2" });
    const left = revisionSelect(revisions);
    const right = revisionSelect(revisions);
    if (right.options.length > 1) right.selectedIndex = 1;
    selectors.append(field("Left source revision", left, "col-md-6"), field("Right source revision", right, "col-md-6"));
    const documents = element("div", { className: "row g-2" });
    const leftPre = element("pre", { className: "col-md-6 small border rounded p-2 overflow-auto", attributes: { style: "max-height:32rem" } });
    const rightPre = element("pre", { className: "col-md-6 small border rounded p-2 overflow-auto", attributes: { style: "max-height:32rem" } });
    const render = () => {
        leftPre.textContent = formatJson(revisions.find(value => value.revision.id === left.value)?.revision.document ?? {});
        rightPre.textContent = formatJson(revisions.find(value => value.revision.id === right.value)?.revision.document ?? {});
    };
    left.addEventListener("change", render);
    right.addEventListener("change", render);
    render();
    documents.append(leftPre, rightPre);
    section.append(selectors, documents);
    return section;
}

function renderDecisionSection(app, model, revisions, conceptId, container) {
    const section = element("div", { className: "border-top pt-3 mt-3" });
    section.append(element("h4", { className: "h6", text: "Manual consolidation decision" }));
    const base = revisionSelect(revisions);
    if (model.latestDecision) {
        const current = Array.from(base.options).find(value => value.value === model.latestDecision.sourceEntityRevisionId);
        if (current) base.value = current.value;
    }
    const mode = element("select", { className: "form-select" });
    mode.append(option("select-source", "Select exact source"), option("json-merge-patch", "JSON merge patch"), option("json-rule-patch", "Structured rule patch"));
    const patch = element("textarea", { className: "form-control font-monospace", rows: 10, placeholder: "Patch JSON (required for patch modes)", attributes: { spellcheck: "false" } });
    const note = element("textarea", { className: "form-control", rows: 2, placeholder: "Adjudication note" });
    const contributionBox = element("div", { className: "mt-2" });
    section.append(field("Base source revision", base), field("Decision mode", mode), field("Patch", patch), field("Decision note", note));
    section.append(element("h5", { className: "h6 mt-3", text: "Additional source provenance" }),
        element("p", { className: "small text-body-secondary", text: "Mark other exact revisions as incorporated when they influenced the resolved rule, or reference when they were deliberately reviewed as context." }), contributionBox);

    const renderContributions = () => {
        clear(contributionBox);
        for (const item of revisions.filter(value => value.revision.id !== base.value)) {
            const row = element("div", { className: "row g-2 align-items-center border-top py-2 contribution-row", attributes: { "data-revision-id": item.revision.id } });
            const enabled = element("input", { type: "checkbox", className: "form-check-input" });
            const kind = element("select", { className: "form-select form-select-sm" });
            kind.append(option("incorporated", "incorporated"), option("reference", "reference"));
            const contributionNote = element("input", { type: "text", className: "form-control form-control-sm", placeholder: "Optional provenance note" });
            const old = (model.latestContributions ?? []).find(value => value.sourceEntityRevisionId === item.revision.id);
            if (old) {
                enabled.checked = true;
                kind.value = old.contributionKind;
                contributionNote.value = old.note ?? "";
            }
            row.append(
                element("div", { className: "col-auto" }, enabled),
                element("div", { className: "col-md-4 small", text: revisionLabel(item) }),
                element("div", { className: "col-md-2" }, kind),
                element("div", { className: "col" }, contributionNote));
            contributionBox.append(row);
        }
    };
    base.addEventListener("change", renderContributions);
    renderContributions();

    const output = element("div", { className: "mt-3" });
    const buttons = element("div", { className: "d-flex gap-2 mt-3" });
    buttons.append(
        actionButton("Preview consolidated rule", async button => {
            setButtonBusy(button, true, "Previewing…");
            try {
                const payload = decisionPayload(base, mode, patch, note, contributionBox);
                const preview = await app.api.previewGlobalDecision(conceptId, payload);
                renderDecisionPreview(output, preview);
            } finally {
                setButtonBusy(button, false);
            }
        }, "btn-outline-primary"),
        actionButton("Save consolidated decision", async button => {
            setButtonBusy(button, true, "Saving…");
            try {
                const payload = decisionPayload(base, mode, patch, note, contributionBox);
                const saved = await app.api.saveGlobalDecision(conceptId, payload);
                window.alert(saved.created ? `Saved global decision #${saved.decisionNumber}. Publish separately when ready.` : `Decision #${saved.decisionNumber} is already current.`);
                await app.renderGlobalConcept(container, conceptId);
            } finally {
                setButtonBusy(button, false);
            }
        }, "btn-primary"));
    section.append(buttons, output);
    return section;
}

function decisionPayload(base, mode, patch, note, contributionBox) {
    const payload = {
        sourceEntityRevisionId: base.value,
        note: note.value.trim() || null,
        mergePatch: null,
        structuredPatch: null,
        contributions: []
    };
    if (mode.value !== "select-source") {
        const raw = patch.value.trim();
        if (!raw) throw new Error("Patch JSON is required for the selected decision mode.");
        let parsed;
        try { parsed = JSON.parse(raw); } catch (error) { throw new Error(`Patch JSON is invalid: ${error.message}`); }
        if (mode.value === "json-merge-patch") payload.mergePatch = parsed;
        else payload.structuredPatch = parsed;
    }

    for (const row of contributionBox.querySelectorAll(".contribution-row")) {
        const enabled = row.querySelector('input[type="checkbox"]');
        if (!enabled?.checked) continue;
        const kind = row.querySelector("select");
        const contributionNote = row.querySelector('input[type="text"]');
        payload.contributions.push({
            sourceEntityRevisionId: row.dataset.revisionId,
            contributionKind: kind.value,
            note: contributionNote.value.trim() || null
        });
    }
    return payload;
}

function renderDecisionPreview(container, preview) {
    clear(container);
    const candidate = preview.candidateDocument ?? preview.candidateResolvedDocument ?? preview.document;
    container.append(element("div", { className: "card card-body bg-body-tertiary" },
        element("h5", { className: "h6", text: "Preview result" }),
        element("div", { className: "small text-body-secondary mb-2", text: `${preview.changes?.length ?? 0} structural change(s). Contribution provenance is saved with the decision but does not alter patch semantics.` }),
        element("pre", { className: "small overflow-auto mb-0", text: formatJson(candidate ?? preview), attributes: { style: "max-height:32rem" } })));
}

function flattenRevisions(sources) {
    return sources.flatMap(source => source.revisions.map(revision => ({ source, revision })));
}

function revisionSelect(revisions) {
    const select = element("select", { className: "form-select" });
    for (const item of revisions) select.append(option(item.revision.id, revisionLabel(item)));
    return select;
}

function sourceSelect(sources) {
    const select = element("select", { className: "form-select" });
    for (const source of sources) select.append(option(source.sourceEntityId, `${source.name} · ${source.gameEdition ?? "edition unknown"} · ${source.sourceCode}`));
    return select;
}

function revisionLabel(item) {
    const source = item.source;
    return `${source.name} · ${source.gameEdition ?? "edition unknown"} · ${source.releaseKind ?? "release unspecified"} · ${source.sourceCode} · revision ${item.revision.revisionNumber}`;
}

function sourceIdentityCard(title, source) {
    return element("div", { className: "card card-body mb-2" },
        element("h4", { className: "h6 mb-1", text: title }),
        element("div", { className: "fw-semibold", text: source.name }),
        element("div", { className: "small text-body-secondary", text: describeSource(source) }),
        element("div", { className: "small", text: (source.boundConcepts ?? []).length ? `Bound concepts: ${source.boundConcepts.map(value => value.key).join(", ")}` : "Not yet bound to a rule concept." }));
}

function describeSource(source) {
    return [source.gameEdition, source.releaseKind, source.publicationDate, source.workDisplayName, source.editionDisplayName, source.sourceCode]
        .filter(Boolean).join(" · ");
}

function actionButton(text, action, className = "btn-outline-secondary") {
    return element("button", {
        type: "button",
        className: `btn btn-sm ${className}`,
        text,
        onClick: async event => {
            const button = event.currentTarget;
            setButtonBusy(button, true, "Working…");
            try { await action(button); }
            catch (error) { window.alert(describeError(error)); }
            finally { setButtonBusy(button, false); }
        }
    });
}

function field(label, control, columnClass = null) {
    const group = element("div", { className: columnClass ?? "mb-2" });
    group.append(element("label", { className: "form-label fw-semibold", text: label }), control);
    return group;
}

function option(value, text) { return element("option", { value, text }); }
