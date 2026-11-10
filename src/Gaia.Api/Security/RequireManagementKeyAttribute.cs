using System.Security.Cryptography;
using System.Text;
using Gaia.Api.Errors;
using Gaia.Core.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Gaia.Api.Security;

/// <summary>
/// Put on an action that changes the catalog: the request must carry the header
/// <c>X-Management-Key</c> with the configured key, or it gets 401 unauthorized before the action runs (R13.AC1).
/// Listing enabled operations and executing them stay open (R13.AC3).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireManagementKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Management-Key";

    /// <summary>
    /// When set, the key is needed only if this query value is "true" — e.g. the full list
    /// <c>GET /api/operations?includeDisabled=true</c> needs it, the end-user list doesn't.
    /// </summary>
    public string? OnlyWhenQueryIsTrue { get; init; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        // Look at every value: "?x=true&x=true" must not slip through as the unparsable "true,true".
        if (OnlyWhenQueryIsTrue is { } name && !request.Query[name].Any(value => bool.TryParse(value, out var on) && on))
            return;

        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<ManagementKeyOptions>>().Value;
        if (options.IsConfigured && Matches(request.Headers[HeaderName].ToString(), options.Key!))
            return;

        var problem = ProblemDetailsMapper.Problem(StatusCodes.Status401Unauthorized, "Unauthorized",
            $"A valid management key is required in the '{HeaderName}' header to add, change or remove operations.",
            ErrorCodes.Unauthorized);
        ProblemDetailsMapper.AddCorrelationId(problem, context.HttpContext);
        // HTTP requires a 401 to say how to authenticate.
        context.HttpContext.Response.Headers.WWWAuthenticate = $"ApiKey header=\"{HeaderName}\"";
        context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status401Unauthorized, ContentTypes = { "application/problem+json" } };
    }

    /// <summary>
    /// Compares in constant time, so response timing reveals nothing about how much of a guess was right.
    /// Hashing first gives both sides the same length (FixedTimeEquals would otherwise exit early on a length mismatch).
    /// </summary>
    private static bool Matches(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
