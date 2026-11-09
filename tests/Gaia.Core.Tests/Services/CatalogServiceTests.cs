using Gaia.Core.Errors;
using Gaia.Core.Operations;
using Gaia.Core.Services;

namespace Gaia.Core.Tests.Services;

public class CatalogServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>In-memory catalog. Like the database, it rejects a save whose version is not the current one.</summary>
    internal sealed class FakeRepository(params OperationDefinition[] operations) : IOperationRepository
    {
        public List<OperationDefinition> Rows { get; } = [.. operations];
        private int _nextVersion = 100;

        public Task<IReadOnlyList<OperationDefinition>> ListAsync(bool includeDisabled, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OperationDefinition>>(
                Rows.Where(o => !o.IsDeleted && (includeDisabled || o.IsEnabled)).ToList());

        public Task<OperationDefinition?> FindAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.FirstOrDefault(o => o.Id == id));

        public Task<OperationDefinition> AddAsync(OperationDefinition operation, CancellationToken cancellationToken = default)
        {
            operation.Version = [(byte)_nextVersion++];
            Rows.Add(operation);
            return Task.FromResult(operation);
        }

        public Task<OperationDefinition> UpdateAsync(OperationDefinition operation, byte[] expectedVersion,
            CancellationToken cancellationToken = default)
        {
            var index = Rows.FindIndex(o => o.Id == operation.Id);
            if (!Rows[index].Version.AsSpan().SequenceEqual(expectedVersion))
                throw new ConflictException("changed", ErrorCodes.EditConflict);
            operation.Version = [(byte)_nextVersion++];
            Rows[index] = operation;
            return Task.FromResult(operation);
        }
    }

    internal static OperationInput Input(string? id = "avg", string? name = "Average", string? formula = "(A + B) / 2",
        OperationKind? kind = OperationKind.Arithmetic, InputType? inputType = InputType.Number,
        string? fieldALabel = "First number", string? fieldBLabel = "Second number", bool isEnabled = true,
        string? version = null) =>
        new(id, name, kind, fieldALabel, fieldBLabel, inputType, formula, isEnabled, version);

    internal static CatalogService Service(FakeRepository repository) =>
        new(repository, new FixedTime(Now));

    internal sealed class FixedTime(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    [Theory]
    [InlineData("", "id")]
    [InlineData("A", "id")]                        // too short
    [InlineData("Avg", "id")]                      // upper case
    [InlineData("1avg", "id")]                     // must start with a letter
    [InlineData("avg value", "id")]                // no spaces
    [InlineData("avg/2", "id")]                    // would break the URL
    public async Task R4_AC4_an_invalid_id_is_rejected_naming_the_id_field(string id, string field)
    {
        var repository = new FakeRepository();

        var error = await Assert.ThrowsAsync<ValidationException>(() => Service(repository).CreateAsync(Input(id: id)));

        Assert.Equal(ErrorCodes.Validation, error.Code);
        Assert.Equal(field, error.Field);
        Assert.Empty(repository.Rows);
    }

    [Fact]
    public async Task R4_AC4_an_id_longer_than_50_characters_is_rejected()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(new FakeRepository()).CreateAsync(Input(id: "a" + new string('b', 50))));

        Assert.Equal("id", error.Field);
    }

    [Theory]
    [InlineData(null, "name")]
    [InlineData("   ", "name")]
    public async Task R4_AC4_a_missing_name_is_rejected(string? name, string field)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(new FakeRepository()).CreateAsync(Input(name: name)));

        Assert.Equal(field, error.Field);
    }

    [Fact]
    public async Task R4_AC4_missing_labels_kind_or_input_type_are_rejected_naming_the_field()
    {
        var service = Service(new FakeRepository());

        Assert.Equal("fieldALabel", (await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Input(fieldALabel: "")))).Field);
        Assert.Equal("fieldBLabel", (await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Input(fieldBLabel: null)))).Field);
        Assert.Equal("kind", (await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Input(kind: null)))).Field);
        Assert.Equal("inputType", (await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Input(inputType: null)))).Field);
    }

    [Fact]
    public async Task R4_AC4_an_undefined_enum_number_is_rejected()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(new FakeRepository()).CreateAsync(Input(kind: (OperationKind)99)));

        Assert.Equal("kind", error.Field);
    }

    [Fact]
    public async Task R4_AC4_too_long_name_or_label_is_rejected()
    {
        var service = Service(new FakeRepository());

        Assert.Equal("name", (await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Input(name: new string('n', 101))))).Field);
        Assert.Equal("fieldALabel", (await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Input(fieldALabel: new string('l', 51))))).Field);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task R4_AC4_a_custom_operation_needs_a_formula(string? formula)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(new FakeRepository()).CreateAsync(Input(formula: formula)));

        Assert.Equal("formula", error.Field);
    }

    [Theory]
    [InlineData("A +")]
    [InlineData("sqrt(A, B)")]
    [InlineData("C * 2")]
    public async Task R4_AC4_a_bad_formula_is_rejected_with_formula_invalid_and_nothing_is_saved(string formula)
    {
        var repository = new FakeRepository();

        var error = await Assert.ThrowsAsync<ValidationException>(() => Service(repository).CreateAsync(Input(formula: formula)));

        Assert.Equal(ErrorCodes.FormulaInvalid, error.Code);
        Assert.Equal("formula", error.Field);
        Assert.Empty(repository.Rows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R4_AC4_a_duplicate_id_is_a_conflict_even_if_the_old_one_was_deleted(bool deleted)
    {
        var existing = Op("avg", deleted: deleted);
        var repository = new FakeRepository(existing);

        var error = await Assert.ThrowsAsync<ConflictException>(() => Service(repository).CreateAsync(Input(id: "avg")));

        Assert.Equal(ErrorCodes.DuplicateId, error.Code);
        Assert.Equal("id", error.Field);
        Assert.Same(existing, Assert.Single(repository.Rows));
    }

    [Fact]
    public async Task T28_created_operations_are_always_custom_formula_operations()
    {
        var repository = new FakeRepository();

        var created = await Service(repository).CreateAsync(Input(name: "  Average  ", isEnabled: false));

        Assert.False(created.IsSystem);
        Assert.False(created.IsDeleted);
        Assert.False(created.IsEnabled);
        Assert.Equal("Average", created.Name);              // trimmed
        Assert.Equal("(A + B) / 2", created.Formula);
        Assert.Equal(Now, created.CreatedAtUtc);
        Assert.Equal(Now, created.UpdatedAtUtc);
        Assert.NotEmpty(created.Version);
        Assert.Same(created, Assert.Single(repository.Rows));
    }

    internal static OperationDefinition Op(string id, string? formula = "A + B", bool system = false, bool deleted = false,
        bool enabled = true) => new()
    {
        Id = id,
        Name = id,
        Kind = OperationKind.Arithmetic,
        FieldALabel = "A",
        FieldBLabel = "B",
        InputType = InputType.Number,
        Formula = formula,
        IsSystem = system,
        IsDeleted = deleted,
        IsEnabled = enabled,
        Version = [1],
    };
}
