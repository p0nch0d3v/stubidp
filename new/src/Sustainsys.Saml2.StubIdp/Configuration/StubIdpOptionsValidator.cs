using Microsoft.Extensions.Options;

namespace Sustainsys.Saml2.StubIdp.Configuration;

/// <summary>
/// Validates <see cref="StubIdpOptions"/> so that a misconfigured application fails at startup
/// instead of at the first request.
/// </summary>
public class StubIdpOptionsValidator : IValidateOptions<StubIdpOptions>
{
    /// <summary>
    /// Validates the supplied options instance.
    /// </summary>
    /// <param name="name">The name of the options instance being validated, or
    /// <see langword="null"/> for the default instance.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>
    /// <see cref="ValidateOptionsResult.Success"/> when every required value is present, otherwise
    /// a failure result listing every problem found.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is
    /// <see langword="null"/>.</exception>
    public ValidateOptionsResult Validate(string? name, StubIdpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var section = name is null or "" ? StubIdpOptions.SectionName : $"{StubIdpOptions.SectionName} ({name})";
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DefaultAcsUrl))
        {
            failures.Add($"{section}:{nameof(StubIdpOptions.DefaultAcsUrl)} must be a non-empty value.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultNameId))
        {
            failures.Add($"{section}:{nameof(StubIdpOptions.DefaultNameId)} must be a non-empty value.");
        }

        if (string.IsNullOrWhiteSpace(options.DataPath))
        {
            failures.Add($"{section}:{nameof(StubIdpOptions.DataPath)} must be a non-empty value.");
        }

        if (options.Certificates is null)
        {
            failures.Add($"{section}:{nameof(StubIdpOptions.Certificates)} must be configured.");
        }
        else
        {
            ValidateCertificate(options.Certificates.Default, nameof(CertificatesOptions.Default));
            ValidateCertificate(options.Certificates.LegacyKentor, nameof(CertificatesOptions.LegacyKentor));
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);

        void ValidateCertificate(CertificateOptions? certificate, string key)
        {
            if (certificate is null || string.IsNullOrWhiteSpace(certificate.File))
            {
                failures.Add(
                    $"{section}:{nameof(StubIdpOptions.Certificates)}:{key}:{nameof(CertificateOptions.File)} must be a non-empty value.");
            }
        }
    }
}
