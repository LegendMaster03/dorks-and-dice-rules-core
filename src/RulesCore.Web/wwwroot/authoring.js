import {
    alertNode,
    badge,
    clear,
    codeBlock,
    definitionList,
    describeError,
    element,
    formatDate,
    formatJson,
    setButtonBusy
} from "./ui.js";

const RULES_LAWYER_ROLE = "Rules Lawyer";
const CAMPAIGN_DM_ROLE = "DM";
const DORKS_MODE = "dorks-and-dice";

export class RulesAuthoringApp {
    constructor(root, api, hostContext, session, campaigns) {
        this.root = root;
        this.api = api;
        this.hostContext = hostContext;
        this.session = session;
        this.campaigns = Array.isArray(campaigns) ? campaigns : [];
        this.dmCampaigns = this.campaigns.filter(value => value.role === CAMPAIGN_DM_ROLE);
        this.canEditGlobal = hostContext.siteMode === DORKS_MODE
            && (session.globalRoles ?? []).includes(RULES_LAWYER_ROLE);
        this.canEditCampaign = hostContext.siteMode === DORKS_MODE && this.dmCampaigns.length > 0;
        this.activeView = this.canEditGlobal ? "global" : (this.canEditCampaign ? "campaign" : "none");
        this.activeCampaignId = this.dmCampaigns[0]?.id ?? null;
    }

    async render() {
        clear(this.root);
        this.root.classList.add("rules-core-app");
        this.root.append(this.renderHeader());

        if (this.activeView === "none") {
            this.root.append(alertNode(
                "secondary",
                "This account does not currently have Rules Lawyer authority or DM authority for an enabled campaign in Dorks & Dice mode."));
            return;
        }

        const nav = this.renderNavigation();
        const body = element("div", { className: "mt-3" });
        this.root.append(nav, body);
        await this.renderActiveView(body);
    }

    renderHeader() {
        const card = element("div", { className: "card card-body" });
        const row = element("div", {
            className: "d-flex flex-wrap align-items-start justify-content-between gap-2"
        });

        const title = element("div");
        title.append(
            element("h2", { className: "h4 mb-1", text: "Rules Core" }),
            element("p", {
                className: "text-body-secondary mb-0",
                text: "Browse, preview, save, and publish rule decisions."
            }));

        const user = element("div", { className: "text-end small" });
        user.append(
            element("div", {
                className: "fw-semibold",
                text: this.session.user?.displayName ?? "Signed-in user"
            }),
            element("div", {
                className: "text-body-secondary",
                text: `Mode: ${this.hostContext.siteMode}`
            }));

        row.append(title, user);
        card.append(row);
        return card;
    }

    renderNavigation() {
        const nav = element("div", { className: "d-flex flex-wrap gap-2 align-items-center" });

        if (this.canEditGlobal) {
            nav.append(this.navButton("Global Rules", "global"));
        }
        if (this.canEditCampaign) {
            nav.append(this.navButton("Campaign Rules", "campaign"));
        }

        return nav;
    }

    navButton(label, view) {
        return element("button", {
            type: "button",
            className: `btn ${this.activeView === view ? "btn-primary" : "btn-outline-primary"}`,
            text: label,
            onClick: async () => {
                this.activeView = view;
                await this.render();
            }
        });
    }

    async renderActiveView(container) {
        clear(container);
        container.append(this.loadingCard("Loading authoring state…"));
        try {
            if (this.activeView === "global") {
                await this.renderGlobalOverview(container);
            } else {
                await this.renderCampaignOverview(container);
            }
        } catch (error) {
            clear(container);
            container.append(alertNode("danger", describeError(error)));
        }
    }

