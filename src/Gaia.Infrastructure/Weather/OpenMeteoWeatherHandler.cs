using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gaia.Core.Errors;
using Gaia.Core.Formulas;
using Gaia.Core.Operations;

namespace Gaia.Infrastructure.Weather;

/// <summary>
/// The "weather" operation: today's forecast for Field A = latitude, Field B = longitude, from Open-Meteo
/// (free, no API key). "Today" is today at that location (timezone=auto). Every failure — error status,
/// unreachable host, unexpected answer, or no answer within the timeout — becomes an ExternalServiceException (R3.AC4).
/// </summary>
public sealed class OpenMeteoWeatherHandler(HttpClient http, WeatherOptions options) : IOperationHandler
{
    public string OperationId => "weather";

    public async Task<string> ExecuteAsync(Value a, Value b, CancellationToken cancellationToken)
    {
        var (latitude, longitude) = (((NumberValue)a).Number, ((NumberValue)b).Number);
        var url = string.Create(CultureInfo.InvariantCulture,
            $"v1/forecast?latitude={latitude}&longitude={longitude}&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=1");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        try
        {
            using var response = await http.GetAsync(url, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new ExternalServiceException($"The weather service returned an error (HTTP {(int)response.StatusCode}).");

            var forecast = await response.Content.ReadFromJsonAsync<Forecast>(timeout.Token);
            return Describe(forecast);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our timer fired, not the caller's cancellation.
            throw new ExternalServiceException(
                $"The weather service did not answer within {options.TimeoutSeconds} seconds.", ErrorCodes.ExternalServiceTimeout);
        }
        catch (HttpRequestException)
        {
            throw new ExternalServiceException("The weather service is unreachable.");
        }
        catch (JsonException)
        {
            throw UnexpectedAnswer();
        }
    }

    private static string Describe(Forecast? forecast)
    {
        if (forecast?.Daily is not { WeatherCode: [var code, ..], Max: [var max, ..], Min: [var min, ..] })
            throw UnexpectedAnswer();

        return string.Create(CultureInfo.InvariantCulture,
            $"{WeatherCodes.Describe(code)}, {Degrees(min)}–{Degrees(max)} °C");
    }

    // Whole degrees; "+ 0.0" turns -0 (from rounding -0.3) into 0.
    private static double Degrees(double celsius) => Math.Round(celsius) + 0.0;

    private static ExternalServiceException UnexpectedAnswer() =>
        new("The weather service returned an unexpected answer.");

    // Only the fields we use from Open-Meteo's JSON.
    private sealed record Forecast([property: JsonPropertyName("daily")] Daily? Daily);

    private sealed record Daily(
        [property: JsonPropertyName("weather_code")] int[]? WeatherCode,
        [property: JsonPropertyName("temperature_2m_max")] double[]? Max,
        [property: JsonPropertyName("temperature_2m_min")] double[]? Min);
}
