using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Projects source-neutral, always-on saving-throw advantages and fixed skill
/// bonuses from selected native Origin traits. Each effect remains attached to
/// its complete canonical trait: this view neither stacks overlapping benefits
/// nor evaluates a saving throw or skill check. Construction authorization is
/// an input, not a conclusion of this projector.
/// </summary>
public static class NativeOriginPassiveEntitlementProjector
{
    public sealed record SavingThrowAdvantage(
        IReadOnlyList<string> Against, IReadOnlyList<string> Abilities, string? Scope);

    public sealed record SkillBonus(string SkillId, int Amount);

    public sealed record GrantedTrait(
        string GrantId, string DefinitionId, string SourceId, string OccurrencePath,
        IReadOnlyList<string> SourceCandidateIds,
        SavingThrowAdvantage? SavingThrows, IReadOnlyList<SkillBonus> SkillBonuses,
        JsonElement CompleteEffect);

    public sealed record OutsideScopeTrait(
        string GrantId, string DefinitionId, string SourceId,
        string OccurrencePath, JsonElement CompleteEffect)
    {
        // These fields describe verified selection evidence, not executable
        // semantics, parameter authorizations, or gameplay state.
        public string ManifestationId { get; init; } = "";
        public IReadOnlyList<string> SourceCandidateIds { get; init; } = [];
    }

    public sealed record View(
        string Status, IReadOnlyList<GrantedTrait> Traits,
        IReadOnlyList<string> UnresolvedReasons, bool PreviewOnly = true)
    {
        /// <summary>
        /// Authenticated selections of other native traits whose complete
        /// canonical definitions remain opaque to this bounded interpreter.
        /// Status="none" means NO TYPED PASSIVE ENTRIES, not no Origin traits.
        /// These definitions are not executable mechanical entitlements.
        /// </summary>
        public IReadOnlyList<OutsideScopeTrait> OutsideScopeTraits { get; init; } = [];
    }

    public static View Project(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result) =>
        ProjectCore(catalog, result, approvedMixed: false);

    public static View ProjectApprovedMixed(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result) =>
        ProjectCore(catalog, result, approvedMixed: true);

    private static View ProjectCore(NativeCatalogSnapshot catalog,
        NativeOriginConstructionResult result, bool approvedMixed)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(result);

        var foundation = approvedMixed
            ? NativeOriginApprovedMixedIntrinsicProjector.Project(result)
            : NativeOriginIntrinsicProjector.Project(result);
        if (foundation.Status != "resolved" || result.Profile is not { } profile)
            return Unresolved(approvedMixed
                ? "mixed-origin-foundation-unverified"
                : "direct-origin-foundation-unverified");

