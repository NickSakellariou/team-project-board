using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Users.Domain.Tokens;
using Modules.Users.Infrastructure.Database;
using Modules.Users.Tests.Integration.Configuration;
using Modules.Users.Tests.Integration.Contracts;

namespace Modules.Users.Tests.Integration.Tests;

/// <summary>
/// Verifies that <c>AuditableInterceptor</c> stamps the audit columns as rows are saved.
/// </summary>
/// <remarks>
/// <para>
/// These columns are the one thing in the module no endpoint returns, so nothing else in
/// the suite would notice them being wrong — and they are the record you reach for after
/// an incident, when it is too late to start collecting.
/// </para>
/// <para>
/// A real database, not a substitute, because the rule that matters most here is about
/// generated SQL: the interceptor marks <c>CreatedAtUtc</c> unmodified so the UPDATE
/// statement leaves the column out. A fake change tracker would report the property as
/// unmodified and prove nothing about what Postgres was actually told.
/// </para>
/// </remarks>
public class AuditingTests(UsersApiFactory factory) : BaseApiTest(factory)
{
    [Fact]
    public async Task ANewlyInsertedRow_IsStampedAsCreatedAndNotUpdated()
    {
        var user = await RegisterAsync();
        await LoginAsync();

        var token = await TheOnlyRefreshTokenFor(user.Id);

        Assert.NotEqual(default, token.CreatedAtUtc);
        // Null rather than equal to CreatedAtUtc: "never touched since creation" is a
        // distinction worth being able to make when reading the table later.
        Assert.Null(token.UpdatedAtUtc);
    }

    [Fact]
    public async Task AnUpdatedRow_IsStamped_WithoutLosingItsCreationTime()
    {
        var user = await RegisterAsync();
        var tokens = await LoginAsync();

        var beforeRefresh = await TheOnlyRefreshTokenFor(user.Id);

        // Rotation marks the old token spent, which is an UPDATE on the row just inserted.
        var response = await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(tokens.AccessToken, tokens.RefreshToken));

        response.EnsureSuccessStatusCode();

        var afterRefresh = await RefreshTokenByValue(tokens.RefreshToken);

        Assert.True(afterRefresh.Used);
        Assert.NotNull(afterRefresh.UpdatedAtUtc);

        // The guard this test exists for. Without the interceptor marking CreatedAtUtc
        // unmodified, the UPDATE would carry the column along and overwrite the original
        // creation time with whatever the loaded entity happened to hold — quietly, and
        // only ever noticed by someone reading the audit trail long afterwards.
        Assert.Equal(beforeRefresh.CreatedAtUtc, afterRefresh.CreatedAtUtc);
    }

    [Fact]
    public async Task TimestampsAreStoredInUtc()
    {
        var user = await RegisterAsync();
        await LoginAsync();

        var token = await TheOnlyRefreshTokenFor(user.Id);

        // The server's local time zone is an accident of where the container runs. A
        // timestamp stamped in local time is comparable with nothing else in the table.
        Assert.Equal(DateTimeKind.Utc, token.CreatedAtUtc.Kind);
        Assert.InRange(token.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));
    }

    private async Task<RefreshToken> TheOnlyRefreshTokenFor(string userId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        return await dbContext.RefreshTokens
            .AsNoTracking()
            .SingleAsync(token => token.UserId == userId);
    }

    private async Task<RefreshToken> RefreshTokenByValue(string value)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        return await dbContext.RefreshTokens
            .AsNoTracking()
            .SingleAsync(token => token.Token == value);
    }
}
