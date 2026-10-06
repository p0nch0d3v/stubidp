# Task03 — Core services: certificate, URL resolution, tenant store

## Goal and scope

Replace static MVC helpers with injectable services. Preserve legacy host selection, URL composition, default-tenant behavior, null-on-unknown lookup, and uppercase MD5 ETag semantics.

## Implementation

Create `Services/CertificateService.cs`, `Services/StubIdpUrlResolver.cs`, `Services/ITenantConfigurationStore.cs`, and `Services/TenantConfigurationStore.cs`; register them as singletons except the URL resolver (scoped). Add/port `Models/IdpConfigurationModel.cs` using `System.Text.Json`.

### Certificate service

Load both PFX files once using `X509Certificate2` with `X509KeyStorageFlags.MachineKeySet` (plus `EphemeralKeySet` where supported and appropriate). Resolve the selected certificate from the current request host: exact `stubidp.kentor.se` selects `LegacyKentor`; all other hosts select `Default`. Expose `SigningCertificate` and a `KeyDescriptor` holding an X509 key entry. Fail with a clear startup/runtime configuration error for missing files or invalid passwords. The singleton service owns the `X509Certificate2` instances for the application lifetime — never dispose them per request; implement `IDisposable` on the service only if certificates are ever reloaded. All public members of these services get XML documentation comments, and public method arguments are guarded with `ArgumentNullException.ThrowIfNull(...)`.

Before: `CertificateHelper.SigningCertificate` reads `HttpContext.Current` and static PFX fields. After:

```csharp
public X509Certificate2 GetSigningCertificate(HttpContext context) =>
    string.Equals(context.Request.Host.Host, legacy.HostName, StringComparison.OrdinalIgnoreCase)
        ? legacyCertificate : defaultCertificate;
```

### URL resolver

Implement methods/properties for root, SSO, Metadata, Manage, ArtifactResolve, and Logout absolute URLs. Use `IHttpContextAccessor`, `Request.Scheme`, `Request.Host`, `Request.PathBase`, and detect a canonical GUID in the first path segment below PathBase. Preserve a GUID segment when generating each endpoint URL; avoid duplicating PathBase or GUID segments.

### Tenant store

Use default GUID `e73d98ff-0f1c-4cc2-8808-6d1bf028a8a9` → `default.json`; every other GUID maps to `{guid:D}.json`. Store a `ConcurrentDictionary<Guid, IdpConfigurationModel>`. On cache miss, read and parse the file once using async file I/O (`File.ReadAllTextAsync`); absent file returns null and is not cached as a non-null configuration. Saving writes indented JSON atomically where feasible (`File.WriteAllTextAsync` with a `CancellationToken`) and replaces the cache value. Preserve `GetOrAdd` race semantics without returning stale null when a value is read during factory execution. Compute `ETag` as uppercase hex of MD5 over the exact JSON string.

## Test phase

Add unit tests under `Services/`:

- Host header `stubidp.kentor.se` selects the legacy certificate; any other host selects default; assert key descriptor contains selected public certificate.
- URLs with and without `/{guid}` and with non-empty `PathBase` retain correct absolute host/scheme/path.
- Store reads default and sample tenant, returns null for unknown GUID, reuses the cached instance, updates cache after save, and returns stable uppercase MD5 ETag for identical JSON.
- Null-parameter validation: public service methods throw `ArgumentNullException` for null arguments (e.g. null `HttpContext`, null JSON string).

Example ETag assertion: `Assert.Equal(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(json))), model.ETag);`.

Run `dotnet test new/StubIdp.slnx`; do not load certificates from process-global current-request state.

## Before/after and test sketches

```csharp
// Before (legacy static/current-request coupling)
var cert = CertificateHelper.SigningCertificate;
var metadataUrl = UrlResolver.MetadataUrl;

// After (new/src/Sustainsys.Saml2.StubIdp/Services)
var cert = certificateService.GetSigningCertificate(httpContext);
var metadataUrl = urlResolver.MetadataUrl;
```

Package pins: no additional package; production/test dependencies retain Task01 pins.

```csharp
// Unit
Assert.Same(legacyCert, service.GetSigningCertificate(contextWithHost("stubidp.kentor.se")));
Assert.Equal("https://localhost/base/{guid}/Metadata", resolver.MetadataUrl.ToString());

// Integration
var response = await client.GetAsync($"/{tenantId:D}/Manage/CurrentConfiguration");
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```
