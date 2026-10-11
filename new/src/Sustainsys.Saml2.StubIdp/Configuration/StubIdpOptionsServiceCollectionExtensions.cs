using Microsoft.Extensions.Options;

namespace Sustainsys.Saml2.StubIdp.Configuration;

/// <summary>
/// Registration helpers for the stub identity provider configuration.
/// </summary>
public static class StubIdpOptionsServiceCollectionExtensions
{
    /// <summary>
    /// Binds the <c>StubIdp</c> configuration section to <see cref="StubIdpOptions"/>, registers
    /// <see cref="StubIdpOptionsValidator"/> and enables eager validation at application startup.
    /// </summary>
    /// <param name="services">The service collection to add the options to.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <returns>The same <paramref name="services"/> instance, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or
    /// <paramref name="configuration"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddStubIdpOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<StubIdpOptions>()
            .Bind(configuration.GetSection(StubIdpOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<StubIdpOptions>, StubIdpOptionsValidator>();

        return services;
    }
}
