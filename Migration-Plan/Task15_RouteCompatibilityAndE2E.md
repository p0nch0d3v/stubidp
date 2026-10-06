# Task15 — Route compatibility matrix and SAML round-trip verification

## Goal and scope

Close route-coverage gaps against `legacy/.../App_Start/RouteConfig.cs`, then verify actual SSO and artifact round trips. Fix discovered mismatches in their implementation task, not by weakening assertions.

## Implementation

Create `tests/Sustainsys.Saml2.StubIdp.IntegrationTests/Compatibility/LegacyRouteMatrixTests.cs`. Enumerate default and named-GUID routes for `/`, `/Logout`, `/DiscoveryService`, `/Metadata`, `/Metadata/BrowserFriendly`, `/Federation`, `/Federation/BrowserFriendly`, `/Certificate`, `/Manage/CurrentConfiguration`, and `/Manage?idpId=...`; include POST `/ArtifactResolve` for both forms. Add `/Manage` query handling. For each, assert status, content type, and required Location/header semantics; use `GET` or `POST` according to legacy route/action behavior rather than accepting accidental 404/405.

Add `SsoRoundTripTests.cs`:

1. Create a Sustainsys `Saml2AuthenticationRequest`, bind it with Redirect, and GET the resulting URL from the test client.
2. Assert form prefill includes request ID and expected issuer/ACS.
3. Submit an assertion form with a chosen Redirect or POST binding. Decode/inflate returned SAMLResponse according to binding.
4. Parse response XML and verify signature against the public certificate returned by `/Certificate` (also exercise `/{guid}/Certificate` where applicable).
5. Verify `InResponseTo`, issuer, NameId, audience, and ACS.

Add artifact round-trip coverage: generate an artifact-bound response, obtain/retain the artifact in pending state, submit SOAP ArtifactResolve, and validate returned ArtifactResponse and embedded signed response. Isolate static `PendingMessages` entries and clear them in `finally`.

Example route matrix data:

```csharp
public static TheoryData<string, string> GetRoutes => new()
{
    { "GET", "/Metadata" },
    { "GET", $"/{TenantId:D}/Metadata" },
    { "GET", "/Federation/BrowserFriendly" },
    { "GET", $"/{TenantId:D}/Manage/CurrentConfiguration" },
};
```

Extend it to every route/action and assert MIME type per `PLAN.md` (not one blanket type).

## Test phase

The compatibility and end-to-end tests are the task output. Run `dotnet test new/StubIdp.slnx`; all routes and both round trips must pass with a clean test data directory. Confirm all named routes use `{idpId:guid}` and no changes were made under `legacy/`.

## Before/after and test sketches

```csharp
// Before (legacy MVC route definition)
routes.MapRoute("Default", "{controller}/{action}/{id}", defaults);

// After (new/src/Sustainsys.Saml2.StubIdp endpoint registrations)
app.MapGet("/Metadata", MetadataHandler);
app.MapGet("/{idpId:guid}/Metadata", NamedMetadataHandler);
```

Package pins: no package additions; use xunit 2.9.3, Microsoft.NET.Test.Sdk 18.10.1, MVC.Testing 10.0.12, and coverlet.collector 10.1.0 as pinned in Task01.

```csharp
// Unit/helper test
Assert.Equal($"/{tenantId:D}/Metadata", routeBuilder.BuildNamedMetadataPath(tenantId));

// Integration matrix example
using var response = await client.GetAsync($"/{tenantId:D}/Metadata");
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
Assert.Equal("application/samlmetadata+xml", response.Content.Headers.ContentType!.MediaType);
```
