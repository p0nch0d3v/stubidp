using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sustainsys.Saml2.StubIdp.Configuration;

namespace Sustainsys.Saml2.StubIdp.IntegrationTests.Configuration;

/// <summary>
/// Verifies that the hosted application binds <see cref="StubIdpOptions"/> from its own
/// <c>appsettings.json</c>, that the migrated data files are available at runtime, and that no
/// certificate password is present in tracked configuration.
/// </summary>
public class ConfigurationAndDataFilesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationAndDataFilesTests"/> class.
    /// </summary>
    /// <param name="factory">The test host factory for the StubIdp application.</param>
    public ConfigurationAndDataFilesTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        this.factory = factory;
    }

    private StubIdpOptions Options =>
        factory.Services.GetRequiredService<IOptions<StubIdpOptions>>().Value;

    private IWebHostEnvironment Environment =>
        factory.Services.GetRequiredService<IWebHostEnvironment>();

    /// <summary>
    /// Verifies that the application's <c>appsettings.json</c> supplies the legacy
    /// <c>Web.config</c> defaults.
    /// </summary>
    [Fact]
    public void Options_bind_from_appsettings()
    {
        var options = Options;

        Assert.Equal("https://sp.example.com/SAML2/Acs", options.DefaultAcsUrl);
        Assert.Equal("JohnDoe", options.DefaultNameId);
        Assert.Equal("App_Data", options.DataPath);
        Assert.Equal("App_Data/stubidp.sustainsys.com.pfx", options.Certificates.Default.File);
        Assert.Equal("App_Data/Kentor.AuthServices.StubIdp.pfx", options.Certificates.LegacyKentor.File);
        Assert.Equal("stubidp.kentor.se", options.Certificates.LegacyKentor.HostName);
    }

    /// <summary>
    /// Verifies that the configured data directory exists relative to the content root and
    /// contains the migrated tenant configuration and certificate files.
    /// </summary>
    /// <param name="fileName">The expected data file name.</param>
    [Theory]
    [InlineData("default.json")]
    [InlineData("2273dd5a-8e86-4abc-9078-236215f06c0e.json")]
    [InlineData("stubidp.sustainsys.com.pfx")]
    [InlineData("stubidp.sustainsys.com.cer")]
    [InlineData("Kentor.AuthServices.StubIdp.pfx")]
    [InlineData("Kentor.AuthServices.StubIdp.cer")]
    public void Data_file_is_available_at_the_configured_data_path(string fileName)
    {
        var path = Path.Combine(Environment.ContentRootPath, Options.DataPath, fileName);

        Assert.True(File.Exists(path), $"Expected data file '{path}' to exist.");
    }

    /// <summary>
    /// Verifies that the data files are copied to the build output, so a published or
    /// binary-only deployment has them too.
    /// </summary>
    /// <param name="fileName">The expected data file name.</param>
    [Theory]
    [InlineData("default.json")]
    [InlineData("2273dd5a-8e86-4abc-9078-236215f06c0e.json")]
    [InlineData("stubidp.sustainsys.com.pfx")]
    [InlineData("stubidp.sustainsys.com.cer")]
    [InlineData("Kentor.AuthServices.StubIdp.pfx")]
    [InlineData("Kentor.AuthServices.StubIdp.cer")]
    public void Data_file_is_copied_to_the_build_output(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "App_Data", fileName);

        Assert.True(File.Exists(path), $"Expected data file '{path}' in the build output.");
    }

    /// <summary>
    /// Verifies that the tenant JSON schema is on disk where the manage page validator can read
    /// it.
    /// </summary>
    [Fact]
    public void Idp_configuration_schema_is_available_on_disk()
    {
        var path = Path.Combine(Environment.WebRootPath, "Content", "IdpConfigurationSchema.json");

        Assert.True(File.Exists(path), $"Expected schema file '{path}' to exist.");
    }

    /// <summary>
    /// Verifies that the tenant JSON schema is still served from its legacy public URL with the
    /// <c>application/json</c> media type.
    /// </summary>
    [Fact]
    public async Task Idp_configuration_schema_is_served_as_json()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/Content/IdpConfigurationSchema.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Verifies that the per-tenant data files are not reachable as static content; they are only
    /// served through the tenant configuration endpoints.
    /// </summary>
    /// <param name="path">The request path that must not resolve to a data file.</param>
    [Theory]
    [InlineData("/App_Data/default.json")]
    [InlineData("/App_Data/stubidp.sustainsys.com.pfx")]
    public async Task Data_files_are_not_served_as_static_content(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that no certificate password is stored in a configuration file that lives in the
    /// repository. Passwords must come from user secrets or environment configuration only.
    /// </summary>
    /// <param name="fileName">The configuration file to inspect.</param>
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public async Task Repository_configuration_file_contains_no_certificate_password(string fileName)
    {
        var path = Path.Combine(Environment.ContentRootPath, fileName);
        Assert.True(File.Exists(path), $"Expected configuration file '{path}' to exist.");

        var content = await File.ReadAllTextAsync(path);

        Assert.DoesNotContain("Password", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that options validation is eager: a host with an invalid <c>StubIdp</c> section
    /// fails to start rather than serving requests with missing settings.
    /// </summary>
    [Fact]
    public void Invalid_configuration_fails_at_startup()
    {
        using var invalidFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["StubIdp:DataPath"] = "" })));

        var exception = Assert.Throws<OptionsValidationException>(
            () => invalidFactory.CreateClient());

        Assert.Contains("StubIdp:DataPath", exception.Message, StringComparison.Ordinal);
    }
}