    async renderGlobalOverview(container) {
        const overview = await this.api.getGlobalAuthoringOverview();
        clear(container);

        const summary = element("div", { className: "card card-body mb-3" });
        const top = element("div", {
            className: "d-flex flex-wrap justify-content-between align-items-start gap-3"
        });
        const publication = overview.latestPublishedRuleset;

        const metrics = element("div");
        metrics.append(
            element("h3", { className: "h5 mb-2", text: "Global authoring" }),
            definitionList([
                ["Concepts", String(overview.conceptCount)],
                ["With decisions", String(overview.conceptsWithDecisions)],
                ["Pending decisions", String(overview.pendingDecisionCount)],
                ["Published revision", publication ? `#${publication.revisionNumber}` : "None"]
            ]));

        const publish = element("button", {
            type: "button",
            className: "btn btn-success",
            text: overview.pendingDecisionCount > 0 ? "Publish pending rules" : "Publish ruleset",
            onClick: async event => this.publishGlobal(event.currentTarget, container)
        });
        top.append(metrics, publish);
        summary.append(top);
        container.append(summary);

        if (!overview.concepts.length) {
            container.append(alertNode(
                "secondary",
                "No rule concepts exist yet. Concept creation and source binding are not exposed in this UI slice."));
            return;
        }

        const table = element("table", { className: "table table-hover align-middle mb-0" });
        const head = element("thead");
        const headRow = element("tr");
        for (const text of ["Rule", "Type", "Bindings", "Decision", "State", ""]) {
            headRow.append(element("th", { text }));
        }
        head.append(headRow);

        const body = element("tbody");
        for (const concept of overview.concepts) {
            const row = element("tr");
            const nameCell = element("td");
            nameCell.append(
                element("div", { className: "fw-semibold", text: concept.displayName }),
                element("div", {
                    className: "small text-body-secondary font-monospace",
                    text: concept.key
                }));
            row.append(nameCell);
            row.append(element("td", { text: concept.entityType }));
            row.append(element("td", { text: String(concept.bindingCount) }));
            row.append(element("td", {
                text: concept.latestDecisionNumber
                    ? `#${concept.latestDecisionNumber} · ${concept.latestDecisionKind}`
                    : "—"
            }));

            const stateCell = element("td");
            if (concept.hasUnpublishedChanges) {
                stateCell.append(badge("Pending", "warning"));
            } else if (concept.latestDecisionId) {
                stateCell.append(badge("Published", "success"));
            } else {
                stateCell.append(badge("Needs decision", "secondary"));
            }
            row.append(stateCell);

            const actionCell = element("td", { className: "text-end" });
            actionCell.append(element("button", {
                type: "button",
                className: "btn btn-sm btn-outline-primary",
                text: "Open",
                onClick: async () => this.renderGlobalConcept(container, concept.id)
            }));
            row.append(actionCell);
            body.append(row);
        }

        table.append(head, body);
        container.append(element("div", { className: "card" },
            element("div", { className: "table-responsive" }, table)));
    }

    async renderGlobalConcept(container, conceptId) {
        clear(container);
        container.append(this.backButton(async () => this.renderGlobalOverview(container)));
        const loading = this.loadingCard("Loading rule concept…");
        container.append(loading);

        try {
            const detail = await this.api.getGlobalAuthoringConcept(conceptId);
            loading.remove();
            container.append(this.renderConceptSummary(detail, "global"));
            container.append(this.createDecisionEditor("global", detail, container));
        } catch (error) {
            loading.remove();
            container.append(alertNode("danger", describeError(error)));
        }
    }

    async publishGlobal(button, container) {
        setButtonBusy(button, true, "Publishing…");
        try {
            const result = await this.api.publishGlobalRules();
            window.alert(result.createdRevision === false
                ? `Global ruleset revision #${result.revisionNumber} is already current.`
                : `Published global ruleset revision #${result.revisionNumber}.`);
            await this.renderGlobalOverview(container);
        } catch (error) {
            window.alert(describeError(error));
        } finally {
            setButtonBusy(button, false);
        }
    }

