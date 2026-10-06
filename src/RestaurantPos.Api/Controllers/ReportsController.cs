using Microsoft.AspNetCore.Mvc;
using RestaurantPos.Application.Reports;

namespace RestaurantPos.Api.Controllers;

[ApiController]
[Route("reports")]
public class ReportsController(IReportQueries reports, TimeProvider clock) : ControllerBase
{
    /// <summary>Paid sales per day, with a running total. Default: last 30 days.</summary>
    [HttpGet("daily-sales")]
    public async Task<ActionResult<IReadOnlyList<DailySalesRow>>> DailySales(
        DateOnly? fromDate, DateOnly? toDate, int utcOffsetMinutes, CancellationToken ct)
    {
        if (!TryRange(fromDate, toDate, utcOffsetMinutes, out var from, out var to, out var error))
            return error!;
        return Ok(await reports.DailySalesAsync(from, to, utcOffsetMinutes, ct));
    }

    /// <summary>Best-selling items ranked by revenue (RANK window function).</summary>
    [HttpGet("top-items")]
    public async Task<ActionResult<IReadOnlyList<TopItemRow>>> TopItems(
        DateOnly? fromDate, DateOnly? toDate, int utcOffsetMinutes, int limit = 10, CancellationToken ct = default)
    {
        if (limit is < 1 or > 50)
            return Problem(detail: "limit must be between 1 and 50.", statusCode: 400);
        if (!TryRange(fromDate, toDate, utcOffsetMinutes, out var from, out var to, out var error))
            return error!;
        return Ok(await reports.TopItemsAsync(from, to, utcOffsetMinutes, limit, ct));
    }

    /// <summary>Sales per hour of the day, with each hour's share of the total.</summary>
    [HttpGet("sales-by-hour")]
    public async Task<ActionResult<IReadOnlyList<HourlySalesRow>>> SalesByHour(
        DateOnly? fromDate, DateOnly? toDate, int utcOffsetMinutes, CancellationToken ct)
    {
        if (!TryRange(fromDate, toDate, utcOffsetMinutes, out var from, out var to, out var error))
            return error!;
        return Ok(await reports.HourlySalesAsync(from, to, utcOffsetMinutes, ct));
    }

    // utcOffsetMinutes: e.g. 330 for India (UTC+05:30). Used to decide which local day/hour a sale belongs to.
    private bool TryRange(DateOnly? fromDate, DateOnly? toDate, int offset,
        out DateOnly from, out DateOnly to, out ActionResult? error)
    {
        from = default; to = default; error = null;
        if (offset is < -720 or > 840)
        {
            error = Problem(detail: "utcOffsetMinutes must be between -720 and 840.", statusCode: 400);
            return false;
        }
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddMinutes(offset));
        to = toDate ?? today;
        from = fromDate ?? to.AddDays(-29);
        if (from > to)
        {
            error = Problem(detail: "fromDate must not be after toDate.", statusCode: 400);
            return false;
        }
        if (to.DayNumber - from.DayNumber > 366)
        {
            error = Problem(detail: "The date range can be at most one year.", statusCode: 400);
            return false;
        }
        return true;
    }
}
