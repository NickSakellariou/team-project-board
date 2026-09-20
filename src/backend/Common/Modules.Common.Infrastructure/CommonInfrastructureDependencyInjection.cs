using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Modules.Common.Infrastructure.Configuration;
using Modules.Common.Infrastructure.Policies;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Cross-cutting infrastructure: authentication, authorization and telemetry.
/// </summary>
public static class CommonInfrastructureDependencyInjection
{
    /// <summary>
    /// Adds JWT authentication, module-contributed authorization policies and OpenTelemetry.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Configuration holding the <c>AuthConfiguration</c> section.</param>
    /// <param name="activitySourceNames">Module activity sources to include in tracing.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCoreInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        params string[] activitySourceNames)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddMemoryCache();

        services.AddJwtAuthentication(configuration);
        services.AddClaimsAuthorization();
        services.AddHostOpenTelemetry(activitySourceNames);

        return services;
    }

    private static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthConfiguration>()
            .Bind(configuration.GetSection(nameof(AuthConfiguration)))
            // Why ValidateOnStart: without it a missing signing key surfaces as a
            // confusing 500 on the first login attempt. With it, the app refuses to start
            // and says exactly what is missing.
            .Validate(auth => !string.IsNullOrWhiteSpace(auth.Key), "AuthConfiguration:Key must be configured.")
            .Validate(auth => !string.IsNullOrWhiteSpace(auth.Issuer), "AuthConfiguration:Issuer must be configured.")
            .Validate(auth => !string.IsNullOrWhiteSpace(auth.Audience), "AuthConfiguration:Audience must be configured.")
            .ValidateOnStart();

        var signingKey = configuration["AuthConfiguration:Key"]
                         ?? throw new InvalidOperationException("AuthConfiguration:Key is not configured.");

        // These parameters are the entire contract for trusting an incoming token. Each
        // check matters:
        //   IssuerSigningKey  - proves the token was minted by us and not altered since
        //   Issuer / Audience - stops a validly signed token from another system being
        //                       replayed here (relevant when one key is shared)
        //   Lifetime          - enforces expiry, which is what makes short-lived tokens
        //                       worth anything
        // ClockSkew defaults to five minutes of leniency; five seconds is plenty when
        // client and server both use NTP, and it keeps the revocation window honest.
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = configuration["AuthConfiguration:Issuer"],
            ValidAudience = configuration["AuthConfiguration:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromSeconds(5)
        };

        // Registered as a singleton because the refresh-token flow needs to validate an
        // *expired* access token with the same rules, minus the lifetime check.
        services.AddSingleton(tokenValidationParameters);

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options => options.TokenValidationParameters = tokenValidationParameters);
    }

    private static void AddClaimsAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IConfigureOptions<AuthorizationOptions>, AuthorizationConfigureOptions>();
        services.AddAuthorization();
    }

    private static void AddHostOpenTelemetry(this IServiceCollection services, params string[] activitySourceNames)
    {
        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("TeamProjectBoard"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    // Why: without Npgsql instrumentation a trace shows "this request took
                    // 800ms" but not that 780ms of it was one query. Database spans are
                    // usually where the answer is.
                    .AddNpgsql();

                if (activitySourceNames.Length > 0)
                {
                    tracing.AddSource(activitySourceNames);
                }

                tracing.AddOtlpExporter();
            });
    }
}
