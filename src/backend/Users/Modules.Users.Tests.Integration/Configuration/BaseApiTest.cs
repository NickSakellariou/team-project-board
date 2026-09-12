using System.Net.Http.Headers;
using System.Net.Http.Json;
using Modules.Users.Tests.Integration.Contracts;

namespace Modules.Users.Tests.Integration.Configuration;

/// <summary>
/// Shared setup for tests that call the API over HTTP.
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class BaseApiTest(UsersApiFactory factory) : IAsyncLifetime
{
    /// <summary>An HttpClient wired into the in-memory application.</summary>
    protected HttpClient Client { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Client = factory.CreateClient();
        return Task.CompletedTask;
    }

    // Runs after every test, so each one starts against an empty users table.
    public async Task DisposeAsync()
    {
        Client.Dispose();
        await factory.ResetDatabaseAsync();
    }

    /// <summary>Registers a user and returns the created record.</summary>
    protected async Task<UserResponse> RegisterAsync(
        string email = "nick@example.com",
        string password = "Password1",
        string displayName = "Nick")
    {
        var response = await Client.PostAsJsonAsync(
            "/api/users/register",
            new RegisterUserRequest(email, password, displayName));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    /// <summary>Signs in and returns the token pair.</summary>
    protected async Task<TokenResponse> LoginAsync(
        string email = "nick@example.com",
        string password = "Password1")
    {
        var response = await Client.PostAsJsonAsync(
            "/api/users/login",
            new LoginUserRequest(email, password));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    /// <summary>Attaches a bearer token to every subsequent request on <see cref="Client"/>.</summary>
    protected void Authenticate(string accessToken) =>
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    /// <summary>Signs in as the seeded admin and authenticates the client.</summary>
    protected async Task AuthenticateAsAdminAsync()
    {
        var tokens = await LoginAsync(TestUsers.AdminEmail, TestUsers.AdminPassword);
        Authenticate(tokens.AccessToken);
    }
}
