// Gaia Operations Platform — server entry point: registers services, prepares the database,
// and builds the HTTP pipeline. Business logic lives in Gaia.Core; data and weather in Gaia.Infrastructure.
// The details of each registration are in the Extensions folder.

using Gaia.Api.Extensions;
using Gaia.Api.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging: Serilog, writing the error log and the request/response log (see LogSetup).
builder.Services.AddSerilog((services, logger) =>
    LogSetup.Configure(logger, builder.Configuration, services, builder.Environment.ContentRootPath));

// --- Services ---
builder.Services
    .AddGaiaDatabase()                            // SQL Server via EF Core
    .AddGaiaServices(builder.Configuration)       // repositories, business services, clock, management key
    .AddGaiaWeather(builder.Configuration)        // the weather operation: Open-Meteo or a fake
    .AddGaiaControllers()                         // controllers, JSON, bad-request format
    .AddGaiaSwagger()                             // API documentation at /swagger
    .AddGaiaErrorHandling();                      // every error as ProblemDetails with a "code"

var app = builder.Build();

await app.PrepareDatabaseAsync();                // create/upgrade the database (Development, E2E)
app.WarnIfNoManagementKey();

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
