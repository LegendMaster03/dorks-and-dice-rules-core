import {
    alertNode,
    badge,
    definitionList,
    describeError,
    element,
    formatDate,
    setButtonBusy
} from "./ui.js";

const MAX_RENDERED_CHANGES = 200;

export function installCampaignBaselineAuthoring(app) {
    const renderCampaignOverview = app.renderCampaignOverview.bind(app);
    app.renderCampaignOverview = async container => {
        await renderCampaignOverview(container);
        if (app.activeView !== "campaign" || !app.activeCampaignId) {
            return;
        }

        try {
            const card = await createBaselineCard(app, container);
            const campaignSelector = container.firstElementChild;
            if (campaignSelector?.nextSibling) {
                container.insertBefore(card, campaignSelector.nextSibling);
            } else {
                container.append(card);
            }
        } catch (error) {
            const failure = alertNode(
                "danger",
                `Global baseline controls could not load: ${describeError(error)}`);
            const campaignSelector = container.firstElementChild;
            if (campaignSelector?.nextSibling) {
                container.insertBefore(failure, campaignSelector.nextSibling);
            } else {
                container.append(failure);
            }
        }
    };
}

async function createBaselineCard(app, container) {
    const candidates = await app.api.getCampaignBaselineCandidates(app.activeCampaignId);
    const card = element("div", { className: "card card-body mb-3" });
    const heading = element("div", {
        className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3"
    });
    heading.append(
        element("div", {},
            element("h3", { className: "h5 mb-1", text: "Global baseline" }),
            element("p", {
                className: "text-body-secondary mb-0",
                text: "Preview and deliberately select the published global ruleset this campaign should use. Selecting a baseline does not make it active for players until the campaign ruleset is published."
            })),
        badge("Campaign DM", "primary"));
    card.append(heading);

    if (!candidates.length) {
        card.append(alertNode(
            "secondary",
            "No published global rulesets are available yet. A Rules Lawyer must publish at least one global ruleset before this campaign can select a baseline."));
        return card;
    }

    const selected = candidates.find(candidate => candidate.isSelectedBaseline) ?? null;
    const published = candidates.find(candidate => candidate.isPublishedCampaignBaseline) ?? null;

    const state = element("div", { className: "mb-3" });
    state.append(definitionList([
        ["Selected for next publication", selected ? `#${selected.revisionNumber}` : "None"],
        ["Active published baseline", published ? `#${published.revisionNumber}` : "None"],
        ["Available global revisions", String(candidates.length)]
    ]));
    card.append(state);

    if (selected && (!published || selected.rulesetRevisionId !== published.rulesetRevisionId)) {
        card.append(alertNode(
            "warning",
            `Global revision #${selected.revisionNumber} is selected but is not the active published campaign baseline. Publish the campaign ruleset when the migration and overrides are ready.`));
    }

    const controls = element("div", { className: "row g-2 align-items-end" });
    const selectColumn = element("div", { className: "col-lg-7" });
    const select = element("select", {
        className: "form-select",
        ariaLabel: "Published global ruleset baseline"
    });

    for (const candidate of candidates) {
        const labels = [];
        if (candidate.isSelectedBaseline) {
            labels.push("selected");
        }
        if (candidate.isPublishedCampaignBaseline) {
            labels.push("active");
        }
        const suffix = labels.length ? ` · ${labels.join(", ")}` : "";
        const option = element("option", {
            value: candidate.rulesetRevisionId,
            text: `Revision #${candidate.revisionNumber} · ${candidate.entryCount} rules · ${formatDate(candidate.publishedAt)}${suffix}`
        });
        option.selected = candidate.isSelectedBaseline;
        select.append(option);
    }
    if (!selected) {
        select.value = candidates[0].rulesetRevisionId;
    }
    selectColumn.append(
        element("label", { className: "form-label fw-semibold", text: "Candidate global revision" }),
        select);

    const previewColumn = element("div", { className: "col-lg-2 d-grid" });
    const previewButton = element("button", {
        type: "button",
        className: "btn btn-outline-primary",
        text: "Preview migration"
    });
    previewColumn.append(previewButton);

    const selectActionColumn = element("div", { className: "col-lg-3 d-grid" });
    const selectButton = element("button", {
        type: "button",
        className: "btn btn-primary",
        text: "Select baseline"
    });
    selectActionColumn.append(selectButton);
    controls.append(selectColumn, previewColumn, selectActionColumn);
    card.append(controls);

    const previewResult = element("div", { className: "mt-3" });
    card.append(previewResult);

    const selectedCandidate = () =>
        candidates.find(candidate => candidate.rulesetRevisionId === select.value) ?? null;

    const syncSelectionState = () => {
        const candidate = selectedCandidate();
        const alreadySelected = candidate?.isSelectedBaseline === true;
        selectButton.disabled = alreadySelected;
        selectButton.textContent = alreadySelected ? "Already selected" : "Select baseline";
        previewResult.replaceChildren();
    };

    select.addEventListener("change", syncSelectionState);
    syncSelectionState();

    previewButton.addEventListener("click", async () => {
        previewResult.replaceChildren();
        setButtonBusy(previewButton, true, "Previewing…");
        selectButton.disabled = true;
        try {
            const candidate = selectedCandidate();
            if (!candidate) {
                throw new Error("Select a global ruleset revision to preview.");
            }
            const preview = await app.api.previewCampaignBaseline(
                app.activeCampaignId,
                candidate.rulesetRevisionId);
            renderMigrationPreview(previewResult, preview);
        } catch (error) {
            previewResult.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(previewButton, false);
            syncButtonWithoutClearingPreview(selectButton, selectedCandidate());
        }
    });

    selectButton.addEventListener("click", async () => {
        setButtonBusy(selectButton, true, "Selecting…");
        previewButton.disabled = true;
        try {
            const candidate = selectedCandidate();
            if (!candidate) {
                throw new Error("Select a global ruleset revision first.");
            }
            const result = await app.api.selectCampaignBaseline(
                app.activeCampaignId,
                candidate.rulesetRevisionId);
            window.alert(result.created
                ? `Selected global ruleset revision #${result.rulesetRevisionNumber}. It will become active when the campaign ruleset is published.`
                : `Global ruleset revision #${result.rulesetRevisionNumber} is already the selected baseline.`);
            await app.renderCampaignOverview(container);
        } catch (error) {
            window.alert(describeError(error));
            syncButtonWithoutClearingPreview(selectButton, selectedCandidate());
        } finally {
            previewButton.disabled = false;
        }
    });

    return card;
}

