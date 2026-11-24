using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Shared sense definitions simplify inheritance without making distinct
/// contributor offers interchangeable or granting duplicate activations.
/// </summary>
public sealed class NativeOriginSenseOverlapTests
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

    [Fact]
    public void DwarfAndRockGnomeOfferOneCanonicalTremorsenseDefinition()
    {
        var proposal = new NativeOriginProposal("shared-sense-offers",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("dwarf", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.dwarf")),
                new("rock", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.gnome.rock"))
            ], [], []);
        var discovered = new NativeOriginConstructionEngine(Catalog())
            .Evaluate(new(proposal)).EvaluatedProposal.Candidates;
        var dwarf = Assert.Single(discovered, x => x.ContributorId == "dwarf" &&
            x.Identity.MechanicalDefinitionId == "trait.stonebound-tremorsense");
        var rock = Assert.Single(discovered, x => x.ContributorId == "rock" &&
            x.Identity.MechanicalDefinitionId == "trait.stonebound-tremorsense");
        Assert.NotEqual(dwarf.Id, rock.Id);
        Assert.NotEqual(dwarf.Identity.Occurrence.Source.Id,
            rock.Identity.Occurrence.Source.Id);
        Assert.Equal(dwarf.OfferedValue!.Value.GetRawText(),
            rock.OfferedValue!.Value.GetRawText());
        Assert.Equal(new[] { "stone" }, dwarf.OfferedParameters!["subTrait"]
            .GetProperty("contactMaterials").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(new[] { "earth", "stone" }, rock.OfferedParameters!["subTrait"]
            .GetProperty("contactMaterials").EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    [Fact]
    public void LowLightVisionAndTremorsenseRemainDistinctCharacteristics()
    {
        var dwarf = new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(new NativeOriginProposal("direct-dwarf", NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"),
                [], [], [], SelectedSpeciesId: "species.dwarf"),
                AutoManifestDirectDefaults: true));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, dwarf.Status);
        var sense = Assert.Single(NativeOriginTremorsenseProjector
            .Project(Catalog(), dwarf).Senses);
        Assert.Equal("trait.stonebound-tremorsense", sense.DefinitionId);
        Assert.Equal("none", NativeOriginVisionProjector.Project(Catalog(), dwarf).Status);
    }
}
