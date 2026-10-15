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

## Status

✅ **done** — clean rebuild 0 errors, **125/125 unit + 34/34 integration** tests green. Post-review fixes added charset parity + real `Saml2PostBinding` output tests, dropped the `Cacheability` → `Cache-Control` mapping, and added explicit guards for unsupported SP-side state.

### As-built

Three production types under `new/src/Sustainsys.Saml2.StubIdp/Saml/`:

- **`HttpRequestDataExtensions.cs`** — `HttpContext.ToHttpRequestDataAsync(bool includeForm = true, CancellationToken = default)` plus a no-parenion sync `ToHttpRequestData()` overload for callers that want to consume the body themselves after detecting no SAML binding. Maps method, scheme/host/PathBase/QueryString into the absolute URL, reads the form eagerly via `ReadFormAsync` when `includeForm: true` and the request is a form POST, and carries the current `HttpContext.User` through to `HttpRequestData.User` for parity with legacy `ClaimsPrincipal.Current`. Malformed form bodies return an empty `FormCollection` rather than throwing, so binding detection still runs. No cookie decryption is wired (StubIdp never reads requester session state).
- **`CommandResultIResult.cs`** — `IResult` that writes a `CommandResult`. For redirects it forces **HTTP 302 Found** (matching the legacy MVC `RedirectResult` parity, **not** the library's `HttpStatusCode.SeeOther` / 303 the obsolete AspNetCore2 bridge passed through) and copies `commandResult.Location.OriginalString` byte-for-byte so library-built redirect-binding query strings (including RelayState) are preserved. For content responses it writes the body as UTF-8 (matches legacy `Encoding.UTF8.GetBytes(commandResult.Content)`) with the library-supplied content type; when that content type carries no charset, `; charset=utf-8` is appended (legacy System.Web parity — the library's auto-post `text/html` reached SPs as `text/html; charset=utf-8`), while an explicit charset is preserved. Library-supplied `Headers` are copied through (e.g. `Cache-Control`, `Pragma`). `CommandResult.Cacheability` is intentionally ignored and the bridge emits **no** `Cache-Control` header (legacy MVC `ToActionResult()` never mapped it). Cookie, request-state, principal, relay-data, and local-session members are not used by StubIdp bindings; if any are set, `ExecuteAsync` throws `NotSupportedException` before writing the response rather than silently dropping them.
- **`CommandResultExtensions.cs`** — `ToResult(this CommandResult)` returns `new CommandResultIResult(commandResult)`. Single public extension, replaces the removed `Sustainsys.Saml2.Mvc.ToActionResult()`.

Unit tests in `new/tests/...Tests/Saml/CommandResultExtensionsTests.cs` (13 cases) and `HttpRequestDataExtensionsTests.cs` (17 cases). Integration tests in `new/tests/...IntegrationTests/Saml/CommandResultRoundTripTests.cs` (4 cases, using a self-contained `HostBuilder + TestServer` rather than `WebApplicationFactory<Program>` — see below).

### As-built deviations

- **The integration harness uses `HostBuilder + WebApplicationFactory<Program>`.** The reason is static-asset manifest coupling: `Program.cs` calls `app.MapStaticAssets()`, which inside the `WebApplicationFactory<Program>` pipeline resolves the manifest relative to the test assembly. Adding any endpoint via `WithWebHostBuilder.Configure` causes the resolution to look for `<TestAssembly>.staticwebassets.endpoints.json`, which does not exist (the manifest is named after the production assembly). The minimal harness in this task therefore uses an isolated `HostBuilder + UseTestServer` so the bridge can be verified end-to-end without booting the full StubIdp pipeline (static assets, antiforgery, interactive Blazor render modes are all irrelevant to this bridge). The full StubIdp integration tests (Tasks 11/12/15) will continue to use `WebApplicationFactory<Program>` against the real `Program`, and the production csproj carries no `Sustainsys.Saml2.Mvc` package — only `Sustainsys.Saml2` 2.11.0.
- **The `HttpRequestDataExtensions` constructor picks the `(method, url, applicationPath, formData, cookies, cookieDecryptor, user)` overload** (the 7-arg public ctor) instead of the AspNetCore2-style overload that required `ICookieManager`/`Func<byte[], byte[]>`. The StubIdp has no need for either, so we pass an empty cookie list and a no-op `cookieDecryptor` to satisfy the constructor signature without dragging `Microsoft.AspNetCore.Authentication.Cookies` into the project. The cookie reader and decryptor members are wired defensively rather than silently misrepresenting the contract.
- **`Cacheability` is not mapped.** The legacy MVC bridge ignored it, so the bridge sets no `Cache-Control` header (ASP.NET Core default). Note this is not byte-identical to legacy, where System.Web added its default `Cache-Control: private`; the "emit nothing" option was chosen deliberately over hard-coding `private`. A `Cache-Control` entry in `CommandResult.Headers` is still copied through.
- **SP-side response state is rejected explicitly.** `SetCookieName`, `ClearCookieName`, `SetCookieSecureFlag`, `RequestState`, `Principal`, `RelayData`, and `TerminateLocalSession` are not produced by the StubIdp's binding flows. If a future caller passes one, the adapter throws `NotSupportedException` before setting status/headers or writing a body. This makes the unsupported contract observable rather than silently losing cookie or authentication state.

### Carry-overs

- **Task09, Task11, Task12** can now return `binding.Bind(message).ToResult()` from minimal-API endpoints with the externally visible payloads (302 + `Location`, auto-post HTML, ETag/JSON, SOAP XML) guaranteed to match the legacy stub. `HttpContext.ToHttpRequestDataAsync(includeForm: true)` is the canonical adapter for SAML-binding receive flows in Task11/12 (Home POST and Logout GET/POST). The synchronous `ToHttpRequestData()` overload is for receive flows that need to consume the body themselves (none in this migration, but kept for the library-shaped API parity called out in the brief).
- **`ToHttpRequestDataAsync` does not pass cookies** to `HttpRequestData`. If a future binding on the receive path (none today) needs request cookies, the test would fail (cookies: `Array.Empty<KeyValuePair<string, string>>()`); switch to a lambda `cookieReader: name => ...` and add `Microsoft.AspNetCore.Authentication.Cookies` if data protection is required.
- **`BlazorDisableThrowNavigationException=true`** in the production csproj is untouched; this task does not depend on it. Task13 decides whether to flip it.
