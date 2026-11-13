using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Sustainsys.Saml2.Saml2P;
using Sustainsys.Saml2.StubIdp.Configuration;
using Sustainsys.Saml2.StubIdp.Models;
using Sustainsys.Saml2.StubIdp.Services;

namespace Sustainsys.Saml2.StubIdp.Tests.Models;

/// <summary>
/// Tests for the logout models in <c>Models/LogoutModels.cs</c>: the port of the legacy
/// <c>ToLogoutRequest</c>/<c>ToLogoutResponse</c> factories with explicit resolver and
/// certificate dependencies.
/// </summary>
public class LogoutModelsTests
{
    private const string SpLogoutUrl = "https://sp.example.com/SAML2/Logout";

    /// <summary>
    /// A plain <see cref="DefaultHttpContext"/> is shared; it must not be wrapped in a static
    /// <see cref="HttpContextAccessor"/>, which stores the context in an <c>AsyncLocal</c>
    /// that does not flow to xUnit's parallel test threads.
    /// </summary>
    private static readonly HttpContext LogoutRequestContext = CreateContext();

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/Logout";
        return context;
    }

    private static StubIdpUrlResolver CreateResolver() =>
        new(new HttpContextAccessor { HttpContext = LogoutRequestContext });

    private static X509Certificate2 CreateSigningCertificate()
    {
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

        var service = new CertificateService(
            Options.Create(options), environment.Object, NullLogger<CertificateService>.Instance);

        return service.GetSigningCertificate(LogoutRequestContext);
    }

    /// <summary>
    /// Verifies that the initiate-logout model keeps the legacy default session index.
    /// </summary>
    [Fact]
    public void InitiateLogoutModel_defaults_session_index_to_42()
    {
        Assert.Equal(AssertionModel.DefaultSessionIndex, new InitiateLogoutModel().SessionIndex);
        Assert.Equal("42", new InitiateLogoutModel().SessionIndex);
    }

    /// <summary>
    /// Verifies that the logout request preserves destination, NameID, session data, issuer
    /// (the resolved metadata URL), signing certificate and the SHA-256 signing algorithm.
    /// </summary>
    [Fact]
    public void ToLogoutRequest_preserves_destination_nameid_session_and_signing()
    {
        var certificate = CreateSigningCertificate();
        var model = new InitiateLogoutModel
        {
            DestinationUrl = new Uri(SpLogoutUrl),
            NameId = "JohnDoe",
            SessionIndex = "42",
        };

        var request = model.ToLogoutRequest(CreateResolver(), certificate);

        Assert.Equal(new Uri(SpLogoutUrl), request.DestinationUrl);
        Assert.Equal("https://localhost/Metadata", request.Issuer!.Id);
        Assert.Equal("JohnDoe", request.NameId!.Value);
        Assert.Equal("42", request.SessionIndex);
        Assert.Same(certificate, request.SigningCertificate);
        Assert.Equal(SignedXml.XmlDsigRSASHA256Url, request.SigningAlgorithm);
    }

    /// <summary>
    /// Verifies that the logout response preserves destination, InResponseTo, relay state,
    /// issuer, success status, signing certificate and the SHA-256 signing algorithm.
    /// </summary>
    [Fact]
    public void ToLogoutResponse_preserves_destination_inresponseto_relaystate_and_signing()
    {
        var certificate = CreateSigningCertificate();
        var model = new RespondToLogoutRequestModel
        {
            DestinationUrl = new Uri(SpLogoutUrl),
            InResponseTo = "id1234567890abcdef",
            RelayState = "SomeRelayState",
        };

        var response = model.ToLogoutResponse(CreateResolver(), certificate);

        Assert.Equal(new Uri(SpLogoutUrl), response.DestinationUrl);
        Assert.Equal("https://localhost/Metadata", response.Issuer!.Id);
        Assert.Equal("id1234567890abcdef", response.InResponseTo!.Value);
        Assert.Equal("SomeRelayState", response.RelayState);
        Assert.Equal(Saml2StatusCode.Success, response.Status);
        Assert.Same(certificate, response.SigningCertificate);
        Assert.Equal(SignedXml.XmlDsigRSASHA256Url, response.SigningAlgorithm);
    }

    /// <summary>
    /// Verifies the null-argument guards on <see cref="InitiateLogoutModel.ToLogoutRequest"/>.
    /// </summary>
    [Fact]
    public void ToLogoutRequest_throws_for_null_arguments()
    {
        var certificate = CreateSigningCertificate();
        var model = new InitiateLogoutModel { DestinationUrl = new Uri(SpLogoutUrl), NameId = "JohnDoe" };

        Assert.Throws<ArgumentNullException>(() => model.ToLogoutRequest(null!, certificate));
        Assert.Throws<ArgumentNullException>(() => model.ToLogoutRequest(CreateResolver(), null!));
    }

    /// <summary>
    /// Verifies the null-argument guards on
    /// <see cref="RespondToLogoutRequestModel.ToLogoutResponse"/>.
    /// </summary>
    [Fact]
    public void ToLogoutResponse_throws_for_null_arguments()
    {
        var certificate = CreateSigningCertificate();
        var model = new RespondToLogoutRequestModel
        {
            DestinationUrl = new Uri(SpLogoutUrl),
            InResponseTo = "id1234567890abcdef",
        };

        Assert.Throws<ArgumentNullException>(() => model.ToLogoutResponse(null!, certificate));
        Assert.Throws<ArgumentNullException>(() => model.ToLogoutResponse(CreateResolver(), null!));
    }
}
