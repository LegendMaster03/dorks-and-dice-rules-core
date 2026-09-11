import {
    alertNode,
    badge,
    clear,
    describeError,
    element,
    setButtonBusy
} from "./ui.js";

const RULES_LAWYER_ROLE = "Rules Lawyer";
const DORKS_MODE = "dorks-and-dice";
const FORMAT_KINDS = ["5etools-json", "legacy-srd-text"];
const RESOURCE_KINDS = ["direct-json", "json-index", "github-tree", "html-index"];
const GAME_EDITIONS = ["", "1e", "2e", "3e", "3.5e", "4e", "5e", "5.5e"];
const RELEASE_KINDS = ["", "published", "playtest", "preview", "errata", "srd", "third-party", "other"];

export function installHostedSourceAuthoring(app) {
    app.canManageHostedSources = app.hostContext.siteMode === DORKS_MODE
        && (app.session.globalRoles ?? []).includes(RULES_LAWYER_ROLE);

    const renderNavigation = app.renderNavigation.bind(app);
    app.renderNavigation = () => {
        const nav = renderNavigation();
        if (app.canManageHostedSources) nav.append(app.navButton("Hosted Sources", "hosted-sources"));
        return nav;
    };

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        if (app.activeView === "hosted-sources") {
            await renderHostedSources(app, container);
            return;
        }
        await renderActiveView(container);
    };
}

async function renderHostedSources(app, container) {
    clear(container);
    container.append(element("div", { className: "card card-body mb-3" },
        element("h3", { className: "h5 mb-1", text: "Hosted source definitions" }),
        element("p", { className: "text-body-secondary mb-0", text: "Register canonical live source locations once. Refreshing fetches the current remote documents and writes only changed immutable Source Layer revisions; runtime rules never depend on the remote host remaining online." })));

    const result = element("div", { className: "mb-3" });
    const editor = buildEditor(app, result);
    const catalog = element("div");
    container.append(result, editor.card, catalog);
    await renderCatalog(app, catalog, editor, result);
}

