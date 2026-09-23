import {
    actionBar,
    alertNode,
    clear,
    describeError,
    element,
    field,
    sectionHeading,
    setButtonBusy
} from "./ui.js";

const DORKS_MODE = "dorks-and-dice";

export function installSourceRemoval(app) {
    app.canRemoveSource = app.hostContext.siteMode === DORKS_MODE && Boolean(app.session.user);
    if (!app.canRemoveSource) return;

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        await renderActiveView(container);
        if (app.activeView !== "sources") return;

        const notice = app._sourceRemovalNotice ?? null;
        app._sourceRemovalNotice = null;
        const panel = await buildSourceRemovalPanel(app, notice);
        if (!panel) return;

        const addSource = container.querySelector(":scope > .rules-core-add-source");
        if (addSource) {
            addSource.after(panel);
        } else {
            container.prepend(panel);
        }
    };
}

async function buildSourceRemovalPanel(app, notice) {
    let sources;
    let jobs;
    try {
        [sources, jobs] = await Promise.all([
            app.api.getCurrentUserSources(),
            app.api.getCurrentUserSourceImportJobs()
        ]);
    } catch (error) {
        return element("section", {
            className: "card card-body mb-3 rules-core-panel rules-core-source-removal"
        }, alertNode("warning", `Source access controls could not be loaded: ${describeError(error)}`));
    }

    if (!sources.length && !notice) return null;

    const panel = element("section", {
        className: "card card-body mb-3 rules-core-panel rules-core-source-removal"
    });
    panel.append(sectionHeading({
        title: "Manage source access",
        description: "Removing a source revokes your access and removes its account registration. Stored Source Layer evidence, canonical identity, revision history, and Rules Layer decisions are retained. Re-adding the same source later can reuse that existing identity and history.",
        level: 4
    }));

    if (notice) {
        panel.append(alertNode(notice.kind ?? "success", notice.message));
    }

    if (!sources.length) {
        panel.append(element("div", {
            className: "small text-body-secondary",
            text: "No account-added sources remain."
        }));
        return panel;
    }

    const activeSourceIds = new Set(
        jobs
            .filter(job => job.status === "queued" || job.status === "running")
            .map(job => job.currentUserSourceId)
            .filter(Boolean));
    const result = element("div", { className: "mt-2" });
    const select = element("select", {
        className: "form-select form-select-sm",
        ariaLabel: "Added source to remove"
    });
    for (const source of sources) {
        const active = activeSourceIds.has(source.id);
        select.append(element("option", {
            value: source.id,
            text: `${source.displayName}${active ? " (import active)" : ""}`
        }));
    }

    const removeButton = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-danger",
        text: "Remove source"
    });
    const state = element("div", { className: "small text-body-secondary mt-2" });

    function selectedSource() {
        return sources.find(source => source.id === select.value) ?? null;
    }

    function updateState() {
        const source = selectedSource();
        const active = source && activeSourceIds.has(source.id);
        removeButton.disabled = !source || active;
        state.textContent = active
            ? "This source currently has an active import. Removal becomes available after the import completes or fails."
            : "This changes account access only. It does not erase imported source history or Rules Lawyer work.";
    }

    select.addEventListener("change", updateState);
    updateState();

    removeButton.addEventListener("click", async () => {
        const source = selectedSource();
        if (!source || activeSourceIds.has(source.id)) return;

        const confirmed = window.confirm(
            `Remove "${source.displayName}" from your account?\n\n` +
            "Your access and account registration will be removed. Stored source history, canonical identity, and Rules Layer decisions will be retained for safe reuse if the same source is added again.");
        if (!confirmed) return;

        clear(result);
        setButtonBusy(removeButton, true, "Removing…");
        select.disabled = true;
        try {
            await app.api.removeCurrentUserSource(source.id);
            app._sourceRemovalNotice = {
                kind: "success",
                message: `${source.displayName} was removed from your account. Stored source identity, revision history, and Rules Layer decisions were retained.`
            };
            await app.render();
        } catch (error) {
            result.append(alertNode("danger", describeError(error)));
            select.disabled = false;
            setButtonBusy(removeButton, false);
            updateState();
        }
    });

    panel.append(
        element("div", { className: "rules-core-toolbar" },
            field("Added source", select),
            actionBar(removeButton)),
        state,
        result);
    return panel;
}