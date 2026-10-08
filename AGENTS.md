# AGENTS.md

Guidance for AI agents (and humans) working in this repository.

## Project overview

`stubidp` is a mock/stub SAML Identity Provider for testing SSO flows, manually forked from
[Sustainsys/Saml2](https://github.com/Sustainsys/Saml2) branch `v2` (commit `d4c716f`).

The active work is a **complete rewrite** of the legacy ASP.NET MVC 5 (.NET Framework 4.7.2)
application as a **.NET 10 Blazor Web App**. The authoritative specification lives in
`Migration-Plan/`:

- `Migration-Plan/PLAN.md` — full analysis, architecture, and task list (start here)
- `Migration-Plan/Task01_*.md` … `Task16_*.md` — sequential, self-contained task specs, each
  with its own implementation steps and test phase

Read `PLAN.md` before making any changes. Follow tasks in order (Task01 → Task16). A task is
done only when its test phase passes.

## Current progress

| Task | State |
|---|---|
| Task01 — Scaffold solution, projects, test skeleton | ✅ done (build 0 errors, 1/1 unit + 1/1 integration test) |
| Task02 — Configuration, options, and data files | ⏭ **next** |
| Task03 … Task16 | ⏳ pending |

`new/` is scaffolded and green: `new/StubIdp.slnx` with the Blazor Web App and both test
projects. Known carry-over items from Task01:

- the production csproj keeps the template's `BlazorDisableThrowNavigationException=true`
  (Task13 decides whether to flip it for the static-SSR redirect);
- transitive NuGet advisories NU1903 (`Newtonsoft.Json` 10.0.1) and NU1904
  (`System.Drawing.Common` 4.7.0) come from `Sustainsys.Saml2` 2.11.0 and are accepted as
  warnings — do not "fix" them by bumping packages (rule 6); Task16 documents them.

When you finish a task: record status + as-built notes + carry-overs in its `TaskNN_*.md`, tick
it off in `PLAN.md` §3 and §5, and update this table.

## Repository layout

```
legacy/          # READ-ONLY reference. DO NOT modify anything under legacy/.
  Sustainsys.Saml2.StubIdp/        # the app being migrated (MVC 5, net472)
  Sustainsys.Saml2/                # core library — NOT ported; replaced by NuGet package
  Sustainsys.Saml2.Mvc/            # System.Web bridge — NOT ported
  Sustainsys.Saml2.AspNetCore2/    # reference for the CommandResult/HttpRequestData bridges
new/             # TARGET: the migrated .NET 10 solution (scaffolded by Task01)
Migration-Plan/  # the migration specification (PLAN.md + Task01..Task16)
PROMPTS/         # prompt files used to generate the plan
```

Target solution layout (created by Task01):

```
new/
  StubIdp.slnx
  src/Sustainsys.Saml2.StubIdp/            # Blazor Web App (net10.0)
  tests/Sustainsys.Saml2.StubIdp.Tests/              # xUnit + Moq + bUnit (unit/component)
  tests/Sustainsys.Saml2.StubIdp.IntegrationTests/   # xUnit + WebApplicationFactory
```

## Hard rules

1. **Never modify anything under `legacy/`.** It is read-only reference material. When a task
   lists legacy sources, read them for behavior — do not edit them.
2. **Preserve all public routes.** Every endpoint exists in two shapes: `/Metadata` and
   `/{guid}/Metadata` (see `legacy/.../App_Start/RouteConfig.cs`). Named routes use the
   `{idpId:guid}` route constraint.
3. **Character-for-character preservation of externally visible payloads** (SAML XML, redirect
   query composition, metadata content types, ETag/304 semantics) takes precedence over
   internal elegance. Do not rebuild query strings or SAML forms the library already generates.
4. **SAML protocol endpoints must never require antiforgery tokens** (external SPs POST to
   them). Interactive Blazor forms use the built-in `EditForm` antiforgery.
5. **No secrets in config.** Certificate passwords go in `dotnet user-secrets`
   (`StubIdp:Certificates:*:Password`), never in `appsettings.json` or the repo.
6. **Pinned package versions** — do not bump or add packages beyond what the tasks specify.
7. **Run all CLI commands from the repository root** with explicit `new/...` paths.

## Architecture decisions (summary)

### Render-mode split

| Surface | Implementation | Render mode |
|---|---|---|
| Home GET `/`, `/{idpId}` | `Home.razor` + interactive `AssertionForm.razor` island | Static SSR + InteractiveServer island |
| Home POST (SAML emit/receive) | Minimal API + `RazorComponentResult<Home>` | Minimal API |
| Logout receive | `Logout.razor` | Static SSR |
| Logout emits | Minimal API → `CommandResult` → 302 | Minimal API |
| DiscoveryService | `DiscoveryService.razor` | Static SSR |
| Manage tenant | `ManageTenant.razor` | Interactive Server |
| Metadata, Federation, Certificate, CurrentConfiguration, ArtifactResolve | Minimal API | Minimal API |

Split rule: when one URL does both UI and protocol, the request shape decides — SAML/message
input → protocol path; otherwise → UI path.

### Key bridges (Task04)

- `HttpContext.ToHttpRequestData(...)` — ported from `legacy/Sustainsys.Saml2.AspNetCore2/HttpRequestExtensions.cs`
- `CommandResult.ToResult()` → `CommandResultIResult : IResult` — replaces the removed
  `Sustainsys.Saml2.Mvc` `ToActionResult()`
- `CompressAttribute` → ASP.NET Core Response Compression middleware

### Core services (Task03)

- `CertificateService` — host-aware cert selection (`stubidp.kentor.se` → LegacyKentor, else Default)
- `StubIdpUrlResolver` — GUID-segment- and PathBase-aware absolute URLs
- `ITenantConfigurationStore` — `App_Data/{guid}.json` + `ConcurrentDictionary` cache; ETag =
  uppercase hex MD5 of exact JSON; default GUID `e73d98ff-0f1c-4cc2-8808-6d1bf028a8a9` → `default.json`

### Configuration (Task02)

`appsettings.json` `StubIdp` section: `DefaultAcsUrl`, `DefaultNameId`, `DataPath`, `Certificates`.
Bound via Options Pattern with `IValidateOptions`. Per-tenant `App_Data/*.json` files stay data
files behind the storage service, not in appsettings.

## Pinned packages

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
| Microsoft.Web.LibraryManager.Cli | 3.0.114 | dotnet tool (normalize.css via LibMan) |

## Commands

All from the repository root:

```sh
dotnet build new/StubIdp.slnx
dotnet test new/StubIdp.slnx          # full suite; each task's gate
dotnet restore new/StubIdp.slnx
dotnet tool restore && dotnet tool run libman restore   # LibMan (Task16 clean setup)
dotnet run --project new/src/Sustainsys.Saml2.StubIdp
```

Tooling: .NET SDK 10 (see `.devcontainer/Dockerfile`). A `Makefile` exists for devcontainer
build only (`make devcontainer`).

## Testing strategy

- **Unit/component** (`new/tests/...Tests/`): xUnit + Moq; bUnit for Blazor components
  (`RenderComponent<T>`, event dispatch, `EditForm` validation). Mirror production namespaces.
- **Integration** (`new/tests/...IntegrationTests/`): `WebApplicationFactory<Program>` +
  `HttpClient`; assert status codes, content types, ETag/304, redirect `Location`, and the full
  default + named-GUID route matrix. `Program` is made test-visible via
  `public partial class Program { }` (Task01).
- Every task embeds its own test phase; there are no separate test tasks. Task15 adds the full
  legacy route matrix + SSO/artifact round-trip E2E suite. When a mismatch is found, fix it in
  the originating task — do not weaken assertions.

## Code conventions

Authoritative list: `Migration-Plan/PLAN.md` §4 "Global conventions for all tasks". Summary:

- Production code: `new/src/Sustainsys.Saml2.StubIdp/`
- Unit tests: `new/tests/Sustainsys.Saml2.StubIdp.Tests/` (mirror namespaces)
- Integration tests: `new/tests/Sustainsys.Saml2.StubIdp.IntegrationTests/`
- Target framework `net10.0`, nullable + implicit usings enabled
- Namespaces mirror folders: `Sustainsys.Saml2.StubIdp.{Configuration|Services|Models|Endpoints|Saml|Components}`
- XML documentation comments (`<summary>`, `<param>`, `<returns>`) on all public types and
  members; `<GenerateDocumentationFile>` enabled in the production project
- Primary-constructor DI where it fits; guard public service arguments with
  `ArgumentNullException.ThrowIfNull(...)` and test null-parameter validation
- Structured `ILogger<T>` logging (in-box `Microsoft.Extensions.Logging`, no new packages);
  never log SAML payloads, certificate passwords, or tenant secrets
- Async file/network I/O in services (`File.ReadAllTextAsync`, `ReadFormAsync`, …) with
  `CancellationToken` where available
- Inject services (Options, `IHttpContextAccessor`, `ITenantConfigurationStore`) — no static
  `HttpContext.Current` coupling, no `ConfigurationManager.AppSettings`
- Keep DataAnnotations on models for `EditForm` validation
- Certificate lifetime: singleton `CertificateService` owns its `X509Certificate2` instances;
  never dispose per request
- Localization exception: externally visible strings stay literal for legacy parity — no
  RESX/`ResourceManager` in this migration
- Preserve legacy quirks that are externally visible (e.g. ArtifactResolve returns
  `text/html; charset=utf-8` — document, do not "fix")

## Legacy behavior reference (high-signal)

- Routes: `NamedIdp` `{idpId}/{controller}/{action}/{id}` (GUID-constrained) and `Default`
  `{controller}/{action}/{id}`, both defaulting to `Home/Index`.
- Content types: metadata `application/samlmetadata+xml` / `text/xml`; certificate `text/plain`
  with `Content-Disposition: attachment`; CurrentConfiguration `application/json` + ETag/304.
- Signing: `SignedXml.XmlDsigRSASHA256Url`; cert selection by Host header.
- When in doubt about exact behavior, read the corresponding legacy controller/view listed in
  the task file's "Legacy sources" section and match it.