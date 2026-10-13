using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace RestaurantPos.Tests;

// Starts a throw-away SQL Server container, then runs the real API against it.
// The app applies its own migrations on startup, so the schema is the real one.
public class PosApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _db = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    // Use a dedicated database instead of "master"; EF creates it when migrating.
    public string ConnectionString => new SqlConnectionStringBuilder(_db.GetConnectionString())
    {
        InitialCatalog = "restaurant_tests",
        TrustServerCertificate = true
    }.ConnectionString;

    public Task InitializeAsync() => _db.StartAsync();

    Task IAsyncLifetime.DisposeAsync() => _db.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("ConnectionStrings:Default", ConnectionString);
}

// One container is shared by all API tests (they run one after another).
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<PosApiFactory>;
