# Migration Plan: Sustainsys.Saml2.StubIdp — ASP.NET MVC 5 (.NET Framework 4.7.2) → Blazor Web App (.NET 10)

This plan describes the complete rewrite of `legacy/Sustainsys.Saml2.StubIdp/` as a .NET 10 (`net10.0`)
Blazor Web App in `new/`. It was produced by analyzing the legacy source tree. Every task is
sequential and self-contained, and **each task embeds its own test phase** (there are no separate
test tasks).

---

## 1. Current-State Analysis (Summary of Findings)

### 1.1 Application profile
- ASP.NET MVC 5 (`Microsoft.AspNet.Mvc` 5.2.4), Razor 3, .NET Framework 4.7.2, `System.Web`.
- Project references: `legacy/Sustainsys.Saml2/` (core) and `legacy/Sustainsys.Saml2.Mvc/`
  (System.Web bridge providing `CommandResult.ToActionResult()`).
- Entry point `Global.asax.cs` → `RouteConfig.RegisterRoutes` + `BundleConfig.RegisterBundles`.

### 1.2 Controllers and their nature (drives the render-mode split)
| Controller / Action | Verb | Legacy behavior | Classification |
|---|---|---|---|
| `HomeController.Index` | GET | Renders assertion form; **also unbinds an incoming AuthnRequest** (Redirect binding via query string) and pre-fills the form | UI + SAML receive |
| `HomeController.Index` | POST | Valid model → `Saml2Response` bound to Redirect/Post/Artifact binding (`CommandResult`); invalid model → re-render view; **also receives POST-binding AuthnRequests** (model binding fails → re-render with pre-filled data) | SAML emit + SAML receive + UI re-render |
| `LogoutController.Index` | GET/POST | Unbinds incoming `LogoutRequest` → RespondToLogout view; `LogoutResponse` → ReceivedLogoutResponse view; no message → InitiateLogout view (query-string pre-fill via `TryUpdateModel`) | SAML receive + UI |
| `LogoutController.InitiateLogout` | POST | Builds `Saml2LogoutRequest`, Redirect binding → 302 | SAML emit |
| `LogoutController.RespondToLogoutRequest` | POST | Builds `Saml2LogoutResponse`, Redirect binding → 302 | SAML emit |
| `DiscoveryServiceController.Index` | GET/POST | `isPassive=true` or POST → 302 to `return` URL with `returnIDParam=SelectedIdp`; otherwise form view | SAML protocol redirect + UI |
| `MetadataController.Index` / `.BrowserFriendly` | GET | Signed IdP metadata XML; `application/samlmetadata+xml` / `text/xml` | Data endpoint |
| `FederationController.Index` / `.BrowserFriendly` | GET | Signed federation metadata XML; same content types | Data endpoint |
| `CertificateController.Index` | GET | `.cer` file download, `Content-Disposition: attachment`, host-based cert selection (`stubidp.kentor.se` → legacy Kentor cert) | File endpoint |
| `ManageController.Index` | GET | JSON tenant editor view (loads file or template derived from `default.json`) | UI-only |
| `ManageController.Index` | POST | JSON parse + JSON-Schema validation → write `App_Data/{idpId}.json`, update cache, redirect | UI-only (form handling) |
| `ManageController.CurrentConfiguration` | GET | `[Compress]`; tenant JSON with ETag / 304 (`If-None-Match`); 500 when no config | Data endpoint (consumed by `ViewIndex.js`) |
| `ArtifactResolveController.Index` | POST | SOAP `ArtifactResolve` → `ArtifactResponse`; uses `Saml2ArtifactBinding.PendingMessages`; signs message; returns envelope via `Content(...)` (default `text/html; charset=utf-8` — **preserved as-is**, see Task09) | SOAP protocol endpoint |

### 1.3 Routing (must be preserved — `legacy/.../App_Start/RouteConfig.cs`)
- `NamedIdp`: `{idpId}/{controller}/{action}/{id}`, `idpId` constrained to a GUID
  (`^(\{{0,1}([0-9a-fA-F]){8}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){12}\}{0,1})$`),
  defaults `Home/Index`.
- `Default`: `{controller}/{action}/{id}`, defaults `Home/Index`.
- Consequence: **every** public endpoint exists in two shapes, e.g. `/Metadata` and `/{guid}/Metadata`.
  `UrlResolver` reflects the GUID segment into generated URLs (entity IDs, metadata locations).

### 1.4 Supporting code to port
- `CertificateHelper.cs` — two static signing certificates from `App_Data/*.pfx`, host-based
  selection, `KeyDescriptor` creation.
- `UrlResolver.cs` — absolute `Metadata/Manage/ArtifactResolve/Logout/Root` URLs, GUID-segment aware,
  based on current request + application path.
