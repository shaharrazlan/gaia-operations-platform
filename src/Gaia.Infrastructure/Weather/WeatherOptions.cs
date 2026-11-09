namespace Gaia.Infrastructure.Weather;

/// <summary>The "Weather" section of appsettings.json.</summary>
public sealed class WeatherOptions
{
    /// <summary>"OpenMeteo" (real forecasts) or "Fake" (fixed answer, for E2E tests and offline demos).</summary>
    public string Provider { get; set; } = "OpenMeteo";

    public string BaseUrl { get; set; } = "https://api.open-meteo.com/";

    /// <summary>How long to wait for the weather service before giving up (NFR2).</summary>
    public double TimeoutSeconds { get; set; } = 5;
}
