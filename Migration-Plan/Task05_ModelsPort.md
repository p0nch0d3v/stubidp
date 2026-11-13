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

## Status

✅ **done** — clean rebuild 0 errors (only the accepted NU1903/NU1904 advisories), **153/153 unit + 34/34 integration** tests green (28 new unit tests).

### As-built

Model ports under `new/src/Sustainsys.Saml2.StubIdp/Models/` (`IdpConfigurationModel` was already ported in Task03):

- **`AssertionModel.cs`** — legacy `CreateFromConfiguration()` (reading `ConfigurationManager.AppSettings`) became `Create(StubIdpOptions)`; `ToSaml2Response()` now takes the explicit request-scoped dependencies `(StubIdpUrlResolver, X509Certificate2)` instead of static `UrlResolver`/`CertificateHelper`. Preserved character-for-character: `NameIdentifier` claim with the `NameIdFormat.Unspecified` URI claim property, `SessionIndex` claim only when non-empty (`DefaultSessionIndex = "42"` kept), attribute-statement claims, `InResponseTo` only when non-empty, relay state, audience (`null` when empty), ACS destination, and issuer = resolved metadata URL (GUID-segment aware). All DataAnnotations kept; `ResponseBinding` still defaults to `Saml2BindingType.HttpPost`.
- **`AttributeStatementModel.cs`**, **`HomePageModel.cs`** (legacy file `HomepageModel.cs`, class `HomePageModel`), **`ManageIdpModel.cs`** — direct ports with DataAnnotations intact.
- **`LogoutModels.cs`** — `InitiateLogoutModel` (ctor still defaults `SessionIndex` to `42`), `RespondToLogoutRequestModel`, `ResponseModel`. `ToLogoutRequest`/`ToLogoutResponse` take `(StubIdpUrlResolver, X509Certificate2)` and preserve destinations, NameID/session data, relay state, issuer = metadata URL, and `SigningAlgorithm = SignedXml.XmlDsigRSASHA256Url`. `[DisplayName]` attributes were converted to `[Display(Name = ...)]` with identical strings (single DataAnnotations dialect across the port, same display metadata for `EditForm`).
- **`DiscoveryServiceModel.cs`** — property names deliberately stay lowercase (`entityID`, `@return`, `returnIDParam`, `isPassive`, `SelectedIdp`) to remain character-for-character compatible with the IdP Discovery Service Protocol parameter names for form/query binding; the parameterless ctor keeps the `returnIDParam = "entityID"` default and the request-specific `SelectedIdp` default moved to `Create(StubIdpUrlResolver)`.

New service under `new/src/Sustainsys.Saml2.StubIdp/Services/`:

- **`MetadataService.cs`** — port of the legacy static `MetadataModel`, registered **scoped** in `AddStubIdpCoreServices()` (it depends on the scoped `StubIdpUrlResolver`). Primary-constructor DI with `(StubIdpUrlResolver, CertificateService, IHttpContextAccessor)`. `CreateIdpMetadata(bool includeCacheDuration = true)` builds: entity ID = resolved metadata URL (GUID-segment aware), Redirect + POST SSO endpoints at `SsoServiceUrl`, SOAP `ArtifactResolutionService` (index 0, `IsDefault`), Redirect + POST SLO endpoints at `LogoutServiceUrl`, the host-selected signing `KeyDescriptor`, `PT15M` cache duration and one-day `ValidUntil`. `CreateFederationMetadata()` wraps `CreateIdpMetadata(false)` in an `EntitiesDescriptor` named `Sustainsys.Saml2.StubIdp Federation` (public const `FederationName`) with its own `PT15M`/one-day cache metadata. Descriptors are returned **unsigned** — signing stays in the endpoints (Task06), exactly as the legacy controllers did.

Tests: `Models/AssertionModelTests.cs` (9 cases), `Models/LogoutModelsTests.cs` (5), `Models/DiscoveryServiceModelTests.cs` (4), `Services/MetadataServiceTests.cs` (8, including signed-XML serialization via `ToXmlString(certificate, SignedXml.XmlDsigRSASHA256Url)` asserting SSO/SLO/artifact endpoints, `X509Certificate`, `cacheDuration="PT15M"`, `validUntil`, RSA-SHA256 `SignatureMethod`, and the federation name), plus one extended DI-registration assertion. Options defaults (`https://sp.example.com/SAML2/Acs`, `JohnDoe`, session `42`) are asserted through `AssertionModel.Create` and the existing Task02 binding tests. Validity assertions use a ±1h range around `UtcNow.AddDays(1)` so tests are wall-clock independent.

### As-built deviations

- **`Saml2Id`/`Saml2NameIdentifier` come from `Microsoft.IdentityModel.Tokens.Saml2`** (transitive via Sustainsys.Saml2 2.11.0) — unlike the legacy fork, the NuGet package does not define its own copies. The `Saml2Response` ctor is the `params ClaimsIdentity[]` overload; the single-identity call is source-identical to legacy.
- **Nullable annotations**: string properties are `string?` with `[Required]` preserved where legacy had it (EditForm validation semantics unchanged); legacy `Uri DestinationUrl` became `Uri?`.
- **Lambda fix**: legacy `YieldIfNotNullOrEmpty(SessionIndex).Select(s => new Claim(..., SessionIndex))` re-captured the outer variable; the port uses the yielded `s` (identical output, no behavior change).
- **Test-harness note**: a `static readonly HttpContextAccessor` cannot be shared across xUnit test classes running in parallel — `HttpContextAccessor` stores the context in an `AsyncLocal` that does not flow to sibling test threads. Tests therefore share a plain `static DefaultHttpContext` and wrap it in a fresh accessor per call.

### Carry-overs

- **Task06**: consume `MetadataService.CreateIdpMetadata()` / `CreateFederationMetadata()` and sign with `ToXmlString(certificateService.GetSigningCertificate(context), SignedXml.XmlDsigRSASHA256Url)` — the exact legacy controller call; content types `application/samlmetadata+xml` / `text/xml` per task file.
- **Task11**: `AssertionModel.Create(options.Value)` + `ToSaml2Response(urlResolver, certificateService.GetSigningCertificate(context))`; branch the emitted `CommandResult` on `model.ResponseBinding`.
- **Task12**: logout models are ready with explicit `(urlResolver, signingCertificate)` factories; `InitiateLogoutModel` keeps the `42` session-index default.
- **Task13**: `DiscoveryServiceModel` lowercase property names map 1:1 to the discovery protocol parameters — bind them directly (`[SupplyParameterFromQuery/Form]` without `Name=` overrides).
