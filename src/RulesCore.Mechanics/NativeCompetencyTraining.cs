using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// An occurrence of a source-validated Competency grant. The caller must
/// establish that the character actually earned the source and any choices;
/// a SourceId string by itself is never proof of entitlement.
/// </summary>
public sealed record NativeCompetencyTrainingGrant(
    string SourceId, string OccurrenceId, string CompetencyId,
    int Ranks = 0, bool Proficient = false);

/// <summary>Ranks and proficiency are independent parts of one Competency.</summary>
public sealed record NativeCompetencyTrainingState(int Ranks, bool Proficient);

public sealed record NativeCompetencyGrantResult(
    string Status, IReadOnlyDictionary<string, NativeCompetencyTrainingState> Training,
    IReadOnlyList<NativeIssue> Issues);

/// <summary>
/// A typed, native prerequisite. Requiring a rank never implicitly requires
/// proficiency, and requiring proficiency never implies a minimum rank.
/// </summary>
public sealed record NativeCompetencyPrerequisite(
    string CompetencyId, int MinimumRanks = 0, bool RequiresProficiency = false);

public sealed record NativeCompetencyPrerequisiteResult(
    string Status, bool? Satisfied, string? Reason = null);

/// <summary>
/// Pure aggregation and prerequisite evaluation for already-authorized grants.
/// This class does not validate which features a character owns, how a choice
/// was selected, whether a source may repeat, or equipment qualifications.
/// Those are mandatory upstream checks before applying the returned training.
/// </summary>
public static class NativeCompetencyTraining
{
    private const int TransportRankLimit = 1_000_000;

    public static NativeCompetencyGrantResult CombineValidated(
        NativeCatalogSnapshot catalog, IReadOnlyList<NativeCompetencyTrainingGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(grants);

        var training = new SortedDictionary<string, NativeCompetencyTrainingState>(
            StringComparer.Ordinal);
        var issues = new List<NativeIssue>();
        var occurrences = new HashSet<string>(StringComparer.Ordinal);

        if (grants.Count > 500)
            issues.Add(new("invalid-competency-grants", "competencies",
                "The number of training grant occurrences exceeds the transport limit."));

        foreach (var grant in grants.Take(501))
        {
            if (grant is null || string.IsNullOrWhiteSpace(grant.SourceId) ||
                string.IsNullOrWhiteSpace(grant.OccurrenceId) ||
                string.IsNullOrWhiteSpace(grant.CompetencyId) ||
                grant.SourceId.Length > 160 || grant.OccurrenceId.Length > 160 ||
                grant.CompetencyId.Length > 160)
            {
                issues.Add(new("invalid-competency-grant", "competencies",
                    "Every grant needs bounded source, occurrence and Competency identities."));
                continue;
            }
            if (!occurrences.Add(grant.OccurrenceId))
            {
                issues.Add(new("duplicate-competency-grant-occurrence", grant.OccurrenceId,
                    "One grant occurrence can contribute only once."));
                continue;
            }
            if (!catalog.TryGetById(grant.CompetencyId, out var definition) ||
                definition?.Category != "competency")
            {
                issues.Add(new("unknown-competency-grant-target", grant.CompetencyId,
                    "The grant target must be a native Competency."));
                continue;
            }
            if (grant.Ranks is < 0 or > TransportRankLimit ||
                (grant.Ranks == 0 && !grant.Proficient))
            {
                issues.Add(new("invalid-competency-grant-effect", grant.OccurrenceId,
                    "Each grant must provide a nonnegative rank increment, proficiency, or both."));
                continue;
            }
            var current = training.GetValueOrDefault(grant.CompetencyId)
                ?? new NativeCompetencyTrainingState(0, false);
            if (current.Ranks > TransportRankLimit - grant.Ranks)
            {
                issues.Add(new("competency-rank-overflow", grant.CompetencyId,
                    "Accumulated ranks exceed the transport safety limit."));
                continue;
            }
            training[grant.CompetencyId] = new(
                current.Ranks + grant.Ranks, current.Proficient || grant.Proficient);
        }

        // An invalid source-grant batch cannot yield a usable partial character.
        return issues.Count == 0
            ? new("resolved", training, [])
            : new("invalid", new Dictionary<string, NativeCompetencyTrainingState>(),
                issues.ToArray());
    }

    public static NativeCompetencyPrerequisiteResult CheckPrerequisite(
        NativeCatalogSnapshot catalog,
        IReadOnlyDictionary<string, NativeCompetencyTrainingState> training,
        NativeCompetencyPrerequisite prerequisite)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(prerequisite);

        if (string.IsNullOrWhiteSpace(prerequisite.CompetencyId) ||
            prerequisite.MinimumRanks is < 0 or > TransportRankLimit ||
            !catalog.TryGetById(prerequisite.CompetencyId, out var definition) ||
            definition?.Category != "competency")
            return new("unresolved", null,
                "The prerequisite does not reference a valid native Competency and rank threshold.");

        var state = training.GetValueOrDefault(prerequisite.CompetencyId)
            ?? new NativeCompetencyTrainingState(0, false);
        var satisfied = state.Ranks >= prerequisite.MinimumRanks &&
            (!prerequisite.RequiresProficiency || state.Proficient);
        return new(satisfied ? "satisfied" : "not-satisfied", satisfied);
    }
}
