namespace Modules.Users.Features.Users.Shared.Routes;

/// <summary>
/// Every route the Users module serves.
/// </summary>
/// <remarks>
/// Slices are self-contained by design, which makes it easy to end up with
/// <c>/api/users/{id}</c> in one file and <c>/api/user/{userId}</c> in another. Collecting
/// the routes here is the one deliberate exception: the module's whole URL surface is
/// visible in a single screen, and a base-path change is one edit.
/// </remarks>
internal static class RouteConsts
{
    private const string Base = "/api/users";

    /// <summary>POST — create an account.</summary>
    internal const string Register = $"{Base}/register";

    /// <summary>POST — exchange credentials for a token pair.</summary>
    internal const string Login = $"{Base}/login";

    /// <summary>POST — exchange an expired token pair for a fresh one.</summary>
    internal const string Refresh = $"{Base}/refresh";

    /// <summary>GET — the caller's own details.</summary>
    internal const string Me = $"{Base}/me";

    /// <summary>GET — any user's details. Requires users:read.</summary>
    internal const string GetById = $"{Base}/{{userId}}";

    /// <summary>PUT — update a user's profile. Requires users:update.</summary>
    internal const string Update = $"{Base}/{{userId}}";

    /// <summary>DELETE — remove a user. Requires users:delete.</summary>
    internal const string Delete = $"{Base}/{{userId}}";

    /// <summary>PUT — change a user's system role. Requires users:update.</summary>
    internal const string UpdateRole = $"{Base}/{{userId}}/role";

    /// <summary>The Swagger tag grouping these endpoints.</summary>
    internal const string Tag = "Users";
}
