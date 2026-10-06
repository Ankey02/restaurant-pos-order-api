using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace RestaurantPos.Tests;

// Starts a throw-away PostgreSQL container, then runs the real API against it.
// The app applies its own migrations on startup, so the schema is the real one.
public class PosApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    public string ConnectionString => _db.GetConnectionString();

    public Task InitializeAsync() => _db.StartAsync();

    Task IAsyncLifetime.DisposeAsync() => _db.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("ConnectionStrings:Default", ConnectionString);
}

// One container is shared by all API tests (they run one after another).
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<PosApiFactory>;
