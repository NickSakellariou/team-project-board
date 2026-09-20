using System.Net;
using System.Net.Http.Json;
using Modules.Users.Tests.Integration.Configuration;
using Modules.Users.Tests.Integration.Contracts;

namespace Modules.Users.Tests.Integration.Tests;

/// <summary>
/// Verifies the two rules that stop an administrator locking everybody out.
/// </summary>
/// <remarks>
/// An admin who deletes their own account, or demotes themselves, leaves an installation
/// with no one able to promote anyone — a state no endpoint in the module can repair. The
/// guards are one <c>if</c> each, and they read like paternalism to anyone who meets them
/// without the context, which is exactly why they need a test with the reason attached.
/// </remarks>
public class SelfAdministrationGuardTests(UsersApiFactory factory) : BaseApiTest(factory)
{
    [Fact]
    public async Task DeleteUser_DeletingYourOwnAccount_IsRejected()
    {
        var admin = await AuthenticateAsAdminAndGetSelfAsync();

        var response = await Client.DeleteAsync(new Uri($"/api/users/{admin.Id}", UriKind.Relative));

        // 409, not 403: the caller holds users:delete and the request is well formed. What
        // is wrong is the state it would leave behind.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_DeletingYourOwnAccount_LeavesTheAccountIntact()
    {
        var admin = await AuthenticateAsAdminAndGetSelfAsync();

        await Client.DeleteAsync(new Uri($"/api/users/{admin.Id}", UriKind.Relative));

        // The rejection has to mean nothing happened. A guard that returns an error after
        // the delete has already gone through would look identical from the status code.
        var stillThere = await Client.GetAsync(new Uri($"/api/users/{admin.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRole_ChangingYourOwnRole_IsRejected()
    {
        var admin = await AuthenticateAsAdminAndGetSelfAsync();

        var response = await Client.PutAsJsonAsync(
            $"/api/users/{admin.Id}/role",
            new UpdateUserRoleRequest("User"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRole_ChangingYourOwnRole_LeavesTheRoleIntact()
    {
        var admin = await AuthenticateAsAdminAndGetSelfAsync();

        await Client.PutAsJsonAsync($"/api/users/{admin.Id}/role", new UpdateUserRoleRequest("User"));

        var current = await Client.GetFromJsonAsync<UserResponse>("/api/users/me");

        Assert.NotNull(current);
        Assert.Equal(["Admin"], current.Roles);
    }

    [Fact]
    public async Task DeleteUser_DeletingSomeoneElse_IsStillAllowed()
    {
        // The counterweight. A guard that rejected every delete would pass all of the
        // tests above and break the feature.
        var target = await RegisterAsync();
        await AuthenticateAsAdminAsync();

        var response = await Client.DeleteAsync(new Uri($"/api/users/{target.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRole_ChangingSomeoneElsesRole_IsStillAllowed()
    {
        var target = await RegisterAsync();
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsJsonAsync(
            $"/api/users/{target.Id}/role",
            new UpdateUserRoleRequest("Admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<UserResponse> AuthenticateAsAdminAndGetSelfAsync()
    {
        await AuthenticateAsAdminAsync();

        // The admin's id is not known to the test ahead of time — the seeder generates it
        // — so it is read the way a client would, from the token's own endpoint.
        return (await Client.GetFromJsonAsync<UserResponse>("/api/users/me"))!;
    }
}
