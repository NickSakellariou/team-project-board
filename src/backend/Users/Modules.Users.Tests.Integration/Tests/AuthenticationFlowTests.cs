using System.Net;
using System.Net.Http.Json;
using Modules.Users.Tests.Integration.Configuration;
using Modules.Users.Tests.Integration.Contracts;

namespace Modules.Users.Tests.Integration.Tests;

/// <summary>
/// Exercises registration, sign-in and token refresh end to end.
/// </summary>
/// <remarks>
/// These are the tests that would have caught a middleware ordering mistake, a policy that
/// was never registered, or a claim that never reached the token — none of which any unit
/// test can see.
/// </remarks>
public class AuthenticationFlowTests(UsersApiFactory factory) : BaseApiTest(factory)
{
    [Fact]
    public async Task Register_WithValidRequest_CreatesTheUser()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/users/register",
            new RegisterUserRequest("nick@example.com", "Password1", "Nick"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();

        Assert.NotNull(user);
        Assert.Equal("nick@example.com", user.Email);
        Assert.Equal("Nick", user.DisplayName);
        // Registration never grants Admin, whatever the client sends.
        Assert.Equal(["User"], user.Roles);
    }

    [Fact]
    public async Task Register_WithADuplicateEmail_ReturnsConflict()
    {
        await RegisterAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/users/register",
            new RegisterUserRequest("nick@example.com", "Password1", "Someone Else"));

        // Proves the whole Result-to-ProblemDetails path: a domain Conflict error becomes
        // a 409 rather than an unhandled exception or a 500.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithAWeakPassword_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/users/register",
            new RegisterUserRequest("nick@example.com", "short", "Nick"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsATokenPair()
    {
        await RegisterAsync();

        var tokens = await LoginAsync();

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.True(tokens.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_IsRejected()
    {
        await RegisterAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/users/login",
            new LoginUserRequest("nick@example.com", "WrongPassword1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithAnUnknownEmail_IsRejectedTheSameWay()
    {
        // Deliberately indistinguishable from a wrong password, so the endpoint cannot be
        // used to discover which email addresses have accounts.
        var response = await Client.PostAsJsonAsync(
            "/api/users/login",
            new LoginUserRequest("nobody@example.com", "Password1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithAValidToken_ReturnsTheCaller()
    {
        var registered = await RegisterAsync();
        var tokens = await LoginAsync();
        Authenticate(tokens.AccessToken);

        var user = await Client.GetFromJsonAsync<UserResponse>("/api/users/me");

        Assert.NotNull(user);
        Assert.Equal(registered.Id, user.Id);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAToken_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync(new Uri("/api/users/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithAGarbageToken_ReturnsUnauthorized()
    {
        Authenticate("not-a-real-jwt");

        var response = await Client.GetAsync(new Uri("/api/users/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAValidPair_ReturnsNewTokens()
    {
        await RegisterAsync();
        var tokens = await LoginAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(tokens.AccessToken, tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refreshed = await response.Content.ReadFromJsonAsync<TokenResponse>();

        Assert.NotNull(refreshed);
        // Rotation: the refresh token must be a new one, or a stolen token would stay
        // usable indefinitely.
        Assert.NotEqual(tokens.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReusingASpentRefreshToken_IsRejected()
    {
        await RegisterAsync();
        var tokens = await LoginAsync();

        var first = await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(tokens.AccessToken, tokens.RefreshToken));

        first.EnsureSuccessStatusCode();

        // The replay. The first refresh spent this token; presenting it again is the
        // signature of a stolen token being reused.
        var replay = await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(tokens.AccessToken, tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task Refresh_AfterAReplay_InvalidatesTheWholeChain()
    {
        await RegisterAsync();
        var tokens = await LoginAsync();

        var refreshed = await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(tokens.AccessToken, tokens.RefreshToken));

        var newTokens = (await refreshed.Content.ReadFromJsonAsync<TokenResponse>())!;

        // Trigger the replay detection with the old token...
        await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(tokens.AccessToken, tokens.RefreshToken));

        // ...and the legitimate token issued a moment ago is now dead too. That is
        // intended: once a chain is known to be compromised, ending all of it is safer
        // than guessing which holder is the real user.
        var afterReplay = await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(newTokens.AccessToken, newTokens.RefreshToken));

        Assert.Equal(HttpStatusCode.BadRequest, afterReplay.StatusCode);
    }
}
