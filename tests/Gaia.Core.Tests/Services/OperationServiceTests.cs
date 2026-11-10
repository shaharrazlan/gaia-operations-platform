using Gaia.Core.Errors;
using Gaia.Core.Formulas;
using Gaia.Core.History;
using Gaia.Core.Operations;
using Gaia.Core.Services;
using Gaia.Core.Tests.History;
using Microsoft.Extensions.Logging;

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

        public Task<OperationDefinition> AddAsync(OperationDefinition operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationDefinition> UpdateAsync(OperationDefinition operation, byte[] expectedVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

    private static readonly DateTime Now = new(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

    private static OperationService Service(IOperationRepository repository, params IOperationHandler[] handlers) =>
        Service(repository, new FakeRunRepository(), new ListLogger<RunRecorder>(), handlers);

    private static OperationService Service(IOperationRepository repository, IRunRepository runs, ListLogger<RunRecorder> log,
        params IOperationHandler[] handlers) =>
        new(repository, handlers, new RunRecorder(runs, new FixedTime(Now), log));

    // --- Recording runs (T43) ---

    [Fact]
    public async Task R7_AC1_a_successful_run_is_recorded_with_its_inputs_result_and_time()
    {
        var runs = new FakeRunRepository();
        var service = Service(new FakeRepository(Op("divide", "A / B")), runs, new ListLogger<RunRecorder>());

        await service.ExecuteAsync("divide", "7", "2");

        var run = Assert.Single(runs.Runs);
        Assert.Equal(("divide", "7", "2", "3.5", true), (run.OperationId, run.FieldA, run.FieldB, run.Result, run.IsSuccess));
        Assert.Equal(Now, run.CreatedAtUtc);
        Assert.Null(run.ErrorCode);
    }

    [Fact]
    public async Task R7_AC1_a_failed_run_is_recorded_with_its_error_and_the_error_still_reaches_the_caller()
    {
        var runs = new FakeRunRepository();
        var service = Service(new FakeRepository(Op("divide", "A / B")), runs, new ListLogger<RunRecorder>());

        await Assert.ThrowsAsync<DomainException>(() => service.ExecuteAsync("divide", "5", "0"));

        var run = Assert.Single(runs.Runs);
        Assert.False(run.IsSuccess);
        Assert.Null(run.Result);
        Assert.Equal(ErrorCodes.Domain, run.ErrorCode);
        Assert.Equal("Division by zero.", run.ErrorMessage);
    }

    [Fact]
    public async Task R7_AC1_inputs_longer_than_the_column_are_stored_shortened()
    {
        var runs = new FakeRunRepository();
        var service = Service(new FakeRepository(Op("add", "A + B")), runs, new ListLogger<RunRecorder>());

        await Assert.ThrowsAsync<ValidationException>(() => service.ExecuteAsync("add", new string('9', 5000), "1"));

        Assert.Equal(OperationRun.MaxFieldLength, Assert.Single(runs.Runs).FieldA!.Length);
    }

    [Fact]
    public async Task S8_a_run_is_stored_even_if_the_caller_disconnects_while_it_runs()
    {
        using var disconnect = new CancellationTokenSource();
        var runs = new FakeRunRepository();
        var handler = new CancelsCallerHandler("weather", disconnect);
        var service = Service(new FakeRepository(Op("weather", null, input: InputType.Coordinate)), runs, new ListLogger<RunRecorder>(), handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync("weather", "32", "34", disconnect.Token));

        Assert.True(Assert.Single(runs.Runs).IsSuccess);   // the operation did run, so it is history
    }

    /// <summary>Finishes its work, but the caller goes away in the meantime.</summary>
    private sealed class CancelsCallerHandler(string id, CancellationTokenSource caller) : IOperationHandler
    {
        public string OperationId => id;

        public async Task<string> ExecuteAsync(Value a, Value b, CancellationToken cancellationToken)
        {
            await caller.CancelAsync();
            return "Clear sky";
        }
    }

    [Fact]
    public async Task T43_an_unknown_operation_records_no_run()
    {
        var runs = new FakeRunRepository();
        var service = Service(new FakeRepository(), runs, new ListLogger<RunRecorder>());

        await Assert.ThrowsAsync<NotFoundException>(() => service.ExecuteAsync("nope", "1", "2"));

        Assert.Empty(runs.Runs);
    }

    [Fact]
    public async Task R7_AC3_when_the_history_store_fails_the_result_is_still_returned_and_the_failure_is_logged()
    {
        var log = new ListLogger<RunRecorder>();
        var service = Service(new FakeRepository(Op("divide", "A / B")), new FakeRunRepository { Broken = true }, log);

        var result = await service.ExecuteAsync("divide", "7", "2");

        Assert.Equal("3.5", result.Result);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("divide", entry.Message);
        Assert.NotNull(entry.Exception);
    }

    [Fact]
    public async Task R8_AC1_a_successful_run_comes_back_with_its_history()
    {
        var runs = new FakeRunRepository();
        var service = Service(new FakeRepository(Op("add", "A + B")), runs, new ListLogger<RunRecorder>());
        await service.ExecuteAsync("add", "1", "1");

        var result = await service.ExecuteAsync("add", "2", "2");

        Assert.NotNull(result.History);
        Assert.Equal(["1"], result.History.PreviousRuns.Select(r => r.FieldA));
        Assert.Equal(2, result.History.MonthCount);
    }

    [Fact]
    public async Task R7_AC3_when_reading_history_fails_the_result_still_comes_back_with_no_history_and_it_is_logged()
    {
        var runs = new BrokenReadsRunRepository();
        var log = new ListLogger<RunRecorder>();
        var service = Service(new FakeRepository(Op("divide", "A / B")), runs, log);

        var result = await service.ExecuteAsync("divide", "7", "2");

        Assert.Equal("3.5", result.Result);
        Assert.Null(result.History);
        Assert.Equal(LogLevel.Error, Assert.Single(log.Entries).Level);
    }

    private sealed class BrokenReadsRunRepository : IRunRepository
    {
        public Task AddAsync(OperationRun run, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<OperationRun>> LatestSuccessfulAsync(string operationId, long exceptRunId, int count,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("read failed");

        public Task<int> CountSuccessfulSinceAsync(string operationId, DateTime sinceUtc, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("read failed");
    }

    [Fact]
    public async Task R7_AC3_when_the_history_store_fails_on_a_failed_run_the_callers_error_is_unchanged()
    {
        var service = Service(new FakeRepository(Op("divide", "A / B")), new FakeRunRepository { Broken = true }, new ListLogger<RunRecorder>());

        var error = await Assert.ThrowsAsync<DomainException>(() => service.ExecuteAsync("divide", "5", "0"));

        Assert.Equal("Division by zero.", error.Message);
    }

    [Fact]
    public async Task R2_AC1_executes_a_formula_operation_and_returns_its_result()
    {
        var service = Service(new FakeRepository(Op("divide", "A / B")));

        var result = await service.ExecuteAsync("divide", "7", "2");

        Assert.Equal(("divide", "7", "2", "3.5"), (result.OperationId, result.FieldA, result.FieldB, result.Result));
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
