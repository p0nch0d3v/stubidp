using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using Microsoft.IdentityModel.Tokens.Saml2;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.Saml2P;
using Sustainsys.Saml2.StubIdp.Services;

namespace Sustainsys.Saml2.StubIdp.Models;

/// <summary>
/// The initiate-logout form model and factory for the emitted
/// <see cref="Saml2LogoutRequest"/>. Port of the legacy <c>InitiateLogoutModel</c> in
/// <c>legacy/Sustainsys.Saml2.StubIdp/Models/LogoutModels.cs</c>: the static
/// <c>UrlResolver</c>/<c>CertificateHelper</c> accesses are replaced by explicit parameters.
/// </summary>
public class InitiateLogoutModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InitiateLogoutModel"/> class with the
    /// default session index, as in the legacy model.
    /// </summary>
    public InitiateLogoutModel()
    {
        SessionIndex = AssertionModel.DefaultSessionIndex;
    }

    /// <summary>
    /// Gets or sets the single logout URL of the service provider.
    /// </summary>
    [Required]
    [Display(Name = "SP Single Logout Url")]
    public Uri? DestinationUrl { get; set; }

    /// <summary>
    /// Gets or sets the session index of the session to terminate. Defaults to
    /// <see cref="AssertionModel.DefaultSessionIndex"/>.
    /// </summary>
    [Required]
    [Display(Name = "Session index")]
    public string? SessionIndex { get; set; }

    /// <summary>
    /// Gets or sets the subject NameID of the session to terminate.
    /// </summary>
    [Required]
    [Display(Name = "Subject NameID")]
    public string? NameId { get; set; }

    /// <summary>
    /// Creates the <see cref="Saml2LogoutRequest"/> described by this model, signed with the
    /// SHA-256 algorithm as in the legacy implementation.
    /// </summary>
    /// <param name="urlResolver">The URL resolver for the current request, providing the
    /// issuer (metadata URL).</param>
    /// <param name="signingCertificate">The signing certificate selected for the current
    /// request host.</param>
    /// <returns>The logout request, to be bound by the caller.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public Saml2LogoutRequest ToLogoutRequest(
        StubIdpUrlResolver urlResolver,
        X509Certificate2 signingCertificate)
    {
        ArgumentNullException.ThrowIfNull(urlResolver);
        ArgumentNullException.ThrowIfNull(signingCertificate);

        return new Saml2LogoutRequest
        {
            DestinationUrl = DestinationUrl,
            SigningCertificate = signingCertificate,
            SigningAlgorithm = SignedXml.XmlDsigRSASHA256Url,
            Issuer = new EntityId(urlResolver.MetadataUrl.ToString()),
            NameId = new Saml2NameIdentifier(NameId!),
            SessionIndex = SessionIndex,
        };
    }
}

/// <summary>
/// The respond-to-logout form model (shown after receiving a <c>LogoutRequest</c>) and
/// factory for the emitted <see cref="Saml2LogoutResponse"/>. Port of the legacy
/// <c>RespondToLogoutRequestModel</c> in
/// <c>legacy/Sustainsys.Saml2.StubIdp/Models/LogoutModels.cs</c>.
/// </summary>
public class RespondToLogoutRequestModel
{
    /// <summary>
    /// Gets or sets the pretty-printed XML of the received logout request, for display.
    /// </summary>
    [Display(Name = "Received LogoutRequest")]
    public string? LogoutRequestXml { get; set; }

    /// <summary>
    /// Gets or sets the ID of the received logout request, echoed as InResponseTo.
    /// </summary>
    [Display(Name = "InResponseTo")]
    public string? InResponseTo { get; set; }

    /// <summary>
    /// Gets or sets the single logout URL of the service provider the response is sent to.
    /// </summary>
    [Required]
    [Display(Name = "SP Single Logout Url")]
    public Uri? DestinationUrl { get; set; }

    /// <summary>
    /// Gets or sets the opaque relay state echoed back to the service provider.
    /// </summary>
    [Display(Name = "Relay State")]
    public string? RelayState { get; set; }

    /// <summary>
    /// Creates the successful <see cref="Saml2LogoutResponse"/> described by this model,
    /// signed with the SHA-256 algorithm as in the legacy implementation.
    /// </summary>
    /// <param name="urlResolver">The URL resolver for the current request, providing the
    /// issuer (metadata URL).</param>
    /// <param name="signingCertificate">The signing certificate selected for the current
    /// request host.</param>
    /// <returns>The logout response, to be bound by the caller.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public Saml2LogoutResponse ToLogoutResponse(
        StubIdpUrlResolver urlResolver,
        X509Certificate2 signingCertificate)
    {
        ArgumentNullException.ThrowIfNull(urlResolver);
        ArgumentNullException.ThrowIfNull(signingCertificate);

        return new Saml2LogoutResponse(Saml2StatusCode.Success)
        {
            DestinationUrl = DestinationUrl,
            SigningCertificate = signingCertificate,
            SigningAlgorithm = SignedXml.XmlDsigRSASHA256Url,
            InResponseTo = new Saml2Id(InResponseTo!),
            Issuer = new EntityId(urlResolver.MetadataUrl.ToString()),
            RelayState = RelayState,
        };
    }
}

/// <summary>
/// The model of the received-logout-response view. Port of the legacy <c>ResponseModel</c>
/// in <c>legacy/Sustainsys.Saml2.StubIdp/Models/LogoutModels.cs</c>.
/// </summary>
public class ResponseModel
{
    /// <summary>
    /// Gets or sets the status of the received response, for display.
    /// </summary>
    [Display(Name = "Response Status")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the pretty-printed XML of the received response, for display.
    /// </summary>
    [Display(Name = "Response XML")]
    public string? ResponseXml { get; set; }
}
