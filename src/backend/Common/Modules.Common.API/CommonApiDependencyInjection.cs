using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Modules.Common.API.ErrorHandling;
using Serilog;

namespace Modules.Common.API;

/// <summary>
/// Web-API services shared by every module: OpenAPI, problem details, JSON options.
/// </summary>
public static class CommonApiDependencyInjection
{
    /// <summary>
    /// Adds Swagger, the global exception handler and shared JSON settings.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCoreWebApiInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddEndpointsApiExplorer()
            .AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Team Project Board API",
                    Version = "v1"
                });

                // Why declare the scheme at all: without it Swagger UI has no "Authorize"
                // button, and every secured endpoint returns 401 when tried from the
                // browser. This is purely documentation — it does not enforce anything.
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Paste the JWT only, without the 'Bearer ' prefix.",
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    Scheme = "Bearer"
                });

                options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer")] = []
                });
            });

        // AddProblemDetails makes ASP.NET Core return RFC 7807 bodies for framework-level
        // failures (404, 415, 400 from model binding) so they match the shape our own
        // Result-to-problem mapping produces. Without it a client sees two different error
        // formats depending on where the failure happened.
        services
            .AddExceptionHandler<GlobalExceptionHandler>()
            .AddProblemDetails();

        // Why: enums serialize as their integer value by default, so a role would appear
        // as 0 in JSON. The converter emits "Admin" instead — self-describing to the
        // frontend and stable if the enum is ever reordered.
        services.Configure<JsonOptions>(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return services;
    }

    /// <summary>
    /// Replaces the default logger with Serilog, configured from appsettings.
    /// </summary>
    /// <param name="builder">The host builder being configured.</param>
    public static void AddCoreHostLogging(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Reading configuration rather than hard-coding sinks means log levels and
        // destinations change with an appsettings edit, no rebuild.
        builder.Host.UseSerilog((context, loggerConfiguration) =>
            loggerConfiguration.ReadFrom.Configuration(context.Configuration));
    }
}
