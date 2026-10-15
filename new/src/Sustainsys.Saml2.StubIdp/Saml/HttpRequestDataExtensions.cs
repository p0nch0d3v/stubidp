using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Sustainsys.Saml2.WebSso;

namespace Sustainsys.Saml2.StubIdp.Saml;

/// <summary>
/// Bridges between the ASP.NET Core <see cref="HttpContext"/> and the
/// <see cref="Sustainsys.Saml2.WebSso"/> <see cref="HttpRequestData"/> that the library uses
/// internally for SAML binding operations.
/// </summary>
/// <remarks>
/// <para>
/// These methods are adapted from the legacy
/// <c>legacy/Sustainsys.Saml2.AspNetCore2/HttpRequestExtensions.cs</c> reference and the
/// System.Web <c>legacy/Sustainsys.Saml2.HttpModule/HttpRequestBaseExtensions.cs</c>. They
/// contain no <c>System.Web</c> coupling and no cookie decryption: the StubIdp never reads
/// the requester's session and the SAML library's redirect-binding support does not require
/// decrypted state for receive or emit flows.
/// </para>
/// <para>
/// The form collection is read eagerly via <see cref="HttpRequest.ReadFormAsync"/>. Callers
/// that need to read the body themselves after binding detection should pass
/// <c>includeForm: false</c> so the form stream is not consumed prematurely.
/// </para>
/// </remarks>
public static class HttpRequestDataExtensions
{
    /// <summary>
    /// Creates a <see cref="HttpRequestData"/> from the current ASP.NET Core request, reading
    /// the form collection if the request is a POST with form content type.
    /// </summary>
    /// <param name="httpContext">The HTTP context to convert.</param>
    /// <param name="includeForm">
    /// When <see langword="true"/> (the default) and the request is a POST with a form content
    /// type, the form is read eagerly and copied into the resulting <see cref="HttpRequestData"/>.
    /// Pass <see langword="false"/> when the caller intends to consume the form/body manually
    /// after detecting that no SAML binding is present.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the form read.</param>
    /// <returns>The populated <see cref="HttpRequestData"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> is <see langword="null"/>.</exception>
    public static Task<HttpRequestData> ToHttpRequestDataAsync(
        this HttpContext httpContext,
        bool includeForm = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return ToHttpRequestDataCoreAsync(httpContext, includeForm, cancellationToken);
    }

    /// <summary>
    /// Creates a <see cref="HttpRequestData"/> from the current ASP.NET Core request without
    /// reading the form. Equivalent to <c>ToHttpRequestDataAsync(includeForm: false)</c>.
    /// </summary>
    /// <param name="httpContext">The HTTP context to convert.</param>
    /// <returns>The populated <see cref="HttpRequestData"/> with no form data attached.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> is <see langword="null"/>.</exception>
    public static HttpRequestData ToHttpRequestData(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return ToHttpRequestDataCore(httpContext);
    }

    private static async Task<HttpRequestData> ToHttpRequestDataCoreAsync(
        HttpContext httpContext,
        bool includeForm,
        CancellationToken cancellationToken)
    {
        var request = httpContext.Request;

        var form = includeForm && IsFormPost(request)
            ? await ReadFormSafelyAsync(request, cancellationToken).ConfigureAwait(false)
            : null;

        return BuildHttpRequestData(httpContext, request, form);
    }

    private static HttpRequestData ToHttpRequestDataCore(HttpContext httpContext)
    {
        var request = httpContext.Request;

        // No form read - caller's responsibility to avoid double-reading the body stream.
        return BuildHttpRequestData(httpContext, request, form: null);
    }

    private static HttpRequestData BuildHttpRequestData(
        HttpContext httpContext,
        HttpRequest request,
        IFormCollection? form)
    {
        // PathBase is "/" when not set, matching legacy System.Web ApplicationPath behavior
        // (legacy: requestBase.ApplicationPath was always non-empty, defaulting to "/" for the
        // root site). The library uses it as the application root, not for cookie decryption.
        var pathBase = request.PathBase.HasValue ? request.PathBase.Value! : "/";

        // Absolute URL composed from scheme + host + path + query, matching the legacy
        // HttpRequestBase.Url value the library expects.
        var url = new Uri(
            request.Scheme
                + Uri.SchemeDelimiter
                + (request.Host.HasValue ? request.Host.ToUriComponent() : string.Empty)
                + request.Path.ToString()
                + request.QueryString.ToString(),
            UriKind.Absolute);

        IEnumerable<KeyValuePair<string, IEnumerable<string>>>? formData = null;
        if (form is not null)
        {
            formData = form.Select(
                field => new KeyValuePair<string, IEnumerable<string>>(
                    field.Key,
                    (IEnumerable<string>)field.Value));
        }

        // Use the constructor that takes (method, url, applicationPath, formData, cookies,
        // cookieDecryptor, user). StubIdp does not need encrypted cookies, so the cookie
        // reader and decryptor are no-ops and the user principal is the request's current
        // principal (matching ClaimsPrincipal.Current in legacy System.Web).
        return new HttpRequestData(
            request.Method,
            url,
            pathBase,
            formData,
            cookies: Array.Empty<KeyValuePair<string, string>>(),
            cookieDecryptor: _ => Array.Empty<byte>(),
            user: httpContext.User);
    }

    private static bool IsFormPost(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) && request.HasFormContentType;

    private static async Task<IFormCollection> ReadFormSafelyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Surface cancellation to the caller; do not silently produce a request without
            // form data.
            throw;
        }
        catch (InvalidDataException)
        {
            // Malformed form payload - return an empty form so binding detection can still
            // proceed (the library will treat it as a non-form request).
            return await Task.FromResult(new FormCollection(new Dictionary<string, StringValues>()))
                .ConfigureAwait(false);
        }
    }
}