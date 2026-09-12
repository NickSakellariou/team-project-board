// The Aspire AppHost. This is not part of the API — it is a small program whose only job
// is to start everything the application needs and wire the connections between them.
//
// Running this project starts the Postgres container, waits for it to be ready, then
// starts the API with a connection string pointing at it, and opens a dashboard showing
// logs, traces and metrics from all of it. Nothing here ships to production; in
// production the orchestrator (Compose, Kubernetes, App Service) plays this role.
//
// See docs/concepts/aspire.md.

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    // Why a named volume: without it the container's data is discarded on every restart
    // and you re-register your test users several times a day.
    .WithDataVolume("team-project-board-postgres-data")
    .WithPgAdmin();

// AddDatabase declares a database *inside* that server, and Aspire creates it if missing.
// Referencing the database rather than the server is what makes the injected connection
// string include "Database=teamprojectboard" — reference the server instead and EF
// connects with no database selected, which fails at the first query.
var database = postgres.AddDatabase("teamprojectboard");

// The JWT signing key for local development.
//
// Why here rather than in appsettings.json: appsettings is deployed with the application,
// so a key committed there is a key that reaches production unless someone remembers to
// override it. The AppHost is a development-only program that is never deployed at all,
// which makes it the safe place for a value that must exist for the app to start but must
// never leave a developer machine.
//
// In production this arrives as the AuthConfiguration__Key environment variable from a
// secret store. Anyone holding it can mint tokens this API will trust, so it is treated
// like a password. The value below is public in this repository and is therefore worthless
// as a secret — which is exactly why it must never be used anywhere real.
const string DevelopmentSigningKey = "development-only-signing-key-not-for-any-real-environment";

builder.AddProject<Projects.TeamProjectBoard_Host>("api")
    // Double underscore is the .NET convention for nesting in an environment variable
    // name: AuthConfiguration__Key binds to configuration key "AuthConfiguration:Key".
    .WithEnvironment("AuthConfiguration__Key", DevelopmentSigningKey)
    .WithEnvironment("Seed__AdminEmail", "admin@teamprojectboard.local")
    .WithEnvironment("Seed__AdminPassword", "Admin123!")
    // WithReference injects ConnectionStrings__teamprojectboard into the API's
    // environment. The connection string is therefore never written in appsettings, and
    // never committed.
    .WithReference(database)
    // WaitFor holds the API until Postgres reports healthy. Without it the API starts
    // first, tries to migrate against a container that is not listening yet, and crashes.
    .WaitFor(database);

await builder.Build().RunAsync();
