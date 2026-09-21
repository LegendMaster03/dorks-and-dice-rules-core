import { installWorkspaceRouting, RULES_LAWYER_TOOL_ROUTE } from "../src/RulesCore.Web/wwwroot/workspace-routing.js";

function assert(condition, message) {
    if (!condition) throw new Error(message);
}

function createWindow(pathname) {
    const listeners = [];
    return {
        location: { pathname, search: "" },
        history: {
            pushState(_state, _title, href) {
                const parsed = new URL(href, "https://rules.test");
                globalThis.window.location.pathname = parsed.pathname;
                globalThis.window.location.search = parsed.search;
            }
        },
        addEventListener(type, listener, options) {
            if (type === "popstate") listeners.push({ listener, options });
        },
        async dispatchPopState() {
            let stopped = false;
            const event = { stopImmediatePropagation() { stopped = true; } };
            for (const entry of listeners) {
                await entry.listener(event);
                if (stopped) break;
            }
            return stopped;
        }
    };
}

function createApp({ route, canEditGlobal }) {
    let renders = 0;
    return {
        hostContext: {
            toolBasePath: "/tools/rules-core",
            toolRoute: route
        },
        canEditGlobal,
        activeView: "library",
        libraryDeepLink: null,
        libraryRouteActive: false,
        browserDeepLink: null,
        browserSelectedConceptKey: null,
        viewNavigation: {},
        renderActiveView: async () => {},
        render: async () => { renders += 1; },
        get renderCount() { return renders; }
    };
}

globalThis.window = createWindow("/tools/rules-core/adjudication/rules-lawyer");
let app = createApp({ route: RULES_LAWYER_TOOL_ROUTE, canEditGlobal: true });
installWorkspaceRouting(app);
assert(app.activeView === "global", "direct Rules Lawyer route must select the global workspace");

window.location.pathname = "/tools/rules-core/monsters";
await app.viewNavigation.global();
assert(window.location.pathname === "/tools/rules-core/adjudication/rules-lawyer",
    "menu navigation must push the addressable Rules Lawyer route");
assert(app.activeView === "global", "menu navigation must render Rules Lawyer");
assert(app.renderCount === 1, "menu navigation must render exactly once");

window.location.pathname = "/tools/rules-core/monsters";
let stopped = await window.dispatchPopState();
assert(!stopped, "non-workspace popstate must remain available to catalog routing");

window.location.pathname = "/tools/rules-core/adjudication/rules-lawyer";
stopped = await window.dispatchPopState();
assert(stopped, "Rules Lawyer popstate must claim its route before catalog routing");
assert(app.activeView === "global", "back/forward navigation must restore Rules Lawyer");
assert(app.renderCount === 2, "Rules Lawyer popstate must rerender the workspace");

globalThis.window = createWindow("/tools/rules-core/adjudication/rules-lawyer");
app = createApp({ route: RULES_LAWYER_TOOL_ROUTE, canEditGlobal: false });
installWorkspaceRouting(app);
assert(app.activeView === "rules-lawyer-access-denied",
    "direct routing must not bypass Rules Lawyer authority");