    async renderCampaignOverview(container) {
        clear(container);
        container.append(this.renderCampaignSelector(container));

        if (!this.activeCampaignId) {
            container.append(alertNode("secondary", "No DM campaigns are available."));
            return;
        }

        const loading = this.loadingCard("Loading campaign authoring state…");
        container.append(loading);
        const overview = await this.api.getCampaignAuthoringOverview(this.activeCampaignId);
        loading.remove();

        const campaign = this.dmCampaigns.find(value => value.id === this.activeCampaignId);
        const baseline = overview.selectedBaseline;
        const published = overview.latestPublishedRuleset;
        const summary = element("div", { className: "card card-body mb-3" });
        const top = element("div", {
            className: "d-flex flex-wrap justify-content-between align-items-start gap-3"
        });

        const metrics = element("div");
        metrics.append(
            element("h3", { className: "h5 mb-2", text: campaign?.name ?? "Campaign" }),
            definitionList([
                ["Selected global baseline", baseline ? `#${baseline.rulesetRevisionNumber}` : "None"],
                ["Published campaign revision", published ? `#${published.revisionNumber}` : "None"],
                ["Concepts", String(overview.conceptCount)],
                ["Overrides", String(overview.conceptsWithOverrides)],
                ["Pending overrides", String(overview.pendingOverrideCount)]
            ]));

        const actions = element("div", { className: "d-flex flex-column gap-2 align-items-end" });
        if (overview.hasUnpublishedBaselineChange) {
            actions.append(badge("Baseline migration pending", "warning"));
        }
        actions.append(overview.needsPublication
            ? badge("Publication required", "warning")
            : badge("Published", "success"));
        actions.append(element("button", {
            type: "button",
            className: "btn btn-success mt-1",
            text: "Publish campaign rules",
            disabled: !baseline,
            onClick: async event => this.publishCampaign(event.currentTarget, container)
        }));

        top.append(metrics, actions);
        summary.append(top);
        container.append(summary);

        if (!baseline) {
            container.append(alertNode(
                "info",
                "This campaign has not selected a global ruleset baseline yet. Baseline discovery and migration selection are not exposed in this UI slice."));
            return;
        }

        const table = element("table", { className: "table table-hover align-middle mb-0" });
        const head = element("thead");
        const headRow = element("tr");
        for (const text of ["Rule", "Global baseline", "Campaign override", "State", ""]) {
            headRow.append(element("th", { text }));
        }
        head.append(headRow);
        const body = element("tbody");

        for (const concept of overview.concepts) {
            const row = element("tr");
            const nameCell = element("td");
            nameCell.append(
                element("div", { className: "fw-semibold", text: concept.displayName }),
                element("div", {
                    className: "small text-body-secondary font-monospace",
                    text: concept.key
                }));
            row.append(nameCell);
            row.append(element("td", {
                text: `#${concept.baselineGlobalDecisionNumber} · ${concept.baselineGlobalDecisionKind}`
            }));
            row.append(element("td", {
                text: concept.latestCampaignDecisionNumber
                    ? `#${concept.latestCampaignDecisionNumber} · ${concept.latestCampaignDecisionKind}`
                    : "Inherit global"
            }));
            const state = element("td");
            state.append(concept.hasUnpublishedOverrideChange
                ? badge("Pending", "warning")
                : badge("Published", "success"));
            row.append(state);
            const action = element("td", { className: "text-end" });
            action.append(element("button", {
                type: "button",
                className: "btn btn-sm btn-outline-primary",
                text: "Open",
                onClick: async () => this.renderCampaignConcept(container, concept.id)
            }));
            row.append(action);
            body.append(row);
        }

        table.append(head, body);
        container.append(element("div", { className: "card" },
            element("div", { className: "table-responsive" }, table)));
    }

    renderCampaignSelector(container) {
        const select = element("select", {
            className: "form-select",
            ariaLabel: "Campaign",
            onChange: async event => {
                this.activeCampaignId = event.currentTarget.value;
                await this.renderCampaignOverview(container);
            }
        });

        for (const campaign of this.dmCampaigns) {
            const option = element("option", { value: campaign.id, text: campaign.name });
            option.selected = campaign.id === this.activeCampaignId;
            select.append(option);
        }

        const card = element("div", { className: "card card-body mb-3" });
        card.append(
            element("label", { className: "form-label fw-semibold", text: "Campaign" }),
            select);
        return card;
    }

    async renderCampaignConcept(container, conceptId) {
        clear(container);
        container.append(this.backButton(async () => this.renderCampaignOverview(container)));
        const loading = this.loadingCard("Loading campaign rule concept…");
        container.append(loading);

        try {
            const detail = await this.api.getCampaignAuthoringConcept(this.activeCampaignId, conceptId);
            loading.remove();
            container.append(this.renderConceptSummary(detail, "campaign"));
            container.append(this.createDecisionEditor("campaign", detail, container));
        } catch (error) {
            loading.remove();
            container.append(alertNode("danger", describeError(error)));
        }
    }

