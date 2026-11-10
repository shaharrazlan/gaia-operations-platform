// A34D
// Gaia Operations Platform — server entry point: registers services, prepares the database,
// and builds the HTTP pipeline. Business logic lives in Gaia.Core; data and weather in Gaia.Infrastructure.

using System.Text.Json.Serialization;
using Gaia.Api;
using Gaia.Api.Errors;
using Gaia.Api.Logging;
using Gaia.Api.Security;
using Gaia.Core.Errors;
using Gaia.Core.History;
using Gaia.Core.Operations;
using Gaia.Core.Services;
using Gaia.Infrastructure.Data;
using Gaia.Infrastructure.Weather;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging: Serilog, writing the error log and the request/response log (see LogSetup).
builder.Services.AddSerilog((services, logger) =>
    LogSetup.Configure(logger, builder.Configuration, services, builder.Environment.ContentRootPath));

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
builder.Services.AddScoped<IRunRepository, EfRunRepository>();
builder.Services.AddScoped<RunRecorder>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<ManagementKeyOptions>(builder.Configuration.GetSection(ManagementKeyOptions.Section));

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
            // Name the field: a JSON property ("$.fieldA" → "fieldA") or a query/route value ("includeDisabled").
            // The key of the body itself ("request") only means "the body isn't valid JSON".
            var failed = context.ModelState.Where(e => e.Value!.Errors.Count > 0).Select(e => e.Key).ToList();
            var bodyNames = context.ActionDescriptor.Parameters
                .Where(p => p.BindingInfo?.BindingSource == BindingSource.Body)
                .Select(p => p.Name);
            var field = failed.Where(k => k.StartsWith("$.", StringComparison.Ordinal)).Select(k => k[2..]).FirstOrDefault()
                ?? failed.FirstOrDefault(k => k.Length > 0 && !bodyNames.Contains(k));
            var problem = ProblemDetailsMapper.Problem(StatusCodes.Status400BadRequest, "Invalid request",
                field is null ? "The request body must be valid JSON (see /swagger for examples)." : $"'{field}' has a value of the wrong type or an unknown value.",
                ErrorCodes.Validation, field);
            ProblemDetailsMapper.AddCorrelationId(problem, context.HttpContext);
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

    // "Authorize" button: enter the management key once, Swagger sends it on add / change / remove (R13).
    swagger.AddSecurityDefinition(ManagementKeyOperationFilter.SchemeId, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = RequireManagementKeyAttribute.HeaderName,
        Description = "The management key (server setting Management:Key). Needed to add, change or remove operations.",
    });
    swagger.OperationFilter<ManagementKeyOperationFilter>();
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
    ProblemDetailsMapper.AddCorrelationId(context.ProblemDetails, context.HttpContext);
});
builder.Services.AddExceptionHandler<ProblemDetailsMapper>();

var app = builder.Build();

await DatabaseStartup.PrepareAsync(app);

// Closed by default: with no key, add / change / delete always answer 401 (R13.AC8). Say so loudly at startup.
if (!app.Services.GetRequiredService<IOptions<ManagementKeyOptions>>().Value.IsConfigured)
    app.Logger.LogWarning("No management key is configured (Management:Key), so adding, changing and removing operations is disabled. " +
        "Set the environment variable Management__Key to enable it.");

// --- HTTP pipeline (order matters: outer first) ---
app.UseMiddleware<CorrelationIdMiddleware>();            // id for this request, on every log line and in the response
app.UseMiddleware<RequestResponseLoggingMiddleware>();   // sees the final response, including error responses below
app.UseExceptionHandler();
app.UseStatusCodePages();   // empty 404/405 responses become ProblemDetails
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program;
