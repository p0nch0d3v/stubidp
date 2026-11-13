using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.WebSso;

namespace Sustainsys.Saml2.StubIdp.Services;

/// <summary>
/// Builds the metadata descriptors of the stub identity provider: the IdP
/// <see cref="EntityDescriptor"/> and the wrapping federation
/// <see cref="EntitiesDescriptor"/>. Port of the legacy static <c>MetadataModel</c>
/// (<c>legacy/Sustainsys.Saml2.StubIdp/Models/MetadataModel.cs</c>): the static
/// <c>UrlResolver</c> and <c>CertificateHelper</c> accesses are replaced by the injected
/// <see cref="StubIdpUrlResolver"/> and <see cref="CertificateService"/>.
/// </summary>
/// <remarks>
/// The service is registered as scoped: it depends on the scoped URL resolver and can only
/// be used while a request is being processed. The descriptors are unsigned when returned;
/// signing happens in the metadata endpoints (Task06), as in the legacy controllers.
/// </remarks>
public class MetadataService(
    StubIdpUrlResolver urlResolver,
    CertificateService certificateService,
    IHttpContextAccessor httpContextAccessor)
{
    private const string Saml2ProtocolUri = "urn:oasis:names:tc:SAML:2.0:protocol";

    /// <summary>
    /// The name of the federation metadata, preserved from the legacy implementation because
    /// it is externally visible.
    /// </summary>
    public const string FederationName = "Sustainsys.Saml2.StubIdp Federation";

    /// <summary>
    /// Creates the IdP metadata: Redirect and POST single sign-on endpoints, the SOAP
    /// artifact resolution service (index 0, default), Redirect and POST single logout
    /// endpoints, and the signing key selected for the current request host. The entity ID is
    /// the resolved metadata URL, which reflects the tenant GUID segment on named routes.
    /// </summary>
    /// <param name="includeCacheDuration">When <see langword="true"/> (the default), sets the
    /// 15-minute cache duration and one-day validity, as in the legacy implementation. The
    /// federation metadata passes <see langword="false"/> so that the child entity does not
    /// duplicate the federation's own cache metadata.</param>
    /// <returns>The IdP entity descriptor.</returns>
    /// <exception cref="InvalidOperationException">There is no active
    /// <see cref="HttpContext"/>; the service can only be used while a request is being
    /// processed.</exception>
    public EntityDescriptor CreateIdpMetadata(bool includeCacheDuration = true)
    {
        var metadata = new EntityDescriptor
        {
            EntityId = new EntityId(urlResolver.MetadataUrl.ToString()),
        };

        if (includeCacheDuration)
        {
            metadata.CacheDuration = new XsdDuration(minutes: 15);
            metadata.ValidUntil = DateTime.UtcNow.AddDays(1);
        }

        var idpSsoDescriptor = new IdpSsoDescriptor();
        idpSsoDescriptor.ProtocolsSupported.Add(new Uri(Saml2ProtocolUri));
        metadata.RoleDescriptors.Add(idpSsoDescriptor);

        idpSsoDescriptor.SingleSignOnServices.Add(new SingleSignOnService
        {
            Binding = Saml2Binding.HttpRedirectUri,
            Location = urlResolver.SsoServiceUrl,
        });
        idpSsoDescriptor.SingleSignOnServices.Add(new SingleSignOnService
        {
            Binding = Saml2Binding.HttpPostUri,
            Location = urlResolver.SsoServiceUrl,
        });

        idpSsoDescriptor.ArtifactResolutionServices.Add(0, new ArtifactResolutionService
        {
            Index = 0,
            IsDefault = true,
            Binding = Saml2Binding.SoapUri,
            Location = urlResolver.ArtifactServiceUrl,
        });

        idpSsoDescriptor.SingleLogoutServices.Add(new SingleLogoutService
        {
            Binding = Saml2Binding.HttpRedirectUri,
            Location = urlResolver.LogoutServiceUrl,
        });
        idpSsoDescriptor.SingleLogoutServices.Add(new SingleLogoutService
        {
            Binding = Saml2Binding.HttpPostUri,
            Location = urlResolver.LogoutServiceUrl,
        });

        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "MetadataService requires an active HttpContext; it can only be used while a request is being processed.");

        idpSsoDescriptor.Keys.Add(certificateService.GetSigningKeyDescriptor(context));

        return metadata;
    }

    /// <summary>
    /// Creates the federation metadata: an <see cref="EntitiesDescriptor"/> named
    /// <see cref="FederationName"/> with a 15-minute cache duration and one-day validity,
    /// wrapping the IdP metadata without duplicating its cache metadata.
    /// </summary>
    /// <returns>The federation entities descriptor.</returns>
    /// <exception cref="InvalidOperationException">There is no active
    /// <see cref="HttpContext"/>.</exception>
    public EntitiesDescriptor CreateFederationMetadata()
    {
        var metadata = new EntitiesDescriptor
        {
            Name = FederationName,
            CacheDuration = new XsdDuration(minutes: 15),
            ValidUntil = DateTime.UtcNow.AddDays(1),
        };

        metadata.ChildEntities.Add(CreateIdpMetadata(false));

        return metadata;
    }
}
