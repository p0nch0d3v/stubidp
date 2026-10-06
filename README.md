# stubidp

**stubIdp (Stub Identity Provider)** is a mock, dummy, or simulated SAML Identity Provider
server used by software developers to test single sign-on (SSO) and authentication flows.

> Manually forked from repo: [https://github.com/Sustainsys/Saml2/](https://github.com/Sustainsys/Saml2/)
> branch: [v2](https://github.com/Sustainsys/Saml2/tree/v2),
> commit: [d4c716f2db25b823b2dd6a73d06a5c92218c0d27](https://github.com/Sustainsys/Saml2/commit/d4c716f2db25b823b2dd6a73d06a5c92218c0d27)

## Current status

The active work is a **complete rewrite** of the legacy ASP.NET MVC 5 (.NET Framework 4.7.2)
application as a **.NET 10 Blazor Web App**, executed task-by-task from the specification in
[`Migration-Plan/`](Migration-Plan/).

| Area | State |
|---|---|
| `legacy/` | ✅ Forked and frozen as read-only reference (do not modify) |
| `Migration-Plan/` | ✅ Complete — `PLAN.md` + `Task01` … `Task16`, each with its own test phase |
| `new/` | ⏳ Target solution — scaffolded by `Task01`, not started yet |
| Task progress | ⏳ Tasks run sequentially (`Task01` → `Task16`); a task is done only when its test phase passes (`dotnet test new/StubIdp.slnx`) |

## Repository layout

```
legacy/           # READ-ONLY reference: the original Sustainsys.Saml2 fork (MVC 5, net472)
  Sustainsys.Saml2.StubIdp/    # the app being migrated
  Sustainsys.Saml2/            # core SAML library (replaced by NuGet package Sustainsys.Saml2 2.11.0)
  Sustainsys.Saml2.Mvc/        # System.Web bridge (replaced by in-project CommandResult → IResult)
  Sustainsys.Saml2.AspNetCore2/ # reference for the AspNetCore bridges ported in Task04
new/              # TARGET: the migrated .NET 10 solution (created by Task01)
  StubIdp.slnx
  src/Sustainsys.Saml2.StubIdp/             # Blazor Web App (net10.0)
  tests/Sustainsys.Saml2.StubIdp.Tests/     # xUnit + Moq + bUnit (unit/component)
  tests/Sustainsys.Saml2.StubIdp.IntegrationTests/  # xUnit + WebApplicationFactory
Migration-Plan/   # migration specification: PLAN.md + Task01..Task16
PROMPTS/          # prompt files used to generate the plan
AGENTS.md         # guidance for AI agents (and humans) working in this repository
```

## Migration plan

Start with [`Migration-Plan/PLAN.md`](Migration-Plan/PLAN.md) — it contains the full
current-state analysis, target architecture, and the task list. Then read
[`AGENTS.md`](AGENTS.md) for working rules.

Key decisions:

- **SAML core** comes from the **Sustainsys.Saml2 2.11.0** NuGet package; the forked
  `Sustainsys.Saml2` source is *not* ported.
- **Render-mode split**: protocol/data endpoints are Minimal APIs (preserving content types,
  ETag/304, compression, redirect `Location`); UI surfaces are Blazor components
  (static SSR, an Interactive Server island on Home, and a fully interactive
  `ManageTenant` page).
- **Route compatibility is mandatory**: every public endpoint keeps both shapes —
  `/Metadata` and `/{guid}/Metadata` (GUID-constrained `idpId` route segment).
- **Character-for-character preservation** of externally visible payloads (SAML XML, redirect
  query composition, metadata content types) takes precedence over internal elegance.
- **No secrets in config**: certificate passwords live in `dotnet user-secrets`, never in
  `appsettings.json`.
- Package versions are **pinned** in `AGENTS.md`; SAML protocol endpoints never require
  antiforgery tokens.

### Tasks

| # | Task | # | Task |
|---|---|---|---|
| 01 | Scaffold solution | 09 | ArtifactResolve endpoint |
| 02 | Configuration & data files | 10 | Layout & static assets |
| 03 | Core services | 11 | Home page & SSO endpoint |
| 04 | SAML protocol bridges | 12 | Logout flow |
| 05 | Models port | 13 | DiscoveryService |
| 06 | Metadata / federation endpoints | 14 | Manage tenant page |
| 07 | Certificate endpoint | 15 | Route compatibility & E2E |
| 08 | CurrentConfiguration endpoint | 16 | Final cleanup & docs |

## Development

Tooling: **.NET SDK 10** (see `.devcontainer/Dockerfile`), Git.

All commands run from the repository root:

```sh
# Build & test (once new/ is scaffolded by Task01)
dotnet build new/StubIdp.slnx
dotnet test new/StubIdp.slnx

# Restore tooling & static assets (LibMan → normalize.css)
dotnet tool restore && dotnet tool run libman restore

# Run the app
dotnet run --project new/src/Sustainsys.Saml2.StubIdp

# Dev container image
make devcontainer
```

## License

See [LICENSE](LICENSE).
