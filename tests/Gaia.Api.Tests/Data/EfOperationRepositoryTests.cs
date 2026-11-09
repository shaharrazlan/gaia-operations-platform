using Gaia.Core.Operations;
using Gaia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Api.Tests.Data;

public class EfOperationRepositoryTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>, IAsyncLifetime
{
    // Unique ids so these rows never clash with the seed or with other tests in this class.
    private readonly string _disabledId = $"t17-disabled-{Guid.NewGuid():N}"[..30];
    private readonly string _deletedId = $"t17-deleted-{Guid.NewGuid():N}"[..30];

    public async Task InitializeAsync()
    {
        await using var db = factory.CreateDbContext();
        db.Operations.AddRange(
            Custom(_disabledId, isEnabled: false, isDeleted: false),
            Custom(_deletedId, isEnabled: true, isDeleted: true));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task T17_List_without_disabled_excludes_disabled_and_deleted()
    {
        await using var db = factory.CreateDbContext();

        var ids = (await new EfOperationRepository(db).ListAsync(includeDisabled: false)).Select(o => o.Id).ToList();

        Assert.Contains("add", ids);
        Assert.DoesNotContain(_disabledId, ids);
        Assert.DoesNotContain(_deletedId, ids);
    }

    [Fact]
    public async Task T17_List_with_disabled_includes_disabled_but_never_deleted()
    {
        await using var db = factory.CreateDbContext();

        var ids = (await new EfOperationRepository(db).ListAsync(includeDisabled: true)).Select(o => o.Id).ToList();

        Assert.Contains(_disabledId, ids);
        Assert.DoesNotContain(_deletedId, ids);
    }

    [Fact]
    public async Task T17_Find_returns_null_for_unknown_id()
    {
        await using var db = factory.CreateDbContext();

        Assert.Null(await new EfOperationRepository(db).FindAsync("no-such-operation"));
    }

    [Fact]
    public async Task T17_Find_is_case_sensitive()
    {
        await using var db = factory.CreateDbContext();

        Assert.Null(await new EfOperationRepository(db).FindAsync("ADD"));
    }

    [Fact]
    public async Task T17_timestamps_are_read_back_as_UTC()
    {
        await using var db = factory.CreateDbContext();

        var add = await new EfOperationRepository(db).FindAsync("add");

        Assert.Equal(DateTimeKind.Utc, add!.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task T17_database_rejects_an_unknown_kind_value()
    {
        await using var db = factory.CreateDbContext();

        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE Operations SET Kind = 'Arithmatic' WHERE Id = 'add'"));
    }

    [Fact]
    public async Task T17_Find_returns_a_deleted_operation_so_callers_can_decide()
    {
        await using var db = factory.CreateDbContext();

        var found = await new EfOperationRepository(db).FindAsync(_deletedId);

        Assert.NotNull(found);
        Assert.True(found.IsDeleted);
        Assert.False(found.IsAvailable);
    }

    private static OperationDefinition Custom(string id, bool isEnabled, bool isDeleted) => new()
    {
        Id = id,
        Name = id,
        Kind = OperationKind.Arithmetic,
        FieldALabel = "A",
        FieldBLabel = "B",
        InputType = InputType.Number,
        Formula = "A + B",
        IsEnabled = isEnabled,
        IsDeleted = isDeleted,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };
}
