import { alertNode, badge, codeBlock, element, describeError, setButtonBusy } from "./ui.js";

export function installSemanticComparison(app) {
    const createDecisionEditor = app.createDecisionEditor.bind(app);
    app.createDecisionEditor = (scope, detail, container) => {
        const editor = createDecisionEditor(scope, detail, container);
        const revisions = flattenRevisions(detail.accessibleSources ?? []);
        if (revisions.length < 2) return editor;

        const card = element("div", { className: "card card-body mb-3" });
        card.append(
            element("h3", { className: "h5 mb-1", text: "Semantic comparison" }),
            element("p", {
                className: "small text-body-secondary",
                text: "Compare bound source revisions by rule meaning. Matching fields are counted but hidden so attention stays on additions, omissions, and contradictions."
            }));

        const left = revisionSelect(revisions);
        const right = revisionSelect(revisions);
        left.value = revisions[0].id;
        right.value = revisions[1].id;
        const compare = element("button", { type: "button", className: "btn btn-outline-primary", text: "Compare sources" });
        const result = element("div", { className: "mt-3" });
        card.append(element("div", { className: "row g-2 align-items-end" },
            field("Left source", left, "col-lg-5"),
            field("Right source", right, "col-lg-5"),
            element("div", { className: "col-lg-2 d-grid" }, compare)), result);

        compare.addEventListener("click", async () => {
            result.replaceChildren();
            setButtonBusy(compare, true, "Comparing…");
            try {
                const scopeRequest = scope === "global"
                    ? { kind: "global", campaignId: null }
                    : { kind: "campaign", campaignId: detail.campaignId };
                const comparison = await app.api.backend("/api/workspace/comparison", {
                    method: "POST",
                    body: {
                        scope: scopeRequest,
                        ruleConceptId: detail.concept.id,
                        leftSourceEntityRevisionId: left.value,
                        rightSourceEntityRevisionId: right.value
                    }
                });
                renderSemanticComparison(result, comparison);
            } catch (error) {
                result.replaceChildren(alertNode("danger", describeError(error)));
            } finally {
                setButtonBusy(compare, false);
            }
        });

        return element("div", {}, card, editor);
    };
}

export function renderSemanticComparison(
    container,
    comparison,
    { leftLabel = "Left", rightLabel = "Right" } = {})
{
    const summary = element("div", { className: "d-flex flex-wrap gap-2 mb-3" },
        badge(`${comparison.unchangedValueCount} unchanged`, "secondary"),
        badge(`${comparison.compatibleDifferenceCount} compatible`, "success"),
        badge(`${comparison.metadataOnlyDifferenceCount} metadata`, "info"),
        badge(`${comparison.contradictionCount} conflicts`, comparison.contradictionCount ? "danger" : "success"));
    container.append(summary, alertNode(
        comparison.canResolveAutomatically ? "success" : "warning",
        comparison.explanation));

    if (!comparison.differences?.length) {
        container.append(element("div", { className: "text-body-secondary", text: "No semantic differences." }));
        return;
    }

    const list = element("div", { className: "list-group list-group-flush" });
    for (const difference of comparison.differences) {
        const kind = difference.requiresDecision
            ? "danger"
            : difference.kind === "metadata-only" ? "info" : "success";
        const item = element("div", { className: "list-group-item px-0" });
        item.append(element("div", { className: "d-flex flex-wrap gap-2 align-items-center" },
            badge(humanizeDifferenceKind(difference.kind), kind),
            element("code", { text: difference.path })));
        item.append(element("p", { className: "small mb-2 mt-1", text: difference.explanation }));

        const values = element("div", { className: "rules-core-semantic-values" });
        if (difference.left !== null && difference.left !== undefined) {
            values.append(renderSemanticValue(leftLabel, difference.left));
        }
        if (difference.right !== null && difference.right !== undefined) {
            values.append(renderSemanticValue(rightLabel, difference.right));
        }
        if (values.children.length) item.append(values);
        list.append(item);
    }
    container.append(list);
}

function renderSemanticValue(label, value) {
    const body = isPrimitive(value)
        ? element("div", {
            className: "rules-core-semantic-value-text",
            text: String(value)
        })
        : codeBlock(value);
    return element("section", { className: "rules-core-semantic-value" },
        element("div", { className: "rules-core-semantic-value-label", text: label }),
        body);
}

function isPrimitive(value) {
    return value === null
        || value === undefined
        || typeof value === "string"
        || typeof value === "number"
        || typeof value === "boolean";
}

function humanizeDifferenceKind(kind) {
    return String(kind ?? "")
        .replace(/-/g, " ")
        .replace(/^./, value => value.toUpperCase());
}

function flattenRevisions(sources) {
    return sources.flatMap(source => (source.revisions ?? []).map(revision => ({
        ...revision,
        label: `${source.name} · ${source.editionDisplayName} · rev. ${revision.revisionNumber}`
    })));
}

function revisionSelect(revisions) {
    const select = element("select", { className: "form-select" });
    for (const revision of revisions) {
        select.append(element("option", { value: revision.id, text: revision.label }));
    }
    return select;
}

function field(label, control, columnClass) {
    return element("div", { className: columnClass },
        element("label", { className: "form-label fw-semibold", text: label }), control);
}
