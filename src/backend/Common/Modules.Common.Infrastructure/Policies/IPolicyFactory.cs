using Microsoft.AspNetCore.Authorization;

namespace Modules.Common.Infrastructure.Policies;

/// <summary>
/// Supplies the authorization policies owned by one module.
/// </summary>
/// <remarks>
/// <para>
/// Authorization policies are normally all declared in <c>Program.cs</c>, which puts a
/// module's access rules in a file the module does not own — the boundary leaks, and the
/// host grows a line for every permission anyone adds.
/// </para>
/// <para>
/// Instead each module implements this interface, and
/// <see cref="AuthorizationConfigureOptions"/> collects them all at startup. A module
/// defines its own permissions and the host never learns their names.
/// </para>
/// </remarks>
public interface IPolicyFactory
{
    /// <summary>Gets the module's name, used only for startup logging.</summary>
    string ModuleName { get; }

    /// <summary>
    /// Gets this module's policies, keyed by policy name.
    /// </summary>
    /// <returns>A map of policy name to the builder action that configures it.</returns>
    IReadOnlyDictionary<string, Action<AuthorizationPolicyBuilder>> GetPolicies();
}
