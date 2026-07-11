# Zedex Business — Sales & Inventory Management System

ASP.NET Core (.NET 8) MVC · PostgreSQL · EF Core · ASP.NET Identity (Admin/Worker roles with per-module permissions) · Bootstrap 5.

## Solution structure

```
ZedexBusiness.sln
├── Zedex.Domain          entities, enums (no dependencies)
├── Zedex.Application     service interfaces, shared abstractions (PagedResult, exports)
├── Zedex.Infrastructure  AppDbContext, migrations, Identity, seeding, export services
└── Zedex.Web             MVC controllers, Razor views, authorization policies
```

Packages: Npgsql.EntityFrameworkCore.PostgreSQL, ASP.NET Identity, ClosedXML (Excel), QuestPDF (PDF, community license).

## Prerequisites

- .NET 8 SDK
- PostgreSQL 14+ (connection string in `Zedex.Web/appsettings.json`)

## First-time setup

```bash
dotnet tool install --global dotnet-ef     # once per machine
dotnet restore
# Only if the Persistence/Migrations folder is empty:
dotnet ef migrations add InitialCreate -p Zedex.Infrastructure -s Zedex.Web -o Persistence/Migrations
dotnet run --project Zedex.Web             # migrations + seeding run at startup
```

Default admin: **admin / Admin@123** (change after first login).
Seeded lookups: Categories (Hardware, Aluminum), Colors (White, Black, Silver), Gauges (18, 20, 22).

## Modules

| Module | Highlights |
|---|---|
| Master data (admin) | Categories / Colors / Gauges CRUD; delete blocked while in use; recreating a deleted name restores it |
| Users & permissions (admin) | Worker/Admin accounts, activate/deactivate (kills sessions ≤5 min), password reset, 7 per-module toggles |
| Products | Per Unit / Per Foot pricing; pricing-mode locked while stock exists; combo duplicate check |
| Stock | Draft → **Post** workflow; units and cartons × items auto-totals; per-foot products tracked as pieces per length; bulk header + lines; attachments; posted deletes reverse quantities (admin) |
| Customers | Profile image, opening balance, live current balance; delete blocked once history exists |
| Billing | Draft → **Post** with payment prompt (Cash / Partial / Credit); per-line % discounts, editable (rounded) line totals with back-computed %, flat further discount; per-foot cutting (sell 10 ft from an 18 ft piece → 8 ft remainder returns to stock); previous / closing balance printed on bills; small + large invoice views |
| Customer ledger | Running balance, date filter, opening carried forward, manual Payment/Credit/Debit entries; corrections via **reversal (contra) entries** — no deletes, full audit trail |
| Sale returns | Against posted invoices; partial/repeat returns; stock restored at sold size; refund credited to ledger (`RET-yyyyMMdd-####`) |
| Dashboard | Today's sales/bills/cash/credit/partial/collection; receivables vs advances held; low-stock and recent-invoice panels; permission-aware quick actions |
| Reports | Customer Credit, Daily Bill, Daily Sales — search/filters/date range, print, **Excel + PDF export** respecting active filters |

## Business rules worth knowing

- **Ledger convention**: Debit increases what the customer owes; Credit decreases it. Balance = opening + Σ(debit − credit). Negative balance = advance held.
- **Advances**: post invoices as *Credit Sale* to net an advance automatically; Cash/Partial always mean *new money received now*. The post dialog shows the customer's live balance and warns when an advance exists.
- **Numbers**: `INV-yyyyMMdd-####` and `RET-yyyyMMdd-####`, per-day sequences with collision retry.
- **Soft deletes everywhere**: `SaveChanges` converts deletes to `IsDeleted` flags; global query filters hide them. Audit fields (CreatedBy/Date, UpdatedBy/Date) stamped automatically.
- **Negative stock is allowed** (overselling permitted); dashboards and lists flag it in red.
- **Timestamps** are local (single-timezone app, `Npgsql.EnableLegacyTimestampBehavior`); culture pinned to en-US so decimal binding is machine-independent.

## Migration history

Applied automatically at startup. If pulling fresh changes, the expected chain is:
`InitialCreate` → `StockPosting` → `InvoicePosting` → `LineItemDiscounts` → `DiscountPercent` → `FurtherDiscount`.

## Known trade-offs

- Concurrent posting of the *same* stock entry/invoice by two admins in the same instant isn't guarded by optimistic concurrency (acceptable at shop scale; add a `xmin` concurrency token if needed).
- Reports render unpaginated by design (print-friendly); exports always include the full filtered set.
