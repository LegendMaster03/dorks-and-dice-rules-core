export function installToolNavigation(app) {
    app.routeHandlers = [];

    app.registerToolRouteHandler = handler => {
        if (typeof handler === "function") app.routeHandlers.push(handler);
    };

    app.toolHref = route => {
        const base = String(app.hostContext.toolBasePath ?? "/tools/rules-core").replace(/\/$/, "");
        const normalized = normalizeRoute(route);
        return `${base}${normalized}`;
    };

    app.currentToolRoute = () => {
        const base = String(app.hostContext.toolBasePath ?? "/tools/rules-core").replace(/\/$/, "");
        const pathname = window.location.pathname;
        const routePath = pathname === base
            ? "/"
            : pathname.startsWith(`${base}/`)
                ? pathname.slice(base.length)
                : normalizeRoute(app.hostContext.toolRoute ?? "/").split("?", 1)[0];
        return `${routePath || "/"}${window.location.search || ""}`;
    };

    app.applyToolRoute = async (route, { render = true } = {}) => {
        const normalized = normalizeRoute(route);
        let handled = false;
        for (const handler of app.routeHandlers) {
            if (await handler(normalized)) {
                handled = true;
                break;
            }
        }
        if (!handled) {
            for (const handler of app.routeHandlers) {
                if (await handler("/")) {
                    handled = true;
                    break;
                }
            }
        }
        if (render) await app.render();
        return handled;
    };

    app.navigateToolRoute = async (route, { replace = false } = {}) => {
        const normalized = normalizeRoute(route);
        const href = app.toolHref(normalized);
        const current = `${window.location.pathname}${window.location.search}`;
        if (current !== href) {
            window.history[replace ? "replaceState" : "pushState"]({}, "", href);
        }
        await app.applyToolRoute(normalized);
    };

    window.addEventListener("popstate", async () => {
        await app.applyToolRoute(app.currentToolRoute());
    });
}

export function routeParts(route) {
    const normalized = normalizeRoute(route);
    const [path, query = ""] = normalized.split("?", 2);
    return {
        path,
        segments: path.replace(/^\/+|\/+$/g, "").split("/").filter(Boolean).map(decodeURIComponent),
        query: new URLSearchParams(query)
    };
}

export function withQuery(path, values = {}) {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(values)) {
        if (value === null || value === undefined || value === "" || value === false) continue;
        query.set(key, String(value));
    }
    const serialized = query.toString();
    return serialized ? `${path}?${serialized}` : path;
}

function normalizeRoute(route) {
    const value = String(route ?? "/").trim() || "/";
    const hashIndex = value.indexOf("#");
    const withoutHash = hashIndex >= 0 ? value.slice(0, hashIndex) : value;
    if (withoutHash.startsWith("/")) return withoutHash;
    return `/${withoutHash}`;
}