- `BaseController.cs` — `App_Data/{guid}.json` resolution (special default GUID
  `e73d98ff-0f1c-4cc2-8808-6d1bf028a8a9` → `default.json`), `ConcurrentDictionary<Guid, IdpConfigurationModel>`
  cache, ETag/`304` helper (`TestETag`).
- `CompressAttribute.cs` — gzip/deflate response filter → **replaced by ASP.NET Core Response Compression middleware**.
- Models: `AssertionModel`, `AttributeStatementModel`, `HomePageModel`, `IdpConfigurationModel`,
  `LogoutModels` (3 classes), `DiscoveryServiceModel`, `ManageIdpModel`, `MetadataModel`.
- `Views/` (7 cshtml) + `Scripts/ViewIndex.js` (select2 user picker, ICanHaz/mustache attribute
  rows, js.cookie remember-me, show/hide details) + `Content/Site.less` + `normalize.css`.

### 1.5 Web.config settings to migrate (`legacy/.../Web.config`) — ✅ done in Task02
- `appSettings:defaultAcsUrl` = `https://sp.example.com/SAML2/Acs` → `appsettings.json`
- `appSettings:defaultNameId` = `JohnDoe` → `appsettings.json`
- dotless / bundling / IIS handler config → removed (LibMan + static CSS instead).
- Per-tenant JSON files under `App_Data/*.json` stay **data files**, moved behind a storage service.

---

## 2. Target Architecture

### 2.1 Solution layout (created by Task01 — ✅ in place)
```
new/
  StubIdp.slnx
  src/
    Sustainsys.Saml2.StubIdp/            # Blazor Web App (net10.0)
  tests/
    Sustainsys.Saml2.StubIdp.Tests/              # xUnit + Moq + bUnit
    Sustainsys.Saml2.StubIdp.IntegrationTests/   # xUnit + Microsoft.AspNetCore.Mvc.Testing
```

### 2.2 Core dependency decisions
| Legacy | New |
|---|---|
| `ProjectReference` → `Sustainsys.Saml2` | `PackageReference` **Sustainsys.Saml2 2.11.0** (has a `net8.0` target → compatible with `net10.0`; all APIs used by StubIdp verified public: `Saml2Binding`, `CommandResult`, `HttpRequestData`, `Saml2Response/…`, `Saml2ArtifactBinding.PendingMessages`, `XmlHelpers.PrettyPrint/Sign/XmlDocumentFromString`, `DateTimeExtensions.ToSaml2DateTimeString`, `Metadata*.ToXmlString`) |
| `ProjectReference` → `Sustainsys.Saml2.Mvc` (`ToActionResult()`) | **Removed.** Re-implemented in-project as `CommandResult` → `IResult` bridge, based on `legacy/Sustainsys.Saml2.AspNetCore2/CommandResultExtensions.cs` (Task04) |
| `Sustainsys.Saml2.HttpModule` `Request.ToHttpRequestData` (System.Web) | In-project `HttpContext.ToHttpRequestData(...)`, based on `legacy/Sustainsys.Saml2.AspNetCore2/HttpRequestExtensions.cs` (Task04) |
| Newtonsoft.Json 11 + Newtonsoft.Json.Schema 3.0.10 | `System.Text.Json` (in-box) + **JsonSchema.Net 9.4.0** (MIT) for tenant-JSON schema validation |
| Microsoft.IdentityModel.* 5.2.4, System.Security.Cryptography.Xml 4.4.2 | Transitive via Sustainsys.Saml2 2.11.0 — no direct references |
| Microsoft.AspNet.Mvc/Razor/WebPages, Web.Optimization, WebGrease, Antlr, dotless, Microsoft.Web.Infrastructure | **Removed** (Blazor / static assets) |
| jQuery 1.11.3, jQuery.Validate, unobtrusive validation, Select2 4.0.5, ICanHaz 0.9, js.cookie, normalize.css | Replaced by Blazor components (EditForm validation, attribute-row list, user picker) + small vanilla `site.js`; **normalize.css via LibMan** (`Microsoft.Web.LibraryManager.Cli 3.0.114`) |

