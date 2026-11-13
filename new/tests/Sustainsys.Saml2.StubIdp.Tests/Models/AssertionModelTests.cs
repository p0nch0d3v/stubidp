using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Sustainsys.Saml2.StubIdp.Configuration;
using Sustainsys.Saml2.StubIdp.Models;
using Sustainsys.Saml2.StubIdp.Services;

namespace Sustainsys.Saml2.StubIdp.Tests.Models;

/// <summary>
/// Tests for <see cref="AssertionModel"/>: option-driven defaults and the
/// <see cref="AssertionModel.ToSaml2Response(StubIdpUrlResolver, X509Certificate2)"/> port of
/// the legacy static-helper-based factory.
/// </summary>
public class AssertionModelTests
{
    private const string TenantId = "2273dd5a-8e86-4abc-9078-236215f06c0e";
    private const string DefaultAcsUrl = "https://sp.example.com/SAML2/Acs";
    private const string DefaultNameId = "JohnDoe";
    private static readonly XNamespace Saml2Ns = "urn:oasis:names:tc:SAML:2.0:assertion";

    /// <summary>
    /// A plain <see cref="DefaultHttpContext"/> is shared; it must not be wrapped in a static
    /// <see cref="HttpContextAccessor"/>, which stores the context in an <c>AsyncLocal</c>
    /// that does not flow to xUnit's parallel test threads.
    /// </summary>
    private static readonly HttpContext DefaultRequestContext =
        CreateContext("https://localhost/");

    private static DefaultHttpContext CreateContext(string requestUri)
    {
        var uri = new Uri(requestUri);
        var context = new DefaultHttpContext();
        context.Request.Scheme = uri.Scheme;
        context.Request.Host = HostString.FromUriComponent(uri);
        context.Request.Path = uri.AbsolutePath;
        return context;
    }

    private static StubIdpUrlResolver CreateResolver(string requestUri = "https://localhost/") =>
        new(new HttpContextAccessor { HttpContext = CreateContext(requestUri) });

