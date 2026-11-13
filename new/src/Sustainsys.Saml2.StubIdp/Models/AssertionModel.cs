using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens.Saml2;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.Saml2P;
using Sustainsys.Saml2.StubIdp.Configuration;
using Sustainsys.Saml2.StubIdp.Services;
using Sustainsys.Saml2.WebSso;
using ClaimProperties = Microsoft.IdentityModel.Tokens.Saml2.ClaimProperties;

namespace Sustainsys.Saml2.StubIdp.Models;

/// <summary>
/// The assertion form model of the home page and factory for the emitted
/// <see cref="Saml2Response"/>. Port of
/// <c>legacy/Sustainsys.Saml2.StubIdp/Models/AssertionModel.cs</c>: the legacy static
/// <c>CreateFromConfiguration()</c> (reading <c>ConfigurationManager.AppSettings</c>) is
/// replaced by <see cref="Create(StubIdpOptions)"/>, and the static <c>UrlResolver</c> and
/// <c>CertificateHelper</c> accesses in <c>ToSaml2Response()</c> are replaced by explicit
/// parameters.
/// </summary>
public class AssertionModel
{
    /// <summary>
    /// The default session index value pre-filled on the assertion form. Legacy constant,
    /// preserved because it is externally visible in emitted assertions.
    /// </summary>
    public const string DefaultSessionIndex = "42";

    /// <summary>
    /// Gets or sets the assertion consumer service URL the response is sent to.
    /// </summary>
    [Required]
    [Display(Name = "Assertion Consumer Service Url")]
    public string? AssertionConsumerServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets the opaque relay state echoed back to the service provider.
    /// </summary>
    [Display(Name = "Relay State")]
    [StringLength(80)]
    public string? RelayState { get; set; }

    /// <summary>
    /// Gets or sets the subject NameID of the assertion.
    /// </summary>
    [Display(Name = "Subject NameId")]
    [Required]
    public string? NameId { get; set; }

    /// <summary>
    /// Gets or sets the audience restriction of the assertion, or <see langword="null"/> to
    /// emit no audience restriction.
    /// </summary>
    [Display(Name = "Audience")]
    public string? Audience { get; set; }

    /// <summary>
    /// Gets or sets the attribute statement rows (claim type/value pairs) of the assertion.
    /// </summary>
    public ICollection<AttributeStatementModel>? AttributeStatements { get; set; }

    /// <summary>
    /// Gets or sets the ID of an incoming AuthnRequest the response answers, or
    /// <see langword="null"/> for an unsolicited response.
    /// </summary>
    [Display(Name = "In Response To ID")]
    public string? InResponseTo { get; set; }

    /// <summary>
    /// Gets or sets the XML of the incoming AuthnRequest, for display in the form.
    /// </summary>
    [Display(Name = "Incoming AuthnRequest")]
    public string? AuthnRequestXml { get; set; }

    /// <summary>
    /// Gets or sets the binding the response is emitted with. Defaults to HTTP POST, as in
    /// the legacy model.
    /// </summary>
    public Saml2BindingType ResponseBinding { get; set; } = Saml2BindingType.HttpPost;

    /// <summary>
    /// Gets or sets the session index of the authentication statement.
    /// </summary>
    [Display(Name = "Session Index")]
    public string? SessionIndex { get; set; }

    /// <summary>
    /// Creates a new assertion model with the default values from the application options
    /// (legacy <c>CreateFromConfiguration()</c>, which read <c>appSettings:defaultAcsUrl</c>
    /// and <c>appSettings:defaultNameId</c>).
    /// </summary>
    /// <param name="options">The application options.</param>
    /// <returns>An <see cref="AssertionModel"/> pre-filled with the configured defaults.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is
    /// <see langword="null"/>.</exception>
    public static AssertionModel Create(StubIdpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new AssertionModel
        {
            AssertionConsumerServiceUrl = options.DefaultAcsUrl,
            NameId = options.DefaultNameId,
            SessionIndex = DefaultSessionIndex,
        };
    }

    /// <summary>
    /// Creates the <see cref="Saml2Response"/> described by this model: the issuer is the
    /// resolved metadata URL, the subject is the NameID claim (with the legacy
    /// <c>NameIdFormat.Unspecified</c> format), the session index is included as a claim when
    /// non-empty, and every attribute statement row becomes a claim.
    /// </summary>
    /// <param name="urlResolver">The URL resolver for the current request, providing the
    /// issuer (metadata URL).</param>
    /// <param name="signingCertificate">The signing certificate selected for the current
    /// request host.</param>
    /// <returns>The unsigned <see cref="Saml2Response"/>, to be bound by the caller.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public Saml2Response ToSaml2Response(StubIdpUrlResolver urlResolver, X509Certificate2 signingCertificate)
    {
        ArgumentNullException.ThrowIfNull(urlResolver);
        ArgumentNullException.ThrowIfNull(signingCertificate);

        var nameIdClaim = new Claim(ClaimTypes.NameIdentifier, NameId!);
        nameIdClaim.Properties[ClaimProperties.SamlNameIdentifierFormat] =
            NameIdFormat.Unspecified.GetUri().AbsoluteUri;

        var claims =
            new[] { nameIdClaim }
            .Concat(YieldIfNotNullOrEmpty(SessionIndex).Select(
                s => new Claim(Saml2ClaimTypes.SessionIndex, s)))
            .Concat((AttributeStatements ?? Enumerable.Empty<AttributeStatementModel>())
                .Select(att => new Claim(att.Type!, att.Value!)));

        var identity = new ClaimsIdentity(claims);

        Saml2Id? saml2Id = null;
        if (!string.IsNullOrEmpty(InResponseTo))
        {
            saml2Id = new Saml2Id(InResponseTo);
        }

        var audienceUrl = string.IsNullOrEmpty(Audience)
            ? null
            : new Uri(Audience);

        return new Saml2Response(
            new EntityId(urlResolver.MetadataUrl.ToString()),
            signingCertificate,
            new Uri(AssertionConsumerServiceUrl!),
            saml2Id,
            RelayState,
            audienceUrl,
            identity);
    }

    private static IEnumerable<string> YieldIfNotNullOrEmpty(string? src)
    {
        if (!string.IsNullOrEmpty(src))
        {
            yield return src;
        }
    }
}
