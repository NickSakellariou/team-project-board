using Modules.Users.Features.Users.LoginUser;
using Modules.Users.Features.Users.RefreshToken;
using Modules.Users.Features.Users.RegisterUser;
using Modules.Users.Features.Users.UpdateUser;
using Modules.Users.Features.Users.UpdateUserRole;

namespace Modules.Users.Tests.Unit.Users;

/// <summary>
/// Covers the FluentValidation rules.
/// </summary>
/// <remarks>
/// Validators are pure functions over a request object, which makes them the cheapest
/// thing in the codebase to test and the easiest to get subtly wrong — an inverted rule
/// looks identical to a correct one at a glance.
/// </remarks>
public class ValidatorTests
{
    private readonly RegisterUserRequestValidator _registerValidator = new();
    private readonly LoginUserRequestValidator _loginValidator = new();
    private readonly UpdateUserRoleRequestValidator _roleValidator = new();
    private readonly UpdateUserRequestValidator _updateValidator = new();
    private readonly RefreshTokenRequestValidator _refreshValidator = new();

    [Fact]
    public void RegisterUser_WithValidRequest_Passes()
    {
        var result = _registerValidator.Validate(
            new RegisterUserRequest("nick@example.com", "Password1", "Nick"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Password1", "Nick")]
    [InlineData("not-an-email", "Password1", "Nick")]
    [InlineData("nick@example.com", "short", "Nick")]
    [InlineData("nick@example.com", "", "Nick")]
    [InlineData("nick@example.com", "Password1", "")]
    public void RegisterUser_WithInvalidRequest_Fails(string email, string password, string displayName)
    {
        var result = _registerValidator.Validate(new RegisterUserRequest(email, password, displayName));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RegisterUser_WithAnExcessivelyLongPassword_Fails()
    {
        // Guards the upper bound: hashing is deliberately slow, so an unbounded password
        // is a cheap way to make the server do expensive work.
        var request = new RegisterUserRequest("nick@example.com", new string('a', 200), "Nick");

        Assert.False(_registerValidator.Validate(request).IsValid);
    }

    [Fact]
    public void LoginUser_DoesNotEnforceThePasswordPolicy()
    {
        // Intentional: applying the current policy at login would lock out users whose
        // password predates a policy change, and would leak the policy to anyone probing.
        var result = _loginValidator.Validate(new LoginUserRequest("nick@example.com", "short"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void LoginUser_WithMissingPassword_Fails()
    {
        Assert.False(_loginValidator.Validate(new LoginUserRequest("nick@example.com", "")).IsValid);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("User", true)]
    [InlineData("Superuser", false)]
    [InlineData("admin", false)]
    [InlineData("", false)]
    public void UpdateUserRole_AcceptsOnlyKnownRoles(string role, bool expectedValid)
    {
        // "admin" fails on purpose: role comparison is case-sensitive, so accepting a
        // lower-case variant here would produce a request the handler then rejects.
        var result = _roleValidator.Validate(new UpdateUserRoleRequest(role));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void UpdateUser_WithAValidDisplayName_Passes()
    {
        Assert.True(_updateValidator.Validate(new UpdateUserRequest("Nick")).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateUser_WithABlankDisplayName_Fails(string displayName)
    {
        // A whitespace-only name passes a naive null check and then renders as an empty
        // label everywhere the frontend shows it.
        Assert.False(_updateValidator.Validate(new UpdateUserRequest(displayName)).IsValid);
    }

    [Fact]
    public void UpdateUser_AtTheLengthLimit_Passes()
    {
        // The boundary in both directions, because an off-by-one here is invisible until
        // someone with a long name is rejected.
        Assert.True(_updateValidator.Validate(new UpdateUserRequest(new string('a', 128))).IsValid);
    }

    [Fact]
    public void UpdateUser_PastTheLengthLimit_Fails()
    {
        Assert.False(_updateValidator.Validate(new UpdateUserRequest(new string('a', 129))).IsValid);
    }

    [Fact]
    public void RefreshToken_WithBothTokens_Passes()
    {
        Assert.True(_refreshValidator.Validate(new RefreshTokenRequest("access", "refresh")).IsValid);
    }

    [Theory]
    [InlineData("", "refresh")]
    [InlineData("access", "")]
    [InlineData("", "")]
    public void RefreshToken_WithAMissingToken_Fails(string accessToken, string refreshToken)
    {
        // Both halves are required. Letting an empty access token through would send it to
        // the signature check, which rejects it anyway — but as a generic failure, after
        // the work of parsing it.
        var result = _refreshValidator.Validate(new RefreshTokenRequest(accessToken, refreshToken));

        Assert.False(result.IsValid);
    }
}
