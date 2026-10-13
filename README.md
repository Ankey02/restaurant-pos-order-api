# Restaurant POS & Order Management API

A REST API for a restaurant point-of-sale: menu, table orders, kitchen hand-off, payment (cash/card, tax, discount, change) and sales reports. Built with **C# / .NET 9**, **Microsoft SQL Server** and **EF Core**, with hand-written **T-SQL reports (window functions, CTEs and a stored procedure)** through Dapper.

> This is the `sql-server` branch. The original PostgreSQL version is on `main`, so the same system runs on both databases.

## Features

- **Menu**: create items, change price, mark available/unavailable, filter by category
- **Orders**: open an order for a table, add/remove lines, apply a discount, send to kitchen, pay, cancel
- **Payment**: card must match the total; cash must cover it and the change is calculated
- **Reports** (T-SQL): daily sales with running total (stored procedure `dbo.usp_daily_sales`), top-selling items ranked by revenue, sales by hour with share of total
- **Order audit trail**: a SQL Server **trigger** (`trg_orders_status_audit`) logs every status change; `GET /orders/{id}/history` returns it
- **MVC admin page** (`/admin/menu`): ASP.NET MVC with Razor views, model validation and anti-forgery; **jQuery AJAX** for price changes and availability toggles
- **Clear errors**: RFC 7807 `ProblemDetails` with proper status codes (404, 409, 422, 500)
- **52 automated tests**, including integration tests against a real SQL Server (Testcontainers)

## Architecture

Clean Architecture: dependencies point inwards, the domain knows nothing about the database or HTTP.

```mermaid
flowchart LR
    Client[Client / Scalar UI] --> Api
    subgraph Solution
        Api[Api<br/>controllers, error handling] --> App[Application<br/>use cases, DTOs]
        App --> Dom[Domain<br/>Order, MenuItem, rules]
        Infra[Infrastructure<br/>EF Core, Dapper reports] --> App
    end
    Infra --> DB[(SQL Server)]
```

- **Commands** (write side) go through EF Core and the domain model, so every rule is enforced in one place (`Order`).
- **Queries and reports** (read side) project straight to DTOs or run plain SQL; they never load aggregates just to display them.

## Run it (Docker)

```bash
docker compose up -d --build
```

- Menu admin (ASP.NET MVC + jQuery): http://localhost:5090/admin/menu
- API + Swagger-style UI (Scalar): http://localhost:5090/scalar/v1
- Health check: http://localhost:5090/health
- SQL Server is exposed on port 1434 (user `sa`, password `Pos_Strong_Pw1!`, local demo only); connect with SSMS or Azure Data Studio using server `localhost,1434`
- On Apple Silicon Macs, turn on "Use Rosetta for x86/amd64 emulation" in Docker Desktop settings (the SQL Server image is x86 only)

The app applies its database migrations automatically on startup.

Optional: add synthetic demo sales so the reports have data (about 300 made-up paid orders over 14 days):

```bash
docker compose cp scripts/seed-demo-data.sql db:/tmp/seed.sql
docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Pos_Strong_Pw1!" -C -d restaurant -i /tmp/seed.sql
```

Then try, for India time: `GET /reports/daily-sales?utcOffsetMinutes=330`

## API overview

| Method | Path | What it does |
|---|---|---|
| GET / POST | `/menu` | List (optional `?category=`) / create a menu item |
| GET | `/menu/{id}` | One menu item |
| PUT | `/menu/{id}/price` | Change price |
| PUT | `/menu/{id}/availability` | Mark available / unavailable |
| POST | `/orders` | Open an order for a table |
| GET | `/orders`, `/orders/{id}` | Active orders / one order with totals |
| POST | `/orders/{id}/lines` | Add an item (same item merges, quantity 1 to 99) |
| DELETE | `/orders/{id}/lines/{menuItemId}` | Remove an item |
| PUT | `/orders/{id}/discount` | Discount 0 to 100 percent |
| POST | `/orders/{id}/send` | Send to kitchen (locks the order) |
| POST | `/orders/{id}/pay` | Pay with Cash or Card |
| POST | `/orders/{id}/cancel` | Cancel with a reason |
| GET | `/reports/daily-sales` | Orders, gross, discount, tax, total, average, running total per day |
| GET | `/reports/top-items` | Items ranked by revenue (`RANK()`), with percentage share |
| GET | `/reports/sales-by-hour` | Sales per hour of day with share of total |
| GET | `/orders/{id}/history` | Status changes recorded by the audit trigger |
| GET / POST | `/admin/menu` | MVC admin page (HTML): list and add menu items |

