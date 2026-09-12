using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Defaults every service in the Aspire application shares.
/// </summary>
/// <remarks>
/// <para>
/// Aspire calls this the "service defaults" project. In a real distributed system it
/// would be referenced by every service so they all get identical health checks, retry
/// behaviour and telemetry — the cross-cutting concerns you would otherwise copy-paste
/// and then let drift apart.
/// </para>
/// <para>
/// We have one service today, so this looks like indirection for its own sake. It is kept
/// because it is where the SignalR service or a background worker would plug in later,
/// and because it is the shape Aspire tooling expects.
/// </para>
/// <para>
/// Note what is <b>not</b> here: OpenTelemetry. The stock Aspire template configures it in
/// this file, but our modules need tracing to include their own activity sources and
/// Npgsql, so it all lives in one place — <c>AddCoreInfrastructure</c> in
/// Modules.Common.Infrastructure. Configuring it in both would register the instrumentation
/// twice and duplicate every span.
/// </para>
/// </remarks>
public static class ServiceDefaultsExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    /// <summary>
    /// Adds health checks, service discovery and HTTP resilience.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddDefaultHealthChecks();

        // Service discovery lets code say "http://api" instead of "http://localhost:5193".
        // Aspire injects the real address at run time, so ports never appear in source.
        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Retries with exponential backoff, a circuit breaker and a timeout, applied
            // to every HttpClient. Transient network failures are normal, not exceptional.
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Registers the default liveness check.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps the health and liveness endpoints.
    /// </summary>
    /// <param name="app">The application to map endpoints onto.</param>
    /// <returns>The same application, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Two endpoints, because they answer different questions. <c>/alive</c> asks "is the
    /// process running?" — if it fails, restart the container. <c>/health</c> asks "can it
    /// serve traffic?", including its dependencies — if it fails, stop routing requests
    /// here but do not restart, because a restart will not fix a database that is down.
    /// </para>
    /// <para>
    /// Development only: these endpoints report dependency names and failure reasons, which
    /// is reconnaissance for an attacker. Exposing them in production needs authentication
    /// or a port only the orchestrator can reach.
    /// </para>
    /// </remarks>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);

            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains("live")
            });
        }

        return app;
    }
}
