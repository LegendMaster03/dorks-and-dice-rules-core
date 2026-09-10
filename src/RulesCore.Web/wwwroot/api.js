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

    searchSourceEntities({ entityType = null, query = null, limit = 100 } = {}) {
        const parameters = new URLSearchParams();
        if (entityType) {
            parameters.set("entityType", entityType);
        }
        if (query) {
            parameters.set("q", query);
        }
        parameters.set("limit", String(limit));
        return this.backend(`/api/sources/entities?${parameters.toString()}`);
    }

    importSourceDocument(payload) {
        return this.backend("/api/source-admin/import", {
            method: "POST",
            body: payload
        });
    }

    getSourceAdministrationPackages() {
        return this.backend("/api/source-admin/packages");
    }

    grantCurrentUserSourcePackage(sourcePackageId) {
        return this.backend(
            `/api/source-admin/packages/${encodeURIComponent(sourcePackageId)}/current-user-grant`,
            { method: "POST" });
    }

    revokeCurrentUserSourcePackage(sourcePackageId) {
        return this.backend(
            `/api/source-admin/packages/${encodeURIComponent(sourcePackageId)}/current-user-grant`,
            { method: "DELETE" });
    }

    getCurrentUserSourceAcquisitions() {
        return this.backend("/api/source-admin/acquisitions");
    }

    recordCurrentUserSourceAcquisition(sourcePackageId, payload) {
        return this.backend(
            `/api/source-admin/packages/${encodeURIComponent(sourcePackageId)}/current-user-acquisitions`,
            { method: "POST", body: payload });
    }

    voidCurrentUserSourceAcquisition(sourceAcquisitionId, payload = {}) {
        return this.backend(
            `/api/source-admin/acquisitions/${encodeURIComponent(sourceAcquisitionId)}/void`,
            { method: "POST", body: payload });
    }

    getSourceNormalizationCandidates({ entityType = null, query = null, limit = 100 } = {}) {
        const parameters = new URLSearchParams();
        if (entityType) {
            parameters.set("entityType", entityType);
        }
        if (query) {
            parameters.set("q", query);
        }
        parameters.set("limit", String(limit));
        return this.backend(`/api/global/rules/normalization/candidates?${parameters.toString()}`);
    }

    acceptSourceNormalization(sourceEntityId) {
        return this.backend(
            `/api/global/rules/normalization/entities/${encodeURIComponent(sourceEntityId)}/accept`,
            { method: "POST" });
    }

    getSourceRevisionUpdates() {
        return this.backend("/api/global/rules/source-updates");
    }

    previewSourceRevisionUpdate(conceptId) {
        return this.backend(
            `/api/global/rules/source-updates/${encodeURIComponent(conceptId)}/preview`);
    }

    adoptLatestSourceRevision(conceptId, payload) {
        return this.backend(
            `/api/global/rules/source-updates/${encodeURIComponent(conceptId)}/adopt`,
            { method: "POST", body: payload });
    }

    createGlobalConcept(payload) {
        return this.backend("/api/global/rules/concepts", {
            method: "POST",
            body: payload
        });
    }

    bindGlobalConceptSource(conceptId, sourceEntityId) {
        return this.backend(
            `/api/global/rules/concepts/${encodeURIComponent(conceptId)}/bindings`,
            {
                method: "POST",
                body: { sourceEntityId }
            });
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

    async saveGlobalDecision(conceptId, payload) {
        const result = await this.backend(
            `/api/global/rules/concepts/${encodeURIComponent(conceptId)}/decision`,
            { method: "PUT", body: payload });
        return {
            ...result.value,
            created: result.created
        };
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

    getCampaignBaselineCandidates(campaignId) {
        return this.backend(
            `/api/campaigns/${encodeURIComponent(campaignId)}/rules/baselines`);
    }

    previewCampaignBaseline(campaignId, rulesetRevisionId) {
        return this.backend(
            `/api/campaigns/${encodeURIComponent(campaignId)}/rules/baselines/${encodeURIComponent(rulesetRevisionId)}/preview`);
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

    getGlobalRulesCatalog({ entityType = null, query = null, limit = 200 } = {}) {
        const parameters = catalogParameters(entityType, query, limit);
        return this.backend(`/api/rules?${parameters.toString()}`);
    }

    getCampaignRulesCatalog(campaignId, { entityType = null, query = null, limit = 200 } = {}) {
        const parameters = catalogParameters(entityType, query, limit);
        return this.backend(
            `/api/campaigns/${encodeURIComponent(campaignId)}/rules?${parameters.toString()}`);
    }

    getGlobalResolvedRule(conceptKey) {
        return this.backend(`/api/rules/${encodeURIComponent(conceptKey)}`);
    }

    getCampaignResolvedRule(campaignId, conceptKey) {
        return this.backend(
            `/api/campaigns/${encodeURIComponent(campaignId)}/rules/${encodeURIComponent(conceptKey)}`);
    }

    backend(path, options = {}) {
        if (!path.startsWith("/")) {
            throw new Error("Backend paths must start with '/'.");
        }
        return requestJson(`${this.backendBaseUrl}${path}`, options);
    }
}

function catalogParameters(entityType, query, limit) {
    const parameters = new URLSearchParams();
    if (entityType) {
        parameters.set("entityType", entityType);
    }
    if (query) {
        parameters.set("q", query);
    }
    parameters.set("limit", String(limit));
    return parameters;
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
