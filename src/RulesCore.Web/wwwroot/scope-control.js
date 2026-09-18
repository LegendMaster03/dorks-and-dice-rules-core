import { element } from "./ui.js";

const DORKS_MODE = "dorks-and-dice";
const CAMPAIGN_DM_ROLE = "DM";

export function installAdjudicationScopeControl(app) {
    app.dmCampaigns = app.campaigns.filter(campaign => campaign.role === CAMPAIGN_DM_ROLE);
    app.canEditCampaign = app.hostContext.siteMode === DORKS_MODE && app.dmCampaigns.length > 0;

    if (app.activeView === "campaign" && !app.canEditCampaign) {
        app.activeView = app.canEditGlobal ? "global" : "library";
        app.activeCampaignId = null;
    } else if (!app.activeCampaignId && app.canEditCampaign) {
        app.activeCampaignId = app.dmCampaigns[0].id;
    }

    const renderHeader = app.renderHeader.bind(app);
    app.renderHeader = () => {
        const header = renderHeader();
        const options = [];
        if (app.canEditGlobal) {
            options.push({ value: "global", label: "Dorks & Dice" });
        }
        for (const campaign of app.dmCampaigns) {
            options.push({ value: `campaign:${campaign.id}`, label: `Campaign: ${campaign.name}` });
        }

        if (!options.length) return header;

        const select = element("select", {
            className: "form-select form-select-sm",
            ariaLabel: "Adjudication scope",
            onChange: async event => {
                const value = event.currentTarget.value;
                if (value === "global") {
                    app.activeView = "global";
                } else {
                    app.activeView = "campaign";
                    app.activeCampaignId = value.slice("campaign:".length);
                }
                await app.render();
            }
        });
        for (const option of options) {
            const node = element("option", { value: option.value, text: option.label });
            const selectedValue = app.activeView === "campaign" && app.activeCampaignId
                ? `campaign:${app.activeCampaignId}`
                : "global";
            node.selected = option.value === selectedValue;
            select.append(node);
        }

        header.append(element("div", { className: "border-top mt-3 pt-3" },
            element("div", { className: "row g-2 align-items-center" },
                element("div", { className: "col-auto" },
                    element("label", { className: "form-label fw-semibold mb-0", text: "Adjudication scope" })),
                element("div", { className: "col-sm-6 col-lg-4" }, select),
                element("div", {
                    className: "col small text-body-secondary",
                    text: "Every write is authorized again by the server for the selected scope."
                }))));
        return header;
    };
}
