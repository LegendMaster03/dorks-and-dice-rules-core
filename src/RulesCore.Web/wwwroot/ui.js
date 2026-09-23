export function element(tagName, options = {}, ...children) {
    const node = document.createElement(tagName);

    if (options.className) {
        node.className = options.className;
    }
    if (options.text !== undefined) {
        node.textContent = options.text;
    }
    if (options.html !== undefined) {
        node.innerHTML = options.html;
    }
    if (options.type) {
        node.type = options.type;
    }
    if (options.value !== undefined) {
        node.value = options.value;
    }
    if (options.placeholder !== undefined) {
        node.placeholder = options.placeholder;
    }
    if (options.disabled !== undefined) {
        node.disabled = options.disabled;
    }
    if (options.rows !== undefined) {
        node.rows = options.rows;
    }
    if (options.id) {
        node.id = options.id;
    }
    if (options.name) {
        node.name = options.name;
    }
    if (options.title) {
        node.title = options.title;
    }
    if (options.ariaLabel) {
        node.setAttribute("aria-label", options.ariaLabel);
    }
    if (options.dataset) {
        for (const [key, value] of Object.entries(options.dataset)) {
            node.dataset[key] = value;
        }
    }
    if (options.attributes) {
        for (const [name, value] of Object.entries(options.attributes)) {
            if (value !== null && value !== undefined) {
                node.setAttribute(name, value);
            }
        }
    }
    if (options.onClick) {
        node.addEventListener("click", options.onClick);
    }
    if (options.onChange) {
        node.addEventListener("change", options.onChange);
    }
    if (options.onInput) {
        node.addEventListener("input", options.onInput);
    }

    appendChildren(node, children);
    enhanceRenderedFragment(node);
    return node;
}

export function enhanceRenderedFragment(root) {
    if (!root) {
        return root;
    }

    applyPresentationClasses(root);
    for (const child of root.children ?? []) {
        enhanceRenderedFragment(child);
    }
    return root;
}

export function clear(node) {
    node.replaceChildren();
}

export function alertNode(kind, message) {
    return element("div", {
        className: `alert alert-${kind} mb-3`,
        attributes: { role: "alert" }
    }, message);
}

export function badge(text, kind = "secondary") {
    return element("span", { className: `badge text-bg-${kind}`, text });
}

export function pageLead({
    eyebrow = null,
    title,
    description = null,
    actions = [],
    className = ""
} = {}) {
    const copy = element("div", { className: "rules-core-page-lead-copy" });
    if (eyebrow) {
        copy.append(element("div", { className: "rules-core-eyebrow", text: eyebrow }));
    }
    if (title) {
        copy.append(element("h2", { className: "rules-core-page-title", text: title }));
    }
    if (description) {
        copy.append(element("p", { className: "rules-core-page-description", text: description }));
    }

    const lead = element("section", {
        className: `rules-core-page-lead ${className}`.trim()
    }, copy);

    const actionNodes = (Array.isArray(actions) ? actions : [actions]).filter(Boolean);
    if (actionNodes.length) {
        lead.append(element("div", { className: "rules-core-page-actions" }, actionNodes));
    }
    return lead;
}

export function panel({ className = "", tagName = "section", ariaLabelledBy = null } = {}, ...children) {
    const attributes = ariaLabelledBy ? { "aria-labelledby": ariaLabelledBy } : undefined;
    return element(tagName, {
        className: `card rules-core-panel ${className}`.trim(),
        attributes
    }, element("div", { className: "card-body" }, children));
}

export function toolbar(...children) {
    return element("div", { className: "rules-core-toolbar" }, children);
}

export function sectionHeading({
    title,
    description = null,
    actions = [],
    id = null,
    level = 3,
    className = ""
} = {}) {
    const headingLevel = Math.min(6, Math.max(2, Number(level) || 3));
    const copy = element("div", { className: "rules-core-section-heading-copy" },
        element(`h${headingLevel}`, {
            id,
            className: "rules-core-section-title",
            text: title
        }),
        description
            ? element("p", {
                className: "rules-core-section-description",
                text: description
            })
            : null);
    const heading = element("div", {
        className: `rules-core-section-heading ${className}`.trim()
    }, copy);
    const actionNodes = (Array.isArray(actions) ? actions : [actions]).filter(Boolean);
    if (actionNodes.length) {
        heading.append(element("div", {
            className: "rules-core-section-actions"
        }, actionNodes));
    }
    return heading;
}

