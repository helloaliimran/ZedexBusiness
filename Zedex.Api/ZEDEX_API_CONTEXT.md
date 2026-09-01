# Zedex.API — Project Context File

> High-level reference for future sessions. Read this instead of re-scanning source.
> Note: the folder/project is named **Zedex.Api** (the user often calls it "Zedex.API").

## What this is
A .NET 8 REST API that serves the **Zedex Mobile** app inside the larger **ZedexBusiness** solution — a sales & inventory management system (PVC pipes / aluminum hardware business). It is a thin API layer that shares the SAME database, DbContext, and application services as the existing **Zedex.Web** MVC app. It is NOT a separate backend.

## Solution layout (5 projects, .NET 8)
- **Zedex.Domain** — entities, enums (no dependencies).
- **Zedex.Application** — service interfaces, shared abstractions (`ICurrentUserService`, `PagedResult`).
- **Zedex.Infrastructure** — `AppDbContext`, EF migrations, Identity, seeding, shared services.
- **Zedex.Web** — MVC app (main desktop UI; has full posting/billing/stock logic).
- **Zedex.Api** — this REST API for mobile; references `Zedex.Infrastructure` (so it inherits Domain/Application). DbContext/Identity/services are reused as-is.

## Zedex.Api internals
- `Program.cs` — registers Npgsql `AppDbContext`, `AddIdentityCore<ApplicationUser>` (user validation only, **no cookie auth**), JWT Bearer auth, Swagger (root `/`), culture pinned to en-US, and runs EF migrations on startup. Swagger always shown.
- Auth = `AddIdentityCore` + JWT Bearer. `TokenService` issues short-lived access tokens (default 15 min) + long-lived refresh tokens (default 30 days).
- Refresh tokens: random 64-byte base64, stored **hashed (SHA-256)** in `RefreshTokens` table; rotation on refresh (old revoked, new issued); revoke on logout.
- Permissions: users' allowed modules are a **comma-separated int `modules` claim** in the JWT. `ClaimsPrincipalExtensions.HasModule(AppModule)` reads it. Users with no `UserPermission` row (admins) get all modules.
- `ApiCurrentUserService` reads the current user id/name from JWT claims (the Web variant uses cookies).

## Configuration
- `appsettings.json`: DB conn string `Zedex_PVC`; `Jwt` section (Secret/Issuer/Audience/expiries); Kestrel listens on `http://0.0.0.0:61815`.
- CORS policy **"MobileApp"** (allow any origin/header/method) — for the mobile client.

## API endpoints (base routes under `/api`)
- **Auth** (`/api/auth`): `login`, `refresh`, `logout` → JWT pair + `UserInfo` (FullName, Email, AllowedModules).
- **Bills** (`/api/bills`): GET list (paginated, filter by type standard|pvc, customerId, search, date range); GET by id or by invoice number; POST create **draft** bill; PUT edit draft. Drafts only — posting/stock/ledger changes are NOT part of these endpoints.
- **Customers** (`/api/customers`): GET list w/ closing balance (Opening + ΣDebit − ΣCredit); GET `{id}/ledger` paginated with running balances.
- **Lookups** (`/api/lookups`): GET all master-data (Colors, Gauges, Categories, Companies), each sorted by name.
- **Products** (`/api/products/search`): POST batch search — list of rows, each with optional Color/Gauge/Category/Company refinements; results grouped by row index.
- **Stock** (`/api/stock`): GET list (category + name search; PerFoot products include piece-length breakdown), GET `{id}` detail.
- **Tools** (`/api/tools`) — see "AI tool-calling layer" below.

## AI tool-calling layer (`/api/tools`)
A second, deliberately separate front door onto the same data, built for an external AI
agent doing tool calling (not the mobile app). One controller, one shared service —
intentionally NOT split into per-feature controllers/services like the mobile-app side,
to avoid file sprawl for something this small. All `[AllowAnonymous]` for now (no auth
layer yet — add one once the calling app has a way to supply credentials).
Standard (non-PVC) bills only; PVC is explicitly out of scope for tools.

- `Controllers/ToolCallingController.cs` — thin, one action per tool, all under `api/tools`.
- `Services/IToolCallingService.cs` / `ToolCallingService.cs` — all the logic.
- `DTOs/Tools/` — `ToolBillRequestDto`, `ToolBillResultDto`, `ToolCustomerLookupDto`, `ToolSaveBillResult`. Product-search and lookups reuse the existing `DTOs/Products` and `DTOs/Lookups` types as-is.

Four tools:
1. **search_product** — `POST /api/tools/products/search`, same batch "contains" search as the mobile Products screen (reuses `ProductSearchRequestDto`/`ProductSearchGroupDto`).
2. **lookup** — `GET /api/tools/lookups`, same as the mobile Lookups endpoint (Colors/Gauges/Categories/Companies).
3. **create_or_update_bill** — `POST /api/tools/bills`, body `ToolBillRequestDto`. Omit/zero `BillId` → create a new draft; set it → update that draft. Line semantics match `BillItemUpdateDto` (BillItemId null/0 = new line, set = update, missing-from-request = removed). Rejects PVC products, posted bills, and PVC-typed existing bills.
4. **find_customer** — `GET /api/tools/customers?search=`, name/phone "contains" match, max 20, alphabetical. Added specifically for the case where the caller doesn't have a CustomerId yet — resolve a name to an id before calling the bill tool. Omit `search` to browse (top 20 alphabetically).

**Known gotcha fixed here, not yet fixed on the mobile side:** `BillsController.UpdateBill`
recomputes invoice totals via `RecomputeStandardTotals` *before* `SaveChangesAsync` runs,
but removed lines' `IsDeleted` domain flag isn't actually set until `AppDbContext.ApplyAudit()`
converts the EF `Deleted` state during `SaveChangesAsync` itself — so a removed line's amount
was still being counted in the same request's recomputed total. `ToolCallingService.SaveBillAsync`
sets `old.IsDeleted = true` explicitly at removal time to sidestep this. A task has been flagged
to apply the same one-line fix to `BillsController.UpdateBill`.

## Business domain conventions (shared with Web app — important)
- **Two product/bill types**: Standard vs **PVC**, decided from product's Category (`Category.IsPvc`). A bill cannot mix them. PVC items have gas-kit, weight-per-length, sale-type fields; Standard items have per-foot cutting.
- **Pricing modes**: `PerUnit` vs `PerFoot`. PerFoot products tracked as stock pieces per length (StockPieces); selling N ft from a piece leaves remainder back in stock.
- **Ledger convention**: Debit ↑ what customer owes, Credit ↓ it. Balance = Opening + Σ(Debit − Credit); negative = advance held.
- **Numbering**: `INV-yyyyMMdd-####` (counts all invoices that day) and `PVC-yyyyMMdd-####` (counts only PVC) — per-day sequences with retry on duplicate race. Returns: `RET-yyyyMMdd-####`.
- **Soft deletes everywhere**: deletes become `IsDeleted` flags via global query filters.
- **Negative stock allowed**; flagged red. Local timestamps (legacy Npgsql timestamp behavior).

## Known WIP / gotchas in this API
- Several endpoints currently have `[AllowAnonymous]` or commented-out `HasModule`/`Forbid` gates (notably Bills create/update, Products search, bill-by-number GET) — safer to keep if the mobile app needs access; re-enable gating later.
- Bills endpoints only handle **drafts** (no stock/ledger impact); posting is not covered here.
- Do not start a second server assuming it updates the DSH Web GUI — that GUI is served separately by the harness, unrelated to this project.