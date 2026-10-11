namespace Sustainsys.Saml2.StubIdp.Configuration;

/// <summary>
/// The set of signing certificates known to the stub identity provider, bound from the
/// <c>StubIdp:Certificates</c> configuration section.
/// </summary>
public class CertificatesOptions
{
    /// <summary>
    /// Gets or sets the certificate used for every request that does not match a host specific
    /// certificate.
    /// </summary>
    public CertificateOptions Default { get; set; } = new();

    /// <summary>
    /// Gets or sets the legacy Kentor certificate, kept so that service providers configured
    /// against the old <c>stubidp.kentor.se</c> host keep validating signatures.
    /// </summary>
    public CertificateOptions LegacyKentor { get; set; } = new();
}
