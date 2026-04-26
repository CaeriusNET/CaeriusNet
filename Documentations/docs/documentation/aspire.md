# Aspire Integration

CaeriusNet provides first-class integration with [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) through `WithAspireSqlServer` and `WithAspireRedis`. Aspire manages SQL Server and Redis as named resources in the AppHost; CaeriusNet resolves their connection strings automatically.

This page also documents the **OpenTelemetry signals** that CaeriusNet emits, regardless of whether you use Aspire — you only need to register them with your telemetry pipeline.

## Prerequisites

- .NET 10 and a .NET Aspire AppHost project
- The `CaeriusNet` NuGet package in your service project
- SQL Server and (optionally) Redis resources declared in the AppHost

## AppHost configuration

Declare SQL Server and Redis in the AppHost and pass their references to your service:

```csharp
// AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sqlserver")
    .AddDatabase("MyAppDb");

var redis = builder.AddRedis("redis");

builder.AddProject<Projects.MyApp_Api>("api")
    .WithReference(sql)
    .WithReference(redis);

builder.Build().Run();
```

## Service project configuration

In your service's `Program.cs`, use `WithAspireSqlServer` and `WithAspireRedis`. These methods resolve the connection string from Aspire's named connection registry:

::: code-group
```csharp [SQL Server + Redis]
using CaeriusNet.Builders;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(); // Aspire ServiceDefaults

CaeriusNetBuilder
    .Create(builder)
    .WithAspireSqlServer("sqlserver")  // matches the AppHost resource name
    .WithAspireRedis("redis")          // optional distributed cache
    .Build();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
```
```csharp [SQL Server only]
using CaeriusNet.Builders;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

CaeriusNetBuilder
    .Create(builder)
    .WithAspireSqlServer("sqlserver")
    .Build();

var app = builder.Build();
app.Run();
```
:::

## Console / Worker Service pattern

For console apps or background workers running under Aspire:

```csharp
using CaeriusNet.Builders;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

CaeriusNetBuilder
    .Create(builder)
    .WithAspireSqlServer("sqlserver")
    .WithAspireRedis("redis")
    .Build();

var host = builder.Build();
host.Run();
```

## Manual setup (without Aspire)

If you are not using Aspire, use `WithSqlServer` and `WithRedis` with explicit connection strings:

```csharp
CaeriusNetBuilder
    .Create(services)
    .WithSqlServer(configuration.GetConnectionString("Default")!)
    .WithRedis("localhost:6379")  // optional
    .Build();
```

## Resource-name matching

The string passed to `WithAspireSqlServer` and `WithAspireRedis` must match the resource name declared in the AppHost:

| AppHost declaration | Service builder call |
|---|---|
| `builder.AddSqlServer("sqlserver")` | `.WithAspireSqlServer("sqlserver")` |
| `builder.AddRedis("redis")` | `.WithAspireRedis("redis")` |

::: tip Default names
If you use the conventional names `"sqlserver"` and `"redis"`, you can also call the parameter-less overloads:

```csharp
.WithAspireSqlServer()  // defaults to "sqlserver"
.WithAspireRedis()      // defaults to "redis"
```
:::

## Complete example

```csharp
// AppHost/Program.cs
var sql   = builder.AddSqlServer("sqlserver").AddDatabase("CaeriusDb");
var redis = builder.AddRedis("redis");
builder.AddProject<Projects.Api>("api")
    .WithReference(sql)
    .WithReference(redis);
```

```csharp
// Api/Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

CaeriusNetBuilder
    .Create(builder)
    .WithAspireSqlServer("sqlserver")
    .WithAspireRedis("redis")
    .Build();

builder.Services.AddScoped<IUserRepository, UserRepository>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
```

```csharp
// Api/Repositories/UserRepository.cs
public sealed record UserRepository(ICaeriusNetDbContext DbContext)
    : IUserRepository
{
    public async Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "usp_Get_All_Users", 250)
            .AddRedisCache("users:all", TimeSpan.FromMinutes(5))
            .Build();

        return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct) ?? [];
    }
}
```

## Telemetry options

Use `WithTelemetryOptions` to configure how CaeriusNet records spans and metrics. Options are applied globally and consulted by every command pipeline.

```csharp
CaeriusNetBuilder.Create(builder)
    .WithAspireSqlServer("sqlserver")
    .WithAspireRedis()
    .WithTelemetryOptions(new CaeriusTelemetryOptions
    {
        // Include parameter names AND values in caerius.sp.parameters.
        // ⚠ Enable only outside production — values may contain PII
        //   (names, emails, tokens, monetary amounts, etc.).
        CaptureParameterValues = true
    })
    .Build();
```

| Option | Type | Default | Description |
|---|---|---|---|
| `CaptureParameterValues` | `bool` | `false` | When `true`, the `caerius.sp.parameters` tag shows `@name=value` pairs instead of just `@name`. TVP values are always shown as `[TVP]`. |

