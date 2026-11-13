using System.ComponentModel.DataAnnotations;

namespace Sustainsys.Saml2.StubIdp.Models;

/// <summary>
/// A single attribute statement row (claim type/value pair) of the assertion form on the home
/// page. Port of <c>legacy/Sustainsys.Saml2.StubIdp/Models/AttributeStatementModel.cs</c>.
/// </summary>
public class AttributeStatementModel
{
    /// <summary>
    /// Gets or sets the claim type (attribute name) of the row.
    /// </summary>
    [Required]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the claim value (attribute value) of the row.
    /// </summary>
    [Required]
    public string? Value { get; set; }
}
