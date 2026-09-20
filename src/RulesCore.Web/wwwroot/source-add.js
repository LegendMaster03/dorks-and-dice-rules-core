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
        if (app.activeView !== "sources") return;

        const card = await buildAddSourceCard(app);
        const libraryLead = container.querySelector(":scope > .rules-core-sources-hero");
        if (libraryLead) {
            libraryLead.after(card);
        } else {
            container.prepend(card);
        }
    };
}

async function buildAddSourceCard(app) {
    const card = element("section", {
        className: "card card-body mb-3 rules-core-panel rules-core-add-source"
    });
    card.append(element("div", { className: "mb-3" },
        element("h4", { className: "h5 mb-1", text: "Add Source" }),
        element("p", {
            className: "small text-body-secondary mb-0",
            text: "Add a compatible file or Web source. Rules Core detects the format and records publication provenance for you."
        })));

    let currentKind = "web";
    const webMode = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary",
        text: "Web source"
    });
    const uploadMode = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary",
        text: "Upload file"
    });
    const modeGroup = element("div", {
        className: "btn-group mb-3",
        role: "group",
        ariaLabel: "Source type"
    }, webMode, uploadMode);

    const form = element("form", { className: "row g-2 align-items-end" });
    const url = element("input", {
        type: "url",
        className: "form-control",
        placeholder: "https://github.com/5etools-mirror-3/5etools-src/tree/main/data"
    });
    const urlGroup = element("div", { className: "col-md-9" },
        element("label", { className: "form-label fw-semibold", text: "Web source URL" }),
        url);

    const file = element("input", {
        type: "file",
        className: "form-control"
    });
    const fileGroup = element("div", { className: "col-md-9 d-none" },
        element("label", { className: "form-label fw-semibold", text: "Source file" }),
        file);

    const addButton = element("button", {
        type: "submit",
        className: "btn btn-primary w-100",
        text: "Add source"
    });
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
                result.replaceChildren(dismissibleAlertNode(
                    "info",
                    `${added.displayName} was queued. Import activity below shows whether it is waiting, running, complete, or needs attention. Rules Core continues processing if you leave this page.`));
            } else {
                const issues = added?.id
                    ? await loadReconciliationIssues(() =>
                        app.api.getCurrentUserSourceReconciliationIssues(added.id))
                    : [];
                result.replaceChildren(dismissibleAlertNode(
                    issues.length ? "warning" : "success",
                    issues.length
                        ? `${added.displayName} added. Its source material is available, but ${issues.length} canonical reconciliation issue${issues.length === 1 ? "" : "s"} need review.`
                        : `${added.displayName} added. ${added.sourceCodeCount} publication(s), ${added.entityCount} source record(s) are now available to your account.`));
            }
            if (currentKind === "upload") file.value = "";
            await renderExistingSources(app, existing, result);
        } catch (error) {
            result.replaceChildren(dismissibleAlertNode("danger", describeError(error)));
        } finally {
            setButtonBusy(addButton, false);
        }
    });

    await renderExistingSources(app, existing, result);
    return card;
}

