using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Application.Common;
using RestaurantPos.Infrastructure.Persistence;

namespace RestaurantPos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<RestaurantPos.Application.Reports.IReportQueries>(_ => new RestaurantPos.Infrastructure.Reports.ReportQueries(connectionString));

        services.AddScoped<RestaurantPos.Application.Orders.IOrderHistoryQueries>(_ => new RestaurantPos.Infrastructure.Auditing.OrderHistoryQueries(connectionString));

        return services;
    }
}
