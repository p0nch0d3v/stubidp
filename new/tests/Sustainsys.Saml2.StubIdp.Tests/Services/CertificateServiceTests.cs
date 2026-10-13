using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.StubIdp.Configuration;
using Sustainsys.Saml2.StubIdp.Services;

namespace Sustainsys.Saml2.StubIdp.Tests.Services;

/// <summary>
/// Tests for <see cref="CertificateService"/>: host-aware certificate selection and key
/// descriptor creation, using the bundled test certificates copied to the build output.
/// </summary>
public class CertificateServiceTests
{
    private const string DefaultCertificateFile = "App_Data/stubidp.sustainsys.com.pfx";
    private const string LegacyKentorCertificateFile = "App_Data/Kentor.AuthServices.StubIdp.pfx";

    /// <summary>
    /// The content root is the test build output: the data files flow there transitively from
    /// the production project (see Task02).
    /// </summary>
    private static readonly string ContentRootPath = AppContext.BaseDirectory;

    private static IHostEnvironment CreateEnvironment()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.ContentRootPath).Returns(ContentRootPath);
        return environment.Object;
    }

    private static StubIdpOptions CreateOptions(Action<CertificatesOptions>? configure = null)
    {
        var options = new StubIdpOptions
        {
            DefaultAcsUrl = "https://sp.example.com/SAML2/Acs",
            DefaultNameId = "JohnDoe",
            DataPath = "App_Data",
            Certificates = new CertificatesOptions
            {
                Default = new CertificateOptions { File = DefaultCertificateFile },
                LegacyKentor = new CertificateOptions
                {
                    File = LegacyKentorCertificateFile,
                    HostName = "stubidp.kentor.se",
                },
            },
        };
        configure?.Invoke(options.Certificates);
        return options;
    }

    private static CertificateService CreateService(Action<CertificatesOptions>? configure = null) =>
        new(Options.Create(CreateOptions(configure)), CreateEnvironment(), NullLogger<CertificateService>.Instance);

    private static HttpContext CreateContext(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        return context;
    }

    private static string LoadPublicCertificateThumbprint(string fileName) =>
        X509CertificateLoader.LoadCertificateFromFile(Path.Combine(ContentRootPath, "App_Data", fileName)).Thumbprint;

    /// <summary>
    /// Verifies that a request to the legacy host name selects the legacy Kentor certificate,
    /// preserving signature validity for service providers configured against the old host.
    /// </summary>
    [Fact]
    public void Legacy_host_selects_the_legacy_kentor_certificate()
    {
        var service = CreateService();

        var certificate = service.GetSigningCertificate(CreateContext("stubidp.kentor.se"));

        Assert.Equal(LoadPublicCertificateThumbprint("Kentor.AuthServices.StubIdp.cer"), certificate.Thumbprint);
    }

    /// <summary>
    /// Verifies that the legacy host name comparison is case-insensitive.
    /// </summary>
    [Fact]
    public void Legacy_host_name_match_is_case_insensitive()
    {
        var service = CreateService();

        var certificate = service.GetSigningCertificate(CreateContext("STUBIDP.KENTOR.SE"));

        Assert.Equal(LoadPublicCertificateThumbprint("Kentor.AuthServices.StubIdp.cer"), certificate.Thumbprint);
    }

    /// <summary>
    /// Verifies that any other host selects the default certificate.
    /// </summary>
    /// <param name="host">A host name that is not the legacy host.</param>
    [Theory]
    [InlineData("localhost")]
    [InlineData("stubidp.sustainsys.com")]
    [InlineData("example.com")]
    public void Other_hosts_select_the_default_certificate(string host)
    {
        var service = CreateService();

        var certificate = service.GetSigningCertificate(CreateContext(host));

        Assert.Equal(LoadPublicCertificateThumbprint("stubidp.sustainsys.com.cer"), certificate.Thumbprint);
    }

    /// <summary>
    /// Verifies that the service hands out the same certificate instance for repeated calls:
    /// the singleton owns the certificates for the application lifetime.
    /// </summary>
    [Fact]
    public void Repeated_calls_return_the_same_certificate_instance()
    {
        var service = CreateService();

        var first = service.GetSigningCertificate(CreateContext("localhost"));
        var second = service.GetSigningCertificate(CreateContext("localhost"));

        Assert.Same(first, second);
    }

    /// <summary>
    /// Verifies that the key descriptor holds an X509 key entry with the public part of the
    /// certificate selected for the request.
    /// </summary>
    /// <param name="host">The request host.</param>
    /// <param name="expectedCerFile">The .cer file of the expected certificate.</param>
    [Theory]
    [InlineData("localhost", "stubidp.sustainsys.com.cer")]
    [InlineData("stubidp.kentor.se", "Kentor.AuthServices.StubIdp.cer")]
    public void Key_descriptor_contains_the_selected_public_certificate(string host, string expectedCerFile)
    {
        var service = CreateService();

        var keyDescriptor = service.GetSigningKeyDescriptor(CreateContext(host));

        var x509Data = Assert.IsType<X509Data>(Assert.Single(keyDescriptor.KeyInfo.Data));
        var certificate = Assert.Single(x509Data.Certificates);
        Assert.Equal(LoadPublicCertificateThumbprint(expectedCerFile), certificate.Thumbprint);
    }

    /// <summary>
    /// Verifies the null-argument guards on the public methods.
    /// </summary>
    [Fact]
    public void Public_methods_throw_for_null_context()
    {
        var service = CreateService();

        Assert.Throws<ArgumentNullException>(() => service.GetSigningCertificate(null!));
        Assert.Throws<ArgumentNullException>(() => service.GetSigningKeyDescriptor(null!));
    }

    /// <summary>
    /// Verifies the null-argument guards on the constructor.
    /// </summary>
    [Fact]
    public void Constructor_throws_for_null_arguments()
    {
        var options = Options.Create(CreateOptions());
        var environment = CreateEnvironment();
        var logger = NullLogger<CertificateService>.Instance;

        Assert.Throws<ArgumentNullException>(() => new CertificateService(null!, environment, logger));
        Assert.Throws<ArgumentNullException>(() => new CertificateService(options, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new CertificateService(options, environment, null!));
    }

    /// <summary>
    /// Verifies that a missing certificate file fails service construction with a clear
    /// configuration error instead of a raw I/O exception.
    /// </summary>
    [Fact]
    public void Missing_certificate_file_throws_a_configuration_error()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateService(certificates => certificates.Default.File = "App_Data/does-not-exist.pfx"));

        Assert.Contains("StubIdp:Certificates:Default", exception.Message);
    }

    /// <summary>
    /// Verifies that a certificate file that cannot be opened with the configured password
    /// fails with a clear configuration error.
    /// </summary>
    [Fact]
    public void Wrong_certificate_password_throws_a_configuration_error()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateService(certificates => certificates.Default.Password = "wrong-password"));

        Assert.Contains("StubIdp:Certificates:Default:Password", exception.Message);
    }

    /// <summary>
    /// Verifies that an unconfigured certificate file path fails with a clear configuration
    /// error.
    /// </summary>
    [Fact]
    public void Unconfigured_certificate_file_throws_a_configuration_error()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateService(certificates => certificates.Default.File = ""));

        Assert.Contains("StubIdp:Certificates:Default:File", exception.Message);
    }
}
