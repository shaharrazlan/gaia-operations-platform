using Gaia.Core.Formulas;
using Gaia.Core.Operations;
using Gaia.Core.Services;

namespace Gaia.Core.Tests.Services;

public class HandlerCheckTests
{
    private sealed class Handler(string id) : IOperationHandler
    {
        public string OperationId => id;
        public Task<string> ExecuteAsync(Value a, Value b, CancellationToken cancellationToken) => Task.FromResult("");
    }

    private static OperationDefinition Op(string id, string? formula) =>
        new() { Id = id, Name = id, FieldALabel = "A", FieldBLabel = "B", Formula = formula };

    [Fact]
    public void T22_startup_check_fails_fast_when_a_handler_is_missing()
    {
        var operations = new[] { Op("add", "A + B"), Op("weather", null), Op("rates", null) };

        var error = Assert.Throws<InvalidOperationException>(() =>
            HandlerCheck.EnsureEveryCodeOperationHasAHandler(operations, [new Handler("weather")]));

        Assert.Contains("rates", error.Message);
        Assert.DoesNotContain("weather", error.Message);
    }

    [Fact]
    public void T22_two_handlers_for_the_same_operation_fail_the_startup_check()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            HandlerCheck.EnsureEveryCodeOperationHasAHandler([Op("weather", null)], [new Handler("weather"), new Handler("weather")]));

        Assert.Contains("More than one handler", error.Message);
    }

    [Fact]
    public void T22_a_deleted_code_operation_does_not_need_a_handler()
    {
        var retired = Op("rates", null);
        retired.IsDeleted = true;

        HandlerCheck.EnsureEveryCodeOperationHasAHandler([retired], []);
    }

    [Fact]
    public void T22_startup_check_passes_when_every_code_operation_has_a_handler()
    {
        HandlerCheck.EnsureEveryCodeOperationHasAHandler([Op("add", "A + B"), Op("weather", null)], [new Handler("weather")]);
    }
}
