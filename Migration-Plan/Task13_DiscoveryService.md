# Task13 — Discovery Service

## Goal and scope

Port Discovery Service UI and its redirect behavior, preserving URL composition (`?` versus `&`) and both route shapes.

## Implementation

Create `Components/Pages/DiscoveryService.razor` for `/DiscoveryService` and `/{idpId:guid}/DiscoveryService`. Bind `DiscoveryServiceModel` fields from query on GET: `isPassive`, `return`, `returnIDParam`, and `SelectedIdp`. Defaults are `returnIDParam=entityID` and `SelectedIdp` equal to the resolved metadata URL. Display entity ID and form controls on ordinary GET.

For GET `isPassive=true` and all POSTs, redirect to the `return` value with `returnIDParam=SelectedIdp`; append `&` when `return` already contains `?`, otherwise append `?`. Preserve the legacy string-composition behavior and do not silently normalize/encode away the consumer's supplied return URL. Choose a minimal API redirect endpoint for POST/passive branches if needed to guarantee exact status and Location; keep the page component responsible for the normal GET form. Document this split next to endpoint registration. If implementing static SSR redirect via `NavigationManager`, catch/allow its `NavigationException` only in the framework-supported SSR flow.

Before: MVC `DiscoveryServiceController.Index` checks `isPassive || POST`, composes delimiter, then redirects; ordinary GET returns the view. After: static SSR component handles ordinary GET while a minimal endpoint or documented static SSR redirect handles both redirect branches.

## Test phase

Add bUnit tests for the default form, entity ID display, and default `returnIDParam`/`SelectedIdp`. Integration tests assert:

- GET `/DiscoveryService` returns 200 and form.
- GET `?isPassive=true&return=https%3A%2F%2Fsp.example%2Fdiscovery` returns 302 with `Location` containing `?entityID=` and selected metadata URL.
- POST to default and named routes returns 302; a `return` URL already containing a query uses `&entityID=` rather than a second `?`.

Run `dotnet test new/StubIdp.slnx`; assert decoded Location parameters and also the raw delimiter behavior required for compatibility.

## Before/after and test sketches

```csharp
// Before (MVC controller composes redirect string)
var delimiter = model.return.Contains("?") ? "&" : "?";
return Redirect($"{model.return}{delimiter}{model.returnIDParam}={model.SelectedIdp}");

// After (new/src/Sustainsys.Saml2.StubIdp/Endpoints or SSR redirect handler)
var delimiter = returnUrl.Contains('?') ? "&" : "?";
return Results.Redirect($"{returnUrl}{delimiter}{returnIdParam}={selectedIdp}");
```

Package pins: no additional packages; use `bUnit` 2.11.3 for component tests and existing xUnit 2.9.3 / MVC.Testing 10.0.12 pins.

```csharp
// Unit/component
var cut = RenderComponent<DiscoveryService>(p => p.Add(x => x.Model, model));
Assert.Contains("entityID", cut.Markup);

// Integration
using var response = await client.GetAsync("/DiscoveryService?isPassive=true&return=https%3A%2F%2Fsp.example%2Fdiscovery");
Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
Assert.Contains("?entityID=", response.Headers.Location!.OriginalString);
```
