using System.Text.Json;
using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules.CharacterProjection;

internal sealed record CharacterProjectionRule(
    ResolvedRuleCatalogItemView Catalog,
    JsonElement Document,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterWeaponAttackProfile(
    string ConceptKey,
    string DisplayName,
    string ItemType,
    string? WeaponCategory,
    bool Finesse,
    int AttackBonus,
    int DamageBonus,
    string DamageExpression,
    string? DamageType,
    string? Range,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterWeaponCatalogEntry(
    string ConceptKey,
    string DisplayName,
    string ItemType,
    string? WeaponCategory,
    IReadOnlySet<string> Properties,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterSpellSlotProgression(
    string ConceptKey,
    string DisplayName,
    int ClassLevel,
    string? CasterProgression,
    IReadOnlyList<int> SlotsBySpellLevel,
    IReadOnlyList<IReadOnlyList<int>> SlotsByClassLevel,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterPactMagicProgression(
    string ConceptKey,
    string DisplayName,
    int ClassLevel,
    int SlotCount,
    int SlotLevel,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterHitDieProfile(
    string ConceptKey,
    string DisplayName,
    int ClassLevel,
    int? Faces,
    CharacterMechanicProvenanceView Provenance);

internal sealed record CharacterFeatureCatalogEntry(
    string ConceptKey,
    string EntityType,
    string DisplayName,
    string ClassName,
    string? ClassSource,
    string? SubclassName,
    string? SubclassSource,
    int AcquisitionLevel,
    string? FeatureSource,
    IReadOnlyList<CharacterRuleEffectView> Effects,
    CharacterMechanicProvenanceView Provenance);

internal interface ICharacterRuleProjectionModule
{
    bool Handles(CharacterProjectionRule rule, CharacterProjectionContext context);
    void Project(CharacterProjectionRule rule, CharacterProjectionContext context);
}