    private static CertificateService CreateCertificateService()
    {
        var options = new StubIdpOptions
        {
            DefaultAcsUrl = DefaultAcsUrl,
            DefaultNameId = DefaultNameId,
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

        return new CertificateService(
            Options.Create(options), environment.Object, NullLogger<CertificateService>.Instance);
    }

    private static StubIdpOptions CreateOptions() => new()
    {
        DefaultAcsUrl = DefaultAcsUrl,
        DefaultNameId = DefaultNameId,
        DataPath = "App_Data",
    };

    /// <summary>
    /// Verifies that the factory fills the defaults from the options: the configured ACS URL
    /// and name ID, plus the legacy session index default of 42.
    /// </summary>
    [Fact]
    public void Create_fills_defaults_from_options()
    {
        var model = AssertionModel.Create(CreateOptions());

        Assert.Equal(DefaultAcsUrl, model.AssertionConsumerServiceUrl);
        Assert.Equal(DefaultNameId, model.NameId);
        Assert.Equal("42", model.SessionIndex);
        Assert.Equal(AssertionModel.DefaultSessionIndex, model.SessionIndex);
        Assert.Equal(Sustainsys.Saml2.WebSso.Saml2BindingType.HttpPost, model.ResponseBinding);
    }

    /// <summary>
    /// Verifies the null-argument guard on the factory.
    /// </summary>
    [Fact]
    public void Create_throws_for_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => AssertionModel.Create(null!));
    }

    /// <summary>
    /// Verifies the issuer, destination and relay state of the created response.
    /// </summary>
    [Fact]
    public void ToSaml2Response_sets_issuer_destination_and_relay_state()
    {
        var certificateService = CreateCertificateService();
        var model = AssertionModel.Create(CreateOptions());
        model.RelayState = "SomeRelayState";

        var response = model.ToSaml2Response(
            CreateResolver(), certificateService.GetSigningCertificate(DefaultRequestContext));

        Assert.Equal("https://localhost/Metadata", response.Issuer.Id);
        Assert.Equal(new Uri(DefaultAcsUrl), response.DestinationUrl);
        Assert.Equal("SomeRelayState", response.RelayState);
    }

    /// <summary>
    /// Verifies that the issuer reflects the tenant GUID segment on named routes.
    /// </summary>
    [Fact]
    public void ToSaml2Response_uses_tenant_metadata_url_as_issuer_on_named_route()
    {
        var certificateService = CreateCertificateService();
        var model = AssertionModel.Create(CreateOptions());

        var response = model.ToSaml2Response(
            CreateResolver($"https://localhost/{TenantId}"),
            certificateService.GetSigningCertificate(DefaultRequestContext));

        Assert.Equal($"https://localhost/{TenantId}/Metadata", response.Issuer.Id);
    }

    /// <summary>
    /// Verifies that the response uses the certificate selected for the current request host.
    /// </summary>
    [Fact]
    public void ToSaml2Response_uses_the_supplied_signing_certificate()
    {
        var certificateService = CreateCertificateService();
        var certificate = certificateService.GetSigningCertificate(DefaultRequestContext);
        var model = AssertionModel.Create(CreateOptions());

        var response = model.ToSaml2Response(CreateResolver(), certificate);

        Assert.Same(certificate, response.SigningCertificate);
    }

    /// <summary>
    /// Verifies InResponseTo handling: a non-empty value is echoed, an empty or missing value
    /// produces an unsolicited response without InResponseTo.
    /// </summary>
    /// <param name="inResponseTo">The InResponseTo form value.</param>
    /// <param name="expected">The expected Saml2Id value, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("id1234567890abcdef", "id1234567890abcdef")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ToSaml2Response_sets_InResponseTo_only_when_present(string? inResponseTo, string? expected)
    {
        var certificateService = CreateCertificateService();
        var model = AssertionModel.Create(CreateOptions());
        model.InResponseTo = inResponseTo;

        var response = model.ToSaml2Response(
            CreateResolver(), certificateService.GetSigningCertificate(DefaultRequestContext));

        Assert.Equal(expected, response.InResponseTo?.Value);
    }

    /// <summary>
    /// Verifies the assertion XML: NameID with the legacy unspecified format, the session
    /// index claim, custom attribute statements and the audience restriction.
    /// </summary>
    [Fact]
    public void ToSaml2Response_xml_contains_nameid_sessionindex_attributes_and_audience()
    {
        var certificateService = CreateCertificateService();
        var model = AssertionModel.Create(CreateOptions());
        model.Audience = "https://sp.example.com";
        model.AttributeStatements = new List<AttributeStatementModel>
        {
            new() { Type = "http://schemas.example.com/claims/Role", Value = "Admin" },
            new() { Type = "http://schemas.example.com/claims/Role", Value = "User" },
        };

        var response = model.ToSaml2Response(
            CreateResolver(), certificateService.GetSigningCertificate(DefaultRequestContext));

        var xml = response.ToXElement();
        var assertion = xml.Element(Saml2Ns + "Assertion");
        Assert.NotNull(assertion);

        var nameId = assertion!.Element(Saml2Ns + "Subject")!.Element(Saml2Ns + "NameID")!;
        Assert.Equal(DefaultNameId, nameId.Value);
        Assert.Equal(
            "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified",
            nameId.Attribute("Format")?.Value);

        var authnStatement = assertion.Element(Saml2Ns + "AuthnStatement")!;
        Assert.Equal("42", authnStatement.Attribute("SessionIndex")?.Value);

        var attributes = assertion.Element(Saml2Ns + "AttributeStatement")!
            .Elements(Saml2Ns + "Attribute").ToList();
        var role = Assert.Single(attributes);
        Assert.Equal("http://schemas.example.com/claims/Role", role.Attribute("Name")?.Value);
        Assert.Equal(
            new[] { "Admin", "User" },
            role.Elements(Saml2Ns + "AttributeValue").Select(v => v.Value));

        var audience = assertion.Element(Saml2Ns + "Conditions")!
            .Element(Saml2Ns + "AudienceRestriction")!
            .Element(Saml2Ns + "Audience")!;
        Assert.Equal("https://sp.example.com/", audience.Value); // Uri normalizes the root path
    }

    /// <summary>
    /// Verifies the legacy behavior of omitting the session index claim when the value is
    /// empty: the emitted AuthnStatement carries no SessionIndex attribute.
    /// </summary>
    [Fact]
    public void ToSaml2Response_omits_session_index_when_empty()
    {
        var certificateService = CreateCertificateService();
        var model = AssertionModel.Create(CreateOptions());
        model.SessionIndex = string.Empty;

        var response = model.ToSaml2Response(
            CreateResolver(), certificateService.GetSigningCertificate(DefaultRequestContext));

        var authnStatement = response.ToXElement()
            .Element(Saml2Ns + "Assertion")!
            .Element(Saml2Ns + "AuthnStatement")!;

        Assert.Null(authnStatement.Attribute("SessionIndex"));
    }

    /// <summary>
    /// Verifies the null-argument guards on <see cref="AssertionModel.ToSaml2Response"/>.
    /// </summary>
    [Fact]
    public void ToSaml2Response_throws_for_null_arguments()
    {
        var certificateService = CreateCertificateService();
        var certificate = certificateService.GetSigningCertificate(DefaultRequestContext);
        var model = AssertionModel.Create(CreateOptions());

        Assert.Throws<ArgumentNullException>(() => model.ToSaml2Response(null!, certificate));
        Assert.Throws<ArgumentNullException>(() => model.ToSaml2Response(CreateResolver(), null!));
    }
}
