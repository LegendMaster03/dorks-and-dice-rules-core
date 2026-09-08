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

export class RulesAuthoringApp {
    constructor(root, api, hostContext, session, campaigns) {
        this.root = root;
        this.api = api;
        this.hostContext = hostContext;
        this.session = session;
        this.campaigns = Array.isArray(campaigns) ? campaigns : [];
        this.dmCampaigns = this.campaigns.filter(campaign => campaign.role === CAMPAIGN_DM_ROLE);
        this.canEditGlobal = hostContext.siteMode === "dorks-and-dice"
            && (session.globalRoles ?? []).includes(RULES_LAWYER_ROLE);
        this.activeView = this.canEditGlobal ? "global" : (this.dmCampaigns.length ? "campaign" : "none");
        this.activeCampaignId = this.dmCampaigns[0]?.id ?? null;
    }

    async render() {
        clear(this.root);
        this.root.classList.add("rules-core-app");
        this.root.append(this.renderHeader());

        if (this.activeView === "none") {
            this.root.append(alertNode(
                "secondary",
                "This account can open Rules Core, but it does not currently have Rules Lawyer authority or DM authority for an enabled campaign in Dorks & Dice mode."));
            return;
        }

        const body = element("div", { className: "mt-3" });
        this.root.append(this.renderNavigation(), body);
        await this.renderActiveView(body);
    }

    renderHeader() {
        const title = element("div", { className: "d-flex flex-wrap align-items-start justify-content-between gap-2" },
            element("div", {},
                element("h2", { className: "h4 mb-1", text: "Rules Core" }),
                element("p", {
                    className: "text-body-secondary mb-0",
                    text: "Browse, preview, save, and publish Dorks & Dice rule decisions."
                })),
            element("div", { className: "text-end small" },
                element("div", { className: "fw-semibold", text: this.session.user?.displayName ?? "Signed-in user" }),
                element("div", { className: "text-body-secondary", text: `Mode: ${this.hostContext.siteMode}` })));

        return element("div", { className: "card card-body" }, title);
    }

    renderNavigation() {
        const nav = element("div", { className: "d-flex flex-wrap gap-2 align-items-center" });

        if (this.canEditGlobal) {
            nav.append(element("button", {
                type: "button",
                className: `btn ${this.activeView === "global" ? "btn-primary" : "btn-outline-primary"}`,
                text: "Global Rules",
                onClick: async () => {
                    this.activeView = "global";
                    await this.render();
                }
            }));
        }

        if (this.dmCampaigns.length) {
            nav.append(element("button", {
                type: "button",
                className: `btn ${this.activeView === "campaign" ? "btn-primary" : "btn-outline-primary"}`,
                text: "Campaign Rules",
                onClick: async () => {
                    this.activeView = "campaign";
                    await this.render();
                }
            }));
        }

        return nav;
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

        const publication = overview.latestPublishedRuleset;
        const summary = element("div", { className: "card card-body mb-3" },
            element("div", { className: "d-flex flex-wrap justify-content-between align-items-start gap-3" },
                element("div", {},
                    element("h3", { className: "h5 mb-2", text: "Global authoring" }),
                    definitionList([
                        ["Concepts", String(overview.conceptCount)],
                        ["With decisions", String(overview.conceptsWithDecisions)],
                        ["Pending decisions", String(overview.pendingDecisionCount)],
                        ["Published revision", publication ? `#${publication.revisionNumber}` : "None"]
                    ])),
                element("button", {
                    type: "button",
                    className: "btn btn-success",
                    text: overview.pendingDecisionCount > 0 ? "Publish pending rules" : "Publish ruleset",
                    onClick: async event => this.publishGlobal(event.currentTarget, container)
                })));
        container.append(summary);

        if (!overview.concepts.length) {
            container.append(alertNode(
                "secondary",
                "No rule concepts exist yet. Concept creation and source binding are still backend operations in this UI slice."));
            return;
        }

        const table = element("div", { className: "card" },
            element("div", { className: "table-responsive" },
                this.renderGlobalConceptTable(overview.concepts, container)));
        container.append(table);
    }

