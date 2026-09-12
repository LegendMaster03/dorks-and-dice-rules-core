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
    return node;
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

function appendChildren(parent, children) {
    for (const child of children.flat(Infinity)) {
        if (child === null || child === undefined || child === false) {
            continue;
        }
        parent.append(child instanceof Node ? child : document.createTextNode(String(child)));
    }
}
