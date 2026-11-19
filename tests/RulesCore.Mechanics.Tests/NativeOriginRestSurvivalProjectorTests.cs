using System.Text.Json;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// A completed initial Origin can expose authored Trance and Relentless
/// Endurance without making a resting or zero-hit-point gameplay event.
/// Multiple native donors never grant unselected or doubled abilities.
/// </summary>
public sealed class NativeOriginRestSurvivalProjectorTests
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

    private static NativeOriginConstructionResult Direct(string species) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(new NativeOriginProposal("direct-" + species,
                NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"),
                [], [], [], SelectedSpeciesId: species),
                AutoManifestDirectDefaults: true));

    private static NativeOriginConstructionResult Mixed(
        string[] donors, params string[] selectedTraits)
    {
        var catalog = Catalog();
        var proposal = new NativeOriginProposal(
            "selected-native-rest-survival", NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            donors.Select((species, index) =>
                new NativeOriginContributor("parent-" + index,
                    new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, species))).ToArray(),
            [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var offered = engine.Evaluate(new(proposal)).EvaluatedProposal.Candidates;
        var foundation = offered.Where(x => x.ContributorId == "parent-0" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        Assert.Equal(3, foundation.Length);
        var manifestations = foundation.Select((x, index) =>
            new NativeOriginManifestation("foundation-" + index, x.Id,
                SelectedValue: x.OfferedValue)).ToList();
        var index = 0;
        foreach (var traitId in selectedTraits)
        {
            var selected = Assert.Single(offered.Where(x =>
                x.Identity.MechanicalDefinitionId == traitId &&
                !manifestations.Any(y => y.CandidateId == x.Id)));
            Assert.Equal(NativeOriginEligibility.Eligible,
                selected.SelectionBasis.Eligibility);
            manifestations.Add(new("survival-" + index++, selected.Id));
        }
        return engine.Evaluate(new(proposal with
        {
            Manifestations = manifestations
        }));
    }

    [Fact]
    public void DirectElfTranceIsFourHourLongRestWithoutMagicalSleepInference()
    {
        var catalog = Catalog();
        var elf = Direct("species.elf");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, elf.Status);
        var view = NativeOriginRestSurvivalProjector.Project(catalog, elf);
        Assert.Equal("resolved", view.Status);
        Assert.True(view.PreviewOnly);
        var trance = Assert.IsType<NativeOriginRestSurvivalProjector.TranceEffect>(
            view.Trance);
        Assert.Equal(4, trance.LongRestHours);
        Assert.False(trance.SleepRequired);
        Assert.Equal("species.elf", trance.SourceId);
        Assert.StartsWith("/traits/", trance.OccurrencePath);
        Assert.Null(view.RelentlessEndurance);
        Assert.Equal("unresolved",
            NativeOriginRestSurvivalProjector.ProjectApprovedMixed(
                catalog, elf).Status);
    }

    [Fact]
    public void DirectOrcRelentlessEnduranceIsExactlyOneLongRestResource()
    {
        var orc = Direct("species.orc");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, orc.Status);
        var view = NativeOriginRestSurvivalProjector.Project(Catalog(), orc);
        Assert.Equal("resolved", view.Status);
        Assert.Null(view.Trance);
        var effect = Assert.IsType<
            NativeOriginRestSurvivalProjector.RelentlessEnduranceEffect>(
                view.RelentlessEndurance);
        Assert.Equal("species.orc", effect.SourceId);
        Assert.Equal("reduced-to-zero-hit-points-but-not-killed-outright",
            effect.Trigger);
        Assert.Equal(1, effect.HitPointsInstead);
        Assert.Equal(1, effect.Uses);
        Assert.Equal("long-rest", effect.Recharge);
    }

    [Fact]
    public void MixedElfOrcRequiresSelectionAndPreservesBothIndependentEffects()
    {
        var catalog = Catalog();
        var result = Mixed(["species.elf", "species.orc"],
            "trait.elf.trance", "trait.orc.relentless-endurance");
        Assert.True(result.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", result.Issues.Select(x => x.Code + ": " + x.Detail)));
        var view = NativeOriginRestSurvivalProjector.ProjectApprovedMixed(
            catalog, result);
        Assert.Equal("resolved", view.Status);
        Assert.Equal(4, view.Trance!.LongRestHours);
        Assert.Equal("species.elf", view.Trance.SourceId);
        Assert.Equal(1, view.RelentlessEndurance!.Uses);
        Assert.Equal("species.orc", view.RelentlessEndurance.SourceId);
        Assert.Equal("unresolved",
            NativeOriginRestSurvivalProjector.Project(catalog, result).Status);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(result.Profile));
        var restored = NativeOriginRestSurvivalProjector.ProjectApprovedMixed(
            catalog, result with { Profile = saved });
        Assert.Equal("resolved", restored.Status);
        Assert.Equal(view.Trance, restored.Trance);
        Assert.Equal(view.RelentlessEndurance, restored.RelentlessEndurance);
    }

    [Fact]
    public void UnselectedOrcOfferDoesNotAddAnEnduranceBenefit()
    {
        var result = Mixed(["species.elf", "species.orc"],
            "trait.elf.trance");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var view = NativeOriginRestSurvivalProjector.ProjectApprovedMixed(
            Catalog(), result);
        Assert.Equal("resolved", view.Status);
        Assert.NotNull(view.Trance);
        Assert.Null(view.RelentlessEndurance);
    }

    [Fact]
    public void TwoElvenDonorsCannotDoubleTrance()
    {
        var catalog = Catalog();
        var original = new NativeOriginProposal("two-trances",
            NativeOriginIntent.Mixed,
            new("rule.creature-origination", "biological"),
            [
                new("first", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf")),
                new("second", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "species.elf"))
            ], [], []);
        var engine = new NativeOriginConstructionEngine(catalog);
        var offered = engine.Evaluate(new(original)).EvaluatedProposal.Candidates;
        var foundation = offered.Where(x => x.ContributorId == "first" &&
            x.Identity.IntrinsicSlotId is
                "creatureType" or "size" or "walkingSpeedFeet").ToArray();
        var trances = offered.Where(x =>
            x.Identity.MechanicalDefinitionId == "trait.elf.trance").ToArray();
        Assert.Equal(2, trances.Length);
        Assert.All(trances, x =>
            Assert.Equal(NativeOriginEligibility.Eligible,
                x.SelectionBasis.Eligibility));

        var result = engine.Evaluate(new(original with
        {
            Manifestations = foundation.Select((x, i) =>
                new NativeOriginManifestation("physical-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat(trances.Select((x, i) =>
                new NativeOriginManifestation("trance-" + i, x.Id))).ToArray()
        }));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        var grant = Assert.Single(result.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId == "trait.elf.trance");
        Assert.Equal(2, grant.SourceCandidateIds.Count);
        var projected = NativeOriginRestSurvivalProjector.ProjectApprovedMixed(
            catalog, result);
        Assert.Equal("resolved", projected.Status);
        Assert.Equal(4, projected.Trance!.LongRestHours);
        Assert.Null(projected.RelentlessEndurance);
    }

    [Fact]
    public void IncompleteOrTamperedProfileDoesNotProjectSurvivalEntitlements()
    {
        var catalog = Catalog();
        var orc = Direct("species.orc");
        var effect = Assert.Single(orc.Profile!.Underlying.InherentGrants,
            x => x.Identity.MechanicalDefinitionId ==
                "trait.orc.relentless-endurance");
        var changed = effect with
        {
            EffectiveValue = JsonSerializer.SerializeToElement(new
            {
                trigger = "reduced-to-zero-hit-points-but-not-killed-outright",
                hitPointsInstead = 25,
                uses = new { value = 100, recharge = "short-rest" }
            })
        };
        var tampered = WithGrant(orc, changed);
        Assert.Equal("unresolved",
            NativeOriginRestSurvivalProjector.Project(catalog, tampered).Status);

        var forged = WithGrant(orc, effect with
        {
            SourceCandidateIds = ["untrusted-grant"]
        });
        Assert.Equal("unresolved",
            NativeOriginRestSurvivalProjector.Project(catalog, forged).Status);

        var incomplete = orc with
        {
            Status = NativeOriginConstructionStatus.Incomplete
        };
        var withheld = NativeOriginRestSurvivalProjector.Project(
            catalog, incomplete);
        Assert.Equal("unresolved", withheld.Status);
        Assert.Null(withheld.RelentlessEndurance);
    }

    [Fact]
    public void NeitherTraitIsInferredFromAnUnrelatedDirectSpecies()
    {
        var dwarf = Direct("species.dwarf");
        Assert.Equal(NativeOriginConstructionStatus.Authorized, dwarf.Status);
        var view = NativeOriginRestSurvivalProjector.Project(Catalog(), dwarf);
        Assert.Equal("none", view.Status);
        Assert.Null(view.Trance);
        Assert.Null(view.RelentlessEndurance);
    }

    private static NativeOriginConstructionResult WithGrant(
        NativeOriginConstructionResult original, NativeOriginEffectiveGrant changed)
    {
        var current = original.Profile!.Underlying with
        {
            InherentGrants = original.Profile.Underlying.InherentGrants
                .Select(x => x.Id == changed.Id ? changed : x).ToArray()
        };
        return original with
        {
            Profile = original.Profile with
            {
                Underlying = current, Current = current
            }
        };
    }
}
