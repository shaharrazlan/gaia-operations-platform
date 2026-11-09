using Gaia.Core.Formulas;
using Gaia.Infrastructure.Data;

namespace Gaia.Core.Tests.Operations;

public class SeedOperationsTests
{
    [Fact]
    public void T15_every_seeded_formula_is_valid()
    {
        foreach (var operation in SeedOperations.All.Where(o => o.Formula is not null))
            FormulaEngine.Validate(operation.Formula!);
    }

    [Fact]
    public void T15_only_weather_is_a_code_handler_and_it_is_a_system_operation()
    {
        var codeHandlers = SeedOperations.All.Where(o => o.Formula is null).ToList();

        var weather = Assert.Single(codeHandlers);
        Assert.Equal("weather", weather.Id);
        Assert.True(weather.IsSystem);
    }
}
