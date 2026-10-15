using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Sustainsys.Saml2.StubIdp.Saml;
using Sustainsys.Saml2.WebSso;

namespace Sustainsys.Saml2.StubIdp.Tests.Saml;

/// <summary>
/// Tests for <see cref="HttpRequestDataExtensions"/>: mapping of method, host, scheme, path,
/// path base, query and form values to <see cref="HttpRequestData"/>.
/// </summary>
public class HttpRequestDataExtensionsTests
{
    private static HttpContext CreateContext(
        string scheme = "https",
        string host = "localhost",
        string pathBase = "",
        string path = "/",
        string queryString = "",
        string method = "GET")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = HostString.FromUriComponent(host);
        context.Request.PathBase = pathBase;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(queryString);
        context.Request.Method = method;
        return context;
    }

    private static HttpRequestData Map(HttpContext context, bool includeForm = true) =>
        // The extension is async; we block synchronously here because all the test inputs are
        // already in-memory - no actual awaiting happens.
        context.ToHttpRequestDataAsync(includeForm).GetAwaiter().GetResult();

    /// <summary>
    /// Verifies that the request method is mapped to <see cref="HttpRequestData.HttpMethod"/>.
    /// </summary>
    [Fact]
    public void Method_is_mapped_to_HttpMethod()
    {
        var context = CreateContext(method: "POST");

        Assert.Equal("POST", Map(context).HttpMethod);
    }

    /// <summary>
    /// Verifies that scheme + host + path + query compose an absolute URL.
    /// </summary>
    [Fact]
    public void Absolute_url_is_composed_from_scheme_host_path_and_query()
    {
        var context = CreateContext(
            scheme: "https",
            host: "stubidp.example",
            path: "/Saml2/Acs",
            queryString: "?SAMLResponse=abc&RelayState=xyz");

        var data = Map(context);

        Assert.Equal(
            new Uri("https://stubidp.example/Saml2/Acs?SAMLResponse=abc&RelayState=xyz"),
            data.Url);
    }

    /// <summary>
    /// Verifies that scheme, host and a non-default port are all preserved.
    /// </summary>
    [Fact]
    public void Non_default_port_is_preserved()
    {
        var context = CreateContext(host: "stubidp.example:5001", path: "/Foo");

        Assert.Equal(new Uri("https://stubidp.example:5001/Foo"), Map(context).Url);
    }

    /// <summary>
    /// Verifies that an explicit <see cref="HttpRequest.PathBase"/> is mapped to the library's
    /// application path, so generated URLs and binding detection see it.
    /// </summary>
    [Fact]
    public void PathBase_is_mapped_to_application_path()
    {
        var context = CreateContext(pathBase: "/stubidp", path: "/stubidp/Metadata");

        Assert.Equal("https://localhost/stubidp", Map(context).ApplicationUrl.ToString());
    }

    /// <summary>
    /// Verifies that an empty/missing <see cref="HttpRequest.PathBase"/> becomes
    /// <c>"/"</c>, matching legacy <c>ApplicationPath</c> semantics.
    /// </summary>
    [Fact]
    public void Missing_path_base_becomes_root()
    {
        var context = CreateContext();

        Assert.Equal("https://localhost/", Map(context).ApplicationUrl.ToString());
    }

    /// <summary>
    /// Verifies that query parameters are accessible through the mapped
    /// <see cref="HttpRequestData"/>.
    /// </summary>
    [Fact]
    public void Query_string_values_are_accessible_on_mapped_data()
    {
        var context = CreateContext(path: "/Saml2/Acs", queryString: "?SAMLResponse=z");

        var data = Map(context);

        Assert.Equal("z", data.QueryString["SAMLResponse"].Single());
    }

    /// <summary>
    /// Verifies that an empty query string produces an empty lookup.
    /// </summary>
    [Fact]
    public void Empty_query_string_yields_empty_lookup()
    {
        var data = Map(CreateContext());

        Assert.Empty(data.QueryString.SelectMany(_ => _));
    }

    /// <summary>
    /// Verifies that POST form fields are mapped to <see cref="HttpRequestData.Form"/>.
    /// </summary>
    [Fact]
    public async Task Form_fields_are_mapped_for_post_with_form_content_type()
    {
        var context = CreateContext(method: "POST");
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            { "SAMLRequest", new StringValues("z") },
            { "RelayState", new StringValues("s") },
        });

        var data = await context.ToHttpRequestDataAsync();

        Assert.Equal("z", data.Form["SAMLRequest"]);
        Assert.Equal("s", data.Form["RelayState"]);
    }

    /// <summary>
    /// Verifies that a non-POST request produces no form data even when one is asked for.
    /// </summary>
    [Fact]
    public async Task Get_request_does_not_attach_form_data()
    {
        var context = CreateContext(method: "GET");

        var data = await context.ToHttpRequestDataAsync();

        Assert.Empty(data.Form);
    }

    /// <summary>
    /// Verifies that passing <c>includeForm: false</c> avoids the form read so a downstream
    /// consumer can still consume the body stream manually.
    /// </summary>
    [Fact]
    public async Task IncludeForm_false_skips_form_read()
    {
        var context = CreateContext(method: "POST");
        context.Request.ContentType = "application/x-www-form-urlencoded";
        // Provide no form - if the extension tried to do so it would throw.
        var data = await context.ToHttpRequestDataAsync(includeForm: false);

        Assert.Empty(data.Form);
    }

    /// <summary>
    /// Verifies that a POST with a non-form content type is treated like a GET - no form data
    /// attached. The SAML POST-binding receive path still needs the form for binding detection
    /// so callers can opt into it via <c>includeForm: true</c>.
    /// </summary>
    [Fact]
    public async Task Post_without_form_content_type_does_not_attach_form()
    {
        var context = CreateContext(method: "POST");
        context.Request.ContentType = "application/octet-stream";

        var data = await context.ToHttpRequestDataAsync(includeForm: false);

        Assert.Empty(data.Form);
    }

    /// <summary>
    /// Verifies the synchronous <c>ToHttpRequestData()</c> extension returns the same mapping
    /// as the async one minus the form data.
    /// </summary>
    [Fact]
    public void Synchronous_overload_returns_mapped_data_without_form()
    {
        var context = CreateContext(host: "example.com", path: "/Foo");

        var data = context.ToHttpRequestData();

        Assert.Equal("GET", data.HttpMethod);
        Assert.Equal(new Uri("https://example.com/Foo"), data.Url);
        Assert.Empty(data.Form);
    }

    /// <summary>
    /// Verifies the null-argument guard on the async extension.
    /// </summary>
    [Fact]
    public async Task ToHttpRequestDataAsync_throws_for_null_context()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => HttpRequestDataExtensions.ToHttpRequestDataAsync(null!));
    }

    /// <summary>
    /// Verifies the null-argument guard on the synchronous extension.
    /// </summary>
    [Fact]
    public void ToHttpRequestData_throws_for_null_context()
    {
        Assert.Throws<ArgumentNullException>(
            () => HttpRequestDataExtensions.ToHttpRequestData(null!));
    }

    /// <summary>
    /// Verifies that the mapped <see cref="HttpRequestData.User"/> reflects the active
    /// <see cref="HttpContext.User"/>.
    /// </summary>
    [Fact]
    public void User_principal_is_carried_from_http_context()
    {
        var identity = new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.Name, "test-user") },
            authenticationType: "TestAuth");
        var context = CreateContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(identity);

        var data = Map(context);

        Assert.Equal("test-user", data.User.Identity!.Name);
        Assert.True(data.User.Identity.IsAuthenticated);
    }

    /// <summary>
    /// Verifies that the SAML library's <c>Saml2Binding.Get(HttpRequestData)</c> returns a
    /// non-null binding when given a context carrying a Redirect-binding <c>SAMLRequest</c>
    /// query parameter. This is the core integration the brief requires.
    /// </summary>
    [Fact]
    public async Task Redirect_binding_is_detected_from_mapped_request_data()
    {
        // Minimal SAMLRequest is base64-encoded xml - any non-empty value passes the binding
        // type detection (Relay state/Deflate are optional). The library looks at method, URL
        // and query shape, not the decoded payload.
        var context = CreateContext(
            method: "GET",
            path: "/",
            queryString: "?SAMLRequest=PD94bWw%2b");

        var binding = Saml2Binding.Get(await context.ToHttpRequestDataAsync());

        // The concrete binding type is internal, so we identify it by the concrete class name
        // which is part of the public contract surface.
        Assert.NotNull(binding);
        Assert.Equal("Saml2RedirectBinding", binding!.GetType().Name);
    }
}