    async publishCampaign(button, container) {
        setButtonBusy(button, true, "Publishing…");
        try {
            const result = await this.api.publishCampaignRules(this.activeCampaignId);
            window.alert(result.createdRevision === false
                ? `Campaign ruleset revision #${result.revisionNumber} is already current.`
                : `Published campaign ruleset revision #${result.revisionNumber}.`);
            await this.renderCampaignOverview(container);
        } catch (error) {
            window.alert(describeError(error));
        } finally {
            setButtonBusy(button, false);
        }
    }

    renderConceptSummary(detail, scope) {
        const card = element("div", { className: "card card-body mb-3" });
        const row = element("div", {
            className: "d-flex flex-wrap justify-content-between gap-3"
        });
        const info = element("div");
        info.append(
            element("h3", { className: "h5 mb-1", text: detail.concept.displayName }),
            element("div", {
                className: "font-monospace small text-body-secondary mb-2",
                text: detail.concept.key
            }),
            element("p", {
                className: "mb-0",
                text: `${detail.accessibleSources.length} accessible source(s) · ${detail.restrictedBindingCount} restricted binding(s)`
            }));

        const state = element("div", { className: "d-flex flex-wrap gap-2" });
        if (scope === "global") {
            state.append(detail.hasUnpublishedChanges
                ? badge("Unpublished decision", "warning")
                : badge("No unpublished decision", "success"));
        } else {
            if (detail.hasUnpublishedBaselineChange) {
                state.append(badge("Baseline migration pending", "warning"));
            }
            if (detail.hasUnpublishedOverrideChange) {
                state.append(badge("Override pending", "warning"));
            }
            if (!detail.hasUnpublishedBaselineChange && !detail.hasUnpublishedOverrideChange) {
                state.append(badge("Published", "success"));
            }
        }

        row.append(info, state);
        card.append(row);
        return card;
    }

    createDecisionEditor(scope, detail, container) {
        const current = scope === "global" ? detail.latestDecision : detail.latestCampaignDecision;
        const card = element("div", { className: "card card-body mb-3" });
        card.append(element("h3", {
            className: "h5",
            text: scope === "global" ? "Decision editor" : "Campaign override editor"
        }));

        if (scope === "campaign") {
            card.append(alertNode(
                "secondary",
                `Selected global baseline uses decision #${detail.baselineGlobalDecision.decisionNumber} (${detail.baselineGlobalDecision.decisionKind}).`));
        }

        const mode = element("select", { className: "form-select" });
        const modes = scope === "global"
            ? [
                ["select-source", "Use exact source revision"],
                ["json-merge-patch", "Patch object fields (JSON Merge Patch)"],
                ["json-rule-patch", "Patch fields and arrays (structured rule patch)"]
            ]
            : [
                ["inherit-global", "Inherit selected global rule"],
                ["select-source", "Use a different exact source revision"],
                ["json-merge-patch", "Patch selected global rule (JSON Merge Patch)"],
                ["json-rule-patch", "Patch selected global rule and arrays"]
            ];
        for (const [value, label] of modes) {
            mode.append(element("option", { value, text: label }));
        }

        const source = this.renderSourceRevisionSelect(detail.accessibleSources);
        const note = element("textarea", {
            className: "form-control",
            rows: 2,
            placeholder: "Why is this decision being made?"
        });
        const patch = element("textarea", {
            className: "form-control font-monospace rules-core-patch-input",
            rows: 10
        });
        const patchHelp = element("div", { className: "form-text" });
        const patchGroup = element("div", { className: "mb-3" });
        patchGroup.append(
            element("label", { className: "form-label fw-semibold", text: "Patch JSON" }),
            patch,
            patchHelp);

        const sourceGroup = element("div", { className: "mb-3" });
        sourceGroup.append(
            element("label", { className: "form-label fw-semibold", text: "Source revision" }),
            source);

        const grid = element("div", { className: "row g-3 mb-3" });
        const modeColumn = element("div", { className: "col-lg-6" });
        modeColumn.append(
            element("label", { className: "form-label fw-semibold", text: "Decision" }),
            mode);
        const sourceColumn = element("div", { className: "col-lg-6" });
        sourceColumn.append(sourceGroup);
        grid.append(modeColumn, sourceColumn);

        const noteGroup = element("div", { className: "mb-3" });
        noteGroup.append(
            element("label", { className: "form-label fw-semibold", text: "Decision note" }),
            note);
        card.append(grid, noteGroup, patchGroup);

        const previewButton = element("button", {
            type: "button",
            className: "btn btn-outline-primary",
            text: "Preview"
        });
        const saveButton = element("button", {
            type: "button",
            className: "btn btn-primary",
            text: "Save decision"
        });
        const controls = element("div", { className: "d-flex flex-wrap gap-2" });
        controls.append(previewButton, saveButton);
        const result = element("div", { className: "mt-3" });
        card.append(controls, result);

        this.seedEditor(scope, current, mode, source, note, patch);
        const sync = () => this.syncEditor(scope, mode, source, patch, patchHelp, sourceGroup, patchGroup);
        mode.addEventListener("change", sync);
        sync();

        previewButton.addEventListener("click", async () => {
            await this.runEditorAction(previewButton, saveButton, result, "Previewing…", async () => {
                const payload = scope === "global"
                    ? this.buildGlobalPayload(mode, source, note, patch)
                    : this.buildCampaignPayload(mode, source, note, patch);
                const preview = scope === "global"
                    ? await this.api.previewGlobalDecision(detail.concept.id, payload)
                    : await this.api.previewCampaignDecision(detail.campaignId, detail.concept.id, payload);
                this.renderPreview(result, preview);
            });
        });

        saveButton.addEventListener("click", async () => {
            await this.runEditorAction(previewButton, saveButton, result, "Saving…", async () => {
                const payload = scope === "global"
                    ? this.buildGlobalPayload(mode, source, note, patch)
                    : this.buildCampaignPayload(mode, source, note, patch);
                const saved = scope === "global"
                    ? await this.api.saveGlobalDecision(detail.concept.id, payload)
                    : await this.api.saveCampaignDecision(detail.campaignId, detail.concept.id, payload);
                window.alert(saved.created === false
                    ? "The latest decision already matches this candidate."
                    : `Saved decision #${saved.decisionNumber}. It is not active until publication.`);
                if (scope === "global") {
                    await this.renderGlobalConcept(container, detail.concept.id);
                } else {
                    await this.renderCampaignConcept(container, detail.concept.id);
                }
            });
        });

        return card;
    }

