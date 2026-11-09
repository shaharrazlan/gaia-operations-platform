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
    public async Task T16_every_operation_gets_a_rowversion_from_SQL_Server()
    {
        await using var db = factory.CreateDbContext();

        var versions = await db.Operations.AsNoTracking().Select(o => o.Version).ToListAsync();

        Assert.All(versions, v => Assert.Equal(8, v.Length));
    }
}
