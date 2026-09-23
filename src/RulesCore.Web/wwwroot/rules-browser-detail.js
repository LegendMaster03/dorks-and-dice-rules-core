import {
    alertNode,
    badge,
    clear,
    definitionList,
    describeError,
    element,
    formatDate,
    setButtonBusy
} from "./ui.js";
import { renderResolvedRule } from "./rule-renderers.js";
import { renderSemanticComparison } from "./semantic-comparison.js";

export async function renderRuleDetailPane(
    app,
    container,
    conceptKey,
    scopeValue,
    requestSerial,
    getCurrentSerial,
    onBackToList)
{
    container.replaceChildren(element("div", {
        className: "rules-core-library-detail-loading",
        text: "Loading rule…"
    }));

    const campaignId = scopeValue.startsWith("campaign:")
        ? scopeValue.slice("campaign:".length)
        : null;
    try {
        const [resolved, versions, baseline] = await Promise.all([
            campaignId
                ? app.api.getCampaignResolvedRule(campaignId, conceptKey)
                : app.api.getGlobalResolvedRule(conceptKey),
            getOptionalRuleVersions(app, conceptKey),
            campaignId
                ? getOptionalCampaignBaseline(app, campaignId, conceptKey)
                : null
        ]);
        if (requestSerial !== getCurrentSerial()) return;

        const tabBar = element("div", { className: "rules-core-version-tabs" });
        const backToList = element("button", {
            type: "button",
            className: "btn btn-sm btn-outline-secondary rules-core-library-mobile-back",
            text: "← Back to list",
            onClick: onBackToList
        });
        tabBar.append(backToList);
        const body = element("div", { className: "rules-core-library-detail-body" });
        const effectiveLabel = campaignId
            ? campaignName(app, campaignId)
            : "Dorks & Dice";

        const tabs = [{
            key: "effective",
            label: effectiveLabel,
            title: campaignId ? "Effective campaign rule" : "Dorks & Dice combined rule",
            render: () => renderEffectiveRule(app, body, resolved, baseline, campaignId)
        }];

        for (const version of versions?.versions ?? []) {
            tabs.push({
                key: `source:${version.canonicalEntityId}`,
                label: sourceVersionTabLabel(version, versions?.versions ?? []),
                title: sourceVersionLabel(version),
                render: () => renderSourceVersion(body, versions, version, resolved)
            });
        }

        if ((versions?.versions?.length ?? 0) > 1) {
            tabs.push({
                key: "compare",
                label: "Compare",
                title: "Compare source versions",
                render: () => renderComparisonTab(app, body, resolved, versions, campaignId)
            });
        }

        let activeKey = "effective";
        const buttons = new Map();
        const activate = key => {
            activeKey = key;
            for (const [buttonKey, button] of buttons) {
                const selected = buttonKey === activeKey;
                button.classList.toggle("is-active", selected);
                button.setAttribute("aria-selected", selected ? "true" : "false");
            }
            const tab = tabs.find(value => value.key === activeKey) ?? tabs[0];
            tab.render();
            app.presentRenderedFragment?.(body);
        };

        tabBar.append(element("span", {
            className: "rules-core-version-tabs-label",
            text: "View"
        }));
        for (const tab of tabs) {
            const button = element("button", {
                type: "button",
                className: "rules-core-version-tab",
                text: tab.label,
                title: tab.title,
                attributes: {
                    role: "tab",
                    "aria-selected": tab.key === activeKey ? "true" : "false"
                }
            });
            button.addEventListener("click", () => activate(tab.key));
            buttons.set(tab.key, button);
            tabBar.append(button);
        }

        const context = element("div", { className: "rules-core-version-tabs-context" },
            badge(humanizeEntityType(resolved.entityType), "secondary"));
        if ((versions?.versions?.length ?? 0) > 1) {
            context.append(element("span", {
                className: "rules-core-version-count",
                text: `${versions.versions.length} source versions`
            }));
        }
        tabBar.append(context);

        container.replaceChildren(tabBar, body);
        activate("effective");
        app.presentRenderedFragment?.(container);
    } catch (error) {
        if (requestSerial !== getCurrentSerial()) return;
        container.replaceChildren(alertNode("danger", describeError(error)));
        app.presentRenderedFragment?.(container);
    }
}