    renderGlobalConceptTable(concepts, container) {
        const table = element("table", { className: "table table-hover align-middle mb-0" });
        const head = element("thead", {},
            element("tr", {},
                element("th", { text: "Rule" }),
                element("th", { text: "Type" }),
                element("th", { text: "Bindings" }),
                element("th", { text: "Decision" }),
                element("th", { text: "State" }),
                element("th", { className: "text-end", text: "" })));
        const body = element("tbody");

        for (const concept of concepts) {
            const state = concept.hasUnpublishedChanges
                ? badge("Pending", "warning")
                : concept.latestDecisionId
                    ? badge("Published", "success")
                    : badge("Needs decision", "secondary");
            const decision = concept.latestDecisionNumber
                ? `#${concept.latestDecisionNumber} · ${concept.latestDecisionKind}`
                : "—";

            body.append(element("tr", {},
                element("td", {},
                    element("div", { className: "fw-semibold", text: concept.displayName }),
                    element("div", { className: "small text-body-secondary font-monospace", text: concept.key })),
                element("td", { text: concept.entityType }),
                element("td", { text: String(concept.bindingCount) }),
                element("td", { text: decision }),
                element("td", {}, state),
                element("td", { className: "text-end" },
                    element("button", {
                        type: "button",
                        className: "btn btn-sm btn-outline-primary",
                        text: "Open",
                        onClick: async () => this.renderGlobalConcept(container, concept.id)
                    }))));
        }

        table.append(head, body);
        return table;
    }

    async renderGlobalConcept(container, conceptId) {
        clear(container);
        container.append(this.loadingCard("Loading rule concept…"));

        try {
            const detail = await this.api.getGlobalAuthoringConcept(conceptId);
            clear(container);
            container.append(this.backButton(async () => this.renderGlobalOverview(container)));
            container.append(this.renderConceptSummary(detail, "global"));
            container.append(this.renderGlobalDecisionEditor(detail, container));
        } catch (error) {
            clear(container);
            container.append(this.backButton(async () => this.renderGlobalOverview(container)));
            container.append(alertNode("danger", describeError(error)));
        }
    }

    renderConceptSummary(detail, scope) {
        const concept = detail.concept;
        const sourceSummary = detail.accessibleSources.length
            ? `${detail.accessibleSources.length} accessible source${detail.accessibleSources.length === 1 ? "" : "s"}`
            : "No accessible sources";
        const restricted = detail.restrictedBindingCount
            ? ` · ${detail.restrictedBindingCount} restricted binding${detail.restrictedBindingCount === 1 ? "" : "s"}`
            : "";

        const stateBadges = element("div", { className: "d-flex flex-wrap gap-2" });
        if (scope === "global") {
            stateBadges.append(detail.hasUnpublishedChanges
                ? badge("Unpublished decision", "warning")
                : badge("No unpublished decision", "success"));
        } else {
            if (detail.hasUnpublishedBaselineChange) {
                stateBadges.append(badge("Baseline migration pending", "warning"));
            }
            if (detail.hasUnpublishedOverrideChange) {
                stateBadges.append(badge("Override pending", "warning"));
            }
            if (!detail.hasUnpublishedBaselineChange && !detail.hasUnpublishedOverrideChange) {
                stateBadges.append(badge("Published", "success"));
            }
        }

        return element("div", { className: "card card-body mb-3" },
            element("div", { className: "d-flex flex-wrap justify-content-between gap-3" },
                element("div", {},
                    element("h3", { className: "h5 mb-1", text: concept.displayName }),
                    element("div", { className: "font-monospace small text-body-secondary mb-2", text: concept.key }),
                    element("p", { className: "mb-0", text: `${sourceSummary}${restricted}` })),
                stateBadges));
    }

    renderGlobalDecisionEditor(detail, container) {
        const editor = this.createDecisionEditor({
            scope: "global",
            accessibleSources: detail.accessibleSources,
            currentDecision: detail.latestDecision,
            baselineDecision: null
        });

        editor.previewButton.addEventListener("click", async () => {
            await this.runEditorAction(
                editor,
                "Previewing…",
                () => this.api.previewGlobalDecision(detail.concept.id, this.buildGlobalPayload(editor)),
                preview => this.renderPreview(editor.result, preview));
        });

        editor.saveButton.addEventListener("click", async () => {
            await this.runEditorAction(
                editor,
                "Saving…",
                () => this.api.saveGlobalDecision(detail.concept.id, this.buildGlobalPayload(editor)),
                async decision => {
                    editor.result.replaceChildren(alertNode(
                        "success",
                        decision.created === false
                            ? "The latest decision already matches this candidate."
                            : `Saved decision #${decision.decisionNumber}. It is not active until publication.`));
                    await this.renderGlobalConcept(container, detail.concept.id);
                });
        });

        return editor.card;
    }

