import {
    alertNode,
    badge,
    clear,
    describeError,
    element,
    setButtonBusy
} from "./ui.js";

const DEV_ROLE = "Dev";
const DORKS_MODE = "dorks-and-dice";

export function installSourceAdministration(app) {
    app.canAdministerSources = app.hostContext.siteMode === DORKS_MODE
        && (app.session.globalRoles ?? []).includes(DEV_ROLE);

    if (app.canAdministerSources && app.activeView === "none") {
        app.activeView = "source-admin";
    }

    const renderNavigation = app.renderNavigation.bind(app);
    app.renderNavigation = () => {
        const nav = renderNavigation();
        if (app.canAdministerSources) {
            nav.append(app.navButton("Source Administration", "source-admin"));
        }
        return nav;
    };

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        if (app.activeView === "source-admin") {
            await renderSourceAdministration(app, container);
            return;
        }
        await renderActiveView(container);
    };
}

async function renderSourceAdministration(app, container) {
    clear(container);

    const heading = element("div", { className: "card card-body mb-3" });
    heading.append(
        element("h3", { className: "h5 mb-1", text: "Source administration" }),
        element("p", {
            className: "text-body-secondary mb-0",
            text: "Import immutable 5e.tools-shaped source documents into the Source Layer. This is a Dev control-plane operation, separate from Rules Lawyer authority."
        }));
    container.append(heading);

    const formCard = element("div", { className: "card card-body" });
    const form = element("form");
    form.append(
        sectionHeading("Package"),
        packageFields(),
        sectionHeading("Work and edition"));

    const workEdition = workEditionFields();
    form.append(workEdition.row);

    const json = element("textarea", {
        className: "form-control font-monospace rules-core-patch-input",
        rows: 18,
        placeholder: '{\n  "skill": [\n    { "name": "Arcana", "source": "PHB", "ability": "int" }\n  ]\n}',
        attributes: { required: "required", spellcheck: "false" }
    });
    form.append(
        sectionHeading("Source document"),
        element("div", { className: "mb-3" },
            element("label", { className: "form-label fw-semibold", text: "5e.tools-shaped JSON" }),
            json,
            element("div", {
                className: "form-text",
                text: "Complete entity objects are preserved in immutable Source Layer revisions. Reimporting identical content is idempotent."
            })));

    const submit = element("button", {
        type: "submit",
        className: "btn btn-primary",
        text: "Import source document"
    });
    const result = element("div", { className: "mt-3" });
    form.append(submit, result);

    const packageControls = form.querySelectorAll("[data-source-package-field]");
    const packageKey = packageControls[0];
    const packageDisplayName = packageControls[1];
    const provider = packageControls[2];
    const license = packageControls[3];
    const isPublic = packageControls[4];

    form.addEventListener("submit", async event => {
        event.preventDefault();
        result.replaceChildren();
        setButtonBusy(submit, true, "Importing…");
        try {
            const rawJson = json.value.trim();
            if (!rawJson) {
                throw new Error("Source JSON is required.");
            }
            try {
                JSON.parse(rawJson);
            } catch (error) {
                throw new Error(`Source JSON is not valid JSON: ${error.message}`);
            }

            const payload = {
                packageKey: packageKey.value.trim(),
                packageDisplayName: packageDisplayName.value.trim(),
                provider: provider.value.trim(),
                license: license.value.trim() || null,
                isPublic: isPublic.checked,
                workKey: workEdition.workKey.value.trim(),
                workDisplayName: workEdition.workDisplayName.value.trim(),
                editionKey: workEdition.editionKey.value.trim(),
                editionDisplayName: workEdition.editionDisplayName.value.trim(),
                json: rawJson
            };

            for (const [name, value] of Object.entries(payload)) {
                if (name !== "license" && name !== "isPublic" && name !== "json" && !value) {
                    throw new Error(`${humanize(name)} is required.`);
                }
            }

            const imported = await app.api.importSourceDocument(payload);
            renderImportResult(result, imported, payload.isPublic);
        } catch (error) {
            result.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(submit, false);
        }
    });

    formCard.append(form);
    container.append(formCard);
}

