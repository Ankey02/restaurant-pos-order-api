using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Application.Menu;
using RestaurantPos.Application.Orders;

namespace RestaurantPos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<MenuCommands>();
        services.AddScoped<MenuQueries>();
        services.AddScoped<OrderCommands>();
        services.AddScoped<OrderQueries>();
        return services;
    }
}
