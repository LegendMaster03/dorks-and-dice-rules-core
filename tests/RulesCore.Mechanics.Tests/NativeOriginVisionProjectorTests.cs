using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

public sealed class NativeOriginVisionProjectorTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var rulesets = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(rulesets, "first-party-001", "native",
                    "rules", "low-light-vision.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(rulesets, "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Native vision catalog is unavailable.");
    }

    private static NativeOriginConstructionResult Evaluate(
        NativeCatalogSnapshot catalog, string species) =>
        new NativeOriginFirstPartyChoiceEvaluator(catalog).Evaluate(
            new NativeOriginProposal("vision-test", NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"), [], [], [],
                SelectedSpeciesId: species));

    [Theory]
    [InlineData("species.orc")]
    [InlineData("species.elf")]
    [InlineData("species.gnome")]
    public void AuthoredLowLightVisionMapsOnlyDimToBright(string speciesId)
    {
        var catalog = Catalog();
        var result = Evaluate(catalog, speciesId);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var view = NativeOriginVisionProjector.Project(catalog, result);
        Assert.Equal("resolved", view.Status);
        Assert.True(view.PreviewOnly);
        Assert.Empty(view.UnresolvedReasons);
        var sense = Assert.IsType<NativeOriginVisionProjector.GrantedVision>(
            view.LowLightVision);
        Assert.Equal(speciesId, sense.SourceId);
        Assert.StartsWith("grant:", sense.GrantId);
        Assert.Equal("rule.low-light-vision", sense.RuleId);
        Assert.Equal("dim", sense.ActualLightLevel);
        Assert.Equal("bright", sense.PerceivedLightLevel);
        Assert.StartsWith("/traits/", sense.OccurrencePath);
        Assert.Equal("bright",
            NativeOriginVisionProjector.Perceive(view, "dim").PerceivedLightLevel);
        Assert.Equal("bright",
            NativeOriginVisionProjector.Perceive(view, "bright").PerceivedLightLevel);
        Assert.Equal("darkness",
            NativeOriginVisionProjector.Perceive(view, "darkness").PerceivedLightLevel);
        Assert.Equal("unresolved",
            NativeOriginVisionProjector.Perceive(view, "magical-darkness").Status);
    }

    [Fact]
    public void SpeciesWithoutLowLightDoesNotAccidentallyAcquireIt()
    {
        var catalog = Catalog();
        var dwarf = Evaluate(catalog, "species.dwarf");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, dwarf.Status);
        var view = NativeOriginVisionProjector.Project(catalog, dwarf);
        Assert.Equal("none", view.Status);
        Assert.Null(view.LowLightVision);
        Assert.Equal("dim",
            NativeOriginVisionProjector.Perceive(view, "dim").PerceivedLightLevel);
    }

    [Fact]
    public void IncompleteOrUnresolvedOriginDoesNotHaveExecutableVision()
    {
        var catalog = Catalog();
        var incomplete = Evaluate(catalog, "species.human");
        Assert.NotEqual(NativeOriginConstructionStatus.Authorized, incomplete.Status);
        var view = NativeOriginVisionProjector.Project(catalog, incomplete);
        Assert.Equal("unresolved", view.Status);
        Assert.Null(view.LowLightVision);
        Assert.Equal("unresolved",
            NativeOriginVisionProjector.Perceive(view, "dim").Status);
    }

    [Fact]
    public void ForgedOrDuplicatedLowLightGrantCannotYieldAnActiveSense()
    {
        var catalog = Catalog();
        var original = Evaluate(catalog, "species.orc");
        var profile = original.Profile!;
        var sense = profile.Current.InherentGrants.Single(grant =>
            grant.Identity.MechanicalDefinitionId?.EndsWith(
                "#trait:low-light-vision", StringComparison.Ordinal) == true);

        var duplicated = profile.Current with
        {
            InherentGrants = profile.Current.InherentGrants
                .Append(sense with { Id = "grant:injected" }).ToArray()
        };
        var duplicate = original with
        {
            Profile = profile with { Underlying = duplicated, Current = duplicated }
        };
        Assert.Equal("unresolved",
            NativeOriginVisionProjector.Project(catalog, duplicate).Status);

        var injected = sense with { SourceCandidateIds = ["unoffered-candidate"] };
        var replaced = profile.Current with
        {
            InherentGrants = profile.Current.InherentGrants
                .Select(grant => grant.Id == sense.Id ? injected : grant).ToArray()
        };
        var poisoned = original with
        {
            Profile = profile with { Underlying = replaced, Current = replaced }
        };
        Assert.Equal("unresolved",
            NativeOriginVisionProjector.Project(catalog, poisoned).Status);
    }

    [Fact]
    public void ModifiedOrUnapprovedVisionEffectsCannotBeInterpreted()
    {
        var catalog = Catalog();
        var original = Evaluate(catalog, "species.orc");
        var profile = original.Profile!;
        var sense = profile.Current.InherentGrants.Single(grant =>
            grant.Identity.MechanicalDefinitionId?.EndsWith(
                "#trait:low-light-vision", StringComparison.Ordinal) == true);

        var originalJson = sense.EffectiveValue!.Value;
        var modified = JsonNode.Parse(originalJson.GetRawText())!;
        modified["rule"] = "rule.unapproved";
        var replacement = sense with {
            EffectiveValue = JsonSerializer.SerializeToElement(modified)
        };
        var config = profile.Current with
        {
            InherentGrants = profile.Current.InherentGrants
                .Select(x => x.Id == sense.Id ? replacement : x).ToArray()
        };
        var result = original with
        {
            Profile = profile with { Underlying = config, Current = config }
        };
        Assert.Equal("unresolved",
            NativeOriginVisionProjector.Project(catalog, result).Status);
    }
}
