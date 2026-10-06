# Task06 — Metadata & Federation endpoints

## Goal and scope

Expose signed IdP and federation metadata at both default and GUID-prefixed legacy-compatible routes.

## Implementation

In `Program.cs` or `Endpoints/MetadataEndpoints.cs`, map:

- `GET /Metadata` and `GET /{idpId:guid}/Metadata`
- `GET /Metadata/BrowserFriendly` and `GET /{idpId:guid}/Metadata/BrowserFriendly`
- `GET /Federation` and `GET /{idpId:guid}/Federation`
- `GET /Federation/BrowserFriendly` and `GET /{idpId:guid}/Federation/BrowserFriendly`

Use `MetadataService` and the URL resolver so named routes generate entity IDs and endpoint locations containing that same GUID segment. Serialize the descriptors to XML, sign with the selected certificate and `SignedXml.XmlDsigRSASHA256Url`, and return exact MIME values: `application/samlmetadata+xml` for regular endpoints, `text/xml` for BrowserFriendly endpoints. BrowserFriendly controls presentation/serialization only; it must still return valid signed metadata.

Before: controller actions return signed XML through MVC `Content(...)` and route generation. After: explicit minimal endpoint handlers return `Results.Text(xml, contentType, Encoding.UTF8)` (or a dedicated result that preserves XML declaration/encoding) and consume route `idpId` for URL generation.

Suggested endpoint grouping:

```csharp
app.MapGet("/{idpId:guid}/Metadata", (Guid idpId, ...) => ...);
app.MapGet("/Metadata", (...) => ...);
```

Register more-specific named routes and default routes explicitly; ensure no catch-all route shadows the endpoint handlers.

## Test phase

Add integration tests in `Endpoints/MetadataEndpointsTests.cs` using `WebApplicationFactory<Program>`:

- Each of the eight routes returns 200 and the exact content type.
- Parse each body as XML; verify `ds:Signature` exists and signature references the document ID/root as expected.
- IdP metadata has entity ID including the named GUID, SSO/SLO and Artifact endpoints, `CacheDuration`, and `validUntil`.
- Federation metadata includes `Sustainsys.Saml2.StubIdp Federation` and a child IdP descriptor.

Use a test certificate/configuration provisioned for the test host. Run `dotnet test new/StubIdp.slnx`.

## Before/after and test sketches

```csharp
// Before (controller action)
return Content(MetadataModel.CreateIdpMetadata().ToXmlString(), "application/samlmetadata+xml");

// After (new/src/Sustainsys.Saml2.StubIdp/Endpoints)
var xml = metadataService.CreateSignedIdpMetadata(idpId).ToXmlString();
return Results.Text(xml, "application/samlmetadata+xml", Encoding.UTF8);
```

Package pins: no additions; `Sustainsys.Saml2` 2.11.0 is the production API and test package versions are inherited from Task01.

```csharp
// Unit
Assert.Contains("SingleSignOnService", metadataXml);

// Integration
using var response = await client.GetAsync($"/{tenantId:D}/Metadata");
Assert.Equal("application/samlmetadata+xml", response.Content.Headers.ContentType!.MediaType);
Assert.Contains(tenantId.ToString("D"), await response.Content.ReadAsStringAsync());
```
