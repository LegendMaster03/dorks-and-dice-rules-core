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
const IMPORT_RESULT_DISPLAY_LIMIT = 100;
const GAME_EDITIONS = ["", "1e", "2e", "3e", "3.5e", "4e", "5e", "5.5e"];
const RELEASE_KINDS = ["", "published", "playtest", "preview", "errata", "srd", "third-party", "other"];

export function installSourceAdministration(app) {
    app.canAdministerSources = app.hostContext.siteMode === DORKS_MODE
        && (app.session.globalRoles ?? []).includes(DEV_ROLE);

    if (app.canAdministerSources && app.activeView === "none") app.activeView = "source-admin";

    const renderNavigation = app.renderNavigation.bind(app);
    app.renderNavigation = () => {
        const nav = renderNavigation();
        if (app.canAdministerSources) nav.append(app.navButton("Source Administration", "source-admin"));
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
    container.append(element("div", { className: "card card-body mb-3" },
        element("h3", { className: "h5 mb-1", text: "Source administration" }),
        element("p", { className: "text-body-secondary mb-0", text: "Preview, validate, and import immutable 5e.tools-shaped source documents. Source identity and D&D edition are recorded separately; import is a Dev control-plane operation. Before an uploaded copy is accepted, Rules Core checks whether its selected source-code partition is already covered by a Rules Lawyer-managed canonical hosted source." })));

    const formCard = element("div", { className: "card card-body" });
    const form = element("form");
    const packageControls = packageFields();
    const releaseControls = releaseFields();
    const json = element("textarea", {
        className: "form-control font-monospace rules-core-patch-input",
        rows: 18,
        placeholder: '{\n  "skill": [\n    { "name": "Arcana", "source": "PHB", "ability": "int" }\n  ]\n}',
        attributes: { required: "required", spellcheck: "false" }
    });

    form.append(
        sectionHeading("Package"),
        packageControls.row,
        sectionHeading("Work, release, and game edition"),
        releaseControls.row,
        element("div", { className: "alert alert-secondary py-2 small" },
            "Use the current canonical game-edition labels (for example 5e and 5.5e). Legacy 2014/2024 labels remain accepted by the backend for older imported datasets and are normalized to 5e/5.5e. The release key identifies this source release; it is not the D&D edition. If one physical JSON file aggregates multiple 5e.tools source codes, use the optional source-code filter to import each logical work/release separately from the same document."),
        sectionHeading("Source document"),
        element("div", { className: "mb-3" },
            element("label", { className: "form-label fw-semibold", text: "5e.tools-shaped JSON" }),
            json,
            element("div", { className: "form-text", text: "Complete selected entity objects are preserved in immutable Source Layer revisions. Reimporting identical content is idempotent. If the selected source is already registered as a canonical hosted source, Rules Core redirects the normal workflow to that definition instead of silently storing another submitted copy." })));

    const previewButton = element("button", { type: "button", className: "btn btn-outline-primary me-2", text: "Preview import" });
    const importButton = element("button", { type: "submit", className: "btn btn-primary", text: "Import source document", disabled: true });
    const result = element("div", { className: "mt-3" });
    form.append(previewButton, importButton, result);
    formCard.append(form);
    container.append(formCard);

    let approvedSnapshot = null;
    let manualHostedOverride = false;
    const controls = [...form.querySelectorAll("input, select, textarea")];
    for (const control of controls) {
        control.addEventListener("input", () => invalidatePreview());
        control.addEventListener("change", () => invalidatePreview());
    }

    function invalidatePreview() {
        approvedSnapshot = null;
        manualHostedOverride = false;
        importButton.disabled = true;
    }

    previewButton.addEventListener("click", async () => {
        result.replaceChildren();
        setButtonBusy(previewButton, true, "Previewing…");
        try {
            const payload = buildPayload(packageControls, releaseControls, json);
            const hosted = await app.api.findHostedSourceMatches({
                json: payload.json,
                fallbackSourceCode: payload.editionKey,
                includedSourceCodes: payload.includedSourceCodes
            });

            if (hosted.fullyCovered && hosted.matches.length && !manualHostedOverride) {
                approvedSnapshot = null;
                importButton.disabled = true;
                result.replaceChildren(renderHostedMatchCard(
                    app,
                    hosted,
                    true,
                    () => {
                        manualHostedOverride = true;
                        previewButton.click();
                    }));
                return;
            }

            const preview = await app.api.previewSourceDocument(payload);
            renderPreviewResult(result, preview);
            if (hosted.matches.length) {
                result.prepend(renderHostedMatchCard(app, hosted, false));
            }
            if (preview.canImport) {
                approvedSnapshot = JSON.stringify(payload);
                importButton.disabled = false;
            } else {
                approvedSnapshot = null;
                importButton.disabled = true;
            }
        } catch (error) {
            approvedSnapshot = null;
            importButton.disabled = true;
            result.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(previewButton, false);
        }
    });

    form.addEventListener("submit", async event => {
        event.preventDefault();
        result.replaceChildren();
        try {
            const payload = buildPayload(packageControls, releaseControls, json);
            const snapshot = JSON.stringify(payload);
            if (!approvedSnapshot || snapshot !== approvedSnapshot) {
                throw new Error("The source import changed after preview. Preview the exact import again before persisting it.");
            }
            setButtonBusy(importButton, true, "Importing…");
            const imported = await app.api.importSourceDocument(payload);
            approvedSnapshot = null;
            manualHostedOverride = false;
            importButton.disabled = true;
            renderImportResult(result, imported, payload.isPublic);
        } catch (error) {
            result.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(importButton, false);
            if (!approvedSnapshot) importButton.disabled = true;
        }
    });
}

function buildPayload(packageControls, releaseControls, json) {
    const rawJson = json.value.trim();
    if (!rawJson) throw new Error("Source JSON is required.");
    try { JSON.parse(rawJson); }
    catch (error) { throw new Error(`Source JSON is not valid JSON: ${error.message}`); }

    const payload = {
        packageKey: packageControls.packageKey.value.trim(),
        packageDisplayName: packageControls.packageDisplayName.value.trim(),
        provider: packageControls.provider.value.trim(),
        license: packageControls.license.value.trim() || null,
        isPublic: packageControls.isPublic.checked,
        workKey: releaseControls.workKey.value.trim(),
        workDisplayName: releaseControls.workDisplayName.value.trim(),
        editionKey: releaseControls.releaseKey.value.trim(),
        editionDisplayName: releaseControls.releaseDisplayName.value.trim(),
        json: rawJson,
        gameEdition: releaseControls.gameEdition.value || null,
        releaseKind: releaseControls.releaseKind.value || null,
        publicationDate: releaseControls.publicationDate.value || null,
        includedSourceCodes: parseSourceCodes(releaseControls.sourceCodes.value)
    };

    for (const name of ["packageKey", "packageDisplayName", "provider", "workKey", "workDisplayName", "editionKey", "editionDisplayName"]) {
        if (!payload[name]) throw new Error(`${humanize(name)} is required.`);
    }
    return payload;
}

function packageFields() {
    const row = element("div", { className: "row g-3 mb-4" });
    const packageKey = textField("Package key", "wotc-srd", "col-lg-3", true);
    const packageDisplayName = textField("Package display name", "Wizards SRD", "col-lg-3", true);
    const provider = textField("Provider", "manual", "col-lg-2", true);
    const license = textField("License", "CC-BY-4.0", "col-lg-2", false);
    const visibilityColumn = element("div", { className: "col-lg-2" });
    visibilityColumn.append(element("label", { className: "form-label fw-semibold d-block", text: "Visibility" }));
    const isPublic = element("input", { type: "checkbox", className: "form-check-input" });
    isPublic.checked = true;
    visibilityColumn.append(element("div", { className: "form-check pt-2" }, isPublic, element("label", { className: "form-check-label ms-2", text: "Public source" })));
    row.append(packageKey.group, packageDisplayName.group, provider.group, license.group, visibilityColumn);
    return { row, packageKey: packageKey.input, packageDisplayName: packageDisplayName.input, provider: provider.input, license: license.input, isPublic };
}

function releaseFields() {
    const row = element("div", { className: "row g-3 mb-3" });
    const workKey = textField("Work key", "srd-5-1", "col-lg-3", true);
    const workDisplayName = textField("Work display name", "System Reference Document 5.1", "col-lg-3", true);
    const releaseKey = textField("Release key", "original", "col-lg-3", true);
    const releaseDisplayName = textField("Release display name", "Original release", "col-lg-3", true);
    const gameEdition = selectField("D&D game edition", GAME_EDITIONS, "col-lg-3", value => value || "Not specified");
    const releaseKind = selectField("Release kind", RELEASE_KINDS, "col-lg-3", value => value || "Not specified");
    const publicationDate = element("input", { type: "date", className: "form-control" });
    const dateGroup = element("div", { className: "col-lg-3" }, element("label", { className: "form-label fw-semibold", text: "Publication date" }), publicationDate);
    const sourceCodes = textField("Source-code filter", "SRD51", "col-lg-3", false);
    sourceCodes.group.append(element("div", { className: "form-text", text: "Optional, comma-separated. Filters a mixed aggregate without modifying the submitted JSON." }));
    row.append(workKey.group, workDisplayName.group, releaseKey.group, releaseDisplayName.group, gameEdition.group, releaseKind.group, dateGroup, sourceCodes.group);
    return {
        row,
        workKey: workKey.input,
        workDisplayName: workDisplayName.input,
        releaseKey: releaseKey.input,
        releaseDisplayName: releaseDisplayName.input,
        gameEdition: gameEdition.select,
        releaseKind: releaseKind.select,
        publicationDate,
        sourceCodes: sourceCodes.input
    };
}

function textField(label, placeholder, columnClass, required) {
    const input = element("input", { type: "text", className: "form-control", placeholder, attributes: required ? { required: "required" } : {} });
    const group = element("div", { className: columnClass }, element("label", { className: "form-label fw-semibold", text: label }), input);
    return { group, input };
}

function selectField(label, values, columnClass, display) {
    const select = element("select", { className: "form-select" });
    for (const value of values) select.append(element("option", { value, text: display(value) }));
    const group = element("div", { className: columnClass }, element("label", { className: "form-label fw-semibold", text: label }), select);
    return { group, select };
}

function parseSourceCodes(value) {
    const values = value.split(",").map(code => code.trim()).filter(Boolean);
    return values.length ? [...new Set(values)] : null;
}

function sectionHeading(text) { return element("h4", { className: "h6 text-body-secondary text-uppercase mt-1 mb-2", text }); }

function renderHostedMatchCard(app, hosted, blocked, overrideAction = null) {
    const card = element("div", { className: "card card-body border-warning mb-3" },
        element("div", { className: "d-flex flex-wrap gap-2 align-items-center mb-2" },
            element("h4", { className: "h6 mb-0", text: blocked ? "Canonical hosted source found" : "Hosted source overlap found" }),
            badge(hosted.fullyCovered ? "Fully covered" : "Partial coverage", "warning")),
        element("p", { className: "mb-2", text: blocked
            ? `The selected source-code partition (${hosted.sourceCodes.join(", ")}) is already covered by a registered live source. The default workflow uses that canonical link instead of storing another submitted copy.`
            : `Some selected source codes (${hosted.sourceCodes.join(", ")}) overlap registered live sources. Review the overlap before intentionally importing another copy.` }));

    const list = element("div", { className: "list-group list-group-flush mb-2" });
    for (const match of hosted.matches) {
        list.append(element("div", { className: "list-group-item px-0 py-2" },
            element("div", { className: "fw-semibold", text: match.displayName }),
            element("div", { className: "small text-body-secondary", text: `${match.definitionKey} · definition revision ${match.revisionNumber} · ${match.packageKey}/${match.workKey}/${match.editionKey} · matches ${match.matchedSourceCodes.join(", ")}` })));
    }
    card.append(list);

    const actions = element("div", { className: "d-flex flex-wrap gap-2" });
    if (app.canManageHostedSources) {
        const hostedButton = element("button", { type: "button", className: "btn btn-sm btn-primary", text: "Open Hosted Sources" });
        hostedButton.addEventListener("click", async () => {
            app.activeView = "hosted-sources";
            await app.render();
        });
        actions.append(hostedButton);
    }
    if (blocked && overrideAction) {
        const overrideButton = element("button", { type: "button", className: "btn btn-sm btn-outline-warning", text: "Preview submitted copy anyway" });
        overrideButton.addEventListener("click", overrideAction);
        actions.append(overrideButton);
    }
    if (actions.childNodes.length) card.append(actions);
    return card;
}

function renderPreviewResult(container, preview) {
    const card = element("div", { className: `card card-body ${preview.canImport ? "border-success" : "border-danger"}` });
    card.append(
        element("div", { className: "d-flex flex-wrap gap-2 align-items-center mb-2" },
            element("h4", { className: "h6 mb-0", text: "Import preview" }),
            badge(preview.canImport ? "Ready to import" : "Blocked", preview.canImport ? "success" : "danger"),
            preview.gameEdition ? badge(preview.gameEdition, "secondary") : null,
            preview.releaseKind ? badge(preview.releaseKind, "secondary") : null),
        element("p", { className: "mb-2 text-body-secondary", text: `${preview.entityCount} entities · ${preview.newEntityCount} new · ${preview.newRevisionCount} new revisions · ${preview.unchangedCount} unchanged.` }));

    for (const conflict of preview.conflicts ?? []) card.append(alertNode("danger", conflict));
    for (const warning of preview.warnings ?? []) card.append(alertNode("warning", warning));

    const list = element("div", { className: "list-group list-group-flush" });
    for (const item of (preview.entities ?? []).slice(0, IMPORT_RESULT_DISPLAY_LIMIT)) {
        const status = item.action === "new-entity" ? ["New entity", "success"]
            : item.action === "new-revision" ? ["New revision", "warning"]
                : ["Unchanged", "secondary"];
        list.append(element("div", { className: "list-group-item px-0 d-flex justify-content-between gap-2" },
            element("div", {}, element("div", { className: "fw-semibold", text: item.name }), element("div", { className: "small text-body-secondary", text: `${item.entityType} · ${item.sourceCode}${item.currentRevisionNumber ? ` · current revision ${item.currentRevisionNumber}` : ""}` })),
            badge(status[0], status[1])));
    }
    card.append(list);
    container.replaceChildren(card);
}

function renderImportResult(container, imported, isPublic) {
    const created = imported.entities.filter(entity => entity.createdRevision).length;
    const unchanged = imported.entities.length - created;
    const card = element("div", { className: "card card-body border-success" });
    card.append(
        element("div", { className: "d-flex flex-wrap gap-2 align-items-center mb-2" },
            element("h4", { className: "h6 mb-0", text: "Import complete" }),
            badge(isPublic ? "Public" : "Restricted", isPublic ? "success" : "warning"),
            imported.gameEdition ? badge(imported.gameEdition, "secondary") : null),
        element("p", { className: "mb-3 text-body-secondary", text: `${imported.entities.length} entities processed · ${created} new revisions · ${unchanged} unchanged.` }));
    if (!isPublic) card.append(alertNode("info", "This restricted package was not automatically granted to any user. Source grants remain a separate authorization operation."));
    const list = element("div", { className: "list-group list-group-flush" });
    for (const entity of imported.entities.slice(0, IMPORT_RESULT_DISPLAY_LIMIT)) {
        list.append(element("div", { className: "list-group-item px-0 d-flex flex-wrap justify-content-between gap-2" },
            element("div", {}, element("div", { className: "fw-semibold", text: entity.name }), element("div", { className: "small text-body-secondary", text: `${entity.entityType} · ${entity.sourceCode} · revision ${entity.revisionNumber}` })),
            badge(entity.createdRevision ? "New revision" : "Unchanged", entity.createdRevision ? "success" : "secondary")));
    }
    card.append(list);
    container.replaceChildren(card);
}

function humanize(value) { return value.replace(/[A-Z]/g, letter => ` ${letter.toLowerCase()}`).replace(/^./, letter => letter.toUpperCase()); }