    async publishGlobal(button, container) {
        setButtonBusy(button, true, "Publishing…");
        try {
            const result = await this.api.publishGlobalRules();
            const message = result.createdRevision === false
                ? `Global ruleset revision #${result.revisionNumber} is already current.`
                : `Published global ruleset revision #${result.revisionNumber}.`;
            clear(container);
            container.append(alertNode("success", message));
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

        container.append(element("div", { className: "card card-body mb-3" },
            element("div", { className: "d-flex flex-wrap justify-content-between align-items-start gap-3" },
                element("div", {},
                    element("h3", { className: "h5 mb-2", text: campaign?.name ?? "Campaign" }),
                    definitionList([
                        ["Selected global baseline", baseline ? `#${baseline.rulesetRevisionNumber}` : "None"],
                        ["Published campaign revision", published ? `#${published.revisionNumber}` : "None"],
                        ["Concepts", String(overview.conceptCount)],
                        ["Overrides", String(overview.conceptsWithOverrides)],
                        ["Pending overrides", String(overview.pendingOverrideCount)]
                    ])),
                element("div", { className: "d-flex flex-column gap-2 align-items-end" },
                    overview.hasUnpublishedBaselineChange ? badge("Baseline migration pending", "warning") : null,
                    overview.needsPublication ? badge("Publication required", "warning") : badge("Published", "success"),
                    element("button", {
                        type: "button",
                        className: "btn btn-success mt-1",
                        text: "Publish campaign rules",
                        disabled: !baseline,
                        onClick: async event => this.publishCampaign(event.currentTarget, container)
                    }))));

        if (!baseline) {
            container.append(alertNode(
                "info",
                "This campaign has not selected a global ruleset baseline yet. Baseline discovery and migration selection will be the next campaign UI slice."));
            return;
        }

        if (!overview.concepts.length) {
            container.append(alertNode("secondary", "The selected baseline contains no rule concepts."));
            return;
        }

        const table = element("table", { className: "table table-hover align-middle mb-0" });
        table.append(element("thead", {},
            element("tr", {},
                element("th", { text: "Rule" }),
                element("th", { text: "Global baseline" }),
                element("th", { text: "Campaign override" }),
                element("th", { text: "State" }),
                element("th", { className: "text-end", text: "" }))));
        const body = element("tbody");

        for (const concept of overview.concepts) {
            const overrideText = concept.latestCampaignDecisionNumber
                ? `#${concept.latestCampaignDecisionNumber} · ${concept.latestCampaignDecisionKind}`
                : "Inherit global";
            body.append(element("tr", {},
                element("td", {},
                    element("div", { className: "fw-semibold", text: concept.displayName }),
                    element("div", { className: "small text-body-secondary font-monospace", text: concept.key })),
                element("td", { text: `#${concept.baselineGlobalDecisionNumber} · ${concept.baselineGlobalDecisionKind}` }),
                element("td", { text: overrideText }),
                element("td", {}, concept.hasUnpublishedOverrideChange
                    ? badge("Pending", "warning")
                    : badge("Published", "success")),
                element("td", { className: "text-end" },
                    element("button", {
                        type: "button",
                        className: "btn btn-sm btn-outline-primary",
                        text: "Open",
                        onClick: async () => this.renderCampaignConcept(container, concept.id)
                    }))));
        }
        table.append(body);
        container.append(element("div", { className: "card" }, element("div", { className: "table-responsive" }, table)));
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
            if (campaign.id === this.activeCampaignId) {
                option.selected = true;
            }
            select.append(option);
        }

        return element("div", { className: "card card-body mb-3" },
            element("label", { className: "form-label fw-semibold", text: "Campaign" }),
            select);
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
            container.append(this.renderCampaignDecisionEditor(detail, container));
        } catch (error) {
            loading.remove();
            container.append(alertNode("danger", describeError(error)));
        }
    }

