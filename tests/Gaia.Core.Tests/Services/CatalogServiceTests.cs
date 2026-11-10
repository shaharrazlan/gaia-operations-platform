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

    // --- Update and delete (T29) ---

    private static readonly string V1 = Convert.ToBase64String([1]);

    /// <summary>The input an operator would send back after loading <paramref name="op"/> unchanged.</summary>
    private static OperationInput Same(OperationDefinition op, string? version = null) =>
        new(op.Id, op.Name, op.Kind, op.FieldALabel, op.FieldBLabel, op.InputType, op.Formula, op.IsEnabled,
            version ?? Convert.ToBase64String(op.Version));

    [Fact]
    public async Task R4_AC6_a_system_operation_can_be_renamed_and_disabled()
    {
        var repository = new FakeRepository(Op("divide", "A / B", system: true));

        var saved = await Service(repository).UpdateAsync("divide",
            Same(repository.Rows[0]) with { Name = "Division", FieldALabel = "Dividend", IsEnabled = false });

        Assert.Equal("Division", saved.Name);
        Assert.Equal("Dividend", saved.FieldALabel);
        Assert.False(saved.IsEnabled);
        Assert.True(saved.IsSystem);
        Assert.Equal("A / B", saved.Formula);
        Assert.Equal(Now, saved.UpdatedAtUtc);
    }

    [Fact]
    public async Task R4_AC6_a_code_operation_can_be_renamed_with_no_formula()
    {
        var repository = new FakeRepository(Op("weather", formula: null, system: true));

        var saved = await Service(repository).UpdateAsync("weather", Same(repository.Rows[0]) with { Name = "Forecast" });

        Assert.Equal("Forecast", saved.Name);
        Assert.Null(saved.Formula);
    }

    public static TheoryData<string?, OperationInput, string> SystemLogicChanges => new()
    {
        { "A / B", Same(Op("divide", "A / B", system: true)) with { Formula = "A * B" }, "formula" },
        { null, Same(Op("weather", null, system: true)) with { Formula = "A + B" }, "formula" },
        { "A / B", Same(Op("divide", "A / B", system: true)) with { Kind = OperationKind.Other }, "kind" },
        { "A / B", Same(Op("divide", "A / B", system: true)) with { InputType = InputType.Text }, "inputType" },
    };

    [Theory]
    [MemberData(nameof(SystemLogicChanges))]
    public async Task R4_AC6_changing_the_logic_of_a_system_operation_is_rejected(string? formula, OperationInput change, string field)
    {
        var original = Op(change.Id!, formula, system: true);
        var repository = new FakeRepository(original);

        var error = await Assert.ThrowsAsync<ValidationException>(() => Service(repository).UpdateAsync(original.Id, change));

        Assert.Equal(ErrorCodes.SystemLogicReadonly, error.Code);
        Assert.Equal(field, error.Field);
        Assert.Same(original, repository.Rows[0]);
    }

    [Fact]
    public async Task S6_an_update_that_leaves_out_isEnabled_keeps_the_current_state()
    {
        var repository = new FakeRepository(Op("avg", enabled: false));

        var saved = await Service(repository).UpdateAsync("avg", Same(repository.Rows[0]) with { Name = "Mean", IsEnabled = null });

        Assert.False(saved.IsEnabled);
    }

    [Fact]
    public async Task S6_a_create_that_leaves_out_isEnabled_is_enabled()
    {
        var created = await Service(new FakeRepository()).CreateAsync(Input() with { IsEnabled = null });

        Assert.True(created.IsEnabled);
    }

    [Fact]
    public async Task R4_AC2_a_custom_operation_can_change_its_formula_kind_and_input_type()
    {
        var repository = new FakeRepository(Op("avg", "(A + B) / 2"));

        var saved = await Service(repository).UpdateAsync("avg",
            Same(repository.Rows[0]) with { Formula = "concat(A, B)", Kind = OperationKind.String, InputType = InputType.Text });

        Assert.Equal("concat(A, B)", saved.Formula);
        Assert.Equal(OperationKind.String, saved.Kind);
        Assert.Equal(InputType.Text, saved.InputType);
        Assert.False(saved.IsSystem);
    }

    [Fact]
    public async Task R4_AC4_an_update_with_a_bad_formula_is_rejected_and_nothing_changes()
    {
        var original = Op("avg", "(A + B) / 2");
        var repository = new FakeRepository(original);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(repository).UpdateAsync("avg", Same(original) with { Formula = "A +" }));

        Assert.Equal(ErrorCodes.FormulaInvalid, error.Code);
        Assert.Same(original, repository.Rows[0]);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("gone")]
    public async Task R4_AC3_updating_an_unknown_or_deleted_operation_is_not_found(string id)
    {
        var repository = new FakeRepository(Op("gone", deleted: true));

        await Assert.ThrowsAsync<NotFoundException>(() => Service(repository).UpdateAsync(id, Input(id: id, version: V1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not base64!")]
    public async Task R4_AC7_an_update_needs_the_version_it_was_based_on(string? version)
    {
        var repository = new FakeRepository(Op("avg"));

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(repository).UpdateAsync("avg", Same(repository.Rows[0]) with { Version = version }));

        Assert.Equal("version", error.Field);
    }

    [Fact]
    public async Task R4_AC7_an_update_based_on_an_old_version_is_an_edit_conflict()
    {
        var repository = new FakeRepository(Op("avg"));
        var service = Service(repository);
        await service.UpdateAsync("avg", Same(repository.Rows[0], V1) with { Name = "First" });

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAsync("avg", Same(repository.Rows[0], V1) with { Name = "Second" }));

        Assert.Equal(ErrorCodes.EditConflict, error.Code);
        Assert.Equal("First", repository.Rows[0].Name);
    }

    [Fact]
    public async Task T29_the_id_in_the_body_must_match_the_operation_being_changed()
    {
        var repository = new FakeRepository(Op("avg"));

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(repository).UpdateAsync("avg", Same(repository.Rows[0]) with { Id = "other" }));

        Assert.Equal("id", error.Field);
    }

    [Fact]
    public async Task R4_AC6_deleting_a_system_operation_is_rejected()
    {
        var repository = new FakeRepository(Op("divide", "A / B", system: true));

        var error = await Assert.ThrowsAsync<ValidationException>(() => Service(repository).DeleteAsync("divide"));

        Assert.Equal(ErrorCodes.SystemCannotDelete, error.Code);
        Assert.False(repository.Rows[0].IsDeleted);
    }

    [Fact]
    public async Task R4_AC3_deleting_a_custom_operation_hides_it_but_keeps_the_row()
    {
        var repository = new FakeRepository(Op("avg"));

        await Service(repository).DeleteAsync("avg");

        var row = Assert.Single(repository.Rows);
        Assert.True(row.IsDeleted);
        Assert.Equal(Now, row.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("gone")]
    public async Task R4_AC3_deleting_an_unknown_or_already_deleted_operation_is_not_found(string id)
    {
        var repository = new FakeRepository(Op("gone", deleted: true));

        await Assert.ThrowsAsync<NotFoundException>(() => Service(repository).DeleteAsync(id));
    }

    [Fact]
    public async Task T29_the_management_list_includes_disabled_operations()
    {
        var repository = new FakeRepository(Op("on"), Op("off", enabled: false), Op("gone", deleted: true));

        var ids = (await Service(repository).ListAllAsync()).Select(o => o.Id);

        Assert.Equal(["on", "off"], ids);
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