function buildEditor(app, result) {
    const card = element("div", { className: "card card-body mb-3" });
    const form = element("form");
    const definitionKey = textField("Definition key", "5etools-srd51", "col-lg-3", true);
    const displayName = textField("Display name", "SRD 5.1 public corpus", "col-lg-3", true);
    const formatKind = selectField("Import format", FORMAT_KINDS, "col-lg-3", value => value);
    const packageKey = textField("Package key", "wotc-srd-cc", "col-lg-3", true);
    const packageDisplayName = textField("Package display name", "Wizards SRD", "col-lg-3", true);
    const provider = textField("Provider", "Wizards of the Coast", "col-lg-3", true);
    const license = textField("License", "CC-BY-4.0", "col-lg-3", false);
    const workKey = textField("Work key", "srd-5-1", "col-lg-3", true);
    const workDisplayName = textField("Work display name", "System Reference Document 5.1", "col-lg-3", true);
    const editionKey = textField("Release key", "original", "col-lg-3", true);
    const editionDisplayName = textField("Release display name", "Original release", "col-lg-3", true);
    const gameEdition = selectField("D&D game edition", GAME_EDITIONS, "col-lg-3", value => value || "Not specified");
    const releaseKind = selectField("Release kind", RELEASE_KINDS, "col-lg-3", value => value || "Not specified");
    const publicationDate = element("input", { type: "date", className: "form-control" });
    const sourceCodes = textField("Source-code filter", "SRD51", "col-lg-3", false);
    const resources = element("textarea", {
        className: "form-control font-monospace",
        rows: 7,
        placeholder: "direct-json | https://example.test/data/feats.json\njson-index | https://example.test/data/spells/index.json\ngithub-tree | https://github.com/example/repo/tree/main/data\nhtml-index | https://example.test/legacy-srd/",
        attributes: { required: "required", spellcheck: "false" }
    });
    const note = element("textarea", { className: "form-control", rows: 2, placeholder: "Optional provenance or maintenance note" });
    const isPublic = element("input", { type: "checkbox", className: "form-check-input" });
    isPublic.checked = true;
    const isEnabled = element("input", { type: "checkbox", className: "form-check-input" });
    isEnabled.checked = true;

    form.append(
        sectionHeading("Identity"),
        element("div", { className: "row g-3 mb-3" }, definitionKey.group, displayName.group, formatKind.group, packageKey.group),
        element("div", { className: "row g-3 mb-4" },
            packageDisplayName.group,
            provider.group,
            license.group,
            checkboxGroup("Public source", isPublic),
            checkboxGroup("Enabled", isEnabled)),
        sectionHeading("Logical work and release"),
        element("div", { className: "row g-3 mb-3" }, workKey.group, workDisplayName.group, editionKey.group, editionDisplayName.group),
        element("div", { className: "row g-3 mb-4" },
            gameEdition.group,
            releaseKind.group,
            element("div", { className: "col-lg-3" }, element("label", { className: "form-label fw-semibold", text: "Publication date" }), publicationDate),
            sourceCodes.group),
        sectionHeading("Canonical live resources"),
        element("div", { className: "mb-3" },
            element("label", { className: "form-label fw-semibold", text: "Resources" }),
            resources,
            element("div", { className: "form-text", text: "One resource per line as kind | HTTPS URL. 5etools-json supports direct-json, json-index, and github-tree. legacy-srd-text supports html-index and github-tree; legacy GitHub trees enumerate Markdown files. Source-code filters partition 5e.tools aggregates; legacy SRDs use one Rules Core normalization label such as SRD3 or SRD35." })),
        element("div", { className: "mb-3" }, element("label", { className: "form-label fw-semibold", text: "Note" }), note));

    const saveButton = element("button", { type: "submit", className: "btn btn-primary me-2", text: "Save hosted source" });
    const resetButton = element("button", { type: "button", className: "btn btn-outline-secondary", text: "New definition" });
    form.append(saveButton, resetButton);
    card.append(form);

    form.addEventListener("submit", async event => {
        event.preventDefault();
        result.replaceChildren();
        setButtonBusy(saveButton, true, "Saving…");
        try {
            const key = definitionKey.input.value.trim();
            if (!key) throw new Error("Definition key is required.");
            const payload = {
                displayName: required(displayName.input, "Display name"),
                formatKind: formatKind.select.value,
                packageKey: required(packageKey.input, "Package key"),
                packageDisplayName: required(packageDisplayName.input, "Package display name"),
                provider: required(provider.input, "Provider"),
                license: license.input.value.trim() || null,
                isPublic: isPublic.checked,
                workKey: required(workKey.input, "Work key"),
                workDisplayName: required(workDisplayName.input, "Work display name"),
                editionKey: required(editionKey.input, "Release key"),
                editionDisplayName: required(editionDisplayName.input, "Release display name"),
                gameEdition: gameEdition.select.value || null,
                releaseKind: releaseKind.select.value || null,
                publicationDate: publicationDate.value || null,
                includedSourceCodes: parseSourceCodes(sourceCodes.input.value),
                resources: parseResources(resources.value, formatKind.select.value),
                isEnabled: isEnabled.checked,
                note: note.value.trim() || null
            };
            const saved = await app.api.setHostedSource(key, payload);
            result.replaceChildren(alertNode("success", saved.createdRevision
                ? `Hosted source '${saved.displayName}' saved as definition revision ${saved.revisionNumber}.`
                : `Hosted source '${saved.displayName}' is unchanged; no duplicate definition revision was created.`));
            await renderCatalog(app, card.nextElementSibling, editorApi, result);
        } catch (error) {
            result.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(saveButton, false);
        }
    });

    resetButton.addEventListener("click", () => reset());

    function reset() {
        form.reset();
        formatKind.select.value = "5etools-json";
        isPublic.checked = true;
        isEnabled.checked = true;
        definitionKey.input.readOnly = false;
    }

    function edit(definition) {
        definitionKey.input.value = definition.key;
        definitionKey.input.readOnly = true;
        displayName.input.value = definition.displayName;
        formatKind.select.value = definition.formatKind ?? "5etools-json";
        packageKey.input.value = definition.packageKey;
        packageDisplayName.input.value = definition.packageDisplayName;
        provider.input.value = definition.provider;
        license.input.value = definition.license ?? "";
        isPublic.checked = definition.isPublic;
        workKey.input.value = definition.workKey;
        workDisplayName.input.value = definition.workDisplayName;
        editionKey.input.value = definition.editionKey;
        editionDisplayName.input.value = definition.editionDisplayName;
        gameEdition.select.value = definition.gameEdition ?? "";
        releaseKind.select.value = definition.releaseKind ?? "";
        publicationDate.value = definition.publicationDate ?? "";
        sourceCodes.input.value = (definition.includedSourceCodes ?? []).join(", ");
        resources.value = (definition.resources ?? []).map(resource => `${resource.kind} | ${resource.uri}`).join("\n");
        isEnabled.checked = definition.isEnabled;
        note.value = definition.note ?? "";
        card.scrollIntoView({ behavior: "smooth", block: "start" });
    }

    const editorApi = { card, edit, reset };
    return editorApi;
}

