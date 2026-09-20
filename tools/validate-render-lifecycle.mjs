class FakeNode {}

class FakeText extends FakeNode {
    constructor(text) {
        super();
        this.textContent = String(text);
        this.parentElement = null;
    }
}

class FakeClassList {
    constructor() {
        this.values = new Set();
    }

    add(...values) {
        for (const value of values) this.values.add(value);
    }

    remove(...values) {
        for (const value of values) this.values.delete(value);
    }

    contains(value) {
        return this.values.has(value);
    }

    reset(value) {
        this.values = new Set(String(value ?? "").split(/\s+/).filter(Boolean));
    }

    toString() {
        return [...this.values].join(" ");
    }
}

class FakeElement extends FakeNode {
    constructor(tagName) {
        super();
        this.tagName = String(tagName).toUpperCase();
        this.classList = new FakeClassList();
        this.dataset = {};
        this.attributes = new Map();
        this._children = [];
        this._listeners = new Map();
        this.parentElement = null;
        this.textContent = "";
        this.value = "";
        this.disabled = false;
    }

    get children() {
        return this._children.filter(child => child instanceof FakeElement);
    }

    get childNodes() {
        return this._children;
    }

    get parentNode() {
        return this.parentElement;
    }

    get className() {
        return this.classList.toString();
    }

    set className(value) {
        this.classList.reset(value);
    }

    append(...nodes) {
        for (const node of nodes) this.#attach(node, false);
    }

    prepend(...nodes) {
        for (const node of [...nodes].reverse()) this.#attach(node, true);
    }

    insertBefore(node, reference) {
        const child = this.#coerce(node);
        this.#detach(child);
        child.parentElement = this;
        const index = reference ? this._children.indexOf(reference) : -1;
        if (index < 0) this._children.push(child);
        else this._children.splice(index, 0, child);
        return child;
    }

    replaceChildren(...nodes) {
        for (const child of this._children) child.parentElement = null;
        this._children = [];
        this.append(...nodes);
    }

    setAttribute(name, value) {
        this.attributes.set(name, String(value));
    }

    addEventListener(type, listener) {
        const listeners = this._listeners.get(type) ?? [];
        listeners.push(listener);
        this._listeners.set(type, listeners);
    }

    listenerCount(type) {
        return (this._listeners.get(type) ?? []).length;
    }

    querySelector(selector) {
        return this.querySelectorAll(selector)[0] ?? null;
    }

    querySelectorAll(selector) {
        if (selector.startsWith(":scope > ")) {
            const childSelector = selector.slice(9).trim();
            return this.children.filter(child => matches(child, childSelector));
        }

        const matchesFound = [];
        const visit = node => {
            for (const child of node.children) {
                if (matches(child, selector)) matchesFound.push(child);
                visit(child);
            }
        };
        visit(this);
        return matchesFound;
    }

    #attach(value, atStart) {
        const child = this.#coerce(value);
        this.#detach(child);
        child.parentElement = this;
        if (atStart) this._children.unshift(child);
        else this._children.push(child);
    }

    #coerce(value) {
        return value instanceof FakeNode ? value : new FakeText(value);
    }

    #detach(child) {
        if (!child.parentElement) return;
        const siblings = child.parentElement._children;
        const index = siblings.indexOf(child);
        if (index >= 0) siblings.splice(index, 1);
    }
}

function matches(node, selector) {
    return selector.split(",").some(part => matchesSingle(node, part.trim()));
}

function matchesSingle(node, selector) {
    if (!selector) return false;
    if (selector.startsWith(".")) {
        return selector.slice(1).split(".").every(name => node.classList.contains(name));
    }
    return node.tagName.toLowerCase() === selector.toLowerCase();
}

function assert(condition, message) {
    if (!condition) throw new Error(message);
}

globalThis.Node = FakeNode;
globalThis.document = {
    createElement: tagName => new FakeElement(tagName),
    createTextNode: text => new FakeText(text)
};

const ui = await import("../src/RulesCore.Web/wwwroot/ui.js");
const ux = await import("../src/RulesCore.Web/wwwroot/ux-shell.js");

const fragment = ui.element("div", {},
    ui.element("div", { className: "table-responsive" }, ui.element("table")),
    ui.element("form"),
    ui.element("div", { className: "list-group" }),
    ui.alertNode("info", "Partial update"));

assert(fragment.querySelector(".rules-core-table-wrap"), "table-responsive fragments must be enhanced immediately");
assert(fragment.querySelector(".rules-core-table"), "table fragments must be enhanced immediately");
assert(fragment.querySelector(".rules-core-form"), "form fragments must be enhanced immediately");
assert(fragment.querySelector(".rules-core-list"), "list fragments must be enhanced immediately");
assert(fragment.querySelector(".rules-core-alert"), "alert fragments must be enhanced immediately");

const fragmentSnapshot = fragment.querySelector("table").className;
ui.enhanceRenderedFragment(fragment);
ui.enhanceRenderedFragment(fragment);
assert(fragment.querySelector("table").className === fragmentSnapshot, "repeated fragment enhancement must settle");

const app = {
    activeView: "global",
    activeCampaignId: "11111111-1111-1111-1111-111111111111",
    hostContext: { siteMode: "dorks-and-dice" }
};
const container = ui.element("div", {},
    ui.element("div", { className: "rules-core-workflow" },
        ui.element("div", { className: "text-body-secondary small", text: "Old workflow copy" })),
    ui.element("div", { className: "card mb-3" },
        ui.element("h3", { text: "Normalize imported sources" })),
    ui.element("div", { className: "card mb-3" },
        ui.element("h3", { text: "Create rule concept" })));

