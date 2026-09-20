# Observability

## The three signals

Not interchangeable — each answers a different question.

| Signal | Question | Example |
|---|---|---|
| **Logs** | What happened? | "User bcc47b92 signed in" |
| **Traces** | Where did the time go? | request 800ms → SQL 780ms |
| **Metrics** | How is it behaving overall? | 12 req/s, p99 340ms |

Logs tell you about one event. Traces tell you how one request moved through the system.
Metrics tell you about all of them at once and are the only one cheap enough to keep
forever at full fidelity.

A common failure is having only logs, then trying to answer "why is this endpoint slow?" by
reading them. Logs record what someone thought to record; a trace records the shape of the
request whether or not anyone anticipated the question.

## Structured logging

The difference is small in the source and large in what it enables:

```csharp
// Flat text — a string, and nothing more
logger.LogInformation($"Registered user {user.Id}");

// Structured — a message template plus named values
logger.LogInformation("Registered user {UserId}", user.Id);
```

The second records `UserId` as a *field*. So you can ask "every event for user
bcc47b92", or "how many registrations today", instead of grepping and hoping the format
never changed.

It also groups events by template. A thousand registrations are a thousand instances of one
message, not a thousand distinct strings.

The whole codebase uses the placeholder form. Note the convention: placeholders are
PascalCase (`{UserId}`), unlike C# parameters — they are field names in the log store.

**They must not be interpolated.** `logger.LogInformation($"...{user.Id}")` compiles fine
and silently destroys the structure, which is why it is easy to get wrong and worth
watching for in review.

### Where the templates actually live

Neither line above is what you will find at a call site. A template written inline is
anonymous — the only handle on it is its text, so rewording it breaks every saved query
built on it, silently. So every event is declared once, in a catalogue class, and given a
permanent numeric id:

```csharp
// Users/Modules.Users.Domain/Logging/UserLogs.cs
[LoggerMessage(
    EventId = 10_003,
    Level = LogLevel.Information,
    Message = "User {UserId} signed in")]
public static partial void UserSignedIn(this ILogger logger, string userId);
```

```csharp
// the call site
logger.UserSignedIn(user.Id);
```

The source generator writes the implementation, so the call site is cheaper than the
`params object[]` overload as well as being greppable. `CA1848` is an error in
`.editorconfig`, so a bare `logger.LogInformation(...)` does not compile — that is what
keeps the catalogues complete.

[log-event-ids.md](../log-event-ids.md) has the id ranges, what every allocated id means,
and what enforces it.

### Serilog

Replaces the default logger, configured from `appsettings.json` so levels and destinations
change without a rebuild:

```json
"MinimumLevel": {
  "Default": "Information",
  "Override": {
    "Microsoft.AspNetCore": "Warning",
    "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
  }
}
```

The overrides matter for signal-to-noise. At `Information`, EF Core logs every SQL
statement it executes — useful when you are debugging a query, overwhelming the rest of the
time.

`UseSerilogRequestLogging()` replaces the several lines ASP.NET Core logs per request with
one summary line carrying method, path, status and duration.

### What must never be logged

Passwords, tokens, password hashes, security stamps. `AuthenticationService` logs
`user.Id` on a successful login and the *email* on a failed one — never the password
attempt, not even a truncated one.

Worth being deliberate about, because logs are copied, shipped to third-party services and
retained far longer than anyone intends.

## Traces

A **trace** is one request. A **span** is one operation inside it, with a start, a duration
and a parent. Spans nest, so a trace is a tree:

```
POST /api/users/login                                    142ms
├── UserManager.FindByEmailAsync                          12ms
│   └── SELECT ... FROM users."user" WHERE ...             9ms
├── CheckPasswordAsync                                    98ms   ← PBKDF2, expected
└── SaveChangesAsync                                      28ms
    └── INSERT INTO users.refresh_token ...               24ms
```

That picture answers "why is login slow?" immediately, and correctly — the 98ms is password
hashing being deliberately expensive, not a bug.

Configured in `AddCoreInfrastructure`:

```csharp
tracing
    .AddAspNetCoreInstrumentation()   // a span per incoming request
    .AddHttpClientInstrumentation()   // a span per outgoing call
    .AddNpgsql();                     // a span per SQL statement
```

`AddNpgsql()` is the one that earns its place. Without database spans a trace says "this
took 800ms" and stops exactly where the answer usually is.

### Activity sources

.NET's tracing API calls a span an `Activity` and a producer an `ActivitySource`. The
parameter on `AddCoreInfrastructure` exists for modules that want to emit their own:

```csharp
builder.Services.AddCoreInfrastructure(builder.Configuration);   // none yet
```

A module would declare `new ActivitySource("Modules.Boards")` and start activities around
operations worth naming — "reorder tasks", say. Not needed yet; the parameter is there so
adding one is not a change to the Common project.

## Metrics

Aggregates rather than individual events. Registered by
`.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation()`,
which gives request rate and duration histograms, outbound call statistics, and GC and
thread-pool counters.

Metrics are what alerts are built on — "error rate above 1% for five minutes" — because they
are cheap enough to evaluate continuously. A trace is too expensive to keep for every
request forever; a counter is not.

## OpenTelemetry

The unifying piece. Before it, each vendor had its own SDK, and switching meant changing
instrumentation code throughout the application.

OpenTelemetry is a vendor-neutral standard for *producing* telemetry and a wire protocol
(**OTLP**) for shipping it. The application emits OTLP; where it goes is configuration:

```csharp
tracing.AddOtlpExporter();
```

Locally that is the Aspire dashboard. In production it could be Jaeger, Grafana Tempo, Seq,
Honeycomb or Application Insights — with no code change. That portability is the whole
point.

## Where to look, day to day

Run the AppHost and open the dashboard (URL printed at startup):

- **Console logs** — the API's and the containers', in one place.
- **Structured logs** — Serilog's output with properties queryable.
- **Traces** — the waterfall above, per request.
- **Metrics** — request rates and durations.

The traces tab is the one that changes how you debug.

## What is not set up

- **No persistent backend.** The dashboard keeps telemetry in memory and loses it on
  restart. Fine for development; production needs a real store.
- **No alerting.** Nothing watches the metrics.
- **No log correlation across services.** One service, so nothing to correlate. OTLP already
  propagates trace context, so this works when there is a second.
- **No sampling.** Every request is traced. Fine at this volume, expensive at scale.

## Related

- [../log-event-ids.md](../log-event-ids.md) — the id ranges and every allocated event
- [aspire.md](aspire.md) — the dashboard that receives all this
- [ef-core-basics.md](ef-core-basics.md) — where the SQL spans come from
