namespace Sustainsys.Saml2.StubIdp.Services;

/// <summary>
/// Registration helpers for the stub identity provider core services.
/// </summary>
public static class StubIdpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core services of the stub identity provider:
    /// <see cref="CertificateService"/> (singleton, owns the signing certificates),
    /// <see cref="StubIdpUrlResolver"/> (scoped, requires an active request) and
    /// <see cref="ITenantConfigurationStore"/> (singleton), plus the
    /// <see cref="IHttpContextAccessor"/> the URL resolver depends on.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same <paramref name="services"/> instance, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is
    /// <see langword="null"/>.</exception>
    public static IServiceCollection AddStubIdpCoreServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddSingleton<CertificateService>();
        services.AddScoped<StubIdpUrlResolver>();
        services.AddSingleton<ITenantConfigurationStore, TenantConfigurationStore>();

        return services;
    }
}
