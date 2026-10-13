# Task03 — Core services: certificate, URL resolution, tenant store

> **Status: ✅ Completed.** Gate verified from the repository root after deleting all `bin`/`obj`:
> `dotnet build new/StubIdp.slnx` → 0 errors (only the accepted NU1903/NU1904 advisories), and
> `dotnet test new/StubIdp.slnx` → **77/77 unit + 28/28 integration** tests passing.
> See [As-built notes](#as-built-notes) for the resulting state, decisions taken, and carry-over items.

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

## As-built notes

### Resulting files of record

| Path | Notes |
|---|---|
| `new/src/.../Models/IdpConfigurationModel.cs` | System.Text.Json port; `ETag` = `Convert.ToHexString(MD5.HashData(UTF8(json)))`; legacy leniency preserved (invalid JSON/missing key/complex value → `null`; `HideDetails` defaults to `true`) |
| `new/src/.../Services/CertificateService.cs` | Singleton; loads both PFXs once in the ctor; `GetSigningCertificate(HttpContext)` / `GetSigningKeyDescriptor(HttpContext)`; host match vs `Certificates:LegacyKentor:HostName` with `OrdinalIgnoreCase` (spec sketch) |
| `new/src/.../Services/StubIdpUrlResolver.cs` | Scoped, `IHttpContextAccessor`-based; `RootUrl`, `SsoServiceUrl`, `MetadataUrl`, `ManageUrl`, `ArtifactServiceUrl`, `LogoutServiceUrl`; GUID first segment below `PathBase` reflected raw into every URL |
| `new/src/.../Services/ITenantConfigurationStore.cs` | `DefaultTenantId`, `GetConfigurationAsync(Guid, CancellationToken)` → `IdpConfigurationModel?`, `SaveAsync(Guid, string, CancellationToken)` → `IdpConfigurationModel` |
| `new/src/.../Services/TenantConfigurationStore.cs` | Singleton; `ConcurrentDictionary<Guid, Lazy<Task<IdpConfigurationModel?>>>` cache; default GUID → `default.json`, others → `{guid:D}.json` under `ContentRootPath/DataPath` |
| `new/src/.../Services/StubIdpServiceCollectionExtensions.cs` | `AddStubIdpCoreServices()` → `AddHttpContextAccessor` + the three services with their lifetimes |
| `new/src/.../Program.cs` | `builder.Services.AddStubIdpCoreServices();` after `AddStubIdpOptions(...)` |
| `new/tests/...Tests/Models/IdpConfigurationModelTests.cs` | 18 tests |
| `new/tests/...Tests/Services/CertificateServiceTests.cs` | 13 tests |
| `new/tests/...Tests/Services/StubIdpUrlResolverTests.cs` | 10 tests |
| `new/tests/...Tests/Services/TenantConfigurationStoreTests.cs` | 15 tests |
| `new/tests/...Tests/Services/StubIdpServiceCollectionExtensionsTests.cs` | 3 tests |
| `new/tests/...IntegrationTests/Services/CoreServicesTests.cs` | 7 tests (real resolution + real data files via `WebApplicationFactory`) |

### Decisions taken beyond the literal spec

1. **Certificates are loaded lazily (first resolution of the singleton), not eagerly at
   startup.** The spec allows a "startup/runtime configuration error". Eager resolution in
   `Program.cs` was rejected because it would run before `ValidateOnStart()` host validation
   and would mask the `OptionsValidationException` the Task02 integration test asserts for a
   bad `StubIdp` section. Load failures surface as `InvalidOperationException` naming the
   exact configuration key (`StubIdp:Certificates:{Default|LegacyKentor}:File`/`:Password`),
   with the underlying `CryptographicException`/`IOException` as inner exception. No password
   is ever included in the message.
2. **`X509KeyStorageFlags.MachineKeySet | EphemeralKeySet` is used**, literally applying the
   spec's "MachineKeySet (plus EphemeralKeySet where supported and appropriate)": where
   ephemeral keys are supported (net10, all target platforms) no key material is persisted to
   a key store, which also avoids the machine-store write-permission failure mode of plain
   `MachineKeySet` on non-Windows hosts. Loading uses the non-obsolete
   `X509CertificateLoader.LoadPkcs12FromFile` (.NET 9+); `null`/empty passwords work for the
   bundled test certificates.
3. **`GetConfigurationAsync` caches via `Lazy<Task<IdpConfigurationModel?>>`**, the async
   equivalent of the legacy `ConcurrentDictionary.GetOrAdd` semantics: one load per tenant,
   concurrent callers share it, a cached `null` (missing file) is kept like legacy until a
   save replaces it, and a *failed* load is evicted (`TryRemove` of the exact `Lazy` instance)
   so the next caller retries instead of observing a permanently faulted task. The one-time
   load is deliberately not cancelled by the caller's token (a cancelled first request must
   not poison the cache); the token is honored up-front and while waiting via `WaitAsync`.
