using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Sustainsys.Saml2.WebSso;

namespace Sustainsys.Saml2.StubIdp.Saml;

/// <summary>
/// Writes a <see cref="CommandResult"/> produced by the <c>Sustainsys.Saml2</c> library to the
/// current HTTP response. Replaces the legacy
/// <c>Sustainsys.Saml2.Mvc.CommandResultExtensions.ToActionResult()</c> (which returned an MVC
/// <c>ActionResult</c>) and the obsolete <c>Sustainsys.Saml2.AspNetCore2.Apply</c> bridge for
/// this application.
/// </summary>
/// <remarks>
/// <para>
/// <b>Status code parity.</b> Legacy MVC's <c>RedirectResult(commandResult.Location)</c> always
/// produced <c>302 Found</c>, regardless of <see cref="CommandResult.HttpStatusCode"/>. To
/// preserve the externally visible behavior the StubIdp previously emitted (and which external
/// SPs rely on), redirect responses are coerced to <c>302 Found</c> even when the library
/// sets <see cref="System.Net.HttpStatusCode.SeeOther"/> (<c>303</c>). Content responses
/// pass the status code through unchanged.
/// </para>
/// <para>
/// <b>Charset parity.</b> Legacy System.Web appended <c>; charset=utf-8</c> to the
/// <c>Content-Type</c> of written text bodies, so the library's auto-post <c>text/html</c>
/// reached SPs as <c>text/html; charset=utf-8</c>. Content responses whose content type carries
/// no charset get <c>; charset=utf-8</c> appended (matching the UTF-8 body encoding); an
/// explicit charset is kept unchanged.
/// </para>
/// <para>
/// <b>Cacheability.</b> <see cref="CommandResult.Cacheability"/> is intentionally ignored and
/// no <c>Cache-Control</c> header is emitted by the bridge, matching the legacy
/// <c>Sustainsys.Saml2.Mvc.ToActionResult()</c>, which never mapped it. Library-supplied
/// entries in <see cref="CommandResult.Headers"/> (including any <c>Cache-Control</c>) are
/// still copied through.
/// </para>
/// <para>
/// <b>Unsupported members.</b> <see cref="CommandResult.SetCookieName"/>,
/// <see cref="CommandResult.ClearCookieName"/>, <see cref="CommandResult.SetCookieSecureFlag"/>,
/// <see cref="CommandResult.RequestState"/>, <see cref="CommandResult.Principal"/>,
/// <see cref="CommandResult.RelayData"/> and <see cref="CommandResult.TerminateLocalSession"/>
/// are only used by the library's SP-side sign-in, ACS and logout commands, never by the
/// <see cref="Saml2Binding"/> implementations the StubIdp uses. Rather than silently dropping
/// them, <see cref="ExecuteAsync"/> throws <see cref="NotSupportedException"/> before writing
/// anything to the response when any of them is set.
/// </para>
/// </remarks>
public sealed class CommandResultIResult : IResult
{
    private readonly CommandResult commandResult;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResultIResult"/> class.
    /// </summary>
    /// <param name="commandResult">
    /// The library-produced command result to write. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="commandResult"/> is <see langword="null"/>.</exception>
    public CommandResultIResult(CommandResult commandResult)
    {
        ArgumentNullException.ThrowIfNull(commandResult);

        this.commandResult = commandResult;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="httpContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The command result sets a cookie, request-state,
    /// principal, relay-data or local-session member that this bridge does not support.</exception>
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ThrowIfUnsupportedMembersAreSet(commandResult);

        var response = httpContext.Response;

        var headers = response.Headers;

        var statusCode = commandResult.HttpStatusCode;
        var isRedirect = commandResult.Location is not null;

        // Legacy MVC parity: force 302 Found on redirects. See remarks.
        if (isRedirect)
        {
            statusCode = System.Net.HttpStatusCode.Found;
        }

        response.StatusCode = (int)statusCode;

        if (isRedirect)
        {
            // OriginalString preserves the library-built location byte-for-byte, including
            // SAML redirect-binding query composition.
            headers.Location = commandResult.Location!.OriginalString;
        }

        // Copy arbitrary headers that the library may have attached.
        foreach (var header in commandResult.Headers)
        {
            headers[header.Key] = header.Value;
        }

        if (isRedirect)
        {
            // No body for redirects - matches legacy RedirectResult behavior.
            return;
        }

        if (!string.IsNullOrEmpty(commandResult.ContentType))
        {
            response.ContentType = string.IsNullOrEmpty(commandResult.Content)
                ? commandResult.ContentType
                : WithUtf8Charset(commandResult.ContentType);
        }

        if (!string.IsNullOrEmpty(commandResult.Content))
        {
            // The library supplies pre-encoded HTML/XML for content responses (auto-post
            // forms, SOAP envelopes). Writing as UTF-8 keeps non-ASCII bytes literal.
            var buffer = System.Text.Encoding.UTF8.GetBytes(commandResult.Content);
            response.ContentLength ??= buffer.Length;
            await response.Body.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        }
    }

    private static void ThrowIfUnsupportedMembersAreSet(CommandResult commandResult)
    {
        var unsupported = new List<string>();

        if (!string.IsNullOrEmpty(commandResult.SetCookieName))
        {
            unsupported.Add(nameof(CommandResult.SetCookieName));
        }

        if (!string.IsNullOrEmpty(commandResult.ClearCookieName))
        {
            unsupported.Add(nameof(CommandResult.ClearCookieName));
        }

        if (commandResult.SetCookieSecureFlag)
        {
            unsupported.Add(nameof(CommandResult.SetCookieSecureFlag));
        }

        if (commandResult.RequestState is not null)
        {
            unsupported.Add(nameof(CommandResult.RequestState));
        }

        if (commandResult.Principal is not null)
        {
            unsupported.Add(nameof(CommandResult.Principal));
        }

        if (commandResult.RelayData is not null)
        {
            unsupported.Add(nameof(CommandResult.RelayData));
        }

        if (commandResult.TerminateLocalSession)
        {
            unsupported.Add(nameof(CommandResult.TerminateLocalSession));
        }

        if (unsupported.Count > 0)
        {
            throw new NotSupportedException(
                "The StubIdp CommandResult bridge does not support request-state cookies or "
                + "local sign-in/sign-out, which only SP-side Sustainsys.Saml2 commands use. "
                + "Unsupported members set: " + string.Join(", ", unsupported) + ".");
        }
    }

    private static string WithUtf8Charset(string contentType)
    {
        // Legacy System.Web appended "; charset=utf-8" to the Content-Type of every text body
        // written through the response writer (e.g. the library's auto-post "text/html").
        if (!MediaTypeHeaderValue.TryParse(contentType, out var mediaType) || mediaType.Charset.HasValue)
        {
            return contentType;
        }

        return contentType + "; charset=utf-8";
    }
}
