import {
    alertNode,
    badge,
    describeError,
    element,
    formatDate,
    setButtonBusy
} from "./ui.js";

export function installSourceAccessAdministration(app) {
    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        await renderActiveView(container);
        if (app.activeView !== "source-admin" || !app.canAdministerSources) {
            return;
        }

        try {
            container.append(await createSourceAccessCard(app, container));
        } catch (error) {
            container.append(alertNode(
                "danger",
                `Source access controls could not load: ${describeError(error)}`));
        }
    };
}

async function createSourceAccessCard(app, container) {
    const packages = await app.api.getSourceAdministrationPackages();
    const card = element("div", { className: "card card-body mt-3" });
    const currentUser = app.session.user?.displayName ?? "Current account";

    const heading = element("div", {
        className: "d-flex flex-wrap justify-content-between align-items-start gap-2 mb-3"
    });
    heading.append(
        element("div", {},
            element("h3", { className: "h5 mb-1", text: "Current account source access" }),
            element("p", {
                className: "text-body-secondary mb-0",
                text: "Explicitly grant or revoke this signed-in account's access to restricted source packages. Dev authority alone does not grant source-content access."
            })),
        badge(currentUser, "secondary"));
    card.append(heading);

    card.append(alertNode(
        "info",
        "These controls affect only the current authenticated account. They can not grant another user access. Public packages require no grant and are always available through normal Source Layer reads."));

    if (!packages.length) {
        card.append(alertNode(
            "secondary",
            "No source packages have been imported yet."));
        return card;
    }

    const table = element("table", { className: "table table-hover align-middle mb-0" });
    const head = element("thead");
    const headRow = element("tr");
    for (const label of ["Package", "Provider / license", "Visibility", "Current account access", "Imported", ""]) {
        headRow.append(element("th", { text: label }));
    }
    head.append(headRow);

    const body = element("tbody");
    for (const sourcePackage of packages) {
        const row = element("tr");
        const packageCell = element("td");
        packageCell.append(
            element("div", { className: "fw-semibold", text: sourcePackage.displayName }),
            element("div", {
                className: "small text-body-secondary font-monospace",
                text: sourcePackage.key
            }));
        row.append(packageCell);
        row.append(element("td", {
            text: sourcePackage.license
                ? `${sourcePackage.provider} · ${sourcePackage.license}`
                : sourcePackage.provider
        }));

        const visibility = element("td");
        visibility.append(sourcePackage.isPublic
            ? badge("Public", "success")
            : badge("Restricted", "warning"));
        row.append(visibility);

        const access = element("td");
        if (sourcePackage.isPublic) {
            access.append(badge("Available", "success"));
        } else if (sourcePackage.currentUserHasGrant) {
            access.append(badge("Granted", "success"));
        } else {
            access.append(badge("No grant", "secondary"));
        }
        row.append(access);
        row.append(element("td", { text: formatDate(sourcePackage.createdAt) }));

        const action = element("td", { className: "text-end" });
        if (sourcePackage.isPublic) {
            action.append(element("button", {
                type: "button",
                className: "btn btn-sm btn-outline-secondary",
                text: "No grant needed",
                disabled: true
            }));
        } else {
            const button = element("button", {
                type: "button",
                className: sourcePackage.currentUserHasGrant
                    ? "btn btn-sm btn-outline-danger"
                    : "btn btn-sm btn-outline-primary",
                text: sourcePackage.currentUserHasGrant
                    ? "Revoke my account"
                    : "Grant my account"
            });
            button.addEventListener("click", async () => {
                setButtonBusy(
                    button,
                    true,
                    sourcePackage.currentUserHasGrant ? "Revoking…" : "Granting…");
                try {
                    const result = sourcePackage.currentUserHasGrant
                        ? await app.api.revokeCurrentUserSourcePackage(sourcePackage.id)
                        : await app.api.grantCurrentUserSourcePackage(sourcePackage.id);
                    window.alert(result.changed
                        ? (sourcePackage.currentUserHasGrant
                            ? `Revoked ${sourcePackage.displayName} from the current account.`
                            : `Granted ${sourcePackage.displayName} to the current account.`)
                        : "The requested source-access state was already in effect.");
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
    card.append(element("div", { className: "table-responsive" }, table));
    return card;
}
