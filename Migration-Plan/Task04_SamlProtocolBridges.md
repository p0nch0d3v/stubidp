# Task04 — SAML bridges: `HttpRequestData` + `CommandResult` → `IResult`

## Goal and scope

Port the ASP.NET Core 2 bridge patterns into the application, adapting them to ASP.NET Core 10 and avoiding System.Web dependencies. These adapters are protocol infrastructure for Tasks09, 11, and 12.

## Implementation

Create `Saml/HttpRequestDataExtensions.cs` with `HttpContext.ToHttpRequestData(bool includeBody, ...)`. Map method, absolute URL, query, headers, form/body, and PathBase using public Sustainsys.Saml2 APIs. For form posts, call `ReadFormAsync` and preserve all fields including `SAMLRequest`, `SAMLResponse`, and `RelayState`; avoid consuming a body twice. No cookie decryption is needed by this StubIdp. Adapt the legacy `HttpRequestExtensions.cs` and `legacy/Sustainsys.Saml2.AspNetCore2/` behavior, not its obsolete framework assumptions.

Create `Saml/CommandResultExtensions.cs` with `ToResult(this CommandResult)` returning `CommandResultIResult`. Map status code, response headers, `Location`, content and content type; redirect responses must preserve library-generated binding query strings. Handle null optional headers/content defensively. Cookie/principal members are unused by StubIdp bindings; if available in this package API, document and safely map them rather than silently misrepresenting them.

Before: MVC uses `Bind(...).ToActionResult()` from `Sustainsys.Saml2.Mvc`. After: minimal endpoint returns `binding.Bind(message).ToResult()` and a local `IResult` writes the equivalent response.

Representative endpoint usage:

```csharp
var result = Saml2Binding.Get(Saml2BindingType.HttpRedirect).Bind(message);
return result.ToResult();
```

For auto-post binding, write the HTML payload with its library-supplied `ContentType`; for redirects, write the status and `Location` header, with no body unless the command result has one. Do not hard-code a new SAML form or rebuild query parameters in this bridge.

## Test phase

Add focused unit tests in `Saml/CommandResultExtensionsTests.cs` for Redirect (302 + exact `Location`), POST auto-submit HTML containing the expected SAML field, arbitrary response header copying, content type and status passthrough. Add request-adapter tests for method, absolute URL, PathBase, query and form values. Use `DefaultHttpContext` with a `FormCollection` for mapping tests.

Add a TestServer/`WebApplicationFactory` round-trip test that returns a converted IResult from a test minimal endpoint, then asserts status, `Location`, body, and headers through `HttpClient`.

Run `dotnet test new/StubIdp.slnx`. Ensure the project only references `Sustainsys.Saml2` 2.11.0; do not add the legacy MVC package.

## Before/after and test sketches

```csharp
// Before (System.Web + MVC bridge)
var data = Request.ToHttpRequestData(true, null);
return binding.Bind(message).ToActionResult();

// After (new/src/Sustainsys.Saml2.StubIdp/Saml)
var data = await HttpContext.ToHttpRequestDataAsync(includeBody: true);
return binding.Bind(message).ToResult();
```

Package pins: `Sustainsys.Saml2` 2.11.0; no MVC bridge package. Test dependencies remain xunit 2.9.3, Microsoft.NET.Test.Sdk 18.10.1, and Microsoft.AspNetCore.Mvc.Testing 10.0.12 (integration).

```csharp
// Unit
Assert.Equal(StatusCodes.Status302Found, result.StatusCode);
Assert.Equal(expectedLocation, result.Headers.Location);

// Integration (round-trip through TestServer)
using var response = await client.GetAsync("/test/command-result");
Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
```
