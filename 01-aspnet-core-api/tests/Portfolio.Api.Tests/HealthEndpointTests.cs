using Microsoft.AspNetCore.Mvc.Testing;

namespace Portfolio.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Contains("ok", await response.Content.ReadAsStringAsync());
    }
}
