using Gaia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gaia.Api.Tests.Data;

public class MigrationTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>
{
    [Fact]
    public async Task T16_migrated_database_contains_the_10_seeded_operations_with_correct_IsSystem_flags()
    {
        await using var db = factory.CreateDbContext();

        var operations = await db.Operations.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.IsSystem);

        Assert.Equal(10, operations.Count);
        foreach (var id in new[] { "add", "subtract", "multiply", "divide", "power", "concat", "count", "weather" })
            Assert.True(operations[id], $"{id} should be a system operation");
        Assert.False(operations["average"]);
        Assert.False(operations["hypotenuse"]);
    }

    [Fact]
    public async Task T41_migration_creates_OperationRuns_with_the_history_index()
    {
        await using var db = factory.CreateDbContext();

        var indexColumns = await db.Database.SqlQueryRaw<string>("""
            SELECT c.name AS Value
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID('OperationRuns') AND i.name = 'IX_OperationRuns_History'
            ORDER BY ic.key_ordinal
            """).ToListAsync();

        Assert.Equal(["OperationId", "IsSuccess", "CreatedAtUtc"], indexColumns);
    }

    [Fact]
    public async Task T41_a_run_must_belong_to_an_existing_operation()
    {
        await using var db = factory.CreateDbContext();

        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO OperationRuns (OperationId, IsSuccess, CreatedAtUtc, DurationMs) VALUES ('no-such-op', 1, SYSUTCDATETIME(), 0)"));
    }

    [Fact]
    public async Task R4_AC10_upgrading_a_database_with_clashing_names_makes_them_unique_and_keeps_the_first()
    {
        // Its own database, stopped at the migration before unique names, so clashing names can still be stored.
        var options = new DbContextOptionsBuilder<GaiaDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database=GaiaTests_names_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.CommandTimeout(120))
            .Options;
        await using var db = new GaiaDbContext(options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261110075111_AddOperationRuns");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO Operations (Id, Name, Kind, FieldALabel, FieldBLabel, InputType, Formula, IsSystem, IsEnabled, IsDeleted, CreatedAtUtc, UpdatedAtUtc) VALUES
                ('mean-a', 'Mean', 'Arithmetic', 'A', 'B', 'Number', 'A', 0, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
                ('mean-b', 'MEAN', 'Arithmetic', 'A', 'B', 'Number', 'A', 0, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
                ('mean-old', 'Mean', 'Arithmetic', 'A', 'B', 'Number', 'A', 0, 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME())
                """);

            await migrator.MigrateAsync();

            var names = await db.Operations.AsNoTracking().Where(o => o.Id.StartsWith("mean"))
                .ToDictionaryAsync(o => o.Id, o => o.Name);
            Assert.Equal("Mean", names["mean-a"]);              // the first (by id) keeps its name
            Assert.Equal("MEAN (mean-b)", names["mean-b"]);     // a live clash (disabled counts) gets its id
            Assert.Equal("Mean", names["mean-old"]);            // deleted operations don't count
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task T16_every_operation_gets_a_rowversion_from_SQL_Server()
    {
        await using var db = factory.CreateDbContext();

        var versions = await db.Operations.AsNoTracking().Select(o => o.Version).ToListAsync();

        Assert.All(versions, v => Assert.Equal(8, v.Length));
    }
}
