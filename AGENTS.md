# Agent instructions

Follow `.cursor/rules/constitution.mdc`. This file is how to build that system in this repo. If the two disagree, the constitution wins.

The solution file is `LexisNexis Tech Assessment.slnx`. `Orderly` is an existing net9.0 ASP.NET Core host (controllers, OpenAPI). Extend it. Remove the weather-forecast sample when the order API replaces it.

## Layout

```
Orderly/                      API host and composition root
Orderly.Domain/               orders, money, status rules
Orderly.Application/          submit, get, list, change status
Orderly.Infrastructure/       JSON file repository
Orderly.Tests/                xUnit tests
client/                       Angular 18+ app
```

- Domain references nothing in this solution.
- Application references Domain and defines the repository port.
- Infrastructure references Application and implements the port.
- `Orderly` references Application and Infrastructure only to register services.
- `Orderly.Tests` references Application, Domain, and Infrastructure.

Add each project to the `.slnx` file.

## Data model

`Order`

- `Id` (GUID, server-generated)
- `ExternalReference` (required, trimmed, max 64 characters)
- `CustomerName` (required, trimmed, max 200 characters)
- `Lines` (at least one)
- `Total` (sum of line totals, 2 decimal places)
- `Status` (`Submitted` on create)
- `CreatedAtUtc`

`OrderLine`

- `Description` (required, trimmed, max 200 characters)
- `Quantity` (integer ≥ 1)
- `UnitPrice` (decimal ≥ 0)
- `LineTotal` = `Quantity * UnitPrice`, rounded to 2 decimal places with `MidpointRounding.AwayFromZero` before the order total is summed

Two submissions are equivalent when, after trim and price rounding, the customer name and the line sequence match. Line order is part of the payload.

## HTTP API

Base path: `/api/orders`. Use `ProblemDetails` with a `code` extension.

| Action | Request | Success | Failure |
| --- | --- | --- | --- |
| Create | `POST /api/orders` | `201` + `Location` the first time | `400` `validation_error` |
| Repeat, equivalent | same body, same reference | `200` and the stored order | — |
| Repeat, different body | same reference, different content | — | `409` `duplicate_reference`; stored order unchanged |
| Get one | `GET /api/orders/{id}` | `200` | `404` `order_not_found` |
| List | `GET /api/orders` | `200` array, `CreatedAtUtc` descending, then `Id` descending | — |
| Change status | `PATCH /api/orders/{id}/status` body `{ "status": "Confirmed" }` | `200` and the updated order | `409` `invalid_status_transition`; `404` if missing |

Allowed transitions:

- `Submitted` → `Confirmed`, `Cancelled`
- `Confirmed` → `Fulfilled`, `Cancelled`
- `Fulfilled` → none
- `Cancelled` → none

Illegal transition detail shape:

```text
Order {id} is Fulfilled. No further status changes are allowed.
```

```text
Order {id} is Submitted. You can change it to Confirmed or Cancelled.
```

Create request (client totals are not part of the contract):

```json
{
  "externalReference": "PO-1001",
  "customerName": "Northwind",
  "lines": [
    { "description": "Binder", "quantity": 2, "unitPrice": 3.5 }
  ]
}
```

The response always includes server `lineTotal`, `total`, `status`, `id`, and `createdAtUtc`.

Reference matching is ordinal and case-sensitive after trim. `PO-1001` and `po-1001` are different references.

## File storage

- Config key `Storage:OrdersFile`, default `data/orders.json` under the API content root.
- Create the directory if it is missing.
- Hold a lock across read-modify-write.
- Write a temp file, then replace the target so a crash does not leave a half-written store.
- An empty or missing file means there are no orders.

## Backend behavior to preserve

- Controllers map HTTP only. Totals, idempotency, and status rules live in Domain or Application.
- A failed validation or conflict writes nothing.
- List order is fixed by the query. Do not rely on file order.
- Register the repository in `Program.cs`. Keep OpenAPI enabled in Development.

## SOLID, DRY, and sanitation

Put each rule in the type that owns it:

| Concern | Owner |
| --- | --- |
| Line and order totals | Domain order |
| Allowed status changes and the user-facing message | Domain status policy |
| Idempotent submit and list order | Application service |
| JSON file IO | Infrastructure repository |
| Status codes and `ProblemDetails` | One API mapper |

- **Single responsibility.** A controller action calls one application method and maps the result. It does not calculate money or decide transitions.
- **Open/closed.** Add a status by changing the status policy, so the controller and repository stay stable.
- **Liskov.** The file repository and the test double both honor `IOrderRepository`: a missing file is an empty set, and a failed save leaves the previous file intact.
- **Interface segregation.** `IOrderRepository` exposes only what submit, get, list, and status change need. Keep a separate clock abstraction if tests must set `CreatedAtUtc`.
- **Dependency inversion.** Application code takes interfaces. `Program.cs` is the only place that constructs the file repository.

