using Gaia.Api.Security;
using Microsoft.OpenApi;

namespace Gaia.Api.Extensions;

/// <summary>Swagger UI at /swagger: lets anyone (reviewers, external developers) read and try the API in a browser.</summary>
public static class SwaggerExtensions
{
    public static IServiceCollection AddGaiaSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(swagger =>
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
        return services;
    }
}
