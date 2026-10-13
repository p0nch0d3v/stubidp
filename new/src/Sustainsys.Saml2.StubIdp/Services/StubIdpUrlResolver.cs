namespace Sustainsys.Saml2.StubIdp.Services;

/// <summary>
/// Builds the absolute URLs of the stub identity provider endpoints for the current request.
/// Port of the legacy static <c>UrlResolver</c>
/// (<c>legacy/Sustainsys.Saml2.StubIdp/UrlResolver.cs</c>): the scheme, host and path base of
/// the current request are preserved, and a leading tenant GUID path segment (the named-IdP
/// route <c>{idpId}/{controller}/{action}</c>) is reflected into every generated URL.
/// </summary>
/// <remarks>
/// The service is registered as scoped and can only be used while a request is being
/// processed.
/// </remarks>
public class StubIdpUrlResolver(IHttpContextAccessor httpContextAccessor)
{
    private readonly IHttpContextAccessor httpContextAccessor =
        httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

    /// <summary>
    /// Gets the absolute root URL of the application, including the tenant GUID segment when
    /// the current request is on a named-IdP route.
    /// </summary>
    public Uri RootUrl => GetCombinedUrl("");

    /// <summary>
    /// Gets the absolute URL of the single sign-on service, which is the application root.
    /// </summary>
    public Uri SsoServiceUrl => RootUrl;

    /// <summary>
    /// Gets the absolute URL of the metadata endpoint.
    /// </summary>
    public Uri MetadataUrl => GetCombinedUrl("Metadata");

    /// <summary>
    /// Gets the absolute URL of the tenant management endpoint.
    /// </summary>
    public Uri ManageUrl => GetCombinedUrl("Manage");

    /// <summary>
    /// Gets the absolute URL of the artifact resolution endpoint.
    /// </summary>
    public Uri ArtifactServiceUrl => GetCombinedUrl("ArtifactResolve");

    /// <summary>
    /// Gets the absolute URL of the logout endpoint.
    /// </summary>
    public Uri LogoutServiceUrl => GetCombinedUrl("Logout");

    private Uri GetCombinedUrl(string path)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "StubIdpUrlResolver requires an active HttpContext; it can only be used while a request is being processed.");

        var request = context.Request;

        var basePath = request.PathBase.Value ?? string.Empty;
        if (!basePath.EndsWith('/'))
        {
            basePath += "/";
        }

        // A leading GUID segment selects a named tenant and is reflected into every generated
        // URL. The raw segment is kept as-is (legacy Guid.TryParse behavior, which also
        // accepts the "{guid}" format allowed by the legacy route constraint).
        var firstSegment = request.Path.Value?.TrimStart('/').Split('/').FirstOrDefault() ?? string.Empty;
        var idpSegment = Guid.TryParse(firstSegment, out _) ? firstSegment + "/" : string.Empty;

        return new Uri(
            $"{request.Scheme}://{request.Host.ToUriComponent()}{basePath}{idpSegment}{path}",
            UriKind.Absolute);
    }
}
