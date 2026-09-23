using RulesCore.Application.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Coordinates the focused Character mechanics resolver modules after rule projection
/// populates the shared projection context.
/// </summary>
internal static class CharacterCoreMechanicsResolver
{
    internal static void SeedCallerCapabilities(CharacterProjectionContext context)
    {
        foreach (var capability in context.Capabilities.ToArray())
        {
            context.CapabilityViews[capability] = new CharacterCapabilityView(
                capability,
                CharacterProjectionJson.Humanize(capability),
                [],
                CharacterProjectionContext.EmptyProvenance());
        }
    }

    internal static void Resolve(
        CharacterProjectionContext context,
        CharacterMechanicsCatalogView mechanicCatalog)
    {
        CharacterAbilityMechanicsResolver.ResolveAbilities(context);
        CharacterAbilityMechanicsResolver.ResolveProficiency(context);
        CharacterWeaponAttackResolver.Resolve(context);
        CharacterArmorClassResolver.Resolve(context);
        CharacterThreeXCombatResolver.Resolve(context);
        CharacterInitiativeSaveResolver.ResolveInitiative(context);
        CharacterInitiativeSaveResolver.ResolveAbilitySavingThrows(context);
        CharacterCompetencyResolver.Resolve(context, mechanicCatalog);
    }
}