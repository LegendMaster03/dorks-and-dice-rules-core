using Microsoft.EntityFrameworkCore;
using RulesCore.Application.Rules;
using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Bootstrap;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Rules;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

public static class RulesCoreServiceCollectionExtensions
{
    public static IServiceCollection AddRulesCorePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<RulesCoreDbContext>(
            options => options.UseNpgsql(connectionString));
        services.AddScoped<IRulesCoreSchemaInitializer, RulesCoreSchemaInitializer>();
        return services;
    }

    public static IServiceCollection AddRulesCoreSources(this IServiceCollection services)
    {
        services.AddScoped<ISourceImportService, SourceImportService>();
        services.AddScoped<INormalizedSourceImportService, NormalizedSourceImportService>();
        services.AddScoped<ISourceNormalizationMaintenanceService, SourceNormalizationMaintenanceService>();
        services.AddSingleton<ISourceFormatAdapter, FiveEToolsSourceFormatAdapter>();
        services.AddSingleton<ISourceFormatAdapter, PcGenSourceFormatAdapter>();
        services.AddSingleton<ISourceFormatAdapter, PdfSourceFormatAdapter>();
        services.AddSingleton<ISourceFormatAdapterRegistry, SourceFormatAdapterRegistry>();
        services.AddScoped<ISourceCatalogService, SourceCatalogService>();
        services.AddScoped<ISourceEntitySearchService, SourceEntitySearchService>();
        services.AddScoped<ISourceGrantService, SourceGrantService>();
        services.AddHostedService<CurrentUserSourceRefreshBackground>();
        return services;
    }

    public static IServiceCollection AddRulesCoreRules(this IServiceCollection services)
    {
        services.AddScoped<IGlobalRulesService, GlobalRulesService>();
        services.AddScoped<ICampaignRulesService, CampaignRulesService>();
        services.AddScoped<IHarvestingRulesService, HarvestingRulesService>();
        services.AddScoped<IRulePatchPreviewService, RulePatchPreviewService>();
        services.AddScoped<IGlobalRulesAuthoringService, GlobalRulesAuthoringService>();
        services.AddScoped<ICampaignRulesAuthoringService, CampaignRulesAuthoringService>();
        return services;
    }

    public static IServiceCollection AddRulesCoreCharacter(this IServiceCollection services)
    {
        services.AddScoped<ICharacterMechanicsConsumerService, CharacterMechanicsConsumerService>();
        services.AddScoped<ICharacterRulesProjectionService, CharacterRulesProjectionService>();
        return services;
    }

    public static IServiceCollection AddRulesCoreBootstrap(this IServiceCollection services)
    {
        services.AddScoped<IRulesCoreBaselineBootstrapper, RulesCoreBaselineBootstrapper>();
        return services;
    }
}
