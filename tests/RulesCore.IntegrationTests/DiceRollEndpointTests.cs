using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RulesCore.IntegrationTests;

public sealed class DiceRollEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DiceRollEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task RollEndpointSupportsEveryBuiltInD20SelectionMode()
    {
        foreach (var mode in new[] { "normal", "advantage", "disadvantage", "emphasis" })
        {
            using var response = await _client.PostAsJsonAsync(
                "/api/rules/dice/roll",
                new { sides = 20, selectionMode = mode, repeat = 8 });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;

            Assert.Equal(20, root.GetProperty("sides").GetInt32());
            Assert.Equal(mode, root.GetProperty("selectionMode").GetString());
            Assert.Equal(8, root.GetProperty("outcomes").GetArrayLength());

            foreach (var outcome in root.GetProperty("outcomes").EnumerateArray())
            {
                var rolls = outcome.GetProperty("rolls").EnumerateArray()
                    .Select(value => value.GetInt32())
                    .ToArray();
                Assert.All(rolls, value => Assert.InRange(value, 1, 20));
                Assert.Equal(mode == "normal" ? 1 : 2, rolls.Length);
            }
        }
    }

    [Fact]
    public async Task EmphasisRejectsNonD20Dice()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/rules/dice/roll",
            new { sides = 12, selectionMode = "emphasis" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
