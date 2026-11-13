using Microsoft.AspNetCore.Http;
using Sustainsys.Saml2.StubIdp.Models;
using Sustainsys.Saml2.StubIdp.Services;

namespace Sustainsys.Saml2.StubIdp.Tests.Models;

/// <summary>
/// Tests for <see cref="DiscoveryServiceModel"/>: the protocol defaults preserved from the
/// legacy constructor and the request-specific <see cref="DiscoveryServiceModel.Create"/>
/// factory replacing the legacy static <c>UrlResolver</c> access.
/// </summary>
public class DiscoveryServiceModelTests
{
    private const string TenantId = "2273dd5a-8e86-4abc-9078-236215f06c0e";

    private static StubIdpUrlResolver CreateResolver(string path = "/DiscoveryService")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = path;
        return new StubIdpUrlResolver(new HttpContextAccessor { HttpContext = context });
    }

    /// <summary>
    /// Verifies the IdP Discovery Service Protocol default of the return ID parameter.
    /// </summary>
    [Fact]
    public void Parameterless_constructor_defaults_returnIDParam_to_entityID()
    {
        Assert.Equal("entityID", new DiscoveryServiceModel().returnIDParam);
    }

    /// <summary>
    /// Verifies that the factory pre-fills the selected IdP with the resolved metadata URL
    /// (legacy constructor behavior) and keeps the return ID parameter default.
    /// </summary>
    [Fact]
    public void Create_prefills_SelectedIdp_with_metadata_url()
    {
        var model = DiscoveryServiceModel.Create(CreateResolver());

        Assert.Equal("https://localhost/Metadata", model.SelectedIdp);
        Assert.Equal("entityID", model.returnIDParam);
    }

    /// <summary>
    /// Verifies that the tenant GUID segment is reflected into the pre-filled selected IdP
    /// entity ID on named routes.
    /// </summary>
    [Fact]
    public void Create_reflects_tenant_guid_on_named_route()
    {
        var model = DiscoveryServiceModel.Create(CreateResolver($"/{TenantId}/DiscoveryService"));

        Assert.Equal($"https://localhost/{TenantId}/Metadata", model.SelectedIdp);
    }

    /// <summary>
    /// Verifies the null-argument guard on the factory.
    /// </summary>
    [Fact]
    public void Create_throws_for_null_url_resolver()
    {
        Assert.Throws<ArgumentNullException>(() => DiscoveryServiceModel.Create(null!));
    }
}