### 2.3 Render-mode / endpoint split (per scope constraints)
| Surface | Implementation | Render mode |
|---|---|---|
| Home `/`, `/{idpId}` (GET) | Razor page-component `Home.razor` hosting interactive `AssertionForm.razor` island | Static SSR page + `@rendermode InteractiveServer` island |
| Home POST `/`, `/{idpId}` (SAML emit + POST-binding AuthnRequest receive) | Minimal API endpoints with full `HttpContext`; re-render via `RazorComponentResult<Home>` | Minimal API |
| Logout `/Logout` GET/POST message receive | Static SSR `Logout.razor` (cascading `HttpContext`, branches to 3 sub-components) | Static SSR |
| Logout emits (`/Logout/InitiateLogout`, `/Logout/RespondToLogoutRequest` POST) | Minimal API → `CommandResult` `IResult` (302) | Minimal API |
| DiscoveryService `/DiscoveryService` | Static SSR `DiscoveryService.razor`; redirect via `NavigationManager` (static SSR redirect) | Static SSR |
| Manage tenant `/Manage`, `/{idpId}/Manage` | `ManageTenant.razor` with `EditForm` | **Interactive Server** |
| Metadata, Federation, Certificate, `Manage/CurrentConfiguration`, ArtifactResolve | Minimal API endpoints preserving content types, ETag/304, compression | Minimal API |

**Explicit split rule** (legacy actions that return View *or* redirect/data depending on input):
each such action becomes (a) a **protocol endpoint** (Minimal API, no Blazor) that only emits
redirects/XML/JSON/files, and/or (b) a **page component** that only renders UI. Where one URL must
do both (Home `/`, Logout `/Logout`, DiscoveryService), the request shape decides: SAML/message
input → protocol path; otherwise → UI path. The exact branching is specified per task
(Tasks 11–13).

### 2.4 Configuration model (Task02 — ✅ in place)
`appsettings.json`:
```json
"StubIdp": {
  "DefaultAcsUrl": "https://sp.example.com/SAML2/Acs",
  "DefaultNameId": "JohnDoe",
  "DataPath": "App_Data",
  "Certificates": {
    "Default":  { "File": "App_Data/stubidp.sustainsys.com.pfx" },
    "LegacyKentor": { "File": "App_Data/Kentor.AuthServices.StubIdp.pfx", "HostName": "stubidp.kentor.se" }
  }
}
```
Bound via Options Pattern (`StubIdpOptions`, validated with `IValidateOptions`).
**Never** store private-key passwords in `appsettings.json` — `dotnet user-secrets` for
`StubIdp:Certificates:*:Password`; files stay out of source control guidance documented in Task02.

As built: `Configuration/{StubIdpOptions,CertificatesOptions,CertificateOptions,
StubIdpOptionsValidator,StubIdpOptionsServiceCollectionExtensions}.cs`, registered from
`Program.cs` via `AddStubIdpOptions(builder.Configuration)` with `ValidateOnStart()`. Data files
live in `new/src/.../App_Data/` (tenant JSON + `.pfx`/`.cer`, tracked in git like `legacy/`, empty
passwords) and the tenant schema in `new/src/.../wwwroot/Content/IdpConfigurationSchema.json` so
the legacy public URL `/Content/IdpConfigurationSchema.json` is preserved.

### 2.5 Pinned package versions (verified against nuget.org)
| Package | Version | Project |
|---|---|---|
| Sustainsys.Saml2 | 2.11.0 | src |
| JsonSchema.Net | 9.4.0 | src |
| xunit | 2.9.3 | tests |
| xunit.runner.visualstudio | 4.0.0 | tests |
| Microsoft.NET.Test.Sdk | 18.10.1 | tests |
| Moq | 4.21.0 | unit tests |
| bUnit | 2.11.3 | unit tests |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 | integration tests |
| coverlet.collector | 10.1.0 | tests |
| Microsoft.Web.LibraryManager.Cli | 3.0.114 | dotnet tool (build-time only) |

### 2.6 Test strategy (applies to every task)
- **Unit tests** (`new/tests/Sustainsys.Saml2.StubIdp.Tests`): services, models, bridges (xUnit+Moq);
  components with bUnit (`TestContext`, `RenderComponent<T>`, event dispatch, `EditForm` validation).
- **Integration tests** (`new/tests/Sustainsys.Saml2.StubIdp.IntegrationTests`):
  `WebApplicationFactory<Program>` + `HttpClient` asserting status codes, content types, ETag/304,
  redirect `Location` semantics, and the full route matrix (default + named-GUID variants).
  `Program` is made test-visible via `public partial class Program { }`.
- A task is done only when its test phase passes (`dotnet test new/StubIdp.slnx`).

---

## 3. Task List (sequential)

**Progress:** Task01 ✅ · Task02 ✅ completed · Task03 … Task16 ⏳ pending. A task is done only when
its own test phase passes (`dotnet test new/StubIdp.slnx`).

### Task01 — Scaffold solution, projects, and test skeleton ✅
- **Goal:** Create `new/StubIdp.slnx`, the Blazor Web App (`net10.0`, `--interactivity Server`,
  per-component interactivity), both test projects, add all pinned package references
  (Sustainsys.Saml2 2.11.0, JsonSchema.Net 9.4.0, test stack), delete template sample pages
  (Counter/Weather), add `public partial class Program`.
