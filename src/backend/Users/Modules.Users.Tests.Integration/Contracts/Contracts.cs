namespace Modules.Users.Tests.Integration.Contracts;

// Why redeclare the request and response shapes instead of referencing the API's own
// records: these tests act as a client, and a client only has the JSON contract. Sharing
// the types would let a rename change both sides at once and the tests would still pass —
// hiding exactly the breaking change they exist to catch.

/// <summary>Mirrors the register endpoint's request body.</summary>
public sealed record RegisterUserRequest(string Email, string Password, string DisplayName);

/// <summary>Mirrors the login endpoint's request body.</summary>
public sealed record LoginUserRequest(string Email, string Password);

/// <summary>Mirrors the refresh endpoint's request body.</summary>
public sealed record RefreshTokenRequest(string AccessToken, string RefreshToken);

/// <summary>Mirrors the role endpoint's request body.</summary>
public sealed record UpdateUserRoleRequest(string Role);

/// <summary>Mirrors the update endpoint's request body.</summary>
public sealed record UpdateUserRequest(string DisplayName);

/// <summary>Mirrors the user response body.</summary>
public sealed record UserResponse(string Id, string Email, string DisplayName, IReadOnlyList<string> Roles);

/// <summary>Mirrors the login and refresh response body.</summary>
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