function renderEffectiveRule(app, container, resolved, baseline, campaignId) {
    clear(container);
    const campaignScope = Boolean(campaignId);
    const isMonster = String(resolved.entityType ?? "").toLowerCase() === "monster";

    container.append(renderRulingStatusBar(app, resolved, campaignId));

    if (isMonster) {
        container.append(element("section", { className: "rules-core-effective-rule" },
            renderResolvedRule(resolved.entityType, resolved.document, {
                displayName: resolved.displayName,
                showDocument: false
            })));
        container.append(renderRuleContextDisclosure(resolved, campaignScope));
    } else {
        container.append(renderMetadata(resolved, campaignScope));
        container.append(element("section", { className: "rules-core-effective-rule" },
            renderResolvedRule(resolved.entityType, resolved.document, {
                displayName: resolved.displayName
            })));
    }

    if (campaignScope) {
        const campaign = element("div", {
            className: "card card-body mb-3 rules-core-detail-context-card"
        });
        campaign.append(
            element("h4", { className: "h5", text: "Campaign overlay" }),
            definitionList([
                ["Pinned global baseline", `#${resolved.baselineRulesetRevisionNumber}`],
                ["Global decision", `#${resolved.globalDecisionNumber} · ${resolved.globalDecisionKind}`],
                ["Campaign decision", resolved.campaignDecisionNumber
                    ? `#${resolved.campaignDecisionNumber} · ${resolved.effectiveDecisionKind}`
                    : "Inherited without campaign override"],
                ["Campaign note", resolved.campaignDecisionNote]
            ]));
        container.append(campaign);

        if (baseline) {
            const details = element("details", {
                className: "card card-body mb-3 rules-core-detail-context-card"
            });
            details.append(
                element("summary", {
                    className: "fw-semibold",
                    text: `Published global baseline #${baseline.baselineRulesetRevisionNumber}`
                }),
                element("div", { className: "mt-3" },
                    renderResolvedRule(baseline.entityType, baseline.document, {
                        displayName: baseline.displayName,
                        showDocument: !isMonster
                    })));
            container.append(details);
        } else {
            container.append(alertNode(
                "secondary",
                "The pinned global baseline is not available to this account under the independent source-access rules."));
        }
    }

    if (!isMonster) container.append(renderProvenance(resolved));
}

function renderSourceVersion(container, versions, version, resolved) {
    clear(container);
    container.append(element("div", { className: "rules-core-source-version-heading" },
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: "SOURCE VERSION" }),
            element("h3", { className: "h4 mb-1", text: versions.displayName }),
            element("div", {
                className: "text-body-secondary",
                text: [
                    version.gameEdition,
                    version.sourceCode,
                    version.packageDisplayName,
                    version.publicationDate,
                    `rev. ${version.sourceRevisionNumber}`
                ].filter(Boolean).join(" · ")
            })),
        version.sourceEntityRevisionId === resolved.sourceEntityRevisionId
            ? badge("Selected source", "primary")
            : null));

    container.append(element("section", { className: "rules-core-effective-rule" },
        renderResolvedRule(versions.entityType, version.document, {
            displayName: versions.displayName,
            showDocument: String(versions.entityType).toLowerCase() !== "monster"
        })));

    const details = element("details", { className: "rules-core-context-disclosure" });
    details.append(
        element("summary", { text: "Source provenance" }),
        element("div", { className: "rules-core-context-disclosure-body" },
            definitionList([
                ["Edition", version.gameEdition],
                ["Release", version.releaseKind],
                ["Publication date", version.publicationDate],
                ["Source", version.sourceEntityName],
                ["Source code", version.sourceCode],
                ["Package", version.packageDisplayName],
                ["Format", version.formatKey],
                ["Source revision", `#${version.sourceRevisionNumber}`],
                ["Imported", formatDate(version.importedAt)],
                ["Equivalent representations", String(version.equivalentRepresentationCount)]
            ])));
    container.append(details);
}

