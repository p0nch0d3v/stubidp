# Task01 — Scaffold solution, projects, and test skeleton

> **Status: ✅ Completed.** Gate verified from the repository root: `dotnet build new/StubIdp.slnx`
> → 0 errors, `dotnet test new/StubIdp.slnx` → 1/1 unit + 1/1 integration test passing.
> See [As-built notes](#as-built-notes) for the exact resulting state and the two carry-over items.

## Goal and scope

Create the .NET 10 Blazor Web App solution under `new/`, both test projects, package references, and a minimal passing unit/integration test baseline. Keep project and namespace names exactly as in `PLAN.md`. Do not modify `legacy/`.

## Implementation

1. From the repository root, create a Blazor Web App using .NET 10, Server interactivity, and per-component interactivity. Place it at `new/src/Sustainsys.Saml2.StubIdp/` and name its assembly/root namespace `Sustainsys.Saml2.StubIdp`.
2. Create `new/StubIdp.slnx`, `new/tests/Sustainsys.Saml2.StubIdp.Tests/`, and `new/tests/Sustainsys.Saml2.StubIdp.IntegrationTests/`; add all three projects to the solution. Use `net10.0` throughout.
3. Remove template Counter/Weather pages and their links. Retain the generated Blazor host, router, layout, and static asset plumbing needed for a GET `/` smoke test.
4. Add these pinned references to the production project: `Sustainsys.Saml2` 2.11.0 and `JsonSchema.Net` 9.4.0. Unit tests use xunit 2.9.3, xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.10.1, Moq 4.21.0, bUnit 2.11.3, coverlet.collector 10.1.0. Integration tests use xunit 2.9.3, xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.10.1, Microsoft.AspNetCore.Mvc.Testing 10.0.12, and coverlet.collector 10.1.0.
5. Add `public partial class Program { }` at the bottom of the top-level `Program.cs` so `WebApplicationFactory<Program>` can access the entry point.

Representative project change (keep generated SDK attributes and package versions pinned):

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Sustainsys.Saml2" Version="2.11.0" />
  <PackageReference Include="JsonSchema.Net" Version="9.4.0" />
</ItemGroup>
```

Before: a single MVC 5 / .NET Framework application and no `new/` solution. After: `new/StubIdp.slnx` contains the Blazor app and both test projects, with a minimal home page and test-visible entry point.

## Test phase

Add `Unit/SmokeTests.cs` with a trivial assertion (`Assert.True(true)`) to verify discovery and execution. Add `Infrastructure/SmokeTests.cs` in the integration project:

```csharp
public class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    public SmokeTests(WebApplicationFactory<Program> factory) => client = factory.CreateClient();

    [Fact]
    public async Task Root_returns_success()
    {
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

Run `dotnet build new/StubIdp.slnx` and `dotnet test new/StubIdp.slnx` from the repository root. Both must pass before Task02.

## Output checklist

- [x] `new/StubIdp.slnx` and the three named projects exist and target `net10.0`.
- [x] No Counter/Weather sample UI remains.
- [x] `Program` is public to the test host; both smoke tests pass.

## Before/after code sketch

```xml
<!-- Before: legacy target and web framework -->
<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>

<!-- After: new/src/Sustainsys.Saml2.StubIdp/Sustainsys.Saml2.StubIdp.csproj -->
<TargetFramework>net10.0</TargetFramework>
<PackageReference Include="Sustainsys.Saml2" Version="2.11.0" />
<PackageReference Include="JsonSchema.Net" Version="9.4.0" />
```

The exact test package pins are xunit 2.9.3, xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.10.1, Moq 4.21.0, bUnit 2.11.3, Microsoft.AspNetCore.Mvc.Testing 10.0.12, and coverlet.collector 10.1.0.

## As-built notes

Commands used (from the repository root):

```sh
dotnet new blazor -o new/src/Sustainsys.Saml2.StubIdp -n Sustainsys.Saml2.StubIdp \
  -f net10.0 --interactivity Server --no-restore      # per-component: --all-interactive omitted
dotnet new sln -o new -n StubIdp --format slnx
dotnet new xunit -o new/tests/Sustainsys.Saml2.StubIdp.Tests            -f net10.0
dotnet new xunit -o new/tests/Sustainsys.Saml2.StubIdp.IntegrationTests -f net10.0
dotnet sln new/StubIdp.slnx add <all three projects>
```

Resulting files of record:

| Path | Notes |
|---|---|
| `new/StubIdp.slnx` | slnx format; `/src/` + `/tests/` solution folders |
| `new/src/Sustainsys.Saml2.StubIdp/Sustainsys.Saml2.StubIdp.csproj` | `Sustainsys.Saml2` 2.11.0, `JsonSchema.Net` 9.4.0, `<GenerateDocumentationFile>true</GenerateDocumentationFile>` |
| `new/src/Sustainsys.Saml2.StubIdp/Program.cs` | template pipeline + documented `public partial class Program { }` at the bottom |
| `new/tests/Sustainsys.Saml2.StubIdp.Tests/Unit/SmokeTests.cs` | namespace `Sustainsys.Saml2.StubIdp.Tests.Unit` |
| `new/tests/Sustainsys.Saml2.StubIdp.IntegrationTests/Infrastructure/SmokeTests.cs` | namespace `Sustainsys.Saml2.StubIdp.IntegrationTests.Infrastructure`; `ArgumentNullException.ThrowIfNull(factory)` per PLAN §4 |

Decisions taken beyond the literal spec:

- `<GenerateDocumentationFile>true</GenerateDocumentationFile>` was enabled already in Task01
  (PLAN §4 requires it for the production project) and both smoke-test types plus the `Program`
  partial carry XML doc comments.
- Both test projects got a `ProjectReference` to the production project — required by
  `WebApplicationFactory<Program>` and by every later task.
- Template `UnitTest1.cs` files were deleted along with `Counter.razor` / `Weather.razor` and
  their `NavMenu.razor` links. The bootstrap assets under `wwwroot/lib/bootstrap/` and
  `wwwroot/app.css` were **kept** as the generated static-asset plumbing (step 3) — Task10
  replaces them with `site.css` + LibMan `normalize.css`.
- The now-redundant `new/.gitkeep` placeholder was removed.

### Carry-over items (not fixed in Task01)

1. **`<BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>`** was
   emitted by the .NET 10 template into the production csproj and left untouched. Task13 must
   either set it to `false` or choose the minimal-endpoint redirect mechanism, because
   `NavigationManager` + `NavigationException` cannot be used for the static-SSR DiscoveryService
   redirect while this property is `true`.
2. **Transitive NuGet advisories (NU1903 / NU1904).** `Sustainsys.Saml2` 2.11.0 brings
   `Newtonsoft.Json` 10.0.1 (high) and `System.Drawing.Common` 4.7.0 (critical). These surface as
   restore warnings on all three projects. They cannot be resolved without bumping or pinning
   transitive packages, which PLAN §2.5 / AGENTS.md rule 6 forbid. Accepted as warnings; Task16
   documents them in the `new/` README.