function dismissibleAlertNode(kind, message) {
    const alert = element("div", {
        className: `alert alert-${kind} alert-dismissible mb-3`,
        attributes: { role: "alert" }
    }, message);
    const dismiss = element("button", {
        type: "button",
        className: "btn-close",
        ariaLabel: "Dismiss notification",
        title: "Dismiss notification"
    });
    dismiss.addEventListener("click", () => alert.remove());
    alert.append(dismiss);
    return alert;
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
            app.api.getCurrentUserSourceImportJobs()
        ]);
        const sourceIssues = new Map(await Promise.all(sources.map(async source => [
            source.id,
            await loadReconciliationIssues(() =>
                app.api.getCurrentUserSourceReconciliationIssues(source.id))
        ])));
        const sourcesById = new Map(sources.map(source => [source.id, source]));

        container.replaceChildren();

        const visibleJobs = jobs;
        if (!sources.length && !visibleJobs.length) return;

        if (visibleJobs.length) {
            const activeJobs = visibleJobs.filter(job => !isTerminalImportJob(job));
            const terminalJobs = visibleJobs.filter(isTerminalImportJob);
            const attentionJobs = terminalJobs.filter(job =>
                job.status === "failed"
                || (sourceIssues.get(job.currentUserSourceId)?.length ?? 0) > 0);
            const finishedJobs = terminalJobs.filter(job => !attentionJobs.includes(job));

            const activity = element("div", {
                className: "border rounded p-2",
                attributes: { "aria-live": "polite" }
            });
            const headerActions = element("div", {
                className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-1"
            },
            element("div", {},
                element("div", { className: "small fw-semibold", text: "Import activity" }),
                element("div", {
                    className: "small text-body-secondary",
                    text: importActivitySummary(activeJobs.length, attentionJobs.length, finishedJobs.length)
                })),
            terminalJobs.length > 1
                ? buildDismissFinishedButton(terminalJobs, app, container, result)
                : null);
            activity.append(
                headerActions,
                element("div", {
                    className: "small text-body-secondary mb-1",
                    text: "Dismiss hides finished notifications only. It does not cancel imports, remove sources, or erase import history."
                }));

            for (const job of visibleJobs) {
                const source = sourcesById.get(job.currentUserSourceId) ?? null;
                const reconciliationIssues = sourceIssues.get(job.currentUserSourceId) ?? [];
                activity.append(buildImportJobView(
                    job,
                    source,
                    reconciliationIssues,
                    app,
                    container,
                    result));
            }
            container.append(activity);
        }

        if (sources.length) {
            const details = element("details", { className: "mt-2" });
            details.append(element("summary", {
                className: "fw-semibold",
                text: `Your added sources (${sources.length})`
            }));
            const list = element("div", { className: "list-group list-group-flush mt-2" });
            for (const source of sources) {
                const reconciliationIssues = sourceIssues.get(source.id) ?? [];
                const actions = element("div", { className: "d-flex gap-2 align-items-start" });
                if (source.kind === "web") {
                    const refresh = element("button", {
                        type: "button",
                        className: "btn btn-sm btn-outline-primary",
                        text: "Refresh"
                    });
                    refresh.addEventListener("click", async () => {
                        result.replaceChildren();
                        setButtonBusy(refresh, true, "Queueing…");
                        try {
                            const job = await app.api.refreshCurrentUserSource(source.id);
                            result.replaceChildren(dismissibleAlertNode(
                                "info",
                                `${job.displayName} refresh was queued. Import activity above will show when it starts, what stage it is in, and when it finishes.`));
                            await renderExistingSources(app, container, result);
                        } catch (error) {
                            result.replaceChildren(dismissibleAlertNode("danger", describeError(error)));
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
                        element("div", { className: "flex-grow-1" },
                            element("div", { className: "fw-semibold", text: source.displayName }),
                            element("div", { className: "small text-body-secondary", text: metadata }),
                            source.url
                                ? element("div", { className: "small text-break text-body-secondary", text: source.url })
                                : null,
                            reconciliationIssues.length
                                ? buildReconciliationIssueView(
                                    reconciliationIssues,
                                    "The imported source remains available. These conflicts affect canonical recognition only and do not discard the source representation.")
                                : null),
                        actions)));
            }
            details.append(list);
            container.append(details);
        }

        if (jobs.some(job => job.status === "queued" || job.status === "running")) {
            scheduleImportPoll(app, container, result);
        }
    } catch (error) {
        container.replaceChildren(alertNode("warning", `Added sources could not be loaded: ${describeError(error)}`));
    }
}

function buildImportJobView(job, source, reconciliationIssues, app, container, result) {
    const running = job.status === "running";
    const queued = job.status === "queued";
    const failed = job.status === "failed";
    const completed = job.status === "completed";
    const needsReview = completed && reconciliationIssues.length > 0;
    const label = failed
        ? "Import failed"
        : needsReview
            ? "Needs reconciliation"
            : completed
                ? "Import complete"
                : running
                    ? "Importing"
                    : "Queued";
    const badgeClass = failed
        ? "badge text-bg-danger"
        : needsReview
            ? "badge text-bg-warning"
            : completed
                ? "badge text-bg-success"
                : running
                    ? "badge text-bg-primary"
                    : "badge text-bg-secondary";
    const metadata = job.operation === "refresh" ? "Web source refresh" : "New Web source";
    const statusActions = element("div", {
        className: "d-flex flex-wrap gap-2 align-items-center justify-content-end"
    },
    element("span", { className: badgeClass, text: label }));

    if (isTerminalImportJob(job)) {
        const dismiss = element("button", {
            type: "button",
            className: "btn btn-sm btn-outline-secondary",
            text: "Dismiss",
            title: "Hide this finished import notification"
        });
        dismiss.addEventListener("click", () =>
            dismissImportJobs([job.id], dismiss, app, container, result));
        statusActions.append(dismiss);
    }

    return element("div", {
        className: "d-flex flex-wrap justify-content-between align-items-start gap-2 py-2 border-top"
    },
    element("div", { className: "flex-grow-1" },
        element("div", { className: "fw-semibold", text: job.displayName }),
        element("div", {
            className: "small text-body-secondary text-break",
            text: `${metadata}${job.url ? ` · ${job.url}` : ""}`
        }),
        element("div", {
            className: "small mt-1",
            text: importStateExplanation(job, source, reconciliationIssues)
        }),
        element("div", {
            className: "small text-body-secondary mt-1",
            text: importTimingText(job)
        }),
        running || queued ? buildProgressView(job) : null,
        failed && job.error
            ? element("div", {
                className: "alert alert-danger py-2 px-3 mt-2 mb-0",
                attributes: { role: "alert" }
            },
            element("div", { className: "small fw-semibold", text: "Import error" }),
            element("div", { className: "small text-break", text: job.error }))
            : null,
        needsReview
            ? buildReconciliationIssueView(
                reconciliationIssues,
                "Import completed and the source material is available. Rules Core could not safely reconcile some canonical identities automatically.")
            : null),
    statusActions);
}

