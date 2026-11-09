using System.Text.Json.Serialization;
using Gaia.Api.Errors;
using Gaia.Core.Errors;
using Gaia.Core.Operations;
using Gaia.Core.Services;
using Gaia.Infrastructure.Data;
using Gaia.Infrastructure.Weather;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddDbContext<GaiaDbContext>((services, options) =>
{
    // Read when the first DbContext is created, so test hosts can override it.
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("Gaia")
        ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Gaia' in appsettings.json.");
    options.UseSqlServer(connectionString);
});
builder.Services.AddScoped<IOperationRepository, EfOperationRepository>();
builder.Services.AddScoped<OperationService>();

// Code operations (IOperationHandler). Today only weather: real Open-Meteo, or a fixed fake answer.
var weather = builder.Configuration.GetSection("Weather").Get<WeatherOptions>() ?? new WeatherOptions();
builder.Services.AddSingleton(weather);
switch (weather.Provider.ToLowerInvariant())
{
    case "openmeteo":
        builder.Services.AddHttpClient<OpenMeteoWeatherHandler>(http => http.BaseAddress = new Uri(weather.BaseUrl));
        builder.Services.AddScoped<IOperationHandler>(services => services.GetRequiredService<OpenMeteoWeatherHandler>());
        break;
    case "fake":
        builder.Services.AddScoped<IOperationHandler, FakeWeatherHandler>();
        break;
    default:
        throw new InvalidOperationException($"Unknown Weather:Provider '{weather.Provider}'. Use 'OpenMeteo' or 'Fake'.");
}

builder.Services.AddControllers()
    .AddJsonOptions(json => json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(api =>
        // A body that isn't valid JSON gets the same shape and "code" as every other error (R2.AC6).
        api.InvalidModelStateResponseFactory = context =>
        {
            var (field, errors) = context.ModelState.First(entry => entry.Value!.Errors.Count > 0);
            var problem = ProblemDetailsMapper.Problem(StatusCodes.Status400BadRequest, "Invalid request",
                errors!.Errors[0].ErrorMessage is { Length: > 0 } message ? message : "The request body is not valid JSON.",
                ErrorCodes.Validation, field);
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        });

// Every error becomes a ProblemDetails response with a "code" (see ProblemDetailsMapper).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsMapper>();

var app = builder.Build();

// --- Database ---
// Development and E2E create/upgrade their own database, so nobody has to run database commands.
// E2E starts from an empty database every run, so end-to-end tests are repeatable.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("E2E"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GaiaDbContext>();
    if (app.Environment.IsEnvironment("E2E"))
        await db.Database.EnsureDeletedAsync();
    await db.Database.MigrateAsync();

    // Fail fast if an operation in the database has neither a formula nor a C# handler.
    HandlerCheck.EnsureEveryCodeOperationHasAHandler(
        await db.Operations.AsNoTracking().ToListAsync(),
        scope.ServiceProvider.GetServices<IOperationHandler>());
}

// --- HTTP pipeline ---
app.UseExceptionHandler();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program;
