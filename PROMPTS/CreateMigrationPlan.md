Act as an expert .NET Architect. First, analyze the current Sustainsys.Saml2.StubIdp project (ASP.NET MVC 5 on .NET Framework 4.7.2) located at `legacy/Sustainsys.Saml2.StubIdp/` (part of the legacy solution `legacy/Sustainsys.Saml2.sln`). Then, create a comprehensive migration plan to rewrite it on .NET 10 (`net10.0`) using the Blazor Web App template (`dotnet new blazor`).

# Repository Layout:
* `legacy/` — read-only reference code manually forked from https://github.com/Sustainsys/Saml2 (branch `v2`). Do NOT modify anything under `legacy/`.
  * `legacy/Sustainsys.Saml2.StubIdp/` — the application to migrate.
  * `legacy/Sustainsys.Saml2/` — core library (not to be ported).
  * `legacy/Sustainsys.Saml2.Mvc/` — System.Web MVC bridge (not to be ported).
  * `legacy/Sustainsys.Saml2.AspNetCore2/` — may be used as a reference for the ASP.NET Core `CommandResult` bridge.
* `new/` — target folder for the migrated solution (currently empty except `.gitkeep`).
* `PROMPTS/` — prompt files (this file).

# Scope & Constraints:
* Core library: Do NOT port the source of `legacy/Sustainsys.Saml2/`. Replace the existing `ProjectReference` (`..\Sustainsys.Saml2\Sustainsys.Saml2.csproj`) with a `PackageReference` to the latest stable Sustainsys.Saml2 2.x NuGet package that supports `net10.0`.
* Sustainsys.Saml2.Mvc: Remove this `ProjectReference` (`..\Sustainsys.Saml2.Mvc\Sustainsys.Saml2.Mvc.csproj`; it depends on System.Web). Re-implement the `CommandResult` → `IResult`/`IActionResult` bridge (`ToActionResult()`) inside the new project (see `legacy/Sustainsys.Saml2.AspNetCore2/CommandResultExtensions.cs` for reference).
* Other dependencies: Replace every other NuGet package listed in `legacy/Sustainsys.Saml2.StubIdp/packages.config` with its ASP.NET Core equivalent or remove it. Client-side libraries (jQuery, Select2, ICanHaz, normalize.css) must either be replaced by Blazor components or delivered as static assets via LibMan. Bundling/less packages (Web.Optimization, WebGrease, dotless) must be removed.
* Architecture:
  * UI-only pages (forms and displays that do not receive or emit SAML messages): convert to Blazor components using the Interactive Server render mode.
  * SAML protocol steps (receiving external POST/Redirect bindings, issuing redirects or auto-posting forms, e.g. Home/Index POST, Logout, DiscoveryService): implement as static SSR components or Minimal API endpoints that have full `HttpContext` access.
  * Actions that return either a View or a redirect/data depending on input must be split explicitly, and the plan must document how.
  * Data/protocol endpoints (Metadata, Federation, Certificate, ArtifactResolve, Manage/CurrentConfiguration): convert to ASP.NET Core Minimal API or controller endpoints, preserving the existing content types (XML, SOAP, JSON, file) and ETag/304 behavior.
  * Preserve all existing public routes/URLs (as defined in `legacy/Sustainsys.Saml2.StubIdp/App_Start/RouteConfig.cs` and the controllers) so that existing SPs keep working.
* Location: Scaffold the new solution in `new/` at the repository root (a sibling of `legacy/`). Suggested layout:
  * `new/StubIdp.slnx` (or `.sln`)
  * `new/src/Sustainsys.Saml2.StubIdp/` — the Blazor Web App
  * `new/tests/Sustainsys.Saml2.StubIdp.Tests/` — unit tests
  * `new/tests/Sustainsys.Saml2.StubIdp.IntegrationTests/` — integration tests
* Configuration:
  * Move static settings (e.g. `defaultAcsUrl`, `defaultNameId` from `legacy/Sustainsys.Saml2.StubIdp/Web.config`, file paths, certificate locations) into `appsettings.json` and bind them with the strongly-typed Options Pattern.
  * Keep per-IdP runtime data (`legacy/Sustainsys.Saml2.StubIdp/App_Data/*.json`) as data files behind a storage service; do not move it into appsettings.
  * Never store certificate private keys or passwords in `appsettings.json`; use user-secrets or a certificate path in configuration.
* Testing Validation: Every migrated component, API endpoint, and service must be covered by automated tests. In `new/tests/`, scaffold:
  * a unit test project using xUnit, Moq, and bUnit (for Blazor components);
  * integration tests using `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) for API/protocol endpoints and route compatibility.

# Output Structure:
* Generate the plan inside `Migration-Plan/` at the repository root (a sibling of `legacy/` and `new/`).
* Create a main `PLAN.md` that lists every task in sequential order. Each task entry includes its own test phase; do not create separate test tasks.
* Create one Markdown file per task (e.g. `Task01_<Name>.md`). Each file contains both the implementation steps and that task's test phase.

# Workflow:
1. Generate `PLAN.md` only.
2. STOP and wait for my explicit confirmation.
3. After confirmation, generate the individual task files.

# Detail Level:
The task files must act as highly detailed specifications so a human software engineer can execute them without guessing. For each task, include the following where applicable:
* Exact .NET CLI commands for the production and test projects (run from the repository root or with explicit `new/...` paths).
* Required NuGet packages with exact, pinned versions.
* Exact file paths to create or modify (relative to the repository root, e.g. `new/src/Sustainsys.Saml2.StubIdp/Program.cs`), and exact source paths under `legacy/` being migrated.
* Before/after code snippets demonstrating the View-to-Blazor and Controller-to-API translations.
* Unit test code snippets that explicitly validate the behavior and acceptance criteria of the newly migrated code.
* Integration test snippets for endpoint and route behavior.
