using Microsoft.AspNetCore.Authorization;
using Modules.Common.Infrastructure.Policies;
using Modules.Users.Domain.Policies;

namespace Modules.Users.Infrastructure.Policies;

/// <summary>
/// Declares the Users module's authorization policies.
/// </summary>
/// <remarks>
/// Each policy requires the presence of a claim with the same name. The claims reach the
/// caller's token because they are attached to their role at seed time, so "Admin can
/// delete users" is expressed as a role-claim row rather than as C# in an endpoint.
/// </remarks>
internal sealed class UsersPolicyFactory : IPolicyFactory
{
    /// <inheritdoc />
    public string ModuleName => "Users";

    /// <inheritdoc />
    public IReadOnlyDictionary<string, Action<AuthorizationPolicyBuilder>> GetPolicies() =>
        new Dictionary<string, Action<AuthorizationPolicyBuilder>>(StringComparer.Ordinal)
        {
            [UserPolicyConsts.Read] = policy => policy.RequireClaim(UserPolicyConsts.Read),
            [UserPolicyConsts.Create] = policy => policy.RequireClaim(UserPolicyConsts.Create),
            [UserPolicyConsts.Update] = policy => policy.RequireClaim(UserPolicyConsts.Update),
            [UserPolicyConsts.Delete] = policy => policy.RequireClaim(UserPolicyConsts.Delete)
        };
}
