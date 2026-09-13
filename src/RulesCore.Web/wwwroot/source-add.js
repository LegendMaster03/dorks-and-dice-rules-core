import {
    alertNode,
    badge,
    describeError,
    element,
    formatDate,
    setButtonBusy
} from "./ui.js";

const DORKS_MODE = "dorks-and-dice";

export function installSourceAdd(app) {
    app.canAddSource = app.hostContext.siteMode === DORKS_MODE && Boolean(app.session.user);
    if (!app.canAddSource) return;

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        await renderActiveView(container);
        if (app.activeView !== "library") return;
        container.prepend(await buildAddSourceCard(app));
    };
}

async function buildAddSourceCard(app) {
    const card = element("section", { className: "card card-body mb-3" });
    const heading = element("div", {
        className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3"
    },
    element("div", {},
        element("h4", { className: "h5 mb-1", text: "Add Source" }),
        element("p", {
            className: "text-body-secondary mb-0",
            text: "Add a 5e.tools source for your account. Choose a file or a web source; Rules Core handles the internal source metadata and source-code partitioning."
        })),
    badge("Your account", "secondary"));
    card.append(heading);

    const form = element("form", { className: "row g-3 align-items-end" });
    const kind = element("select", { className: "form-select" },
        element("option", { value: "web", text: "Web source" }),
        element("option", { value: "upload", text: "Upload file" }));
    const kindGroup = element("div", { className: "col-md-3" },
        element("label", { className: "form-label fw-semibold", text: "Source type" }),
        kind);

    const url = element("input", {
        type: "url",
        className: "form-control",
        placeholder: "https://github.com/5etools-mirror-3/5etools-src/tree/main/data"
    });
    const urlGroup = element("div", { className: "col-md-7" },
        element("label", { className: "form-label fw-semibold", text: "Web source URL" }),
        url);

    const file = element("input", {
        type: "file",
        className: "form-control",
        attributes: { accept: ".json,application/json" }
    });
    const fileGroup = element("div", { className: "col-md-7 d-none" },
        element("label", { className: "form-label fw-semibold", text: "Source file" }),
        file);

    const addButton = element("button", {
        type: "submit",
        className: "btn btn-primary w-100",
        text: "Add source"
    });
    const actionGroup = element("div", { className: "col-md-2" }, addButton);
    const result = element("div", { className: "col-12" });
    const existing = element("div", { className: "col-12" });

    form.append(kindGroup, urlGroup, fileGroup, actionGroup, result, existing);
    card.append(form);

    function updateKind() {
        const web = kind.value === "web";
        urlGroup.classList.toggle("d-none", !web);
        fileGroup.classList.toggle("d-none", web);
        url.required = web;
        file.required = !web;
    }
    kind.addEventListener("change", updateKind);
    updateKind();

    form.addEventListener("submit", async event => {
        event.preventDefault();
        result.replaceChildren();
        setButtonBusy(addButton, true, "Adding…");
        try {
            let payload;
            if (kind.value === "web") {
                const value = url.value.trim();
                if (!value) throw new Error("Web source URL is required.");
                payload = { kind: "web", url: value };
            } else {
                const selected = file.files?.[0];
                if (!selected) throw new Error("Select a JSON source file.");
                payload = {
                    kind: "upload",
                    fileName: selected.name,
                    json: await selected.text()
                };
            }

            const added = await app.api.addCurrentUserSource(payload);
            result.replaceChildren(alertNode(
                "success",
                `${added.displayName} added. ${added.sourceCodeCount} source code(s), ${added.entityCount} entities are now available to your account.`));
            if (kind.value === "upload") file.value = "";
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

async function renderExistingSources(app, container, result) {
    try {
        const sources = await app.api.getCurrentUserSources();
        container.replaceChildren();
        if (!sources.length) return;

        const details = element("details", { className: "mt-1" });
        details.append(element("summary", {
            className: "fw-semibold",
            text: `Your added sources (${sources.length})`
        }));
        const list = element("div", { className: "list-group list-group-flush mt-2" });
        for (const source of sources) {
            const actions = element("div", { className: "d-flex gap-2 align-items-start" });
            if (source.kind === "web") {
                const refresh = element("button", {
                    type: "button",
                    className: "btn btn-sm btn-outline-primary",
                    text: "Refresh"
                });
                refresh.addEventListener("click", async () => {
                    result.replaceChildren();
                    setButtonBusy(refresh, true, "Refreshing…");
                    try {
                        const refreshed = await app.api.refreshCurrentUserSource(source.id);
                        result.replaceChildren(alertNode(
                            "success",
                            `${refreshed.displayName} refreshed. ${refreshed.entityCount} entities processed.`));
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
                `${source.sourceCodeCount} source code(s)`,
                `${source.entityCount} entities`,
                `updated ${formatDate(source.refreshedAt)}`
            ].join(" · ");
            list.append(element("div", { className: "list-group-item px-0" },
                element("div", { className: "d-flex flex-wrap justify-content-between gap-3" },
                    element("div", {},
                        element("div", { className: "fw-semibold", text: source.displayName }),
                        element("div", { className: "small text-body-secondary", text: metadata }),
                        source.url
                            ? element("div", { className: "small text-break text-body-secondary", text: source.url })
                            : null),
                    actions)));
        }
        details.append(list);
        container.append(details);
    } catch (error) {
        container.replaceChildren(alertNode("warning", `Added sources could not be loaded: ${describeError(error)}`));
    }
}