    renderSourceRevisionSelect(sources) {
        const select = element("select", { className: "form-select" });
        select.append(element("option", {
            value: "",
            text: sources.length ? "Select source revision…" : "No accessible source revisions"
        }));

        for (const source of sources) {
            const group = element("optgroup", {
                attributes: {
                    label: `${source.name} · ${source.editionDisplayName} · ${source.sourceCode}`
                }
            });
            for (const revision of source.revisions) {
                group.append(element("option", {
                    value: revision.id,
                    text: `Revision ${revision.revisionNumber} · ${shortFingerprint(revision.fingerprint)} · ${formatDate(revision.importedAt)}`
                }));
            }
            select.append(group);
        }
        return select;
    }

    seedEditor(scope, current, mode, source, note, patch) {
        mode.value = current?.decisionKind ?? (scope === "global" ? "select-source" : "inherit-global");
        source.value = current?.sourceEntityRevisionId ?? "";
        note.value = current?.note ?? "";
        if (current?.mergePatch) {
            patch.value = formatJson(current.mergePatch);
        } else if (current?.structuredPatch) {
            patch.value = formatJson(current.structuredPatch);
        }
    }

    syncEditor(scope, mode, source, patch, patchHelp, sourceGroup, patchGroup) {
        const kind = mode.value;
        const needsSource = scope === "global" || kind === "select-source";
        source.disabled = !needsSource;
        sourceGroup.classList.toggle("opacity-50", !needsSource);

        const needsPatch = kind === "json-merge-patch" || kind === "json-rule-patch";
        patch.disabled = !needsPatch;
        patchGroup.classList.toggle("d-none", !needsPatch);

        if (kind === "json-merge-patch") {
            patch.placeholder = '{\n  "field": "replacement",\n  "removeMe": null\n}';
            patchHelp.textContent = "Object merge/delete semantics. Arrays are replaced wholesale.";
        } else if (kind === "json-rule-patch") {
            patch.placeholder = '{\n  "mergePatch": { "field": "replacement" },\n  "arrayOperations": [\n    { "operation": "append", "path": "/items", "value": "new item" }\n  ]\n}';
            patchHelp.textContent = "Structured rule patch with optional mergePatch and arrayOperations.";
        }
    }

