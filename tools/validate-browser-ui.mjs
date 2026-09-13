import fs from "node:fs";

class FakeNode {}

class FakeText extends FakeNode {
    constructor(text) {
        super();
        this.textContent = String(text);
        this.parentElement = null;
    }
}

class FakeClassList {
    constructor() { this.values = new Set(); }
    add(...values) { values.forEach(value => this.values.add(value)); }
    remove(...values) { values.forEach(value => this.values.delete(value)); }
    contains(value) { return this.values.has(value); }
    toggle(value, force) {
        const next = force === undefined ? !this.values.has(value) : Boolean(force);
        if (next) this.values.add(value); else this.values.delete(value);
        return next;
    }
    reset(value) { this.values = new Set(String(value ?? "").split(/\s+/).filter(Boolean)); }
    toString() { return [...this.values].join(" "); }
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
        this.selected = false;
        this.required = false;
        this.id = "";
    }
    get children() { return this._children.filter(child => child instanceof FakeElement); }
    get childNodes() { return this._children; }
    get parentNode() { return this.parentElement; }
    get className() { return this.classList.toString(); }
    set className(value) { this.classList.reset(value); }
    set innerHTML(value) { this.textContent = String(value).replace(/<[^>]+>/g, " "); }
    get innerHTML() { return this.textContent; }
    append(...nodes) { nodes.forEach(node => this.#attach(node, false)); }
    prepend(...nodes) { [...nodes].reverse().forEach(node => this.#attach(node, true)); }
    after(node) {
        if (!this.parentElement) return;
        const siblings = this.parentElement._children;
        const index = siblings.indexOf(this);
        const child = this.#coerce(node);
        this.#detach(child);
        child.parentElement = this.parentElement;
        siblings.splice(index + 1, 0, child);
    }
    insertBefore(node, reference) {
        const child = this.#coerce(node);
        this.#detach(child);
        child.parentElement = this;
        const index = reference ? this._children.indexOf(reference) : -1;
        if (index < 0) this._children.push(child); else this._children.splice(index, 0, child);
        return child;
    }
    replaceChildren(...nodes) {
        this._children.forEach(child => { child.parentElement = null; });
        this._children = [];
        this.append(...nodes);
    }
    remove() {
        if (!this.parentElement) return;
        const siblings = this.parentElement._children;
        const index = siblings.indexOf(this);
        if (index >= 0) siblings.splice(index, 1);
        this.parentElement = null;
    }
    setAttribute(name, value) { this.attributes.set(name, String(value)); }
    getAttribute(name) { return this.attributes.get(name) ?? null; }
    addEventListener(type, listener) {
        const listeners = this._listeners.get(type) ?? [];
        listeners.push(listener);
        this._listeners.set(type, listeners);
    }
    querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
    querySelectorAll(selector) {
        if (selector.startsWith(":scope > ")) {
            const childSelector = selector.slice(9).trim();
            return this.children.filter(child => matches(child, childSelector));
        }
        const found = [];
        const visit = node => {
            for (const child of node.children) {
                if (matches(child, selector)) found.push(child);
                visit(child);
            }
        };
        visit(this);
        return found;
    }
    #attach(value, atStart) {
        if (value === null || value === undefined || value === false) return;
        const child = this.#coerce(value);
        this.#detach(child);
        child.parentElement = this;
        if (atStart) this._children.unshift(child); else this._children.push(child);
    }
    #coerce(value) { return value instanceof FakeNode ? value : new FakeText(value); }
    #detach(child) {
        if (!child.parentElement) return;
        const siblings = child.parentElement._children;
        const index = siblings.indexOf(child);
        if (index >= 0) siblings.splice(index, 1);
    }
}

function matches(node, selector) {
    return selector.split(",").some(part => {
        const value = part.trim();
        if (!value) return false;
        if (value.startsWith(".")) return value.slice(1).split(".").every(name => node.classList.contains(name));
        return node.tagName.toLowerCase() === value.toLowerCase();
    });
}

function text(node) {
    if (node instanceof FakeText) return node.textContent;
    return [node.textContent, ...node.childNodes.map(text)].filter(Boolean).join(" ").replace(/\s+/g, " ").trim();
}

function assert(condition, message) {
    if (!condition) throw new Error(message);
}

const listeners = new Map();
const location = { pathname: "/tools/rules-core/library/monsters", search: "?q=construct&page=2" };
function setLocation(href) {
    const parsed = new URL(href, "https://rules.test");
    location.pathname = parsed.pathname;
    location.search = parsed.search;
}

globalThis.Node = FakeNode;
globalThis.document = {
    createElement: tagName => new FakeElement(tagName),
    createTextNode: value => new FakeText(value)
};
globalThis.window = {
    location,
    history: {
        pushState: (_state, _title, href) => setLocation(href),
        replaceState: (_state, _title, href) => setLocation(href)
    },
    addEventListener: (type, listener) => listeners.set(type, listener)
};

const navigation = await import("../src/RulesCore.Web/wwwroot/browser-navigation.js");
const libraryModule = await import("../src/RulesCore.Web/wwwroot/source-library.js");
const publishedModule = await import("../src/RulesCore.Web/wwwroot/rules-browser.js");
const renderers = await import("../src/RulesCore.Web/wwwroot/rule-renderers.js");
const ux = await import("../src/RulesCore.Web/wwwroot/ux-shell.js");

let renderCount = 0;
const app = {
    hostContext: { siteMode: "dorks-and-dice", toolBasePath: "/tools/rules-core", toolRoute: "/" },
    session: { user: null, globalRoles: [] },
    campaigns: [],
    canEditGlobal: false,
    canEditCampaign: false,
    canReviewVersions: false,
    canManageHostedSources: false,
    canAdministerSources: false,
    api: {
        getGlobalAuthoringOverview: async () => ({ concepts: [] }),
        getCampaignAuthoringOverview: async () => ({ concepts: [] })
    },
    renderActiveView: async () => {},
    render: async () => { renderCount += 1; }
};

navigation.installToolNavigation(app);
publishedModule.installResolvedRulesBrowser(app);
libraryModule.installSourceLibrary(app);

await app.applyToolRoute(app.currentToolRoute(), { render: false });
assert(app.activeView === "library", "library route must resolve to Rules Library");
assert(app.libraryRoute.kind === "global-collection", "cross-library collection route must resolve");
assert(app.libraryRoute.entityType === "monster", "library monster route must preserve entity type");
assert(app.libraryRoute.query === "construct" && app.libraryRoute.page === 1, "library query and page must come from URL state");

await app.navigateToolRoute("/published?q=fire&page=3");
assert(app.activeView === "browse", "published route must remain distinct from Source Library");
assert(app.browserRoute.kind === "catalog" && app.browserRoute.query === "fire" && app.browserRoute.page === 2, "published query state must come from URL");
assert(location.pathname === "/tools/rules-core/published" && location.search.includes("q=fire"), "navigation must update the browser URL");

location.pathname = "/tools/rules-core/library";
location.search = "?q=dragon&page=4";
await listeners.get("popstate")();
assert(app.activeView === "library" && app.libraryRoute.query === "dragon" && app.libraryRoute.page === 3, "Back/Forward route restoration must rehydrate library state");
assert(renderCount >= 2, "route changes must trigger explicit application renders");

const legacyMonster = {
    body: "SizeAndType: Large Construct\nHit Dice: 10d10+30 (85 hp)\nInitiative: +0\nSpeed: 40 ft.\nArmor Class: 25, touch 9, flat-footed 25\nBase Attack/Grapple: +7/+15\nFort +6 Ref +3 Will +4\nStr 28 Dex 11 Con 16 Int — Wis 11 Cha 10\nChallenge Rating: 8"
};
const legacyProjection = renderers.projectMonster(legacyMonster);
assert(legacyProjection.hitPoints === "85", `3.5 HP regression: expected 85, got '${legacyProjection.hitPoints}'`);
assert(legacyProjection.hitPoints !== ")", "legacy '(85 hp)' text must never be interpreted as an HP field label");

const standardMonster = renderers.renderRuleDocument("monster", {
    size: ["L"],
    type: "dragon",
    alignment: ["C", "E"],
    ac: [19],
    hp: { average: 256, formula: "19d12 + 133" },
    speed: { walk: 40, fly: 80 },
    cr: "17",
    str: 27, dex: 10, con: 25, int: 16, wis: 13, cha: 21,
    trait: [{ name: "Legendary Resistance", entries: ["The dragon can choose to succeed instead."] }],
    action: [{ name: "Multiattack", entries: ["The dragon makes three attacks."] }]
});
const standardText = text(standardMonster);
assert(standardText.includes("STR 27 (+8)"), "ability scores must use 5.5-style score plus modifier presentation");
assert(standardText.includes("Hit Points 256 (19d12 + 133)"), "standard monster HP must render from structured data");
assert(standardText.includes("Traits") && standardText.includes("Actions"), "monster information hierarchy must separate traits and actions");

const kaiju = renderers.renderRuleDocument("monster", {
    statBlockType: "kaiju",
    type: { type: "elemental", tags: ["kaiju"] },
    ac: [18],
    chaosThreshold: 200,
    str: 30, dex: 14, con: 28, int: 8, wis: 12, cha: 18,
    vulnerableAreas: [
        { name: "Skull", specialTraits: "Targetable as normal; exploitation changes behavior.", cr: 26, ac: 18, hp: 130 }
    ],
    behaviors: [
        { name: "Rampage", trigger: "Chaos Threshold is reduced to 0", effect: "A new Vulnerable Area can be targeted." }
    ],
    finishingBlow: 50
});
const kaijuText = text(kaiju);
for (const required of ["Chaos Threshold", "200", "Vulnerable Areas", "Skull", "Behaviors", "Rampage", "Finishing Blow"]) {
    assert(kaijuText.includes(required), `kaiju presentation must include '${required}'`);
}

const spellText = text(renderers.renderRuleDocument("spell", {
    level: 3, school: "evocation", time: [{ number: 1, unit: "action" }], range: "150 feet",
    components: { v: true, s: true, m: "bat guano" }, duration: "Instantaneous",
    entries: ["A bright streak flashes."], entriesHigherLevel: ["Damage increases by 1d6."]
}));
assert(spellText.includes("Spell details") && spellText.includes("At Higher Levels"), "spell renderer must expose conventional spell metadata and upcasting");

const itemText = text(renderers.renderRuleDocument("item", {
    type: "Wondrous Item", rarity: "rare", reqAttune: true, charges: 7, recharge: "1d6 + 1 daily", entries: ["This item stores magical energy."]
}));
assert(itemText.includes("Rarity") && itemText.includes("Attunement") && itemText.includes("Charges"), "item renderer must expose item mechanics");

const featText = text(renderers.renderRuleDocument("feat", {
    prerequisite: [{ ability: [{ str: 13 }] }], entries: ["Increase your Strength by 1."]
}));
assert(featText.includes("Prerequisites"), "feat renderer must expose prerequisites");

const classText = text(renderers.renderRuleDocument("class", {
    hd: { number: 1, faces: 10 },
    primaryAbility: "Strength",
    classTableGroups: [{ colLabels: ["Level", "Features"], rows: [["1", "Second Wind"], ["2", "Action Surge"]] }],
    classFeatures: [{ name: "Second Wind", entries: ["Regain hit points."] }]
}));
assert(classText.includes("Progression") && classText.includes("Action Surge") && classText.includes("Class Features"), "class renderer must provide progression and feature hierarchy");

const conditionText = text(renderers.renderRuleDocument("condition", { entries: ["A blinded creature cannot see."] }));
assert(conditionText.includes("A blinded creature"), "condition renderer must provide concise reference text");

for (const type of ["race", "species", "skill", "domain", "power", "divineAbility", "houseRule", "rule", "source-fragment", "prestigeClass", "npcClass"]) {
    const rendered = renderers.renderRuleDocument(type, { entries: [`Representative ${type} content.`] });
    assert(text(rendered).includes(`Representative ${type} content.`), `${type} renderer family must produce readable content`);
}

const raw = renderers.rawDocumentDisclosure({ name: "Immutable fixture" });
assert(raw.tagName === "DETAILS" && text(raw).includes("Raw immutable source document"), "raw immutable source must remain available behind a disclosure");

const navApp = {
    activeView: "library",
    hostContext: { siteMode: "dorks-and-dice" },
    session: { user: null },
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
    renderActiveView: async () => {},
    renderGlobalOverview: async () => {},
    renderGlobalConcept: async () => {},
    renderCampaignOverview: async () => {},
    renderCampaignConcept: async () => {},
    navigateToolRoute: async () => {},
    render: async () => {}
};
ux.installRulesCoreUx(navApp);
const navText = text(navApp.renderNavigation());
assert(navText.includes("Rules Library") && navText.includes("Published Rules"), "Explore navigation must explicitly separate Rules Library and Published Rules");
assert(!navText.includes("Rules Lawyer"), "authorization-dependent navigation must hide Rules Lawyer from unauthorized users");

const sourceLibraryCode = fs.readFileSync("./src/RulesCore.Web/wwwroot/source-library.js", "utf8");
for (const developmentLabel of ["Monster beta", "Beta integration surface", "Monster detected", "Detected monster stats", "Copy beta payload", "API discovery"]) {
    assert(!sourceLibraryCode.includes(developmentLabel), `ordinary Source Library UI must not expose '${developmentLabel}'`);
}

for (const file of [
    "./src/RulesCore.Web/wwwroot/browser-navigation.js",
    "./src/RulesCore.Web/wwwroot/rules-browser.js",
    "./src/RulesCore.Web/wwwroot/source-library.js",
    "./src/RulesCore.Web/wwwroot/rule-renderers.js",
    "./src/RulesCore.Web/wwwroot/ux-shell.js"
]) {
    assert(!fs.readFileSync(file, "utf8").includes("MutationObserver"), `${file} must preserve the explicit render lifecycle`);
}

console.log("Rules Core routed browser and entity renderer validation passed.");
