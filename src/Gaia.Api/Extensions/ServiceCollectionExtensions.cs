using Gaia.Api.Security;
using Gaia.Core.History;
using Gaia.Core.Operations;
using Gaia.Core.Services;
using Gaia.Infrastructure.Data;
using Gaia.Infrastructure.Weather;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Api.Extensions;

/// <summary>Registers what the application needs: the database, the business services, and weather.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>The SQL Server database (EF Core), using the connection string "Gaia" from appsettings.json.</summary>
    public static IServiceCollection AddGaiaDatabase(this IServiceCollection services) =>
        services.AddDbContext<GaiaDbContext>((provider, options) =>
        {
            // Read when the first DbContext is created, so test hosts can override it.
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Gaia")
                ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Gaia' in appsettings.json.");
            options.UseSqlServer(connectionString);
        });

    /// <summary>Repositories, business services, the clock, and the management-key setting.</summary>
    public static IServiceCollection AddGaiaServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IOperationRepository, EfOperationRepository>();
        services.AddScoped<OperationService>();
        services.AddScoped<IRunRepository, EfRunRepository>();
        services.AddScoped<RunRecorder>();
        services.AddScoped<CatalogService>();
        services.AddSingleton(TimeProvider.System);
        services.Configure<ManagementKeyOptions>(configuration.GetSection(ManagementKeyOptions.Section));
        return services;
    }

    /// <summary>Code operations (IOperationHandler). Today only weather: real Open-Meteo, or a fixed fake answer.</summary>
    public static IServiceCollection AddGaiaWeather(this IServiceCollection services, IConfiguration configuration)
    {
        var weather = configuration.GetSection("Weather").Get<WeatherOptions>() ?? new WeatherOptions();
        services.AddSingleton(weather);
        switch (weather.Provider?.ToLowerInvariant())
        {
            case "openmeteo":
                services.AddHttpClient<OpenMeteoWeatherHandler>(http => http.BaseAddress = new Uri(weather.BaseUrl));
                services.AddScoped<IOperationHandler>(provider => provider.GetRequiredService<OpenMeteoWeatherHandler>());
                break;
            case "fake":
                services.AddScoped<IOperationHandler, FakeWeatherHandler>();
                break;
            default:
                throw new InvalidOperationException($"Unknown Weather:Provider '{weather.Provider}'. Use 'OpenMeteo' or 'Fake'.");
        }
        return services;
    }
}