function renderComparisonTab(app, container, resolved, versions, campaignId) {
    clear(container);

    const available = versions.versions ?? [];
    const heading = element("div", { className: "rules-core-comparison-heading" },
        element("div", {},
            element("div", { className: "rules-core-eyebrow", text: "VERSION DIFFERENCES" }),
            element("h3", { className: "h4 mb-1", text: `Compare ${versions.displayName}` }),
            element("p", {
                className: "small text-body-secondary mb-0",
                text: "Compare rule-bearing content without collapsing the source versions into one representation."
            })));
    container.append(heading);

    if (available.length < 2) {
        container.append(alertNode("secondary", "At least two accessible source versions are required."));
        return;
    }

    const left = element("select", {
        className: "form-select form-select-sm",
        ariaLabel: "Left source version"
    });
    const right = element("select", {
        className: "form-select form-select-sm",
        ariaLabel: "Right source version"
    });
    for (const version of available) {
        const label = sourceVersionLabel(version);
        left.append(element("option", { value: version.sourceEntityRevisionId, text: label }));
        right.append(element("option", { value: version.sourceEntityRevisionId, text: label }));
    }
    left.value = available[0].sourceEntityRevisionId;
    right.value = available[1].sourceEntityRevisionId;

    const compare = element("button", {
        type: "button",
        className: "btn btn-sm btn-primary",
        text: "Compare"
    });
    const result = element("div", { className: "rules-core-comparison-result" });
    const controls = element("div", { className: "rules-core-comparison-controls" },
        comparisonField("Left", left),
        comparisonField("Right", right),
        element("div", { className: "rules-core-comparison-action" }, compare));
    container.append(controls, result);

    compare.addEventListener("click", async () => {
        result.replaceChildren();
        if (left.value === right.value) {
            result.append(alertNode("secondary", "Choose two different source versions."));
            return;
        }

        setButtonBusy(compare, true, "Comparing…");
        try {
            const comparison = await app.api.compareRuleVersions({
                ruleConceptId: resolved.ruleConceptId,
                leftSourceEntityRevisionId: left.value,
                rightSourceEntityRevisionId: right.value
            });
            const leftVersion = available.find(version =>
                version.sourceEntityRevisionId === left.value);
            const rightVersion = available.find(version =>
                version.sourceEntityRevisionId === right.value);
            renderSemanticComparison(result, comparison, {
                leftLabel: leftVersion ? sourceVersionTabLabel(leftVersion, available) : "Left",
                rightLabel: rightVersion ? sourceVersionTabLabel(rightVersion, available) : "Right"
            });
            const adjudication = adjudicationButton(app, resolved.ruleConceptId, campaignId);
            if (adjudication) {
                result.append(element("div", { className: "rules-core-comparison-adjudication" },
                    element("div", {},
                        element("strong", { text: "Need a ruling?" }),
                        element("div", {
                            className: "small text-body-secondary",
                            text: campaignId
                                ? "Open this concept in the campaign rule editor."
                                : "Open this concept in the global Rules Lawyer editor."
                        })),
                    adjudication));
            }
        } catch (error) {
            result.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(compare, false);
        }
    });

    compare.click();
}

function comparisonField(label, control) {
    return element("label", { className: "rules-core-comparison-field" },
        element("span", { text: label }),
        control);
}

function sourceVersionTabLabel(version, allVersions) {
    const edition = String(version.gameEdition ?? "").trim();
    if (!edition) {
        return version.sourceCode || version.formatKey || version.packageDisplayName;
    }

    const sameEditionCount = allVersions.filter(candidate =>
        String(candidate.gameEdition ?? "").trim().toLowerCase() === edition.toLowerCase()).length;
    return sameEditionCount > 1 && version.sourceCode
        ? `${edition} · ${version.sourceCode}`
        : edition;
}

function sourceVersionLabel(version) {
    return [
        version.gameEdition,
        version.sourceCode || version.formatKey,
        version.packageDisplayName,
        version.publicationDate,
        `rev. ${version.sourceRevisionNumber}`
    ].filter(Boolean).join(" · ");
}

function renderRulingStatusBar(app, resolved, campaignId) {
    const campaignScope = Boolean(campaignId);
    const hasCampaignOverride = campaignScope && Boolean(resolved.campaignDecisionNumber);
    const title = campaignScope
        ? hasCampaignOverride
            ? "Campaign override"
            : "Inherited from Dorks & Dice"
        : "Dorks & Dice ruling";
    const detail = campaignScope
        ? hasCampaignOverride
            ? `Decision #${resolved.campaignDecisionNumber} · ${resolved.effectiveDecisionKind}`
            : `Global decision #${resolved.globalDecisionNumber} · ${resolved.globalDecisionKind}`
        : `Decision #${resolved.globalDecisionNumber} · ${resolved.decisionKind}`;

    return element("div", { className: "rules-core-ruling-status" },
        element("div", {},
            element("div", { className: "rules-core-ruling-status-title", text: title }),
            element("div", { className: "rules-core-ruling-status-detail", text: detail })),
        adjudicationButton(app, resolved.ruleConceptId, campaignId));
}

