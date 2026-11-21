using System.Text.Json;
using System.Text.Json.Nodes;
using RulesCore.Content;
using RulesCore.Mechanics;
using Xunit;

namespace RulesCore.Mechanics.Tests;

/// <summary>
/// Uses the actual level-four Stone Goliath Cavernous Sight rule. An authored
/// conditional range upgrade is not an automatic Species inheritance or a
/// general stacking rule for unrelated native senses.
/// </summary>
public sealed class NativeOriginDarkvisionUpgradeTests
{
    private static NativeCatalogSnapshot Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var root = Path.Combine(directory.FullName, "data", "rulesets");
            if (File.Exists(Path.Combine(root, "first-party-001", "native",
                "rules", "creature-origination.json")))
                return NativeCatalogLoader.Load(NativeRulesetLayout.Resolve(root, "first-party-001"));
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Native catalog is missing.");
    }

    private static NativeOriginConstructionResult Evaluate(
        NativeOriginProposal proposal, INativeOriginAuthority? authority = null,
        bool defaults = false) =>
        new NativeOriginConstructionEngine(Catalog()).Evaluate(
            new(proposal, authority is null ? null : new("campaign", "darkvision-upgrade"),
                AutoManifestDirectDefaults: defaults), authority);

    private static NativeTableRuleDecision Decision(string id, string kind, string target,
        object outcome) =>
        new(id, new("dm", "verified-upgrade"),
            new("campaign", "darkvision-upgrade"), new(kind, target),
            JsonSerializer.SerializeToElement(outcome));

    private sealed class TestAuthority : INativeOriginAuthority
    {
        public bool Authorizes(NativeTableRuleDecision decision,
            NativeTableDecisionScope scope) =>
            scope.Kind == "campaign" && scope.Id == "darkvision-upgrade" &&
            decision.Authority.ActorId == "dm" &&
            decision.Authority.AuthorizationEvidenceId == "verified-upgrade";

        public bool AuthorizesCatalogChoice(string catalogId, string optionId,
            string evidenceId) => false;
    }

    // In-memory rule mutations are intentionally loaded through the normal
    // native-catalog loader, rather than changing the checked-in catalog.
    // The construction result remains from the real, unmodified catalog.
    private sealed class AlteredCatalog : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "rc-darkvision-projection-" + Guid.NewGuid().ToString("N"));

        public NativeCatalogSnapshot Snapshot { get; }

        public AlteredCatalog(JsonNode? trait)
        {
            var package = Path.Combine(_root, "first-party-001");
            var native = Path.Combine(package, "native");
            Directory.CreateDirectory(native);
            File.WriteAllText(Path.Combine(package, "manifest.json"),
                """{"id":"first-party-001","kind":"ruleset-package","schemaVersion":1,"nativeRoot":"native/","runtimeContentRoot":"native/","workingRoot":"working/"}""");
            var definitions = new JsonArray
            {
                JsonNode.Parse(
                    """{"id":"ancestry.goliath.stone","kind":"ancestry","name":"Stone Goliath"}""")
            };
            if (trait is not null)
                definitions.Add(new JsonObject
                {
                    ["id"] = "trait.cavernous-sight",
                    ["kind"] = "origin-trait",
                    ["name"] = "Cavernous Sight",
                    ["trait"] = trait.DeepClone()
                });
            File.WriteAllText(Path.Combine(native, "fixture.json"),
                definitions.ToJsonString());
            Snapshot = NativeCatalogLoader.Load(
                NativeRulesetLayout.Resolve(_root, "first-party-001"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }

    private static JsonObject CanonicalCavernousSight()
    {
        Assert.True(Catalog().TryGetById(
            "trait.cavernous-sight", out var definition));
        return JsonNode.Parse(definition!.Definition.GetProperty("trait")
            .GetRawText())!.AsObject();
    }

    private static NativeOriginConstructionResult DirectStone()
    {
        var proposal = new NativeOriginProposal("direct-stone-negative-tests",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [], SelectedAncestryId: "ancestry.goliath.stone");
        var aptitude = Assert.Single(Evaluate(proposal, defaults: true)
            .EvaluatedProposal.Candidates, x =>
                x.Identity.MechanicalDefinitionId ==
                    "trait.ancestral-aptitude");
        var result = Evaluate(proposal with
        {
            Manifestations =
            [
                new("aptitude", aptitude.Id,
                    Choices: [new("abilityChoice", ["strength"])])
            ]
        }, defaults: true);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, result.Status);
        return result;
    }

    private static NativeOriginConstructionResult AlterSenseGrant(
        NativeOriginConstructionResult origin, string definitionId,
        JsonElement alteredEffect)
    {
        var profile = origin.Profile!;
        var selected = Assert.Single(profile.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == definitionId);
        var configuration = profile.Underlying with
        {
            InherentGrants = profile.Underlying.InherentGrants
                .Select(grant => grant.Id == selected.Id
                    ? grant with { EffectiveValue = alteredEffect }
                    : grant).ToArray()
        };
        return origin with
        {
            Profile = profile with
            {
                Underlying = configuration,
                Current = configuration
            }
        };
    }

    [Theory]
    [InlineData("rangeFeet", "\"60\"")]
    [InlineData("rangeFeet", "1.5")]
    [InlineData("rangeFeet", "null")]
    [InlineData("rangeFeet", "[]")]
    [InlineData("addToExistingDarkvisionFeet", "\"60\"")]
    [InlineData("addToExistingDarkvisionFeet", "true")]
    [InlineData("requiredCharacterLevel", "\"4\"")]
    [InlineData("requiredCharacterLevel", "[]")]
    [InlineData("stacking", "10")]
    [InlineData("stacking", "null")]
    [InlineData("sense", "false")]
    public void MalformedAuthoredExtensionTypesFailClosed(
        string field, string replacementJson)
    {
        var canonical = CanonicalCavernousSight();
        canonical[field] = JsonNode.Parse(replacementJson);
        using var catalog = new AlteredCatalog(canonical);
        // Match both stored and authored effect to reach the actual grammar
        // check, rather than passing solely because the values disagree.
        var origin = AlterSenseGrant(DirectStone(),
            "trait.cavernous-sight",
            JsonSerializer.SerializeToElement(canonical));
        var view = NativeOriginDarkvisionProjector.Project(catalog.Snapshot,
            origin, 4);
        Assert.Equal("unresolved", view.Status);
        Assert.Contains("darkvision-extension-not-verified", view.UnresolvedReasons);
        Assert.Null(view.Darkvision);
        Assert.Empty(view.PendingExtensions);
    }

    [Theory]
    [InlineData("sense")]
    [InlineData("rangeFeet")]
    [InlineData("addToExistingDarkvisionFeet")]
    [InlineData("requiredCharacterLevel")]
    [InlineData("stacking")]
    public void MissingAuthoredExtensionFieldsFailClosed(string field)
    {
        var canonical = CanonicalCavernousSight();
        Assert.True(canonical.Remove(field));
        using var catalog = new AlteredCatalog(canonical);
        var origin = AlterSenseGrant(DirectStone(),
            "trait.cavernous-sight",
            JsonSerializer.SerializeToElement(canonical));
        var view = NativeOriginDarkvisionProjector.Project(catalog.Snapshot,
            origin, 4);
        Assert.Equal("unresolved", view.Status);
        Assert.Contains("darkvision-extension-not-verified", view.UnresolvedReasons);
    }

    [Fact]
    public void MissingCanonicalDefinitionDoesNotHideEffectiveDarkvision()
    {
        using var catalog = new AlteredCatalog(trait: null);
        var view = NativeOriginDarkvisionProjector.Project(
            catalog.Snapshot, DirectStone(), 3);
        Assert.Equal("unresolved", view.Status);
        Assert.Contains("darkvision-extension-not-verified", view.UnresolvedReasons);
        Assert.Null(view.Darkvision);
        Assert.Empty(view.PendingExtensions);
    }

    [Fact]
    public void CanonicalAndEffectiveDarkvisionClaimsMustAgree()
    {
        var canonical = CanonicalCavernousSight();
        canonical["sense"] = "tremorsense";
        canonical.Remove("addToExistingDarkvisionFeet");
        using var catalog = new AlteredCatalog(canonical);
        // The effective grant still claims Darkvision, while its canonical
        // record claims an unrelated sense: it must not be treated as absent.
        var view = NativeOriginDarkvisionProjector.Project(
            catalog.Snapshot, DirectStone(), 3);
        Assert.Equal("unresolved", view.Status);
        Assert.Contains("darkvision-extension-not-verified", view.UnresolvedReasons);

        var originallySelected = DirectStone();
        var alteredEffect = JsonSerializer.SerializeToElement(new
        {
            id = "cavernous-sight", sense = "tremorsense"
        });
        var forged = AlterSenseGrant(originallySelected,
            "trait.cavernous-sight", alteredEffect);
        var reverse = NativeOriginDarkvisionProjector.Project(Catalog(), forged, 4);
        Assert.Equal("unresolved", reverse.Status);
        Assert.Contains("darkvision-extension-not-verified", reverse.UnresolvedReasons);
    }

    [Fact]
    public void ForgedDarkvisionOnUnrelatedSelectedTraitCannotReturnNone()
    {
        var catalog = Catalog();
        // Conventional Orc has Low-Light Vision but no numeric Darkvision:
        // an unrelated selected native trait cannot forge that new sense.
        var directOrc = new NativeOriginConstructionEngine(catalog).Evaluate(
            new(new NativeOriginProposal("orc-with-forged-vision",
                NativeOriginIntent.Direct,
                new("rule.creature-origination", "biological"),
                [], [], [], SelectedSpeciesId: "species.orc"),
                AutoManifestDirectDefaults: true));
        Assert.Equal(NativeOriginConstructionStatus.Authorized, directOrc.Status);
        Assert.Equal("none",
            NativeOriginDarkvisionProjector.Project(catalog, directOrc, 4).Status);
        var forged = AlterSenseGrant(directOrc,
            "trait.relentless-endurance",
            JsonSerializer.SerializeToElement(new
            {
                id = "relentless-endurance",
                sense = "darkvision",
                rangeFeet = 600,
                addToExistingDarkvisionFeet = 600,
                requiredCharacterLevel = 1,
                stacking = "once-per-feature"
            }));
        var view = NativeOriginDarkvisionProjector.Project(catalog, forged, 4);
        Assert.Equal("unresolved", view.Status);
        Assert.Contains("darkvision-extension-not-verified", view.UnresolvedReasons);
        Assert.Null(view.Darkvision);
    }

    [Fact]
    public void DirectStoneGoliathGetsStandaloneDarkvisionOnlyWhenLevelFourUnlocks()
    {
        var proposal = new NativeOriginProposal("direct-stone-vision",
            NativeOriginIntent.Direct,
            new("rule.creature-origination", "biological"),
            [], [], [], SelectedAncestryId: "ancestry.goliath.stone");
        var candidates = Evaluate(proposal, defaults: true).EvaluatedProposal.Candidates;
        var aptitude = Assert.Single(candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.ancestral-aptitude");
        var cavernous = Assert.Single(candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.cavernous-sight");
        Assert.Equal(NativeOriginEligibility.Eligible, cavernous.SelectionBasis.Eligibility);
        var completed = Evaluate(proposal with
        {
            Manifestations = [new("aptitude", aptitude.Id,
                Choices: [new("abilityChoice", ["strength"])])]
        }, defaults: true);
        Assert.True(completed.Status == NativeOriginConstructionStatus.Authorized,
            string.Join("; ", completed.Issues.Select(x => x.Code)));
        Assert.Contains(completed.Profile!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == cavernous.Identity.MechanicalDefinitionId);

        var unknownLevel = NativeOriginDarkvisionProjector.Project(Catalog(), completed);
        Assert.Equal("unresolved", unknownLevel.Status);
        Assert.Contains("darkvision-extension-requires-character-level",
            unknownLevel.UnresolvedReasons);
        var level3 = NativeOriginDarkvisionProjector.Project(Catalog(), completed, 3);
        Assert.Equal("none", level3.Status);
        Assert.Null(level3.Darkvision);
        // An already-selected level-four trait is still part of the Origin,
        // but it does not grant active Darkvision before its authored unlock.
        var pending = Assert.Single(level3.PendingExtensions);
        Assert.Equal(4, pending.RequiredCharacterLevel);
        Assert.Equal(60, pending.AddedRangeFeet);
        Assert.Equal(60, pending.StandaloneRangeFeet);
        Assert.Equal("ancestry.goliath.stone", pending.SourceId);
        Assert.Equal("once-per-feature",
            pending.CompleteEffect.GetProperty("stacking").GetString());
        var level4 = NativeOriginDarkvisionProjector.Project(Catalog(), completed, 4);
        Assert.Empty(level4.PendingExtensions);
        Assert.Equal("resolved", level4.Status);
        Assert.Equal(60, level4.Darkvision!.RangeFeet);
        Assert.Equal(0, level4.Darkvision.BaseRangeFeet);
        Assert.Equal("ancestry.goliath.stone", level4.Darkvision.SourceId);
        var source = Assert.Single(level4.Darkvision.Extensions!);
        Assert.Equal(60, source.AddedRangeFeet);
        Assert.Equal(4, source.RequiredCharacterLevel);
        Assert.Equal("ancestry.goliath.stone", source.SourceId);
        Assert.Equal("grant:auto:" + cavernous.Id, source.GrantId);
        Assert.Equal("unresolved",
            NativeOriginDarkvisionProjector.Project(Catalog(), completed, 0).Status);
        Assert.Equal("unresolved",
            NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), completed, 4).Status);
    }

    [Fact]
    public void MixedDrowStoneDarkvisionAddsOnlyTheAuthoredRangeWhenUnlocked()
    {
        var proposal = new NativeOriginProposal("mixed-drow-stone",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [
                new("drow", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.elf.drow")),
                new("stone", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.goliath.stone"))
            ], [], []);
        var offered = Evaluate(proposal).EvaluatedProposal.Candidates;
        var source = Assert.Single(offered, x => x.ContributorId == "drow" &&
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
        var drawback = Assert.Single(offered, x => x.ContributorId == "drow" &&
            x.Identity.MechanicalDefinitionId == "trait.sunlight-sensitivity");
        var upgrade = Assert.Single(offered, x => x.ContributorId == "stone" &&
            x.Identity.MechanicalDefinitionId == "trait.cavernous-sight");
        var foundation = offered.Where(x => x.ContributorId == "drow" &&
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .ToArray();
        Assert.Equal(3, foundation.Length);
        var permissions = new[]
        {
            Decision("allow-drow-darkvision", "candidate", source.Id,
                new { eligibility = "eligible" }),
            Decision("allow-drow-sunlight", "candidate", drawback.Id,
                new { eligibility = "eligible" }),
            Decision("allow-stone-cavernous", "candidate", upgrade.Id,
                new { eligibility = "eligible" })
        };
        var completed = Evaluate(proposal with
        {
            Manifestations = foundation.Select((x, i) =>
                new NativeOriginManifestation("foundation-" + i, x.Id,
                    SelectedValue: x.OfferedValue)).Concat(
            [
                new("selected-darkvision", source.Id, SelectedValue: source.OfferedValue),
                new("selected-sunlight", drawback.Id),
                new("selected-cavernous", upgrade.Id)
            ]).ToArray(),
            TableDecisions = permissions
        }, new TestAuthority());

        Assert.True(completed.Status == NativeOriginConstructionStatus.Authorized,
            "Status: " + completed.Status + " / " +
            string.Join("; ", completed.Issues.Select(x => x.Code + ":" + x.Detail)));
        var at3 = NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), completed, 3);
        Assert.Equal("resolved", at3.Status);
        Assert.Equal(120, at3.Darkvision!.RangeFeet);
        Assert.Empty(at3.Darkvision.Extensions!);
        var locked = Assert.Single(at3.PendingExtensions);
        Assert.Equal("grant:selected-cavernous", locked.GrantId);
        Assert.Equal(4, locked.RequiredCharacterLevel);
        Assert.Equal(60, locked.StandaloneRangeFeet);

        var at4 = NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), completed, 4);
        Assert.Empty(at4.PendingExtensions);
        Assert.Equal("resolved", at4.Status);
        Assert.Equal(180, at4.Darkvision!.RangeFeet);
        Assert.Equal(120, at4.Darkvision.BaseRangeFeet);
        Assert.Equal("ancestry.elf.drow", at4.Darkvision.SourceId);
        Assert.Equal("grant:selected-darkvision", at4.Darkvision.GrantId);
        var extra = Assert.Single(at4.Darkvision.Extensions!);
        Assert.Equal(60, extra.AddedRangeFeet);
        Assert.Equal("ancestry.goliath.stone", extra.SourceId);
        Assert.Equal("grant:selected-cavernous", extra.GrantId);
        Assert.Equal("unresolved",
            NativeOriginDarkvisionProjector.ProjectApprovedMixed(Catalog(), completed).Status);
        Assert.Equal("unresolved",
            NativeOriginDarkvisionProjector.Project(Catalog(), completed, 4).Status);

        var saved = JsonSerializer.Deserialize<NativeOriginProfile>(
            JsonSerializer.Serialize(completed.Profile));
        Assert.Contains(saved!.Underlying.InherentGrants, x =>
            x.Identity.MechanicalDefinitionId == upgrade.Identity.MechanicalDefinitionId);
        Assert.Contains(saved.Underlying.InherentGrants, x =>
            x.Identity.IntrinsicSlotId == "darkvisionFeet");
        var reloadedAt3 = NativeOriginDarkvisionProjector.ProjectApprovedMixed(
            Catalog(), completed with { Profile = saved }, 3);
        Assert.Equal("resolved", reloadedAt3.Status);
        Assert.Equal(120, reloadedAt3.Darkvision!.RangeFeet);
        var reloadedLocked = Assert.Single(reloadedAt3.PendingExtensions);
        Assert.Equal(locked.GrantId, reloadedLocked.GrantId);
        Assert.True(JsonElement.DeepEquals(locked.CompleteEffect,
            reloadedLocked.CompleteEffect));
    }

    [Fact]
    public void UnselectedGoliathUpgradeDoesNotModifyAuthorizedDrowRange()
    {
        var proposal = new NativeOriginProposal("direct-drow-range",
            NativeOriginIntent.Direct, new("rule.creature-origination", "biological"),
            [], [], [], SelectedAncestryId: "ancestry.elf.drow");
        var casting = Assert.Single(Evaluate(proposal, defaults: true)
            .EvaluatedProposal.Candidates, x =>
                x.Identity.IntrinsicSlotId == "spellcastingAbility");
        var completed = Evaluate(proposal with
        {
            Manifestations = [new("drow-casting", casting.Id,
                Choices: [new("spellcastingAbility", ["wisdom"])])]
        }, defaults: true);
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var result = NativeOriginDarkvisionProjector.Project(Catalog(), completed);
        Assert.Equal("resolved", result.Status);
        Assert.Equal(120, result.Darkvision!.RangeFeet);
        Assert.Equal(120, result.Darkvision.BaseRangeFeet);
        Assert.Empty(result.Darkvision.Extensions!);
    }

    [Fact]
    public void MixedStoneCavernousProvidesStandaloneDarkvisionAtLevelFour()
    {
        var proposal = new NativeOriginProposal("no-implicit-upgrade",
            NativeOriginIntent.Mixed, new("rule.creature-origination", "biological"),
            [
                new("stone", new(NativeOriginInputKind.BiologicalParent),
                    new(NativeOriginSourceKind.Native, "ancestry.goliath.stone"))
            ], [], []);
        var evaluated = Evaluate(proposal).EvaluatedProposal;
        var upgrade = Assert.Single(evaluated.Candidates, x =>
            x.Identity.MechanicalDefinitionId == "trait.cavernous-sight");
        Assert.Equal(NativeOriginEligibility.Eligible,
            upgrade.SelectionBasis.Eligibility);
        var attempts = evaluated.Candidates.Where(x =>
            x.Identity.IntrinsicSlotId is "creatureType" or "size" or "walkingSpeedFeet")
            .Select((x, i) => new NativeOriginManifestation("foundation-" + i,
                x.Id, SelectedValue: x.OfferedValue))
            .Append(new("selected-upgrade", upgrade.Id)).ToArray();
        var completed = Evaluate(proposal with { Manifestations = attempts });
        Assert.Equal(NativeOriginConstructionStatus.Authorized, completed.Status);
        var beforeUnlock = NativeOriginDarkvisionProjector.ProjectApprovedMixed(
            Catalog(), completed, 3);
        Assert.Equal("none", beforeUnlock.Status);
        Assert.Null(beforeUnlock.Darkvision);
        Assert.Single(beforeUnlock.PendingExtensions);
        // Cavernous Sight has its own authored 60-foot standalone range; it
        // does not require an independently inherited Darkvision baseline.
        var atUnlock = NativeOriginDarkvisionProjector.ProjectApprovedMixed(
            Catalog(), completed, 4);
        Assert.Equal("resolved", atUnlock.Status);
        Assert.Equal(60, atUnlock.Darkvision!.RangeFeet);
        Assert.Equal(0, atUnlock.Darkvision.BaseRangeFeet);
        Assert.Equal(60, Assert.Single(atUnlock.Darkvision.Extensions!).AddedRangeFeet);
    }
}
