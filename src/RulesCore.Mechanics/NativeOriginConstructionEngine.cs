using System.Text.Json;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// One catalog-backed construction evaluator for direct, mixed, nested and table-authored
/// Origin proposals. No invocation executes a transformation or changes native content.
/// </summary>
public sealed class NativeOriginConstructionEngine(NativeCatalogSnapshot catalog)
{
    private readonly NativeOriginContractValidator _validator = new(catalog);
    private readonly NativeOriginSourceDeconstructor _deconstructor = new(catalog);
    private readonly NativeOriginNativeLinkedSelectionEvaluator _nativeLinks = new(catalog);
    private readonly NativeOriginBiologicalTransmissionEvaluator _biological = new(catalog);
    private readonly NativeOriginBiologicalInheritanceEvaluator _inheritance = new(catalog);
    private readonly NativeOriginReconciler _reconciler = new(catalog);
    private readonly NativeOriginConstraintEvaluator _constraints = new(catalog);
    private readonly NativeOriginSynergyEvaluator _synergies = new(catalog);

    public NativeOriginConstructionResult Evaluate(NativeOriginConstructionRequest request,
        INativeOriginAuthority? authority = null,
        INativeOriginManifestedContributorAuthority? manifestedContributorAuthority = null) =>
        EvaluateCore(request, authority, manifestedContributorAuthority,
            reconstructionTarget: false, trustedNativeOffers: null);

    /// <summary>
    /// Shared construction path for catalog-verified, procedure-specific native
    /// offers. Never expose this entrypoint to a client-controlled offer list.
    /// </summary>
    internal NativeOriginConstructionResult EvaluateWithTrustedNativeOffers(
        NativeOriginConstructionRequest request,
        IReadOnlyList<NativeOriginCandidate> trustedNativeOffers,
        INativeOriginAuthority? authority = null,
        INativeOriginManifestedContributorAuthority? manifestedContributorAuthority = null) =>
        EvaluateCore(request, authority, manifestedContributorAuthority,
            reconstructionTarget: false, trustedNativeOffers: trustedNativeOffers);

    // Only the trusted, result-oriented reconstruction service may construct
    // a target under a post-origination method without claiming a new birth.
    internal NativeOriginConstructionResult EvaluateReconstructionTarget(
        NativeOriginConstructionRequest request,
        INativeOriginAuthority? authority = null,
        INativeOriginManifestedContributorAuthority? manifestedContributorAuthority = null) =>
        EvaluateCore(request, authority, manifestedContributorAuthority,
            reconstructionTarget: true, trustedNativeOffers: null);

