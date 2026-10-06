# Task16 — Final cleanup, docs, and release readiness

## Goal and scope

Remove migration leftovers, document running and configuring the new app, and prove a clean restore/build/test workflow.

## Implementation

1. Search `new/` for template text/pages and remove any unused template assets, styles, and sample navigation. Keep only files required by the migrated app and tests.
2. Write `new/README.md` covering .NET 10 prerequisites, restore/build/run commands, URL/route map, tenant management workflow, data-file provisioning, certificate selection by host, user-secrets setup for both certificate passwords, and how to run unit/integration tests. Explain that PFX files are private credentials and should follow the repository's chosen handling policy; do not document or check in passwords.
3. Update repository `.gitignore` only for relevant new artifacts (`.user-secrets` ID files if used, build output, local generated tenant JSON as appropriate, and PFX policy chosen by maintainers). Avoid ignoring source JSON tenant fixtures that tests depend on.
4. Ensure `libman.json` restoration is repeatable: wire the local LibraryManager tool and a build target if supported, or document `dotnet tool restore` + `dotnet tool run libman restore` as a required setup step. The clean-checkout recipe must run it before build when a target is not wired.
5. Review package versions, XML/static MIME values, path-base/GUID routes, and tests for environment-specific absolute paths. Confirm no passwords/private credentials are in tracked config.

Before: migration implementation may still contain template assets and incomplete setup notes. After: `new/README.md` documents the supported clean workflow, all app/test assets restore deterministically, and the full solution passes from a clean state.

## Test phase — clean-checkout simulation

From repository root, run the same sequence documented for contributors:

```sh
dotnet tool restore
dotnet tool run libman restore
dotnet restore new/StubIdp.slnx
dotnet build new/StubIdp.slnx --no-restore
dotnet test new/StubIdp.slnx --no-build
```

If LibMan is wired into the build, also verify `dotnet build new/StubIdp.slnx` from a clean `wwwroot/lib` directory. Check `git status --short`, `git diff --check`, review the full diff, and verify no `legacy/` paths appear in changed files. Record any external prerequisite (certificate passwords/files) clearly without committing secrets.

## Release checklist

- `dotnet build` and `dotnet test` pass after restore from scratch.
- README route map and user-secrets commands match application behavior.
- No template pages, unintentional packages, secrets, or legacy modifications remain.

## Before/after and test sketches

```sh
# Before (ad hoc local setup)
dotnet run --project new/src/Sustainsys.Saml2.StubIdp

# After (documented clean setup from repository root)
dotnet tool restore && dotnet tool run libman restore
dotnet restore new/StubIdp.slnx && dotnet test new/StubIdp.slnx
```

Package pins: production `Sustainsys.Saml2` 2.11.0 / `JsonSchema.Net` 9.4.0; test stack xunit 2.9.3, runner 4.0.0, Test SDK 18.10.1, Moq 4.21.0, bUnit 2.11.3, MVC.Testing 10.0.12, coverlet.collector 10.1.0; LibraryManager CLI 3.0.114.

```csharp
// Unit suite smoke assertion
Assert.True(true);

// Integration suite clean-workflow assertion
using var response = await client.GetAsync("/");
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```
