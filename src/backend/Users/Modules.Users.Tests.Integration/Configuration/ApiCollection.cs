namespace Modules.Users.Tests.Integration.Configuration;

/// <summary>
/// Shares one <see cref="UsersApiFactory"/> across every test class in this project.
/// </summary>
/// <remarks>
/// Without this, xUnit would create a fresh fixture — and therefore a fresh Postgres
/// container and a fresh application host — for each test class. A collection fixture
/// makes that cost be paid once per run. Tests within it run sequentially, which is what
/// makes Respawn's between-test reset safe.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<UsersApiFactory>
{
    public const string Name = "Users API";
}
