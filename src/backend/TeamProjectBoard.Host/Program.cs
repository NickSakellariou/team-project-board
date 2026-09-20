using Modules.Common.API;
using Modules.Common.API.Extensions;
using Modules.Common.Infrastructure.Database;
using Serilog;
using TeamProjectBoard.Host.Seeding;

// The composition root. This is the only file that knows every module exists, and its job
// is to put them together — not to contain logic of its own.
//
// Read top to bottom: services are registered, the application is built, then the request
// pipeline is assembled. Order matters in the second half and barely at all in the first.

var builder = WebApplication.CreateBuilder(args);

// Aspire defaults: health checks, service discovery, HTTP resilience.
builder.AddServiceDefaults();

// Serilog replaces the default logger. Registered early so startup itself is logged.
builder.AddCoreHostLogging();

// Cross-cutting web concerns: Swagger, problem details, the global exception handler.
builder.Services.AddCoreWebApiInfrastructure();

// Authentication, authorization and telemetry. Module activity sources would be listed
// here as they are added.
builder.Services.AddCoreInfrastructure(builder.Configuration);

// One line per module. Everything the Users module needs is behind this call.
builder.Services.AddUsersModule(builder.Configuration);

builder.Services.AddScoped<UserSeedService>();

// CORS for the React frontend. Wide open in Development only — a production policy must
// name real origins, since AllowAnyOrigin with credentials is both insecure and rejected
// by browsers.
const string DevCorsPolicy = "DevCors";

if (builder.Environment.IsDevelopment())
{
#pragma warning disable S5122 // Restrict CORS to trusted origins
    // Justified only by the enclosing IsDevelopment check: the Vite dev server's port
    // moves around, and listing it would break the moment it changed. A production policy
    // must call WithOrigins and name the real front-end origin — and note that
    // AllowAnyOrigin cannot be combined with AllowCredentials at all, which browsers
    // enforce, so this policy could not be shipped even by accident.
    builder.Services.AddCors(options =>
        options.AddPolicy(DevCorsPolicy, policy => policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()));
#pragma warning restore S5122
}

var app = builder.Build();

app.MapDefaultEndpoints();

// Migrate and seed before serving traffic, in Development only. In production both are
// deployment steps: several instances starting at once would race each other, and a
// failed migration would take the application down instead of failing the deploy.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    await scope.MigrateModuleDatabasesAsync();

    var seedService = scope.ServiceProvider.GetRequiredService<UserSeedService>();
    await seedService.SeedAsync();
}

// From here down, order is the request pipeline. Each piece runs in the order written.

// First, so an exception anywhere below is caught and turned into a problem-details
// response rather than a stack trace.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(DevCorsPolicy);
}

// Logs one line per request with method, path, status and duration — replacing the
// several lines ASP.NET Core logs by default.
app.UseSerilogRequestLogging();

// Authentication must precede authorization: the first reads the token and populates
// HttpContext.User, the second decides whether that user is allowed through. Reversed,
// every authorized endpoint would see an anonymous user and return 401.
app.UseAuthentication();
app.UseAuthorization();

// Each module's endpoints register themselves. See MapEndpointExtensions.
app.MapApiEndpoints();

await app.RunAsync();

/// <summary>
/// Exposes the implicit Program class so integration tests can reference it in
/// <c>WebApplicationFactory&lt;Program&gt;</c>. Top-level statements generate an internal
/// class, which the test project cannot see without this.
/// </summary>
#pragma warning disable S1118 // Utility classes should not have public constructors
// The compiler generates Program's entry point and its constructor from the top-level
// statements above. This declaration only widens the visibility of that generated class;
// making it static or giving it a protected constructor is not possible here.
public partial class Program;
#pragma warning restore S1118
