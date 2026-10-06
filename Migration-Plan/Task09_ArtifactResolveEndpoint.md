# Task09 — ArtifactResolve SOAP endpoint

## Goal and scope

Implement the SOAP ArtifactResolve POST endpoint using Sustainsys pending artifacts and preserve the legacy response envelope and content-type quirk.

## Implementation

Add `POST /ArtifactResolve` and `POST /{idpId:guid}/ArtifactResolve` in `Endpoints/ArtifactResolveEndpoints.cs`. Read the XML request body with `XDocument`/`XElement` using `Saml2Namespaces.SoapEnvelope` and `Saml2Namespaces.Saml2P`; extract Artifact and ArtifactResolve `ID`. Decode Base64, then call `Saml2ArtifactBinding.PendingMessages.TryRemove(binaryArtifact, out message)`. Missing/invalid artifacts should produce HTTP 500 to match the legacy thrown failure — log the failure first with `ILogger` scoped to the artifact/request ID, without logging message payloads.

Serialize `message.ToXml()`. If `message.SigningCertificate` exists, parse to `XmlDocument`, sign with `XmlHelpers.Sign(cert, true)`, then use the signed outer XML. Build the ArtifactResponse with a new `Saml2Id`, request ID as `InResponseTo`, UTC issue instant via `ToSaml2DateTimeString`, success status, and message XML exactly in the legacy response structure. Use invariant formatting and preserve its issuer literal (`https://idp.example.com`) unless compatibility testing identifies a later intentional migration requirement. Do not normalize/reformat the embedded SAML XML.

Before: MVC loads `Request.InputStream`, consumes the static pending dictionary and returns `Content(response)` with default `text/html; charset=utf-8`. After: minimal API performs those steps and explicitly returns the same legacy content type.

```csharp
return Results.Content(envelope, "text/html; charset=utf-8", Encoding.UTF8);
```

Keep response construction in `Services/ArtifactResponseBuilder.cs` (or equivalent) so signed and unsigned branches are unit-testable independently of Kestrel.

## Test phase

Unit tests in `Services/ArtifactResponseBuilderTests.cs`: assert envelope names/namespaces, fresh response ID, requested `InResponseTo`, SAML timestamp form, embedded message XML, and signing branch adds a valid XML signature when a certificate is set.

Integration tests in `Endpoints/ArtifactResolveTests.cs`: seed `PendingMessages` with a known artifact and an `ISaml2Message`/real bound message, POST SOAP envelope, assert 200 + `text/html; charset=utf-8` + valid ArtifactResponse containing the message XML; POST unknown artifact and assert 500. Repeat route shape with GUID. Ensure tests remove pending items after execution to avoid shared static-state leakage.

Run `dotnet test new/StubIdp.slnx`.

## Before/after and test sketches

```csharp
// Before (MVC reads InputStream and returns Content(response))
var request = XElement.Load(Request.InputStream);
return Content(response);

// After (new/src/Sustainsys.Saml2.StubIdp/Endpoints)
var request = await XDocument.LoadAsync(HttpContext.Request.Body, LoadOptions.None, ct);
return Results.Content(envelope, "text/html; charset=utf-8", Encoding.UTF8);
```

Package pins: no new package; XML/signing APIs come from .NET 10 and Sustainsys.Saml2 2.11.0.

```csharp
// Unit
var xml = builder.Create(message, requestId);
Assert.Contains($"InResponseTo=\"{requestId}\"", xml);

// Integration
using var response = await client.PostAsync("/ArtifactResolve", new StringContent(soap, Encoding.UTF8, "text/xml"));
Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType); // media type only; legacy charset is asserted separately if needed
Assert.Contains("ArtifactResponse", await response.Content.ReadAsStringAsync());
```
