using Microsoft.OpenApi.Models;

namespace Domus.Api.Swagger;

public static class SwaggerConfig
{
    public static IServiceCollection AddDomusSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Domus API",
                Version = "v1",
                Description =
                    "API multi-tenant Domus (auth, residences, devices, commands). " +
                    "Hub SignalR: /hubs/devices (JWT via query access_token). " +
                    "Contratos: docs/api-contracts.md e docs/realtime-signalr.md."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Access token JWT. Ex.: Bearer {token}"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
        });

        return services;
    }
}
