# Task07 — Certificate download endpoint

## Goal and scope

Port the public signing certificate download as `.cer` bytes, selecting the correct certificate by request host.

## Implementation

Add `GET /Certificate` and `GET /{idpId:guid}/Certificate` in `Endpoints/CertificateEndpoints.cs` (or endpoint registration). Select the same host-specific certificate as `CertificateService`, export only `X509ContentType.Cert`, and set `Content-Disposition: attachment` with the legacy filename for the selected certificate. Preserve response media type `text/plain` and exact DER certificate bytes. The endpoint must not return a PFX/private key.

Before: MVC `CertificateController.Index` selects a `.cer` file based on host. After: minimal API uses `CertificateService` and the request host; named GUID is accepted for route parity and does not affect host choice.

```csharp
var cert = certificateService.GetSigningCertificate(context);
var bytes = cert.Export(X509ContentType.Cert);
return Results.File(bytes, "text/plain", downloadName: selectedFileName);
```

Check ASP.NET Core's resulting `Content-Disposition` filename semantics; if the framework emits a different filename than legacy, set the header explicitly and test it.

## Test phase

Add integration tests for default and named endpoint variants. Assert status 200, `Content-Type` starts with `text/plain`, disposition is attachment with expected `.cer` filename, and response bytes equal the configured `.cer` file/public certificate export. Set `Host: stubidp.kentor.se` and assert selected bytes match `Kentor.AuthServices.StubIdp.cer`; test a normal host selects `stubidp.sustainsys.com.cer`.

Run `dotnet test new/StubIdp.slnx`. Check no test response or static asset exposes a private key.

## Before/after and test sketches

```csharp
// Before (MVC serves the selected .cer asset)
return File(certPath, "text/plain", Path.GetFileName(certPath));

// After (new/src/Sustainsys.Saml2.StubIdp/Endpoints)
var certificate = certificateService.GetSigningCertificate(context);
return Results.File(certificate.Export(X509ContentType.Cert), "text/plain", fileName);
```

Package pins: no package additions; use .NET 10 X509 APIs and existing Task01 test dependencies.

```csharp
// Unit
Assert.Equal(X509ContentType.Cert, X509Certificate2.GetCertContentType(bytes));

// Integration
using var response = await client.GetAsync("/Certificate");
Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
Assert.Equal(expectedDer, await response.Content.ReadAsByteArrayAsync());
```
