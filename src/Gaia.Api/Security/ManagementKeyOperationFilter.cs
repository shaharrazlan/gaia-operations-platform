using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Gaia.Api.Security;

/// <summary>
/// Swagger: marks every action with [RequireManagementKey] as needing the key, so the Swagger UI
/// "Authorize" button sends X-Management-Key on exactly those calls (keeps R4.AC5 usable from a browser).
/// </summary>
public sealed class ManagementKeyOperationFilter : IOperationFilter
{
    public const string SchemeId = "ManagementKey";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.GetCustomAttribute<RequireManagementKeyAttribute>() is not { } attribute)
            return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeId, context.Document)] = [],
        });

        var when = attribute.OnlyWhenQueryIsTrue is { } name ? $" (only with {name}=true)" : "";
        operation.Responses ??= new OpenApiResponses();
        operation.Responses["401"] = new OpenApiResponse
        {
            Description = $"unauthorized — missing or wrong {RequireManagementKeyAttribute.HeaderName}{when}.",
        };
    }
}
