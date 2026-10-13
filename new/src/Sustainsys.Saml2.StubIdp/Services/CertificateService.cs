using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.StubIdp.Configuration;

namespace Sustainsys.Saml2.StubIdp.Services;

/// <summary>
/// Loads and owns the signing certificates of the stub identity provider and selects the one
/// to use for the current request. Port of the legacy static <c>CertificateHelper</c>
/// (<c>legacy/Sustainsys.Saml2.StubIdp/CertificateHelper.cs</c>).
/// </summary>
/// <remarks>
/// The service is registered as a singleton and owns its <see cref="X509Certificate2"/>
/// instances for the application lifetime; they are never disposed per request. Both
/// certificates are loaded once, when the service is constructed. A missing file or an
/// invalid password fails the service construction with a clear configuration error instead
/// of a raw cryptographic exception.
/// </remarks>
public class CertificateService
{
    private readonly LoadedCertificate defaultCertificate;
    private readonly LoadedCertificate legacyKentorCertificate;
    private readonly string? legacyKentorHostName;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateService"/> class and loads both
    /// configured certificates.
    /// </summary>
    /// <param name="options">The application options holding the certificate files.</param>
    /// <param name="environment">The host environment used to root relative file paths.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A certificate file is missing or cannot be
    /// loaded with the configured password.</exception>
    public CertificateService(
        IOptions<StubIdpOptions> options,
        IHostEnvironment environment,
        ILogger<CertificateService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        var certificates = options.Value.Certificates;
        legacyKentorHostName = certificates?.LegacyKentor?.HostName;

        defaultCertificate = Load(certificates?.Default, nameof(CertificatesOptions.Default), environment, logger);
        legacyKentorCertificate = Load(certificates?.LegacyKentor, nameof(CertificatesOptions.LegacyKentor), environment, logger);
    }

    /// <summary>
    /// Gets the signing certificate for the request described by <paramref name="context"/>.
    /// Requests to the configured legacy host name (<c>stubidp.kentor.se</c>) use the legacy
    /// Kentor certificate so that service providers configured against the old host keep
    /// validating signatures; every other host uses the default certificate.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The signing certificate for the current request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is
    /// <see langword="null"/>.</exception>
    public X509Certificate2 GetSigningCertificate(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Select(context).Certificate;
    }

    /// <summary>
    /// Gets a metadata <see cref="KeyDescriptor"/> holding the public part of the certificate
    /// selected for the request described by <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A key descriptor with an X509 key entry of the selected certificate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is
    /// <see langword="null"/>.</exception>
    public KeyDescriptor GetSigningKeyDescriptor(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Select(context).KeyDescriptor;
    }

    private LoadedCertificate Select(HttpContext context)
    {
        if (legacyKentorHostName is not null
            && string.Equals(context.Request.Host.Host, legacyKentorHostName, StringComparison.OrdinalIgnoreCase))
        {
            return legacyKentorCertificate;
        }

        return defaultCertificate;
    }

    private static LoadedCertificate Load(
        CertificateOptions? certificateOptions,
        string key,
        IHostEnvironment environment,
        ILogger logger)
    {
        if (certificateOptions is null || string.IsNullOrWhiteSpace(certificateOptions.File))
        {
            throw new InvalidOperationException(
                $"The signing certificate \"{key}\" is not configured. Set StubIdp:Certificates:{key}:File to the certificate file.");
        }

        var path = Path.IsPathRooted(certificateOptions.File)
            ? certificateOptions.File
            : Path.Combine(environment.ContentRootPath, certificateOptions.File);

        try
        {
            // MachineKeySet preserves the legacy shared-hosting requirement; EphemeralKeySet
            // keeps the keys in memory where supported, so no key material is written to a key
            // store. The certificates are owned by this singleton for the application lifetime.
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                path,
                certificateOptions.Password,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

            logger.LogDebug(
                "Loaded signing certificate {Thumbprint} for {CertificateKey} from {Path}.",
                certificate.Thumbprint,
                key,
                path);

            return new LoadedCertificate(certificate, CreateKeyDescriptor(certificate));
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Failed to load the signing certificate \"{key}\" from \"{path}\". Ensure the file exists and that StubIdp:Certificates:{key}:Password (user secrets / environment variables) matches the file.",
                ex);
        }
    }

    private static KeyDescriptor CreateKeyDescriptor(X509Certificate2 certificate)
    {
        var keyDescriptor = new KeyDescriptor { KeyInfo = new DSigKeyInfo() };
        var x509Data = new X509Data();
        x509Data.Certificates.Add(certificate);
        keyDescriptor.KeyInfo.Data.Add(x509Data);
        return keyDescriptor;
    }

    private sealed record LoadedCertificate(X509Certificate2 Certificate, KeyDescriptor KeyDescriptor);
}
