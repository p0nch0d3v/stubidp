namespace Sustainsys.Saml2.StubIdp.Configuration;

/// <summary>
/// Describes a single signing certificate used by the stub identity provider.
/// </summary>
/// <remarks>
/// The legacy application hard coded two certificates in
/// <c>legacy/Sustainsys.Saml2.StubIdp/CertificateHelper.cs</c> and selected between them on the
/// request <c>Host</c> header. Both are now configured through the <c>StubIdp:Certificates</c>
/// section instead.
/// </remarks>
public class CertificateOptions
{
    /// <summary>
    /// Gets or sets the path to the PKCS#12 (<c>.pfx</c>) file holding the certificate and its
    /// private key. Relative paths are resolved against the application content root.
    /// </summary>
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request host name that selects this certificate, or <see langword="null"/>
    /// when the certificate is not bound to a specific host. The legacy behavior is to use the
    /// Kentor certificate when the request host is <c>stubidp.kentor.se</c>.
    /// </summary>
    public string? HostName { get; set; }

    /// <summary>
    /// Gets or sets the password protecting the private key of <see cref="File"/>.
    /// </summary>
    /// <remarks>
    /// This value must never be stored in <c>appsettings.json</c> or any other tracked file. Use
    /// <c>dotnet user-secrets</c> in development and environment variables or a secret store in
    /// other environments. <see langword="null"/> and the empty string are both treated as
    /// "no password", which is what the bundled test certificates use.
    /// </remarks>
    public string? Password { get; set; }
}