    private NativeOriginConstructionResult EvaluateCore(
        NativeOriginConstructionRequest request, INativeOriginAuthority? authority,
        INativeOriginManifestedContributorAuthority? manifestedContributorAuthority,
        bool reconstructionTarget,
        IReadOnlyList<NativeOriginCandidate>? trustedNativeOffers)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Proposal);
        var (trustedProposal, verifiedManifested, verificationIssues) =
            NativeOriginManifestedContributorVerifier.Resolve(request.Proposal,
                request.TrustedScope, manifestedContributorAuthority);
        var (sourceOffers, discoveryIssues) = _deconstructor.Deconstruct(
            trustedProposal, trustedNativeOffers);
        var trustedNativeOfferIds = (trustedNativeOffers ?? [])
            .Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        // Biology is one origination procedure, not a property of every
        // characteristic or the universal Origin engine. The method's authored
        // rule promotes only permitted offers; the player still selects them.
        var (withNativeLinks, nativeLinkIssues) =
            _nativeLinks.Expand(_biological.Evaluate(sourceOffers, verifiedManifested));
        var discovered = withNativeLinks;
        var issues = new List<NativeOriginContractIssue>(discoveryIssues);
        issues.AddRange(verificationIssues);
        // Once a parent's authoritative underlying state has been resolved,
        // keep its previous composition as audit evidence. Historical candidates
        // are never imported as new selectable traits or extra resource pools.
        var inheritedOffers = trustedProposal.Contributors
            .Where(x => x is not null && verifiedManifested.Contains(x.Id) &&
                x.ManifestedProfile?.InherentGrants is not null)
            .SelectMany(x => x.ManifestedProfile!.InherentGrants.Select(grant => new
            {
                CandidateId = "nested:" + x.Id + ":" + grant.Id,
                ProfileId = x.Source.Id,
                ProfileRevision = x.Source.Version!,
                ContributorId = x.Id,
                Grant = grant
            }))
            .GroupBy(x => x.CandidateId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        issues.AddRange(nativeLinkIssues);
        // Only trusted snapshots can supply a carried, previously unexpressed
        // gene. An older direct profile still exposes its effective grants.
        var parentGeneOffers = trustedProposal.Contributors
            .Where(x => x is not null && verifiedManifested.Contains(x.Id) &&
                x.ManifestedProfile?.BiologicalGenes is not null)
            .SelectMany(x => x.ManifestedProfile!.BiologicalGenes!.Select(gene => new
            {
                CandidateId = "nested:" + x.Id + ":gene:" + gene.Id,
                ProfileId = x.Source.Id,
                ProfileRevision = x.Source.Version!,
                ContributorId = x.Id,
                Gene = gene
            }))
            .GroupBy(x => x.CandidateId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var candidates = discovered.Candidates.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var manifests = (discovered.Manifestations ?? []).ToList();
        var selected = new HashSet<string>(manifests.Select(x => x.CandidateId),
            StringComparer.Ordinal);

        // Only unambiguous directly selected native defaults can be manifested
        // automatically. A choice, a mixed contribution, or a restricted source
        // always needs its own actual selection and authorization.
        if (request.AutoManifestDirectDefaults && discovered.Intent == NativeOriginIntent.Direct)
        {
            foreach (var candidate in discovered.Candidates)
            {
                if (candidate.ContributorId is not null ||
                    candidate.SelectionBasis.Eligibility != NativeOriginEligibility.Eligible ||
                    selected.Contains(candidate.Id) ||
                    (candidate.RequiredChoices ?? []).Any(x =>
                        x.Timing == NativeOriginChoiceTiming.Creation))
                    continue;
                if (candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                    candidate.OfferedValue is null) continue;
                manifests.Add(new("auto:" + candidate.Id, candidate.Id,
                    candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic
                        ? candidate.OfferedValue : null));
                selected.Add(candidate.Id);
            }
        }

        // A finite choice for an intrinsic slot is the selected intrinsic value,
        // not a separate mechanical source. Preserve the explicit choice record.
        for (var i = 0; i < manifests.Count; i++)
        {
            var manifest = manifests[i];
            if (manifest.SelectedValue is not null ||
                !candidates.TryGetValue(manifest.CandidateId, out var candidate) ||
                candidate.Identity.Kind != NativeOriginCharacteristicKind.Intrinsic)
                continue;
            // This shortcut applies only to an unambiguous single finite
            // choice. Multiple or malformed records belong to the structural
            // validator, not SingleOrDefault() (which throws for two choices).
            if (manifest.Choices is not { Count: 1 } choices ||
                choices[0] is not { SelectedOptionIds: { Count: 1 } } choice ||
                candidate.RequiredChoices is not { Count: 1 } requirements ||
                requirements[0] is not { } requirement ||
                choice.RequirementId != requirement.Id ||
                requirement.FiniteOptions?.Contains(choice.SelectedOptionIds[0],
                    StringComparer.Ordinal) != true)
                continue;
            if (choice.SelectedOptionIds[0] is not null)
                manifests[i] = manifest with
                {
                    SelectedValue = JsonSerializer.SerializeToElement(choice.SelectedOptionIds[0])
                };
        }

        var proposal = discovered with { Manifestations = manifests.ToArray() };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var accepted = new List<NativeOriginSelectedGrant>();
        var usedDm = false;
        var verifiedDmDecisionIds = new SortedSet<string>(StringComparer.Ordinal);

        NativeTableRuleDecision? Verified(string kind, string id)
        {
            if (authority is null || request.TrustedScope is null) return null;
            foreach (var decision in proposal.TableDecisions ?? [])
            {
                if (decision is null || decision.Target is null || decision.Scope is null ||
                    decision.Target.Kind != kind || decision.Target.Id != id ||
                    !ScopeMatches(decision.Scope, request.TrustedScope, proposal.Id))
                    continue;
                if (authority.Authorizes(decision, request.TrustedScope))
                    return decision;
            }
            return null;
        }

        // Replace untrusted eligibility claims only after verifying the exact decision.
        // The structural validator then inspects the actual table-authorized basis.
        proposal = proposal with { Candidates = proposal.Candidates.Select(candidate =>
        {
            var d = Verified("candidate", candidate.Id) ??
                Verified("source", candidate.Identity.Occurrence.Source.Id);
            return d is not null &&
                JsonString(d.EffectiveOutcome, "eligibility") == "eligible" &&
                (selected.Contains(candidate.Id) ||
                 proposal.BiologicalTransmission?.CandidateIds?.Contains(candidate.Id,
                     StringComparer.Ordinal) == true)
                ? candidate with { SelectionBasis = new(NativeOriginEligibility.DmAuthorized,
                    TableDecisionId: d.Id) } : candidate;
        }).ToArray() };
        candidates = proposal.Candidates.ToDictionary(x => x.Id, StringComparer.Ordinal);
        // Transmission is a distinct player decision from manifestation.
        // Validate against catalog-discovered, table-authenticated candidate
        // eligibility; a proposal never self-authorizes heredity.
        var transmission = NativeOriginBiologicalTransmissionValidator.Review(proposal, Verified);
        issues.AddRange(transmission.Issues);
        var structural = _validator.ReviewProposal(proposal);
        issues.AddRange(structural.Issues.Where(x => !UnselectedOfferIssue(x, selected)));
        var nativeMethod = _validator.FindMethod(proposal.Method);
        issues.AddRange(NativeOriginProcessEvaluator.Evaluate(proposal,
            nativeMethod, Verified));
        if (!reconstructionTarget && proposal.Intent == NativeOriginIntent.Direct &&
            nativeMethod?.Stage == "post-origination")
            Add("post-origin-method-requires-existing-individual", "method",
                NativeOriginIssueSeverity.Unresolved,
                "A post-origination method is not an initial-creation acquisition path; its authorized target must be executed against an existing individual.");

        // A creature-type list is a default, not an exhaustive method ban.
        // A process type supplied by a caller is not trusted unless it follows
        // the selected native Species or an exact authenticated table ruling.
        string? selectedNativeType = null;
        if (proposal.Intent == NativeOriginIntent.Direct &&
            proposal.SelectedSpeciesId is string speciesId &&
            catalog.TryGetById(speciesId, out var species) &&
            species?.Category == "species" &&
            species.Definition.TryGetProperty("creatureType", out var typeValue) &&
            typeValue.ValueKind == JsonValueKind.String)
            selectedNativeType = "creature-type." + typeValue.GetString();
        var processType = proposal.ProcessCreatureTypeId ?? selectedNativeType;
        if (processType is not null)
        {
            var normalizedType = processType.StartsWith("creature-type.",
                StringComparison.Ordinal) ? processType : "creature-type." + processType;
            var processTypeDecision = Verified("process-type", proposal.Id);
            var trustedType = normalizedType == selectedNativeType;
            if (!trustedType && processTypeDecision is not null &&
                JsonString(processTypeDecision.EffectiveOutcome, "creatureTypeId") ==
                    normalizedType)
            {
                trustedType = true;
                usedDm = true;
                verifiedDmDecisionIds.Add(processTypeDecision.Id);
            }
            if (!trustedType)
                Add("process-type-unverified", "processCreatureTypeId",
                    NativeOriginIssueSeverity.Unresolved,
                    "The proposed process creature type lacks native source or verified table evidence.");
            else if (nativeMethod?.Stage != "post-origination")
            {
                var available = new NativeOriginMethodAvailabilityEvaluator(catalog)
                    .Resolve(normalizedType, proposal.Method);
                if (available.Status == NativeOriginMethodDefaultStatus.Unresolved)
                    Add("process-type-default-unresolved", "method",
                        NativeOriginIssueSeverity.Unresolved,
                        "The selected native type or method default cannot be resolved.");
                else if (available.Status == NativeOriginMethodDefaultStatus.NotGranted &&
                    ReadBool(Verified("method", proposal.Method.MethodId)?.EffectiveOutcome ??
                        default, "allow") != true)
                    Add("process-method-not-default", "method",
                        NativeOriginIssueSeverity.Unresolved,
                        "No ordinary method default is granted for this creature type; an authored process or verified table permission is needed.");
            }
        }

        // An authenticated table may replace a source acquisition gate or
        // authorize a new method without rewriting native catalog metadata.
        var methodOverride = Verified("method", proposal.Method.MethodId);
        if (methodOverride is not null &&
            ReadBool(methodOverride.EffectiveOutcome, "allow") == true)
        {
            usedDm = true;
            verifiedDmDecisionIds.Add(methodOverride.Id);
        }
        issues.RemoveAll(issue =>
            issue.Code == "origin-source-restricted" &&
            JsonString(Verified("source", issue.Path)?.EffectiveOutcome ?? default,
                "eligibility") == "eligible" ||
            issue.Code == "method-not-in-native-catalog" &&
            ReadBool(Verified("method", proposal.Method.MethodId)?.EffectiveOutcome ?? default,
                "allow") == true);
        if (proposal.Intent == NativeOriginIntent.Direct &&
            proposal.SelectedSpeciesId is null && proposal.SelectedAncestryId is null &&
            proposal.ExistingConfiguration is null)
            Add("origin-source-required", "selectedSpeciesId",
                NativeOriginIssueSeverity.Incomplete,
                "A direct Origin needs an actual selected native Species or Ancestry.");
        if (proposal.Intent is NativeOriginIntent.Mixed or NativeOriginIntent.Custom &&
            manifests.Count == 0)
            Add("origin-selection-incomplete", "manifestations",
                NativeOriginIssueSeverity.Incomplete,
                "A proposed mixed or custom Origin needs selected, authorized characteristics.");

        // These fields represent preexisting state, not executable transformations.
        // Untrusted serialized authority claims must never certify alternate forms.
        if (proposal.ActiveTemporaryTransformation is { } active &&
            ReadBool(Verified("active-form", active.Id)?.EffectiveOutcome ?? default,
                "allow") != true)
            Add("active-form-authorization-deferred", active.Id,
                NativeOriginIssueSeverity.Unresolved,
                "Phase 2 preserves active form evidence but does not verify the form transition.");
        if (proposal.ExistingEffectiveConfigurationBasis is { } effectiveBasis)
        {
            var decision = Verified("configuration", proposal.Id);
            var matches = decision is not null && decision.Id == effectiveBasis.TableDecisionId &&
                decision.EffectiveOutcome.ValueKind == JsonValueKind.Object &&
                decision.EffectiveOutcome.TryGetProperty("configuration", out var config) &&
                proposal.ExistingEffectiveConfiguration is not null &&
                JsonElement.DeepEquals(config,
                    JsonSerializer.SerializeToElement(proposal.ExistingEffectiveConfiguration));
            if (effectiveBasis.Adjudication == NativeOriginAdjudication.DmAuthorized && !matches)
                Add("effective-configuration-claim-unverified", "existingEffectiveConfigurationBasis",
                    NativeOriginIssueSeverity.Unresolved,
                    "The full alternate configuration lacks a matching authenticated ruling.");
            if (effectiveBasis.Adjudication == NativeOriginAdjudication.Supported)
                Add("effective-configuration-rule-not-executed", "existingEffectiveConfigurationBasis",
                    NativeOriginIssueSeverity.Unresolved,
                    "The authored non-form configuration rule has not been executed.");
        }
        if (proposal.TemporaryTransformationPrecedence is { } precedence)
        {
            var approved = Verified("form-precedence", proposal.Id);
            var matches = approved?.Id == precedence.TableDecisionId &&
                JsonString(approved!.EffectiveOutcome, "effectiveFormId") ==
                    proposal.ActiveTemporaryTransformation?.FormId;
            if (precedence.Adjudication == NativeOriginAdjudication.DmAuthorized && !matches ||
                precedence.Adjudication == NativeOriginAdjudication.Supported)
                Add("form-precedence-not-executed", "temporaryTransformationPrecedence",
                    NativeOriginIssueSeverity.Unresolved,
                    "Concurrent form precedence needs executed native or matching authenticated evidence.");
        }
        foreach (var applicability in request.IndependentGrantApplicability ?? [])
            if (applicability.Basis?.Adjudication == NativeOriginAdjudication.DmAuthorized)
            {
                var decision = Verified("independent-grant", applicability.GrantId);
                if (decision?.Id != applicability.Basis.TableDecisionId ||
                    JsonString(decision!.EffectiveOutcome, "state") !=
                        applicability.State.ToString())
                    Add("independent-applicability-claim-unverified", applicability.GrantId,
                        NativeOriginIssueSeverity.Unresolved,
                        "Independent grant applicability needs matching authenticated evidence.");
            }


        if (proposal.Intent == NativeOriginIntent.Direct)
            foreach (var candidate in proposal.Candidates.Where(x =>
                x.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
                x.ContributorId is null && !selected.Contains(x.Id) &&
                (x.RequiredChoices ?? []).Any(y => y.Timing == NativeOriginChoiceTiming.Creation)))
                Add("required-origin-selection-missing", candidate.Id,
                    NativeOriginIssueSeverity.Incomplete,
                    "The directly selected source requires a persistent character-creation choice.");

        foreach (var manifest in manifests.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (!seen.Add(manifest.CandidateId))
            {
                Add("candidate-selected-more-than-once", manifest.CandidateId,
                    NativeOriginIssueSeverity.Invalid,
                    "One candidate cannot produce multiple independent grants without an authored rule.");
                continue;
            }
            if (!candidates.TryGetValue(manifest.CandidateId, out var candidate)) continue;
            // When explicit transmission was requested, a manifestation
            // cannot grant a gene that was never selected from the donor pool.
            // A null plan continues to use the existing legacy selections.
            if (transmission.Explicit &&
                proposal.Intent == NativeOriginIntent.Mixed &&
                proposal.Method.MethodId == "biological" &&
                proposal.Method.RuleId == "rule.creature-origination" &&
                !transmission.CandidateIds.Contains(candidate.Id))
            {
                Add("biological-expression-without-transmission", manifest.CandidateId,
                    NativeOriginIssueSeverity.Unresolved,
                    "An expressed biological characteristic must first be selected for transmission.");
                continue;
            }
            var source = candidate.Identity.Occurrence.Source;
            var nativeSource = source.Kind == NativeOriginSourceKind.Native &&
                catalog.TryGetById(source.Id, out var native) && native is not null;
            // Deconstruction already checked the selected Species/Ancestry chain,
            // including any intermediate native Ancestry family.
            var baseEligible = nativeSource &&
                candidate.SelectionBasis.Eligibility == NativeOriginEligibility.Eligible &&
                (proposal.Intent == NativeOriginIntent.Direct &&
                    candidate.ContributorId is null ||
                 proposal.Intent == NativeOriginIntent.Mixed &&
                    proposal.Method.MethodId == "biological" &&
                    candidate.ContributorId is not null &&
                    candidate.SelectionBasis.RuleReference == "rule.creature-origination" ||
                 candidate.ContributorId is null &&
                    trustedNativeOfferIds.Contains(candidate.Id));
            var permission = Verified("candidate", candidate.Id) ?? Verified("source", source.Id);
            var decisionEligible = permission is not null &&
                JsonString(permission.EffectiveOutcome, "eligibility") == "eligible";
            if (!baseEligible && !decisionEligible)
            {
                Add("selection-eligibility-unresolved", candidate.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    "Contributor or custom availability is not an approved effective grant.");
                continue;
            }
            if (decisionEligible)
            {
                usedDm = true;
                verifiedDmDecisionIds.Add(permission!.Id);
            }

            var needsDecision = !nativeSource && !decisionEligible;
            if (needsDecision) continue;

            var requirements = candidate.RequiredChoices ?? [];
            var choices = manifest.Choices ?? [];
            // Do not publish a grant from an invalid choice collection, even
            // when a different entry would satisfy an authored requirement.
            // The structural validator also reports these errors by path.
            if (choices.Any(choice => choice is null ||
                string.IsNullOrWhiteSpace(choice.RequirementId) ||
                choice.SelectedOptionIds is null ||
                choice.SelectedOptionIds.Any(string.IsNullOrWhiteSpace)) ||
                choices.Where(choice => choice is not null)
                    .GroupBy(choice => choice.RequirementId, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
            {
                Add("dependent-choice-invalid", candidate.Id,
                    NativeOriginIssueSeverity.Invalid,
                    "Choice records must be nonnull, uniquely identified and contain valid option lists.");
                continue;
            }
            var incomplete = false;
            foreach (var req in requirements)
            {
                var choice = choices.FirstOrDefault(x => x.RequirementId == req.Id);
                if (req.Timing == NativeOriginChoiceTiming.OnUse) continue;
                if (choice is null)
                {
                    if (req.Minimum > 0)
                    {
                        Add("dependent-choice-incomplete", candidate.Id + ":" + req.Id,
                            NativeOriginIssueSeverity.Incomplete,
                            "The selected trait requires an additional persistent choice.");
                        incomplete = true;
                    }
                    continue;
                }
                if (choice.SelectedOptionIds is null ||
                    choice.SelectedOptionIds.Count < req.Minimum ||
                    choice.SelectedOptionIds.Count > req.Maximum ||
                    choice.SelectedOptionIds.Distinct(StringComparer.Ordinal).Count() !=
                        choice.SelectedOptionIds.Count ||
                    (req.FiniteOptions is not null &&
                     choice.SelectedOptionIds.Any(x => !req.FiniteOptions.Contains(x, StringComparer.Ordinal))))
                {
                    Add("dependent-choice-invalid", candidate.Id + ":" + req.Id,
                        NativeOriginIssueSeverity.Invalid, "Selection is outside authored choice bounds.");
                    incomplete = true;
                }
                if (req.OptionCatalogId is not null &&
                    (string.IsNullOrWhiteSpace(choice.CatalogEligibilityEvidenceId) ||
                     authority is null || request.TrustedScope is null ||
                     (choice.SelectedOptionIds ?? []).Any(x => !authority.AuthorizesCatalogChoice(
                         req.OptionCatalogId, x, choice.CatalogEligibilityEvidenceId!))))
                {
                    Add("choice-authorization-unresolved", candidate.Id + ":" + req.Id,
                        NativeOriginIssueSeverity.Unresolved,
                        "Open-catalog selection lacks trusted membership and eligibility verification.");
                    incomplete = true;
                }
            }
            if (choices.Any(x => !requirements.Any(y => y.Id == x.RequirementId &&
                y.Timing != NativeOriginChoiceTiming.OnUse)))
            {
                Add("unrecognized-persistent-choice", candidate.Id,
                    NativeOriginIssueSeverity.Invalid, "An on-use or unoffered choice was persisted.");
                incomplete = true;
            }
            if (incomplete) continue;

            if (candidate.UnresolvedEvidence?.Count > 0)
            {
                Add("unsupported-source-binding", candidate.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    string.Join("; ", candidate.UnresolvedEvidence));
                continue;
            }
            var value = manifest.SelectedValue ?? candidate.OfferedValue;
            if (candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                value is null)
            {
                Add("intrinsic-value-incomplete", candidate.Id,
                    NativeOriginIssueSeverity.Incomplete,
                    "The intrinsic slot needs an explicit selected value.");
                continue;
            }
            var overridesValue = manifest.SelectedValue is not null &&
                candidate.OfferedValue is not null &&
                !SameJson(manifest.SelectedValue.Value, candidate.OfferedValue.Value);
            var overridesParameters = manifest.SelectedParameters?.Any(pair =>
                candidate.OfferedParameters is null ||
                !candidate.OfferedParameters.TryGetValue(pair.Key, out var offered) ||
                !SameJson(pair.Value, offered)) == true;
            NativeTableRuleDecision? modification = null;
            if (overridesValue || overridesParameters || manifest.TableDecisionId is not null)
            {
                modification = proposal.TableDecisions?.FirstOrDefault(x =>
                    x.Id == manifest.TableDecisionId && x.Target is not null &&
                    ((x.Target.Kind == "manifestation" && x.Target.Id == manifest.Id) ||
                     (x.Target.Kind == "candidate" && x.Target.Id == candidate.Id)));
                if (modification is null || request.TrustedScope is null || authority is null ||
                    !ScopeMatches(modification.Scope, request.TrustedScope, proposal.Id) ||
                    !authority.Authorizes(modification, request.TrustedScope) ||
                    (overridesValue && (!modification.EffectiveOutcome.TryGetProperty("value",
                        out var dmValue) || !SameJson(dmValue, value!.Value))) ||
                    (overridesParameters && !MatchingParameters(modification.EffectiveOutcome,
                        manifest.SelectedParameters!)))
                {
                    Add("manifestation-override-unauthorized", manifest.Id,
                        NativeOriginIssueSeverity.Unresolved,
                        "A selected mechanical alteration requires the exact verified table outcome.");
                    continue;
                }
                usedDm = true;
                verifiedDmDecisionIds.Add(modification.Id);
            }
            // A selected value on a choice-driven slot must agree with its finite
            // option; it cannot inject an unrelated intrinsic value.
            if (candidate.OfferedValue is null &&
                candidate.Identity.Kind == NativeOriginCharacteristicKind.Intrinsic &&
                requirements.Count > 0 && !choices.Any(x =>
                    (x.SelectedOptionIds ?? []).Any(id => value?.ValueKind == JsonValueKind.String &&
                        value.Value.GetString() == id)))
            {
                Add("intrinsic-choice-value-mismatch", candidate.Id,
                    NativeOriginIssueSeverity.Invalid, "Intrinsic value differs from the selected option.");
                continue;
            }

            var attribution = modification ?? (decisionEligible ? permission : null);
            var basis = attribution is null
                ? new NativeOriginDecisionBasis(NativeOriginAdjudication.Supported,
                    baseEligible && proposal.Intent == NativeOriginIntent.Mixed
                        ? candidate.SelectionBasis.RuleReference : source.Id)
                : new NativeOriginDecisionBasis(NativeOriginAdjudication.DmAuthorized,
                    TableDecisionId: attribution.Id);
            var grant = new NativeOriginEffectiveGrant("grant:" + manifest.Id, manifest.Id,
                candidate.Identity, [candidate.Id], value,
                manifest.SelectedParameters ?? candidate.OfferedParameters, choices, basis);
            if (parentGeneOffers.TryGetValue(candidate.Id, out var previousGene))
            {
                // The inherited instance may have been latent in its parent.
                // Previously configured finite choices travel unchanged.
                grant = grant with
                {
                    ResolvedChoices = previousGene.Gene.ResolvedChoices,
                    ParameterSources = previousGene.Gene.Parameters?.Keys.ToDictionary(
                        key => key, _ => candidate.Id, StringComparer.Ordinal)
                };
            }
            if (inheritedOffers.TryGetValue(candidate.Id, out var prior))
            {
                // The original choices and configured mechanics are inherited as
                // a complete manifestation. A descendant does not reopen the
                // ancestor's unchosen option menu or keep the ancestor's active
                // resource-owner IDs as live links.
                grant = grant with
                {
                    ResolvedChoices = prior.Grant.ResolvedChoices,
                    ParameterSources = prior.Grant.Parameters?.Keys.ToDictionary(
                        key => key, _ => candidate.Id, StringComparer.Ordinal),
                    InheritedFrom =
                    [
                        new(prior.ContributorId, prior.ProfileId,
                            prior.ProfileRevision, prior.Grant.Id,
                            prior.Grant.SourceCandidateIds,
                            prior.Grant.ParameterSources, prior.Grant.ResourceOwners)
                    ]
                };
            }
            accepted.Add(new(candidate, manifest, grant));
        }

        var (constrained, constraintIssues, constraintsDm) =
            _constraints.Apply(proposal, accepted, Verified);
        issues.AddRange(constraintIssues);
        usedDm |= constraintsDm;
        if (constraintsDm)
            foreach (var constraint in proposal.Constraints ?? [])
            {
                var decision = Verified("constraint", constraint.Id);
                if (decision is not null &&
                    (constraint.TableDecisionId is null ||
                     decision.Id == constraint.TableDecisionId) &&
                    (ReadBool(decision.EffectiveOutcome, "allow") == true ||
                     ReadBool(decision.EffectiveOutcome, "enforce") == true))
                    verifiedDmDecisionIds.Add(decision.Id);
            }
        var (resolved, reconciliationIssues, reconciliationDm) =
            _reconciler.Reconcile(proposal, constrained, Verified);
        issues.AddRange(reconciliationIssues);
        usedDm |= reconciliationDm;
        // Every binder is monotonic: it can suppress an unsupported dependent,
        // but never create a new grant. A general requirement may remove an
        // otherwise valid resource/choice owner after its dependent was bound.
        // Rerun the canonical binders to a fixed point so a dependent cannot
        // retain a stale owner ID or source choice in the final profile.
        IReadOnlyList<NativeOriginEffectiveGrant> effective = resolved;
        while (true)
        {
            // Rebind from immutable reconciled input, not the previous pass's
            // derived Parameters. Otherwise an auto-bound intrinsic parameter
            // can appear inherited when its actual owner was just suppressed.
            // Only grant IDs surviving the last pass may be reconsidered.
            var survivingIds = effective.Select(x => x.Id)
                .ToHashSet(StringComparer.Ordinal);
            var inputs = resolved.Where(x => survivingIds.Contains(x.Id)).ToArray();
            var previousCount = inputs.Length;
            var (bound, bindingIssues) =
                NativeOriginDependentBinder.Bind(catalog, proposal, inputs);
            issues.AddRange(bindingIssues);
            var (resourceBound, resourceIssues) =
                NativeOriginResourceDependencyBinder.Bind(catalog, proposal, bound);
            issues.AddRange(resourceIssues);
            var (linkedGrants, linkedIssues) =
                _nativeLinks.VerifyEffectiveLinks(proposal, resourceBound, Verified);
            issues.AddRange(linkedIssues);
            var (closedGrants, closureIssues) =
                _constraints.VerifyEffectiveRequirements(proposal, linkedGrants, Verified);
            issues.AddRange(closureIssues);
            effective = closedGrants;
            if (effective.Count == previousCount)
                break;
        }
        resolved = effective;
        // Derive only from the final, already reconciled and dependency-closed
        // effective set. Derived results are rebuilt, not copied from old state.
        // Table-authored definitions use the same grammar as native rules, but
        // can only enter evaluation after the existing authenticated policy
        // verifies their complete decision against the trusted scope. A serialized
        // table claim never authorizes itself. Ambiguous duplicate targets fail
        // closed rather than minting alternate results from competing rulings.
        var verifiedTableSynergies = new List<NativeTableRuleDecision>();
        var tableTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var decision in proposal.TableDecisions ?? [])
        {
            if (decision?.Target?.Kind != "origin-synergy") continue;
            if (string.IsNullOrWhiteSpace(decision.Id) ||
                string.IsNullOrWhiteSpace(decision.Target.Id) ||
                !tableTargets.Add(decision.Target.Id))
            {
                Add("synergy-table-decision-conflict", "tableDecisions",
                    NativeOriginIssueSeverity.Invalid,
                    "Table-authored interactions require distinct, nonempty targets.");
                continue;
            }
            // Structural validation is advisory and may already have
            // reported a malformed record. Check the untrusted authority
            // shape before calling host policy, which may dereference it.
            if (decision.Authority is null ||
                string.IsNullOrWhiteSpace(decision.Authority.ActorId) ||
                string.IsNullOrWhiteSpace(decision.Authority.AuthorizationEvidenceId) ||
                authority is null || request.TrustedScope is null ||
                !ScopeMatches(decision.Scope, request.TrustedScope, proposal.Id) ||
                !authority.Authorizes(decision, request.TrustedScope))
            {
                Add("synergy-table-decision-unverified", "tableDecisions." + decision.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    "A table-authored interaction requires a verified, scoped ruling.");
                continue;
            }
            verifiedTableSynergies.Add(decision);
        }
        var (derived, synergyIssues) = _synergies.Evaluate(proposal, resolved,
            verifiedTableSynergies);
        issues.AddRange(synergyIssues);
        foreach (var grant in derived.Where(x =>
            x.Authorization?.Adjudication == NativeOriginAdjudication.DmAuthorized &&
            x.Authorization.TableDecisionId is not null))
        {
            usedDm = true;
            verifiedDmDecisionIds.Add(grant.Authorization!.TableDecisionId!);
        }
        resolved = resolved.Concat(derived).ToArray();
        var (inheritedGenes, geneIssues, _, _) = _inheritance.Evaluate(
            proposal, transmission, resolved, verifiedManifested,
            request.TrustedScope is null ? null : authority, Verified);
        issues.AddRange(geneIssues);
        if (Verified("biological-inheritance", proposal.Id) is { } capacityDecision)
        {
            usedDm = true;
            verifiedDmDecisionIds.Add(capacityDecision.Id);
        }
        // A partial set of inherited biological traits is a useful design
        // preview, but it is not a complete playable character. Require one
        // selected, reconciled value in each universal physical foundation slot
        // when the native biological policy issued any first-party selections.
        if (proposal.Intent == NativeOriginIntent.Mixed &&
            accepted.Any(x => x.Candidate.SelectionBasis.Eligibility ==
                NativeOriginEligibility.Eligible &&
                x.Candidate.SelectionBasis.RuleReference == "rule.creature-origination"))
            foreach (var slot in new[] { "creatureType", "size", "walkingSpeedFeet" })
                if (resolved.Count(x => x.Identity.IntrinsicSlotId == slot) != 1)
                    Add("biological-foundation-incomplete", "intrinsics." + slot,
                        NativeOriginIssueSeverity.Incomplete,
                        "A complete player-selected biological Origin needs one effective value for this intrinsic slot.");
        if (reconciliationDm)
            foreach (var resolution in proposal.Resolutions ?? [])
            {
                var decision = Verified("relationship", resolution.RelationshipId);
                if (decision is not null &&
                    decision.Id == resolution.Basis?.TableDecisionId)
                    verifiedDmDecisionIds.Add(decision.Id);
            }

        // Phase 2 does not execute permanent or temporary form transitions. A
        // supplied baseline remains authoritative, and the active effective form
        // remains separate. Reconstructed grants are not unioned into a form.
        if (proposal.Intent == NativeOriginIntent.Transformation && manifests.Count > 0)
            Add("transformation-execution-deferred", "intent", NativeOriginIssueSeverity.Unresolved,
                "The form transition or permanent reconstruction needs its later executable policy.");
        if (proposal.ExistingConfiguration is not null && resolved.Count > 0)
            Add("baseline-replacement-deferred", "existingConfiguration",
                NativeOriginIssueSeverity.Unresolved,
                "A current baseline cannot be overwritten by an unexecuted reconstruction.");

        var configure = Verified("configuration", proposal.Id);
        var configuredSpecies = proposal.Intent == NativeOriginIntent.Direct ?
            proposal.SelectedSpeciesId : null;
        var configuredAncestry = proposal.Intent == NativeOriginIntent.Direct ?
            proposal.SelectedAncestryId : null;
        if (configure is not null)
        {
            var outcome = configure.EffectiveOutcome;
            if (outcome.ValueKind == JsonValueKind.Object &&
                (outcome.TryGetProperty("speciesId", out _) ||
                 outcome.TryGetProperty("ancestryId", out _)))
            {
                configuredSpecies = JsonString(outcome, "speciesId");
                configuredAncestry = JsonString(outcome, "ancestryId");
                usedDm = true;
                verifiedDmDecisionIds.Add(configure.Id);
            }
        }

        var profileId = "origin:" + proposal.Id;
        var underlying = proposal.ExistingConfiguration ??
            new NativeOriginCurrentConfiguration(configuredSpecies, configuredAncestry,
                resolved, profileId, transmission.Explicit ? inheritedGenes : null);
        var current = proposal.ExistingEffectiveConfiguration ?? underlying;
        var history = (proposal.History ?? []).ToList();
        var addedConstructionEvent = !history.Any(x => x.Id == "construction:" + proposal.Id);
        if (addedConstructionEvent)
            history.Add(new("construction:" + proposal.Id, proposal.Method,
                NativeOriginEventStage.Pending,
                (proposal.Contributors ?? []).Select(x => x.Id)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                false, ResultingProfileId: profileId));

        NativeOriginConstructionStatus Status()
        {
            if (issues.Any(x => x.Severity == NativeOriginIssueSeverity.Invalid))
                return NativeOriginConstructionStatus.Invalid;
            if (issues.Any(x => x.Severity == NativeOriginIssueSeverity.Conflict))
                return NativeOriginConstructionStatus.Conflicting;
            if (issues.Any(x => x.Severity == NativeOriginIssueSeverity.Incomplete))
                return NativeOriginConstructionStatus.Incomplete;
            if (issues.Any(x => x.Severity == NativeOriginIssueSeverity.Unresolved))
                return NativeOriginConstructionStatus.Unresolved;
            return NativeOriginConstructionStatus.Authorized;
        }

        var preliminary = Status();
        var adjudication = preliminary == NativeOriginConstructionStatus.Authorized
            ? new NativeOriginDecisionBasis(usedDm ? NativeOriginAdjudication.DmAuthorized :
                NativeOriginAdjudication.Supported,
                usedDm ? null : proposal.Method.RuleId,
                usedDm ? verifiedDmDecisionIds.FirstOrDefault() : null)
            : new NativeOriginDecisionBasis(preliminary == NativeOriginConstructionStatus.Conflicting ?
                NativeOriginAdjudication.Conflict : NativeOriginAdjudication.Unresolved);
        // If multiple independent rulings apply, the profile-level basis names one
        // anchor; each grant and disposition retains its exact individual decision.
        var profile = new NativeOriginProfile(profileId, underlying, current,
            request.ExistingIndependentGrants ?? [], history, adjudication, proposal.Id,
            request.PersistentInfluences, proposal.ActiveTemporaryTransformation,
            proposal.OtherActiveTemporaryTransformations,
            proposal.TemporaryTransformationPrecedence,
            proposal.ExistingEffectiveConfigurationBasis,
            request.IndependentGrantApplicability);

        // Previously acquired baseline grants belong to an earlier construction proposal.
        // Do not demand that their original manifestations appear in this proposal.
        var profileReview = _validator.ReviewProfileVerified(profile,
            proposal.ExistingConfiguration is null ? proposal : null,
            verifiedTableSynergies);
        issues.AddRange(profileReview.Issues.Where(x =>
            x.Code is not ("profile-outcome-unresolved" or "profile-outcome-conflict")));
        var status = Status();
        // An evaluation preview must not claim an Origin event already happened.
        // Only an authorized result promotes this evaluation's event to completed.
        if (addedConstructionEvent && status == NativeOriginConstructionStatus.Authorized)
            profile = profile with { History = history.Select(x =>
                x.Id == "construction:" + proposal.Id
                    ? x with { Stage = NativeOriginEventStage.Completed } : x).ToArray() };
        var outcome = new NativeOriginConstructionResult(status, proposal,
            status == NativeOriginConstructionStatus.Invalid ? null : profile,
            issues.OrderBy(x => x.Path, StringComparer.Ordinal)
                .ThenBy(x => x.Code, StringComparer.Ordinal).Distinct().ToArray());
        return outcome with
        {
            BiologicalCombination = NativeOriginBiologicalCombinationProjector.Build(outcome)
        };

        void Add(string code, string path, NativeOriginIssueSeverity severity, string detail) =>
            issues.Add(new(code, path, severity, detail));
    }

    private static bool ScopeMatches(NativeTableDecisionScope source,
        NativeTableDecisionScope trusted, string proposalId) =>
        source is not null &&
        ((source.Kind == trusted.Kind && source.Id == trusted.Id) ||
         (source.Kind == "proposal" && source.Id == proposalId));

    private static bool UnselectedOfferIssue(NativeOriginContractIssue issue, HashSet<string> selected)
    {
        if (issue.Code is not ("candidate-eligibility-unresolved" or
            "candidate-rule-evidence-required" or "unresolved-source-evidence" or
            "choice-catalog-unresolved" or "choice-timing-unresolved")) return false;
        return !selected.Any(id => issue.Path.StartsWith("candidates." + id,
            StringComparison.Ordinal));
    }

    private static bool? ReadBool(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(key, out var result) &&
        result.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? result.GetBoolean() : null;

    private static bool SameJson(JsonElement x, JsonElement y) =>
        JsonElement.DeepEquals(x, y);

    private static string? JsonString(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(key, out var result) &&
        result.ValueKind == JsonValueKind.String ? result.GetString() : null;

    private static bool MatchingParameters(JsonElement outcome,
        IReadOnlyDictionary<string, JsonElement> selected)
    {
        if (outcome.ValueKind != JsonValueKind.Object ||
            !outcome.TryGetProperty("parameters", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Object) return false;
        return selected.All(x => parameters.TryGetProperty(x.Key, out var actual) &&
            SameJson(x.Value, actual));
    }
}
