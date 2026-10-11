namespace Sustainsys.Saml2.StubIdp.Configuration;

/// <summary>
/// Strongly typed settings for the stub identity provider, bound from the <c>StubIdp</c>
/// configuration section.
/// </summary>
/// <remarks>
/// Replaces the legacy <c>appSettings</c> entries in
/// <c>legacy/Sustainsys.Saml2.StubIdp/Web.config</c> (<c>defaultAcsUrl</c>,
/// <c>defaultNameId</c>) and the hard coded <c>Server.MapPath("~/App_Data/...")</c> lookups.
/// </remarks>
public class StubIdpOptions
{
    /// <summary>
    /// The name of the configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "StubIdp";

    /// <summary>
    /// Gets or sets the assertion consumer service URL pre-filled on the home page when no
    /// incoming authentication request supplies one. Legacy <c>appSettings:defaultAcsUrl</c>.
    /// </summary>
    public string DefaultAcsUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name identifier pre-filled on the home page. Legacy
    /// <c>appSettings:defaultNameId</c>.
    /// </summary>
    public string DefaultNameId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the directory holding the per-tenant configuration files and the signing
    /// certificates. Relative paths are resolved against the application content root, which is
    /// the modern equivalent of the legacy <c>~/App_Data</c> mapping.
    /// </summary>
    public string DataPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the signing certificates available to the application.
    /// </summary>
    public CertificatesOptions Certificates { get; set; } = new();
}
