using RulesCore.Content;

namespace RulesCore.Mechanics;

/// <summary>
/// Application-supplied, authenticated current state for a previously constructed
/// contributor. The provider must check access, record ownership and current
/// revision before returning the profile. Tests may supply a self-override fixture
/// through this interface without exposing an API or weakening default admission.
/// </summary>
public interface INativeOriginManifestedContributorAuthority
{
    NativeOriginVerifiedManifestedContributor? ResolveCurrent(
        NativeOriginSourceReference source, NativeTableDecisionScope trustedScope);
}

/// <summary>
/// The revision identifies the stored profile snapshot that was verified.
/// Historical genealogy and caller-supplied form snapshots are not eligible sources.
/// </summary>
public sealed record NativeOriginVerifiedManifestedContributor(
    NativeOriginProfile Profile, string Revision);

/// <summary>
/// Replaces untrusted nested snapshot claims with the verified UNDERLYING
/// configuration, not Current (which may be a temporary form). No resolver is
/// necessary for conventional/native donors or for the legacy table-decision path.
/// </summary>
internal static class NativeOriginManifestedContributorVerifier
{
    public static (NativeOriginProposal Proposal,
        IReadOnlySet<string> VerifiedContributorIds,
        IReadOnlyList<NativeOriginContractIssue> Issues) Resolve(
        NativeOriginProposal proposal, NativeTableDecisionScope? trustedScope,
        INativeOriginManifestedContributorAuthority? authority)
    {
        var verified = new HashSet<string>(StringComparer.Ordinal);
        var issues = new List<NativeOriginContractIssue>();
        if (authority is null) return (proposal, verified, issues);

        var contributors = new List<NativeOriginContributor>();
        foreach (var contributor in proposal.Contributors ?? [])
        {
            if (contributor?.Source?.Kind != NativeOriginSourceKind.ManifestedProfile)
            {
                contributors.Add(contributor!);
                continue;
            }

            var source = contributor.Source;
            var record = trustedScope is null ? null :
                authority.ResolveCurrent(source, trustedScope);
            if (record is null || string.IsNullOrWhiteSpace(record.Revision) ||
                string.IsNullOrWhiteSpace(record.Profile?.Id) ||
                record.Profile.Id != source.Id ||
                source.Version is not null && source.Version != record.Revision ||
                record.Profile.Underlying?.InherentGrants is null ||
                record.Profile.Adjudication?.Adjudication is not
                    (NativeOriginAdjudication.Supported or NativeOriginAdjudication.DmAuthorized) ||
                record.Profile.Underlying.InherentGrants.Any(grant =>
                    grant is null || grant.Identity is null ||
                    grant.Authorization?.Adjudication is not
                        (NativeOriginAdjudication.Supported or NativeOriginAdjudication.DmAuthorized)))
            {
                // An invalid trusted lookup must not fall back to a caller-supplied
                // snapshot. Its absence cannot manufacture any heritable offers.
                contributors.Add(contributor with { ManifestedProfile = null });
                issues.Add(new("manifested-contributor-unverified",
                    "contributors." + contributor.Id,
                    NativeOriginIssueSeverity.Unresolved,
                    "Current underlying inherent grants require authenticated profile identity, revision and authorization."));
                continue;
            }

            verified.Add(contributor.Id);
            contributors.Add(contributor with
            {
                ManifestedProfile = record.Profile.Underlying
            });
        }
        return (proposal with { Contributors = contributors }, verified, issues);
    }
}