function importStateExplanation(job, source, reconciliationIssues) {
    if (job.status === "queued") {
        return "Waiting to start. The background importer has not begun this job yet. You can leave this page without losing the queued import.";
    }
    if (job.status === "running") {
        return "Rules Core is processing this source in the background. You can leave this page without stopping the import.";
    }
    if (job.status === "failed") {
        return job.operation === "refresh"
            ? "The refresh stopped before completion. The previously imported source remains available; review the error below and retry when ready."
            : "The source add stopped before completion. Review the error below and retry when ready.";
    }
    if (job.status === "completed" && reconciliationIssues.length > 0) {
        return `Import finished and the source is available. ${reconciliationIssues.length} canonical reconciliation issue${reconciliationIssues.length === 1 ? "" : "s"} still need review.`;
    }
    if (job.status === "completed") {
        return source
            ? `Import finished. ${source.sourceCodeCount} publication(s) and ${source.entityCount} source record(s) are available.`
            : "Import finished successfully and the imported source is available.";
    }
    return "Rules Core has recorded this import job, but its state is not recognized by this version of the interface.";
}

function importTimingText(job) {
    const parts = [`Queued ${formatDate(job.createdAt)}`];
    if (job.startedAt) {
        parts.push(`Started ${formatDate(job.startedAt)}`);
    }
    if (job.status === "running" && job.progressUpdatedAt) {
        parts.push(`Last update ${formatDate(job.progressUpdatedAt)}`);
    }
    if (job.completedAt) {
        parts.push(`${job.status === "failed" ? "Stopped" : "Finished"} ${formatDate(job.completedAt)}`);
    }
    return parts.join(" · ");
}

function importActivitySummary(activeCount, attentionCount, finishedCount) {
    const parts = [];
    if (activeCount) parts.push(`${activeCount} active`);
    if (attentionCount) parts.push(`${attentionCount} need${attentionCount === 1 ? "s" : ""} attention`);
    if (finishedCount) parts.push(`${finishedCount} finished`);
    return parts.length ? parts.join(" · ") : "No visible import notifications";
}

function buildDismissFinishedButton(terminalJobs, app, container, result) {
    const dismiss = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary",
        text: "Dismiss finished",
        title: "Hide all finished import notifications"
    });
    dismiss.addEventListener("click", () =>
        dismissImportJobs(
            terminalJobs.map(job => job.id),
            dismiss,
            app,
            container,
            result));
    return dismiss;
}

function isTerminalImportJob(job) {
    return job.status === "completed" || job.status === "failed";
}

async function dismissImportJobs(jobIds, button, app, container, result) {
    setButtonBusy(button, true, "Dismissing…");
    try {
        await app.api.dismissCurrentUserSourceImportJobs(jobIds);
        await renderExistingSources(app, container, result);
    } catch (error) {
        result.replaceChildren(dismissibleAlertNode("danger", describeError(error)));
    } finally {
        setButtonBusy(button, false);
    }
}

async function loadReconciliationIssues(load) {
    try {
        const issues = await load();
        return Array.isArray(issues) ? issues : [];
    } catch (error) {
        if (error?.status === 404) return [];
        throw error;
    }
}

