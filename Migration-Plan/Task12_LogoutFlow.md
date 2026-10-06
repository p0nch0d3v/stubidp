# Task12 — Logout flow: receive, initiate, and respond

## Goal and scope

Port logout receive screens as static SSR and protocol emissions as minimal API endpoints, with both default and GUID-prefixed routes.

## Implementation

Create `Components/Pages/Logout.razor` for `/Logout` and `/{idpId:guid}/Logout`. Access the current `HttpContext` via cascading parameter and adapt it with `ToHttpRequestData`. When there is no SAML message, render `InitiateLogoutForm` with values bound from query fields (legacy `TryUpdateModel` behavior). For `LogoutRequest`, unbind and render `RespondToLogoutPanel` with pretty XML, InResponseTo, RelayState from query/form, and destination `new Uri(new Uri(issuer + "/"), "Logout")`. For `LogoutResponse`, render `ReceivedLogoutResponsePanel` with status and pretty XML. Unknown message XML must fail with HTTP 500 to preserve legacy behavior.

Create components under `Components/Logout/`. Keep all displayed XML encoded by Razor; do not use raw markup. Models remain DataAnnotations-compatible.

Map `POST /Logout/InitiateLogout`, `POST /{idpId:guid}/Logout/InitiateLogout`, `POST /Logout/RespondToLogoutRequest`, and `POST /{idpId:guid}/Logout/RespondToLogoutRequest`. Read and validate posted models, construct request/response with injected model services, bind using HttpRedirect, and return `CommandResult.ToResult()`.

Before: `LogoutController.Index` unbinds and selects one of three MVC views; separate POST actions call `.ToActionResult()`. After: one static SSR component selects one of three child panels and four minimal API routes emit Redirect binding responses.

## Test phase

Add bUnit tests verifying all three panels render their supplied values (InResponseTo, RelayState, DestinationUrl, status, and XML). Integration tests should generate real Redirect-binding messages:

- GET `/Logout?SAMLRequest=...` with LogoutRequest displays InResponseTo.
- GET with LogoutResponse displays success status.
- POST InitiateLogout returns 302 with `SAMLRequest` in Location.
- POST RespondToLogoutRequest returns 302 with `SAMLResponse` and `RelayState` in Location.
- Verify named GUID route variants and unknown XML -> 500.

Run `dotnet test new/StubIdp.slnx`; assert semantic query values rather than ordering except where legacy explicitly requires ordering.

## Before/after and test sketches

The snippet below is illustrative pseudo-code for panel selection; real rendering uses Razor
components rather than returned render values.

```csharp
// Before (MVC action returns a named view)
return View("RespondToLogout", model);

// After (new/src/Sustainsys.Saml2.StubIdp/Components/Pages/Logout.razor)
return messageName switch
{
    "LogoutRequest" => RenderRespondToLogout(model),
    "LogoutResponse" => RenderReceivedLogoutResponse(model),
    _ => throw new InvalidOperationException()
};
```

Package pins: no additional runtime packages; use Sustainsys.Saml2 2.11.0, bUnit 2.11.3, and the Task01 xUnit/integration test pins.

```csharp
// Unit/component
var cut = RenderComponent<RespondToLogoutPanel>(p => p.Add(x => x.Model, model));
Assert.Contains(model.InResponseTo, cut.Markup);

// Integration
using var response = await client.GetAsync("/Logout?SAMLResponse=" + encodedResponse);
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```
