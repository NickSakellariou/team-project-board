using Modules.Common.Domain.Results;
using Modules.Users.Domain.Authentication;
using Modules.Users.Domain.Errors;
using Modules.Users.Features.Users.RefreshToken;
using NSubstitute;

namespace Modules.Users.Tests.Unit.Users;

/// <summary>
/// Covers <see cref="RefreshTokenHandler"/>.
/// </summary>
/// <remarks>
/// The handler is a thin translation between the service's tokens and the endpoint's
/// response, so these tests guard the two things a translation gets wrong: dropping the
/// errors, and pairing up the wrong fields.
/// </remarks>
public class RefreshTokenHandlerTests
{
    private readonly IAuthenticationService _authenticationService = Substitute.For<IAuthenticationService>();

    private RefreshTokenHandler CreateHandler() => new(_authenticationService);

    [Fact]
    public async Task HandleAsync_WithAValidPair_ReturnsTheNewTokens()
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(15);
        _authenticationService
            .RefreshAsync("old-access", "old-refresh", Arg.Any<CancellationToken>())
            .Returns(new AuthenticationTokens("new-access", "new-refresh", expiresAt));

        var result = await CreateHandler()
            .HandleAsync(new RefreshTokenRequest("old-access", "old-refresh"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Two same-typed fields side by side: transposing them would still compile, still
        // return 200, and break every client on its next refresh.
        Assert.Equal("new-access", result.Value!.AccessToken);
        Assert.Equal("new-refresh", result.Value.RefreshToken);
        Assert.Equal(expiresAt, result.Value.ExpiresAtUtc);
    }

    [Fact]
    public async Task HandleAsync_PassesBothTokensThroughInOrder()
    {
        _authenticationService
            .RefreshAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticationTokens("a", "r", DateTime.UtcNow));

        await CreateHandler()
            .HandleAsync(new RefreshTokenRequest("the-access-token", "the-refresh-token"), CancellationToken.None);

        await _authenticationService.Received(1).RefreshAsync(
            "the-access-token",
            "the-refresh-token",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenTheTokenIsRejected_PropagatesTheError()
    {
        _authenticationService
            .RefreshAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthenticationTokens>.FromError(UserErrors.InvalidToken()));

        var result = await CreateHandler()
            .HandleAsync(new RefreshTokenRequest("access", "spent-refresh"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.InvalidToken", result.FirstError.Code);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task HandleAsync_WhenTheServiceReturnsSeveralErrors_KeepsThemAll()
    {
        _authenticationService
            .RefreshAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthenticationTokens>.FromErrors(
                [UserErrors.InvalidToken(), UserErrors.NotFound("user-1")]));

        var result = await CreateHandler()
            .HandleAsync(new RefreshTokenRequest("access", "refresh"), CancellationToken.None);

        // The handler rebuilds the result rather than returning it, which is where errors
        // beyond the first are easiest to lose.
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public async Task HandleAsync_PassesTheCancellationTokenThrough()
    {
        using var cts = new CancellationTokenSource();

        _authenticationService
            .RefreshAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticationTokens("a", "r", DateTime.UtcNow));

        await CreateHandler().HandleAsync(new RefreshTokenRequest("access", "refresh"), cts.Token);

        await _authenticationService.Received(1).RefreshAsync("access", "refresh", cts.Token);
    }
}
