using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Sustainsys.Saml2.StubIdp.IntegrationTests.Infrastructure;

/// <summary>
/// Baseline tests verifying that the application can be hosted by
/// <see cref="WebApplicationFactory{TEntryPoint}"/> and serves the home page.
/// </summary>
public class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmokeTests"/> class.
    /// </summary>
    /// <param name="factory">The test host factory for the StubIdp application.</param>
    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        client = factory.CreateClient();
    }

    /// <summary>
    /// Verifies that a GET request to the application root returns HTTP 200.
    /// </summary>
    [Fact]
    public async Task Root_returns_success()
    {
        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
