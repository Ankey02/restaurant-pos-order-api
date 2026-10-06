using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Middleware;
using RestaurantPos.Application;
using RestaurantPos.Application.Common;
using RestaurantPos.Infrastructure;
using RestaurantPos.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddSingleton(new RestaurantSettings(
    builder.Configuration.GetValue("Restaurant:TaxRateBasisPoints", 500)));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing connection string 'Default'."));

var app = builder.Build();

// Create or update the database tables when the app starts.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
