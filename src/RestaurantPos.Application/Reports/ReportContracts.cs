namespace RestaurantPos.Application.Reports;

public record DailySalesRow
{
    public string Day { get; init; } = "";
    public int Orders { get; init; }
    public long GrossCents { get; init; }
    public long DiscountCents { get; init; }
    public long TaxCents { get; init; }
    public long TotalCents { get; init; }
    public long AvgOrderCents { get; init; }
    public long RunningTotalCents { get; init; }
}

public record TopItemRow
{
    public int Rank { get; init; }
    public string ItemName { get; init; } = "";
    public long QuantitySold { get; init; }
    public long RevenueCents { get; init; }
    public decimal SharePercent { get; init; }
}

public record HourlySalesRow
{
    public int Hour { get; init; }
    public int Orders { get; init; }
    public long TotalCents { get; init; }
    public decimal SharePercent { get; init; }
}

public interface IReportQueries
{
    Task<IReadOnlyList<DailySalesRow>> DailySalesAsync(DateOnly from, DateOnly to, int utcOffsetMinutes, CancellationToken ct);
    Task<IReadOnlyList<TopItemRow>> TopItemsAsync(DateOnly from, DateOnly to, int utcOffsetMinutes, int limit, CancellationToken ct);
    Task<IReadOnlyList<HourlySalesRow>> HourlySalesAsync(DateOnly from, DateOnly to, int utcOffsetMinutes, CancellationToken ct);
}
