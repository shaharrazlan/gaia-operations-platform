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

/// <summary>Helpers to read the in-memory logs the way the two log files would be filtered.</summary>
internal static class Logs
{
    public static string CorrelationIdOf(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("X-Correlation-Id"));

    public static List<LogEvent> For(ListSink sink, string correlationId, Func<LogEvent, bool> file) =>
        sink.Events.Where(e => file(e)
            && e.Properties.TryGetValue("CorrelationId", out var id)
            && id is ScalarValue { Value: string s } && s == correlationId).ToList();
}

public class ErrorLogTests(LoggingFactory factory) : IClassFixture<LoggingFactory>
{
    private Task<HttpResponseMessage> ExecuteAsync(string id, string a, string b) =>
        factory.CreateClient().PostAsJsonAsync($"/api/operations/{id}/execute", new { fieldA = a, fieldB = b });

    [Fact]
    public async Task R10_AC1_an_unexpected_error_is_written_to_the_error_log_with_its_stack_and_the_caller_gets_no_details()
    {
        var response = await ExecuteAsync("weather", "32", "34");   // the test handler throws

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("internal_error", body);
        Assert.DoesNotContain("XYZ-123", body);       // the exception message stays on the server
        Assert.DoesNotContain("   at ", body);        // no stack trace

        var entry = Assert.Single(Logs.For(factory.Sink, Logs.CorrelationIdOf(response), Gaia.Api.Logging.LogSetup.IsErrorLog));
        Assert.Equal(LogEventLevel.Error, entry.Level);
        var exception = Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains("XYZ-123", exception.Message);
        Assert.NotNull(exception.StackTrace);
        Assert.Contains("/api/operations/weather/execute", entry.RenderMessage());
    }

    [Theory]
    [InlineData("divide", "abc", "2", HttpStatusCode.BadRequest)]           // validation
    [InlineData("nope", "1", "2", HttpStatusCode.NotFound)]                 // not found
    [InlineData("divide", "5", "0", HttpStatusCode.UnprocessableEntity)]    // division by zero
    public async Task R10_AC2_expected_business_errors_are_not_written_to_the_error_log(string id, string a, string b, HttpStatusCode status)
    {
        var response = await ExecuteAsync(id, a, b);

        Assert.Equal(status, response.StatusCode);
        Assert.Empty(Logs.For(factory.Sink, Logs.CorrelationIdOf(response), Gaia.Api.Logging.LogSetup.IsErrorLog));
    }
}

public class RequestLogTests(LoggingFactory factory) : IClassFixture<LoggingFactory>
{
    private static string Text(LogEvent e, string property) =>
        e.Properties.TryGetValue(property, out var value) && value is ScalarValue { Value: var v } ? v?.ToString() ?? "" : "";

    [Fact]
    public async Task R11_AC1_each_API_request_gets_one_entry_with_method_path_bodies_status_and_duration()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/operations/divide/execute", new { fieldA = "7", fieldB = "2" });

        var entry = Assert.Single(Logs.For(factory.Sink, Logs.CorrelationIdOf(response), Gaia.Api.Logging.LogSetup.IsRequestLog));
        Assert.Equal("POST", Text(entry, "Method"));
        Assert.Equal("/api/operations/divide/execute", Text(entry, "Path"));
        Assert.Equal("200", Text(entry, "StatusCode"));
        Assert.Contains("\"fieldA\":\"7\"", Text(entry, "RequestBody"));
        Assert.Contains("\"result\":\"3.5\"", Text(entry, "ResponseBody"));
        Assert.True(double.Parse(Text(entry, "DurationMs"), System.Globalization.CultureInfo.InvariantCulture) >= 0);
        Assert.Equal("3.5", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetString()); // the caller still gets the body
    }

    [Fact]
    public async Task R11_AC1_error_responses_are_logged_too()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/operations/divide/execute", new { fieldA = "5", fieldB = "0" });

        var entry = Assert.Single(Logs.For(factory.Sink, Logs.CorrelationIdOf(response), Gaia.Api.Logging.LogSetup.IsRequestLog));
        Assert.Equal("422", Text(entry, "StatusCode"));
        Assert.Contains("Division by zero.", Text(entry, "ResponseBody"));
    }

    [Fact]
    public async Task T48_non_API_requests_like_health_are_not_in_the_request_log()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Empty(Logs.For(factory.Sink, Logs.CorrelationIdOf(response), Gaia.Api.Logging.LogSetup.IsRequestLog));
    }

    [Fact]
    public async Task T48_long_bodies_are_cut_at_16_KB_in_the_log()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/operations/concat/execute", new { fieldA = new string('x', 40_000), fieldB = "y" });

        var entry = Assert.Single(Logs.For(factory.Sink, Logs.CorrelationIdOf(response), Gaia.Api.Logging.LogSetup.IsRequestLog));
        Assert.InRange(Text(entry, "RequestBody").Length, 16 * 1024, 16 * 1024 + 100);
        Assert.EndsWith("…(truncated)", Text(entry, "RequestBody"));
    }

    [Fact]
    public async Task R11_AC2_the_request_log_and_error_log_entries_of_one_request_share_its_correlation_id()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/operations/weather/execute", new { fieldA = "32", fieldB = "34" });
        var id = Logs.CorrelationIdOf(response);

        Assert.Single(Logs.For(factory.Sink, id, Gaia.Api.Logging.LogSetup.IsRequestLog));
        Assert.Single(Logs.For(factory.Sink, id, Gaia.Api.Logging.LogSetup.IsErrorLog));
    }

    [Fact]
    public async Task R13_AC7_the_management_key_is_logged_as_stars_and_its_text_is_in_no_log_line()
    {
        var response = await factory.CreateManagementClient().GetAsync("/api/operations?includeDisabled=true");

        var entry = Assert.Single(Logs.For(factory.Sink, Logs.CorrelationIdOf(response), Gaia.Api.Logging.LogSetup.IsRequestLog));
        Assert.Contains("X-Management-Key: ***", Text(entry, "RequestHeaders"));
        Assert.DoesNotContain(factory.Sink.Events, e =>
            e.RenderMessage().Contains(GaiaApiFactory.ManagementKey)
            || e.Properties.Values.Any(v => v.ToString().Contains(GaiaApiFactory.ManagementKey))
            || (e.Exception?.ToString().Contains(GaiaApiFactory.ManagementKey) ?? false));
    }
}
