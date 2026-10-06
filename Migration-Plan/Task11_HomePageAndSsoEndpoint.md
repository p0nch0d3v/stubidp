# Task11 — Home page and SSO protocol endpoint

## Goal and scope

Implement home UI and SSO send/receive behavior while splitting interactive form submission from non-circuit protocol requests. External SP POSTs must not require antiforgery tokens.

## Implementation

### Page and interactive island

Create `Components/Pages/Home.razor` at `/` and `/{idpId:guid}` as static SSR. Construct `HomePageModel` from options, apply tenant config from `ITenantConfigurationStore` (ACS, audience, description, NameId reset, HideDetails), and unbind Redirect-binding AuthnRequest from request query using `HttpContext.ToHttpRequestData(...)`. Populate `InResponseTo`, ACS override, RelayState, issuer audience, and pretty AuthnRequest XML; clear model errors for inbound protocol messages. Render `AssertionForm.razor` as `@rendermode InteractiveServer`, plus details and `AboutCard`. Preserve the kentor.se migration and HTTP-to-HTTPS information banners' legacy conditions.

`AssertionForm.razor` uses `EditForm`, `DataAnnotationsValidator`, dynamic `AttributeStatementModel` rows (add/remove), and a tenant user picker using stored configuration. Selecting a user fills NameId and associated attributes. Use JS interop for remembered NameId cookie and optional `document.write` only for auto-post binding. Redirect binding navigates to the library-generated Location using `NavigationManager.NavigateTo(location, forceLoad: true)`. POST/Artifact binding writes the library-generated auto-submit HTML without rewriting its payload.

### Protocol POST endpoint

Map `POST /` and `POST /{idpId:guid}`. Read `Request.Form` manually; do not attach antiforgery middleware/metadata because external SAML SPs submit directly. If form contains `SAMLRequest`, unbind it as a POST-binding AuthnRequest and return `RazorComponentResult<Home>` with prefilled values. Otherwise bind fields to the form model, validate DataAnnotations server-side, and either return `binding.Bind(response).ToResult()` or a `RazorComponentResult<Home>` with errors. Make the Home component parameters/cascading values sufficient to render validation without relying on a live circuit.

Before: MVC GET and POST share `HomeController.Index`; POST model binding distinguishes SAML receive from response form. After: static SSR component handles GET, minimal POST endpoint explicitly branches on `SAMLRequest` and form validity.

## Test phase

Add bUnit tests for default/pre-filled fields, add/remove attribute statements, selecting a user, DataAnnotations validation preventing invalid submit, and HideDetails behavior. Integration tests:

- GET `/` and `/{guid}` return 200.
- GET with deflated Redirect `SAMLRequest` prefills InResponseTo, ACS, RelayState, audience.
- POST form containing `SAMLRequest` returns 200 and prefilled page.
- Valid redirect-binding response POST returns 302 and `Location` contains `SAMLResponse`.
- Valid POST-binding response returns auto-post HTML whose form action is the ACS URL and contains `SAMLResponse`.
- Invalid response form returns 200 with validation text; both route variants are exercised.

Use actual Sustainsys binding APIs to build/deflate requests and decode responses; avoid brittle assertions against generated IDs. Run `dotnet test new/StubIdp.slnx`.

## Before/after and test sketches

```csharp
// Before (MVC GET/POST share one controller action)
if (ModelState.IsValid) return binding.Bind(model.ToSaml2Response()).ToActionResult();
return View(model);

// After (new/src/Sustainsys.Saml2.StubIdp)
app.MapPost("/", HandleExternalSamlPost);
app.MapPost("/{idpId:guid}", HandleExternalSamlPost);
// HandleExternalSamlPost branches on form.ContainsKey("SAMLRequest"), then returns IResult or RazorComponentResult<Home>.
```

Package pins: `Sustainsys.Saml2` 2.11.0, `bUnit` 2.11.3, and integration `Microsoft.AspNetCore.Mvc.Testing` 10.0.12; remaining test versions are from Task01.

```csharp
// Unit/component
var cut = RenderComponent<AssertionForm>(p => p.Add(x => x.Model, prefillingModel));
Assert.Contains(prefillingModel.NameId, cut.Markup);

// Integration
using var response = await client.GetAsync("/");
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```
