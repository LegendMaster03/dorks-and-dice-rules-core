using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

/// <summary>
/// Projects unresolved placeholders for legacy mechanics that remain applicable under the
/// effective ruleset but require state or semantics not yet resolved by specialized modules.
/// </summary>
internal static class CharacterLegacyMechanicsResolver
{
    internal static void Resolve(
        CharacterProjectionContext context,
        CharacterMechanicsCatalogView catalog)
    {
        foreach (var mechanic in catalog.Mechanics.Where(value =>
                     value.Competency is null
                     && value.IsAvailableUnderRuleset))
        {
            if (context.Mechanics.ContainsKey(mechanic.MechanicKey))
            {
                continue;
            }
    
            var requiredCapabilities = mechanic.Applicability.RequiredCapabilityKeys
                .Where(value => !context.Capabilities.Contains(value))
                .ToArray();
            if (requiredCapabilities.Length > 0)
            {
                continue;
            }
    
            if (mechanic.MechanicKey is "save.fortitude" or "save.reflex" or "save.will"
                or "combat.base-attack-bonus" or "combat.grapple"
                or "defense.ac.touch" or "defense.ac.flat-footed"
                or "defense.damage-reduction" or "defense.spell-resistance"
                or "resource.nonlethal-damage")
            {
                context.Mechanics[mechanic.MechanicKey] = CharacterProjectionResolutionHelpers.Unresolved(
                    mechanic.MechanicKey,
                    mechanic.Kind,
                    mechanic.DisplayName,
                    CharacterResolutionStates.ApplicableUnresolved,
                    provenance: mechanic.Provenance ?? CharacterProjectionResolutionHelpers.Provenance(mechanic.SourceAttributions));
            }
        }
    }
    
}
