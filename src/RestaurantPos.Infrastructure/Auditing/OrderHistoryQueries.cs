using Dapper;
using Microsoft.Data.SqlClient;
using RestaurantPos.Application.Common;
using RestaurantPos.Application.Orders;

namespace RestaurantPos.Infrastructure.Auditing;

// Reads the audit rows written by the trg_orders_status_audit trigger.
public class OrderHistoryQueries(string connectionString) : IOrderHistoryQueries
{
    private const string Sql = """
        SELECT old_status AS FromStatus,
               new_status AS ToStatus,
               changed_at AS ChangedAt
        FROM dbo.order_status_audit
        WHERE order_id = @orderId
        ORDER BY id
        """;

    public async Task<IReadOnlyList<OrderStatusChangeDto>> GetAsync(Guid orderId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        var rows = (await conn.QueryAsync<AuditRow>(new CommandDefinition(
            Sql, new { orderId }, cancellationToken: ct))).ToList();

        // The trigger writes a row when an order is created, so no rows means no such order.
        if (rows.Count == 0)
            throw new NotFoundException("Order not found.");

        // datetime2 has no time zone; the trigger stores UTC (SYSUTCDATETIME).
        return rows
            .Select(r => new OrderStatusChangeDto(
                r.FromStatus, r.ToStatus, DateTime.SpecifyKind(r.ChangedAt, DateTimeKind.Utc)))
            .ToList();
    }

    private sealed class AuditRow
    {
        public string? FromStatus { get; init; }
        public string ToStatus { get; init; } = "";
        public DateTime ChangedAt { get; init; }
    }
}