    renderCampaignDecisionEditor(detail, container) {
        const editor = this.createDecisionEditor({
            scope: "campaign",
            accessibleSources: detail.accessibleSources,
            currentDecision: detail.latestCampaignDecision,
            baselineDecision: detail.baselineGlobalDecision
        });

        editor.previewButton.addEventListener("click", async () => {
            await this.runEditorAction(
                editor,
                "Previewing…",
                () => this.api.previewCampaignDecision(
                    detail.campaignId,
                    detail.concept.id,
                    this.buildCampaignPayload(editor)),
                preview => this.renderPreview(editor.result, preview));
        });

        editor.saveButton.addEventListener("click", async () => {
            await this.runEditorAction(
                editor,
                "Saving…",
                () => this.api.saveCampaignDecision(
                    detail.campaignId,
                    detail.concept.id,
                    this.buildCampaignPayload(editor)),
                async decision => {
                    editor.result.replaceChildren(alertNode(
                        "success",
                        decision.created === false
                            ? "The latest campaign decision already matches this candidate."
                            : `Saved campaign decision #${decision.decisionNumber}. It is not active until publication.`));
                    await this.renderCampaignConcept(container, detail.concept.id);
                });
        });

        return editor.card;
    }

    async publishCampaign(button, container) {
        setButtonBusy(button, true, "Publishing…");
        try {
            const result = await this.api.publishCampaignRules(this.activeCampaignId);
            const message = result.createdRevision === false
                ? `Campaign ruleset revision #${result.revisionNumber} is already current.`
                : `Published campaign ruleset revision #${result.revisionNumber}.`;
            window.alert(message);
            await this.renderCampaignOverview(container);
        } catch (error) {
            window.alert(describeError(error));
        } finally {
            setButtonBusy(button, false);
        }
    }