function syncButtonWithoutClearingPreview(button, candidate) {
    const alreadySelected = candidate?.isSelectedBaseline === true;
    button.disabled = alreadySelected;
    button.textContent = alreadySelected ? "Already selected" : "Select baseline";
}

function renderMigrationPreview(container, preview) {
    const card = element("div", { className: "card card-body border-primary" });
    const header = element("div", {
        className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3"
    });
    header.append(
        element("div", {},
            element("h4", { className: "h6 mb-1", text: "Baseline migration preview" }),
            element("div", {
                className: "small text-body-secondary",
                text: preview.currentSelectedBaseline
                    ? `Compare selected global revision #${preview.currentSelectedBaseline.revisionNumber} with candidate #${preview.candidateBaseline.revisionNumber}.`
                    : `Establish global revision #${preview.candidateBaseline.revisionNumber} as the campaign's first selected baseline.`
            })),
        badge(`Candidate #${preview.candidateBaseline.revisionNumber}`, "primary"));
    card.append(header);

    card.append(definitionList([
        ["Added concepts", String(preview.addedConceptCount)],
        ["Removed concepts", String(preview.removedConceptCount)],
        ["Changed global decisions", String(preview.changedConceptCount)],
        ["Unchanged concepts", String(preview.unchangedConceptCount)],
        ["Campaign decisions remaining active", String(preview.overridesRemainingActiveCount)],
        ["Campaign decisions becoming inactive", String(preview.overridesBecomingInactiveCount)],
        ["Historical campaign decisions becoming active", String(preview.historicalOverridesBecomingActiveCount)]
    ]));

    if (preview.publishedCampaignBaseline
        && (!preview.currentSelectedBaseline
            || preview.publishedCampaignBaseline.rulesetRevisionId
                !== preview.currentSelectedBaseline.rulesetRevisionId)) {
        card.append(alertNode(
            "info",
            `Players are still on published global revision #${preview.publishedCampaignBaseline.revisionNumber}. This preview compares against the campaign's current selected baseline, not the last published baseline.`));
    }

    const relevantChanges = preview.changes.filter(change =>
        change.changeKind !== "unchanged" || change.overrideImpact);
    if (!relevantChanges.length) {
        card.append(element("p", {
            className: "text-body-secondary mb-0 mt-3",
            text: "No concept-level differences or campaign-decision activation changes were found."
        }));
        container.replaceChildren(card);
        return;
    }

    const shownChanges = relevantChanges.slice(0, MAX_RENDERED_CHANGES);
    const table = element("table", { className: "table table-hover align-middle mb-0" });
    const head = element("thead");
    const headRow = element("tr");
    for (const label of ["Rule", "Type", "Baseline change", "Current global", "Candidate global", "Campaign decision"] ) {
        headRow.append(element("th", { text: label }));
    }
    head.append(headRow);

    const body = element("tbody");
    for (const change of shownChanges) {
        const row = element("tr");
        const name = element("td");
        name.append(
            element("div", { className: "fw-semibold", text: change.displayName }),
            element("div", {
                className: "small text-body-secondary font-monospace",
                text: change.conceptKey
            }));
        row.append(name);
        row.append(element("td", { text: change.entityType }));

        const changeCell = element("td");
        changeCell.append(badge(change.changeKind, changeBadgeKind(change.changeKind)));
        row.append(changeCell);
        row.append(element("td", {
            text: decisionLabel(change.currentGlobalDecisionNumber, change.currentGlobalDecisionKind)
        }));
        row.append(element("td", {
            text: decisionLabel(change.candidateGlobalDecisionNumber, change.candidateGlobalDecisionKind)
        }));

        const campaignDecision = element("td");
        if (change.latestCampaignDecisionNumber) {
            campaignDecision.append(element("div", {
                text: decisionLabel(change.latestCampaignDecisionNumber, change.latestCampaignDecisionKind)
            }));
            if (change.overrideImpact) {
                campaignDecision.append(
                    element("div", { className: "mt-1" },
                        badge(overrideImpactLabel(change.overrideImpact), overrideImpactBadgeKind(change.overrideImpact))));
            }
        } else {
            campaignDecision.textContent = "—";
        }
        row.append(campaignDecision);
        body.append(row);
    }

    table.append(head, body);
    card.append(element("div", { className: "table-responsive mt-3" }, table));

    if (relevantChanges.length > shownChanges.length) {
        card.append(alertNode(
            "secondary",
            `${relevantChanges.length - shownChanges.length} additional affected concepts are omitted from this browser view. The summary counts include all of them.`));
    }

    if (preview.unchangedConceptCount > 0) {
        card.append(element("div", {
            className: "small text-body-secondary mt-2",
            text: `${preview.unchangedConceptCount} unchanged concepts are included in the summary and omitted from the table unless a campaign-decision activation state changes.`
        }));
    }

    container.replaceChildren(card);
}

function decisionLabel(number, kind) {
    return number ? `#${number} · ${kind}` : "—";
}

function changeBadgeKind(kind) {
    if (kind === "added") {
        return "success";
    }
    if (kind === "removed") {
        return "danger";
    }
    if (kind === "changed") {
        return "warning";
    }
    return "secondary";
}

function overrideImpactLabel(impact) {
    if (impact === "remains-active") {
        return "remains active";
    }
    if (impact === "becomes-inactive") {
        return "becomes inactive";
    }
    if (impact === "becomes-active") {
        return "becomes active";
    }
    return impact;
}

function overrideImpactBadgeKind(impact) {
    if (impact === "becomes-inactive") {
        return "danger";
    }
    if (impact === "becomes-active") {
        return "warning";
    }
    return "secondary";
}
