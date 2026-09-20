using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Respawn;
using TeamProjectBoard.Host.Seeding;
using Testcontainers.PostgreSql;

namespace Modules.Users.Tests.Integration.Configuration;

/// <summary>
/// Starts the real application against a throwaway Postgres container.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a real database.</b> The obvious alternative, EF Core's in-memory provider, is
/// not a database: it has no schemas, no unique constraints, no cascade deletes and no SQL
/// translation. Every one of those is something this module relies on, so a test against
/// it would pass while production broke. Testcontainers gives a real Postgres, started
/// from Docker, discarded afterwards.
/// </para>
/// <para>
/// <b>What WebApplicationFactory does.</b> It boots the actual Program.cs — real DI, real
/// middleware pipeline, real endpoint routing — and hands back an HttpClient wired
/// straight into it, with no network socket involved. So these tests exercise
/// authentication, authorization, model binding and serialization, which unit tests
/// cannot reach.
/// </para>
/// </remarks>
public sealed class UsersApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The key the application signs and validates access tokens with.</summary>
    /// <remarks>
    /// A constant rather than a literal in <c>ConfigureWebHost</c> because
    /// <c>TokenSecurityTests</c> forges tokens with it: a test that re-signs a token with
    /// a different key has to know the real one to prove the difference matters.
    /// </remarks>
    public const string SigningKey = "integration-test-signing-key-long-enough-for-hmac-sha256";

    /// <summary>The issuer and audience the application expects.</summary>
    public const string TokenIssuer = "TeamProjectBoard";

    // Pinning the image version matters: "postgres:latest" would mean a test suite that
    // starts failing one morning because the tag moved.
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("teamprojectboard_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private Respawner? _respawner;
    private NpgsqlConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Development, so the startup path that migrates and seeds runs — the tests need
        // the roles and their claims to exist, exactly as a developer's machine does.
        builder.UseEnvironment(Environments.Development);

        builder.UseSetting("ConnectionStrings:teamprojectboard", _database.GetConnectionString());

        // Supplied here rather than from the AppHost, which is not running in a test.
        builder.UseSetting("AuthConfiguration:Key", SigningKey);
        builder.UseSetting("AuthConfiguration:Issuer", TokenIssuer);
        builder.UseSetting("AuthConfiguration:Audience", TokenIssuer);
        builder.UseSetting("Seed:AdminEmail", TestUsers.AdminEmail);
        builder.UseSetting("Seed:AdminPassword", TestUsers.AdminPassword);
    }

    // Explicit interface implementation: WebApplicationFactory already has its own
    // DisposeAsync returning ValueTask, while xUnit's IAsyncLifetime wants Task. Naming
    // the interface keeps both, with no clash.
    async Task IAsyncLifetime.InitializeAsync()
    {
        await _database.StartAsync();

        // Creating the client forces the host to build, which runs migrations and seeding.
        // The Respawner snapshot below must be taken after that, or it would treat the
        // seeded roles as rows to delete.
        using var _ = CreateClient();

        _connection = new NpgsqlConnection(_database.GetConnectionString());
        await _connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["users"],
            // Preserved across resets, because they are configuration rather than test
            // data: re-seeding roles and their claims before every test would be slow, and
            // deleting them would break every authorization check.
            TablesToIgnore = ["role", "role_claim", "migration_history"]
        });
    }

    /// <summary>
    /// Empties the data tables between tests and restores the seeded admin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why not recreate the database per test: starting a container takes seconds, and
    /// deleting rows takes milliseconds. Respawn works out the foreign-key order and issues
    /// the deletes, so each test starts from a known state without paying for a new
    /// container.
    /// </para>
    /// <para>
    /// The re-seed is not optional. Respawn clears the user table, which includes the
    /// seeded admin — so without this, the first test to run would pass and every later
    /// admin test would fail on login. A neat illustration of the hazard of shared
    /// fixtures: the bug shows up as tests that pass alone and fail together.
    /// </para>
    /// </remarks>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null || _connection is null)
        {
            return;
        }

        await _respawner.ResetAsync(_connection);

        using var scope = Services.CreateScope();
        var seedService = scope.ServiceProvider.GetRequiredService<UserSeedService>();
        await seedService.SeedAsync();
    }

    // xUnit calls this once every test class in the collection has finished.
    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await _database.DisposeAsync();
        await base.DisposeAsync();
    }
}