function adjudicationButton(app, ruleConceptId, campaignId) {
    const campaignCanEdit = campaignId
        && app.dmCampaigns?.some(value => String(value.id) === String(campaignId));
    if (!campaignId && !app.canEditGlobal) return null;
    if (campaignId && !campaignCanEdit) return null;

    return element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-primary",
        text: campaignId ? "Edit campaign rule" : "Edit Dorks & Dice rule",
        onClick: async () => openAdjudication(app, ruleConceptId, campaignId)
    });
}

async function openAdjudication(app, ruleConceptId, campaignId) {
    if (campaignId) {
        app.activeView = "campaign";
        app.activeCampaignId = campaignId;
    } else {
        app.activeView = "global";
    }

    await app.render();
    const body = app.root.querySelector(".rules-core-main");
    if (!body) return;

    if (campaignId) {
        await app.renderCampaignConcept(body, ruleConceptId);
    } else {
        await app.renderGlobalConcept(body, ruleConceptId);
    }
}

function renderMetadata(resolved, campaignScope) {
    return element("div", { className: "rules-core-detail-heading" },
        element("div", {},
            element("div", {
                className: "rules-core-eyebrow",
                text: campaignScope ? "CAMPAIGN RULE" : "DORKS & DICE RULE"
            }),
            element("h3", { className: "h3 mb-1", text: resolved.displayName }),
            element("div", {
                className: "text-body-secondary font-monospace small",
                text: resolved.conceptKey
            })),
        element("div", { className: "rules-core-detail-heading-meta" },
            badge(humanizeEntityType(resolved.entityType), "primary"),
            element("span", {
                className: "small text-body-secondary",
                text: `Published #${resolved.campaignRulesetRevisionNumber ?? resolved.rulesetRevisionNumber}`
            })));
}

function renderRuleContextDisclosure(resolved, campaignScope) {
    const details = element("details", { className: "rules-core-context-disclosure" });
    const body = element("div", { className: "rules-core-context-disclosure-body" });
    body.append(definitionList([
        ["Concept", resolved.conceptKey],
        ["Scope", campaignScope ? "Campaign effective rule" : "Published Dorks & Dice rule"],
        ["Published revision", `#${resolved.campaignRulesetRevisionNumber ?? resolved.rulesetRevisionNumber}`],
        ["Decision", resolved.effectiveDecisionKind ?? resolved.decisionKind],
        ["Selected source", `${resolved.sourceEntityName} · ${resolved.sourceCode} · rev. ${resolved.sourceRevisionNumber}`],
        ["Package", resolved.packageDisplayName],
        ["Decision note", resolved.decisionNote ?? resolved.campaignDecisionNote]
    ]));
    appendContributions(body, resolved);
    details.append(element("summary", { text: "Rule context and provenance" }), body);
    return details;
}

function renderProvenance(resolved) {
    const card = element("details", { className: "rules-core-context-disclosure" });
    const body = element("div", { className: "rules-core-context-disclosure-body" });
    body.append(definitionList([
        ["Selected source", `${resolved.sourceEntityName} · ${resolved.sourceCode} · rev. ${resolved.sourceRevisionNumber}`],
        ["Package", resolved.packageDisplayName],
        ["Decision note", resolved.decisionNote ?? resolved.campaignDecisionNote],
        ["Additional contributing sources", String((resolved.contributions ?? resolved.globalContributions ?? []).length)]
    ]));
    appendContributions(body, resolved);
    card.append(element("summary", { text: "Rule context and provenance" }), body);
    return card;
}

function appendContributions(container, resolved) {
    const contributions = resolved.contributions ?? resolved.globalContributions ?? [];
    if (!contributions.length) return;
    const list = element("ul", { className: "mb-0 mt-3" });
    for (const contribution of contributions) {
        list.append(element("li", {},
            element("span", {
                className: "fw-semibold",
                text: `${contribution.sourceEntityName} · ${contribution.sourceCode || contribution.editionDisplayName}`
            }),
            ` — ${contribution.contributionKind}${contribution.note ? `: ${contribution.note}` : ""}`));
    }
    container.append(list);
}


function campaignName(app, campaignId) {
    return app.campaigns.find(value => String(value.id) === String(campaignId))?.name
        ?? "Campaign";
}

function humanizeEntityType(entityType) {
    const value = String(entityType ?? "");
    if (!value) return "Rule";
    return value
        .replace(/([a-z])([A-Z])/g, "$1 $2")
        .replace(/^./, match => match.toUpperCase());
}
