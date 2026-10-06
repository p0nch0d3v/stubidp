# Task02 — Configuration, options, and data files

## Goal and scope

Move legacy defaults, tenant JSON/schema, and certificate assets behind modern configuration and data-path conventions. Never place a private-key password in tracked JSON. Do not modify `legacy/`.

## Implementation

1. Add `Configuration/StubIdpOptions.cs` with `DefaultAcsUrl`, `DefaultNameId`, `DataPath`, and nested certificate options (`File`, optional `HostName`, optional `Password`). Bind section `StubIdp`; provide a public `IValidateOptions<StubIdpOptions>` validator requiring non-empty defaults/data path and both certificate file paths. Register with `ValidateOnStart()`.
2. Add `StubIdp` to `new/src/Sustainsys.Saml2.StubIdp/appsettings.json`:

```json
"StubIdp": {
  "DefaultAcsUrl": "https://sp.example.com/SAML2/Acs",
  "DefaultNameId": "JohnDoe",
  "DataPath": "App_Data",
  "Certificates": {
    "Default": { "File": "App_Data/stubidp.sustainsys.com.pfx" },
    "LegacyKentor": { "File": "App_Data/Kentor.AuthServices.StubIdp.pfx", "HostName": "stubidp.kentor.se" }
  }
}
```

3. Copy `legacy/Sustainsys.Saml2.StubIdp/App_Data/default.json`, `2273dd5a-8e86-4abc-9078-236215f06c0e.json`, both `.cer` files, both `.pfx` files, and `Content/IdpConfigurationSchema.json` to `new/src/Sustainsys.Saml2.StubIdp/App_Data/` and `new/src/Sustainsys.Saml2.StubIdp/Content/`. Preserve filenames. Mark data as `Content` with `CopyToOutputDirectory=PreserveNewest`; keep the schema available at runtime. Exclude PFX files from source control if repository policy requires that; document local provisioning rather than introducing passwords.
4. Add a user-secrets ID to the production `.csproj`; document `dotnet user-secrets set "StubIdp:Certificates:Default:Password" ...` and the corresponding `LegacyKentor` key. Keep empty passwords usable when the local PFX is unencrypted.
5. Configure JSON static content MIME type as `application/json` if served from `wwwroot`; tenant `App_Data` files should be consumed through the store, not exposed as static files.

Before: `ConfigurationManager.AppSettings["defaultAcsUrl"]` and `defaultNameId`, plus `Server.MapPath("~/App_Data/...")`. After: `IOptions<StubIdpOptions>` for settings, and a rooted `DataPath` resolved from `IHostEnvironment.ContentRootPath` for data.

## Test phase

Add options tests in `Configuration/StubIdpOptionsTests.cs`: bind a valid in-memory `StubIdp` section and assert exact values; assert invalid/empty `DataPath` fails validation. Add an output-copy test (or build assertion) that checks `App_Data/default.json`, the sample tenant JSON, schema, certificates, and schema output exist after build. Do not assert or check in certificate passwords.

Run `dotnet test new/StubIdp.slnx`. Verify `new/src/Sustainsys.Saml2.StubIdp/bin/Debug/net10.0/App_Data/default.json` exists after build and `git status --short` does not show unexpected legacy changes.

## Output checklist

- Options bind and validation run at application startup.
- Data files are copied to build output and read from a configured data directory.
- Passwords are user-secrets/environment configuration only; add setup guidance in final docs.

## Before/after and test sketches

```csharp
// Before (MVC/Web.config)
var acs = ConfigurationManager.AppSettings["defaultAcsUrl"];
var path = Server.MapPath("~/App_Data/default.json");

// After (new/src/Sustainsys.Saml2.StubIdp)
var acs = options.Value.DefaultAcsUrl;
var path = Path.Combine(environment.ContentRootPath, options.Value.DataPath, "default.json");
```

Package pins: no package additions in this task; use `JsonSchema.Net` 9.4.0 from Task01, with the test package versions pinned there.

```csharp
// Unit: configuration binds and validates
Assert.Equal("JohnDoe", options.Value.DefaultNameId);
Assert.False(validator.Validate(Options.DefaultName, new StubIdpOptions { DataPath = "" }).Succeeded);

// Integration: copied runtime fixture is available to the app
Assert.True(File.Exists(Path.Combine(factory.Server.Services.GetRequiredService<IHostEnvironment>()
    .ContentRootPath, "App_Data", "default.json")));
```