- **Legacy sources:** none (greenfield), `legacy/.../packages.config` as checklist.
- **Key outputs:** `new/StubIdp.slnx`, `new/src/Sustainsys.Saml2.StubIdp/`, `new/tests/...`.
- **Test phase:** solution builds; smoke unit test and a `WebApplicationFactory` smoke test
  (GET `/` → 200) pass.
- **Status:** ✅ done — build 0 errors, 1/1 unit + 1/1 integration test green.
  `<GenerateDocumentationFile>` was enabled already here; both test projects have a
  `ProjectReference` to the production project; template bootstrap assets retained for Task10.
  Two carry-over items recorded in the task file: the template's
  `BlazorDisableThrowNavigationException=true` (decide in Task13) and the transitive
  NU1903/NU1904 advisories from `Sustainsys.Saml2` 2.11.0 (document in Task16).
- 📄 `Task01_ScaffoldSolution.md`

### Task02 — Configuration, options, and data files ✅
- **Goal:** `appsettings.json` `StubIdp` section; `StubIdpOptions` + validation; user-secrets
  setup for certificate passwords; copy `legacy/.../App_Data/default.json`,
  `App_Data/2273dd5a-… .json` (sample tenant), `App_Data/*.pfx|*.cer`,
  `Content/IdpConfigurationSchema.json` into the new project with correct copy semantics;
  configure Kestrel/static-file `.json` MIME parity.
- **Legacy sources:** `Web.config` (appSettings), `App_Data/*`, `Content/IdpConfigurationSchema.json`.
- **Test phase:** options bind/validate from JSON; missing-data-path fails validation; data files
  copied on build; secrets documented (no password in repo).
- **Status:** ✅ done — clean rebuild 0 errors, 18/18 unit + 21/21 integration tests green.
  Five types under `Configuration/`, registered via `AddStubIdpOptions(...)` +
  `ValidateOnStart()`; `<UserSecretsId>` added; all seven data files copied byte-identically
  (MD5-verified) and asserted present both at `ContentRootPath/{DataPath}` and in the build
  output. Two decisions beyond the spec: the schema went to **`wwwroot/Content/`** (preserves the
  legacy public URL `/Content/IdpConfigurationSchema.json` and stays disk-readable — no code was
  needed for `.json` MIME parity) and the `.pfx` files are **tracked in git** as in `legacy/`
  (public test certs, empty passwords, keeps the Task16 clean-checkout gate working). Certificates
  needed explicit `<Content Include>` items because the SDK glob only covers `*.config`/`*.json`.
  Four carry-overs recorded in the task file, notably: `wwwroot` is not copied to `bin` in .NET 10,
  so **Task14 must read the schema via `IWebHostEnvironment.WebRootPath`**.
- 📄 `Task02_ConfigurationAndDataFiles.md`

### Task03 — Core services: certificate, URL resolution, tenant store
- **Goal:** `CertificateService` (host-aware cert + `KeyDescriptor`, port of `CertificateHelper.cs`);
  `StubIdpUrlResolver` (port of `UrlResolver.cs`, `IHttpContextAccessor`-based, GUID-segment aware);
  `ITenantConfigurationStore` (port of `BaseController` file/caching logic + `IdpConfigurationModel`
  on `System.Text.Json`, MD5 ETag hex-uppercase, `GetOrAdd` semantics, save+cache-update).
- **Legacy sources:** `CertificateHelper.cs`, `UrlResolver.cs`, `Controllers/BaseController.cs`,
  `Models/IdpConfigurationModel.cs`.
- **Carry-over from Task02:** `StubIdpOptions.DataPath` is a raw relative string; rooting it
  against `IHostEnvironment.ContentRootPath` belongs to the services added here. Certificate
  passwords come from `options.Certificates.*.Password` (`null`/empty = unencrypted, which is what
  the bundled test certificates use).
- **Test phase:** unit tests — cert selection per host (incl. `stubidp.kentor.se`); URL resolution
  with/without GUID and app path-base; store cache hit/miss, ETag stability, unknown-GUID → null.
- 📄 `Task03_CoreServices.md`

### Task04 — SAML bridges: `HttpRequestData` + `CommandResult` → `IResult`
- **Goal:** `HttpContext.ToHttpRequestData(...)` (from AspNetCore2 reference, adapted: no cookie
  decryption needed) and `CommandResultExtensions.ToResult()` returning a custom
  `CommandResultIResult : IResult` (status code, `Location`, headers, content + content type;
  cookie/principal members documented as unused by StubIdp bindings but handled defensively).
