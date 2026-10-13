using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sustainsys.Saml2.StubIdp.Services;

namespace Sustainsys.Saml2.StubIdp.Tests.Services;

/// <summary>
/// Tests for <see cref="StubIdpServiceCollectionExtensions.AddStubIdpCoreServices"/>.
/// </summary>
public class StubIdpServiceCollectionExtensionsTests
{
    /// <summary>
    /// Verifies that the core services are registered with the lifetimes required by their
    /// design: the certificate service and the tenant store are singletons, the URL resolver
    /// is scoped to the request.
    /// </summary>
    [Fact]
    public void Registers_core_services_with_expected_lifetimes()
    {
        var services = new ServiceCollection();

        services.AddStubIdpCoreServices();

        Assert.Contains(services, d =>
            d.ServiceType == typeof(CertificateService) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d =>
            d.ServiceType == typeof(StubIdpUrlResolver) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d =>
            d.ServiceType == typeof(ITenantConfigurationStore)
            && d.ImplementationType == typeof(TenantConfigurationStore)
            && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(IHttpContextAccessor));
    }

    /// <summary>
    /// Verifies that the extension returns the same service collection to allow chaining.
    /// </summary>
    [Fact]
    public void Returns_the_same_service_collection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddStubIdpCoreServices());
    }

    /// <summary>
    /// Verifies the null-argument guard.
    /// </summary>
    [Fact]
    public void Throws_for_null_services()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddStubIdpCoreServices());
    }
}
