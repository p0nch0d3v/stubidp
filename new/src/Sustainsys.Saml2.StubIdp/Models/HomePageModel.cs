using System.ComponentModel.DataAnnotations;

namespace Sustainsys.Saml2.StubIdp.Models;

/// <summary>
/// The model of the home page: the assertion form plus the tenant-specific description and
/// the hide-details flag from the tenant configuration. Port of
/// <c>legacy/Sustainsys.Saml2.StubIdp/Models/HomepageModel.cs</c>.
/// </summary>
public class HomePageModel
{
    /// <summary>
    /// Gets or sets the assertion model backing the SSO form.
    /// </summary>
    [Required]
    public AssertionModel? AssertionModel { get; set; }

    /// <summary>
    /// Gets or sets the custom description of the tenant, or <see langword="null"/> when the
    /// tenant configuration does not define one.
    /// </summary>
    public string? CustomDescription { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the details section of the page is hidden.
    /// </summary>
    public bool HideDetails { get; set; }
}