ux.enhanceRenderedView(app, container);
const firstChildCount = container.children.length;
const firstLeadCount = container.querySelectorAll(":scope > .rules-core-generated-page-lead").length;
const firstDisclosureCount = container.querySelectorAll(":scope > .rules-core-tool-disclosure").length;
const firstToggleListeners = container.querySelectorAll(":scope > .rules-core-tool-disclosure")
    .reduce((sum, disclosure) => sum + disclosure.listenerCount("toggle"), 0);

ux.enhanceRenderedView(app, container);
assert(container.children.length === firstChildCount, "repeated view enhancement must not add top-level nodes");
assert(container.querySelectorAll(":scope > .rules-core-generated-page-lead").length === firstLeadCount,
    "repeated view enhancement must not duplicate generated page leads");
assert(container.querySelectorAll(":scope > .rules-core-tool-disclosure").length === firstDisclosureCount,
    "repeated view enhancement must not duplicate generated disclosures");
assert(container.querySelectorAll(":scope > .rules-core-tool-disclosure")
    .reduce((sum, disclosure) => sum + disclosure.listenerCount("toggle"), 0) === firstToggleListeners,
    "repeated view enhancement must not multiply event handlers");
assert(container.querySelector(".rules-core-workflow").querySelector(".text-body-secondary.small").textContent
    .includes("publication remains explicit"), "Rules Lawyer presentation copy must still be applied");

container.replaceChildren(ui.element("div", { className: "card" }, ui.element("h3", { text: "Campaign" })));
app.activeView = "campaign";
ux.enhanceRenderedView(app, container);
ux.enhanceRenderedView(app, container);
assert(container.querySelectorAll(":scope > .rules-core-generated-page-lead").length === 1,
    "switching views must apply one idempotent generated page lead");
const campaignSiteLink = container.querySelector(".rules-core-campaign-site-link");
assert(campaignSiteLink, "campaign view must link back to the native Dorks & Dice campaign UI");
assert(campaignSiteLink.attributes.get("href") === `/campaigns/${app.activeCampaignId}`,
    "campaign link must target the active campaign details route");
assert(campaignSiteLink.attributes.get("target") === "_top",
    "campaign link must leave the embedded tool surface when necessary");

const lifecycleApp = {
    hostContext: { siteMode: "dorks-and-dice" },
    session: { user: null },
    activeView: "sources",
    canBrowseSourceLibrary: true,
    canBrowseRules: true,
    canEditGlobal: false,
    canReviewVersions: false,
    canEditCampaign: false,
    canManageHostedSources: false,
    canAdministerSources: false,
    api: {
        getGlobalAuthoringOverview: async () => ({ concepts: [] }),
        getCampaignAuthoringOverview: async () => ({ concepts: [] })
    },
    renderActiveView: async target => {
        target.replaceChildren(ui.element("div", { className: "card" }, ui.element("h3", { text: "Sources" })));
    },
    renderGlobalOverview: async target => {
        target.replaceChildren(ui.element("div", { className: "card" }, ui.element("h3", { text: "Global" })));
    },
    renderGlobalConcept: async target => {
        target.replaceChildren(ui.element("div", { className: "card" }, ui.element("h3", { text: "Global concept" })));
    },
    renderCampaignOverview: async target => {
        target.replaceChildren(ui.element("div", { className: "card" }, ui.element("h3", { text: "Campaign" })));
    },
    renderCampaignConcept: async target => {
        target.replaceChildren(ui.element("div", { className: "card" }, ui.element("h3", { text: "Campaign concept" })));
    }
};

ux.installRulesCoreUx(lifecycleApp);

const hostedHeader = lifecycleApp.renderHeader();
assert(hostedHeader.classList.contains("rules-core-topbar-hosted"),
    "Dorks & Dice hosted mode must not render a second visible Rules Core brand header");
assert(hostedHeader.attributes.has("hidden"),
    "Dorks & Dice Tool Host must own the visible Rules Core title chrome");

const groupedNavigation = lifecycleApp.renderNavigation();
const navigationLabels = groupedNavigation.querySelectorAll("summary")
    .map(summary => summary.textContent);
assert(navigationLabels.includes("Players"), "top navigation must expose the Players group");
assert(navigationLabels.includes("Rules"), "top navigation must expose the Rules group");
assert(navigationLabels.includes("Dungeon Masters"), "top navigation must expose the Dungeon Masters group");
assert(navigationLabels.includes("Sources"), "top navigation must expose the Sources group");
assert(!navigationLabels.includes("Adjudication"),
    "Adjudication navigation must remain hidden without Rules Lawyer or version-review authority");
assert(groupedNavigation.querySelectorAll(".rules-core-nav-menu").length === 4,
    "hosted read-only navigation must render only authorized top-level groups");

const lifecycleContainer = ui.element("div");
await lifecycleApp.renderActiveView(lifecycleContainer);
assert(lifecycleContainer.classList.contains("rules-core-main"), "full active-view renders must install the view shell");
assert(lifecycleContainer.querySelector(":scope > .card").classList.contains("rules-core-page-lead"),
    "full active-view renders must run presentation enhancement explicitly");

lifecycleApp.activeView = "global";
await lifecycleApp.renderGlobalOverview(lifecycleContainer);
assert(lifecycleContainer.querySelectorAll(":scope > .rules-core-generated-page-lead").length === 1,
    "direct in-place authoring renders must run presentation enhancement explicitly");
await lifecycleApp.renderGlobalOverview(lifecycleContainer);
assert(lifecycleContainer.querySelectorAll(":scope > .rules-core-generated-page-lead").length === 1,
    "repeated direct in-place renders must settle without duplicate presentation nodes");

console.log("Rules Core explicit render lifecycle validation passed.");