    buildGlobalPayload(mode, source, note, patch) {
        if (!source.value) {
            throw new Error("Select an accessible source revision.");
        }
        const payload = {
            sourceEntityRevisionId: source.value,
            note: note.value.trim() || null
        };
        if (mode.value === "json-merge-patch") {
            payload.mergePatch = parseRequiredJson(patch.value, "Merge patch");
        } else if (mode.value === "json-rule-patch") {
            payload.structuredPatch = parseRequiredJson(patch.value, "Structured patch");
        }
        return payload;
    }

    buildCampaignPayload(mode, source, note, patch) {
        const payload = {
            decisionKind: mode.value,
            sourceEntityRevisionId: null,
            note: note.value.trim() || null
        };
        if (mode.value === "select-source") {
            if (!source.value) {
                throw new Error("Select an accessible source revision.");
            }
            payload.sourceEntityRevisionId = source.value;
        } else if (mode.value === "json-merge-patch") {
            payload.mergePatch = parseRequiredJson(patch.value, "Merge patch");
        } else if (mode.value === "json-rule-patch") {
            payload.structuredPatch = parseRequiredJson(patch.value, "Structured patch");
        }
        return payload;
    }

    async runEditorAction(previewButton, saveButton, result, busyText, action) {
        result.replaceChildren();
        setButtonBusy(previewButton, true, busyText);
        setButtonBusy(saveButton, true, busyText);
        try {
            await action();
        } catch (error) {
            result.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(previewButton, false);
            setButtonBusy(saveButton, false);
        }
    }

    renderPreview(container, preview) {
        const card = element("div", { className: "card card-body border-primary" });
        const header = element("div", {
            className: "d-flex flex-wrap justify-content-between gap-2 align-items-center mb-2"
        });
        header.append(
            element("h4", { className: "h6 mb-0", text: "Preview" }),
            badge(preview.decisionKind, "primary"));
        card.append(header);
        card.append(element("div", {
            className: "small text-body-secondary mb-3",
            text: `${preview.changes.length} structural change${preview.changes.length === 1 ? "" : "s"}`
        }));

        if (!preview.changes.length) {
            card.append(element("div", { className: "text-body-secondary", text: "No structural changes." }));
        } else {
            const list = element("div", { className: "list-group list-group-flush" });
            for (const change of preview.changes) {
                const item = element("div", { className: "list-group-item px-0" });
                const title = element("div", { className: "d-flex flex-wrap gap-2 align-items-center mb-1" });
                const kind = change.changeKind === "remove"
                    ? "danger"
                    : (change.changeKind === "add" ? "success" : "warning");
                title.append(badge(change.changeKind, kind), element("code", { text: change.path || "/" }));
                item.append(title);
                if (change.before !== null && change.before !== undefined) {
                    item.append(element("div", { className: "small text-body-secondary", text: "Before" }));
                    item.append(codeBlock(change.before));
                }
                if (change.after !== null && change.after !== undefined) {
                    item.append(element("div", { className: "small text-body-secondary mt-2", text: "After" }));
                    item.append(codeBlock(change.after));
                }
                list.append(item);
            }
            card.append(list);
        }

        const details = element("details", { className: "mt-3" });
        details.append(
            element("summary", { className: "fw-semibold", text: "Resolved candidate JSON" }),
            element("div", { className: "mt-2" }, codeBlock(preview.previewDocument)));
        card.append(details);
        container.replaceChildren(card);
    }

    loadingCard(message) {
        return element("div", { className: "card card-body text-body-secondary", text: message });
    }

    backButton(onClick) {
        return element("button", {
            type: "button",
            className: "btn btn-sm btn-outline-secondary mb-3",
            text: "← Back",
            onClick
        });
    }
}

function parseRequiredJson(value, label) {
    const trimmed = value.trim();
    if (!trimmed) {
        throw new Error(`${label} JSON is required.`);
    }
    try {
        return JSON.parse(trimmed);
    } catch (error) {
        throw new Error(`${label} is not valid JSON: ${error.message}`);
    }
}

function shortFingerprint(value) {
    if (!value) {
        return "no fingerprint";
    }
    return value.length > 12 ? `${value.slice(0, 12)}…` : value;
}