function buildReconciliationIssueView(issues, explanation) {
    const wrapper = element("div", {
        className: "alert alert-warning py-2 px-3 mt-2 mb-0",
        attributes: { role: "status" }
    },
    element("div", {
        className: "small fw-semibold",
        text: `${issues.length} canonical reconciliation issue${issues.length === 1 ? "" : "s"}`
    }),
    element("div", { className: "small", text: explanation }));

    const details = element("details", { className: "small mt-1" },
        element("summary", { text: "Show reconciliation details" }));
    const list = element("ul", { className: "mb-0 mt-1 ps-3" });
    for (const issue of issues) {
        const publication = issue.publicationDisplayName
            || issue.publicationLocalKey
            || "Publication";
        list.append(element("li", {
            className: "mb-1",
            text: `${publication}: ${issue.message || "Canonical identity could not be reconciled automatically."}`
        }));
    }
    details.append(list);
    wrapper.append(details);
    return wrapper;
}

function buildProgressView(job) {
    const structured = normalizedImportProgress(job);
    const stageKey = structured?.stage || job.progressStage;
    const stage = progressStageLabel(stageKey, job.status);
    const current = Number.isInteger(structured?.current)
        ? structured.current
        : Number.isInteger(job.progressCurrent) ? job.progressCurrent : null;
    const total = Number.isInteger(structured?.total) && structured.total > 0
        ? structured.total
        : Number.isInteger(job.progressTotal) && job.progressTotal > 0
            ? job.progressTotal
            : null;
    const hasMeasuredProgress = current !== null && total !== null;
    const unit = progressStageUnit(stageKey);
    const summary = hasMeasuredProgress
        ? `${stage} · ${current} of ${total} ${unit}`
        : stage;
    const wrapper = element("div", { className: "mt-2" },
        element("div", { className: "small fw-semibold", text: summary }));

    const progress = element("div", { className: "progress mt-1", role: "progressbar" });
    const bar = element("div", {
        className: hasMeasuredProgress
            ? "progress-bar"
            : "progress-bar progress-bar-striped progress-bar-animated"
    });
    if (hasMeasuredProgress) {
        const percent = Math.max(0, Math.min(100, Math.round((current / total) * 100)));
        bar.style.width = `${percent}%`;
        progress.setAttribute("aria-valuenow", String(percent));
        progress.setAttribute("aria-valuemin", "0");
        progress.setAttribute("aria-valuemax", "100");
    } else {
        bar.style.width = "100%";
    }
    progress.append(bar);
    wrapper.append(progress);

    const detail = structured?.detail || progressDetailText(job.progressDetail);
    const currentItem = structured?.currentItem;
    const currentItemType = structured?.currentItemType;
    const secondary = detail || (currentItem
        ? `Current: ${currentItem}${currentItemType ? ` · ${currentItemType}` : ""}`
        : null);
    if (secondary) {
        wrapper.append(element("div", {
            className: "small text-body-secondary text-break mt-1",
            text: secondary
        }));
    }
    return wrapper;
}

function normalizedImportProgress(job) {
    if (job?.progress && typeof job.progress === "object") return job.progress;
    if (typeof job?.progressDetail !== "string") return null;
    const raw = job.progressDetail.trim();
    if (!raw.startsWith("{")) return null;
    try {
        const parsed = JSON.parse(raw);
        return parsed && typeof parsed === "object" ? parsed : null;
    } catch {
        return null;
    }
}

function progressDetailText(value) {
    if (typeof value !== "string") return null;
    const trimmed = value.trim();
    if (!trimmed || trimmed.startsWith("{")) return null;
    return trimmed;
}

function progressStageUnit(stage) {
    switch (stage) {
        case "discovering":
        case "downloading":
            return "files";
        case "reconciling":
            return "publications";
        case "translating":
        case "persisting":
        case "importing":
        case "finalizing":
            return "records";
        default:
            return "items";
    }
}

function progressStageLabel(stage, status) {
    switch (stage) {
        case "queued": return "Waiting for background importer";
        case "starting": return "Starting import";
        case "discovering": return "Discovering source files";
        case "downloading": return "Downloading source files";
        case "translating": return "Translating source records";
        case "persisting": return "Persisting source records";
        case "reconciling": return "Reconciling publication identities";
        case "importing": return "Importing source data";
        case "checking": return "Checking upstream source";
        case "finalizing": return "Finalizing import";
        case "completed": return "Import complete";
        case "failed": return "Import stopped";
        default: return status === "queued" ? "Waiting for background importer" : "Processing source";
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