async function renderCatalog(app, container, editor, result) {
    if (!container) return;
    clear(container);
    const definitions = await app.api.getHostedSources(true);
    const card = element("div", { className: "card card-body" },
        element("div", { className: "d-flex justify-content-between align-items-center mb-3" },
            element("h4", { className: "h6 mb-0", text: "Registered hosted sources" }),
            badge(`${definitions.length} definitions`, "secondary")));

    if (!definitions.length) {
        card.append(element("p", { className: "text-body-secondary mb-0", text: "No hosted sources are registered yet." }));
        container.append(card);
        return;
    }

    const list = element("div", { className: "list-group list-group-flush" });
    for (const definition of definitions) {
        const previewButton = element("button", { type: "button", className: "btn btn-sm btn-outline-primary", text: "Preview live" });
        const refreshButton = element("button", { type: "button", className: "btn btn-sm btn-primary", text: "Refresh" });
        const editButton = element("button", { type: "button", className: "btn btn-sm btn-outline-secondary", text: "Edit" });
        previewButton.disabled = !definition.isEnabled;
        refreshButton.disabled = !definition.isEnabled;
        editButton.addEventListener("click", () => editor.edit(definition));
        previewButton.addEventListener("click", async () => {
            result.replaceChildren();
            setButtonBusy(previewButton, true, "Previewing…");
            try {
                const response = await app.api.previewHostedSource(definition.id);
                const preview = response.preview;
                result.replaceChildren(alertNode(
                    preview.canImport ? "success" : "warning",
                    `${definition.displayName}: ${response.documents.length} live document(s), ${preview.entityCount} entities, ${preview.newEntityCount} new, ${preview.newRevisionCount} changed revisions, ${preview.unchangedCount} unchanged.`));
            } catch (error) {
                result.replaceChildren(alertNode("danger", describeError(error)));
            } finally {
                setButtonBusy(previewButton, false);
            }
        });
        refreshButton.addEventListener("click", async () => {
            result.replaceChildren();
            setButtonBusy(refreshButton, true, "Refreshing…");
            try {
                const response = await app.api.refreshHostedSource(definition.id);
                const created = response.import.entities.filter(entity => entity.createdRevision).length;
                result.replaceChildren(alertNode(
                    "success",
                    `${definition.displayName}: ${response.import.entities.length} entities processed from ${response.documents.length} live document(s); ${created} immutable source revision(s) created.`));
            } catch (error) {
                result.replaceChildren(alertNode("danger", describeError(error)));
            } finally {
                setButtonBusy(refreshButton, false);
            }
        });

        list.append(element("div", { className: "list-group-item px-0" },
            element("div", { className: "d-flex flex-wrap justify-content-between gap-3" },
                element("div", {},
                    element("div", { className: "fw-semibold", text: definition.displayName }),
                    element("div", { className: "small text-body-secondary", text: `${definition.key} · definition revision ${definition.revisionNumber} · ${definition.packageKey}/${definition.workKey}/${definition.editionKey}` }),
                    element("div", { className: "small text-body-secondary", text: `${definition.formatKind} · ${(definition.includedSourceCodes ?? []).join(", ") || "all source codes"} · ${(definition.resources ?? []).length} root resource(s)` }),
                    element("div", { className: "d-flex gap-2 mt-1" },
                        badge(definition.isEnabled ? "Enabled" : "Disabled", definition.isEnabled ? "success" : "secondary"),
                        badge(definition.isPublic ? "Public" : "Restricted", definition.isPublic ? "success" : "warning"))),
                element("div", { className: "d-flex gap-2 align-items-start" }, editButton, previewButton, refreshButton))));
    }
    card.append(list);
    container.append(card);
}

function parseResources(value, formatKind) {
    const allowedKinds = formatKind === "legacy-srd-text"
        ? ["html-index", "github-tree"]
        : ["direct-json", "json-index", "github-tree"];
    const resources = value.split(/\r?\n/).map(line => line.trim()).filter(Boolean).map(line => {
        const separator = line.indexOf("|");
        if (separator < 0) throw new Error(`Resource '${line}' must use kind | HTTPS URL.`);
        const kind = line.slice(0, separator).trim();
        const uri = line.slice(separator + 1).trim();
        if (!RESOURCE_KINDS.includes(kind)) throw new Error(`Unsupported resource kind '${kind}'.`);
        if (!allowedKinds.includes(kind)) throw new Error(`Resource kind '${kind}' is not valid for ${formatKind}.`);
        if (!uri) throw new Error("Hosted resource URL can not be blank.");
        return { kind, uri };
    });
    if (!resources.length) throw new Error("At least one hosted resource is required.");
    return resources;
}

function parseSourceCodes(value) {
    const values = value.split(",").map(code => code.trim()).filter(Boolean);
    return values.length ? [...new Set(values)] : null;
}

function required(input, label) {
    const value = input.value.trim();
    if (!value) throw new Error(`${label} is required.`);
    return value;
}

function textField(label, placeholder, columnClass, isRequired) {
    const input = element("input", {
        type: "text",
        className: "form-control",
        placeholder,
        attributes: isRequired ? { required: "required" } : {}
    });
    const group = element("div", { className: columnClass },
        element("label", { className: "form-label fw-semibold", text: label }), input);
    return { group, input };
}

function selectField(label, values, columnClass, display) {
    const select = element("select", { className: "form-select" });
    for (const value of values) select.append(element("option", { value, text: display(value) }));
    const group = element("div", { className: columnClass },
        element("label", { className: "form-label fw-semibold", text: label }), select);
    return { group, select };
}

function checkboxGroup(label, input) {
    return element("div", { className: "col-lg-3" },
        element("label", { className: "form-label fw-semibold d-block", text: label }),
        element("div", { className: "form-check pt-2" }, input, element("label", { className: "form-check-label ms-2", text: label })));
}

function sectionHeading(text) {
    return element("h4", { className: "h6 text-body-secondary text-uppercase mt-1 mb-2", text });
}
