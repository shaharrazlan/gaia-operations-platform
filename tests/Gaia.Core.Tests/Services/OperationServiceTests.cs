using Gaia.Core.Errors;
using Gaia.Core.Formulas;
using Gaia.Core.Operations;
using Gaia.Core.Services;

namespace Gaia.Core.Tests.Services;

public class OperationServiceTests
{
    private static OperationDefinition Op(string id, string? formula, bool enabled = true, bool deleted = false,
        InputType input = InputType.Number) => new()
    {
        Id = id,
        Name = id,
        FieldALabel = "A",
        FieldBLabel = "B",
        InputType = input,
        Formula = formula,
        IsEnabled = enabled,
        IsDeleted = deleted,
    };

    private sealed class FakeRepository(params OperationDefinition[] operations) : IOperationRepository
    {
        public Task<IReadOnlyList<OperationDefinition>> ListAsync(bool includeDisabled, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OperationDefinition>>(
                operations.Where(o => !o.IsDeleted && (includeDisabled || o.IsEnabled)).ToList());

        public Task<OperationDefinition?> FindAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(operations.FirstOrDefault(o => o.Id == id));
    }

    private sealed class FakeHandler(string id, string answer) : IOperationHandler
    {
        public string OperationId => id;
        public (Value A, Value B)? Received { get; private set; }

        public Task<string> ExecuteAsync(Value a, Value b, CancellationToken cancellationToken)
        {
            Received = (a, b);
            return Task.FromResult(answer);
        }
    }

    private static OperationService Service(IOperationRepository repository, params IOperationHandler[] handlers) =>
        new(repository, handlers);

    [Fact]
    public async Task R2_AC1_executes_a_formula_operation_and_returns_its_result()
    {
        var service = Service(new FakeRepository(Op("divide", "A / B")));

        var result = await service.ExecuteAsync("divide", "7", "2");

        Assert.Equal(new ExecutionResult("divide", "7", "2", "3.5"), result);
    }

    [Fact]
    public async Task R2_AC1_executes_a_code_handler_operation_with_parsed_inputs()
    {
        var handler = new FakeHandler("weather", "Clear sky, 20–25 °C");
        var service = Service(new FakeRepository(Op("weather", null, input: InputType.Coordinate)), handler);

        var result = await service.ExecuteAsync("weather", "32.08", "34.78");

        Assert.Equal("Clear sky, 20–25 °C", result.Result);
        Assert.Equal((new NumberValue(32.08), new NumberValue(34.78)), handler.Received);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("disabled")]
    [InlineData("deleted")]
    public async Task R2_AC2_unknown_disabled_or_deleted_id_throws_not_found_naming_the_id(string id)
    {
        var service = Service(new FakeRepository(Op("disabled", "A", enabled: false), Op("deleted", "A", deleted: true)));

        var error = await Assert.ThrowsAsync<NotFoundException>(() => service.ExecuteAsync(id, "1", "2"));

        Assert.Equal(ErrorCodes.OperationNotFound, error.Code);
        Assert.Contains($"'{id}'", error.Message);
    }

    [Fact]
    public async Task R2_AC3_missing_field_A_throws_validation_naming_fieldA()
    {
        var service = Service(new FakeRepository(Op("add", "A + B")));

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.ExecuteAsync("add", null, "2"));

        Assert.Equal("fieldA", error.Field);
    }

    [Fact]
    public async Task T18_code_operation_without_a_registered_handler_is_a_system_error()
    {
        var service = Service(new FakeRepository(Op("weather", null)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync("weather", "1", "2"));
    }

    [Fact]
    public async Task T18_list_returns_only_available_operations()
    {
        var service = Service(new FakeRepository(Op("add", "A + B"), Op("off", "A", enabled: false)));

        var ids = (await service.ListAsync()).Select(o => o.Id);

        Assert.Equal(["add"], ids);
    }
}
