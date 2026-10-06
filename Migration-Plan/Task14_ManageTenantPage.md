# Task14 — Manage tenant page

## Goal and scope

Implement the tenant JSON editor as an Interactive Server Blazor page; preserve tenant setup/edit behavior, validation, and store cache updates.

## Implementation

Create `Components/Pages/ManageTenant.razor` using `@rendermode InteractiveServer`. Support `/{idpId:guid}/Manage`; support `/Manage?idpId={guid}` for the legacy default-route/query form. If no valid tenant GUID is supplied, show an actionable validation state rather than attempting an unsafe default write. Reject edits to special default GUID `e73d98ff-0f1c-4cc2-8808-6d1bf028a8a9`.

On load, read existing `{guid}.json`; if absent, read `default.json`, parse JSON and apply exact legacy placeholders: `DefaultAssertionConsumerServiceUrl = "http://www.example.com/Saml2/Acs (optional, you may remove this line)"`, `DefaultAudience = "http://www.example.com/Saml2 (optional, but usually a good idea to set to Entity ID of SP)"`, and `IdpDescription = "This is my custom IDP description"`. Display/edit indented JSON in an `EditForm` textarea.

Implement `Services/TenantConfigurationValidator.cs` using `System.Text.Json` parse validation and JsonSchema.Net 9.4.0 against `Content/IdpConfigurationSchema.json`. Distinguish invalid syntax (`Invalid Json`) from schema failure (`Json does not match schema. ...`). On valid data, normalize to indented JSON, save via `ITenantConfigurationStore` (async `SaveAsync` with a `CancellationToken`), refresh cache, and show confirmation. Load template/tenant files with `File.ReadAllTextAsync`. Log save success/failure and validation outcomes with `ILogger<T>` scoped to the tenant GUID — never log the JSON payload itself. Render entity ID/manage URLs, GDPR explanation, schema link, and `MetadataLinks`.

Before: MVC GET creates a template or reads file; POST parses with Newtonsoft, validates JSchema, writes and updates static cache. After: interactive component calls a validator/store service and preserves corresponding messages and save behavior.

## Test phase

Unit tests for validator accept default/sample tenant JSON and reject malformed JSON and schema-invalid JSON. bUnit tests: missing tenant shows placeholder template; malformed JSON and schema errors appear; default GUID save rejected; valid save calls store and shows confirmation. Integration test GET existing tenant page returns 200 and JSON is present; save a new tenant through the component's form/test seam, then GET `/{guid}/Manage/CurrentConfiguration` and assert JSON plus updated ETag.

Run `dotnet test new/StubIdp.slnx`; use temporary test data path and clean it after each test.

## Before/after and test sketches

```csharp
// Before (MVC POST owns parsing, schema validation, file write, and cache update)
var json = JObject.Parse(model.JsonData);
System.IO.File.WriteAllText(fileName, json.ToString(Formatting.Indented));

// After (new/src/Sustainsys.Saml2.StubIdp/Services + Interactive Server component)
var result = validator.Validate(model.JsonData);
if (result.IsValid) await tenantStore.SaveAsync(idpId, result.NormalizedJson, cancellationToken);
```

Package pins: `JsonSchema.Net` 9.4.0 and `bUnit` 2.11.3; integration uses `Microsoft.AspNetCore.Mvc.Testing` 10.0.12. Other test package versions remain pinned in Task01.

```csharp
// Unit
Assert.True(validator.Validate(File.ReadAllText(sampleTenantPath)).IsValid);

// Integration
using var response = await client.GetAsync($"/{tenantId:D}/Manage/CurrentConfiguration");
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```
