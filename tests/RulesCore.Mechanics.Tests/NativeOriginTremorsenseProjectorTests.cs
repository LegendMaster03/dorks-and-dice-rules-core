using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// One canonical activated Tremorsense gene has different verified contact
/// configurations for Dwarves and Rock Gnomes.
/// </summary>
public sealed class NativeOriginTremorsenseProjectorTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null)
        {
            var root = Path.Combine(folder.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(
                    NativeRulesetLayout.Resolve(root, "first-party-001"));
            folder = folder.Parent;
        }
        throw new InvalidOperationException("Native catalog unavailable.");
    }

    private static NativeOriginConstructionResult Direct(string source) =>
        source == "ancestry.gnome.rock"
            ? NativeOriginGnomeFadeAwayPricingTests.Direct(Catalog(), "rock")
            : new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(new NativeOriginProposal("direct-" + source,
                NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"),
                [], [], [],
                SelectedSpeciesId: source.StartsWith("species.", StringComparison.Ordinal) ? source : null,
                SelectedAncestryId: source.StartsWith("ancestry.", StringComparison.Ordinal) ? source : null),
                AutoManifestDirectDefaults: true));

    [Theory]
    [InlineData("species.dwarf")]
    [InlineData("ancestry.gnome.rock")]
    public void BothNativeSourcesGrantTheirConfiguredActivatedSense(string source)
    {
        var catalog = Catalog();
        var completed = Direct(source);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var projected = NativeOriginTremorsenseProjector.Project(catalog, completed);
        Assert.Equal("resolved", projected.Status);
        var sense = Assert.Single(projected.Senses);
        Assert.Equal("trait.stonebound-tremorsense", sense.DefinitionId);
        Assert.Equal(source, sense.SourceId);
        Assert.Equal(60, sense.RangeFeet);
        Assert.Equal("bonus-action", sense.Activation);
        var rock = source == "ancestry.gnome.rock";
        Assert.Equal(rock ? "earth-or-stone" : "stone", sense.RequiresContact);
        Assert.Equal(rock ? new[] { "earth", "stone" } : new[] { "stone" },
            sense.ContactMaterials);
        Assert.Empty(sense.RequiresSharedContact);
        Assert.Equal(10, sense.DurationMinutes);
        Assert.Equal("proficiency-bonus", sense.UsesFormula);
        Assert.Equal("long-rest", sense.Recharge);
        Assert.Equal(sense.GrantId, sense.ResourcePoolOwnerGrantId);
        var grant = Assert.Single(completed.Profile!.Underlying.InherentGrants,
            x => x.Id == sense.GrantId);
        Assert.Equal(grant.SourceCandidateIds, sense.SourceCandidateIds);
        Assert.True(JsonElement.DeepEquals(grant.EffectiveValue!.Value, sense.CompleteEffect));
        Assert.Equal("unresolved",
            NativeOriginTremorsenseProjector.ProjectApprovedMixed(catalog, completed).Status);
    }

    [Fact]
    public void ForgedPayloadAndCandidateProvenanceFailClosed()
    {
        var catalog = Catalog();
        var completed = Direct("species.dwarf");
        var grant = Assert.Single(completed.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId ==
                "trait.stonebound-tremorsense");
        var forgedValue = JsonSerializer.SerializeToElement(new
        {
            id = "stonebound-tremorsense",
            sense = "tremorsense",
            rangeFeet = 999,
            activation = "bonus-action"
        });
        NativeOriginConstructionResult Tampered(NativeOriginEffectiveGrant changedGrant)
        {
            var current = completed.Profile.Underlying with
            {
                InherentGrants = completed.Profile.Underlying.InherentGrants
                    .Select(x => x.Id == grant.Id ? changedGrant : x).ToArray()
            };
            return completed with
            {
                Profile = completed.Profile with
                {
                    Underlying = current, Current = current
                }
            };
        }

        var effect = NativeOriginTremorsenseProjector.Project(catalog,
            Tampered(grant with { EffectiveValue = forgedValue }));
        Assert.Equal("unresolved", effect.Status);
        Assert.Contains(effect.UnresolvedReasons,
            x => x.StartsWith("tremorsense-grant-not-verified:",
                StringComparison.Ordinal));

        var source = NativeOriginTremorsenseProjector.Project(catalog,
            Tampered(grant with { SourceCandidateIds = ["invented-candidate"] }));
        Assert.Equal("unresolved", source.Status);
        var authority = NativeOriginTremorsenseProjector.Project(catalog,
            Tampered(grant with
            {
                Authorization = grant.Authorization! with
                {
                    NativeRuleReference = "species.elf"
                }
            }));
        Assert.Equal("unresolved", authority.Status);
        // Unauthored resource owners and parameter overrides are not valid.
        var fabricatedPool = NativeOriginTremorsenseProjector.Project(catalog,
            Tampered(grant with
            {
                ResourceOwners = new Dictionary<string, string>
                {
                    ["uses"] = "invented-resource-owner"
                }
            }));
        Assert.Equal("unresolved", fabricatedPool.Status);
        var fabricatedParameter = NativeOriginTremorsenseProjector.Project(catalog,
            Tampered(grant with
            {
                Parameters = new Dictionary<string, JsonElement>
                {
                    ["rangeFeet"] = JsonSerializer.SerializeToElement(500)
                }
            }));
        Assert.Equal("unresolved", fabricatedParameter.Status);
        var wrongMaterial = NativeOriginTremorsenseProjector.Project(catalog,
            Tampered(grant with
            {
                Parameters = new Dictionary<string, JsonElement>
                {
                    ["subTrait"] = JsonSerializer.SerializeToElement(
                        new { rangeFeet = 60, contactMaterials = new[] { "earth", "stone" } })
                }
            }));
        Assert.Equal("unresolved", wrongMaterial.Status);
        Assert.Equal("unresolved", NativeOriginTremorsenseProjector.Project(
            catalog, completed with
            {
                Status = NativeOriginConstructionStatus.Unresolved
            }).Status);
    }

    [Fact]
    public void AbsentSenseIsNoneRatherThanAnInferredGrant()
    {
        var catalog = Catalog();
        var elf = Direct("species.elf");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, elf.Status);
        var view = NativeOriginTremorsenseProjector.Project(catalog, elf);
        Assert.Equal("none", view.Status);
        Assert.Empty(view.Senses);
    }
}
