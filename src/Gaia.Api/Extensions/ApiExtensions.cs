using System.Text.Json.Serialization;
using Gaia.Api.Errors;
using Gaia.Api.Security;
using Gaia.Core.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace Gaia.Api.Extensions;

/// <summary>How the API speaks HTTP: controllers, JSON, and one error format for everything.</summary>
public static class ApiExtensions
{
    /// <summary>Controllers, enums as words in JSON, and bad request bodies answered in the Gaia error format.</summary>
    public static IServiceCollection AddGaiaControllers(this IServiceCollection services)
    {
        services.AddControllers()
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
        return services;
    }

    /// <summary>Every error becomes a ProblemDetails response with a "code" (see ProblemDetailsMapper).</summary>
    public static IServiceCollection AddGaiaErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(problems => problems.CustomizeProblemDetails = context =>
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
        services.AddExceptionHandler<ProblemDetailsMapper>();
        return services;
    }

    /// <summary>Closed by default: with no key, add / change / delete always answer 401 (R13.AC8). Say so loudly at startup.</summary>
    public static void WarnIfNoManagementKey(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<IOptions<ManagementKeyOptions>>().Value.IsConfigured)
            app.Logger.LogWarning("No management key is configured (Management:Key), so adding, changing and removing operations is disabled. " +
                "Set the environment variable Management__Key to enable it.");
    }
}
