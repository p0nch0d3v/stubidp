using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.StubIdp.Configuration;
using Sustainsys.Saml2.StubIdp.Services;
using Sustainsys.Saml2.WebSso;

namespace Sustainsys.Saml2.StubIdp.Tests.Services;

/// <summary>
/// Tests for <see cref="MetadataService"/>: the port of the legacy static
/// <c>MetadataModel</c> to a service building the IdP and federation metadata descriptors.
/// </summary>
public class MetadataServiceTests
{
    private const string TenantId = "2273dd5a-8e86-4abc-9078-236215f06c0e";
    private static readonly Uri Saml2ProtocolUri = new("urn:oasis:names:tc:SAML:2.0:protocol");
    private static readonly XNamespace Md = "urn:oasis:names:tc:SAML:2.0:metadata";
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    private static HttpContextAccessor CreateAccessor(string host = "localhost", string path = "/Metadata")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        return new HttpContextAccessor { HttpContext = context };
    }

    private static MetadataService CreateService(string host = "localhost", string path = "/Metadata") =>
        CreateServiceWithDependencies(host, path).Service;

    private static (MetadataService Service, CertificateService Certificates, HttpContext Context)
        CreateServiceWithDependencies(string host = "localhost", string path = "/Metadata")
    {
        var accessor = CreateAccessor(host, path);

        var options = new StubIdpOptions
        {
            DataPath = "App_Data",
            Certificates = new CertificatesOptions
            {
                Default = new CertificateOptions { File = "App_Data/stubidp.sustainsys.com.pfx" },
                LegacyKentor = new CertificateOptions
                {
                    File = "App_Data/Kentor.AuthServices.StubIdp.pfx",
                    HostName = "stubidp.kentor.se",
                },
            },
        };

        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

        var certificateService = new CertificateService(
            Options.Create(options), environment.Object, NullLogger<CertificateService>.Instance);

        return (
            new MetadataService(new StubIdpUrlResolver(accessor), certificateService, accessor),
            certificateService,
            accessor.HttpContext!);
    }

    private static string LoadDefaultCertificateThumbprint() =>
        X509CertificateLoader.LoadCertificateFromFile(
            Path.Combine(AppContext.BaseDirectory, "App_Data", "stubidp.sustainsys.com.cer")).Thumbprint;

    private static IdpSsoDescriptor SingleIdpSsoDescriptor(EntityDescriptor metadata) =>
        Assert.IsType<IdpSsoDescriptor>(Assert.Single(metadata.RoleDescriptors));

    /// <summary>
    /// Verifies the entity ID and the 15-minute cache duration / one-day validity of the IdP
    /// metadata, asserting the validity against a future range so that the test does not
    /// depend on the exact wall clock.
    /// </summary>
    [Fact]
    public void Idp_metadata_has_entity_id_and_cache_metadata()
    {
        var service = CreateService();

        var metadata = service.CreateIdpMetadata();

        Assert.Equal("https://localhost/Metadata", metadata.EntityId.Id);
        Assert.Equal(15, metadata.CacheDuration!.Value.Minutes);
        Assert.InRange(
            metadata.ValidUntil!.Value,
            DateTime.UtcNow.AddHours(23),
            DateTime.UtcNow.AddHours(25));
    }

    /// <summary>
    /// Verifies that the tenant GUID segment of a named route is reflected into the entity ID
    /// and that the legacy host header selects the legacy signing key.
    /// </summary>
    [Fact]
    public void Idp_metadata_reflects_tenant_guid_and_host_certificate()
    {
        var service = CreateService(host: "stubidp.kentor.se", path: $"/{TenantId}/Metadata");

        var metadata = service.CreateIdpMetadata();

        Assert.Equal($"https://stubidp.kentor.se/{TenantId}/Metadata", metadata.EntityId.Id);

        var legacyThumbprint = X509CertificateLoader.LoadCertificateFromFile(
            Path.Combine(AppContext.BaseDirectory, "App_Data", "Kentor.AuthServices.StubIdp.cer")).Thumbprint;
        var keyDescriptor = Assert.Single(SingleIdpSsoDescriptor(metadata).Keys);
        var x509Data = Assert.IsType<X509Data>(Assert.Single(keyDescriptor.KeyInfo.Data));
        Assert.Equal(legacyThumbprint, Assert.Single(x509Data.Certificates).Thumbprint);
    }

    /// <summary>
    /// Verifies the IdP descriptor contents: the SAML 2.0 protocol, the Redirect and POST
    /// single sign-on endpoints, the SOAP artifact resolution service with index 0 as the
    /// default, the Redirect and POST single logout endpoints and the selected signing key.
    /// </summary>
    [Fact]
    public void Idp_metadata_contains_sso_slo_artifact_endpoints_and_key()
    {
        var service = CreateService();

        var descriptor = SingleIdpSsoDescriptor(service.CreateIdpMetadata());

        Assert.Contains(Saml2ProtocolUri, descriptor.ProtocolsSupported);

        Assert.Equal(2, descriptor.SingleSignOnServices.Count);
        var ssoServices = descriptor.SingleSignOnServices.OrderBy(s => s.Binding.ToString()).ToList();
        Assert.Equal(Saml2Binding.HttpPostUri, ssoServices[0].Binding);
        Assert.Equal(Saml2Binding.HttpRedirectUri, ssoServices[1].Binding);
        Assert.All(ssoServices, s => Assert.Equal(new Uri("https://localhost/"), s.Location));

        var artifactService = Assert.Single(descriptor.ArtifactResolutionServices.Values);
        Assert.Same(artifactService, descriptor.ArtifactResolutionServices.Default);
        Assert.Equal(0, artifactService.Index);
        Assert.True(artifactService.IsDefault);
        Assert.Equal(Saml2Binding.SoapUri, artifactService.Binding);
        Assert.Equal(new Uri("https://localhost/ArtifactResolve"), artifactService.Location);

        Assert.Equal(2, descriptor.SingleLogoutServices.Count);
        var sloServices = descriptor.SingleLogoutServices.OrderBy(s => s.Binding.ToString()).ToList();
        Assert.Equal(Saml2Binding.HttpPostUri, sloServices[0].Binding);
        Assert.Equal(Saml2Binding.HttpRedirectUri, sloServices[1].Binding);
        Assert.All(sloServices, s => Assert.Equal(new Uri("https://localhost/Logout"), s.Location));

        var keyDescriptor = Assert.Single(descriptor.Keys);
        var x509Data = Assert.IsType<X509Data>(Assert.Single(keyDescriptor.KeyInfo.Data));
        Assert.Equal(LoadDefaultCertificateThumbprint(), Assert.Single(x509Data.Certificates).Thumbprint);
    }

    /// <summary>
    /// Verifies that the cache metadata can be omitted, which the federation metadata uses
    /// so that the child entity does not duplicate the federation's own cache values.
    /// </summary>
    [Fact]
    public void Idp_metadata_can_omit_cache_metadata()
    {
        var service = CreateService();

        var metadata = service.CreateIdpMetadata(includeCacheDuration: false);

        Assert.Null(metadata.CacheDuration);
        Assert.Null(metadata.ValidUntil);
    }

    /// <summary>
    /// Verifies the federation metadata: the legacy name, its own cache metadata, and the
    /// wrapped IdP entity without duplicated cache metadata.
    /// </summary>
    [Fact]
    public void Federation_metadata_wraps_idp_metadata()
    {
        var service = CreateService();

        var federation = service.CreateFederationMetadata();

        Assert.Equal("Sustainsys.Saml2.StubIdp Federation", federation.Name);
        Assert.Equal(MetadataService.FederationName, federation.Name);
        Assert.Equal(15, federation.CacheDuration!.Value.Minutes);
        Assert.InRange(
            federation.ValidUntil!.Value,
            DateTime.UtcNow.AddHours(23),
            DateTime.UtcNow.AddHours(25));

        var child = Assert.IsType<EntityDescriptor>(Assert.Single(federation.ChildEntities));
        Assert.Equal("https://localhost/Metadata", child.EntityId.Id);
        Assert.Null(child.CacheDuration);
        Assert.Null(child.ValidUntil);
        Assert.Contains(Saml2ProtocolUri, SingleIdpSsoDescriptor(child).ProtocolsSupported);
    }

    /// <summary>
    /// Verifies that the service fails clearly when used outside of a request.
    /// </summary>
    [Fact]
    public void Idp_metadata_throws_without_http_context()
    {
        var accessor = new HttpContextAccessor();

        var options = new StubIdpOptions
        {
            DataPath = "App_Data",
            Certificates = new CertificatesOptions
            {
                Default = new CertificateOptions { File = "App_Data/stubidp.sustainsys.com.pfx" },
                LegacyKentor = new CertificateOptions
                {
                    File = "App_Data/Kentor.AuthServices.StubIdp.pfx",
                    HostName = "stubidp.kentor.se",
                },
            },
        };

        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

        var certificateService = new CertificateService(
            Options.Create(options), environment.Object, NullLogger<CertificateService>.Instance);

        var service = new MetadataService(
            new StubIdpUrlResolver(accessor), certificateService, accessor);

        Assert.Throws<InvalidOperationException>(() => service.CreateIdpMetadata());
    }

    /// <summary>
    /// Verifies that the IdP metadata serializes to signed XML (the exact
    /// <c>ToXmlString(certificate, SignedXml.XmlDsigRSASHA256Url)</c> call the legacy
    /// MetadataController made) containing the entity ID, cache metadata, SSO/SLO/artifact
    /// endpoints and the signing certificate.
    /// </summary>
    [Fact]
    public void Idp_metadata_serializes_to_signed_xml_with_endpoints_and_key()
    {
        var (service, certificateService, context) = CreateServiceWithDependencies();
        var certificate = certificateService.GetSigningCertificate(context);

        var xml = XDocument.Parse(service.CreateIdpMetadata()
            .ToXmlString(certificate, SignedXml.XmlDsigRSASHA256Url));

        var root = xml.Root!;
        Assert.Equal(Md + "EntityDescriptor", root.Name);
        Assert.Equal("https://localhost/Metadata", root.Attribute("entityID")?.Value);
        Assert.Equal("PT15M", root.Attribute("cacheDuration")?.Value);
        Assert.False(string.IsNullOrEmpty(root.Attribute("validUntil")?.Value));

        Assert.Equal(2, root.Descendants(Md + "SingleSignOnService").Count());
        Assert.Equal(2, root.Descendants(Md + "SingleLogoutService").Count());
        Assert.Equal(
            new[] { "https://localhost/", "https://localhost/ArtifactResolve", "https://localhost/Logout" },
            root.Descendants(Md + "SingleSignOnService")
                .Concat(root.Descendants(Md + "SingleLogoutService"))
                .Concat(root.Descendants(Md + "ArtifactResolutionService"))
                .Select(e => e.Attribute("Location")?.Value)
                .Distinct()
                .OrderBy(u => u));

        var artifact = root.Descendants(Md + "ArtifactResolutionService").Single();
        Assert.Equal("0", artifact.Attribute("index")?.Value);
        Assert.Equal("true", artifact.Attribute("isDefault")?.Value);
        Assert.Equal(Saml2Binding.SoapUri.ToString(), artifact.Attribute("Binding")?.Value);

        var certificateData = root.Descendants(Md + "KeyDescriptor")
            .Descendants(Ds + "X509Certificate").Single().Value;
        Assert.Equal(Convert.ToBase64String(certificate.RawData), certificateData);

        var signatureMethod = root.Descendants(Ds + "SignatureMethod").Single();
        Assert.Equal(SignedXml.XmlDsigRSASHA256Url, signatureMethod.Attribute("Algorithm")?.Value);
    }

    /// <summary>
    /// Verifies that the federation metadata serializes with the legacy federation name and
    /// wraps the IdP entity.
    /// </summary>
    [Fact]
    public void Federation_metadata_serializes_with_name_and_child_entity()
    {
        var (service, certificateService, context) = CreateServiceWithDependencies();
        var certificate = certificateService.GetSigningCertificate(context);

        var xml = XDocument.Parse(service.CreateFederationMetadata()
            .ToXmlString(certificate, SignedXml.XmlDsigRSASHA256Url));

        var root = xml.Root!;
        Assert.Equal(Md + "EntitiesDescriptor", root.Name);
        Assert.Equal(MetadataService.FederationName, root.Attribute("Name")?.Value);
        Assert.Equal("PT15M", root.Attribute("cacheDuration")?.Value);

        var child = root.Elements(Md + "EntityDescriptor").Single();
        Assert.Equal("https://localhost/Metadata", child.Attribute("entityID")?.Value);
        Assert.Null(child.Attribute("cacheDuration"));
    }
}
