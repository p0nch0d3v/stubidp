# Task05 — Port models and metadata service

## Goal and scope

Port the legacy model behavior into `new/src/Sustainsys.Saml2.StubIdp/Models/`, retaining validation/display metadata while replacing static configuration and helpers with injected services. Add `Services/MetadataService.cs` for metadata construction and signing.

## Implementation

Port `AssertionModel`, `AttributeStatementModel`, `HomePageModel`, the three logout models, `DiscoveryServiceModel`, `ManageIdpModel`, and `IdpConfigurationModel`. Keep DataAnnotations (`Required`, `StringLength`, `Display`) used by MVC and needed by Blazor `EditForm`. Convert legacy static factory methods to injected configuration or explicit factory methods that accept `StubIdpOptions`, `ICertificateService`, and `IStubIdpUrlResolver`.

For assertion responses preserve:

- NameIdentifier claim and `NameIdFormat.Unspecified` URI.
- SessionIndex claim when non-empty; default session index remains `42`.
- Attribute statement claim type/value pairs.
- `InResponseTo`, RelayState, audience, ACS, and issuer equal to resolved metadata URL.
- Signing certificate selected for the current request host and default ACS/name ID from options.

`ToSaml2Response` should receive an explicit context/service dependency rather than reading static `UrlResolver` or `CertificateHelper`. Logout request/response models must preserve destinations, NameID/session data, relay state, and SHA-256 signature algorithm where the API supports specifying it.

Port `MetadataModel` behavior to `MetadataService`: IdP descriptor includes Redirect and POST SSO, SOAP ArtifactResolutionService index 0/default, Redirect and POST SLO, selected signing key, 15-minute `CacheDuration`, and one-day `ValidUntil`. Federation descriptor is named `Sustainsys.Saml2.StubIdp Federation`, wraps the IdP descriptor without duplicating IdP cache metadata, and has federation cache/validity values.

Before: `AssertionModel.CreateFromConfiguration()` reads `ConfigurationManager.AppSettings`; `MetadataModel` uses static URL/certificate access. After: factory/service methods receive `IOptions<StubIdpOptions>`, URL resolver, and certificate service through DI.

## Test phase

Add unit tests under `Models/` and `Services/MetadataServiceTests.cs`:

1. Build a response and inspect claims for NameId, SessionIndex, custom attributes, audience, ACS, `InResponseTo`, and metadata issuer.
2. Build logout request/response and assert destination, RelayState/NameID, and SHA-256 signing configuration.
3. Serialize IdP/federation metadata and assert SSO/SLO/Artifact endpoints, key/certificate presence, federation name, `CacheDuration`, and `ValidUntil`.
4. Verify options provide `https://sp.example.com/SAML2/Acs`, `JohnDoe`, and session default `42`.

Run `dotnet test new/StubIdp.slnx`; keep tests independent of the current wall clock by asserting validity is in the expected future range.

## Before/after and test sketches

```csharp
// Before (configuration and services are static)
var model = AssertionModel.CreateFromConfiguration();
var response = model.ToSaml2Response();

// After (configuration and request-specific dependencies are explicit)
var model = AssertionModel.Create(options.Value);
var response = model.ToSaml2Response(urlResolver, certificateService.GetSigningCertificate(context));
```

Package pins: no new package; use Sustainsys.Saml2 2.11.0 and the pinned xUnit/Moq test stack from Task01.

```csharp
// Unit
Assert.Contains(response.ClaimsIdentity.Claims, c => c.Type == Saml2ClaimTypes.SessionIndex && c.Value == "42");
Assert.Equal(urlResolver.MetadataUrl, response.Issuer);

// Integration
using var response = await client.GetAsync("/Metadata");
var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```
