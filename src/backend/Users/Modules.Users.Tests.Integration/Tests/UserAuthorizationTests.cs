using System.Net;
using System.Net.Http.Json;
using Modules.Users.Tests.Integration.Configuration;
using Modules.Users.Tests.Integration.Contracts;

namespace Modules.Users.Tests.Integration.Tests;

/// <summary>
/// Verifies that the claim-based policies actually gate the administrative endpoints.
/// </summary>
/// <remarks>
/// The point of these tests is the 403s. It is easy to write an endpoint that looks
/// protected — <c>RequireAuthorization(SomePolicy)</c> reads convincingly — while the
/// policy was never registered or the claim never reaches the token, in which case the
/// guard silently does nothing.
/// </remarks>
public class UserAuthorizationTests(UsersApiFactory factory) : BaseApiTest(factory)
{
    [Fact]
    public async Task GetUserById_AsAnOrdinaryUser_ReturnsForbidden()
    {
        var other = await RegisterAsync("other@example.com", displayName: "Other");
        await RegisterAsync();

        var tokens = await LoginAsync();
        Authenticate(tokens.AccessToken);

        var response = await Client.GetAsync(new Uri($"/api/users/{other.Id}", UriKind.Relative));

        // 403, not 401: the caller is authenticated, just not permitted. Getting this
        // distinction wrong sends a signed-in user to a login screen they do not need.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_AsAnAdmin_ReturnsTheUser()
    {
        var target = await RegisterAsync();
        await AuthenticateAsAdminAsync();

        var user = await Client.GetFromJsonAsync<UserResponse>($"/api/users/{target.Id}");

        Assert.NotNull(user);
        Assert.Equal(target.Id, user.Id);
    }

    [Fact]
    public async Task GetUserById_ForAnUnknownId_ReturnsNotFound()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.GetAsync(
            new Uri($"/api/users/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRole_AsAnAdmin_ChangesTheRole()
    {
        var target = await RegisterAsync();
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsJsonAsync(
            $"/api/users/{target.Id}/role",
            new UpdateUserRoleRequest("Admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UserResponse>();

        Assert.NotNull(updated);
        // Exactly one role: the old one is removed rather than added to.
        Assert.Equal(["Admin"], updated.Roles);
    }

    [Fact]
    public async Task UpdateUserRole_AsAnOrdinaryUser_ReturnsForbidden()
    {
        var target = await RegisterAsync("target@example.com", displayName: "Target");
        await RegisterAsync();

        var tokens = await LoginAsync();
        Authenticate(tokens.AccessToken);

        // The privilege-escalation attempt this endpoint exists to refuse.
        var response = await Client.PutAsJsonAsync(
            $"/api/users/{target.Id}/role",
            new UpdateUserRoleRequest("Admin"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRole_WithAnUnknownRole_ReturnsBadRequest()
    {
        var target = await RegisterAsync();
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsJsonAsync(
            $"/api/users/{target.Id}/role",
            new UpdateUserRoleRequest("Superuser"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AsAnAdmin_RemovesTheUser()
    {
        var target = await RegisterAsync();
        await AuthenticateAsAdminAsync();

        var deleteResponse = await Client.DeleteAsync(
            new Uri($"/api/users/{target.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await Client.GetAsync(new Uri($"/api/users/{target.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_AsAnAdmin_ChangesTheDisplayName()
    {
        var target = await RegisterAsync();
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsJsonAsync(
            $"/api/users/{target.Id}",
            new UpdateUserRequest("Renamed"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UserResponse>();

        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated.DisplayName);
    }
}
