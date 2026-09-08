const root = document.getElementById("tool-root");

if (!root) {
    throw new Error("Rules Core could not find the Dorks & Dice tool root.");
}

root.replaceChildren();

const card = document.createElement("div");
card.className = "card card-body";

const heading = document.createElement("h2");
heading.className = "h5";
heading.textContent = "Rules Core";

const description = document.createElement("p");
description.className = "mb-2";
description.textContent = "Rules Core is connected through the Dorks & Dice Tool Host.";

const status = document.createElement("p");
status.className = "mb-0 text-body-secondary";
status.textContent = "Checking host context…";

card.append(heading, description, status);
root.append(card);

const contextUrl = root.dataset.toolContextUrl;
if (!contextUrl) {
    status.textContent = "Host context endpoint was not supplied.";
} else {
    try {
        const response = await fetch(contextUrl, {
            credentials: "same-origin",
            headers: { Accept: "application/json" }
        });

        status.textContent = response.ok
            ? "Host context is available."
            : `Host context returned HTTP ${response.status}.`;
    } catch (error) {
        console.error("Rules Core host-context check failed.", error);
        status.textContent = "Host context could not be reached.";
    }
}
