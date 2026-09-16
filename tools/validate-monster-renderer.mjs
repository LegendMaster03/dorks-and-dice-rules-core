class FakeNode {
    constructor(tagName = "", text = "") {
        this.tagName = String(tagName).toUpperCase();
        this.children = [];
        this.attributes = new Map();
        this.dataset = {};
        this.className = "";
        this._text = String(text ?? "");
        this.classList = {
            contains: value => this.className.split(/\s+/).filter(Boolean).includes(value),
            add: value => {
                const values = new Set(this.className.split(/\s+/).filter(Boolean));
                values.add(value);
                this.className = [...values].join(" ");
            }
        };
    }

    append(...nodes) {
        for (const node of nodes) {
            if (node === null || node === undefined) continue;
            this.children.push(node);
        }
    }

    replaceChildren(...nodes) {
        this.children = [];
        this._text = "";
        this.append(...nodes);
    }

    setAttribute(name, value) {
        this.attributes.set(name, String(value));
    }

    addEventListener() {}

    querySelector(selector) {
        if (!selector.startsWith(".")) return null;
        const className = selector.slice(1);
        for (const child of this.children) {
            if (!(child instanceof FakeNode)) continue;
            if (child.classList.contains(className)) return child;
            const nested = child.querySelector(selector);
            if (nested) return nested;
        }
        return null;
    }

    set textContent(value) {
        this._text = String(value ?? "");
        this.children = [];
    }

    get textContent() {
        return this._text + this.children.map(child => child?.textContent ?? String(child ?? "")).join("");
    }
}

globalThis.Node = FakeNode;
globalThis.document = {
    createElement: tagName => new FakeNode(tagName),
    createTextNode: text => new FakeNode("#text", text)
};

const { renderResolvedRule } = await import("../src/RulesCore.Web/wwwroot/rule-renderers.js");

const monster = {
    name: "Acceptance Drake",
    source: "SRD52",
    size: ["M"],
    type: { type: "dragon", tags: ["shapechanger"] },
    alignment: ["N"],
    ac: [15, { ac: 18, from: ["natural armor"] }],
    hp: { average: 45, formula: "6d10 + 12" },
    speed: { walk: 30, fly: { number: 60, condition: "while airborne" } },
    str: 14,
    dex: 16,
    con: 15,
    int: 10,
    wis: 12,
    cha: 11,
    save: { dex: "+5", con: "+4" },
    skill: { perception: "+4", stealth: "+5" },
    senses: ["darkvision 60 ft."],
    languages: ["Draconic"],
    cr: "3",
    trait: [
        { name: "Keen Senses", entries: ["The drake has advantage on Wisdom (Perception) checks."] }
    ],
    action: [
        { name: "Bite", entries: ["{@atk mw} {@hit 5} to hit, reach 5 ft., one target."] }
    ]
};

const rendered = renderResolvedRule("monster", monster, { showDocument: false });
const text = rendered.textContent;
for (const required of [
    "Acceptance Drake",
    "Armor Class",
    "Hit Points",
    "Speed",
    "Ability Scores",
    "Challenge Rating",
    "Keen Senses",
    "Bite",
    "Draconic",
    "darkvision 60 ft."
]) {
    if (!text.includes(required)) {
        throw new Error(`Native 5e.tools monster renderer omitted '${required}'. Rendered text: ${text}`);
    }
}
if (text.includes("[object Object]")) {
    throw new Error(`Native 5e.tools structured monster data leaked as [object Object]. Rendered text: ${text}`);
}
if (!rendered.classList.contains("rules-core-monster-stat-block")) {
    throw new Error("Monster renderer did not produce the dedicated stat-block presentation.");
}

console.log("Native 5e.tools monster renderer validation passed.");
