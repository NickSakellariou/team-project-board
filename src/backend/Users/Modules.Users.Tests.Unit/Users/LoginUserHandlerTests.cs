using Modules.Common.Domain.Results;
using Modules.Users.Domain.Authentication;
using Modules.Users.Domain.Errors;
using Modules.Users.Features.Users.LoginUser;
using NSubstitute;

namespace Modules.Users.Tests.Unit.Users;

/// <summary>
/// Covers <see cref="LoginUserHandler"/>.
/// </summary>
/// <remarks>
/// A good illustration of why the handler depends on <see cref="IAuthenticationService"/>
/// rather than doing the work itself: these tests exercise the use case with no database,
/// no cryptography and no configuration, in milliseconds.
/// </remarks>
public class LoginUserHandlerTests
{
    private readonly IAuthenticationService _authenticationService = Substitute.For<IAuthenticationService>();

    private LoginUserHandler CreateHandler() => new(_authenticationService);

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ReturnsTokens()
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(15);
        _authenticationService
            .LoginAsync("nick@example.com", "Password1", Arg.Any<CancellationToken>())
            .Returns(new AuthenticationTokens("access", "refresh", expiresAt));

        var result = await CreateHandler()
            .HandleAsync(new LoginUserRequest("nick@example.com", "Password1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("access", result.Value!.AccessToken);
        Assert.Equal("refresh", result.Value.RefreshToken);
        Assert.Equal(expiresAt, result.Value.ExpiresAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WithWrongPassword_ReturnsInvalidCredentials()
    {
        _authenticationService
            .LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthenticationTokens>.FromError(UserErrors.InvalidCredentials()));

        var result = await CreateHandler()
            .HandleAsync(new LoginUserRequest("nick@example.com", "wrong"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.InvalidCredentials", result.FirstError.Code);
        // Validation maps to 400, not 401 — deliberate, so a failed login is not confused
        // with a missing or expired token.
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task HandleAsync_WhenAccountIsLockedOut_ReturnsLockedOut()
    {
        _authenticationService
            .LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthenticationTokens>.FromError(UserErrors.LockedOut()));

        var result = await CreateHandler()
            .HandleAsync(new LoginUserRequest("nick@example.com", "Password1"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task HandleAsync_PassesTheCancellationTokenThrough()
    {
        using var cts = new CancellationTokenSource();

        _authenticationService
            .LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticationTokens("a", "r", DateTime.UtcNow));

        await CreateHandler()
            .HandleAsync(new LoginUserRequest("nick@example.com", "Password1"), cts.Token);

        // Easy to drop by accident, and the consequence — work continuing after the client
        // has disconnected — is invisible until it matters.
        await _authenticationService.Received(1).LoginAsync("nick@example.com", "Password1", cts.Token);
    }
}
