export class RulesCoreHttpError extends Error {
    constructor(message, status, details = null) {
        super(message);
        this.name = "RulesCoreHttpError";
        this.status = status;
        this.details = details;
    }
}

export async function loadToolHostContext(root) {
    const contextUrl = root.dataset.toolContextUrl;
    if (!contextUrl) {
        throw new Error("The Tool Host did not provide a context URL.");
    }

    return requestJson(contextUrl, { method: "GET" });
}

export class RulesCoreApi {
    constructor(hostContext) {
        if (!hostContext?.apiBaseUrl) {
            throw new Error("The Tool Host context did not include an API base URL.");
        }

        this.hostContext = hostContext;
        this.hostApiBaseUrl = trimTrailingSlash(hostContext.apiBaseUrl);
        this.backendBaseUrl = `${this.hostApiBaseUrl}/upstream`;
    }

    getSession() {
        return requestJson(`${this.hostApiBaseUrl}/session`, { method: "GET" });
    }

    getCampaigns() {
        return requestJson(`${this.hostApiBaseUrl}/campaigns`, { method: "GET" });
    }

    getGlobalAuthoringOverview() {
        return this.backend("/api/global/rules/authoring");
    }

    getGlobalAuthoringConcept(conceptId) {
        return this.backend(`/api/global/rules/authoring/concepts/${encodeURIComponent(conceptId)}`);
    }

    previewGlobalDecision(conceptId, payload) {
        return this.backend(
            `/api/global/rules/concepts/${encodeURIComponent(conceptId)}/preview`,
            { method: "POST", body: payload });
    }

    saveGlobalDecision(conceptId, payload) {
        return this.backend(
            `/api/global/rules/concepts/${encodeURIComponent(conceptId)}/decision`,
            { method: "PUT", body: payload });
    }

    publishGlobalRules() {
        return this.backend("/api/global/rules/publish", { method: "POST" });
    }

    getCampaignAuthoringOverview(campaignId) {
        return this.backend(`/api/campaigns/${encodeURIComponent(campaignId)}/rules/authoring`);
    }

    getCampaignAuthoringConcept(campaignId, conceptId) {
        return this.backend(
            `/api/campaigns/${encodeURIComponent(campaignId)}/rules/authoring/concepts/${encodeURIComponent(conceptId)}`);
    }

    previewCampaignDecision(campaignId, conceptId, payload) {
        return this.backend(
            `/api/campaigns/${encodeURIComponent(campaignId)}/rules/concepts/${encodeURIComponent(conceptId)}/preview`,
            { method: "POST", body: payload });
    }

    saveCampaignDecision(campaignId, conceptId, payload) {
        return this.backend(
            `/api/campaigns/${encodeURIComponent(campaignId)}/rules/concepts/${encodeURIComponent(conceptId)}/decision`,
            { method: "PUT", body: payload });
    }

    publishCampaignRules(campaignId) {
        return this.backend(
            `/api/campaigns/${encodeURIComponent(campaignId)}/rules/publish`,
            { method: "POST" });
    }

    selectCampaignBaseline(campaignId, rulesetRevisionId) {
        return this.backend(
            `/api/campaigns/${encodeURIComponent(campaignId)}/rules/baseline`,
            {
                method: "PUT",
                body: { rulesetRevisionId }
            });
    }

    backend(path, options = {}) {
        if (!path.startsWith("/")) {
            throw new Error("Backend paths must start with '/'.");
        }
        return requestJson(`${this.backendBaseUrl}${path}`, options);
    }
}

async function requestJson(url, options = {}) {
    const headers = new Headers(options.headers ?? {});
    headers.set("Accept", "application/json");

    const requestOptions = {
        method: options.method ?? "GET",
        credentials: "same-origin",
        cache: "no-store",
        headers
    };

    if (Object.prototype.hasOwnProperty.call(options, "body")) {
        headers.set("Content-Type", "application/json");
        requestOptions.body = JSON.stringify(options.body);
    }

    const response = await fetch(url, requestOptions);
    const contentType = response.headers.get("Content-Type") ?? "";
    let payload = null;

    if (response.status !== 204) {
        if (contentType.includes("application/json") || contentType.includes("application/problem+json")) {
            payload = await response.json();
        } else {
            const text = await response.text();
            payload = text ? { detail: text } : null;
        }
    }

    if (!response.ok) {
        const message = payload?.detail
            ?? payload?.title
            ?? `Request failed with HTTP ${response.status}.`;
        throw new RulesCoreHttpError(message, response.status, payload);
    }

    return payload;
}

function trimTrailingSlash(value) {
    return value.endsWith("/") ? value.slice(0, -1) : value;
}
