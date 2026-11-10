using Gaia.Core.Formulas;
using Gaia.Infrastructure.Data;

namespace Gaia.Core.Tests.Operations;

public class SeedOperationsTests
{
    [Fact]
    public void R4_AC8_every_seeded_formula_is_valid_with_its_own_input_type()
    {
        var failures = new List<string>();
        foreach (var operation in SeedOperations.Create().Where(o => o.Formula is not null))
        {
            try { FormulaEngine.Validate(operation.Formula!, operation.InputType); }
            catch (Exception e) { failures.Add($"{operation.Id}: {e.Message}"); }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void T15_only_weather_is_a_code_handler_and_it_is_a_system_operation()
    {
        var codeHandlers = SeedOperations.Create().Where(o => o.Formula is null).ToList();

        var weather = Assert.Single(codeHandlers);
        Assert.Equal("weather", weather.Id);
        Assert.True(weather.IsSystem);
    }
}