DRY in practice:

- One function rounds money to 2 decimal places.
- One map of allowed transitions produces both the decision and the error text.
- Share max lengths between validation and DTOs as named constants.
- One Angular service owns every `/api/orders` call. One function builds the create body.

Sanitation:

- Enable nullable reference types on every project. Treat new nullable warnings as defects.
- Prefer guard clauses and domain constructors that reject invalid orders.
- Use an `OrderStatus` enum. Map it at the API boundary.
- Keep methods short enough to test without a harness of mocks.
- Use `async` for file IO and HTTP. Propagate cancellation tokens from the controller.
- Delete the weather-forecast sample, unused usings, and commented-out blocks.
- Name types for the business (`SubmitOrder`, `ChangeOrderStatus`), and match Angular names to the same language.

## Transport security and API hardening

Order data between the browser and the API is encrypted with TLS.

- Run the API with the `https` launch profile: `https://localhost:7222`, with `http://localhost:5170` redirecting to HTTPS. Keep `UseHttpsRedirection`.
- Add HSTS when the environment is not Development.
- Set the Angular `apiUrl` to `https://localhost:7222`. The client has no HTTP API URL.
- Trust the local development certificate for browser calls (`dotnet dev-certs https --trust` when the browser rejects the cert).

Also put these controls in place:

- CORS allows only the Angular dev origin (and a configured production origin). Credentials and wildcard origins stay off.
- `[ApiController]` plus explicit request DTOs. Ignore extra JSON properties. A client `total` or `lineTotal` is not bound.
- Cap request bodies (orders are small; 64 KB is enough) and require `application/json` on `POST` and `PATCH`.
- Validate every bound field with the limits in the data model. Invalid JSON and unknown status values return `400` `validation_error`.
- `ProblemDetails` in non-Development includes `title`, `status`, `detail`, and `code`. It excludes `exception`, stack traces, and file paths.
- The store path comes from `Storage:OrdersFile` and stays under the content root. A request cannot choose the path.
- Do not enable static files for `data/`. The JSON file is readable only by the API process.
- Add `X-Content-Type-Options: nosniff` and `Referrer-Policy: no-referrer`.
- Log the order id and the failure `code`. Leave customer names, references, and prices out of logs.
- Keep secrets out of source. `appsettings.json` holds the file path and CORS origin, nothing else sensitive.

Angular:

- Bind order fields with interpolation or property binding so sanitization stays on. Do not use `bypassSecurityTrustHtml` or `innerHTML` for order data.
- Send the create payload over `HttpClient` to the HTTPS `apiUrl` only.

## Tests

`Orderly.Tests` uses xUnit. Cover at least:

- Line total and order total, including rounding to 2 decimal places.
- Quantity `0`, negative, or fractional is rejected. Negative price is rejected. Zero price is kept.
- Missing reference, customer, or lines is rejected.
- First submit stores one order with `Submitted`, a new id, and server totals.
- Equivalent resubmit returns that same id and leaves a single stored order.
- Different body with the same reference does not insert or mutate.
- List returns newest `CreatedAtUtc` first.
- Each allowed transition updates status. Each illegal one returns the helpful message and leaves status unchanged.
- HTTPS redirection is enabled. A non-development pipeline adds HSTS.
- CORS rejects an origin that is not the configured Angular origin.
- A response for a failed request has no stack trace or internal file path.
- A create body that includes `total` still stores the server-calculated total.

Prefer tests against application services with a fake or the real file repository pointed at a temp path. Add an API test only when HTTP mapping itself needs proof.

## Angular client

Create `client/` with Angular 18 or newer (`ng version` must report 18+). Use standalone components and `provideHttpClient`. Do not add NgModules.

One screen is enough:

- Form to submit `externalReference`, `customerName`, and lines (`description`, `quantity`, `unitPrice`).
- List bound to `GET /api/orders` in the returned order (newest first).
- Detail for the selected order showing each line total, the order total, and status.

Point `HttpClient` at `https://localhost:7222` through an environment file. Enable CORS on the API for that Angular dev origin only.

The form must not send totals. After a successful `201` or idempotent `200`, refresh the list and show the returned order so the user can confirm totals and status.

Optional, only after the flow above works: a status control that calls `PATCH` and shows the API error detail, or a simple client-side text filter on the loaded list.

Add a focused client spec for the create payload (no total fields) if the form mapping lives in a function or service.

## Out of scope

Authentication, users, payments, inventory, email, and any database. Do not add them.