        var projected = new List<GrantedTrait>();
        var outsideScope = new List<OutsideScopeTrait>();
        var seenGrants = new HashSet<string>(StringComparer.Ordinal);
        var seenManifestations = new HashSet<string>(StringComparer.Ordinal);
        var seenSourceCandidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grant in profile.Underlying.InherentGrants)
        {
            var definitionId = grant.Identity.MechanicalDefinitionId;
            // A mutated Kind must not disguise a real native trait as an
            // unrelated characteristic. Intrinsics are handled separately.
            if (grant.Identity.Kind != NativeOriginCharacteristicKind.Trait)
            {
                if (grant.Identity.Kind != NativeOriginCharacteristicKind.Intrinsic ||
                    grant.Identity.Occurrence.OccurrencePath?
                        .StartsWith("/traits/", StringComparison.Ordinal) == true ||
                    definitionId is not null &&
                    catalog.TryGetById(definitionId, out var masquerading) &&
                    masquerading is not null && masquerading.Category == "origin-trait")
                    return Unresolved("native-characteristic-kind-unverified:" + grant.Id);
                continue;
            }

            if (string.IsNullOrWhiteSpace(definitionId) ||
                !catalog.TryGetById(definitionId, out var definition) ||
                definition is null || definition.Category != "origin-trait" ||
                !definition.Definition.TryGetProperty("trait", out var canonical) ||
                canonical.ValueKind != JsonValueKind.Object ||
                grant.EffectiveValue is not { ValueKind: JsonValueKind.Object } effect ||
                !JsonElement.DeepEquals(canonical, effect) ||
                !seenGrants.Add(grant.Id) ||
                !ValidSourceOccurrence(catalog, grant, definitionId))
                return Unresolved("native-trait-definition-or-occurrence-unverified:" +
                    (definitionId ?? grant.Id));

            // A canonical source definition and its occurrence are not proof
            // that the character actually selected it. This must precede BOTH
            // the typed-passive path and the opaque outside-scope path.
            // Do not require every opaque mechanic to fit the passive grammar,
            // and do not execute its choices, dependencies or resource pools.
            if (!VerifiedSelectedGrant(result, grant, approvedMixed))
                return Unresolved("native-trait-selection-unverified:" + definitionId);

            // A selected candidate/manifestation can produce only one surviving
            // effective grant. A distinct forged grant ID must not allow the
            // same selected occurrence to be reported twice. This does not
            // collapse distinct authorized RetainBoth occurrences or multiple
            // contributors that belong to ONE consolidated grant.
            if (!seenManifestations.Add(grant.ManifestationId) ||
                grant.SourceCandidateIds.Any(id => !seenSourceCandidates.Add(id)))
                return Unresolved("native-trait-selection-reused:" + definitionId);

            var canonicalClaims = ClaimsSupportedShape(canonical);
            var effectiveClaims = ClaimsSupportedShape(effect);
            if (!canonicalClaims && !effectiveClaims)
            {
                outsideScope.Add(new(grant.Id, definitionId,
                    grant.Identity.Occurrence.Source.Id,
                    // ValidSourceOccurrence has already rejected null or malformed paths.
                    grant.Identity.Occurrence.OccurrencePath!, canonical.Clone())
                {
                    ManifestationId = grant.ManifestationId,
                    SourceCandidateIds = grant.SourceCandidateIds
                        .OrderBy(x => x, StringComparer.Ordinal).ToArray()
                });
                continue;
            }

            // Inspect both authored and serialized shapes. Otherwise a forged
            // payload could add an entitlement to an unrelated definition, or
            // remove an authored entitlement to evade provenance validation.
            if (!canonicalClaims || !effectiveClaims ||
                grant.Parameters is { Count: > 0 } ||
                grant.ParameterSources is { Count: > 0 } ||
                grant.ResourceOwners is { Count: > 0 } ||
                grant.ResolvedChoices is { Count: > 0 } ||
                // This increment supports only unconditional creation-time
                // passives. A future level gate must not activate by default.
                canonical.TryGetProperty("requiredCharacterLevel", out _) ||
                !HasFixedPassiveSelection(result, grant))
                return Unresolved("passive-entitlement-grant-not-verified:" +
                    (definitionId ?? grant.Id));

            SavingThrowAdvantage? savingThrows = null;
            if (canonical.TryGetProperty("savingThrows", out var saves) &&
                !TrySavingThrows(saves, out savingThrows))
                return Unresolved("saving-throw-grammar-unrecognized:" + definitionId);

            IReadOnlyList<SkillBonus> skillBonuses = [];
            if (canonical.TryGetProperty("skillBonuses", out var skills) &&
                !TrySkillBonuses(catalog, skills, out skillBonuses))
                return Unresolved("skill-bonus-grammar-unrecognized:" + definitionId);

            // No combined bonus, chosen "best" effect, proficiency, or
            // character-state mutation is implied by this collection.
            projected.Add(new(grant.Id, definitionId,
                grant.Identity.Occurrence.Source.Id,
                grant.Identity.Occurrence.OccurrencePath!,
                grant.SourceCandidateIds.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                savingThrows, skillBonuses, canonical.Clone()));
        }

        return new(projected.Count == 0 ? "none" : "resolved",
            projected.OrderBy(x => x.DefinitionId, StringComparer.Ordinal)
                .ThenBy(x => x.SourceId, StringComparer.Ordinal)
                .ThenBy(x => x.GrantId, StringComparer.Ordinal).ToArray(), [])
        {
            OutsideScopeTraits = outsideScope
                .OrderBy(x => x.DefinitionId, StringComparer.Ordinal)
                .ThenBy(x => x.SourceId, StringComparer.Ordinal)
                .ThenBy(x => x.OccurrencePath, StringComparer.Ordinal)
                .ThenBy(x => x.GrantId, StringComparer.Ordinal).ToArray()
        };
    }

    /// <summary>
    /// The catalog's source-position-to-definition index is derived from the
    /// original native trait reference. Merely having a valid source ID and
    /// a valid definition ID does not prove they belong to the same occurrence.
    /// </summary>
    private static bool ValidSourceOccurrence(NativeCatalogSnapshot catalog,
        NativeOriginEffectiveGrant grant, string definitionId)
    {
        var source = grant.Identity.Occurrence.Source;
        var path = grant.Identity.Occurrence.OccurrencePath;
        if (grant.Identity.IntrinsicSlotId is not null ||
            grant.Identity.TraitConceptId is not null ||
            source.Kind != NativeOriginSourceKind.Native ||
            source.Version is not null ||
            !catalog.TryGetById(source.Id, out var owner) ||
            owner is null || owner.Category is not ("species" or "ancestry") ||
            path is null || !path.StartsWith("/traits/", StringComparison.Ordinal) ||
            !int.TryParse(path.AsSpan("/traits/".Length),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var index) ||
            index < 0 || path != "/traits/" + index.ToString(
                System.Globalization.CultureInfo.InvariantCulture) ||
            !owner.Definition.TryGetProperty("traits", out var traits) ||
            traits.ValueKind != JsonValueKind.Array ||
            index >= traits.GetArrayLength())
            return false;

        var reference = traits[index];
        return reference.ValueKind == JsonValueKind.Object &&
            catalog.OriginTraitDefinitionId(source.Id, index) == definitionId;
    }

    private static bool ClaimsSupportedShape(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object &&
        (value.TryGetProperty("savingThrows", out _) ||
         value.TryGetProperty("skillBonuses", out _));

    private static bool TrySavingThrows(JsonElement value,
        out SavingThrowAdvantage? advantage)
    {
        advantage = null;
        if (value.ValueKind != JsonValueKind.Object) return false;
        var properties = value.EnumerateObject().Select(x => x.Name).ToArray();
        if (properties.Length == 1 && properties[0] == "advantageAgainst" &&
            value.TryGetProperty("advantageAgainst", out var against) &&
            TryStrings(against, out var effects))
        {
            advantage = new(effects, [], null);
            return true;
        }

        if (properties.Length == 2 &&
            properties.Contains("advantageAbilities", StringComparer.Ordinal) &&
            properties.Contains("scope", StringComparer.Ordinal) &&
            value.TryGetProperty("scope", out var scope) &&
            scope.ValueKind == JsonValueKind.String &&
            scope.GetString() == "magic-only" &&
            value.TryGetProperty("advantageAbilities", out var abilities) &&
            TryStrings(abilities, out var chosen) &&
            chosen.All(x => x is "strength" or "dexterity" or "constitution"
                or "intelligence" or "wisdom" or "charisma"))
        {
            advantage = new([], chosen, "magic-only");
            return true;
        }
        return false;
    }

    private static bool TrySkillBonuses(NativeCatalogSnapshot catalog,
        JsonElement value, out IReadOnlyList<SkillBonus> bonuses)
    {
        bonuses = [];
        if (value.ValueKind != JsonValueKind.Object) return false;
        var fields = value.EnumerateObject().ToArray();
        if (fields.Length == 0) return false;
        var result = new List<SkillBonus>();
        foreach (var field in fields)
        {
            if (!field.Name.StartsWith("skill.", StringComparison.Ordinal) ||
                !catalog.TryGetById(field.Name, out var skill) ||
                skill is null || skill.Category != "skill" ||
                field.Value.ValueKind != JsonValueKind.Number ||
                !field.Value.TryGetInt32(out var amount) ||
                amount is <= 0 or > 1_000_000)
                return false;
            result.Add(new(field.Name, amount));
        }
        bonuses = result.OrderBy(x => x.SkillId, StringComparer.Ordinal).ToArray();
        return true;
    }

    private static bool TryStrings(JsonElement value, out IReadOnlyList<string> result)
    {
        result = [];
        if (value.ValueKind != JsonValueKind.Array ||
            value.GetArrayLength() == 0) return false;
        var values = new List<string>();
        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(element.GetString()))
                return false;
            values.Add(element.GetString()!);
        }
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            return false;
        result = values.ToArray();
        return true;
    }

    /// <summary>
    /// Validates source authorization and exact selected manifestation evidence
    /// for ANY native trait, including opaque definitions. Does not adjudicate
    /// the trait's separate parameters, choices, resources or runtime effects.
    /// </summary>
    private static bool VerifiedSelectedGrant(NativeOriginConstructionResult result,
        NativeOriginEffectiveGrant grant, bool mixed)
    {
        if (mixed)
        {
            if (!NativeOriginApprovedMixedIntrinsicProjector
                .HasVerifiedContributingEvidence(result, grant))
                return false;
        }
        else if (grant.Authorization?.Adjudication != NativeOriginAdjudication.Supported ||
            grant.Authorization.NativeRuleReference !=
                grant.Identity.Occurrence.Source.Id ||
            grant.SourceCandidateIds.Count != 1)
            return false;

        if (grant.SourceCandidateIds.Count == 0 ||
            grant.SourceCandidateIds.Distinct(StringComparer.Ordinal).Count() !=
                grant.SourceCandidateIds.Count)
            return false;

        var selected = result.EvaluatedProposal.Candidates
            .Where(x => grant.SourceCandidateIds.Contains(x.Id, StringComparer.Ordinal))
            .ToArray();
        if (selected.Length != grant.SourceCandidateIds.Count ||
            // A candidate's selected manifestation must be this grant's
            // actual origin, not an unrelated selection sharing an effect.
            result.EvaluatedProposal.Manifestations.Count(m =>
                m.Id == grant.ManifestationId) != 1 ||
            !result.EvaluatedProposal.Manifestations.Any(m =>
                m.Id == grant.ManifestationId &&
                grant.SourceCandidateIds.Contains(m.CandidateId, StringComparer.Ordinal)))
            return false;

        return selected.All(candidate =>
                candidate.Identity == grant.Identity &&
                (!mixed && candidate.ContributorId is null ||
                    mixed && candidate.ContributorId is not null) &&
                (!mixed ? candidate.SelectionBasis.Eligibility ==
                    NativeOriginEligibility.Eligible
                    : candidate.SelectionBasis.Eligibility is
                        (NativeOriginEligibility.Eligible or NativeOriginEligibility.DmAuthorized)) &&
                candidate.OfferedValue is { } offered &&
                grant.EffectiveValue is { } actual &&
                JsonElement.DeepEquals(offered, actual) &&
                candidate.UnresolvedEvidence is not { Count: > 0 } &&
                result.EvaluatedProposal.Manifestations.Count(x =>
                    x.CandidateId == candidate.Id) == 1 &&
                result.EvaluatedProposal.Manifestations
                    .Where(x => x.CandidateId == candidate.Id)
                    .All(x => (x.SelectedValue is null ||
                         grant.EffectiveValue is { } selectedValue &&
                         JsonElement.DeepEquals(x.SelectedValue.Value, selectedValue))));
    }

    /// <summary>
    /// A recognized fixed passive is safe to type only if it has no unresolved
    /// configuration, on-use choices, per-source parameters or resource owners.
    /// Opaque traits are retained as definitions, NOT typed as unconditional.
    /// </summary>
    private static bool HasFixedPassiveSelection(NativeOriginConstructionResult result,
        NativeOriginEffectiveGrant grant) =>
        grant.SourceCandidateIds.All(id =>
            result.EvaluatedProposal.Candidates
                .Where(candidate => candidate.Id == id)
                .All(candidate =>
                    candidate.OfferedParameters is not { Count: > 0 } &&
                    candidate.RequiredChoices is not { Count: > 0 }) &&
            result.EvaluatedProposal.Manifestations
                .Where(manifest => manifest.CandidateId == id)
                .All(manifest =>
                    manifest.SelectedParameters is not { Count: > 0 } &&
                    manifest.Choices is not { Count: > 0 }));

    private static View Unresolved(string reason) =>
        new("unresolved", [], [reason]);
}
