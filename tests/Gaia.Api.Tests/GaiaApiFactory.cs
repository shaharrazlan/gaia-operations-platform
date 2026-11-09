using Gaia.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Api.Tests;

/// <summary>
/// Starts the real API in memory for integration tests.
/// Each factory instance (one per test class, via IClassFixture) gets its own LocalDB database:
/// created with the real migrations before the class's first test, dropped after its last test.
/// So test classes never see each other's data, can run in parallel, and leave nothing behind.
/// </summary>
public class GaiaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DatabaseName { get; } = $"GaiaTests_{Guid.NewGuid():N}";

    public string ConnectionString =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>A context on this factory's database, for arranging data and checking results.</summary>
    public GaiaDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<GaiaDbContext>()
            // Creating or dropping a database can take longer than the default 30 s on a busy machine.
            .UseSqlServer(ConnectionString, sql => sql.CommandTimeout(120))
            .Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Gaia", ConnectionString);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var db = CreateDbContext())
            await db.Database.EnsureDeletedAsync();
        await DisposeAsync();
    }
}
