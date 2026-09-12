import { RulesCoreApi, loadToolHostContext } from "./api.js";
import { RulesAuthoringApp } from "./authoring.js";
import { installCampaignBaselineAuthoring } from "./campaign-baseline-authoring.js";
import { installConceptSourceAuthoring } from "./concept-source-authoring.js";
import { installHostedSourceAuthoring } from "./hosted-source-authoring.js";
import { installResolvedRulesBrowser } from "./rules-browser.js";
import { installSourceAccessAdministration } from "./source-access-admin.js";
import { installSourceAcquisitionAdministration } from "./source-acquisition-admin.js";
import { installSourceAdministration } from "./source-admin.js";
import { installSourceLibrary } from "./source-library.js";
import { installSourceNormalization } from "./source-normalization.js";
import { installSourceRevisionReview } from "./source-revision-review.js";
import { installSourceVersioning } from "./source-versioning.js";
import { installRulesCoreUx } from "./ux-shell.js";
import { alertNode, clear, describeError, element } from "./ui.js";

const root = document.getElementById("tool-root");
if (!root) throw new Error("Rules Core could not find the Dorks & Dice tool root.");

installStylesheets();
clear(root);
root.append(element("div", { className: "card card-body text-body-secondary", text: "Loading Rules Core…" }));

try {
    const hostContext = await loadToolHostContext(root);
    const api = new RulesCoreApi(hostContext);
    const [session, campaigns] = await Promise.all([
        api.getOptionalSession(),
        api.getOptionalCampaigns()
    ]);
    const effectiveSession = session ?? {
        user: null,
        globalRoles: []
    };
    const app = new RulesAuthoringApp(root, api, hostContext, effectiveSession, campaigns);
    installResolvedRulesBrowser(app);
    installConceptSourceAuthoring(app);
    installSourceNormalization(app);
    installSourceRevisionReview(app);
    installSourceVersioning(app);
    installHostedSourceAuthoring(app);
    installSourceAdministration(app);
    installSourceAccessAdministration(app);
    installSourceAcquisitionAdministration(app);
    installCampaignBaselineAuthoring(app);
    if (hostContext.siteMode === "dorks-and-dice") {
        installSourceLibrary(app);
    }
    installRulesCoreUx(app);
    await app.render();
} catch (error) {
    console.error("Rules Core failed to initialize.", error);
    clear(root);
    root.append(element("div", { className: "card card-body" },
        element("h2", { className: "h5", text: "Rules Core unavailable" }),
        alertNode("danger", describeError(error))));
}

function installStylesheets() {
    for (const [id, filename] of [
        ["rules-core-module-styles", "./rules-core.css"],
        ["rules-core-detail-styles", "./rules-core-detail.css"]
    ]) {
        if (document.getElementById(id)) continue;
        const link = document.createElement("link");
        link.id = id;
        link.rel = "stylesheet";
        link.href = new URL(filename, import.meta.url).href;
        document.head.append(link);
    }
}
