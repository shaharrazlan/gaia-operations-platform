using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Gaia.Infrastructure.Weather;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.Api.Tests.Api;

public class WeatherTests(GaiaApiFactory factory) : IClassFixture<GaiaApiFactory>
{
    private sealed class StubHttp(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(cancellationToken);
    }

    /// <summary>The real API, with Open-Meteo replaced by a stub at the HTTP boundary.</summary>
    private HttpClient ClientWithWeather(StubHttp stub, double timeoutSeconds = 5) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<OpenMeteoWeatherHandler>().ConfigurePrimaryHttpMessageHandler(() => stub);
            services.AddSingleton(new WeatherOptions { TimeoutSeconds = timeoutSeconds });
        })).CreateClient();

    private static StubHttp Answer(HttpStatusCode status, string body) =>
        new(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

    private static Task<HttpResponseMessage> ExecuteWeather(HttpClient client) =>
        client.PostAsJsonAsync("/api/operations/weather/execute", new { fieldA = "32.08", fieldB = "34.78" });

    [Fact]
    public async Task R3_AC3_execute_weather_returns_the_forecast_line()
    {
        var stub = Answer(HttpStatusCode.OK,
            """{"daily":{"weather_code":[2],"temperature_2m_max":[21.3],"temperature_2m_min":[12.1]}}""");

        var response = await ExecuteWeather(ClientWithWeather(stub));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Partly cloudy, 12–21 °C", body.GetProperty("result").GetString());
    }

    [Fact]
    public async Task R3_AC4_provider_failure_returns_502_external_service_error()
    {
        var response = await ExecuteWeather(ClientWithWeather(Answer(HttpStatusCode.InternalServerError, "down")));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("external_service_error", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task R3_AC4_provider_timeout_returns_504_external_service_timeout()
    {
        var slow = new StubHttp(async token =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var response = await ExecuteWeather(ClientWithWeather(slow, timeoutSeconds: 0.2));

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("external_service_timeout", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task R2_AC4_latitude_out_of_range_is_rejected_before_calling_the_provider()
    {
        var neverCalled = new StubHttp(_ => throw new InvalidOperationException("The provider must not be called."));

        var response = await ClientWithWeather(neverCalled)
            .PostAsJsonAsync("/api/operations/weather/execute", new { fieldA = "95", fieldB = "34.78" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
