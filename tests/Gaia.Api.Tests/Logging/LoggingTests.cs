using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaia.Core.Operations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Core;
using Serilog.Events;

namespace Gaia.Api.Tests.Logging;

/// <summary>Keeps every Serilog event in memory, so tests can read "the logs".</summary>
public sealed class ListSink : ILogEventSink
{
    public ConcurrentQueue<LogEvent> Events { get; } = new();

    public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
}

/// <summary>
/// The API with an in-memory log sink, and a "weather" handler that crashes — the one way to cause
/// an unexpected error without adding a test-only endpoint to the product.
/// </summary>
public sealed class LoggingFactory : GaiaApiFactory
{
    public ListSink Sink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILogEventSink>(Sink);
            services.RemoveAll<IOperationHandler>();
            services.AddScoped<IOperationHandler, CrashingWeatherHandler>();
        });
    }

    private sealed class CrashingWeatherHandler : IOperationHandler
    {
        public string OperationId => "weather";

        public Task<string> ExecuteAsync(Gaia.Core.Formulas.Value a, Gaia.Core.Formulas.Value b, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated bug: secret connection detail XYZ-123");
    }
}

public class CorrelationIdTests(LoggingFactory factory) : IClassFixture<LoggingFactory>
{
    [Theory]
    [InlineData("/api/operations")]
    [InlineData("/health")]
    [InlineData("/api/no-such-route")]
    public async Task T46_every_response_has_an_X_Correlation_Id_header(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.Matches("^[0-9a-f]{32}$", Assert.Single(values));
    }

    [Fact]
    public async Task T46_a_caller_supplied_correlation_id_is_kept()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/operations");
        request.Headers.Add("X-Correlation-Id", "order-42-abc");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal("order-42-abc", Assert.Single(response.Headers.GetValues("X-Correlation-Id")));
    }

    [Theory]
    [InlineData("has spaces in it")]
    [InlineData("<script>")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task T46_an_unsafe_caller_supplied_id_is_replaced(string unsafeId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/operations");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", unsafeId);

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Matches("^[0-9a-f]{32}$", Assert.Single(response.Headers.GetValues("X-Correlation-Id")));
    }

    [Fact]
    public async Task T46_error_bodies_carry_the_same_correlation_id_as_the_header()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/operations/nope/execute", new { fieldA = "1", fieldB = "2" });

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Assert.Single(response.Headers.GetValues("X-Correlation-Id")), problem.GetProperty("correlationId").GetString());
    }
}
