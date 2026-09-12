using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Common.Infrastructure.Policies;

/// <summary>
/// Collects every module's <see cref="IPolicyFactory"/> into the app's
/// <see cref="AuthorizationOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>IConfigureOptions&lt;T&gt;</c> is the framework's extension point for contributing
/// to a settings object that something else owns. Registering one lets us add policies
/// after the DI container is built — which matters here, because the policies come from
/// services (the factories) that do not exist yet when <c>AddAuthorization()</c> is called.
/// </para>
/// <para>
/// See docs/concepts/dependency-injection.md for the pattern and
/// docs/concepts/authentication-and-jwt.md for what the policies themselves do.
/// </para>
/// </remarks>
internal sealed class AuthorizationConfigureOptions(
    IEnumerable<IPolicyFactory> policyFactories,
    ILogger<AuthorizationConfigureOptions> logger)
    : IConfigureOptions<AuthorizationOptions>
{
    /// <inheritdoc />
    public void Configure(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var factory in policyFactories)
        {
            var policies = factory.GetPolicies();
            var policyCount = policies.Count;

            foreach (var (policyName, configurePolicy) in policies)
            {
                options.AddPolicy(policyName, configurePolicy);
            }

            logger.LogInformation(
                "Registered {PolicyCount} authorization policies for module {ModuleName}",
                policyCount,
                factory.ModuleName);
        }
    }
}
