using RulesCore.Content;
using Xunit;

namespace RulesCore.Host.Tests;

public sealed class NativeRulesetLayoutTests
{
    [Fact]
    public void LayoutAlwaysResolvesOnlyTheNativeDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "rules-core-test-content");
        var location = NativeRulesetLayout.Resolve(root, "first-party-001");

        Assert.Equal(Path.GetFullPath(root), location.ContentRoot);
        Assert.Equal(
            Path.Combine(Path.GetFullPath(root), "first-party-001", "native"),
            location.NativeDirectory);
        Assert.NotEqual(
            Path.Combine(Path.GetFullPath(root), "first-party-001", "working"),
            location.NativeDirectory);
    }

    [Theory]
    [InlineData("../working")]
    [InlineData("first-party-001/working")]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("FIRST-PARTY-001")]
    public void RulesetIdsCannotEscapeTheSelectedNamespace(string id)
    {
        Assert.Throws<ArgumentException>(() =>
            NativeRulesetLayout.Resolve(Path.GetTempPath(), id));
    }
}
