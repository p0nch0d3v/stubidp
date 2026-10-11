using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sustainsys.Saml2.StubIdp.Configuration;

namespace Sustainsys.Saml2.StubIdp.Tests.Configuration;

/// <summary>
/// Tests for <see cref="StubIdpOptions"/> binding and <see cref="StubIdpOptionsValidator"/>.
/// </summary>
public class StubIdpOptionsTests
{
    private static readonly Dictionary<string, string?> ValidSettings = new()
    {
        ["StubIdp:DefaultAcsUrl"] = "https://sp.example.com/SAML2/Acs",
        ["StubIdp:DefaultNameId"] = "JohnDoe",
        ["StubIdp:DataPath"] = "App_Data",
        ["StubIdp:Certificates:Default:File"] = "App_Data/stubidp.sustainsys.com.pfx",
        ["StubIdp:Certificates:LegacyKentor:File"] = "App_Data/Kentor.AuthServices.StubIdp.pfx",
        ["StubIdp:Certificates:LegacyKentor:HostName"] = "stubidp.kentor.se",
    };

    private static IConfiguration BuildConfiguration(IDictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static StubIdpOptions Resolve(IDictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddStubIdpOptions(BuildConfiguration(settings));

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<StubIdpOptions>>().Value;
    }

    private static StubIdpOptions ValidOptions() => new()
    {
        DefaultAcsUrl = "https://sp.example.com/SAML2/Acs",
        DefaultNameId = "JohnDoe",
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

    /// <summary>
    /// Verifies that the <c>StubIdp</c> section binds every value, including the nested
    /// certificate entries.
    /// </summary>
    [Fact]
    public void Section_binds_all_values()
    {
        var options = Resolve(ValidSettings);

        Assert.Equal("https://sp.example.com/SAML2/Acs", options.DefaultAcsUrl);
        Assert.Equal("JohnDoe", options.DefaultNameId);
        Assert.Equal("App_Data", options.DataPath);
        Assert.Equal("App_Data/stubidp.sustainsys.com.pfx", options.Certificates.Default.File);
        Assert.Null(options.Certificates.Default.HostName);
        Assert.Equal("App_Data/Kentor.AuthServices.StubIdp.pfx", options.Certificates.LegacyKentor.File);
        Assert.Equal("stubidp.kentor.se", options.Certificates.LegacyKentor.HostName);
    }

    /// <summary>
    /// Verifies that no certificate password is configured by default, so that a password can
    /// only ever arrive from user secrets or environment configuration.
    /// </summary>
    [Fact]
    public void Certificate_passwords_are_not_configured_by_default()
    {
        var options = Resolve(ValidSettings);

        Assert.Null(options.Certificates.Default.Password);
        Assert.Null(options.Certificates.LegacyKentor.Password);
    }

    /// <summary>
    /// Verifies that a certificate password supplied by an out-of-repository configuration source
    /// (user secrets, environment variables) is bound.
    /// </summary>
    [Fact]
    public void Certificate_password_binds_from_configuration()
    {
        var settings = new Dictionary<string, string?>(ValidSettings)
        {
            ["StubIdp:Certificates:Default:Password"] = "not-a-real-password",
        };

        var options = Resolve(settings);

        Assert.Equal("not-a-real-password", options.Certificates.Default.Password);
    }

    /// <summary>
    /// Verifies that the section name constant matches the key used in <c>appsettings.json</c>.
    /// </summary>
    [Fact]
    public void Section_name_is_StubIdp()
    {
        Assert.Equal("StubIdp", StubIdpOptions.SectionName);
    }

    /// <summary>
    /// Verifies that fully configured options pass validation.
    /// </summary>
    [Fact]
    public void Validator_accepts_complete_options()
    {
        var result = new StubIdpOptionsValidator().Validate(Options.DefaultName, ValidOptions());

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Verifies that every required scalar value is rejected when missing.
    /// </summary>
    /// <param name="property">The name of the property to clear.</param>
    [Theory]
    [InlineData(nameof(StubIdpOptions.DefaultAcsUrl))]
    [InlineData(nameof(StubIdpOptions.DefaultNameId))]
    [InlineData(nameof(StubIdpOptions.DataPath))]
    public void Validator_rejects_missing_required_value(string property)
    {
        var options = ValidOptions();
        switch (property)
        {
            case nameof(StubIdpOptions.DefaultAcsUrl):
                options.DefaultAcsUrl = "";
                break;
            case nameof(StubIdpOptions.DefaultNameId):
                options.DefaultNameId = "   ";
                break;
            default:
                options.DataPath = "";
                break;
        }

        var result = new StubIdpOptionsValidator().Validate(Options.DefaultName, options);

        Assert.False(result.Succeeded);
        Assert.Contains($"StubIdp:{property}", result.FailureMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that an empty <see cref="StubIdpOptions.DataPath"/> fails validation, matching the
    /// Task02 acceptance criterion.
    /// </summary>
    [Fact]
    public void Validator_rejects_empty_data_path_on_otherwise_default_options()
    {
        var result = new StubIdpOptionsValidator()
            .Validate(Options.DefaultName, new StubIdpOptions { DataPath = "" });

        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// Verifies that both certificate file paths are required.
    /// </summary>
    [Fact]
    public void Validator_rejects_missing_certificate_files()
    {
        var options = ValidOptions();
        options.Certificates.Default.File = "";
        options.Certificates.LegacyKentor.File = "";

        var result = new StubIdpOptionsValidator().Validate(Options.DefaultName, options);

        Assert.False(result.Succeeded);
        Assert.Contains("StubIdp:Certificates:Default:File", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("StubIdp:Certificates:LegacyKentor:File", result.FailureMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a missing certificates section fails validation.
    /// </summary>
    [Fact]
    public void Validator_rejects_missing_certificates_section()
    {
        var options = ValidOptions();
        options.Certificates = null!;

        var result = new StubIdpOptionsValidator().Validate(Options.DefaultName, options);

        Assert.False(result.Succeeded);
        Assert.Contains("StubIdp:Certificates", result.FailureMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a certificate password is never required, because the bundled test
    /// certificates have no password.
    /// </summary>
    [Fact]
    public void Validator_does_not_require_certificate_passwords()
    {
        var options = ValidOptions();
        options.Certificates.Default.Password = null;
        options.Certificates.LegacyKentor.Password = "";

        var result = new StubIdpOptionsValidator().Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Verifies that the validator guards its options argument.
    /// </summary>
    [Fact]
    public void Validator_throws_on_null_options()
    {
        Assert.Throws<ArgumentNullException>(
            () => new StubIdpOptionsValidator().Validate(Options.DefaultName, null!));
    }

    /// <summary>
    /// Verifies that validation is eager: resolving the options for an incomplete configuration
    /// throws instead of yielding half-configured values.
    /// </summary>
    [Fact]
    public void Resolving_incomplete_options_throws()
    {
        var settings = new Dictionary<string, string?>(ValidSettings);
        settings["StubIdp:DataPath"] = "";

        var exception = Assert.Throws<OptionsValidationException>(() => Resolve(settings));

        Assert.Contains("StubIdp:DataPath", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a completely missing <c>StubIdp</c> section is reported rather than silently
    /// accepted.
    /// </summary>
    [Fact]
    public void Missing_section_fails_validation()
    {
        Assert.Throws<OptionsValidationException>(
            () => Resolve(new Dictionary<string, string?>()));
    }

    /// <summary>
    /// Verifies the argument guards of the registration helper.
    /// </summary>
    [Fact]
    public void AddStubIdpOptions_guards_arguments()
    {
        var configuration = BuildConfiguration(ValidSettings);

        Assert.Throws<ArgumentNullException>(
            () => StubIdpOptionsServiceCollectionExtensions.AddStubIdpOptions(null!, configuration));
        Assert.Throws<ArgumentNullException>(
            () => new ServiceCollection().AddStubIdpOptions(null!));
    }

    /// <summary>
    /// Verifies that the registration helper registers the validator so it participates in
    /// startup validation.
    /// </summary>
    [Fact]
    public void AddStubIdpOptions_registers_validator()
    {
        var services = new ServiceCollection();
        services.AddStubIdpOptions(BuildConfiguration(ValidSettings));

        using var provider = services.BuildServiceProvider();

        Assert.Contains(
            provider.GetServices<IValidateOptions<StubIdpOptions>>(),
            validator => validator is StubIdpOptionsValidator);
    }
}
