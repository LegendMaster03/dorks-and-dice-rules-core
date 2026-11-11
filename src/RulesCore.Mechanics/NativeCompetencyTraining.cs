using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// An occurrence of a source-validated Competency grant. The caller must
/// establish that the character actually earned the source and any choices;
/// a SourceId string by itself is never proof of entitlement.
/// </summary>
public sealed record NativeCompetencyTrainingGrant(
    string SourceId, string OccurrenceId, string CompetencyId,
    int Ranks = 0, bool Proficient = false,
    string? DuplicateProficiencyAlternativeCompetencyId = null);

/// <summary>Ruleset-selected outcome for independent duplicate proficiency grants.</summary>
public enum NativeCompetencyDuplicateProficiencyPolicy
{
    AddRank = 0,
    ChooseAnotherProficiency = 1
}

/// <summary>Identifies the original source occurrence and resolved duplicate outcome.</summary>
public sealed record NativeCompetencyDuplicateResolution(
    string SourceId, string OccurrenceId, string CompetencyId,
    string Outcome, string? AlternativeCompetencyId = null);

/// <summary>Ranks and proficiency are independent parts of one Competency.</summary>
public sealed record NativeCompetencyTrainingState(int Ranks, bool Proficient);

public sealed record NativeCompetencyGrantResult(
    string Status, IReadOnlyDictionary<string, NativeCompetencyTrainingState> Training,
    IReadOnlyList<NativeIssue> Issues,
    IReadOnlyList<NativeCompetencyDuplicateResolution>? DuplicateResolutions = null);

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

    /// <summary>
    /// Aggregate already authorized grants. The provisional default converts
    /// each distinct duplicate proficiency occurrence to one rank; the other
    /// mode requires explicit authorized alternate proficiency selections.
    /// The caller is responsible for ruleset-setting and source authorization.
    /// </summary>
    public static NativeCompetencyGrantResult CombineValidated(
        NativeCatalogSnapshot catalog, IReadOnlyList<NativeCompetencyTrainingGrant> grants,
        NativeCompetencyDuplicateProficiencyPolicy duplicatePolicy =
            NativeCompetencyDuplicateProficiencyPolicy.AddRank)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(grants);

        var training = new SortedDictionary<string, NativeCompetencyTrainingState>(
            StringComparer.Ordinal);
        var issues = new List<NativeIssue>();
        var occurrences = new HashSet<string>(StringComparer.Ordinal);
        var validated = new List<NativeCompetencyTrainingGrant>();
        var resolutions = new List<NativeCompetencyDuplicateResolution>();

        if (duplicatePolicy is not (NativeCompetencyDuplicateProficiencyPolicy.AddRank
            or NativeCompetencyDuplicateProficiencyPolicy.ChooseAnotherProficiency))
            issues.Add(new("invalid-competency-duplicate-policy", "competencies",
                "Unknown Competency duplicate-proficiency policy."));
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
            if (grant.DuplicateProficiencyAlternativeCompetencyId is { } alternative &&
                (!grant.Proficient || string.IsNullOrWhiteSpace(alternative) ||
                 alternative.Length > 160))
            {
                issues.Add(new("invalid-competency-alternative", grant.OccurrenceId,
                    "Only a bounded proficiency grant can select a duplicate alternative."));
                continue;
            }
            validated.Add(grant);
        }

        if (issues.Count > 0)
            return new("invalid", new Dictionary<string, NativeCompetencyTrainingState>(),
                issues.ToArray(), []);

        var proficiencyGroups = validated.Where(g => g.Proficient)
            .GroupBy(g => g.CompetencyId, StringComparer.Ordinal).ToArray();
        var originalProficiencies = proficiencyGroups.Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
        var alternatives = new HashSet<string>(StringComparer.Ordinal);

        if (duplicatePolicy == NativeCompetencyDuplicateProficiencyPolicy.AddRank)
        {
            foreach (var grant in validated)
                if (grant.DuplicateProficiencyAlternativeCompetencyId is not null)
                    issues.Add(new("unexpected-competency-alternative", grant.OccurrenceId,
                        "Rank mode does not accept alternate proficiency choices."));

            // A sorted occurrence establishes stable provenance, not
            // an order of earning. Outcomes are independent of input order.
            foreach (var group in proficiencyGroups)
                foreach (var extra in group
                    .OrderBy(g => g.OccurrenceId, StringComparer.Ordinal).Skip(1))
                    resolutions.Add(new(extra.SourceId, extra.OccurrenceId,
                        group.Key, "add-rank"));
        }
        else if (duplicatePolicy ==
            NativeCompetencyDuplicateProficiencyPolicy.ChooseAnotherProficiency)
        {
            foreach (var group in proficiencyGroups)
            {
                var chosen = group.Where(g =>
                    g.DuplicateProficiencyAlternativeCompetencyId is not null).ToArray();
                if (chosen.Length != group.Count() - 1)
                    issues.Add(new("missing-competency-duplicate-alternative", group.Key,
                        "Each duplicate proficiency needs an authorized alternate choice."));

                foreach (var grant in chosen)
                {
                    var target = grant.DuplicateProficiencyAlternativeCompetencyId!;
                    if (originalProficiencies.Contains(target) ||
                        !alternatives.Add(target) ||
                        !catalog.TryGetById(target, out var definition) ||
                        definition?.Category != "competency")
                        issues.Add(new("invalid-competency-duplicate-alternative",
                            grant.OccurrenceId,
                            "The chosen alternative must be a distinct unproficient Competency."));
                    else
                        resolutions.Add(new(grant.SourceId, grant.OccurrenceId,
                            grant.CompetencyId, "alternate-proficiency", target));
                }
            }
        }

        if (issues.Count > 0)
            return new("invalid", new Dictionary<string, NativeCompetencyTrainingState>(),
                issues.ToArray(), []);

        foreach (var grant in validated)
        {
            var current = training.GetValueOrDefault(grant.CompetencyId)
                ?? new NativeCompetencyTrainingState(0, false);
            if (current.Ranks > TransportRankLimit - grant.Ranks)
            {
                issues.Add(new("competency-rank-overflow", grant.CompetencyId,
                    "Accumulated ranks exceed the transport safety limit."));
                continue;
            }
            // An alternative redirects only the extra proficiency entitlement.
            // Ranks explicitly granted by that source stay on its original method.
            var proficiencyApplies = grant.Proficient &&
                grant.DuplicateProficiencyAlternativeCompetencyId is null;
            training[grant.CompetencyId] = new(
                current.Ranks + grant.Ranks, current.Proficient || proficiencyApplies);
        }
        if (duplicatePolicy == NativeCompetencyDuplicateProficiencyPolicy.AddRank)
        {
            foreach (var group in proficiencyGroups)
            {
                var current = training.GetValueOrDefault(group.Key)!;
                var extraRanks = group.Count() - 1;
                if (current.Ranks > TransportRankLimit - extraRanks)
                {
                    issues.Add(new("competency-rank-overflow", group.Key,
                        "Duplicate proficiency conversion exceeds the rank safety limit."));
                    continue;
                }
                training[group.Key] = new(current.Ranks + extraRanks, current.Proficient);
            }
        }
        else
        {
            foreach (var resolution in resolutions)
            {
                var target = resolution.AlternativeCompetencyId!;
                var current = training.GetValueOrDefault(target)
                    ?? new NativeCompetencyTrainingState(0, false);
                training[target] = current with { Proficient = true };
            }
        }

        // Proficiency groups are discovered in caller order, but audit results must
        // not depend on that order. Stable provenance is important when replaying
        // the same authorized grants after switching a playtest policy.
        var orderedResolutions = resolutions
            .OrderBy(resolution => resolution.CompetencyId, StringComparer.Ordinal)
            .ThenBy(resolution => resolution.OccurrenceId, StringComparer.Ordinal)
            .ToArray();

        return issues.Count == 0
            ? new("resolved", training, [], orderedResolutions)
            : new("invalid", new Dictionary<string, NativeCompetencyTrainingState>(),
                issues.ToArray(), []);
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
