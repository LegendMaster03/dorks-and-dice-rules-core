import {
    alertNode,
    badge,
    describeError,
    element,
    formatDate,
    setButtonBusy
} from "./ui.js";

const ACQUISITION_KINDS = [
    ["physical-copy", "Physical copy"],
    ["digital-copy", "Digital copy"],
    ["subscription", "Subscription"],
    ["licensed-access", "Licensed access"],
    ["other", "Other"]
];

export function installSourceAcquisitionAdministration(app) {
    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        await renderActiveView(container);
        if (app.activeView !== "source-admin" || !app.canAdministerSources) {
            return;
        }

        try {
            container.append(await renderAcquisitionCard(app, container));
        } catch (error) {
            container.append(alertNode(
                "danger",
                `Source acquisition history could not load: ${describeError(error)}`));
        }
    };
}

async function renderAcquisitionCard(app, container) {
    const [packages, acquisitions] = await Promise.all([
        app.api.getSourceAdministrationPackages(),
        app.api.getCurrentUserSourceAcquisitions()
    ]);

    const card = element("div", { className: "card card-body mt-3" });
    card.append(
        element("h3", { className: "h5 mb-1", text: "Current account acquisition history" }),
        element("p", {
            className: "text-body-secondary",
            text: "Record how this account obtained a source package. Acquisition history is provenance only: it does not grant or revoke source-content access."
        }),
        alertNode(
            "info",
            "Use the separate Current account source access controls to change authorization. Do not enter license keys, passwords, payment data, or other secrets in the reference field."));

    if (packages.length) {
        card.append(renderRecordForm(app, container, packages));
    } else {
        card.append(alertNode("secondary", "Import a source package before recording an acquisition."));
    }

    card.append(renderHistory(app, container, acquisitions));
    return card;
}

function renderRecordForm(app, container, packages) {
    const form = element("form", { className: "row g-3 align-items-end mb-4" });

    const packageSelect = element("select", { className: "form-select" });
    for (const sourcePackage of packages) {
        const option = element("option", {
            value: sourcePackage.id,
            text: `${sourcePackage.displayName} (${sourcePackage.key})`
        });
        packageSelect.append(option);
    }

    const kindSelect = element("select", { className: "form-select" });
    for (const [value, label] of ACQUISITION_KINDS) {
        kindSelect.append(element("option", { value, text: label }));
    }

    const reference = element("input", {
        type: "text",
        className: "form-control",
        placeholder: "Optional provider/order/library reference"
    });
    reference.maxLength = 500;

    const acquiredAt = element("input", {
        type: "datetime-local",
        className: "form-control"
    });

    const submit = element("button", {
        type: "submit",
        className: "btn btn-outline-primary w-100",
        text: "Record acquisition"
    });

    form.append(
        field("Package", packageSelect, "col-lg-4"),
        field("Acquisition type", kindSelect, "col-lg-2"),
        field("Reference", reference, "col-lg-3"),
        field("Acquired at", acquiredAt, "col-lg-2"),
        element("div", { className: "col-lg-1" }, submit));

    form.addEventListener("submit", async event => {
        event.preventDefault();
        setButtonBusy(submit, true, "Saving…");
        try {
            const acquiredValue = acquiredAt.value
                ? new Date(acquiredAt.value).toISOString()
                : null;
            await app.api.recordCurrentUserSourceAcquisition(packageSelect.value, {
                acquisitionKind: kindSelect.value,
                reference: reference.value.trim() || null,
                acquiredAt: acquiredValue
            });
            await app.renderActiveView(container);
        } catch (error) {
            window.alert(describeError(error));
            setButtonBusy(submit, false);
        }
    });

    return form;
}

function renderHistory(app, container, acquisitions) {
    const section = element("div");
    section.append(element("h4", { className: "h6", text: "Recorded acquisitions" }));

    if (!acquisitions.length) {
        section.append(element("div", {
            className: "text-body-secondary small",
            text: "No acquisition records have been entered for this account."
        }));
        return section;
    }

    const table = element("table", { className: "table table-hover align-middle mb-0" });
    const head = element("thead");
    const headRow = element("tr");
    for (const label of ["Package", "Type", "Reference", "Acquired", "Recorded", "Status", ""]) {
        headRow.append(element("th", { text: label }));
    }
    head.append(headRow);

    const body = element("tbody");
    for (const acquisition of acquisitions) {
        const row = element("tr");
        row.append(
            element("td", {},
                element("div", { className: "fw-semibold", text: acquisition.packageDisplayName }),
                element("div", { className: "small font-monospace text-body-secondary", text: acquisition.packageKey })),
            element("td", { text: acquisition.acquisitionKind }),
            element("td", { text: acquisition.reference ?? "—" }),
            element("td", { text: acquisition.acquiredAt ? formatDate(acquisition.acquiredAt) : "—" }),
            element("td", { text: formatDate(acquisition.recordedAt) }),
            element("td", {}, acquisition.isVoided
                ? badge("Voided", "secondary")
                : badge("Recorded", "success")));

        const action = element("td", { className: "text-end" });
        if (acquisition.isVoided) {
            const details = acquisition.voidReason
                ? `Voided: ${acquisition.voidReason}`
                : "Voided";
            action.append(element("span", { className: "small text-body-secondary", text: details }));
        } else {
            const button = element("button", {
                type: "button",
                className: "btn btn-sm btn-outline-secondary",
                text: "Void record"
            });
            button.addEventListener("click", async () => {
                const reason = window.prompt(
                    "Optional reason for voiding this acquisition record. This does not revoke source access.",
                    "");
                if (reason === null) {
                    return;
                }
                setButtonBusy(button, true, "Voiding…");
                try {
                    await app.api.voidCurrentUserSourceAcquisition(acquisition.id, {
                        reason: reason.trim() || null
                    });
                    await app.renderActiveView(container);
                } catch (error) {
                    window.alert(describeError(error));
                    setButtonBusy(button, false);
                }
            });
            action.append(button);
        }
        row.append(action);
        body.append(row);
    }

    table.append(head, body);
    section.append(element("div", { className: "table-responsive" }, table));
    return section;
}

function field(label, control, columnClass) {
    const group = element("div", { className: columnClass });
    group.append(
        element("label", { className: "form-label fw-semibold", text: label }),
        control);
    return group;
}
