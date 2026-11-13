using System.ComponentModel.DataAnnotations;
using Sustainsys.Saml2.StubIdp.Services;

namespace Sustainsys.Saml2.StubIdp.Models;

/// <summary>
/// The discovery service form model. Port of
/// <c>legacy/Sustainsys.Saml2.StubIdp/Models/DiscoveryServiceModel.cs</c>. The property names
/// deliberately keep the lowercase IdP Discovery Service Protocol parameter names
/// (<c>entityID</c>, <c>return</c>, <c>returnIDParam</c>, <c>isPassive</c>) so that form and
/// query-string binding stays character-for-character compatible with the legacy application.
/// The legacy constructor read the static <c>UrlResolver</c>; the request-specific defaults
/// are now supplied by <see cref="Create(StubIdpUrlResolver)"/>.
/// </summary>
public class DiscoveryServiceModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryServiceModel"/> class with the
    /// protocol default <c>returnIDParam</c>.
    /// </summary>
    public DiscoveryServiceModel()
    {
        returnIDParam = "entityID";
    }

    /// <summary>
    /// Gets or sets the entity ID of the requesting service provider.
    /// </summary>
    [Display(Name = "SP Entity ID")]
    public string? entityID { get; set; }

    /// <summary>
    /// Gets or sets the URL the discovery response redirects back to.
    /// </summary>
    [Display(Name = "Return URL to SP")]
    [Required]
    public string? @return { get; set; }

    /// <summary>
    /// Gets or sets the query string parameter name the selected IdP entity ID is appended
    /// as. Defaults to <c>entityID</c>.
    /// </summary>
    [Display(Name = "Return ID Param")]
    [Required]
    public string? returnIDParam { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the discovery service must not interact with
    /// the user; a passive request is answered with an immediate redirect.
    /// </summary>
    public bool isPassive { get; set; }

    /// <summary>
    /// Gets or sets the entity ID of the selected identity provider. Defaults to this
    /// instance's metadata URL, see <see cref="Create(StubIdpUrlResolver)"/>.
    /// </summary>
    [Display(Name = "Entity ID of Selected Idp")]
    [Required]
    public string? SelectedIdp { get; set; }

    /// <summary>
    /// Creates a discovery service model pre-filled with the request-specific defaults: the
    /// protocol default <c>returnIDParam</c> and this instance's metadata URL as the selected
    /// IdP entity ID (legacy constructor behavior).
    /// </summary>
    /// <param name="urlResolver">The URL resolver for the current request.</param>
    /// <returns>A pre-filled <see cref="DiscoveryServiceModel"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="urlResolver"/> is
    /// <see langword="null"/>.</exception>
    public static DiscoveryServiceModel Create(StubIdpUrlResolver urlResolver)
    {
        ArgumentNullException.ThrowIfNull(urlResolver);

        return new DiscoveryServiceModel
        {
            SelectedIdp = urlResolver.MetadataUrl.ToString(),
        };
    }
}
