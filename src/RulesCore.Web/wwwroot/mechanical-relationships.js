import { alertNode, badge, describeError, element, setButtonBusy } from "./ui.js";

export function installMechanicalRelationships(app) {
    const renderConceptSummary = app.renderConceptSummary.bind(app);
    app.renderConceptSummary = (detail, scope) => {
        const summary = renderConceptSummary(detail, scope);
        if (scope !== "global") return summary;

        const wrapper = element("div");
        const relationshipHost = element("div", { className: "mb-3" });
        wrapper.append(summary, relationshipHost);
        void loadRelationships(app, detail.concept.id, relationshipHost);
        return wrapper;
    };
}

async function loadRelationships(app, conceptId, container) {
    container.replaceChildren(element("div", {
        className: "card card-body text-body-secondary",
        text: "Checking mechanical relationships…"
    }));

    try {
        const relationships = await app.api.backend(
            `/api/global/rules/mechanical-relationships/concepts/${encodeURIComponent(conceptId)}`);
        if (!Array.isArray(relationships) || relationships.length === 0) {
            container.replaceChildren();
            return;
        }

        const cards = relationships.map(recommendation =>
            renderRelationship(app, recommendation));
        container.replaceChildren(element("div", { className: "d-grid gap-3" }, ...cards));
    } catch (error) {
        container.replaceChildren(alertNode("danger", describeError(error)));
    }
}

function renderRelationship(app, recommendation) {
    const relationship = recommendation.relationship;
    const card = element("div", { className: "card card-body" });
    const heading = element("div", { className: "d-flex flex-wrap justify-content-between gap-2 align-items-start" });
    const title = element("div");
    title.append(
        element("h3", { className: "h5 mb-1", text: relationship.parent.displayName }),
        element("div", {
            className: "small text-body-secondary",
            text: `Mechanical relationship · ${relationship.kind}`
        }));
    const status = element("div", { className: "d-flex flex-wrap gap-2" });
    status.append(
        badge(relationship.composition, "info"),
        badge(relationship.direction, "secondary"));
    if (recommendation.canResolveStructurally) {
        status.append(badge("Structurally recognized", "success"));
    } else {
        status.append(badge("Incomplete structure", "warning"));
    }
    if (recommendation.isOverridden) {
        status.append(badge("Rules Lawyer override", "warning"));
    }
    heading.append(title, status);
    card.append(heading);

    const components = element("ul", { className: "mb-3" });
    for (const component of relationship.components) {
        const state = component.ruleConceptId
            ? component.hasRuleBinding ? " · source-backed" : " · concept present"
            : " · not present";
        components.append(element("li", { text: `${component.displayName}${state}` }));
    }
    card.append(
        element("div", { className: "small fw-semibold mt-3 mb-1", text: "Derived from" }),
        components,
        alertNode("info", "Recommended: preserve the granular skills as the rule-bearing foundation and derive the umbrella competency from them."),
        element("p", { className: "small text-body-secondary", text: recommendation.explanation }));

    if (recommendation.requiresAdjudication) {
        card.append(alertNode(
            "warning",
            "The parent and one or more components both have source-backed rules. They remain separate and visible for adjudication; the relationship does not alias or discard either side."));
    }

    if (recommendation.missingConceptKeys?.length) {
        card.append(element("div", {
            className: "small text-body-secondary mb-3",
            text: `Missing concepts: ${recommendation.missingConceptKeys.join(", ")}`
        }));
    }

    const resolution = element("select", { className: "form-select" });
    resolution.append(
        element("option", {
            value: "derive-parent",
            text: "Preserve granular skills; derive umbrella (recommended)"
        }),
        element("option", {
            value: "independent-parent",
            text: "Override: treat umbrella as independent"
        }));
    resolution.value = recommendation.effectiveResolutionKind;

    const note = element("textarea", {
        className: "form-control",
        rows: 2,
        placeholder: "Optional ruling note",
        value: recommendation.latestRuling?.note ?? ""
    });
    const save = element("button", {
        type: "button",
        className: "btn btn-outline-primary",
        text: "Save relationship ruling"
    });
    const result = element("div", { className: "mt-2" });

    save.addEventListener("click", async () => {
        setButtonBusy(save, true, "Saving…");
        result.replaceChildren();
        try {
            const updated = await app.api.backend(
                `/api/global/rules/mechanical-relationships/${encodeURIComponent(relationship.key)}/ruling`,
                {
                    method: "PUT",
                    body: {
                        resolutionKind: resolution.value,
                        note: note.value.trim() || null
                    }
                });
            card.replaceWith(renderRelationship(app, updated));
        } catch (error) {
            result.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(save, false);
        }
    });

    const controls = element("div", { className: "row g-2 align-items-end" });
    controls.append(
        field("Structural ruling", resolution, "col-lg-6"),
        field("Ruling note", note, "col-lg-4"),
        element("div", { className: "col-lg-2 d-grid" }, save));
    card.append(controls, result);
    return card;
}

function field(label, control, columnClass) {
    return element("div", { className: columnClass },
        element("label", { className: "form-label fw-semibold", text: label }),
        control);
}
