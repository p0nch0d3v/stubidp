# Task02 — Configuration, options, and data files

> **Status: ✅ Completed.** Gate verified from the repository root after deleting all `bin`/`obj`:
> `dotnet build new/StubIdp.slnx` → 0 errors (only the accepted NU1903/NU1904 advisories), and
> `dotnet test new/StubIdp.slnx` → **18/18 unit + 21/21 integration** tests passing.
> See [As-built notes](#as-built-notes) for the resulting state, the two decisions taken beyond
> the literal spec, and the carry-over items.

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

- [x] Options bind and validation run at application startup.
- [x] Data files are copied to build output and read from a configured data directory.
- [x] Passwords are user-secrets/environment configuration only; add setup guidance in final docs.

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

## As-built notes

### Resulting files of record

| Path | Notes |
|---|---|
| `new/src/.../Configuration/StubIdpOptions.cs` | `DefaultAcsUrl`, `DefaultNameId`, `DataPath`, `Certificates`, plus `public const string SectionName = "StubIdp"` |
| `new/src/.../Configuration/CertificatesOptions.cs` | `Default`, `LegacyKentor` |
| `new/src/.../Configuration/CertificateOptions.cs` | `File`, `HostName?`, `Password?` |
| `new/src/.../Configuration/StubIdpOptionsValidator.cs` | `IValidateOptions<StubIdpOptions>`; collects **all** failures in one result, messages keyed by full config path (e.g. `StubIdp:Certificates:Default:File`) |
| `new/src/.../Configuration/StubIdpOptionsServiceCollectionExtensions.cs` | `AddStubIdpOptions(IConfiguration)` → `AddOptions<StubIdpOptions>().Bind(...).ValidateOnStart()` + validator registration |
| `new/src/.../appsettings.json` | `StubIdp` section exactly as §2.4 of `PLAN.md`; **no** `Password` keys |
| `new/src/.../Program.cs` | `builder.Services.AddStubIdpOptions(builder.Configuration);` before `AddRazorComponents()` |
| `new/src/.../Sustainsys.Saml2.StubIdp.csproj` | `<UserSecretsId>54e89770-5ba7-4528-97b9-1f0d9f5c72ea</UserSecretsId>`; content items for the data files |
| `new/src/.../App_Data/` | `default.json`, `2273dd5a-8e86-4abc-9078-236215f06c0e.json`, both `.pfx`, both `.cer` — all byte-identical to `legacy/` (MD5-verified) |
| `new/src/.../wwwroot/Content/IdpConfigurationSchema.json` | byte-identical to `legacy/Sustainsys.Saml2.StubIdp/Content/IdpConfigurationSchema.json` |
| `new/tests/...Tests/Configuration/StubIdpOptionsTests.cs` | 17 tests |
| `new/tests/...IntegrationTests/Configuration/ConfigurationAndDataFilesTests.cs` | 20 tests |

### Decisions taken beyond the literal spec

1. **The schema lives in `wwwroot/Content/`, not `Content/`.** Step 3 said
   `new/src/Sustainsys.Saml2.StubIdp/Content/`, but the legacy app serves the schema at the public
   URL `/Content/IdpConfigurationSchema.json` (linked from `Views/Manage/Index.cshtml:49`) *and*
   reads it from disk (`ManageController.cs:57`). Placing it under `wwwroot/Content/` satisfies
   both without an extra static-file mapping or endpoint, and honours the "preserve all public
   routes" rule. Step 5 needed no code: the default
   `FileExtensionContentTypeProvider` already maps `.json` → `application/json`, which is asserted
   by an integration test.
2. **The `.pfx` files are tracked in git**, matching `legacy/App_Data/` where they are already
   committed. They are public test certificates with an empty password, so a clean clone stays
   runnable and the clean-checkout gate in Task16 keeps working. Step 3's "exclude PFX from source
   control if repository policy requires that" therefore does not apply; the user-secrets path
   remains the documented mechanism for real certificates.

### Other implementation notes

- **Explicit content items were required for the certificates.** The Web SDK's default content glob
  (`Sdk.StaticWebAssets.StaticAssets.ProjectSystem.props`) only covers `**/*.config` and
  `**/*.json`, so `App_Data/*.json` is copied automatically but `*.pfx`/`*.cer` are not. The csproj
  adds `<Content Include="App_Data/*.pfx;App_Data/*.cer" CopyToOutputDirectory="PreserveNewest" ...>`
  plus a `<Content Update="App_Data/*.json" ...>` that restates the copy semantics so the data
  contract is visible in the project file. These items also flow transitively into the test
  projects' output, which is what makes the build-output assertions possible.
- **No `DataAnnotations` on the options class.** Step 1 asks for an `IValidateOptions`
  implementation, so validation lives there; the generic `.agents` skill suggestion to use
  validation attributes was not applied for this type.
- **Validator is non-throwing except for `null` options**, where it uses
  `ArgumentNullException.ThrowIfNull` per `PLAN.md` §4, with a matching test.
- **No new packages**, no logging added (nothing in this task performs I/O yet — the data path is
  only consumed from Task03 onward), and `legacy/` is untouched (`git status --short legacy/` → 0
  lines).

### Test coverage added (37 tests)

Unit — `Configuration/StubIdpOptionsTests.cs`:
full section binding including nested certificates; `HostName` null for `Default`; passwords unset
by default but bindable from an external source; `SectionName` constant; validator accepts complete
options; per-field rejection theory (`DefaultAcsUrl`/`DefaultNameId`/`DataPath`, including
whitespace-only); the spec's `new StubIdpOptions { DataPath = "" }` case; both certificate `File`
paths required; missing `Certificates` section; passwords never required; `ArgumentNullException`
guards on the validator and on both `AddStubIdpOptions` parameters; `OptionsValidationException` on
incomplete and on entirely missing section; validator is registered by the extension.

Integration — `Configuration/ConfigurationAndDataFilesTests.cs`:
options bind from the application's real `appsettings.json`; all six data files present at
`ContentRootPath/{DataPath}` (theory) and in the build output (theory); schema present at
`WebRootPath/Content`; schema served at `/Content/IdpConfigurationSchema.json` → 200 +
`application/json`; `/App_Data/default.json` and `/App_Data/*.pfx` are **not** served as static
content; no `Password` string in `appsettings.json` / `appsettings.Development.json`; invalid
`StubIdp` section makes the host fail to start with `OptionsValidationException`.

### Carry-over items (not fixed in Task02)

1. **`wwwroot` is not copied to `bin` in .NET 10.** Static web assets are served through the
   generated `*.staticwebassets.*.json` manifest, which points at the project folder at build time.
   The schema is therefore reachable over HTTP and via `IWebHostEnvironment.WebRootPath` (content
   root is the project directory under both `dotnet run` and `WebApplicationFactory`), and it is
   included on `dotnet publish`. **Task14 must read it via `WebRootPath`**, not via
   `AppContext.BaseDirectory`.
2. **Task16 docs:** document the exact secret-provisioning commands —
   `dotnet user-secrets --project new/src/Sustainsys.Saml2.StubIdp set "StubIdp:Certificates:Default:Password" "<pwd>"`
   and the `StubIdp:Certificates:LegacyKentor:Password` equivalent — and record that the bundled
   test certificates need no password. The user-secrets ID is already in the csproj.
3. **`new/src/Sustainsys.Saml2.StubIdp/appsettings.Development.json` is untracked** — it was left
   out of the `Task01` commit and is not covered by `.gitignore`. Task16 should either commit it or
   ignore it deliberately. One integration test asserts it contains no password and currently
   depends on the file existing on disk.
4. **`StubIdpOptions.DataPath` is still a raw relative string.** Rooting it against
   `IHostEnvironment.ContentRootPath` (the "After" sketch above) happens inside the services added
   by Task03; no helper was added here to avoid shipping untested API surface.
