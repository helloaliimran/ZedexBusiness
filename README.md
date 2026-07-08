# Zedex Business — Sales & Inventory Management System

ASP.NET Core (.NET 8) MVC + PostgreSQL + EF Core + Identity (Admin/Worker roles with per-module permissions).

## Solution structure

```
ZedexBusiness.sln
├── Zedex.Domain          entities, enums (no dependencies)
├── Zedex.Application     service interfaces, shared abstractions (PagedResult, etc.)
├── Zedex.Infrastructure  AppDbContext, migrations, Identity, seeding, services
└── Zedex.Web             MVC controllers, Razor views (Bootstrap 5), authorization policies
```

## Prerequisites

- .NET 8 SDK — https://dotnet.microsoft.com/download/dotnet/8.0
- PostgreSQL 14+ running locally (default connection: `Host=localhost;Database=zedex;Username=postgres;Password=postgres` — change in `Zedex.Web/appsettings.json`)

## First-time setup

```bash
# 1. Install the EF tool (once per machine)
dotnet tool install --global dotnet-ef

# 2. Restore packages
dotnet restore

# 3. Generate the initial migration (once — commit the generated files)
dotnet ef migrations add InitialCreate -p Zedex.Infrastructure -s Zedex.Web -o Persistence/Migrations

# 4. Run — migrations apply and data seeds automatically at startup
dotnet run --project Zedex.Web
```

Default admin login: **admin** / **Admin@123** (change after first login).
Seeded master data: Categories (Hardware, Aluminum), Colors (White, Black, Silver), Gauges (18, 20, 22).

## Architecture notes

- **Audit + soft delete**: every business entity inherits `BaseEntity` (CreatedBy/CreatedDate/UpdatedBy/UpdatedDate/IsDeleted). `AppDbContext` stamps audit fields on save and converts hard deletes to soft deletes; global query filters hide soft-deleted rows.
- **Permissions**: Admins bypass all checks. Workers hold a `UserPermission` row with per-module toggles; controllers use `[Authorize(Policy = Policies.For(AppModule.X))]`. The sidebar renders only granted modules.
- **Timestamps**: `Npgsql.EnableLegacyTimestampBehavior` is on — this is a single-timezone business app, so local times are stored as-is (simpler day-based reporting).
- **Stock model**: `Product.CurrentStock` caches totals (units, or total feet for per-foot products). Per-foot products additionally track a per-length breakdown in `StockPiece` — selling 10 ft cut from an 18 ft piece decrements the 18 ft row and adds an 8 ft remainder piece. Negative stock is allowed (business rule).
- **Ledger**: single `LedgerEntry` table; Debit increases what the customer owes, Credit decreases it. Payments and returns are ledger entries — one source of truth for balances.
- **Money**: PKR; all decimals are `numeric(18,2)`. Invoice numbers: `INV-yyyyMMdd-####`; returns: `RET-yyyyMMdd-####`.

## Build phases

1. ✅ Scaffold: solution, EF Core + PostgreSQL, Identity + role seeding, base layout
2. Master data (Category / Color / Gauge CRUD, admin-only)
3. User & permission management
4. Product management
5. Stock management (units, cartons, lengths, bulk entry, attachments)
6. Customer management
7. Billing (invoices, cutting logic, payment methods)
8. Customer ledger (+ sale returns)
9. Dashboard
10. Reports (Excel/PDF export)
11. Polish
