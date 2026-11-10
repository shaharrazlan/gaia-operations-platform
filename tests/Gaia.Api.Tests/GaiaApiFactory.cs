using Gaia.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
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
    /// <summary>The management key the test server is configured with (R13). Test-only value.</summary>
    public const string ManagementKey = "test-management-key";

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
        builder.UseSetting("Management:Key", ManagementKey);
    }

    /// <summary>A client that sends the management key, for add / change / delete and the full list.</summary>
    public HttpClient CreateManagementClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Management-Key", ManagementKey);
        return client;
    }

    public async Task InitializeAsync()
    {
        // LocalDB is sometimes very slow to create a database (seen as a timeout during the first migration).
        // Retry once on a clean slate, and never leave a half-made database behind.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var db = CreateDbContext();
                await db.Database.MigrateAsync();
                return;
            }
            catch (SqlException) when (attempt < 2)
            {
                await DropDatabaseAsync();
            }
            catch
            {
                await DropDatabaseAsync();
                throw;
            }
        }
    }

    private async Task DropDatabaseAsync()
    {
        try
        {
            await using var db = CreateDbContext();
            await db.Database.EnsureDeletedAsync();
        }
        catch (SqlException)
        {
            // best effort; the original error matters more
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var db = CreateDbContext())
            await db.Database.EnsureDeletedAsync();
        await DisposeAsync();
    }
}
