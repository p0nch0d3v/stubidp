using Sustainsys.Saml2.StubIdp.Models;

namespace Sustainsys.Saml2.StubIdp.Services;

/// <summary>
/// Stores and caches the per-tenant configuration files (<c>App_Data/{guid}.json</c>) of the
/// stub identity provider. Port of the file/caching logic in the legacy
/// <c>BaseController</c> (<c>legacy/Sustainsys.Saml2.StubIdp/Controllers/BaseController.cs</c>)
/// and of the save logic in the legacy <c>ManageController</c>.
/// </summary>
public interface ITenantConfigurationStore
{
    /// <summary>
    /// Gets the well-known GUID of the default tenant
    /// (<c>e73d98ff-0f1c-4cc2-8808-6d1bf028a8a9</c>). The default tenant is stored in
    /// <c>default.json</c> instead of <c>{guid}.json</c>.
    /// </summary>
    Guid DefaultTenantId { get; }

    /// <summary>
    /// Gets the configuration of the tenant <paramref name="idpId"/>, reading and caching the
    /// underlying file on first access.
    /// </summary>
    /// <param name="idpId">The tenant GUID.</param>
    /// <param name="cancellationToken">A token to cancel waiting for the read.</param>
    /// <returns>
    /// The tenant configuration, or <see langword="null"/> when no configuration file exists
    /// for the tenant. A <see langword="null"/> result is cached, like in the legacy
    /// implementation, until the tenant is saved through
    /// <see cref="SaveAsync(Guid, string, CancellationToken)"/>.
    /// </returns>
    Task<IdpConfigurationModel?> GetConfigurationAsync(Guid idpId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves tenant JSON: normalizes it to the indented on-disk representation, writes it to
    /// the tenant file and replaces the cached value.
    /// </summary>
    /// <param name="idpId">The tenant GUID.</param>
    /// <param name="jsonData">The tenant JSON to save.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <returns>The saved configuration, including the ETag of the normalized JSON.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="jsonData"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="System.Text.Json.JsonException"><paramref name="jsonData"/> is not
    /// valid JSON.</exception>
    Task<IdpConfigurationModel> SaveAsync(Guid idpId, string jsonData, CancellationToken cancellationToken = default);
}
