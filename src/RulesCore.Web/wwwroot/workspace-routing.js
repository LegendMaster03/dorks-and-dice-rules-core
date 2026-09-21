import { alertNode, clear, element } from "./ui.js";

export const RULES_LAWYER_TOOL_ROUTE = "/adjudication/rules-lawyer";
const ACCESS_DENIED_VIEW = "rules-lawyer-access-denied";

export function installWorkspaceRouting(app) {
    app.viewNavigation ??= {};

    const initialRoute = parseWorkspaceRoute(app.hostContext.toolRoute);
    if (initialRoute === "global") {
        applyRulesLawyerRoute(app);
    }

    app.viewNavigation.global = async () => {
        applyRulesLawyerRoute(app);
        pushWorkspaceRoute(app, RULES_LAWYER_TOOL_ROUTE);
        await app.render();
    };

    const renderActiveView = app.renderActiveView.bind(app);
    app.renderActiveView = async container => {
        if (app.activeView === ACCESS_DENIED_VIEW) {
            renderRulesLawyerAccessDenied(container);
            return;
        }
        await renderActiveView(container);
    };

    window.addEventListener("popstate", async event => {
        const view = parseWorkspaceRoute(currentToolRoute(app));
        if (view !== "global") return;

        event.stopImmediatePropagation();
        applyRulesLawyerRoute(app);
        await app.render();
    }, { capture: true });
}

function applyRulesLawyerRoute(app) {
    app.libraryDeepLink = null;
    app.libraryRouteActive = false;
    app.browserDeepLink = null;
    app.browserSelectedConceptKey = null;
    app.activeView = app.canEditGlobal ? "global" : ACCESS_DENIED_VIEW;
}

function renderRulesLawyerAccessDenied(container) {
    clear(container);
    container.append(element("section", { className: "card card-body" },
        element("h2", { className: "h5", text: "Rules Lawyer" }),
        alertNode(
            "warning",
            "Rules Lawyer authority is required to open this workspace. Source access and campaign roles do not grant global Rules Lawyer authority.")));
}

function parseWorkspaceRoute(toolRoute) {
    if (toolRoute === null || toolRoute === undefined) return null;
    const path = String(toolRoute).split(/[?#]/, 1)[0];
    const normalized = "/" + path.replace(/^\/+|\/+$/g, "");
    return normalized === RULES_LAWYER_TOOL_ROUTE ? "global" : null;
}

function currentToolRoute(app) {
    const base = (app.hostContext.toolBasePath ?? "/tools/rules-core").replace(/\/$/, "");
    const path = window.location.pathname;
    return path.startsWith(base) ? path.slice(base.length) || "/" : "/";
}

function workspaceHref(app, toolRelativePath) {
    const base = (app.hostContext.toolBasePath ?? "/tools/rules-core").replace(/\/$/, "");
    return `${base}${toolRelativePath}`;
}

function pushWorkspaceRoute(app, toolRelativePath) {
    const href = workspaceHref(app, toolRelativePath);
    const current = `${window.location.pathname}${window.location.search}`;
    if (current !== href) window.history.pushState({}, "", href);
}
