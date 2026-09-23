import {
    renderCompactStatistic,
    renderEntityHeader,
    renderGeneric,
    renderNamedRuleEntry
} from "./rule-renderer-support.js";
import {
    renderClass,
    renderCondition,
    renderFeat,
    renderGeneralRule,
    renderItem,
    renderMonster,
    renderPrestigeClass,
    renderSkill,
    renderSpecies,
    renderSpell,
    renderSubclass
} from "./rule-renderers-specialized.js";

const renderers = new Map([
    ["monster", renderMonster],
    ["spell", renderSpell],
    ["class", renderClass],
    ["subclass", renderSubclass],
    ["prestigeclass", renderPrestigeClass],
    ["race", renderSpecies],
    ["species", renderSpecies],
    ["feat", renderFeat],
    ["item", renderItem],
    ["condition", renderCondition],
    ["skill", renderSkill],
    ["houserule", renderGeneralRule],
    ["rule", renderGeneralRule]
]);

export function renderResolvedRule(entityType, document, options = {}) {
    const renderer = renderers.get(String(entityType ?? "").toLowerCase())
        ?? renderGeneric;
    return renderer(document, options);
}

export {
    renderCompactStatistic,
    renderEntityHeader,
    renderNamedRuleEntry
};
