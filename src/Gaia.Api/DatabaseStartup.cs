using System.Data.Common;
using Gaia.Core.Operations;
using Gaia.Core.Services;
using Gaia.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Api;

/// <summary>What the API does with its database when it starts.</summary>
public static class DatabaseStartup
{
    public const string E2EDatabasePrefix = "GaiaE2E";

    public static async Task PrepareAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GaiaDbContext>();

        // Development and E2E create/upgrade their own database, so nobody has to run database commands.
        // E2E starts from an empty database every run, so end-to-end tests are repeatable.
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("E2E"))
        {
            if (app.Environment.IsEnvironment("E2E"))
            {
                var connectionString = db.Database.GetConnectionString()!;
                if (!IsE2EDatabase(connectionString))
                    throw new InvalidOperationException(
                        $"The E2E environment deletes its database on startup, so its name must start with '{E2EDatabasePrefix}'.");
                await db.Database.EnsureDeletedAsync();
            }
            await db.Database.MigrateAsync();
        }

        // In every environment: fail fast if an operation has neither a formula nor exactly one C# handler.
        try
        {
            HandlerCheck.EnsureEveryCodeOperationHasAHandler(
                await db.Operations.AsNoTracking().ToListAsync(),
                scope.ServiceProvider.GetServices<IOperationHandler>());
        }
        catch (DbException error)
        {
            // Database not reachable yet: start anyway; requests will get 503 until it is.
            app.Logger.LogWarning(error, "Could not check operation handlers at startup: the database is not available.");
        }
    }

    /// <summary>Guard against wiping a real database by running with ASPNETCORE_ENVIRONMENT=E2E by mistake.</summary>
    public static bool IsE2EDatabase(string connectionString) =>
        new SqlConnectionStringBuilder(connectionString).InitialCatalog.StartsWith(E2EDatabasePrefix, StringComparison.OrdinalIgnoreCase);
}
