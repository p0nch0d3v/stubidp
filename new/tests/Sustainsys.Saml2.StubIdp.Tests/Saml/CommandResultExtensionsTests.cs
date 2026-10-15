using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Sustainsys.Saml2.Saml2P;
using Sustainsys.Saml2.StubIdp.Saml;
using Sustainsys.Saml2.WebSso;

namespace Sustainsys.Saml2.StubIdp.Tests.Saml;

/// <summary>
/// Tests for <see cref="CommandResultExtensions.ToResult"/> and
/// <see cref="CommandResultIResult"/>: redirect, content, status, header and content-type
/// passthrough matching the legacy <c>Sustainsys.Saml2.Mvc.ToActionResult</c> behavior.
/// </summary>
public class CommandResultExtensionsTests
{
    private const string LocationUrl = "https://sp.example.com/SAML2/Acs?SAMLResponse=abc&RelayState=xyz";

    private static async Task<HttpResponse> ExecuteAsync(CommandResult commandResult)
    {
        return await ExecuteAsync(new CommandResultIResult(commandResult));
    }

    private static async Task<HttpResponse> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        return context.Response;
    }

    private static string ReadBodyAsString(HttpResponse response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Verifies that redirect responses produce 302 with the library-built Location header,
    /// mirroring the legacy <c>RedirectResult</c> parity.
    /// </summary>
    [Fact]
    public async Task Redirect_returns_302_with_location()
    {
        var result = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.SeeOther,
            Location = new Uri(LocationUrl),
        };

        var response = await ExecuteAsync(result);

        Assert.Equal((int)HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(LocationUrl, response.Headers.Location.ToString());
        Assert.Equal(string.Empty, ReadBodyAsString(response));
    }

    /// <summary>
    /// Verifies that the <c>ToResult()</c> extension produces the same 302 redirect behavior
    /// via the <see cref="IResult"/> contract used by minimal APIs.
    /// </summary>
    [Fact]
    public async Task ToResult_wraps_redirect_into_IResult()
    {
        var commandResult = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.Redirect, // Any non-303 redirect URL
            Location = new Uri(LocationUrl),
        };

        var response = await ExecuteAsync(commandResult.ToResult());

        Assert.Equal((int)HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(LocationUrl, response.Headers.Location.ToString());
    }

    /// <summary>
    /// Verifies that redirect responses never carry a body, even when the library populated
    /// <see cref="CommandResult.Content"/> for some reason.
    /// </summary>
    [Fact]
    public async Task Redirect_with_unexpected_content_does_not_write_a_body()
    {
        var result = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.Found,
            Location = new Uri(LocationUrl),
            Content = "should-not-be-written",
            ContentType = "text/plain",
        };

        var response = await ExecuteAsync(result);

        Assert.Equal(string.Empty, ReadBodyAsString(response));
    }

    /// <summary>
    /// Verifies that an OK command result with content writes the body and library-supplied
    /// content type - the auto-post HTML payload case.
    /// </summary>
    [Fact]
    public async Task Ok_content_response_writes_body_and_content_type()
    {
        const string html = "<html><body><form><input name='SAMLResponse' value='abc'/></form></body></html>";

        var result = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.OK,
            Content = html,
            ContentType = "text/html; charset=utf-8",
        };

        var response = await ExecuteAsync(result);

        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.ContentType);
        Assert.Equal(html, ReadBodyAsString(response));
    }

    /// <summary>
    /// Verifies that the real library auto-post binding output (<c>text/html</c>, no charset)
    /// is written as <c>text/html; charset=utf-8</c> - legacy System.Web parity - with the
    /// <c>SAMLResponse</c> form field intact.
    /// </summary>
    [Fact]
    public async Task Post_binding_output_gets_utf8_charset_and_keeps_saml_field()
    {
        var message = new Saml2LogoutResponse(Saml2StatusCode.Success)
        {
            DestinationUrl = new Uri("https://sp.example.com/SAML2/Acs"),
            Issuer = new global::Sustainsys.Saml2.Metadata.EntityId("https://stubidp.example/Metadata"),
        };
        var commandResult = Saml2Binding.Get(Saml2BindingType.HttpPost).Bind(message);
        Assert.Equal("text/html", commandResult.ContentType);

        var response = await ExecuteAsync(commandResult.ToResult());

        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.ContentType);
        var body = ReadBodyAsString(response);
        Assert.Equal(commandResult.Content, body);
        Assert.Contains("name=\"SAMLResponse\"", body);
    }

    /// <summary>
    /// Verifies that a content type without a charset gets <c>; charset=utf-8</c> appended.
    /// </summary>
    [Theory]
    [InlineData("text/html", "text/html; charset=utf-8")]
    [InlineData("text/xml", "text/xml; charset=utf-8")]
    [InlineData("application/samlmetadata+xml", "application/samlmetadata+xml; charset=utf-8")]
    public async Task Content_type_without_charset_gets_utf8_appended(string contentType, string expected)
    {
        var result = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.OK,
            Content = "x",
            ContentType = contentType,
        };

        var response = await ExecuteAsync(result);

        Assert.Equal(expected, response.ContentType);
    }

    /// <summary>
    /// Verifies that an explicit charset supplied by the library is kept unchanged.
    /// </summary>
    [Fact]
    public async Task Explicit_charset_is_preserved()
    {
        var result = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.OK,
            Content = "x",
            ContentType = "text/plain; charset=iso-8859-1",
        };

        var response = await ExecuteAsync(result);

        Assert.Equal("text/plain; charset=iso-8859-1", response.ContentType);
    }

    /// <summary>
    /// Verifies that an OK command result with no content does not write a body and leaves the
    /// content type untouched.
    /// </summary>
    [Fact]
    public async Task Ok_response_without_content_does_not_write_a_body()
    {
        var result = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.OK,
        };

        var response = await ExecuteAsync(result);

        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, ReadBodyAsString(response));
    }

    /// <summary>
    /// Verifies that arbitrary library-supplied response headers are copied through to the
    /// response - e.g. <c>Cache-Control</c> or <c>Pragma</c>.
    /// </summary>
    [Fact]
    public async Task Arbitrary_response_headers_are_copied()
    {
        var result = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.OK,
            Content = "x",
            ContentType = "text/plain",
        };
        result.Headers["X-Custom"] = "test-value";
        result.Headers["Pragma"] = "no-cache";

        var response = await ExecuteAsync(result);

        Assert.Equal("test-value", response.Headers["X-Custom"].ToString());
        Assert.Equal("no-cache", response.Headers["Pragma"].ToString());
    }

    /// <summary>
    /// Verifies that <see cref="CommandResult.Cacheability"/> is ignored and no
    /// <c>Cache-Control</c> header is emitted - legacy MVC <c>ToActionResult()</c> parity.
    /// </summary>
    [Theory]
    [InlineData(Cacheability.NotSpecified)]
    [InlineData(Cacheability.NoCache)]
    [InlineData(Cacheability.Private)]
    [InlineData(Cacheability.Server)]
    [InlineData(Cacheability.Public)]
    [InlineData(Cacheability.ServerAndPrivate)]
    public async Task Cacheability_is_ignored_and_no_cache_control_is_emitted(Cacheability cacheability)
    {
        var redirect = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.SeeOther,
            Location = new Uri(LocationUrl),
            Cacheability = cacheability,
        };
        var content = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.OK,
            Content = "x",
            ContentType = "text/html",
            Cacheability = cacheability,
        };

        var redirectResponse = await ExecuteAsync(redirect);
        var contentResponse = await ExecuteAsync(content);

        Assert.False(redirectResponse.Headers.ContainsKey("Cache-Control"));
        Assert.False(contentResponse.Headers.ContainsKey("Cache-Control"));
    }

    /// <summary>
    /// Verifies that a library-supplied <c>Cache-Control</c> entry in
    /// <see cref="CommandResult.Headers"/> is still copied through unchanged.
    /// </summary>
    [Fact]
    public async Task Library_supplied_cache_control_header_is_copied()
    {
        var result = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.OK,
            Content = "x",
            ContentType = "text/plain",
        };
        result.Headers["Cache-Control"] = "no-cache";

        var response = await ExecuteAsync(result);

        Assert.Equal("no-cache", response.Headers.CacheControl.ToString());
    }

    /// <summary>
    /// Verifies that unsupported cookie/principal/session state members fail explicitly before
    /// any part of the response is written.
    /// </summary>
    [Theory]
    [InlineData(nameof(CommandResult.SetCookieName))]
    [InlineData(nameof(CommandResult.ClearCookieName))]
    [InlineData(nameof(CommandResult.SetCookieSecureFlag))]
    [InlineData(nameof(CommandResult.RequestState))]
    [InlineData(nameof(CommandResult.Principal))]
    [InlineData(nameof(CommandResult.RelayData))]
    [InlineData(nameof(CommandResult.TerminateLocalSession))]
    public async Task Unsupported_members_throw_without_writing_the_response(string member)
    {
        var commandResult = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.SeeOther,
            Location = new Uri(LocationUrl),
        };
        switch (member)
        {
            case nameof(CommandResult.SetCookieName):
                commandResult.SetCookieName = "Saml2.abc";
                break;
            case nameof(CommandResult.ClearCookieName):
                commandResult.ClearCookieName = "Saml2.abc";
                break;
            case nameof(CommandResult.SetCookieSecureFlag):
                commandResult.SetCookieSecureFlag = true;
                break;
            case nameof(CommandResult.RequestState):
                commandResult.RequestState = new StoredRequestState(null!, null!, null!, null!);
                break;
            case nameof(CommandResult.Principal):
                commandResult.Principal = new System.Security.Claims.ClaimsPrincipal();
                break;
            case nameof(CommandResult.RelayData):
                commandResult.RelayData = new Dictionary<string, string> { ["key"] = "value" };
                break;
            case nameof(CommandResult.TerminateLocalSession):
                commandResult.TerminateLocalSession = true;
                break;
        }

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => commandResult.ToResult().ExecuteAsync(context));

        Assert.Contains(member, ex.Message);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        Assert.Equal(0, context.Response.Body.Length);
    }

    /// <summary>
    /// Verifies that real binding results used by StubIdp do not set members rejected by the
    /// bridge.
    /// </summary>
    [Theory]
    [InlineData(Saml2BindingType.HttpRedirect)]
    [InlineData(Saml2BindingType.HttpPost)]
    public async Task Library_bindings_do_not_trip_the_unsupported_member_guard(Saml2BindingType bindingType)
    {
        var message = new Saml2LogoutResponse(Saml2StatusCode.Success)
        {
            DestinationUrl = new Uri("https://sp.example.com/SAML2/Acs"),
            Issuer = new global::Sustainsys.Saml2.Metadata.EntityId("https://stubidp.example/Metadata"),
            RelayState = "xyz",
        };

        var response = await ExecuteAsync(Saml2Binding.Get(bindingType).Bind(message).ToResult());

        Assert.True(response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status302Found);
    }

    /// <summary>
    /// Verifies that the content body is UTF-8 encoded when written - matches the legacy
    /// <c>Encoding.UTF8.GetBytes(commandResult.Content)</c> behavior.
    /// </summary>
    [Fact]
    public async Task Content_is_written_as_utf8_bytes()
    {
        const string content = "P\u00e4\u00e4kaupunki \u2014 \u4e2d\u6587";

        var result = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.OK,
            Content = content,
            ContentType = "text/plain",
        };

        var response = await ExecuteAsync(result);

        response.Body.Position = 0;
        var bytes = new byte[response.Body.Length];
        response.Body.ReadExactly(bytes, 0, bytes.Length);
        Assert.Equal(content, Encoding.UTF8.GetString(bytes));
    }

    /// <summary>
    /// Verifies that an empty <see cref="CommandResult.ContentType"/> leaves the response
    /// content type untouched (no header is set).
    /// </summary>
    [Fact]
    public async Task Missing_content_type_does_not_set_response_content_type()
    {
        var result = new CommandResult
        {
            HttpStatusCode = HttpStatusCode.OK,
            Content = "x",
        };

        var response = await ExecuteAsync(result);

        Assert.Equal(string.Empty, response.ContentType ?? string.Empty);
    }

    /// <summary>
    /// Verifies that the null-argument guard on <see cref="CommandResultExtensions.ToResult"/>
    /// raises <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void ToResult_throws_for_null_command_result()
    {
        Assert.Throws<ArgumentNullException>(() => CommandResultExtensions.ToResult(null!));
    }

    /// <summary>
    /// Verifies that the null-argument guard on <see cref="CommandResultIResult"/> constructor
    /// raises <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void CommandResultIResult_throws_for_null_command_result()
    {
        Assert.Throws<ArgumentNullException>(() => new CommandResultIResult(null!));
    }

    /// <summary>
    /// Verifies that <see cref="CommandResultIResult.ExecuteAsync"/> raises
    /// <see cref="ArgumentNullException"/> for a null <see cref="HttpContext"/>.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_throws_for_null_http_context()
    {
        var result = new CommandResultIResult(new CommandResult
        {
            HttpStatusCode = HttpStatusCode.OK,
        });

        await Assert.ThrowsAsync<ArgumentNullException>(() => result.ExecuteAsync(null!));
    }
}
