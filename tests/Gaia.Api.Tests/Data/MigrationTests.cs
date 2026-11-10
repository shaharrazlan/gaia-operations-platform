using Microsoft.EntityFrameworkCore;

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
    public async Task T16_every_operation_gets_a_rowversion_from_SQL_Server()
    {
        await using var db = factory.CreateDbContext();

        var versions = await db.Operations.AsNoTracking().Select(o => o.Version).ToListAsync();

        Assert.All(versions, v => Assert.Equal(8, v.Length));
    }
}
