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
        this.session = session ?? { user: null, globalRoles: [] };
        this.campaigns = Array.isArray(campaigns) ? campaigns : [];
        this.dmCampaigns = this.campaigns.filter(value => value.role === CAMPAIGN_DM_ROLE);
        this.canEditGlobal = hostContext.siteMode === DORKS_MODE
            && (this.session.globalRoles ?? []).includes(RULES_LAWYER_ROLE);
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
                "No public Rules Core view is available in the current site mode, and this session does not have Rules Lawyer or campaign DM authority."));
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
                text: "Browse published rules, with authoring controls shown only when your session has authority."
            }));

        const user = element("div", { className: "text-end small" });
        user.append(
            element("div", {
                className: "fw-semibold",
                text: this.session.user?.displayName ?? "Guest"
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
            container.append(this.renderDecisionEditor(detail, "global", container));
        } catch (error) {
            loading.remove();
            container.append(alertNode("danger", describeError(error)));
        }
    }

    async renderCampaignOverview(container) {
        const campaign = this.dmCampaigns.find(value => value.id === this.activeCampaignId)
            ?? this.dmCampaigns[0];
        if (!campaign) {
            clear(container);
            container.append(alertNode("secondary", "No DM campaign is available."));
            return;
        }

        this.activeCampaignId = campaign.id;
        const overview = await this.api.getCampaignAuthoringOverview(campaign.id);
        clear(container);

        const selector = this.renderCampaignSelector();
        const summary = element("div", { className: "card card-body mb-3" });
        const publication = overview.latestPublishedRuleset;
        summary.append(
            element("h3", { className: "h5", text: campaign.name ?? "Campaign rules" }),
            definitionList([
                ["Baseline", overview.selectedBaseline
                    ? `Global revision #${overview.selectedBaseline.globalRulesetRevisionNumber}`
                    : "None"],
                ["Campaign concepts", String(overview.campaignConceptCount)],
                ["Published campaign revision", publication ? `#${publication.revisionNumber}` : "None"]
            ]));
        container.append(selector, summary);

        if (!overview.concepts.length) {
            container.append(alertNode("secondary", "No campaign rules are currently available."));
            return;
        }

        const table = element("table", { className: "table table-hover align-middle mb-0" });
        const head = element("thead");
        const headRow = element("tr");
        for (const text of ["Rule", "Type", "Decision", "State", ""]) {
            headRow.append(element("th", { text }));
        }
        head.append(headRow);

        const body = element("tbody");
        for (const concept of overview.concepts) {
            const row = element("tr");
            const nameCell = element("td");
            nameCell.append(
                element("div", { className: "fw-semibold", text: concept.displayName }),
                element("div", { className: "small text-body-secondary font-monospace", text: concept.key }));
            row.append(nameCell);
            row.append(element("td", { text: concept.entityType }));
            row.append(element("td", {
                text: concept.latestDecisionNumber
                    ? `#${concept.latestDecisionNumber} · ${concept.latestDecisionKind}`
                    : "Inherit"
            }));
            const stateCell = element("td");
            if (concept.hasUnpublishedChanges) stateCell.append(badge("Pending", "warning"));
            else if (concept.latestDecisionId) stateCell.append(badge("Published override", "success"));
            else stateCell.append(badge("Inherit baseline", "secondary"));
            row.append(stateCell);
            const actionCell = element("td", { className: "text-end" });
            actionCell.append(element("button", {
                type: "button",
                className: "btn btn-sm btn-outline-primary",
                text: "Open",
                onClick: async () => this.renderCampaignConcept(container, campaign.id, concept.id)
            }));
            row.append(actionCell);
            body.append(row);
        }

        table.append(head, body);
        container.append(element("div", { className: "card" },
            element("div", { className: "table-responsive" }, table)));
    }

    async renderCampaignConcept(container, campaignId, conceptId) {
        clear(container);
        container.append(this.backButton(async () => this.renderCampaignOverview(container)));
        const loading = this.loadingCard("Loading campaign rule…");
        container.append(loading);

        try {
            const detail = await this.api.getCampaignAuthoringConcept(campaignId, conceptId);
            loading.remove();
            container.append(this.renderConceptSummary(detail, "campaign"));
            container.append(this.renderDecisionEditor(detail, "campaign", container, campaignId));
        } catch (error) {
            loading.remove();
            container.append(alertNode("danger", describeError(error)));
        }
    }

    renderConceptSummary(detail, scope) {
        const card = element("div", { className: "card card-body mb-3" });
        card.append(
            element("div", {
                className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3"
            },
            element("div", {},
                element("h3", { className: "h4 mb-1", text: detail.displayName }),
                element("div", {
                    className: "small text-body-secondary font-monospace",
                    text: detail.key
                })),
            badge(scope === "global" ? "Global" : "Campaign", scope === "global" ? "primary" : "warning")),
            definitionList([
                ["Entity type", detail.entityType],
                ["Bound source entities", String(detail.bindings.length)],
                ["Latest decision", detail.latestDecision
                    ? `#${detail.latestDecision.decisionNumber} · ${detail.latestDecision.decisionKind}`
                    : "None"]
            ]));
        if (detail.latestDecision?.note) {
            card.append(element("p", { className: "small text-body-secondary mb-0", text: detail.latestDecision.note }));
        }
        return card;
    }

    renderDecisionEditor(detail, scope, container, campaignId = null) {
        const card = element("div", { className: "card card-body mb-3" });
        card.append(element("h4", { className: "h5", text: "Decision" }));

        const form = element("form");
        const kind = element("select", { className: "form-select" });
        for (const value of ["select", "patch", "manual"]) {
            kind.append(element("option", { value, text: value }));
        }
        kind.value = detail.latestDecision?.decisionKind ?? "select";

        const sourceRevision = element("select", { className: "form-select" });
        sourceRevision.append(element("option", { value: "", text: "No source revision" }));
        for (const candidate of detail.candidates ?? []) {
            sourceRevision.append(element("option", {
                value: candidate.sourceEntityRevisionId,
                text: `${candidate.sourceEntityName} · ${candidate.packageDisplayName} · ${candidate.editionDisplayName} · rev. ${candidate.revisionNumber}`
            }));
        }
        sourceRevision.value = detail.latestDecision?.selectedSourceEntityRevisionId ?? "";

        const patch = element("textarea", {
            className: "form-control font-monospace",
            rows: 10,
            placeholder: '[{"op":"replace","path":"/field","value":"replacement"}]'
        });
        patch.value = detail.latestDecision?.patchJson
            ? formatJson(detail.latestDecision.patchJson)
            : "[]";

        const manual = element("textarea", {
            className: "form-control font-monospace",
            rows: 10,
            placeholder: '{"name":"Manual rule"}'
        });
        manual.value = detail.latestDecision?.manualDocumentJson
            ? formatJson(detail.latestDecision.manualDocumentJson)
            : "{}";

        const note = element("textarea", {
            className: "form-control",
            rows: 2,
            placeholder: "Why this decision is authoritative"
        });
        note.value = detail.latestDecision?.note ?? "";

        const groups = [
            fieldGroup("Decision kind", kind),
            fieldGroup("Source revision", sourceRevision),
            fieldGroup("Patch operations", patch),
            fieldGroup("Manual document", manual),
            fieldGroup("Note", note)
        ];
        for (const group of groups) form.append(group);

        const previewButton = element("button", {
            type: "button",
            className: "btn btn-outline-primary me-2",
            text: "Preview"
        });
        const saveButton = element("button", {
            type: "submit",
            className: "btn btn-primary",
            text: "Save decision"
        });
        const result = element("div", { className: "mt-3" });
        form.append(previewButton, saveButton, result);
        card.append(form);

        const payload = () => ({
            decisionKind: kind.value,
            selectedSourceEntityRevisionId: sourceRevision.value || null,
            patchOperations: kind.value === "patch" ? parseJson(patch.value, "Patch operations") : [],
            manualDocument: kind.value === "manual" ? parseJson(manual.value, "Manual document") : null,
            note: note.value.trim() || null
        });

        previewButton.addEventListener("click", async () => {
            result.replaceChildren();
            setButtonBusy(previewButton, true, "Previewing…");
            try {
                const preview = scope === "global"
                    ? await this.api.previewGlobalDecision(detail.id, payload())
                    : await this.api.previewCampaignDecision(campaignId, detail.id, payload());
                result.replaceChildren(this.renderPreview(preview));
            } catch (error) {
                result.replaceChildren(alertNode("danger", describeError(error)));
            } finally {
                setButtonBusy(previewButton, false);
            }
        });

        form.addEventListener("submit", async event => {
            event.preventDefault();
            result.replaceChildren();
            setButtonBusy(saveButton, true, "Saving…");
            try {
                const saved = scope === "global"
                    ? await this.api.saveGlobalDecision(detail.id, payload())
                    : await this.api.saveCampaignDecision(campaignId, detail.id, payload());
                result.replaceChildren(alertNode(
                    "success",
                    `Saved decision #${saved.decisionNumber}. Publish the ${scope} ruleset when it is ready for consumers.`));
                if (scope === "global") {
                    await this.renderGlobalConcept(container, detail.id);
                } else {
                    await this.renderCampaignConcept(container, campaignId, detail.id);
                }
            } catch (error) {
                result.replaceChildren(alertNode("danger", describeError(error)));
            } finally {
                setButtonBusy(saveButton, false);
            }
        });

        return card;
    }

    renderPreview(preview) {
        const card = element("div", { className: "border rounded p-3" });
        card.append(
            definitionList([
                ["Base revision", preview.baseSourceRevisionNumber ? `#${preview.baseSourceRevisionNumber}` : "None"],
                ["Result", preview.valid ? "Valid" : "Invalid"]
            ]),
            element("h5", { className: "h6", text: "Resolved document" }),
            codeBlock(preview.resolvedDocument));
        return card;
    }

    renderCampaignSelector() {
        const wrapper = element("div", { className: "card card-body mb-3" });
        const select = element("select", { className: "form-select" });
        for (const campaign of this.dmCampaigns) {
            select.append(element("option", {
                value: campaign.id,
                text: campaign.name ?? `Campaign ${campaign.id}`
            }));
        }
        select.value = this.activeCampaignId;
        select.addEventListener("change", async () => {
            this.activeCampaignId = select.value;
            await this.render();
        });
        wrapper.append(
            element("label", { className: "form-label fw-semibold", text: "Campaign" }),
            select);
        return wrapper;
    }

    async publishGlobal(button, container) {
        setButtonBusy(button, true, "Publishing…");
        try {
            const published = await this.api.publishGlobalRules();
            await this.renderGlobalOverview(container);
            container.prepend(alertNode("success", `Published global ruleset revision #${published.revisionNumber}.`));
        } catch (error) {
            container.prepend(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(button, false);
        }
    }

    backButton(onClick) {
        return element("button", {
            type: "button",
            className: "btn btn-outline-secondary mb-3",
            text: "Back",
            onClick
        });
    }

    loadingCard(text) {
        return element("div", { className: "card card-body text-body-secondary", text });
    }
}

function fieldGroup(label, control) {
    return element("div", { className: "mb-3" },
        element("label", { className: "form-label fw-semibold", text: label }),
        control);
}

function parseJson(value, label) {
    try {
        return JSON.parse(value);
    } catch (error) {
        throw new Error(`${label} must contain valid JSON. ${error.message}`);
    }
}
