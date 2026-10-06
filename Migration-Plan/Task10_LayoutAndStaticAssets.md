# Task10 — Layout, shared components, and static assets

## Goal and scope

Port the MVC shared layout and static assets to Blazor, replacing Less/jQuery-era helpers with CSS and small vanilla JavaScript.

## Implementation

1. Implement `Components/Layout/MainLayout.razor` and update `Components/App.razor` / router to provide navbar, tenant-aware navigation, page heading, footer, and page body. Keep the Google Analytics snippet only if the legacy layout contains it; make its enablement configurable as planned.
2. Port shared view partials to `Components/Shared/AboutCard.razor` and `MetadataLinks.razor`. Pass tenant GUID and URLs via parameters/resolver; tenant-specific links/menu must only render when `idpId` exists.
3. Convert `Content/Site.less` into plain CSS at `wwwroot/css/site.css`; copy `Content/Sustainsys.png` to `wwwroot/Sustainsys.png`. Add `wwwroot/js/site.js` for show/hide details and remember-user cookie read/write via `document.cookie`; no jQuery dependency.
4. Add `libman.json` with pinned normalize.css library and target `wwwroot/lib/normalize/`; restore using `libman restore` and verify the exact served path. Add Microsoft.Web.LibraryManager.Cli 3.0.114 as a local or documented dotnet tool. Do not reintroduce Select2, jQuery, ICanHaz, or js.cookie; Task11 replaces those UI features.
5. Reference site CSS, normalize CSS, and JS in the host document in deterministic order.

Before: `_Layout.cshtml` bundles Less, jQuery, Select2, and scripts; shared partials use MVC route helpers. After: Razor layout/components with normal static links and injected/resolved tenant URLs.

## Test phase

Add bUnit tests for `MainLayout` tenant navigation (no tenant menu without idpId; correct named route when supplied) and `AboutCard` metadata links from a stub URL resolver. Add integration tests asserting `/css/site.css`, `/lib/normalize/normalize.css`, `/js/site.js`, and `/Sustainsys.png` return 200 with appropriate CSS/JS/image content types. Run LibMan restore and `dotnet test new/StubIdp.slnx`.

## Output checklist

- No runtime references to Less, jQuery, Select2, or MVC partials remain.
- Static assets are rooted under `wwwroot` and resolve with the tested paths.

## Before/after and test sketches

```html
<!-- Before (MVC layout) -->
@Styles.Render("~/Content/css") @Scripts.Render("~/bundles/jquery")

<!-- After (new/src/Sustainsys.Saml2.StubIdp/Components/App.razor host document) -->
<link rel="stylesheet" href="/lib/normalize/normalize.css" />
<link rel="stylesheet" href="/css/site.css" />
<script src="/js/site.js"></script>
```

Package pins: `Microsoft.Web.LibraryManager.Cli` 3.0.114; no Select2/jQuery packages. Other project/test versions remain those pinned in Task01.

```csharp
// Unit (bUnit)
var cut = RenderComponent<MainLayout>(p => p.Add(x => x.IdpId, tenantId));
Assert.Contains($"/{tenantId:D}/Manage", cut.Markup);

// Integration
using var response = await client.GetAsync("/css/site.css");
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
Assert.Equal("text/css", response.Content.Headers.ContentType!.MediaType);
```
