import {
    alertNode,
    describeError,
    element,
    formatDate,
    setButtonBusy
} from "./ui.js";

const DORKS_MODE = "dorks-and-dice";
const IMPORT_POLL_INTERVAL_MS = 2000;

export function installSourceAdd(app) {
    app.canAddSource = app.hostContext.siteMode === DORKS_MODE && Boolean(app.session.user);
    if (!app.canAddSource) return;

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        await renderActiveView(container);
        if (app.activeView !== "library" || app.libraryRoute?.kind !== "index") return;

        const card = await buildAddSourceCard(app);
        const libraryLead = container.querySelector(":scope > .rules-core-library-hero");
        if (libraryLead) libraryLead.after(card);
        else container.prepend(card);
    };
}

async function buildAddSourceCard(app) {
    const card = element("section", { className: "card card-body mb-3 rules-core-panel rules-core-add-source" });
    card.append(element("div", { className: "mb-3" },
        element("h4", { className: "h5 mb-1", text: "Add Source" }),
        element("p", {
            className: "small text-body-secondary mb-0",
            text: "Add a compatible file or Web source. Rules Core detects the format and records publication provenance for you."
        })));

    let currentKind = "web";
    const webMode = element("button", { type: "button", className: "btn btn-sm btn-outline-secondary", text: "Web source" });
    const uploadMode = element("button", { type: "button", className: "btn btn-sm btn-outline-secondary", text: "Upload file" });
    const modeGroup = element("div", { className: "btn-group mb-3", role: "group", ariaLabel: "Source type" }, webMode, uploadMode);

    const form = element("form", { className: "row g-2 align-items-end" });
    const url = element("input", {
        type: "url",
        className: "form-control",
        placeholder: "https://github.com/5etools-mirror-3/5etools-src/tree/main/data"
    });
    const urlGroup = element("div", { className: "col-md-9" },
        element("label", { className: "form-label fw-semibold", text: "Web source URL" }), url);

    const file = element("input", { type: "file", className: "form-control" });
    const fileGroup = element("div", { className: "col-md-9 d-none" },
        element("label", { className: "form-label fw-semibold", text: "Source file" }), file);

    const addButton = element("button", { type: "submit", className: "btn btn-primary w-100", text: "Add source" });
    const actionGroup = element("div", { className: "col-md-3" }, addButton);
    const result = element("div", { className: "mt-3" });
    const existing = element("div", { className: "mt-2" });

    form.append(urlGroup, fileGroup, actionGroup);
    card.append(modeGroup, form, result, existing);

    function updateKind(kind) {
        currentKind = kind;
        const web = currentKind === "web";
        urlGroup.classList.toggle("d-none", !web);
        fileGroup.classList.toggle("d-none", web);
        url.required = web;
        file.required = !web;
        webMode.classList.toggle("active", web);
        uploadMode.classList.toggle("active", !web);
        webMode.setAttribute("aria-pressed", web ? "true" : "false");
        uploadMode.setAttribute("aria-pressed", web ? "false" : "true");
    }
    webMode.addEventListener("click", () => updateKind("web"));
    uploadMode.addEventListener("click", () => updateKind("upload"));
    updateKind("web");

    form.addEventListener("submit", async event => {
        event.preventDefault();
        result.replaceChildren();
        setButtonBusy(addButton, true, currentKind === "web" ? "Queueing…" : "Adding…");
        try {
            let payload;
            if (currentKind === "web") {
                const value = url.value.trim();
                if (!value) throw new Error("Web source URL is required.");
                payload = { kind: "web", url: value };
            } else {
                const selected = file.files?.[0];
                if (!selected) throw new Error("Select a source file.");
                payload = {
                    kind: "upload",
                    fileName: selected.name,
                    contentBase64: arrayBufferToBase64(await selected.arrayBuffer())
                };
            }

            const added = await app.api.addCurrentUserSource(payload);
            if (added?.status === "queued" || added?.status === "running") {
                result.replaceChildren(alertNode("info", `${added.displayName} is queued for import. You can leave this page; Rules Core will continue processing it in the background.`));
            } else {
                result.replaceChildren(alertNode("success", `${added.displayName} added. ${added.sourceCodeCount} publication(s), ${added.entityCount} source record(s) are now available to your account.`));
            }
            if (currentKind === "upload") file.value = "";
            await renderExistingSources(app, existing, result);
        } catch (error) {
            result.replaceChildren(alertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(addButton, false);
        }
    });

    await renderExistingSources(app, existing, result);
    return card;
}

function arrayBufferToBase64(buffer) {
    const bytes = new Uint8Array(buffer);
    const chunkSize = 0x8000;
    let binary = "";
    for (let offset = 0; offset < bytes.length; offset += chunkSize) {
        binary += String.fromCharCode(...bytes.subarray(offset, offset + chunkSize));
    }
    return btoa(binary);
}

async function renderExistingSources(app, container, result) {
    try {
        const [sources, jobs] = await Promise.all([
            app.api.getCurrentUserSources(),
            app.api.backend("/api/sources/current-user/import-jobs")
        ]);
        container.replaceChildren();

        const visibleJobs = jobs.filter(job => job.status !== "completed");
        if (!sources.length && !visibleJobs.length) return;

        if (visibleJobs.length) {
            const activity = element("div", { className: "border rounded p-2" },
                element("div", { className: "small fw-semibold mb-1", text: "Import activity" }));
            for (const job of visibleJobs) {
                const running = job.status === "running";
                const failed = job.status === "failed";
                const label = failed ? "Import failed" : running ? "Importing" : "Queued";
                const metadata = job.operation === "refresh" ? "Web source refresh" : "Web source";
                activity.append(element("div", {
                    className: "d-flex flex-wrap justify-content-between align-items-start gap-2 py-2 border-top"
                },
                element("div", { className: "flex-grow-1" },
                    element("div", { className: "fw-semibold", text: job.displayName }),
                    element("div", { className: "small text-body-secondary text-break", text: `${metadata} · ${job.url ?? ""}` }),
                    failed && job.error ? element("div", { className: "small text-danger mt-1", text: job.error }) : null),
                element("span", { className: failed ? "badge text-bg-danger" : "badge text-bg-secondary", text: label })));
            }
            container.append(activity);
        }

        if (sources.length) {
            const details = element("details", { className: "mt-2" });
            details.append(element("summary", { className: "fw-semibold", text: `Your added sources (${sources.length})` }));
            const list = element("div", { className: "list-group list-group-flush mt-2" });
            for (const source of sources) {
                const actions = element("div", { className: "d-flex gap-2 align-items-start" });
                if (source.kind === "web") {
                    const refresh = element("button", { type: "button", className: "btn btn-sm btn-outline-primary", text: "Refresh" });
                    refresh.addEventListener("click", async () => {
                        result.replaceChildren();
                        setButtonBusy(refresh, true, "Queueing…");
                        try {
                            const job = await app.api.refreshCurrentUserSource(source.id);
                            result.replaceChildren(alertNode("info", `${job.displayName} refresh queued. Rules Core will continue processing it in the background.`));
                            await renderExistingSources(app, container, result);
                        } catch (error) {
                            result.replaceChildren(alertNode("danger", describeError(error)));
                        } finally {
                            setButtonBusy(refresh, false);
                        }
                    });
                    actions.append(refresh);
                }

                const metadata = [
                    source.kind === "web" ? "Web source" : "Uploaded file",
                    `${source.sourceCodeCount} publication(s)`,
                    `${source.entityCount} source record(s)`,
                    `updated ${formatDate(source.refreshedAt)}`
                ].join(" · ");
                list.append(element("div", { className: "list-group-item px-0" },
                    element("div", { className: "d-flex flex-wrap justify-content-between gap-3" },
                        element("div", {},
                            element("div", { className: "fw-semibold", text: source.displayName }),
                            element("div", { className: "small text-body-secondary", text: metadata }),
                            source.url ? element("div", { className: "small text-break text-body-secondary", text: source.url }) : null),
                        actions)));
            }
            details.append(list);
            container.append(details);
        }

        if (jobs.some(job => job.status === "queued" || job.status === "running")) scheduleImportPoll(app, container, result);
    } catch (error) {
        container.replaceChildren(alertNode("warning", `Added sources could not be loaded: ${describeError(error)}`));
    }
}

function scheduleImportPoll(app, container, result) {
    if (container._rulesCoreImportPollTimer) return;
    container._rulesCoreImportPollTimer = setTimeout(async () => {
        container._rulesCoreImportPollTimer = null;
        if (!container.isConnected) return;
        await renderExistingSources(app, container, result);
    }, IMPORT_POLL_INTERVAL_MS);
}
