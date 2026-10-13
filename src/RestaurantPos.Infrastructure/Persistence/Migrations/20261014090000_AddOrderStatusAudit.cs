using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Infrastructure.Persistence.Migrations
{
    // Adds an audit table and a T-SQL trigger that records every order status change
    // (Open -> SentToKitchen -> Paid / Cancelled) inside the database itself, so the
    // history is complete even when rows are changed outside the API (scripts, SSMS).
    // The audit table is read with Dapper and is not part of the EF model,
    // so the model snapshot only gains the trigger declaration on "orders".
    [DbContext(typeof(AppDbContext))]
    [Migration("20261014090000_AddOrderStatusAudit")]
    public partial class AddOrderStatusAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE dbo.order_status_audit (
                    id          bigint IDENTITY(1, 1) NOT NULL CONSTRAINT pk_order_status_audit PRIMARY KEY,
                    order_id    uniqueidentifier NOT NULL
                                CONSTRAINT fk_order_status_audit_orders REFERENCES dbo.orders (id) ON DELETE CASCADE,
                    old_status  nvarchar(20) NULL,
                    new_status  nvarchar(20) NOT NULL,
                    changed_at  datetime2 NOT NULL CONSTRAINT df_order_status_audit_changed_at DEFAULT SYSUTCDATETIME()
                );
                """);

            migrationBuilder.Sql(
                "CREATE INDEX ix_order_status_audit_order_id ON dbo.order_status_audit (order_id, id);");

            // CREATE TRIGGER must be the only statement in its batch, so it gets its own Sql() call.
            // "inserted" and "deleted" hold every affected row, so one UPDATE touching many orders
            // writes one audit row per order (set-based, no cursor).
            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER dbo.trg_orders_status_audit
                ON dbo.orders
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    INSERT INTO dbo.order_status_audit (order_id, old_status, new_status)
                    SELECT i.id, d.status, i.status
                    FROM inserted i
                    LEFT JOIN deleted d ON d.id = i.id
                    WHERE d.id IS NULL            -- new order
                       OR d.status <> i.status;   -- status really changed
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.trg_orders_status_audit;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS dbo.order_status_audit;");
        }
    }
}
