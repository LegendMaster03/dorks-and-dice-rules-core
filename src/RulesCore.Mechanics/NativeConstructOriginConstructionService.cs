using System.Text.Json;
using System.Text.Json.Serialization;
using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Adapts catalog-validated Construct assembly instances to the existing
/// universal Origin construction pipeline. It never accepts client-provided
/// native trait assertions as authority or issues its own mechanical grants.
/// </summary>
public sealed class NativeConstructOriginConstructionService(NativeCatalogSnapshot catalog)
{
    private readonly NativeConstructAssemblyEvaluator _assembly = new(catalog);
    private readonly NativeOriginConstructionEngine _origins = new(catalog);

    public NativeConstructOriginConstructionResult Evaluate(
        NativeConstructOriginConstructionRequest request,
        INativeOriginAuthority? authority = null,
        INativeOriginManifestedContributorAuthority? manifestedContributorAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Origin);
        ArgumentNullException.ThrowIfNull(request.Origin.Proposal);
        ArgumentNullException.ThrowIfNull(request.Assembly);

        var assembly = _assembly.Evaluate(request.Assembly);
        var proposal = request.Origin.Proposal;
        if (proposal.Method?.RuleId != "rule.creature-origination" ||
            proposal.Method.MethodId != "construct-assembly")
            return Stop(NativeOriginConstructionStatus.Invalid, assembly, proposal,
                [new("construct-origin-method-mismatch", "method",
                    NativeOriginIssueSeverity.Invalid,
                    "Construct assembly may only issue offers under the authored Construct Assembly method.")]);

        // Gameplay modifications may be previewed without a starting budget,
        // but require separately authenticated acquisition/installation rules
        // before they can permanently change an existing Origin profile.
        if (!request.Assembly.InitialConstruction)
            return Stop(assembly.Status == NativeOriginConstructionStatus.Invalid
                    ? NativeOriginConstructionStatus.Invalid
                    : NativeOriginConstructionStatus.Incomplete,
                assembly, proposal, assembly.Issues.Concat(
                [new NativeOriginContractIssue("construct-modification-authorization-deferred",
                    "assembly.initialConstruction", NativeOriginIssueSeverity.Incomplete,
                    "A gameplay modification preview cannot issue first-party Origin grants.")])
                    .ToArray());

        // Never treat a client-provided allowance as proof of playable starting
        // capacity. The exact budget must be native authored for this method.
        var method = new NativeOriginContractValidator(catalog).FindMethod(proposal.Method);
        if (method?.AuthoredDefinition.TryGetProperty("initialConstructionAllowance",
                out var allowance) != true ||
            allowance.ValueKind != JsonValueKind.Number ||
            !allowance.TryGetInt32(out var permitted) || permitted < 0)
            return Stop(NativeOriginConstructionStatus.Unresolved, assembly, proposal,
                [new("construct-starting-allowance-unresolved", "method",
                    NativeOriginIssueSeverity.Unresolved,
                    "No native first-party initial construction allowance has been authored.")]);
        if (request.Assembly.InitialAllowance != permitted)
            return Stop(NativeOriginConstructionStatus.Invalid, assembly, proposal,
                [new("construct-starting-allowance-unverified", "assembly.initialAllowance",
                    NativeOriginIssueSeverity.Invalid,
                    "The proposed starting allowance does not match the authorized native rule.")]);

        // Only a coherent and fully costed assembly may promote its native
        // component references into candidate Origin characteristics. A partial
        // draft remains useful to the assembly UI but is not authorization.
        if (!assembly.IsAuthorized)
            return Stop(assembly.Status, assembly, proposal, assembly.Issues);

