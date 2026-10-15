using Sustainsys.Saml2.WebSso;

namespace Sustainsys.Saml2.StubIdp.Saml;

/// <summary>
/// Extension methods that adapt <see cref="CommandResult"/> values produced by the
/// <c>Sustainsys.Saml2</c> library to the ASP.NET Core minimal-API/endpoint result contract.
/// </summary>
public static class CommandResultExtensions
{
    /// <summary>
    /// Converts a <see cref="CommandResult"/> to an <see cref="IResult"/> that can be returned
    /// from a minimal-API endpoint, a Razor component, or any other ASP.NET Core handler.
    /// </summary>
    /// <param name="commandResult">The library-produced command result.</param>
    /// <returns>An <see cref="IResult"/> that writes the equivalent response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="commandResult"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This replaces the legacy <c>Sustainsys.Saml2.Mvc.ToActionResult()</c> and the
    /// System.Web <c>CommandResult.Apply(...)</c> helper. It preserves the externally visible
    /// behavior of both: redirects produce <c>302 Found</c> with the library-built
    /// <c>Location</c>, content responses write the body with the library's
    /// <c>ContentType</c> (typically <c>text/html</c> for auto-post binding or
    /// <c>text/xml</c> for SOAP envelopes).
    /// </para>
    /// <para>
    /// Do not rebuild query parameters or SAML form HTML by hand - the library's bindings
    /// already produce byte-stable representations that external SPs decode.
    /// </para>
    /// </remarks>
    public static IResult ToResult(this CommandResult commandResult)
    {
        ArgumentNullException.ThrowIfNull(commandResult);

        return new CommandResultIResult(commandResult);
    }
}