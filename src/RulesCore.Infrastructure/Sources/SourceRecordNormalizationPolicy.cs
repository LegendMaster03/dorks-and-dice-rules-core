using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Applies source-format interpretation and normalization before canonical alias evidence is
/// attached. Keeping this sequence outside trusted-lineage policy prevents importer concerns
/// from accumulating inside canonical identity code and provides one extension point for future
/// source-family normalization passes.
/// </summary>
internal static class SourceRecordNormalizationPolicy
{
    public static NormalizedSourceRecord Apply(
        NormalizedSourceRepresentation representation,
        NormalizedSourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentNullException.ThrowIfNull(record);

        record = ThreeXSourceNormalizationPolicy.Apply(representation, record);
        record = ThreeXBulkTranslationPolicy.Apply(representation, record);
        record = ThreeXLegacySourceCompatibilityPolicy.Apply(representation, record);
        record = ThreeXPcGenSupplementPolicy.Apply(representation, record);
        record = ThreeXPcGenNormalizationCleanupPolicy.Apply(representation, record);
        return ExactCompetencyTranslationPolicy.Apply(representation, record);
    }
}
