using Microsoft.AspNetCore.Http;
using Sustainsys.Saml2.StubIdp.Services;

namespace Sustainsys.Saml2.StubIdp.Tests.Services;

/// <summary>
/// Tests for <see cref="StubIdpUrlResolver"/>: absolute URL composition with and without a
/// tenant GUID segment and an application path base.
/// </summary>
public class StubIdpUrlResolverTests
{
    private const string TenantId = "2273dd5a-8e86-4abc-9078-236215f06c0e";

    private static StubIdpUrlResolver CreateResolver(
        string scheme = "https",
        string host = "localhost",
        string pathBase = "",
        string path = "/")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = HostString.FromUriComponent(host);
        context.Request.PathBase = pathBase;
        context.Request.Path = path;

        return new StubIdpUrlResolver(new HttpContextAccessor { HttpContext = context });
    }

    /// <summary>
    /// Verifies the endpoint URLs on the default route without a tenant GUID.
    /// </summary>
    [Fact]
    public void Default_route_produces_plain_endpoint_urls()
    {
        var resolver = CreateResolver(path: "/");

        Assert.Equal("https://localhost/", resolver.RootUrl.ToString());
        Assert.Equal("https://localhost/", resolver.SsoServiceUrl.ToString());
        Assert.Equal("https://localhost/Metadata", resolver.MetadataUrl.ToString());
        Assert.Equal("https://localhost/Manage", resolver.ManageUrl.ToString());
        Assert.Equal("https://localhost/ArtifactResolve", resolver.ArtifactServiceUrl.ToString());
        Assert.Equal("https://localhost/Logout", resolver.LogoutServiceUrl.ToString());
    }

    /// <summary>
    /// Verifies that a leading tenant GUID segment is reflected into every generated URL.
    /// </summary>
    [Fact]
    public void Named_route_reflects_the_tenant_guid_into_every_url()
    {
        var resolver = CreateResolver(path: $"/{TenantId}/Metadata");

        Assert.Equal($"https://localhost/{TenantId}/", resolver.RootUrl.ToString());
        Assert.Equal($"https://localhost/{TenantId}/", resolver.SsoServiceUrl.ToString());
        Assert.Equal($"https://localhost/{TenantId}/Metadata", resolver.MetadataUrl.ToString());
        Assert.Equal($"https://localhost/{TenantId}/Manage", resolver.ManageUrl.ToString());
        Assert.Equal($"https://localhost/{TenantId}/ArtifactResolve", resolver.ArtifactServiceUrl.ToString());
        Assert.Equal($"https://localhost/{TenantId}/Logout", resolver.LogoutServiceUrl.ToString());
    }

    /// <summary>
    /// Verifies that a GUID-only request path (the named home route) is recognized.
    /// </summary>
    [Fact]
    public void Guid_only_path_is_recognized_as_a_tenant_segment()
    {
        var resolver = CreateResolver(path: $"/{TenantId}");

        Assert.Equal($"https://localhost/{TenantId}/", resolver.RootUrl.ToString());
        Assert.Equal($"https://localhost/{TenantId}/Metadata", resolver.MetadataUrl.ToString());
    }

    /// <summary>
    /// Verifies that the path base is preserved without duplicating segments (the assertion
    /// from the task specification).
    /// </summary>
    [Fact]
    public void Path_base_is_preserved_in_front_of_the_tenant_guid()
    {
        var resolver = CreateResolver(pathBase: "/base", path: $"/{TenantId}/Metadata");

        Assert.Equal($"https://localhost/base/{TenantId}/Metadata", resolver.MetadataUrl.ToString());
        Assert.Equal($"https://localhost/base/{TenantId}/", resolver.RootUrl.ToString());
    }

    /// <summary>
    /// Verifies the path base without a tenant GUID.
    /// </summary>
    [Fact]
    public void Path_base_without_tenant_guid()
    {
        var resolver = CreateResolver(pathBase: "/base", path: "/Metadata");

        Assert.Equal("https://localhost/base/", resolver.RootUrl.ToString());
        Assert.Equal("https://localhost/base/Metadata", resolver.MetadataUrl.ToString());
    }

    /// <summary>
    /// Verifies that a non-GUID first segment is not mistaken for a tenant.
    /// </summary>
    [Fact]
    public void Non_guid_first_segment_is_not_a_tenant_segment()
    {
        var resolver = CreateResolver(path: "/Home/Index");

        Assert.Equal("https://localhost/Metadata", resolver.MetadataUrl.ToString());
    }

    /// <summary>
    /// Verifies that the legacy braced GUID format accepted by the legacy route constraint is
    /// still recognized and preserved as-is, like the legacy implementation did.
    /// </summary>
    [Fact]
    public void Braced_guid_segment_is_recognized_and_preserved()
    {
        var resolver = CreateResolver(path: $"/{{{TenantId}}}/Metadata");

        Assert.Equal($"https://localhost/{{{TenantId}}}/Metadata", resolver.MetadataUrl.ToString());
    }

    /// <summary>
    /// Verifies that scheme, host and a non-default port are preserved.
    /// </summary>
    [Fact]
    public void Scheme_host_and_port_are_preserved()
    {
        var resolver = CreateResolver(scheme: "http", host: "example.com:5001", path: "/");

        Assert.Equal("http://example.com:5001/", resolver.RootUrl.ToString());
        Assert.Equal("http://example.com:5001/Metadata", resolver.MetadataUrl.ToString());
    }

    /// <summary>
    /// Verifies that the resolver fails with a clear error when no request is active, instead
    /// of a null-reference exception.
    /// </summary>
    [Fact]
    public void Missing_active_request_throws_a_clear_error()
    {
        var resolver = new StubIdpUrlResolver(new HttpContextAccessor());

        Assert.Throws<InvalidOperationException>(() => resolver.MetadataUrl);
    }

    /// <summary>
    /// Verifies the null-argument guard on the constructor.
    /// </summary>
    [Fact]
    public void Constructor_throws_for_null_accessor()
    {
        Assert.Throws<ArgumentNullException>(() => new StubIdpUrlResolver(null!));
    }
}
