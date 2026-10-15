using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sustainsys.Saml2.Saml2P;
using Sustainsys.Saml2.StubIdp.Saml;
using Sustainsys.Saml2.WebSso;

namespace Sustainsys.Saml2.StubIdp.IntegrationTests.Saml;

/// <summary>
/// Verifies that <see cref="CommandResultExtensions.ToResult"/> produces the same externally
/// observable HTTP behavior when executed by the real ASP.NET Core request pipeline as the
/// legacy <c>Sustainsys.Saml2.Mvc.ToActionResult</c> did for the stub IdP.
/// </summary>
/// <remarks>
/// <para>
/// A minimal in-process <see cref="IHost"/> is built per test fixture with a single minimal-API
/// endpoint that returns <c>commandResult.ToResult()</c>. The endpoint accepts
/// <c>?kind=redirect</c> or <c>?kind=content</c> so the same client can exercise both the
/// legacy-302-redirect and the auto-post HTML paths through the real pipeline.
/// </para>
/// <para>
/// The harness deliberately does not boot the StubIdp <c>Program</c>: it needs only the bridge
/// plus the routing/middleware pipeline to verify that an <see cref="IResult"/> produced by
/// <see cref="CommandResultExtensions.ToResult"/> writes the response expected by external
/// SPs. Bootstrapping the full Blazor host would drag in static-asset manifest, antiforgery and
/// the Razor component render mode - none of which is relevant to this bridge.
/// </para>
/// </remarks>
public class CommandResultRoundTripTests : IClassFixture<CommandResultRoundTripTests.Harness>
{
    /// <summary>
    /// In-process host that exposes a small minimal-API surface exercising the bridge through
    /// the real ASP.NET Core middleware pipeline.
    /// </summary>
    public sealed class Harness : IDisposable
    {
        private readonly IHost host;

        /// <summary>
        /// Initializes a new instance of the <see cref="Harness"/> class, exposing the minimal
        /// command-result endpoints.
        /// </summary>
        public Harness()
        {
            host = new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.UseTestServer();
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddRouting();
                    });
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            var location = new Uri("https://sp.example.com/Saml2/Acs?SAMLResponse=abc&RelayState=xyz");

                            endpoints.MapGet("/test/command-result/redirect", () =>
                            {
                                var result = new CommandResult
                                {
                                    HttpStatusCode = HttpStatusCode.SeeOther,
                                    Location = location,
                                };
                                return result.ToResult();
                            });

                            endpoints.MapGet("/test/command-result/content", () =>
                            {
                                const string html =
                                    "<html><body><form action=\"https://sp.example.com/Saml2/Acs\" method=\"POST\">" +
                                    "<input type=\"hidden\" name=\"SAMLResponse\" value=\"abc\"/>" +
                                    "</form></body></html>";
                                var result = new CommandResult
                                {
                                    HttpStatusCode = HttpStatusCode.OK,
                                    Content = html,
                                    ContentType = "text/html; charset=utf-8",
                                };
                                return result.ToResult();
                            });

                            endpoints.MapGet("/test/command-result/post-binding", () =>
                            {
                                var message = new Saml2LogoutResponse(Saml2StatusCode.Success)
                                {
                                    DestinationUrl = new Uri("https://sp.example.com/Saml2/Acs"),
                                    Issuer = new global::Sustainsys.Saml2.Metadata.EntityId("https://stubidp.example/Metadata"),
                                };
                                return Saml2Binding.Get(Saml2BindingType.HttpPost).Bind(message).ToResult();
                            });

                            endpoints.MapGet("/test/command-result/headers", () =>
                            {
                                var result = new CommandResult
                                {
                                    HttpStatusCode = HttpStatusCode.OK,
                                    Content = "x",
                                    ContentType = "text/plain",
                                };
                                result.Headers["X-Stub-Saml2"] = "marker";
                                return result.ToResult();
                            });
                        });
                    });
                })
                .Build();

            host.Start();
        }

        /// <summary>
        /// Gets the <see cref="HttpClient"/> for sending requests to the harness.
        /// </summary>
        public HttpClient Client => host.GetTestClient();

        /// <inheritdoc/>
        public void Dispose() => host.Dispose();
    }

    private readonly Harness harness;

    /// <param name="harness">The hosted test harness.</param>
    public CommandResultRoundTripTests(Harness harness)
    {
        ArgumentNullException.ThrowIfNull(harness);
        this.harness = harness;
    }

    /// <summary>
    /// Verifies that the IResult surfaces as a 302 redirect with the library-built Location
    /// through the real ASP.NET Core pipeline - matches the legacy MVC RedirectResult parity.
    /// </summary>
    [Fact]
    public async Task Redirect_status_and_location_round_trip_through_the_pipeline()
    {
        using var response = await harness.Client.GetAsync("/test/command-result/redirect");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(
            "https://sp.example.com/Saml2/Acs?SAMLResponse=abc&RelayState=xyz",
            response.Headers.Location?.ToString());
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Verifies that content responses carry the library-supplied content type and body.
    /// </summary>
    [Fact]
    public async Task Content_status_body_and_content_type_round_trip()
    {
        using var response = await harness.Client.GetAsync("/test/command-result/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SAMLResponse", body);
        Assert.Contains("abc", body);
    }

    /// <summary>
    /// Verifies that the real library auto-post output reaches the client as
    /// <c>text/html; charset=utf-8</c> (legacy System.Web parity) with the SAML form intact.
    /// </summary>
    [Fact]
    public async Task Post_binding_round_trips_with_utf8_charset()
    {
        using var response = await harness.Client.GetAsync("/test/command-result/post-binding");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("action=\"https://sp.example.com/Saml2/Acs\"", body);
        Assert.Contains("name=\"SAMLResponse\"", body);
    }

    /// <summary>
    /// Verifies that arbitrary library-supplied headers are copied through the pipeline.
    /// </summary>
    [Fact]
    public async Task Arbitrary_headers_round_trip_through_the_pipeline()
    {
        using var response = await harness.Client.GetAsync("/test/command-result/headers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("marker", response.Headers.GetValues("X-Stub-Saml2").Single());
    }

    /// <summary>
    /// Verifies that the bridge emits no Cache-Control header for library-default results
    /// (legacy MVC <c>ToActionResult()</c> ignored cacheability).
    /// </summary>
    [Theory]
    [InlineData("/test/command-result/redirect")]
    [InlineData("/test/command-result/post-binding")]
    public async Task No_cache_control_header_is_emitted(string path)
    {
        using var response = await harness.Client.GetAsync(path);

        Assert.Null(response.Headers.CacheControl);
    }
}