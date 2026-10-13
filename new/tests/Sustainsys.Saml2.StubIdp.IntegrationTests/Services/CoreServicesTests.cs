using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Sustainsys.Saml2.StubIdp.Models;
using Sustainsys.Saml2.StubIdp.Services;

namespace Sustainsys.Saml2.StubIdp.IntegrationTests.Services;

/// <summary>
/// Verifies that the core services are registered in the hosted application and work against
/// the real configuration and data files: certificate selection per host, and tenant reads
/// through <see cref="ITenantConfigurationStore"/>.
/// </summary>
public class CoreServicesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid DefaultTenantGuid = Guid.Parse("e73d98ff-0f1c-4cc2-8808-6d1bf028a8a9");
    private static readonly Guid SampleTenantGuid = Guid.Parse("2273dd5a-8e86-4abc-9078-236215f06c0e");

    private readonly WebApplicationFactory<Program> factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoreServicesTests"/> class.
    /// </summary>
    /// <param name="factory">The test host factory for the StubIdp application.</param>
    public CoreServicesTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        this.factory = factory;
    }

    private string ContentRootPath =>
        factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;

    private static HttpContext CreateContext(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        return context;
    }

    private string LoadPublicCertificateThumbprint(string fileName) =>
        System.Security.Cryptography.X509Certificates.X509CertificateLoader
            .LoadCertificateFromFile(Path.Combine(ContentRootPath, "App_Data", fileName))
            .Thumbprint;

    /// <summary>
    /// Verifies that the certificate service is hosted as a singleton and selects the legacy
    /// Kentor certificate for the legacy host name.
    /// </summary>
    [Fact]
    public void Certificate_service_selects_the_legacy_certificate_for_the_legacy_host()
    {
        var service = factory.Services.GetRequiredService<CertificateService>();

        var certificate = service.GetSigningCertificate(CreateContext("stubidp.kentor.se"));

        Assert.Equal(LoadPublicCertificateThumbprint("Kentor.AuthServices.StubIdp.cer"), certificate.Thumbprint);
    }

    /// <summary>
    /// Verifies that any other host selects the default certificate.
    /// </summary>
    [Fact]
    public void Certificate_service_selects_the_default_certificate_for_other_hosts()
    {
        var service = factory.Services.GetRequiredService<CertificateService>();

        var certificate = service.GetSigningCertificate(CreateContext("localhost"));

        Assert.Equal(LoadPublicCertificateThumbprint("stubidp.sustainsys.com.cer"), certificate.Thumbprint);
    }

    /// <summary>
    /// Verifies that the tenant store serves the bundled <c>default.json</c> with the exact
    /// content and its uppercase MD5 ETag.
    /// </summary>
    [Fact]
    public async Task Tenant_store_reads_the_default_tenant()
    {
        var store = factory.Services.GetRequiredService<ITenantConfigurationStore>();

        var model = await store.GetConfigurationAsync(DefaultTenantGuid);

        Assert.NotNull(model);
        var json = await File.ReadAllTextAsync(Path.Combine(ContentRootPath, "App_Data", "default.json"));
        Assert.Equal(json, model.JsonData);
        Assert.Equal(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(json))), model.ETag);
    }

    /// <summary>
    /// Verifies that the tenant store serves the bundled sample tenant.
    /// </summary>
    [Fact]
    public async Task Tenant_store_reads_the_sample_tenant()
    {
        var store = factory.Services.GetRequiredService<ITenantConfigurationStore>();

        var model = await store.GetConfigurationAsync(SampleTenantGuid);

        Assert.NotNull(model);
        Assert.Equal("This is my custom IDP description", model.IdpDescription);
        Assert.True(model.HideDetails);
    }

    /// <summary>
    /// Verifies that an unknown tenant GUID yields null.
    /// </summary>
    [Fact]
    public async Task Tenant_store_returns_null_for_an_unknown_tenant()
    {
        var store = factory.Services.GetRequiredService<ITenantConfigurationStore>();

        Assert.Null(await store.GetConfigurationAsync(Guid.NewGuid()));
    }

    /// <summary>
    /// Verifies that the store exposes the legacy default tenant GUID.
    /// </summary>
    [Fact]
    public void Tenant_store_exposes_the_default_tenant_guid()
    {
        var store = factory.Services.GetRequiredService<ITenantConfigurationStore>();

        Assert.Equal(DefaultTenantGuid, store.DefaultTenantId);
    }

    /// <summary>
    /// Verifies that the scoped URL resolver can be constructed from the hosted service
    /// provider.
    /// </summary>
    [Fact]
    public void Url_resolver_resolves_from_a_scope()
    {
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<StubIdpUrlResolver>());
    }
}