4. **`SaveAsync` normalizes the JSON itself** (parse with `JsonNode`, re-serialize with
   `WriteIndented` + `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, which matches Newtonsoft's
   literal handling of non-ASCII characters). This preserves the legacy observable behavior —
   the legacy `ManageController` re-serialized with `Formatting.Indented` before writing —
   independently of what the (Task14) manage page passes in. Writes go to a temp file followed
   by `File.Move(overwrite: true)`, so a concurrent reader never sees a half-written file
   ("atomically where feasible"). Legacy did not collapse duplicate JSON keys on save; the
   System.Text.Json `JsonNode` round-trip keeps the last occurrence — an accepted edge-case
   difference for malformed input that the schema validation in Task14 rejects anyway.
5. **`IdpConfigurationModel.GetJsonValue` mirrors `(string)JToken` conversions** for scalar
   JSON types: strings as-is, `true`/`false` → `"True"`/`"False"` (so `"HideDetails": false`
   parses identically to legacy), numbers → raw text; objects/arrays and invalid JSON →
   `null` (legacy catch-all). `HideDetails` uses `ToLowerInvariant()`, which is
   byte-identical to legacy `ToLower()` for every casing of `true`/`false` in every culture
   (no dotless-i characters involved).

### Test coverage added (66 tests)

Unit — model (18): spec ETag assertion, ETag stability, exact `JsonData`, scalar reads,
8-case `HideDetails` theory (bool/string/missing/null/casing), missing keys, number→text,
complex→null theory, invalid-JSON leniency, null-ctor guard.
Unit — certificate service (13): legacy host + case-insensitive match → Kentor `.cer`
thumbprint; 3 other hosts → default `.cer`; same instance on repeat calls (singleton
ownership); key-descriptor X509 entry per host; null guards on both methods and all three
ctor parameters; missing file / wrong password / unconfigured file → clear
`InvalidOperationException`.
Unit — URL resolver (10): default and named routes for all six URL properties, GUID-only
path, PathBase with and without GUID (spec assertion), non-GUID segment, braced GUID
preserved raw (legacy route-constraint format), scheme/host/port preservation, missing
active request → `InvalidOperationException`, null-accessor guard.
Unit — tenant store (15): default GUID constant, `default.json` vs `{guid:D}.json`
resolution, unknown → null, cached null parity, `Assert.Same` cache reuse, 10-way concurrent
first-lookup race, invalid-JSON file leniency, save writes indented JSON + cache replacement
(new ETag observable immediately, also replacing a cached null), save rejects invalid JSON,
null guard, cancellation honored on read/write, ctor null guards.
Unit — DI extension (3): lifetimes (singleton/scoped), chaining return, null guard.
Integration (7): certificate service resolved from the hosted app selecting the right `.cer`
thumbprint per host; tenant store reading the real `default.json` (exact content + MD5 ETag)
and the sample tenant; unknown tenant → null; `DefaultTenantId`; scoped URL resolver
resolution.

Two test-design notes: `JsonNode.Parse` throws `JsonReaderException` (a `JsonException`
subclass), so save-validation asserts with `ThrowsAnyAsync<JsonException>`; and the
failed-read eviction path has no unit test because a deterministic mid-read I/O failure is
not reproducible with real files running as root (permission bits are bypassed and
`File.Exists` returns `false` for directories, yielding the cached-null path instead).

### Carry-over items (not fixed in Task03)

1. **Task07 needs the per-host `.cer` file name.** `CertificateService` selects certificates
   by host but only exposes the loaded cert/key descriptor; the `.cer` download path (legacy
   `CertificateController`) is the configured `.pfx` path with the extension swapped. Task07
   must decide whether to derive it that way or extend the options/service surface.
2. The `GetOrAdd`-cached `null` for a missing tenant file means a tenant JSON dropped onto
   disk after the first lookup is not picked up until a save (legacy parity, now explicitly
   documented on `ITenantConfigurationStore.GetConfigurationAsync`). Task14's save flow relies
   on this replacement behavior; no code change is planned.