Reports take `fromDate`, `toDate` (default: last 30 days) and `utcOffsetMinutes` (for example `330` for India) so sales fall into the restaurant's local day and hour.

## Design decisions

- **Money is integer cents** (`long`), tax in **basis points** (500 = 5%). Rounding is half-up using integer maths only, so there are no floating-point errors. The SQL reports use the same formulas as the C# code, and the tests check that they agree.
- **One active order per table** is enforced by the database with a **filtered unique index** (`[status] IN (N'Open', N'SentToKitchen')`), not only in code, so two waiters can't open the same table at the same time.
- **Optimistic concurrency** with a SQL Server `rowversion` column: if two people change the same order at once, the second gets a **409** instead of silently overwriting.
- **Price snapshot**: an order line stores the item name and price at the time of sale, so changing the menu later never alters old orders or reports.
- **Business rules live in the domain**: you can't pay an empty order, add items after it was sent, or pay less than the total in cash. These return **422**.
- **Hand-written T-SQL for reports**: aggregates with window functions (`RANK() OVER`, running `SUM() OVER`, share of total) are clearer and faster as SQL than as LINQ. The daily report is a **stored procedure** created by an EF Core migration; the other two are parameterised queries.
- **UTC everywhere**: times are stored in `datetime2` as UTC, and a value converter marks them as UTC again when read.
- **Audit trigger in the database**: `AFTER INSERT, UPDATE` on `orders`, set-based on the `inserted` / `deleted` tables, so the history is complete even for changes made outside the API (scripts, SSMS). The table is declared with `HasTrigger` so EF Core saves without an `OUTPUT` clause, which SQL Server does not allow on tables with triggers.
- **MVC next to the API**: the admin page uses the same Application layer (`MenuCommands`, `MenuQueries`) as the REST controllers. Adding an item is a server-side form post (validation, anti-forgery token, Post/Redirect/Get); price and availability changes call the REST API with jQuery `$.ajax` and update the row without a reload.
- `TimeProvider` is injected, so time-dependent logic can be tested.

## Tests

```bash
dotnet test
```

52 tests (Docker must be running for the integration tests):

- **32 unit tests** for the domain: totals, rounding, discount, payment, state rules
- **20 integration tests** that start a real SQL Server 2022 in Docker (Testcontainers) and call the API: the full open, add, send, pay flow, the 404 / 409 / 422 error cases, table reuse after payment, and the three reports checked against **hand-calculated numbers**, including the UTC offset moving a sale into the next local day, the audit trigger history, and the MVC page (list, valid and invalid form posts, missing anti-forgery token)

## PostgreSQL vs SQL Server: what changed

| | PostgreSQL (`main`) | SQL Server (`sql-server`) |
|---|---|---|
| EF Core provider | `UseNpgsql` | `UseSqlServer` |
| Concurrency token | `xmin` system column | `rowversion` column |
| One active order per table | partial unique index | filtered unique index |
| Local time in reports | `AT TIME ZONE` + `make_interval` | `DATEADD(MINUTE, @offset, ...)` |
| Limiting rows | `LIMIT` | `TOP (@limit)` |
| Daily report | inline query | stored procedure |
| Test database | Testcontainers.PostgreSql | Testcontainers.MsSql |

## Project layout

```
src/
  RestaurantPos.Domain          entities and business rules
  RestaurantPos.Application     use cases, DTOs, interfaces
  RestaurantPos.Infrastructure  EF Core, migrations (incl. stored procedure), Dapper reports
  RestaurantPos.Api             API + MVC controllers, Razor views, error handling, Program.cs
tests/RestaurantPos.Tests       unit + integration tests
scripts/seed-demo-data.sql      synthetic demo data
```

## Limitations

- No authentication or user roles yet (a real POS would need staff login and permissions).
- Payment records what was tendered; it does not talk to a real card processor.
- One restaurant and one flat tax rate (from configuration).
- Demo data in `scripts/seed-demo-data.sql` is synthetic.
