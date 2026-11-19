using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>Reads real selected-native evidence; never converts source labels into grants.</summary>
public sealed class NativeOriginIntrinsicProjectorTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var rulesets = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(rulesets, "first-party-001", "native",
                    "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(rulesets, "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Native catalog is unavailable.");
    }

    private static NativeOriginConstructionResult Evaluate(
        string? species, NativeOriginIntent intent = NativeOriginIntent.Direct,
        IReadOnlyList<NativeOriginContributor>? contributors = null)
    {
        var proposal = new NativeOriginProposal("intrinsic-test", intent,
            new("rule.creature-origination", "biological"),
            contributors ?? [], [], [], species);
        return new NativeOriginFirstPartyChoiceEvaluator(Catalog()).Evaluate(proposal);
    }

    [Fact]
    public void DirectOrcProjectsOnlyAuthorizedNativeIntrinsicValuesWithOccurrenceProvenance()
    {
        var result = Evaluate("species.orc");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var intrinsics = NativeOriginIntrinsicProjector.Project(result);
        Assert.Equal("resolved", intrinsics.Status);
        Assert.True(intrinsics.PreviewOnly);
        Assert.Empty(intrinsics.UnresolvedSlots);
        Assert.Equal("humanoid", intrinsics.CreatureType!.Value);
        Assert.Equal("medium", intrinsics.Size!.Value);
        Assert.Equal(30, intrinsics.WalkingSpeedFeet!.Value);
        foreach (var source in new[] {
            intrinsics.CreatureType.SourceId,
            intrinsics.Size.SourceId,
            intrinsics.WalkingSpeedFeet.SourceId
        })
            Assert.Equal("species.orc", source);
        Assert.Equal("/walkingSpeedFeet", intrinsics.WalkingSpeedFeet.OccurrencePath);
        Assert.StartsWith("grant:", intrinsics.WalkingSpeedFeet.GrantId);
    }

    [Fact]
    public void IncompleteHumanDoesNotAcquireItsPartiallyOfferedIntrinsicGrants()
    {
        var result = Evaluate("species.human");
        Assert.Equal(NativeOriginConstructionStatus.Incomplete, result.Status);
        var intrinsics = NativeOriginIntrinsicProjector.Project(result);
        Assert.Equal("unresolved", intrinsics.Status);
        Assert.Null(intrinsics.CreatureType);
        Assert.Null(intrinsics.Size);
        Assert.Null(intrinsics.WalkingSpeedFeet);
        Assert.Contains("origin-not-a-supported-initial-direct-profile",
            intrinsics.UnresolvedSlots);
    }

    [Fact]
    public void MixedSourcesNeverBecomeAResolvedIntrinsicOrAuthorizedParent()
    {
        var result = Evaluate(null, NativeOriginIntent.Mixed,
            [
                new("parent-one", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.human")),
                new("parent-two", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf"))
            ]);
        var intrinsics = NativeOriginIntrinsicProjector.Project(result);
        Assert.Equal("unresolved", intrinsics.Status);
        Assert.Null(intrinsics.WalkingSpeedFeet);
    }

    [Fact]
    public void DuplicateOrUnattributedIntrinsicFailsClosed()
    {
        var original = Evaluate("species.orc");
        var profile = original.Profile!;
        var speed = profile.Current.InherentGrants.Single(grant =>
            grant.Identity.IntrinsicSlotId == "walkingSpeedFeet");
        var duplicate = profile.Underlying with
        {
            InherentGrants = profile.Current.InherentGrants
                .Append(speed with { Id = "forged-duplicate" }).ToArray()
        };
        var result = original with {
            Profile = profile with { Underlying = duplicate, Current = duplicate }
        };
        var intrinsics = NativeOriginIntrinsicProjector.Project(result);
        Assert.Equal("unresolved", intrinsics.Status);
        Assert.Null(intrinsics.Size);
        Assert.Null(intrinsics.WalkingSpeedFeet);
        Assert.Contains(intrinsics.UnresolvedSlots, entry =>
            entry == "unsupported-or-conflicting-intrinsic:walkingSpeedFeet");

        var bad = speed with {
            SourceCandidateIds = ["unoffered-candidate"]
        };
        var invalidConfig = profile.Underlying with {
            InherentGrants = profile.Current.InherentGrants
                .Select(grant => grant.Id == speed.Id ? bad : grant).ToArray()
        };
        var badResult = original with {
            Profile = profile with {
                Underlying = invalidConfig, Current = invalidConfig
            }
        };
        Assert.Equal("unresolved",
            NativeOriginIntrinsicProjector.Project(badResult).Status);
    }

    [Fact]
    public void ChangedActiveFormIsNotAnAuthorizedDirectIntrinsicBaseline()
    {
        var original = Evaluate("species.orc");
        var profile = original.Profile!;
        var form = profile.Current with { SpeciesId = "species.werewolf" };
        var result = original with {
            Profile = profile with { Current = form }
        };
        var projected = NativeOriginIntrinsicProjector.Project(result);
        Assert.Equal("unresolved", projected.Status);
        Assert.Null(projected.WalkingSpeedFeet);
    }
}
