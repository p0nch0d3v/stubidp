using System.ComponentModel.DataAnnotations;

namespace Sustainsys.Saml2.StubIdp.Models;

/// <summary>
/// The model of the tenant management form, holding the raw tenant configuration JSON. Port
/// of <c>legacy/Sustainsys.Saml2.StubIdp/Models/ManageIdpModel.cs</c>.
/// </summary>
public class ManageIdpModel
{
    /// <summary>
    /// Gets or sets the tenant configuration JSON edited on the management page.
    /// </summary>
    [Required]
    [Display(Name = "Configuration data")]
    [DataType(DataType.MultilineText)]
    public string? JsonData { get; set; }
}
