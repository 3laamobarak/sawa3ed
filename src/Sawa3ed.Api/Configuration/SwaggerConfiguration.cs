using Microsoft.OpenApi;

namespace Sawa3ed.Api.Configuration;

internal static class SwaggerConfiguration
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo { Title = "Sawa3ed Foundation API", Version = "v1", Description = "Identity, private files, learning assistant, and health. Business-module endpoints are intentionally excluded." });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                Description = "Paste only the accessToken from POST /api/v1/auth/login."
            });
            o.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });
        return services;
    }
}
