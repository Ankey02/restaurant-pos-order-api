using Dapper;
using System.Data;
using Microsoft.Data.SqlClient;
using RestaurantPos.Application.Reports;

namespace RestaurantPos.Infrastructure.Reports;

// Reports are hand-written T-SQL (Dapper) because they are read-only and
// aggregate-heavy: window functions and CTEs are clearer here than LINQ.
// Money is integer cents; discount and tax use the same half-up integer
// rounding as the C# Order class, so reports always match the receipts.
public class ReportQueries(string connectionString) : IReportQueries
{
    // Shared CTEs (T-SQL): one row per PAID order with its computed totals.
    // @offset shifts UTC timestamps to the restaurant's local time (minutes).
    private const string OrderTotalsCte = """
        WITH line_totals AS (
            SELECT order_id, SUM(unit_price_cents * quantity) AS subtotal
            FROM order_lines
            GROUP BY order_id
        ),
        priced AS (
            SELECT DATEADD(MINUTE, @offset, o.paid_at) AS paid_local,
                   lt.subtotal,
                   (lt.subtotal * o.discount_percent + 50) / 100 AS discount,
                   CAST(o.tax_rate_basis_points AS bigint) AS bp
            FROM orders o
            JOIN line_totals lt ON lt.order_id = o.id
            WHERE o.status = N'Paid'
        ),
        order_totals AS (
            SELECT paid_local, subtotal, discount,
                   ((subtotal - discount) * bp + 5000) / 10000 AS tax,
                   subtotal - discount + ((subtotal - discount) * bp + 5000) / 10000 AS total
            FROM priced
        )
        """;

    // Daily sales live in a stored procedure (dbo.usp_daily_sales, created by a migration).
    private const string DailySalesProcedure = "dbo.usp_daily_sales";

    private const string HourlySql = OrderTotalsCte + """
        ,
        hourly AS (
            SELECT DATEPART(HOUR, paid_local) AS hr,
                   COUNT(*) AS orders,
                   SUM(total) AS total
            FROM order_totals
            WHERE CAST(paid_local AS date) BETWEEN CAST(@fromDay AS date) AND CAST(@toDay AS date)
            GROUP BY DATEPART(HOUR, paid_local)
        )
        SELECT hr AS [Hour],
               orders AS [Orders],
               total AS [TotalCents],
               CAST(ROUND(total * 100.0 / SUM(total) OVER (), 1) AS decimal(5, 1)) AS [SharePercent]
        FROM hourly
        ORDER BY hr
        """;

    // Item revenue here is line revenue (price x quantity) before order-level discount and tax.
    private const string TopItemsSql = """
        WITH item_sales AS (
            SELECT l.item_name,
                   SUM(CAST(l.quantity AS bigint)) AS qty,
                   SUM(l.unit_price_cents * l.quantity) AS revenue
            FROM order_lines l
            JOIN orders o ON o.id = l.order_id
            WHERE o.status = N'Paid'
              AND CAST(DATEADD(MINUTE, @offset, o.paid_at) AS date)
                  BETWEEN CAST(@fromDay AS date) AND CAST(@toDay AS date)
            GROUP BY l.item_name
        )
        SELECT TOP (@limit)
               CAST(RANK() OVER (ORDER BY revenue DESC) AS int) AS [Rank],
               item_name AS [ItemName],
               qty AS [QuantitySold],
               revenue AS [RevenueCents],
               CAST(ROUND(revenue * 100.0 / SUM(revenue) OVER (), 1) AS decimal(5, 1)) AS [SharePercent]
        FROM item_sales
        ORDER BY [Rank], item_name
        """;

    public async Task<IReadOnlyList<DailySalesRow>> DailySalesAsync(
        DateOnly from, DateOnly to, int utcOffsetMinutes, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<DailySalesRow>(new CommandDefinition(
            DailySalesProcedure, Args(from, to, utcOffsetMinutes),
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TopItemRow>> TopItemsAsync(
        DateOnly from, DateOnly to, int utcOffsetMinutes, int limit, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<TopItemRow>(new CommandDefinition(
            TopItemsSql,
            new { fromDay = Day(from), toDay = Day(to), offset = utcOffsetMinutes, limit },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<HourlySalesRow>> HourlySalesAsync(
        DateOnly from, DateOnly to, int utcOffsetMinutes, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = await conn.QueryAsync<HourlySalesRow>(new CommandDefinition(
            HourlySql, Args(from, to, utcOffsetMinutes), cancellationToken: ct));
        return rows.ToList();
    }

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static object Args(DateOnly from, DateOnly to, int offset) =>
        new { fromDay = Day(from), toDay = Day(to), offset };
}