::: warning Production guidance
`CaptureParameterValues = true` is convenient in staging or development to correlate a trace with the exact parameters that produced it. In production, parameter values can contain sensitive data (user IDs, emails, amounts …) and should generally **not** be emitted to a shared telemetry backend.
:::

## Tracing & telemetry {#tracing-telemetry}

CaeriusNet emits OpenTelemetry-compatible signals through the BCL primitives — no OpenTelemetry SDK package is added to the library itself. Consumers (typically the Aspire `ServiceDefaults` project) opt in by registering the source and meter:

```csharp
// ServiceDefaults/Extensions.cs
using CaeriusNet.Telemetry;

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(CaeriusDiagnostics.SourceName))   // "CaeriusNet"
    .WithMetrics(m => m.AddMeter (CaeriusDiagnostics.SourceName));  // "CaeriusNet"
```

When no listener is subscribed, no allocation is performed.

### Spans

Every Stored Procedure call creates an `Activity` of `ActivityKind.Client` named `SP {schema}.{procedure}`. Failures set `ActivityStatusCode.Error` and attach the SQL exception via `Activity.AddException`.

| Tag | Description |
|---|---|
| `db.system` | Always `mssql` (OpenTelemetry semantic convention) |
| `db.operation` | The calling command (`FirstQueryAsync`, `QueryMultipleImmutableArrayAsync`, `ExecuteNonQueryAsync`, …) |
| `db.statement` | `{schema}.{procedure}` |
| `caerius.sp.schema` | Schema of the Stored Procedure |
| `caerius.sp.name` | Name of the Stored Procedure |
| `caerius.sp.parameters` | Comma-separated parameter names (e.g. `@id,@tvp`); shows `@name=value` when `CaptureParameterValues = true`. TVP values always render as `[TVP]`. |
| `caerius.sp.command` | Same as `db.operation` (kept for filter convenience) |
| `caerius.tvp.used` | `true` when at least one TVP is attached |
| `caerius.tvp.type_name` | TVP type name (e.g. `dbo.tvp_int`); comma-separated when several TVPs are used |
| `caerius.resultset.multi` | `true` when more than one result set is requested |
| `caerius.resultset.expected_count` | Number of result sets requested (1 by default; 2/3/4/5 for the multi-RS overloads) |
| `caerius.cache.tier` / `caerius.cache.hit` | Set on the active span when a cache lookup happens during the call |
| `caerius.tx` | `true` when the call runs inside an `ICaeriusNetTransaction` |
| `caerius.rows_returned` / `caerius.rows_affected` | Set on success |

### Metrics

Four instruments are exposed by the `CaeriusNet` meter, all tagged with the same `caerius.sp.*` dimensions as the spans:

| Instrument | Type | Unit | Purpose |
|---|---|---|---|
| `caerius.sp.duration` | Histogram | ms | Stored Procedure execution duration |
| `caerius.sp.executions` | Counter | calls | Number of executions started (success or failure) |
| `caerius.sp.errors` | Counter | calls | Number of executions that failed with a SQL error |
| `caerius.cache.lookups` | Counter | lookups | Cache lookups, tagged with `caerius.cache.tier` (`Frozen` / `InMemory` / `Redis`) and `caerius.cache.hit` (`true` / `false`) |

When a cache hit short-circuits the SQL call, **no DB span is created** and only `caerius.cache.lookups{hit=true}` is emitted — the Aspire dashboard accurately shows the database was not contacted.

## Transaction tracing {#transaction-tracing}

Every `ICaeriusNetTransaction` scope emits a parent **`TX` span** (kind = Internal) that wraps all child SP spans. This produces a single cohesive trace in the Aspire dashboard instead of one orphaned span per command:

```text
TX  (kind=Internal, caerius.tx.isolation_level=ReadCommitted, caerius.tx.outcome=committed)
├── SP Users.usp_Create_User  (kind=Client, caerius.tx=true)
└── SP Users.usp_Create_Order (kind=Client, caerius.tx=true)
```

| Tag | Description |
|---|---|
| `caerius.tx.isolation_level` | The SQL Server isolation level (e.g. `ReadCommitted`) |
| `caerius.tx.outcome` | `committed`, `rolled-back`, `auto-rollback`, `poisoned-auto-rollback`, `commit-failed`, `rollback-failed` |

::: tip SQL-side rollback in the dashboard
A Stored Procedure that wraps its own `BEGIN TRY / BEGIN CATCH` rolls back internally and re-throws, which surfaces as a `CaeriusNetSqlException`. The corresponding SP span is tagged `ActivityStatusCode.Error` — this is **expected** and intentional, not a CaeriusNet bug.
:::

---

**Next:** [API Reference](/documentation/api) — full surface of all public types and methods.
