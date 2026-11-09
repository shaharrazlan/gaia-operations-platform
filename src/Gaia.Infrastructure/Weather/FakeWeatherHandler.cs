using Gaia.Core.Formulas;
using Gaia.Core.Operations;

namespace Gaia.Infrastructure.Weather;

/// <summary>
/// Same operation id as the real handler, but a fixed answer and no network.
/// Selected with Weather:Provider = "Fake" (E2E tests, offline demos).
/// </summary>
public sealed class FakeWeatherHandler : IOperationHandler
{
    public string OperationId => "weather";

    public Task<string> ExecuteAsync(Value a, Value b, CancellationToken cancellationToken) =>
        Task.FromResult("Clear sky, 20–25 °C (sample data)");
}