        var issues = new List<NativeOriginContractIssue>();
        var offers = new List<NativeOriginCandidate>();
        foreach (var instance in request.Assembly.Components
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal))
        {
            // The assembly evaluator already verified every component identity
            // and definition. Resolve the source again rather than trusting any
            // serialized candidate, value, or purported entitlement.
            if (!catalog.TryGetById(instance.DefinitionId, out var component) ||
                component?.Category != "construct-component")
            {
                issues.Add(new("construct-origin-source-invalid", instance.InstanceId,
                    NativeOriginIssueSeverity.Invalid,
                    "An Origin component must resolve to a native Construct definition."));
                continue;
            }
            var definition = component.Definition;
            foreach (var field in new[] { "installedOriginTraitIds", "operationalOriginTraitIds" })
            {
            if (!definition.TryGetProperty(field, out var traits))
                continue;
            if (traits.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new("construct-origin-traits-invalid", component.Id,
                    NativeOriginIssueSeverity.Invalid,
                    "Installed Origin traits must be an array of canonical trait IDs."));
                continue;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var raw in traits.EnumerateArray())
            {
                var item = index++;
                var referencePath = "components." + instance.InstanceId +
                    "." + field + "." + item;
                var id = raw.ValueKind == JsonValueKind.String ? raw.GetString() : null;
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id) ||
                    !catalog.TryGetById(id, out var nativeTrait) ||
                    nativeTrait?.Category != "origin-trait" ||
                    !nativeTrait.Definition.TryGetProperty("trait", out var canonical) ||
                    canonical.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new("construct-origin-trait-reference-invalid", referencePath,
                        NativeOriginIssueSeverity.Invalid,
                        "Each installed trait must reference a distinct, canonical native origin-trait."));
                    continue;
                }

                // The installed trait is a permanent entitlement, not necessarily
                // an active effect. The separately evaluated operating session gates
                // operationalOriginTraitIds without rewriting the Origin baseline.
                // Choice-bearing traits still need explicit selection/binding.
                if (canonical.TryGetProperty("originBindings", out _) ||
                    canonical.TryGetProperty("choices", out _) ||
                    canonical.TryGetProperty("chooseFrom", out _) ||
                    canonical.TryGetProperty("abilityChoice", out _) ||
                    canonical.TryGetProperty("choiceCount", out _) ||
                    canonical.TryGetProperty("spellList", out _) ||
                    canonical.TryGetProperty("sharedUsesFromTrait", out _))
                {
                    issues.Add(new("construct-origin-trait-binding-deferred", referencePath,
                        NativeOriginIssueSeverity.Unresolved,
                        "A configured or resource-dependent trait requires explicit authoring support."));
                    continue;
                }
                if (!canonical.EnumerateObject().Any(field =>
                    field.Name is not ("id" or "name" or "description" or "reviewNotes")))
                {
                    issues.Add(new("construct-origin-trait-mechanics-unresolved", referencePath,
                        NativeOriginIssueSeverity.Unresolved,
                        "A description alone cannot authorize a mechanical characteristic."));
                    continue;
                }
                // A powered part can remain installed and manifest the entitlement
                // even while disabled. Activation is a runtime operating decision.
                var source = new NativeOriginSourceReference(NativeOriginSourceKind.Native,
                    component.Id);
                var occurrence = new NativeOriginSourceOccurrence(source,
                    "/" + field + "/" + item + "/instance/" + instance.InstanceId);
                var candidateId = "construct:" + instance.InstanceId +
                    (field == "installedOriginTraitIds" ? ":trait:" : ":operational-trait:") +
                    item;
                offers.Add(new NativeOriginCandidate(candidateId,
                    new(NativeOriginCharacteristicKind.Trait, occurrence, nativeTrait.Id),
                    new(NativeOriginEligibility.Eligible,
                        RuleReference: "rule.creature-origination",
                        Explanation: "Catalog-authored installed trait of a Construct component."),
                    OfferedValue: canonical.Clone()));
            }
            }
        }

        if (issues.Count > 0)
        {
            var status = issues.Any(x => x.Severity == NativeOriginIssueSeverity.Invalid)
                ? NativeOriginConstructionStatus.Invalid
                : NativeOriginConstructionStatus.Unresolved;
            return Stop(status, assembly, proposal, issues);
        }

        // Source deconstruction re-verifies every client-supplied offer against
        // the trusted list. The existing engine handles manifestation,
        // authorization, reconciliation, dependency closure and synergy.
        var origin = _origins.EvaluateWithTrustedNativeOffers(request.Origin,
            offers, authority, manifestedContributorAuthority);

        // A custom assembly without a traditional Species may be drafted, but
        // it still needs an effective creature type and game-size classification.
        // A process type is eligibility evidence, not an inherent type grant.
        if (origin.Status == NativeOriginConstructionStatus.Authorized &&
            proposal.Intent == NativeOriginIntent.Custom &&
            origin.Profile is { } profile &&
            (profile.Underlying.InherentGrants.Count(grant =>
                grant.Identity.IntrinsicSlotId == "creatureType") != 1 ||
             profile.Underlying.InherentGrants.Count(grant =>
                grant.Identity.IntrinsicSlotId == "size") != 1))
        {
            var incomplete = new NativeOriginContractIssue(
                "construct-foundation-incomplete", "origin.intrinsics",
                NativeOriginIssueSeverity.Incomplete,
                "A custom Construct Origin must resolve creature type and game size through an authorized source or table rule.");
            var pending = profile.History.Select(x =>
                x.Id == "construction:" + proposal.Id
                    ? x with { Stage = NativeOriginEventStage.Pending } : x).ToArray();
            origin = origin with
            {
                Status = NativeOriginConstructionStatus.Incomplete,
                Profile = profile with
                {
                    History = pending,
                    Adjudication = new(NativeOriginAdjudication.Unresolved)
                },
                Issues = origin.Issues.Concat([incomplete]).ToArray()
            };
        }
        return origin.IsMechanicallyAuthorized && origin.Profile is not null
            ? new(assembly, origin)
            {
                OperatingSession = new NativeConstructOperatingSession(
                    catalog, request.Assembly, origin.Profile)
            }
            : new(assembly, origin);
    }

    private static NativeConstructOriginConstructionResult Stop(
        NativeOriginConstructionStatus status, NativeConstructAssemblyResult assembly,
        NativeOriginProposal proposal, IReadOnlyList<NativeOriginContractIssue> issues) =>
        new(assembly, new(status, proposal, null, issues));
}

public sealed record NativeConstructOriginConstructionRequest(
    NativeOriginConstructionRequest Origin, NativeConstructAssemblyRequest Assembly);

/// <summary>
/// Permanent Origin grants and current Construct operating state are separate.
/// Consumers must honor both statuses before applying gameplay effects.
/// </summary>
public sealed record NativeConstructOriginConstructionResult(
    NativeConstructAssemblyResult Assembly, NativeOriginConstructionResult Origin)
{
    // The session holds a server-verified immutable construction snapshot, not
    // serialized client state or a substitute for a persisted authorized Origin.
    [JsonIgnore]
    public NativeConstructOperatingSession? OperatingSession { get; init; }

    public bool IsMechanicallyAuthorized =>
        Assembly.IsAuthorized && Origin.IsMechanicallyAuthorized;
}