- **Legacy sources:** `legacy/Sustainsys.Saml2.AspNetCore2/CommandResultExtensions.cs`,
  `legacy/Sustainsys.Saml2.AspNetCore2/HttpRequestExtensions.cs`,
  `legacy/Sustainsys.Saml2.Mvc/CommandResultExtensions.cs` (behavioral reference).
- **Test phase:** unit tests for Redirect (302 + Location), auto-post (`text/html` form containing
  `SAMLResponse`), header copy, status passthrough; `HttpRequestData` mapping (method, URL, form,
  path base); TestServer round-trip executing the `IResult`.
- 📄 `Task04_SamlProtocolBridges.md`

### Task05 — Port models (assertion, logout, discovery, manage, metadata)
- **Goal:** Port all 10 model classes (`AssertionModel`, `AttributeStatementModel`,
  `HomePageModel`, `IdpConfigurationModel`, the three logout models, `DiscoveryServiceModel`,
  `ManageIdpModel`, `MetadataModel`) to `new/src/.../Models/`, replacing
  `ConfigurationManager.AppSettings` with `IOptions<StubIdpOptions>` and static
  `CertificateHelper`/`UrlResolver` with injected services; keep DataAnnotations attributes for
  `EditForm` validation; `MetadataModel` → `MetadataService` building signed
  `EntityDescriptor`/`EntitiesDescriptor`.
- **Legacy sources:** `Models/*.cs`.
- **Test phase:** unit tests — `ToSaml2Response` (claims incl. `SessionIndex`, NameId format,
  audience, InResponseTo, issuer = metadata URL), `ToLogoutRequest/Response` (destination, signing
  cert SHA-256 alg), metadata XML contains SSO/SLO/Artifact endpoints + key; defaults from options.
- 📄 `Task05_ModelsPort.md`

### Task06 — Metadata & Federation endpoints
- **Goal:** Minimal API: `GET /Metadata`, `/Metadata/BrowserFriendly`, `/Federation`,
  `/Federation/BrowserFriendly` **plus named-GUID variants**; signed with
  `SignedXml.XmlDsigRSASHA256Url`; exact content types `application/samlmetadata+xml` / `text/xml`;
  entity ID reflects GUID segment on named routes.
- **Legacy sources:** `Controllers/MetadataController.cs`, `Controllers/FederationController.cs`.
- **Test phase:** integration — 200, content types, well-formed signed XML, `entityID` contains
  GUID on named route, `CacheDuration`/`validUntil` present (IdP) vs federation name present.
- 📄 `Task06_MetadataFederationEndpoints.md`

### Task07 — Certificate download endpoint
- **Goal:** Minimal API `GET /Certificate` (+ named variant): `.cer` bytes, `text/plain`,
  `Content-Disposition: attachment; filename=...`, host-based file selection.
- **Legacy sources:** `Controllers/CertificateController.cs`.
- **Test phase:** integration — 200, disposition filename, bytes equal configured `.cer`;
  host-header switch selects legacy cert.
- 📄 `Task07_CertificateEndpoint.md`

### Task08 — `Manage/CurrentConfiguration` JSON endpoint (ETag/304 + compression)
- **Goal:** Response Compression middleware (gzip/deflate, replaces `CompressAttribute`);
  Minimal API `GET /Manage/CurrentConfiguration` (+ named): tenant JSON `application/json`,
  `ETag` header, `If-None-Match` → 304, `Cache-Control: private`-equivalent, 500 when no config
  (legacy parity).
- **Legacy sources:** `Controllers/ManageController.cs` (`CurrentConfiguration`),
  `CompressAttribute.cs`, `BaseController.TestETag`.
- **Test phase:** integration — 200 + ETag + body; matching `If-None-Match` → 304 + empty body;
  `Accept-Encoding: gzip` → compressed; unknown tenant → 500.
- 📄 `Task08_CurrentConfigurationEndpoint.md`

### Task09 — ArtifactResolve SOAP endpoint
- **Goal:** Minimal API `POST /ArtifactResolve` (+ named): parse SOAP envelope from request body,
  resolve artifact via `Saml2ArtifactBinding.PendingMessages` (`TryRemove`), sign when a signing
  cert is present, emit `ArtifactResponse` envelope (new `Saml2Id`, `InResponseTo`,
  `ToSaml2DateTimeString`); unknown artifact → 500 (legacy threw); **preserve legacy
  `Content(...)` default content type `text/html; charset=utf-8`** (documented quirk).
- **Legacy sources:** `Controllers/ArtifactResolveController.cs`.
- **Test phase:** unit — envelope formatting/signing branch; integration — seed `PendingMessages`,
  POST SOAP → 200 + valid `ArtifactResponse` containing the message XML; unknown artifact → 500.
