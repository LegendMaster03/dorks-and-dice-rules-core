using System.Text.Json;
using RulesCore.Infrastructure.Rules;

namespace RulesCore.Tests;

public sealed class WikiReferenceModernSpeciesProjectionTests
{
    [Fact]
    public void FiveFiveESpeciesCreatureTypesProjectIntoWikiBrowserMetadata()
    {
        using var document = JsonDocument.Parse("""
            {
              "name": "Aasimar",
              "source": "XPHB",
              "edition": "one",
              "creatureTypes": ["humanoid"],
              "size": ["S", "M"],
              "speed": 30
            }
            """);

        var fields = WikiReferenceBrowserProjection.Project("species", document.RootElement);

        var creatureType = Assert.Single(fields, value => value.Key == "creatureType");
        Assert.Equal("humanoid", creatureType.Value);
    }
}
