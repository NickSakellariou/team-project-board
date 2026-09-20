namespace Modules.Common.Infrastructure.Configuration;

/// <summary>
/// JWT settings, bound from the <c>AuthConfiguration</c> section of appsettings.
/// </summary>
/// <remarks>
/// Binding configuration to a class rather than reading
/// <c>configuration["AuthConfiguration:Key"]</c> at each use site gives compile-time
/// names and one place to see what must be configured for auth to work.
/// </remarks>
public sealed class AuthConfiguration
{
    /// <summary>
    /// Gets or sets the secret used to sign tokens.
    /// </summary>
    /// <remarks>
    /// Symmetric, so the same value both signs and verifies — anyone holding it can mint
    /// tokens this API will trust. It belongs in user secrets or an environment variable,
    /// never in a committed appsettings file. The development value in this repo is a
    /// placeholder and must not be reused anywhere real.
    /// </remarks>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets who issued the token; checked on every request.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Gets or sets who the token is for; checked on every request.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how long an access token stays valid, in minutes.
    /// </summary>
    /// <remarks>
    /// Kept short deliberately. A JWT cannot be revoked once issued, so its lifetime is
    /// the window in which a stolen token still works. Short lifetime plus a refresh
    /// token is the standard way to bound that window without making users log in again
    /// every few minutes.
    /// </remarks>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Gets or sets how long a refresh token stays valid, in days.</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