function packageFields() {
    const row = element("div", { className: "row g-3 mb-4" });
    const packageKey = textField("Package key", "srd-5e", "col-lg-3", true);
    const displayName = textField("Package display name", "5e SRD", "col-lg-3", true);
    const provider = textField("Provider", "manual", "col-lg-2", true);
    const license = textField("License", "CC-BY-4.0", "col-lg-2", false);

    const visibilityColumn = element("div", { className: "col-lg-2" });
    visibilityColumn.append(element("label", {
        className: "form-label fw-semibold d-block",
        text: "Visibility"
    }));
    const isPublic = element("input", {
        type: "checkbox",
        className: "form-check-input",
        dataset: { sourcePackageField: "true" }
    });
    isPublic.checked = true;
    const check = element("div", { className: "form-check pt-2" });
    check.append(
        isPublic,
        element("label", { className: "form-check-label ms-2", text: "Public source" }));
    visibilityColumn.append(check);

    for (const input of [packageKey.input, displayName.input, provider.input, license.input]) {
        input.dataset.sourcePackageField = "true";
    }
    row.append(packageKey.group, displayName.group, provider.group, license.group, visibilityColumn);
    return row;
}

function workEditionFields() {
    const row = element("div", { className: "row g-3 mb-4" });
    const workKey = textField("Work key", "srd", "col-lg-3", true);
    const workDisplayName = textField("Work display name", "System Reference Document", "col-lg-3", true);
    const editionKey = textField("Edition key", "5e-2014", "col-lg-3", true);
    const editionDisplayName = textField("Edition display name", "D&D 5e (2014)", "col-lg-3", true);
    row.append(workKey.group, workDisplayName.group, editionKey.group, editionDisplayName.group);
    return {
        row,
        workKey: workKey.input,
        workDisplayName: workDisplayName.input,
        editionKey: editionKey.input,
        editionDisplayName: editionDisplayName.input
    };
}

function textField(label, placeholder, columnClass, required) {
    const attributes = required ? { required: "required" } : {};
    const input = element("input", {
        type: "text",
        className: "form-control",
        placeholder,
        attributes
    });
    const group = element("div", { className: columnClass });
    group.append(
        element("label", { className: "form-label fw-semibold", text: label }),
        input);
    return { group, input };
}

function sectionHeading(text) {
    return element("h4", { className: "h6 text-body-secondary text-uppercase mt-1 mb-2", text });
}

function renderImportResult(container, imported, isPublic) {
    const created = imported.entities.filter(entity => entity.createdRevision).length;
    const unchanged = imported.entities.length - created;
    const card = element("div", { className: "card card-body border-success" });
    card.append(
        element("div", { className: "d-flex flex-wrap gap-2 align-items-center mb-2" },
            element("h4", { className: "h6 mb-0", text: "Import complete" }),
            badge(isPublic ? "Public" : "Restricted", isPublic ? "success" : "warning")),
        element("p", {
            className: "mb-3 text-body-secondary",
            text: `${imported.entities.length} entities processed · ${created} new revisions · ${unchanged} unchanged.`
        }));

    if (!isPublic) {
        card.append(alertNode(
            "info",
            "This restricted package was not automatically granted to any user. Source grants remain a separate authorization operation."));
    }

    const list = element("div", { className: "list-group list-group-flush" });
    for (const entity of imported.entities) {
        const item = element("div", {
            className: "list-group-item px-0 d-flex flex-wrap justify-content-between gap-2"
        });
        item.append(
            element("div", {},
                element("div", { className: "fw-semibold", text: entity.name }),
                element("div", {
                    className: "small text-body-secondary",
                    text: `${entity.entityType} · ${entity.sourceCode} · revision ${entity.revisionNumber}`
                })),
            badge(entity.createdRevision ? "New revision" : "Unchanged", entity.createdRevision ? "success" : "secondary"));
        list.append(item);
    }
    card.append(list);
    container.replaceChildren(card);
}

function humanize(value) {
    return value.replace(/[A-Z]/g, letter => ` ${letter.toLowerCase()}`)
        .replace(/^./, letter => letter.toUpperCase());
}
