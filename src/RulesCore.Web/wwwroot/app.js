import { RulesCoreApi, loadToolHostContext } from "./api.js";
import { RulesAuthoringApp } from "./authoring.js";
import { installCampaignBaselineAuthoring } from "./campaign-baseline-authoring.js";
import { installConceptSourceAuthoring } from "./concept-source-authoring.js";
import { installResolvedRulesBrowser } from "./rules-browser.js";
import { installSourceAccessAdministration } from "./source-access-admin.js";
import { installSourceAcquisitionAdministration } from "./source-acquisition-admin.js";
import { installSourceAdministration } from "./source-admin.js";
import { installSourceNormalization } from "./source-normalization.js";
import { installSourceRevisionReview } from "./source-revision-review.js";
import { installSourceVersioning } from "./source-versioning.js";
import { alertNode, clear, describeError, element } from "./ui.js";

const root = document.getElementById("tool-root");
if (!root) throw new Error("Rules Core could not find the Dorks & Dice tool root.");

installStylesheet();
clear(root);
root.append(element("div", { className: "card card-body text-body-secondary", text: "Loading Rules Core…" }));

try {
    const hostContext = await loadToolHostContext(root);
    const api = new RulesCoreApi(hostContext);
    const [session, campaigns] = await Promise.all([api.getSession(), api.getCampaigns()]);
    const app = new RulesAuthoringApp(root, api, hostContext, session, campaigns);
    installResolvedRulesBrowser(app);
    installConceptSourceAuthoring(app);
    installSourceNormalization(app);
    installSourceRevisionReview(app);
    installSourceVersioning(app);
    installSourceAdministration(app);
    installSourceAccessAdministration(app);
    installSourceAcquisitionAdministration(app);
    installCampaignBaselineAuthoring(app);
    await app.render();
} catch (error) {
    console.error("Rules Core failed to initialize.", error);
    clear(root);
    root.append(element("div", { className: "card card-body" },
        element("h2", { className: "h5", text: "Rules Core unavailable" }),
        alertNode("danger", describeError(error))));
}

function installStylesheet() {
    const id = "rules-core-module-styles";
    if (document.getElementById(id)) return;
    const link = document.createElement("link");
    link.id = id;
    link.rel = "stylesheet";
    link.href = new URL("./rules-core.css", import.meta.url).href;
    document.head.append(link);
}