- 📄 `Task09_ArtifactResolveEndpoint.md`

### Task10 — Layout, shared components, and static assets
- **Goal:** Port `_Layout.cshtml` → `MainLayout.razor`/`App.razor` (navbar, dropdowns incl.
  tenant-aware links, page heading, GA snippet decision — keep, configurable); `_About.cshtml` →
  `AboutCard.razor`, `_MetadataLink.cshtml` → `MetadataLinks.razor`; `Site.less` →
  `wwwroot/css/site.css` (plain CSS, dotless removed); LibMan restore of `normalize.css` (+ decision
  to drop select2/jQuery — replaced in Task11); `Sustainsys.png`; `wwwroot/js/site.js` vanilla
  helpers (show/hide details, remember-user cookie via `document.cookie`).
- **Legacy sources:** `Views/Shared/_Layout.cshtml`, `_About.cshtml`, `_MetadataLink.cshtml`,
  `Content/Site.less`, `Content/normalize.css`, `Content/Sustainsys.png`, `Scripts/ViewIndex.js`.
- **Test phase:** bUnit — navbar links (tenant menu only with `idpId`), about card renders metadata
  links with resolved URLs; integration — `/css/site.css`, `/lib/normalize/normalize.css`,
  `/Sustainsys.png` → 200 with correct content types.
- 📄 `Task10_LayoutAndStaticAssets.md`

### Task11 — Home page + SSO protocol endpoint (the core flow)
- **Goal:**
  - `Home.razor` static SSR page at `/` and `/{idpId:guid}`: applies tenant config
    (`ReadCustomIdpConfig`), unbinds Redirect-binding AuthnRequest from query (`SAMLRequest`),
    renders `AssertionForm.razor` island (`InteractiveServer`) + details + `AboutCard`.
  - `AssertionForm.razor`: `EditForm` + `DataAnnotationsValidator`; dynamic attribute-statement
    rows (replaces ICanHaz/mustache); tenant user picker fed by `ITenantConfigurationStore`
    (replaces select2 + `CurrentConfiguration` fetch + mustache template); show/hide details
    (replaces jQuery); remember-user cookie via JS interop.
  - Submit handling (explicit split): `ResponseBinding=HttpRedirect` → build `CommandResult`,
    `NavigationManager.NavigateTo(Location, forceLoad: true)`; `HttpPost`/`Artifact` → render the
    `CommandResult.Content` auto-post HTML via JS interop `document.write`.
  - Minimal API `POST /` and `/{idpId:guid}` (non-circuit clients + SAML POST-binding receive):
    form contains `SAMLRequest` → unbind → `RazorComponentResult<Home>` pre-filled (legacy
    POST-receive parity); otherwise bind form fields → validate → `CommandResultIResult`;
    invalid → `RazorComponentResult<Home>` with errors. **No antiforgery on these endpoints**
    (external SPs POST here) — read `Request.Form` manually, document the decision.
  - kentor.se migration banner + http→https info banner conditions preserved.
- **Legacy sources:** `Controllers/HomeController.cs`, `Views/Home/Index.cshtml`,
  `Scripts/ViewIndex.js`, `Models/HomepageModel.cs`, `Models/AssertionModel.cs`.
- **Test phase:** bUnit — form renders pre-filled, add/remove attribute rows, user-picker fills
  NameId + attributes, validation blocks submit, hide-details respected; integration — GET 200;
  GET with deflated `SAMLRequest` → InResponseTo pre-filled; POST SAMLRequest → 200 pre-filled;
  valid POST → 302 with `SAMLResponse` in `Location` (redirect binding) / auto-post HTML with
  `action`=ACS (post binding); invalid POST → 200 with validation text; named-GUID variants.
- 📄 `Task11_HomePageAndSsoEndpoint.md`

### Task12 — Logout flow (receive + initiate + respond)
- **Goal:** Static SSR `Logout.razor` at `/Logout` (+ named): no message → `InitiateLogoutForm`
  (static `EditForm`, `[SupplyParameterFromQuery]` pre-fill = `TryUpdateModel` parity);
  `LogoutRequest` → `RespondToLogoutPanel` (shows pretty XML, InResponseTo, DestinationUrl =
  `issuer + "/Logout"`, RelayState); `LogoutResponse` → `ReceivedLogoutResponsePanel` (status +
  XML). Minimal API `POST /Logout/InitiateLogout` and `POST /Logout/RespondToLogoutRequest`
  (+ named) → Redirect-binding `CommandResult` (302). Unknown message → 500 (legacy threw).
