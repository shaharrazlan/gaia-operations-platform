// A34D
// Gaia Operations Platform — server entry point: registers services, prepares the database,
// and builds the HTTP pipeline. Business logic lives in Gaia.Core; data and weather in Gaia.Infrastructure.

using System.Text.Json.Serialization;
using Gaia.Api;
using Gaia.Api.Errors;
using Gaia.Core.Errors;
using Gaia.Core.Operations;
using Gaia.Core.Services;
using Gaia.Infrastructure.Data;
using Gaia.Infrastructure.Weather;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

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
switch (weather.Provider?.ToLowerInvariant())
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
            // Prefer an error that points at a JSON field ("$.fieldA" → "fieldA") over the generic body error.
            var field = context.ModelState
                .Where(e => e.Value!.Errors.Count > 0 && e.Key.StartsWith("$.", StringComparison.Ordinal))
                .Select(e => e.Key[2..])
                .FirstOrDefault();
            var problem = ProblemDetailsMapper.Problem(StatusCodes.Status400BadRequest, "Invalid request",
                field is null ? "The request body must be JSON like {\"fieldA\": \"7\", \"fieldB\": \"2\"}." : $"'{field}' must be text or a number.",
                ErrorCodes.Validation, field);
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        });

// Swagger UI at /swagger: lets anyone (reviewers, external developers) read and try the API in a browser.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swagger =>
{
    swagger.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Gaia Operations Platform API",
        Version = "v1",
        Description = "Run operations on two inputs (Field A, Field B), and list or manage the operation catalog.",
    });
    swagger.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Gaia.Api.xml"));
});

// Every error becomes a ProblemDetails response with a "code" (see ProblemDetailsMapper).
builder.Services.AddProblemDetails(problems => problems.CustomizeProblemDetails = context =>
{
    // Errors produced by the framework itself (unknown route, wrong HTTP method) get a code too.
    if (!context.ProblemDetails.Extensions.ContainsKey("code"))
        context.ProblemDetails.Extensions["code"] = context.ProblemDetails.Status switch
        {
            StatusCodes.Status404NotFound => ErrorCodes.RouteNotFound,
            StatusCodes.Status405MethodNotAllowed => ErrorCodes.MethodNotAllowed,
            _ => ErrorCodes.HttpError,
        };
});
builder.Services.AddExceptionHandler<ProblemDetailsMapper>();

var app = builder.Build();

await DatabaseStartup.PrepareAsync(app);

// --- HTTP pipeline ---
app.UseExceptionHandler();
app.UseStatusCodePages();   // empty 404/405 responses become ProblemDetails
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program;
