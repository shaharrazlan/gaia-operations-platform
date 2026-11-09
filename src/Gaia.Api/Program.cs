var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program;
