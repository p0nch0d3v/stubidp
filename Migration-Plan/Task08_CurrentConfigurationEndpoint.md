# Task08 — Current-configuration JSON endpoint

## Goal and scope

Expose tenant configuration with legacy ETag/304 behavior and gzip/deflate response compression.

## Implementation

1. Register `AddResponseCompression` with `EnableForHttps = true` and gzip/deflate providers; call `UseResponseCompression()` before endpoint execution.
2. Map `GET /Manage/CurrentConfiguration` and `GET /{idpId:guid}/Manage/CurrentConfiguration`. Missing `idpId` uses the legacy default GUID. Retrieve the tenant from `ITenantConfigurationStore`; missing file returns 500 with legacy-compatible message.
3. Return exact JSON string as `application/json`; set quoted/valid ETag using the store's uppercase MD5 ETag value and private cache semantics (`Cache-Control: private`). If `If-None-Match` exactly matches the emitted ETag, return 304 with no body. Preserve the legacy equality behavior unless HTTP parsing requirements force weak/list handling; cover exact legacy case.

Before: `[Compress]` filters MVC output and `TestETag` compares header strings. After: response-compression middleware handles negotiated encodings and a minimal API handler implements ETag/304 before writing content.

```csharp
if (Request.Headers.IfNoneMatch.Any(value => value == quotedETag))
    return Results.StatusCode(StatusCodes.Status304NotModified);
Response.Headers.ETag = quotedETag;
Response.Headers.CacheControl = "private";
return Results.Text(configuration.JsonData, "application/json", Encoding.UTF8);
```

Set ETag formatting once and use the identical value for response and comparison. Return 304 before serialization/body output.

## Test phase

Integration tests in `Endpoints/CurrentConfigurationTests.cs` assert:

- Default and named routes return 200, `application/json`, body equal to tenant JSON, ETag present, and `Cache-Control: private`.
- Re-request with returned `If-None-Match` gives 304 and empty body.
- Request with `Accept-Encoding: gzip` receives `Content-Encoding: gzip` and decompresses to the exact JSON.
- Unknown tenant GUID returns 500.

Run `dotnet test new/StubIdp.slnx`; verify compression is activated for endpoint responses and is not double-applied.

## Before/after and test sketches

```csharp
// Before (MVC filter + BaseController helper)
[Compress]
return TestETag(fileData.JsonData, fileData.ETag, "application/json");

// After (new/src/Sustainsys.Saml2.StubIdp/Endpoints)
if (Request.Headers.IfNoneMatch.Contains(etag)) return Results.StatusCode(304);
Response.Headers.ETag = etag;
return Results.Text(json, "application/json", Encoding.UTF8);
```

Package pins: no new package; response compression is provided by the ASP.NET Core 10 shared framework. Existing production package pins stay unchanged.

```csharp
// Unit
Assert.Equal(ETagFor(json), store.Get(defaultTenantId)!.ETag);

// Integration
var first = await client.GetAsync("/Manage/CurrentConfiguration");
var secondRequest = new HttpRequestMessage(HttpMethod.Get, "/Manage/CurrentConfiguration");
secondRequest.Headers.IfNoneMatch.Add(first.Headers.ETag!);
using var second = await client.SendAsync(secondRequest);
Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
```
