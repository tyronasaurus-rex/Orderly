# Orderly

A small purchase-order app: submit an order, list orders, view one order, and change its status. The ASP.NET Core API stores orders in a local JSON file. The Angular client talks to that API over HTTPS.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js](https://nodejs.org/) 18.19.1, 20.11.1, or 22+
- npm (comes with Node.js)

## Run locally

Use two terminals from the repository root.

### 1. Trust the local HTTPS certificate

The browser must trust the ASP.NET development certificate or API calls from the client will fail.

```powershell
dotnet dev-certs https --trust
```

### 2. Start the API

```powershell
dotnet run --project Orderly --launch-profile https
```

- API: https://localhost:7222
- HTTP on http://localhost:5170 redirects to HTTPS
- Orders are written to `Orderly/data/orders.json` (created on first save)
- OpenAPI is available in Development at https://localhost:7222/openapi/v1.json

### 3. Start the Angular client

```powershell
cd client
npm install
npm start
```

Open http://localhost:4200. The client calls `https://localhost:7222` (`client/src/environments/environment.ts`). CORS on the API allows that origin only.

Submit an order from the form. The list shows the newest order first, and the detail pane shows server-calculated line totals, the order total, and status.

## Tests

```powershell
dotnet test "LexisNexis Tech Assessment.slnx"
```

Client payload spec:

```powershell
cd client
npm test
```