- **Legacy sources:** `Controllers/LogoutController.cs`, `Views/Logout/*.cshtml`,
  `Models/LogoutModels.cs`.
- **Test phase:** bUnit — three panels render with model data; integration — GET with
  redirect-binding `LogoutRequest` → respond view contains InResponseTo; POST InitiateLogout →
  302 `Location` contains `SAMLRequest`; POST RespondToLogoutRequest → 302 contains
  `SAMLResponse` + `RelayState`; LogoutResponse receive → 200 status shown.
- 📄 `Task12_LogoutFlow.md`

### Task13 — Discovery Service
- **Goal:** Static SSR `DiscoveryService.razor` at `/DiscoveryService` (+ named): GET with
  `isPassive=true` → 302; POST → 302; else form (entityID display, return/returnIDParam/SelectedIdp
  fields, defaults `returnIDParam=entityID`, `SelectedIdp`=metadata URL). Redirect URL building
  (`?` vs `&` delimiter) exactly as legacy. Static-SSR redirect mechanism documented
  (`NavigationManager` + `NavigationException` or minimal endpoint — chosen in task file).
  **Carry-over from Task01:** the production csproj still carries the template's
  `<BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>`; the
  `NavigationException` variant requires flipping it to `false`, the minimal-endpoint variant does
  not. Decide here.
- **Legacy sources:** `Controllers/DiscoveryServiceController.cs`,
  `Views/DiscoveryService/Index.cshtml`, `Models/DiscoveryServiceModel.cs`.
- **Test phase:** bUnit — form render + defaults; integration — GET passive → 302
  `return?entityID=…`; POST → 302 with `&` when `return` already has query; GET → 200 form.
- 📄 `Task13_DiscoveryService.md`

### Task14 — Manage tenant page (Interactive Server)
- **Goal:** `ManageTenant.razor` (`@rendermode InteractiveServer`) at `/Manage`? **No** —
  legacy requires `idpId`: routes `/{idpId:guid}/Manage` and `/Manage`+`?idpId=` handling per
  legacy route defaults; load existing JSON or generate template from `default.json` (with the
  legacy placeholder replacements); `EditForm` with multiline `JsonData`; server-side JSON syntax +
  JSON-Schema validation (JsonSchema.Net against `IdpConfigurationSchema.json`); reject updates to
  the default GUID; save → write file + update cache → confirmation/refresh; render tenant
  entityId/manage URLs, GDPR panel, schema link, `MetadataLinks`.
- **Legacy sources:** `Controllers/ManageController.cs` (`Index` GET/POST),
  `Views/Manage/Index.cshtml`, `Models/ManageIdpModel.cs`.
- **Carry-over from Task02:** the schema file is at
  `new/src/.../wwwroot/Content/IdpConfigurationSchema.json` and is already served at the legacy
  public URL `/Content/IdpConfigurationSchema.json` (use that for the schema link). Read it from
  disk via `IWebHostEnvironment.WebRootPath` — .NET 10 does not copy `wwwroot` into `bin`, so
  `AppContext.BaseDirectory` will not find it.
- **Test phase:** unit — schema validation accept/reject cases incl. legacy sample files; bUnit —
  template shown for new tenant, error on invalid JSON/schema, default-GUID rejection, successful
  save calls store; integration — GET 200 contains JSON; saved tenant observable via
  `/{idpId}/Manage/CurrentConfiguration` with new ETag.
- 📄 `Task14_ManageTenantPage.md`

### Task15 — Route-compatibility matrix & SAML round-trip verification
- **Goal:** Integration test suite enumerating **every legacy public route** (default + named-GUID)
  asserting status + content type: `/`, `/Logout`, `/DiscoveryService`, `/Metadata`,
  `/Metadata/BrowserFriendly`, `/Federation`, `/Federation/BrowserFriendly`, `/Certificate`,
  `/Manage/CurrentConfiguration`, `/Manage?idpId=…`, `/{guid}` variants, POST `/ArtifactResolve`;
  plus end-to-end SSO round trip: build `AuthnRequest` → GET `/` → submit form → parse
  `SAMLResponse` → inflate/decode → verify XML signature against `/Certificate` download; artifact
  round trip through Task09 endpoint.
- **Legacy sources:** `App_Start/RouteConfig.cs` (authoritative route list).
- **Test phase:** the suite *is* the task output; all green, plus fixes routed back to earlier
  tasks when a mismatch is found.
- 📄 `Task15_RouteCompatibilityAndE2E.md`

