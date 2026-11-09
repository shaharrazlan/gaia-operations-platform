using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gaia.Api.Tests;

/// <summary>
/// Starts the real API in memory for integration tests.
/// Each factory instance (one per test class, via IClassFixture) gets its own LocalDB database,
/// so test classes never see each other's data and can run in parallel.
/// Creating and dropping that database is added in T16, once the DbContext exists.
/// </summary>
public class GaiaApiFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"GaiaTests_{Guid.NewGuid():N}";

    public string ConnectionString =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Gaia", ConnectionString);
    }
}
