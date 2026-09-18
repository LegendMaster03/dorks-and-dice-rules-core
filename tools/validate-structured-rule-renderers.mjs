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
        return this._text
            + this.children.map(child => child?.textContent ?? String(child ?? "")).join("");
    }
}

globalThis.Node = FakeNode;
globalThis.document = {
    createElement: tagName => new FakeNode(tagName),
    createTextNode: text => new FakeNode("#text", text)
};

const { renderResolvedRule } = await import("../src/RulesCore.Web/wwwroot/rule-renderers.js");

const fixtures = [
    {
        type: "spell",
        document: {
            name: "Arc Spark",
            level: 1,
            school: "V",
            time: [{ number: 1, unit: "action" }],
            range: { type: "point", distance: { type: "feet", amount: 60 } },
            components: { v: true, s: true },
            duration: [{ type: "instant" }],
            entries: ["A spark of arcane energy strikes the target."]
        },
        required: ["1st level", "Evocation", "1 action", "60 ft.", "V, S", "Arcane energy"]
    },
    {
        type: "class",
        document: {
            name: "Example Class",
            hd: { number: 1, faces: 8 },
            proficiency: ["wis", "cha"],
            classFeatures: [
                { name: "Focused Study", entries: ["You gain a focused study feature."] }
            ]
        },
        required: ["1d8", "WIS, CHA", "Class Features", "Focused Study"]
    },
    {
        type: "species",
        document: {
            name: "Example Species",
            size: ["M"],
            speed: 30,
            ability: [{ cha: 2 }, { choose: { from: ["str", "dex"], count: 1, amount: 1 } }],
            darkvision: 60,
            entries: [
                { name: "Celestial Legacy", entries: ["You know a celestial secret."] }
            ]
        },
        required: ["Medium", "30 ft.", "CHA +2", "Darkvision", "60 ft.", "Celestial Legacy"]
    },
    {
        type: "feat",
        document: {
            name: "Example Feat",
            category: "G",
            repeatable: false,
            prerequisite: [{ level: 4 }],
            entries: ["You gain an example benefit."]
        },
        required: ["General", "Repeatable", "Prerequisite", "Example benefit"]
    },
    {
        type: "item",
        document: {
            name: "Example Blade",
            type: "M",
            rarity: "rare",
            dmg1: "1d8",
            dmgType: "S",
            weight: 3,
            value: 1500,
            entries: ["This blade carries an example enchantment."]
        },
        required: ["Melee Weapon", "Rare", "1d8 Slashing", "3 lb.", "15 gp", "Example enchantment"]
    },
    {
        type: "skill",
        document: {
            name: "Example Skill",
            ability: "int",
            entries: ["This skill measures specialized knowledge."]
        },
        required: ["INT", "Specialized knowledge"]
    }
];

for (const fixture of fixtures) {
    const rendered = renderResolvedRule(fixture.type, fixture.document);
    if (!rendered.classList.contains("rules-core-structured-rule")) {
        throw new Error(`${fixture.type} did not use the structured rule presentation.`);
    }

    const text = rendered.textContent;
    for (const required of fixture.required) {
        if (!text.toLowerCase().includes(required.toLowerCase())) {
            throw new Error(
                `${fixture.type} renderer omitted '${required}'. Rendered text: ${text}`);
        }
    }
    if (text.includes("[object Object]")) {
        throw new Error(
            `${fixture.type} renderer leaked structured data as [object Object]. Rendered text: ${text}`);
    }
}

console.log("Structured rule renderer validation passed.");
