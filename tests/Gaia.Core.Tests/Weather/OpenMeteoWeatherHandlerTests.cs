using System.Net;
using System.Text;
using Gaia.Core.Errors;
using Gaia.Core.Formulas;
using Gaia.Infrastructure.Weather;

namespace Gaia.Core.Tests.Weather;

public class OpenMeteoWeatherHandlerTests
{
    private const string TelAvivForecast = """
        {"daily":{"time":["2026-11-09"],"weather_code":[2],"temperature_2m_max":[21.3],"temperature_2m_min":[12.1]}}
        """;

    /// <summary>Stands in for the network: returns whatever the test says, records the request.</summary>
    private sealed class StubHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public Uri? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request.RequestUri;
            return respond(request, cancellationToken);
        }
    }

    private static StubHttp Returns(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

    private static OpenMeteoWeatherHandler Handler(HttpMessageHandler http, double timeoutSeconds = 5) =>
        new(new HttpClient(http) { BaseAddress = new Uri("https://api.open-meteo.com/") },
            new WeatherOptions { TimeoutSeconds = timeoutSeconds });

    private static Task<string> Run(OpenMeteoWeatherHandler handler, CancellationToken cancellationToken = default) =>
        handler.ExecuteAsync(new NumberValue(32.08), new NumberValue(34.78), cancellationToken);

    [Fact]
    public async Task R3_AC3_maps_the_Open_Meteo_forecast_to_one_line()
    {
        var result = await Run(Handler(Returns(HttpStatusCode.OK, TelAvivForecast)));

        Assert.Equal("Partly cloudy, 12–21 °C", result);
    }

    [Fact]
    public async Task R3_AC3_near_zero_temperatures_never_show_minus_zero()
    {
        const string cold = """{"daily":{"weather_code":[71],"temperature_2m_max":[2.2],"temperature_2m_min":[-0.3]}}""";

        Assert.Equal("Snow, 0–2 °C", await Run(Handler(Returns(HttpStatusCode.OK, cold))));
    }

    [Fact]
    public async Task R3_AC3_asks_for_today_at_the_given_coordinates_in_local_time()
    {
        var http = Returns(HttpStatusCode.OK, TelAvivForecast);

        await Run(Handler(http));

        var query = http.LastRequest!.Query;
        Assert.Contains("latitude=32.08", query);
        Assert.Contains("longitude=34.78", query);
        Assert.Contains("timezone=auto", query);
        Assert.Contains("forecast_days=1", query);
    }

    [Fact]
    public async Task R3_AC4_HTTP_500_is_an_external_service_error()
    {
        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => Run(Handler(Returns(HttpStatusCode.InternalServerError, "oops"))));

        Assert.Equal(ErrorCodes.ExternalService, error.Code);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"daily":{"weather_code":[],"temperature_2m_max":[],"temperature_2m_min":[]}}""")]
    public async Task R3_AC4_malformed_or_incomplete_answer_is_an_external_service_error(string body)
    {
        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => Run(Handler(Returns(HttpStatusCode.OK, body))));

        Assert.Equal(ErrorCodes.ExternalService, error.Code);
    }

    [Fact]
    public async Task R3_AC4_unreachable_service_is_an_external_service_error()
    {
        var unreachable = new StubHttp((_, _) => throw new HttpRequestException("No such host is known."));

        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => Run(Handler(unreachable)));

        Assert.Contains("unreachable", error.Message);
    }

    [Fact]
    public async Task NFR2_no_answer_within_the_timeout_is_external_service_timeout()
    {
        var slow = new StubHttp(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => Run(Handler(slow, timeoutSeconds: 0.1)));

        Assert.Equal(ErrorCodes.ExternalServiceTimeout, error.Code);
    }

    [Fact]
    public void NFR2_default_timeout_is_5_seconds()
    {
        Assert.Equal(5, new WeatherOptions().TimeoutSeconds);
    }

    [Fact]
    public async Task T21_caller_cancellation_is_not_reported_as_a_timeout()
    {
        var slow = new StubHttp(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(Handler(slow), cancelled.Token));
    }

    [Theory]
    [InlineData(0, "Clear sky")]
    [InlineData(3, "Overcast")]
    [InlineData(63, "Rain")]
    [InlineData(95, "Thunderstorm")]
    [InlineData(42, "Unknown conditions")]
    public void T21_weather_codes_are_described_in_words(int code, string expected)
    {
        Assert.Equal(expected, WeatherCodes.Describe(code));
    }
}