    createDecisionEditor({ scope, accessibleSources, currentDecision, baselineDecision }) {
        const form = element("div", { className: "card card-body mb-3" });
        form.append(element("h3", { className: "h5", text: scope === "global" ? "Decision editor" : "Campaign override editor" }));

        if (scope === "campaign" && baselineDecision) {
            form.append(alertNode(
                "secondary",
                `Selected global baseline uses decision #${baselineDecision.decisionNumber} (${baselineDecision.decisionKind}).`));
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

        const source = element("select", { className: "form-select" });
        source.append(element("option", { value: "", text: accessibleSources.length ? "Select source revision…" : "No accessible source revisions" }));
        for (const sourceEntity of accessibleSources) {
            const group = element("optgroup", {
                attributes: { label: `${sourceEntity.name} · ${sourceEntity.editionDisplayName} · ${sourceEntity.sourceCode}` }
            });
            for (const revision of sourceEntity.revisions) {
                group.append(element("option", {
                    value: revision.id,
                    text: `Revision ${revision.revisionNumber} · ${shortFingerprint(revision.fingerprint)} · ${formatDate(revision.importedAt)}`
                }));
            }
            source.append(group);
        }

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
        const patchGroup = element("div", { className: "mb-3" },
            element("label", { className: "form-label fw-semibold", text: "Patch JSON" }),
            patch,
            patchHelp);
        const sourceGroup = element("div", { className: "mb-3" },
            element("label", { className: "form-label fw-semibold", text: "Source revision" }),
            source);

        form.append(
            element("div", { className: "row g-3 mb-3" },
                element("div", { className: "col-lg-6" },
                    element("label", { className: "form-label fw-semibold", text: "Decision" }), mode),
                element("div", { className: "col-lg-6" }, sourceGroup)),
            element("div", { className: "mb-3" },
                element("label", { className: "form-label fw-semibold", text: "Decision note" }), note),
            patchGroup);

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
        const result = element("div", { className: "mt-3" });
        form.append(element("div", { className: "d-flex flex-wrap gap-2" }, previewButton, saveButton), result);

        const syncEditor = () => {
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
        };
        mode.addEventListener("change", syncEditor);

        this.seedEditorFromDecision({ scope, currentDecision, mode, source, note, patch });
        syncEditor();

        return { card: form, mode, source, note, patch, previewButton, saveButton, result };
    }

    seedEditorFromDecision({ scope, currentDecision, mode, source, note, patch }) {
        if (!currentDecision) {
            if (scope === "global") {
                mode.value = "select-source";
            } else {
                mode.value = "inherit-global";
            }
            return;
        }

        mode.value = currentDecision.decisionKind;
        source.value = currentDecision.sourceEntityRevisionId ?? "";
        note.value = currentDecision.note ?? "";
        if (currentDecision.mergePatch) {
            patch.value = formatJson(currentDecision.mergePatch);
        } else if (currentDecision.structuredPatch) {
            patch.value = formatJson(currentDecision.structuredPatch);
        }
    }

    buildGlobalPayload(editor) {
        const sourceEntityRevisionId = editor.source.value;
        if (!sourceEntityRevisionId) {
            throw new Error("Select an accessible source revision.");
        }

        const payload = {
            sourceEntityRevisionId,
            note: editor.note.value.trim() || null
        };

        if (editor.mode.value === "json-merge-patch") {
            payload.mergePatch = parseRequiredJson(editor.patch.value, "Merge patch");
        } else if (editor.mode.value === "json-rule-patch") {
            payload.structuredPatch = parseRequiredJson(editor.patch.value, "Structured patch");
        }
        return payload;
    }

    buildCampaignPayload(editor) {
        const kind = editor.mode.value;
        const payload = {
            decisionKind: kind,
            sourceEntityRevisionId: null,
            note: editor.note.value.trim() || null
        };

        if (kind === "select-source") {
            if (!editor.source.value) {
                throw new Error("Select an accessible source revision.");
            }
            payload.sourceEntityRevisionId = editor.source.value;
        } else if (kind === "json-merge-patch") {
            payload.mergePatch = parseRequiredJson(editor.patch.value, "Merge patch");
        } else if (kind === "json-rule-patch") {
            payload.structuredPatch = parseRequiredJson(editor.patch.value, "Structured patch");
        }
        return payload;
    }

    async runEditorAction(editor, busyText, action, onSuccess) {
        editor.result.replaceChildren();
        setButtonBusy(editor.previewButton, true, busyText);
        setButtonBusy(editor.saveButton, true, busyText);
        try {
            const result = await action();
            await onSuccess(result);
        } catch (error) {
            editor.result.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(editor.previewButton, false);
            setButtonBusy(editor.saveButton, false);
        }
    }

    renderPreview(container, preview) {
        const changes = element("div", { className: "list-group list-group-flush" });
        if (!preview.changes.length) {
            changes.append(element("div", { className: "list-group-item px-0 text-body-secondary", text: "No structural changes." }));
        } else {
            for (const change of preview.changes) {
                changes.append(element("div", { className: "list-group-item px-0" },
                    element("div", { className: "d-flex flex-wrap gap-2 align-items-center mb-1" },
                        badge(change.changeKind, change.changeKind === "remove" ? "danger" : change.changeKind === "add" ? "success" : "warning"),
                        element("code", { text: change.path || "/" })),
                    element("div", { className: "row g-2" },
                        change.before !== null && change.before !== undefined
                            ? element("div", { className: "col-md-6" }, element("div", { className: "small text-body-secondary", text: "Before" }), codeBlock(change.before))
                            : null,
                        change.after !== null && change.after !== undefined
                            ? element("div", { className: "col-md-6" }, element("div", { className: "small text-body-secondary", text: "After" }), codeBlock(change.after))
                            : null))));
            }
        }

        container.replaceChildren(element("div", { className: "card card-body border-primary" },
            element("div", { className: "d-flex flex-wrap justify-content-between gap-2 align-items-center mb-2" },
                element("h4", { className: "h6 mb-0", text: "Preview" }),
                badge(preview.decisionKind, "primary")),
            element("div", { className: "small text-body-secondary mb-3", text: `${preview.changes.length} structural change${preview.changes.length === 1 ? "" : "s"}` }),
            changes,
            element("details", { className: "mt-3" },
                element("summary", { className: "fw-semibold", text: "Resolved candidate JSON" }),
                element("div", { className: "mt-2" }, codeBlock(preview.previewDocument)))));
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