export function filterBar(...children) {
    return element("div", { className: "rules-core-filter-bar" }, children);
}

export function actionBar(...children) {
    return element("div", { className: "rules-core-action-bar" }, children);
}

export function listDetailWorkspace(list, detail, { className = "" } = {}) {
    return element("div", {
        className: `rules-core-list-detail-workspace ${className}`.trim()
    },
        element("section", { className: "rules-core-list-detail-list" }, list),
        element("section", { className: "rules-core-list-detail-detail" }, detail));
}

export function field(labelText, control, { className = "", helpText = null } = {}) {
    return element("label", {
        className: `rules-core-field ${className}`.trim()
    },
        element("span", { className: "rules-core-field-label", text: labelText }),
        control,
        helpText ? element("span", { className: "rules-core-field-help", text: helpText }) : null);
}

export function formatDate(value) {
    if (!value) {
        return "—";
    }

    const date = new Date(value);
    return Number.isNaN(date.valueOf()) ? String(value) : date.toLocaleString();
}

export function formatJson(value) {
    return JSON.stringify(value, null, 2);
}

export function codeBlock(value, className = "") {
    return element(
        "pre",
        { className: `rules-core-code border rounded p-2 bg-body-tertiary mb-0 ${className}`.trim() },
        element("code", { text: typeof value === "string" ? value : formatJson(value) }));
}

export function definitionList(items) {
    const list = element("dl", { className: "row mb-0" });
    for (const [term, value] of items) {
        list.append(
            element("dt", { className: "col-sm-4", text: term }),
            element("dd", { className: "col-sm-8", text: value ?? "—" }));
    }
    return list;
}

export function setButtonBusy(button, busy, busyText = "Working…") {
    if (!button.dataset.idleText) {
        button.dataset.idleText = button.textContent ?? "";
    }
    button.disabled = busy;
    button.textContent = busy ? busyText : button.dataset.idleText;
}

export const DEFAULT_PAGE_SIZE = 100;

export function paginationControls({
    page = 0,
    pageSize = DEFAULT_PAGE_SIZE,
    itemCount = 0,
    hasNext = false,
    onPage
}) {
    const currentPage = Math.max(0, page);
    const first = itemCount > 0 ? (currentPage * pageSize) + 1 : 0;
    const last = (currentPage * pageSize) + itemCount;
    const previous = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary",
        text: "← Previous",
        disabled: currentPage === 0,
        onClick: async () => onPage?.(Math.max(0, currentPage - 1))
    });
    const next = element("button", {
        type: "button",
        className: "btn btn-sm btn-outline-secondary",
        text: "Next →",
        disabled: !hasNext,
        onClick: async () => onPage?.(currentPage + 1)
    });
    const range = itemCount > 0
        ? `Showing ${first}–${last}${hasNext ? "+" : ""} · Page ${currentPage + 1}`
        : `Page ${currentPage + 1}`;
    return element("div", {
        className: "d-flex flex-wrap justify-content-between align-items-center gap-2 mt-3"
    },
        element("div", { className: "small text-body-secondary", text: range }),
        element("div", { className: "d-flex gap-2" }, previous, next));
}

export function describeError(error) {
    if (!error) {
        return "An unknown error occurred.";
    }
    if (error.status) {
        return `${error.message} (HTTP ${error.status})`;
    }
    return error.message ?? String(error);
}

function applyPresentationClasses(node) {
    if (!node.classList) {
        return;
    }

    const tagName = String(node.tagName ?? "").toLowerCase();
    if (tagName === "table") {
        node.classList.add("rules-core-table");
    }
    if (tagName === "form") {
        node.classList.add("rules-core-form");
    }
    if (node.classList.contains("table-responsive")) {
        node.classList.add("rules-core-table-wrap");
    }
    if (node.classList.contains("list-group")) {
        node.classList.add("rules-core-list");
    }
    if (node.classList.contains("alert")) {
        node.classList.add("rules-core-alert");
    }
}

function appendChildren(parent, children) {
    for (const child of children.flat(Infinity)) {
        if (child === null || child === undefined || child === false) {
            continue;
        }
        parent.append(child instanceof Node ? child : document.createTextNode(String(child)));
    }
}