### Task16 — Final cleanup, docs, and release readiness
- **Goal:** Remove leftover template artifacts; `README.md` for `new/` (run, user-secrets, tenant
  management, route map); `.gitignore` additions (`.user-secrets` id note, `App_Data/*.pfx`
  handling decision); `libman.json` restore wired into build (`dotnet build` runs
  `libman restore` via target or documented step); final `dotnet build` + `dotnet test` full pass;
  update repository-level docs if required. **Carry-over from Task01:** document the accepted
  transitive NuGet advisories (NU1903 `Newtonsoft.Json` 10.0.1, NU1904 `System.Drawing.Common`
  4.7.0 — both via `Sustainsys.Saml2` 2.11.0) in the `new/` README, including why they are not
  resolved (pinned-version rule). **Carry-overs from Task02:** document the exact commands
  `dotnet user-secrets --project new/src/Sustainsys.Saml2.StubIdp set
  "StubIdp:Certificates:Default:Password" "<pwd>"` (and the `LegacyKentor` equivalent) plus the
  fact that the bundled test certificates need no password; note that the `.pfx`/`.cer` files are
  intentionally tracked in git (public test certs, as in `legacy/`), which also means `.gitignore`
  gets **no** `App_Data/*.pfx` rule; and decide whether
  `new/src/.../appsettings.Development.json` (currently untracked and not ignored) is committed or
  ignored.
- **Test phase:** clean-checkout simulation (restore → build → test) passes from scratch.
- 📄 `Task16_FinalCleanupAndDocs.md`

---

## 4. Global conventions for all tasks
- Run all CLI commands from the **repository root** with explicit `new/...` paths.
- Production code: `new/src/Sustainsys.Saml2.StubIdp/`; unit tests mirror namespaces under
  `new/tests/Sustainsys.Saml2.StubIdp.Tests/`; endpoint tests under
  `new/tests/Sustainsys.Saml2.StubIdp.IntegrationTests/`.
- Do **not** modify anything under `legacy/`.
- Every named-GUID route uses constraint `{idpId:guid}` (ASP.NET Core route constraint is
  equivalent to the legacy regex for canonical GUID formats).
- SAML protocol endpoints must never require antiforgery tokens; interactive Blazor forms use the
  built-in `EditForm` antiforgery.
- Character-for-character preservation of externally visible payloads (SAML XML, redirect query
  composition, metadata content types, ETag semantics) takes precedence over internal elegance.
- **XML docs:** all public types and members carry XML documentation comments (`<summary>`,
  `<param>`, `<returns>`); enable `<GenerateDocumentationFile>` in the production project.
- **Namespaces mirror folders** under the root namespace:
  `Sustainsys.Saml2.StubIdp.{Configuration|Services|Models|Endpoints|Saml|Components}`; tests mirror
  the same structure.
- **DI style:** use primary-constructor syntax for injected services where it fits
  (`public class TenantConfigurationStore(...)`) and guard every public service method argument with
  `ArgumentNullException.ThrowIfNull(...)`; add null-parameter validation tests alongside the
  existing null-lookup tests.
- **Logging:** structured `ILogger<T>` with meaningful scope (e.g. tenant GUID); `Microsoft.Extensions.Logging`
  ships in the shared framework — no new packages. Never log SAML payloads, certificate passwords,
  or tenant secrets.
- **Async I/O:** all file/network I/O in services is async (`File.ReadAllTextAsync`,
  `File.WriteAllTextAsync`, `ReadFormAsync`, …) and accepts a `CancellationToken` where the caller
  has one.
- **Localization exception:** externally visible strings (SAML/validation messages) stay literal for
  legacy character-for-character parity — `ResourceManager`/`LogMessages`/`ErrorMessages` resource
  files are deliberately **not** used in this migration.
- **Certificate lifetime:** `X509Certificate2` instances are owned by the singleton certificate
  service for the application lifetime; never dispose them per request (implement `IDisposable` on
  the service only if certificates are ever reloaded).
- `ConfigureAwait(false)` only in library-style code without ASP.NET Core context; endpoint and
  component code omits it.

## 5. Execution workflow
1. ✅ This `PLAN.md`.
2. ✅ Confirmed by the repository owner.
3. ✅ `Task01_…md` … `Task16_…md` generated with full implementation steps, before/after code
   snippets, pinned packages, exact paths, and per-task test phases (unit + integration snippets).
4. ⏳ Implementation, one task at a time in order, each gated on `dotnet test new/StubIdp.slnx`:
   - ✅ **Task01** — solution scaffolded, both smoke tests green.
   - ✅ **Task02** — configuration, options, and data files; 39 tests green.
   - ⏭ **Task03 is next** — core services: certificate, URL resolution, tenant store.
   - ⏳ Task04 … Task16.
5. When a task is finished, record its outcome in the task file (status + as-built notes +
   carry-over items), tick it off in §3, and move the "next" marker above.
