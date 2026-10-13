using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Infrastructure.Persistence.Migrations
{
    // Adds a T-SQL stored procedure for the daily sales report.
    // No tables change, so the model snapshot stays the same.
    [DbContext(typeof(AppDbContext))]
    [Migration("20261013120000_AddDailySalesProcedure")]
    public partial class AddDailySalesProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_daily_sales
                    @fromDay date,
                    @toDay   date,
                    @offset  int    -- minutes from UTC to the restaurant's local time
                AS
                BEGIN
                    SET NOCOUNT ON;

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
                    ),
                    daily AS (
                        SELECT CAST(paid_local AS date) AS d,
                               COUNT(*) AS orders,
                               SUM(subtotal) AS gross,
                               SUM(discount) AS discounts,
                               SUM(tax) AS tax,
                               SUM(total) AS total
                        FROM order_totals
                        WHERE CAST(paid_local AS date) BETWEEN @fromDay AND @toDay
                        GROUP BY CAST(paid_local AS date)
                    )
                    SELECT CONVERT(char(10), d, 23) AS [Day],
                           orders AS [Orders],
                           gross AS [GrossCents],
                           discounts AS [DiscountCents],
                           tax AS [TaxCents],
                           total AS [TotalCents],
                           CAST(ROUND(CAST(total AS decimal(19, 4)) / orders, 0) AS bigint) AS [AvgOrderCents],
                           SUM(total) OVER (ORDER BY d ROWS UNBOUNDED PRECEDING) AS [RunningTotalCents]
                    FROM daily
                    ORDER BY d;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_daily_sales;");
        }
    }
}
